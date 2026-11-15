using System;
using System.Linq;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.UI.Wayfinding
{
    public sealed partial class NavigationService
    {
        const string RequestMessage="CurtainCall.Navigation.Request.V1",StateMessage="CurtainCall.Navigation.State.V1";
        const int MaxPayload=256*1024,MaxRoutes=32,MaxPoints=256;
        [Serializable] sealed class Batch { public Snapshot[] routes; }
        NetworkManager manager;
        CustomMessagingManager messaging;
        bool boundAsServer;
        float lastSnapshot;
        bool IsServer=>manager!=null&&manager.IsListening&&manager.IsServer;
        double ServerClock=>manager!=null&&manager.IsListening?manager.ServerTime.Time:Time.unscaledTimeAsDouble;
        string OwnKey(string id)=>(manager!=null?manager.LocalClientId:0)+"/"+id;
        bool PeerConnected(ulong peer)=>manager!=null&&manager.ConnectedClients.ContainsKey(peer);

        void BindNetwork()
        {
            // Do not create a network session merely because a local guide was requested.
            var next=NetworkSessionManager.Instance!=null?NetworkSessionManager.Instance.NetworkManager:NetworkManager.Singleton;
            if(next==null||!next.IsListening)next=null;
            bool server=next!=null&&next.IsServer;
            if(next==manager&&server==boundAsServer)
            {
                if(!server&&manager!=null&&Time.unscaledTime-lastSnapshot>3)
                    foreach(var r in shared.Values)
                    {r.showing=false;ReleaseOutlines(r);if(r.view!=null){r.view.Hide();r.pulse.SetEndpointPresentation(false,false,Vector3.zero);}}
                return;
            }
            UnbindNetwork();
            foreach(var r in shared.Values)DestroyRoute(r);shared.Clear();
            manager=next;boundAsServer=server;lastSnapshot=Time.unscaledTime;
            if(manager==null)return;
            messaging=manager.CustomMessagingManager;
            messaging.RegisterNamedMessageHandler(server?RequestMessage:StateMessage,server?ReceiveRequest:ReceiveState);
            nextRefresh=nextNetwork=0;
        }
        void UnbindNetwork()
        {
            if(messaging!=null)messaging.UnregisterNamedMessageHandler(boundAsServer?RequestMessage:StateMessage);
            messaging=null;manager=null;
        }
        void SendRequest(Request request)
        {
            if(manager==null||!manager.IsListening||messaging==null)return; // queued handles are retried after the session becomes ready
            if(IsServer)AcceptRequest(manager.LocalClientId,request);
            else Send(RequestMessage,JsonUtility.ToJson(request),NetworkManager.ServerClientId);
        }
        void ReceiveRequest(ulong sender,FastBufferReader reader)
        {
            if(!IsServer||!PeerConnected(sender)||reader.Length>4096)return;
            try {reader.ReadValueSafe(out string json);AcceptRequest(sender,JsonUtility.FromJson<Request>(json));}
            catch(Exception e) when(e is ArgumentException||e is OverflowException||e is IndexOutOfRangeException){Debug.LogWarning("[Navigation] Invalid request ignored.");}
        }
        void AcceptRequest(ulong sender,Request request)
        {
            if(request==null||request.id==null||request.id.Length!=32||!Guid.TryParseExact(request.id,"N",out _))return;
            string key=sender+"/"+request.id;
            if(request.remove)
            {if(shared.Remove(key,out var old))DestroyRoute(old);Publish();return;}
            if(request.scene==null||request.scene.Length>512||!NavigationTarget.Finite(request.start)||!NavigationTarget.Finite(request.goal))return;
            if(request.startAnchor?.Valid==false||request.goalAnchor?.Valid==false)return;
            if(!shared.TryGetValue(key,out var route))
            {
                if(shared.Count>=MaxRoutes)return;
                route=NewRoute(key,request,ServerClock);route.owner=sender;shared.Add(key,route);
            }
            UpdateRequest(route,request,ServerClock);
        }
        void Publish()
        {
            if(!IsServer||messaging==null)return;
            var batch=new Batch{routes=shared.Values.Select(r=>r.state).ToArray()};
            Send(StateMessage,JsonUtility.ToJson(batch),null);
        }
        void Send(string name,string json,ulong? peer)
        {
            int size=FastBufferWriter.GetWriteSize(json);if(size>MaxPayload)return;
            using var writer=new FastBufferWriter(size,Allocator.Temp);writer.WriteValueSafe(json);
            if(peer.HasValue)messaging.SendNamedMessage(name,peer.Value,writer,NetworkDelivery.ReliableFragmentedSequenced);
            else messaging.SendNamedMessageToAll(name,writer,NetworkDelivery.ReliableFragmentedSequenced);
        }
        void ReceiveState(ulong sender,FastBufferReader reader)
        {
            if(IsServer||sender!=NetworkManager.ServerClientId||reader.Length>MaxPayload)return;
            try {reader.ReadValueSafe(out string json);ApplySnapshot(JsonUtility.FromJson<Batch>(json));}
            catch(Exception e) when(e is ArgumentException||e is OverflowException||e is IndexOutOfRangeException){Debug.LogWarning("[Navigation] Invalid state ignored.");}
        }
        void ApplySnapshot(Batch batch)
        {
            if(batch?.routes==null||batch.routes.Length>MaxRoutes)return;
            // Validate the whole snapshot before mutating any live route.
            foreach(var state in batch.routes)
                if(state==null||string.IsNullOrEmpty(state.key)||state.key.Length>64||state.points==null||state.points.Length>MaxPoints
                    ||state.points.Any(p=>!NavigationTarget.Finite(p))||!double.IsFinite(state.anchor)||!double.IsFinite(state.phase)
                    ||state.startAnchor?.Valid==false||state.goalAnchor?.Valid==false)return;
            lastSnapshot=Time.unscaledTime;
            var keys=batch.routes.Select(s=>s.key).ToHashSet();
            foreach(var pair in shared.ToArray())if(!keys.Contains(pair.Key)){DestroyRoute(pair.Value);shared.Remove(pair.Key);}
            foreach(var state in batch.routes)
            {
                if(!shared.TryGetValue(state.key,out var route))shared[state.key]=route=new Route();
                state.speed=SafeSpeed(state.speed);state.emphasis=SafeEmphasis(state.emphasis);state.color=SafeColor(state.color);state.interval=SafeInterval(state.interval);state.follow=SafeFollow(state.follow);state.startGap=SafeGap(state.startGap);
                route.state=state;RenderPath(route);
            }
        }
    }
}
