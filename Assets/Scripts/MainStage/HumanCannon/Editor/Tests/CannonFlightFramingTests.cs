using System.Reflection;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonFlightFramingTests
    {
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Fields).SetValue(o,v);
        [TestCase(false)] [TestCase(true)]
        public void AirborneProfileIsUsedOnlyInFlightAndDoesNotChangeTheSharedPreset(bool useOverride)
        {
            var root=new GameObject("Flight view fixture");root.SetActive(false);
            var actor=new GameObject("Flight view actor");actor.SetActive(false);
            var rig=new GameObject("Flight view rig");rig.SetActive(false);
            HumanCannonPilot pilot=null;CameraViewPreset authored=null;
            var free=Resources.Load<CameraViewPreset>("Settings/Camera/FreeThirdPerson");var distance=free.Distance;
            try
            {
                var sys=root.AddComponent<HumanCannonSystem>();
                if(useOverride){authored=ScriptableObject.CreateInstance<CameraViewPreset>();authored.Kind=CameraViewKind.FreeThirdPerson;authored.Distance=6.5f;authored.Height=8.5f;authored.TransitionDuration=.35f;Set(sys,"airborneView",authored);}
                var player=actor.AddComponent<NetworkPlayer>();pilot=actor.AddComponent<HumanCannonPilot>();actor.SetActive(true);
                var director=rig.AddComponent<CameraViewDirector>();director.SetLocalPlayer(player);
                Set(pilot,"player",player);Set(pilot,"system",sys);Set(pilot,"cannonView",director);
                var refresh=typeof(HumanCannonPilot).GetMethod("RefreshCannonView",Fields);
                var resolver=typeof(CameraViewDirector).GetField("requests",Fields).GetValue(director);
                CameraViewPreset Resolve(){object[] args={null,null,null};Assert.That(resolver.GetType().GetMethod("TryResolve").Invoke(resolver,args),Is.EqualTo(true));return (CameraViewPreset)args[1].GetType().GetField("Preset").GetValue(args[1]);}
                Set(pilot,"stationIndex",0);refresh.Invoke(pilot,null);var seated=Resolve();Assert.That(seated.Distance,Is.EqualTo(distance));
                Set(pilot,"stationIndex",-1);typeof(HumanCannonPilot).GetProperty("IsFlying",Fields).SetValue(pilot,true);
                refresh.Invoke(pilot,null);var airborne=Resolve();Assert.That(airborne.Distance,Is.EqualTo(useOverride?6.5f:distance));Assert.That(airborne.Kind,Is.EqualTo(CameraViewKind.FreeThirdPerson));
                if(useOverride){Assert.That(airborne,Is.Not.SameAs(authored));Assert.That(airborne.TransitionDuration,Is.EqualTo(.35f));}
                typeof(HumanCannonPilot).GetProperty("IsFlying",Fields).SetValue(pilot,false);refresh.Invoke(pilot,null);
                var walking=Resolve();Assert.That(walking.Distance,Is.EqualTo(distance));
                if(useOverride)Assert.That(walking.TransitionDuration,Is.EqualTo(.35f),"Landing returns smoothly to the ordinary view.");
                Assert.That(free.Distance,Is.EqualTo(distance));
            }
            finally
            {
                if(pilot!=null){foreach(var n in new[]{"seatedView","flightView","carryingView","airborneView"}){var v=typeof(HumanCannonPilot).GetField(n,Fields).GetValue(pilot) as Object;Set(pilot,n,null);if(v!=null)Object.DestroyImmediate(v);}}
                Object.DestroyImmediate(actor);Object.DestroyImmediate(rig);Object.DestroyImmediate(root);if(authored!=null)Object.DestroyImmediate(authored);
            }
        }
    }
}
