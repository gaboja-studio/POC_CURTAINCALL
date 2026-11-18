using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>Three real NGO/UTP peers in one process: remote owner, server and observing client.</summary>
    public sealed class RescueNetworkPlayTests
    {
        NetworkManager host,owner,observer;
        GameObject floor;
        const BindingFlags Instance=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
        static object Call(object value,string method,params object[] args)=>value.GetType().GetMethods(Instance).First(m=>m.Name==method && m.GetParameters().Length==args.Length).Invoke(value,args);
        static Component Player(NetworkObject obj)=>obj.GetComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
        static Component Mover(NetworkObject obj)=>obj.GetComponent(T("CurtainCall.Player.PlayerMover"));
        static object Get(object value,string name)=>value.GetType().GetProperty(name).GetValue(value);
        NetworkManager Peer(string name,ushort port,GameObject prefab)
        {
            var root=new GameObject(name);var manager=root.AddComponent<NetworkManager>();var transport=root.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1",port);
            manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=false,TickRate=30};
            manager.AddNetworkPrefab(prefab);return manager;
        }
        static IEnumerator Until(Func<bool> condition,float seconds=8)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while(!condition() && Time.realtimeSinceStartup<until)yield return null;
            Assert.IsTrue(condition(),"Timed out waiting for real network peers");
        }

        [UnityTest,Category("PerformanceEquipmentNetwork")]
        public IEnumerator RemoteOwnerPullIsVisibleOnServerAndObserverWithoutTeleport()
        {
            Assert.IsTrue(NetworkManager.Singleton==null || !NetworkManager.Singleton.IsListening);
            var prefab=Resources.Load<GameObject>("Prefabs/Characters/Players/Player");Assert.NotNull(prefab);
            ushort port;
            using(var socket=new System.Net.Sockets.UdpClient(0))port=(ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint).Port;
            host=Peer("Rescue host",port,prefab);host.SetSingleton();Assert.IsTrue(host.StartHost());
            owner=Peer("Rescued owner",port,prefab);Assert.IsTrue(owner.StartClient());
            observer=Peer("Rescue observer",port,prefab);Assert.IsTrue(observer.StartClient());
            yield return Until(()=>owner.IsConnectedClient && observer.IsConnectedClient);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(500,39.5f,500);floor.transform.localScale=new Vector3(12,1,12);
            var caster=host.SpawnManager.InstantiateAndSpawn(prefab.GetComponent<NetworkObject>(),NetworkManager.ServerClientId);
            var target=host.SpawnManager.InstantiateAndSpawn(prefab.GetComponent<NetworkObject>(),owner.LocalClientId);
            yield return Until(()=>owner.SpawnManager.SpawnedObjects.ContainsKey(target.NetworkObjectId) && observer.SpawnManager.SpawnedObjects.ContainsKey(target.NetworkObjectId));
            yield return new WaitForSecondsRealtime(.4f);
            var localTarget=owner.SpawnManager.SpawnedObjects[target.NetworkObjectId];
            var remoteTarget=observer.SpawnManager.SpawnedObjects[target.NetworkObjectId];
            // Remote replicas share a test physics world; only authoritative capsules participate.
            foreach(var peer in new[]{host,owner,observer})
                foreach(var obj in peer.SpawnManager.SpawnedObjectsList)
                    if(!obj.IsOwner)foreach(var c in obj.GetComponentsInChildren<Collider>())c.enabled=false;
            Call(Mover(caster),"SetStartPose",new Vector3(500,40.035f,500),Quaternion.identity,true);
            Call(Mover(localTarget),"SetStartPose",new Vector3(500,41.2f,502.7f),Quaternion.identity,true);
            Call(Mover(localTarget),"LaunchBounce",1f);
            Mover(localTarget).GetType().GetProperty("Simulated").SetValue(Mover(localTarget),false);
            yield return new WaitForSecondsRealtime(.25f);
            Vector3 start=localTarget.transform.position,landing=new Vector3(500,40.035f,501.1f);
            Assert.IsNull(Player(remoteTarget).GetComponent(T("CurtainCall.MainStage.CheerPoints.RescueTargetIndicator")),"Observer must not receive another player's target HUD.");
            int teleports=0;Action teleported=()=>teleports++;
            Mover(localTarget).GetType().GetEvent("Teleported").AddEventHandler(Mover(localTarget),teleported);
            Call(Player(target),"StartServerRescuePull",Player(caster),landing);
            int hostIntermediate=0,observerIntermediate=0;float until=Time.realtimeSinceStartup+1.8f;
            while(Time.realtimeSinceStartup<until)
            {
                bool Between(Vector3 p)=>Vector3.Distance(p,start)>.15f && Vector3.Distance(p,landing)>.2f && Vector3.Distance(p,start)<3;
                if(Between(target.transform.position))hostIntermediate++;
                if(Between(remoteTarget.transform.position))observerIntermediate++;
                yield return null;
            }
            Assert.Greater(hostIntermediate,2,"The server receives intermediate owner poses.");
            Assert.Greater(observerIntermediate,2,"The observing client receives intermediate owner poses.");
            Assert.Less(Vector3.Distance(landing,localTarget.transform.position),.2f);
            Assert.Less(Vector3.Distance(landing,target.transform.position),.2f);
            Assert.Less(Vector3.Distance(landing,remoteTarget.transform.position),.2f);
            Assert.AreEqual(0,teleports,"Rescue must never call Relocate/NetworkTransform.Teleport.");
            Assert.IsFalse((bool)Get(Player(target),"IsBeingRescued"));
            Assert.IsTrue((bool)Get(Mover(localTarget),"ControlEnabled"));

            // A rescuer dying mid-pull must release the remote owner's movement lock.
            Call(Mover(localTarget),"SetStartPose",start,Quaternion.identity,true);
            Call(Mover(localTarget),"LaunchBounce",1f);
            Mover(localTarget).GetType().GetProperty("Simulated").SetValue(Mover(localTarget),false);
            yield return new WaitForSecondsRealtime(.2f);
            Call(Player(target),"StartServerRescuePull",Player(caster),landing);
            yield return new WaitForSecondsRealtime(.2f);
            Call(Player(caster),"ServerSetState",Enum.Parse(T("CurtainCall.Network.PlayerSync.PlayerState"),"Fallen"),
                Enum.Parse(T("CurtainCall.Network.PlayerSync.NetworkPlayer+DeathOrigin"),"Debug"));
            yield return Until(()=>!(bool)Get(Player(target),"IsBeingRescued") && !(bool)Get(Player(localTarget),"IsBeingRescued"),2);
            Assert.IsTrue((bool)Get(Mover(localTarget),"ControlEnabled"));
            Assert.IsTrue(((Behaviour)localTarget.GetComponent(T("CurtainCall.Player.PlayerController"))).enabled);
        }
        [UnityTearDown]
        public IEnumerator Close()
        {
            foreach(var peer in new[]{observer,owner,host})if(peer)peer.Shutdown();
            yield return null;yield return null;
            foreach(var peer in new[]{observer,owner,host})if(peer)Object.Destroy(peer.gameObject);
            if(floor)Object.Destroy(floor);
            yield return null;
        }
    }
}
