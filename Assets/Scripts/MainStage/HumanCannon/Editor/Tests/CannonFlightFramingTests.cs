using System.Reflection;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    [Category("CommonProgressionRegression")]
    public sealed class CannonFlightFramingTests
    {
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Fields).SetValue(o,v);
        [TestCase(-1f, 0f, false), TestCase(1f, 0f, false), TestCase(-1f, 90f, false), TestCase(1f, 90f, false)]
        [TestCase(-1f, 0f, true), TestCase(1f, 0f, true), TestCase(-1f, 90f, true), TestCase(1f, 90f, true)]
        public void FlyingPilotRoutesSideInputToSharedAirCorrection(float side, float yaw, bool pendingTransportReset)
        {
            var root = new GameObject("Cannon input fixture"); root.SetActive(false);
            var actor = new GameObject("Cannon input actor"); actor.SetActive(false);
            var previousRoute = HumanCannonRouteSystem.Current;
            // Binding tests can request release in the same EditMode frame.
            // This fixture supplies direct flight input, so restore that transient state afterward.
            const BindingFlags gateFlags = BindingFlags.Static | BindingFlags.NonPublic;
            var rearming = typeof(InputGate).GetField("rearming", gateFlags);
            var blockedFrame = typeof(InputGate).GetField("blockedFrame", gateFlags);
            var previousRearming = rearming.GetValue(null);
            var previousBlockedFrame = blockedFrame.GetValue(null);
            try
            {
                Assert.IsFalse(InputGate.IsRequested, InputGate.Describe());
                rearming.SetValue(null, false);
                blockedFrame.SetValue(null, -1);
                if (pendingTransportReset)
                {
                    var route = root.AddComponent<HumanCannonRouteSystem>();
                    typeof(HumanCannonRouteSystem).GetProperty("Current").SetValue(null, route);
                    ((RouteResetBarrier)typeof(HumanCannonRouteSystem).GetField("resetBarrier", Fields).GetValue(route)).Begin();
                }
                var system = root.AddComponent<HumanCannonSystem>();
                var player = actor.AddComponent<NetworkPlayer>();
                var pilot = actor.AddComponent<HumanCannonPilot>();
                Set(pilot, "player", player); Set(pilot, "system", system);
                typeof(HumanCannonPilot).GetProperty("IsFlying", Fields).SetValue(pilot, true);
                var read = typeof(HumanCannonPilot).GetMethod("ReadInput", Fields);
                var air = (CannonAirControl)typeof(HumanCannonPilot).GetField("airControl", Fields).GetValue(pilot);
                Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                float speed = CurtainCall.Settings.GameSettings.Base.AirControl.Speed;
                var velocity = forward * 7.0704784f + Vector3.up * 12;
                Vector3 movement = Vector3.zero;
                Assert.IsFalse(InputGate.IsBlocked, InputGate.Describe());
                for (int i = 0; i < 60; i++)
                {
                    read.Invoke(pilot, new object[] { new CurtainCall.Player.PlayerInputFrame { Posture = side } });
                    movement += air.ConsumePoleVelocity(Time.frameCount, false, forward, speed, velocity,
                        7.0704784f + speed, 1f / 60) / 60;
                }
                Assert.That(Vector3.Dot(movement, right) * side, Is.InRange(speed * .85f, speed));
                Assert.That(movement.y, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(actor); Object.DestroyImmediate(root);
                typeof(HumanCannonRouteSystem).GetProperty("Current").SetValue(null, previousRoute);
                rearming.SetValue(null, previousRearming);
                blockedFrame.SetValue(null, previousBlockedFrame);
            }
        }

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
