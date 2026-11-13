// Run in Play Mode from the isolated NavigationPulseReview scene via Pipeline run_script.
// Two real NGO managers communicate over loopback UDP in one Editor process.
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CurtainCall.UI.Wayfinding;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using CurtainCall.UI.SceneIntegration;
using Object = UnityEngine.Object;

public static class NavigationWireReview
{
    [Serializable] public class State { public string key, scene; public Vector3[] points; public Color color; public float speed, emphasis; public double phase, anchor; public NavigationAnchor startAnchor,goalAnchor; }
    [Serializable] public class Batch { public State[] routes; }
    [Serializable] public class Request { public string id, scene; public bool valid, remove; public Vector3 start, goal; public Color color=Color.magenta; public float speed=2,emphasis=.6f; }
    static Batch received;
    static NavigationService receiver;
    const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static async Task Until(Func<bool> ready, string message, int seconds=15)
    { double end=Time.realtimeSinceStartupAsDouble+seconds; while(!ready()&&Time.realtimeSinceStartupAsDouble<end)await Task.Delay(50);Check(ready(),message); }
    static NetworkManager Manager(string name,ushort port)
    {
        var go=new GameObject(name);var transport=go.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1",port,"127.0.0.1");
        var manager=go.AddComponent<NetworkManager>();
        manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ForceSamePrefabs=false,ConnectionApproval=false};
        return manager;
    }
    static void Receive(ulong sender,FastBufferReader reader)
    {
        reader.ReadValueSafe(out string json);
        // Unity JsonUtility cannot instantiate the ephemeral run_script DTO types. Production types are compiled normally.
        received=Newtonsoft.Json.JsonConvert.DeserializeObject<Batch>(json);
        using var writer=new FastBufferWriter(FastBufferWriter.GetWriteSize(json),Allocator.Temp);writer.WriteValueSafe(json);
        using var copy=new FastBufferReader(writer,Allocator.Temp);
        typeof(NavigationService).GetMethod("ReceiveState",Private).Invoke(receiver,new object[]{sender,copy});
    }
    static void Send(NetworkManager client,Request request)
    {
        string json=Newtonsoft.Json.JsonConvert.SerializeObject(new {request.id,request.scene,request.valid,request.remove,
            start=new{x=request.start.x,y=request.start.y,z=request.start.z},goal=new{x=request.goal.x,y=request.goal.y,z=request.goal.z},
            color=new{r=request.color.r,g=request.color.g,b=request.color.b,a=request.color.a},request.speed,request.emphasis});
        using var writer=new FastBufferWriter(FastBufferWriter.GetWriteSize(json),Allocator.Temp);writer.WriteValueSafe(json);
        client.CustomMessagingManager.SendNamedMessage("CurtainCall.Navigation.Request.V1",0,writer,NetworkDelivery.ReliableFragmentedSequenced);
    }
    public static async Task<string> Run()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Navigation test floor";floor.transform.position=new Vector3(300,-.5f,0);floor.transform.localScale=new Vector3(30,1,30);
        var endpoint=new GameObject("Moving endpoint");endpoint.transform.position=new Vector3(300,0,8);
        Physics.SyncTransforms();NavigationService.EnsureExists().InvalidatePaths();
        NavigationHandle local=null,shared=null;NetworkManager host=null,client=null;NetworkConfig previousConfig=null;
        GameObject networkPrefab=null,spawned=null;
        var receiverRoot=new GameObject("Isolated production receiver");receiverRoot.SetActive(false);
        receiver=receiverRoot.AddComponent<NavigationService>();
        try
        {
            local=WorldNavigation.Show(endpoint,start:new Vector3(300,0,-8));
            await Until(()=>local.IsVisible,"Local path never became visible.");
            endpoint.transform.position=new Vector3(305,0,7);
            await Until(()=>local.Path.Length>0&&Vector3.Distance(local.Path.Last(),endpoint.transform.position)<.5f,"Moving target was not followed.");
            local.SetAppearance(Color.yellow,0,0);
            await Until(()=>!local.IsVisible,"Zero emphasis must hide.");
            local.SetAppearance(Color.magenta,2,.6f);
            await Until(()=>local.IsVisible,"Appearance change must restore visibility.");
            local.SetAppearance(Color.yellow,0,1);await Task.Delay(150);
            var localPulse=NavigationService.EnsureExists().GetComponentInChildren<NavigationPulseSequence>();
            float frozen=localPulse.LastEvaluatedSeconds;await Task.Delay(450);
            Check(Math.Abs(frozen-localPulse.LastEvaluatedSeconds)<.01,"Zero speed must freeze phase.");
            host=NetworkSessionManager.Instance?.NetworkManager;
            if(host==null)host=Manager("Navigation wire host",17941);
            else
            {
                Check(!host.IsListening,"Review requires an offline session.");previousConfig=host.NetworkConfig;
                var transport=host.GetComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",17941,"127.0.0.1");
                host.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ForceSamePrefabs=false,ConnectionApproval=false};
            }
            Check(host.StartHost(),"Host failed.");
            shared=WorldNavigation.Show(new Vector3(300,0,8),start:new Vector3(300,0,-8),synchronize:true,color:Color.yellow,animationSpeed:2,emphasis:.7f);
            await Until(()=>shared.IsVisible,"Host shared route failed.");
            client=Manager("Navigation wire late client",17941);Check(client.StartClient(),"Client failed.");
            await Until(()=>client.IsConnectedClient,"Client handshake failed.");
            typeof(NavigationService).GetField("manager",Private).SetValue(receiver,client);
            client.CustomMessagingManager.RegisterNamedMessageHandler("CurtainCall.Navigation.State.V1",Receive);
            await Until(()=>client.IsConnectedClient&&received?.routes?.Length==1,"Late join snapshot missing or local route leaked.");
            var remoteRoutes=(System.Collections.IDictionary)typeof(NavigationService).GetField("shared",Private).GetValue(receiver);
            Check(remoteRoutes.Count==1,"Production ReceiveState did not accept the snapshot.");
            Check(received.routes[0].points.SequenceEqual(shared.Path),"Wire path differs from host.");
            Check(received.routes[0].color==Color.yellow&&Math.Abs(received.routes[0].speed-2)<.01&&Math.Abs(received.routes[0].emphasis-.7)<.01,"Wire style differs.");
            var state=received.routes[0];double phase=state.phase+Math.Max(0,host.ServerTime.Time-state.anchor)*state.speed;
            var service=Object.FindFirstObjectByType<NavigationService>();
            Check(service.GetComponentsInChildren<NavigationPulseSequence>().Any(p=>Math.Abs(p.LastEvaluatedSeconds-phase)<.15),"Shared phase is not server-clock driven.");
            var request=new Request{id=Guid.NewGuid().ToString("N"),scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,valid=true,start=new Vector3(295,0,-6),goal=new Vector3(304,0,7)};
            Send(client,request);
            await Until(()=>received.routes.Any(r=>r.key.EndsWith(request.id)&&r.points.Length>=2),"Client-origin route was not computed by host.");
            state=received.routes.Single(r=>r.key.EndsWith(request.id));
            Check(Vector3.Distance(state.points[0],request.start)<.4f,"Shared start must be requester's start.");
            request.remove=true;Send(client,request);
            await Until(()=>received.routes.Length==1,"Client removal was not broadcast.");

            // A spawned object must resolve to the CLIENT's object, not the host instance or a guessed scene object.
            networkPrefab=new GameObject("Navigation network target prefab");networkPrefab.SetActive(false);
            var prefabObject=networkPrefab.AddComponent<NetworkObject>();
            typeof(NetworkObject).GetField("GlobalObjectIdHash",Private).SetValue(prefabObject,3933122101u);
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="Target visual";visual.transform.SetParent(networkPrefab.transform,false);
            Object.Destroy(visual.GetComponent<Collider>());
            networkPrefab.SetActive(true);
            host.AddNetworkPrefab(networkPrefab);client.AddNetworkPrefab(networkPrefab);
            spawned=Object.Instantiate(networkPrefab);spawned.transform.position=new Vector3(302,0,6);spawned.SetActive(true);
            var networkObject=spawned.GetComponent<NetworkObject>();networkObject.Spawn();
            await Until(()=>client.SpawnManager.SpawnedObjects.ContainsKey(networkObject.NetworkObjectId),"Target did not spawn on client.");
            var remote=client.SpawnManager.SpawnedObjects[networkObject.NetworkObjectId];
            Check(remote!=networkObject,"Expected a separate client object instance.");
            shared.Update(spawned.transform.GetChild(0),start:new Vector3(300,0,-8),synchronize:true,color:Color.cyan);
            await Until(()=>received.routes.Length==1&&received.routes[0].goalAnchor?.networked==true,"Network endpoint identity missing from snapshot.");
            var route=remoteRoutes.Values.Cast<object>().Single();
            var remoteOutline=(SilhouetteOutline)route.GetType().GetField("goalOutline",Private|System.Reflection.BindingFlags.Public).GetValue(route);
            Check(remoteOutline!=null&&remoteOutline.Target==remote.transform.GetChild(0),$"Receiver did not outline its own corresponding target: outline={remoteOutline?.Target?.name}, remoteActive={remote.gameObject.activeInHierarchy}, visualActive={remote.transform.GetChild(0).gameObject.activeInHierarchy}, children={remote.transform.childCount}, anchor={Newtonsoft.Json.JsonConvert.SerializeObject(received.routes[0].goalAnchor)}, corners={received.routes[0].points.Length}.");
            var remotePulse=(NavigationPulseSequence)route.GetType().GetField("pulse",Private|System.Reflection.BindingFlags.Public).GetValue(route);
            Check(remotePulse.DestinationBeaconHidden,"Object goal must hide the static beacon remotely.");
            remotePulse.EvaluateAt(remotePulse.TravelSeconds+.1f);Check(remotePulse.ArrivalEcho.Opacity>0,"Remote arrival wave was lost.");

            shared.Dispose();await Until(()=>received.routes.Length==0,"Shared disposal was not broadcast.");
            Check(remoteRoutes.Count==0,"Production receiver did not remove the route.");
            Object.Destroy(endpoint);await Until(()=>!local.IsVisible,"Destroyed target must hide.");
            local.Dispose();local.Dispose();
            string report="PASS: local moving object; emphasis hide/restore; zero-speed freeze; two NGO managers over UDP; late join; production ReceiveState/ApplySnapshot; local route isolation; exact shared corners/style; server-clock phase; client-origin start; spawned object/subtransform identity resolved on client; remote outline/static beacon hidden/arrival wave; shared removal; destroyed target; idempotent disposal. Same-process network test with an inactive production receiver; not a multi-machine latency test.";
            File.WriteAllText("Tasks/Active/Task-20261113-122-rehearsal-navigation-art/Captures/V2/navigation-wire-validation.txt",report);
            return report;
        }
        finally
        {
            local?.Dispose();shared?.Dispose();
            if(client!=null){client.Shutdown();await Task.Delay(300);Object.Destroy(client.gameObject);}
            if(host!=null){host.Shutdown();if(previousConfig!=null){await Task.Delay(200);host.NetworkConfig=previousConfig;}else Object.Destroy(host.gameObject);}
            Object.Destroy(floor);if(endpoint!=null)Object.Destroy(endpoint);
            Object.Destroy(receiverRoot);receiver=null;
            if(networkPrefab!=null)Object.Destroy(networkPrefab);if(spawned!=null)Object.Destroy(spawned);
        }
    }
}
