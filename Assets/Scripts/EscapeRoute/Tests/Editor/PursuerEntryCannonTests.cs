using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    [Category("EscapePursuerComplete")]
    public sealed class PursuerEntryCannonTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const ulong Actor = 73;
        GameObject root;
        Component route, bypass, cannon, timer, landing;
        Component[] stations;
        PropertyInfo timerCurrent;
        object previousTimer;
        Vector3 landingFeet;

        static Type T(string fullName) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName)).First(t => t != null);
        static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
        static void Set(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, Flags);
            if (property != null) property.SetValue(target, value);
            else target.GetType().GetField(name, Flags).SetValue(target, value);
        }
        static object Call(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, Flags).Invoke(target, args);
        GameObject Node(string name, bool active = false)
        {
            var go = new GameObject(name);
            go.SetActive(active); go.transform.SetParent(root.transform, false);
            return go;
        }
        Array StationArray(IEnumerable<Component> source)
        {
            var values = source.ToArray();
            var array = Array.CreateInstance(T("CurtainCall.HumanCannon.HumanCannonStation"), values.Length);
            for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i);
            return array;
        }

        [SetUp]
        public void Setup()
        {
            root = new GameObject("Entry cannon landing regression") { hideFlags = HideFlags.DontSave };
            root.transform.position = new Vector3(10000, 10000, 10000);
            timerCurrent = T("CurtainCall.EscapeRoute.EscapeRouteTimer").GetProperty("Current");
            previousTimer = timerCurrent.GetValue(null);
            timer = Node("Isolated clock").AddComponent(T("CurtainCall.EscapeRoute.EscapeRouteTimer"));
            Set(timer, "startedAt", 100d); Set(timer, "synchronized", true); timerCurrent.SetValue(null, timer);
            cannon = Node("Inactive common cannon registry").AddComponent(T("CurtainCall.HumanCannon.HumanCannonSystem"));
            string[] names = { "03_Cannon", "Labyrinth_01_ParallelEntryCannon_2", "Labyrinth_01_ParallelEntryCannon_3",
                "Labyrinth_01_ParallelEntryCannon_4", "Later conveyor cannon", "Labyrinth_01_ParallelEntryCannon_2" };
            stations = names.Select((name, i) => {
                var node = Node(name); node.transform.localPosition = new Vector3(i * 5, 0, 0);
                return node.AddComponent(T("CurtainCall.HumanCannon.HumanCannonStation"));
            }).ToArray();
            Set(cannon, "stations", StationArray(stations));
            var floor = Node("Shared first wire", true);
            var box = floor.AddComponent<BoxCollider>(); box.center = Vector3.down * .2f; box.size = new Vector3(20, .4f, 2);
            landing = floor.AddComponent(T("CurtainCall.HumanCannon.HumanCannonLandingZone"));
            landingFeet = floor.transform.position;
            route = Node("Route departure observer").AddComponent(T("CurtainCall.EscapeRoute.EscapePursuerRoute"));
            bypass = Node("Legacy bypass observer").AddComponent(T("CurtainCall.EscapeRoute.UnderstageCannonBypass"));
            foreach (var observer in new[] { route, bypass })
            {
                Set(observer, "cannon", cannon); Set(observer, "firstCannon", stations[0]); Set(observer, "firstWire", landing);
                Set(observer, "additionalFirstCannons", StationArray(stations.Skip(1).Take(3)));
            }
        }

        [TearDown]
        public void Cleanup()
        {
            if (root) Object.DestroyImmediate(root);
            if (timerCurrent != null) timerCurrent.SetValue(null, previousTimer);
        }

        void LandAndExpect(int station, bool safe, Vector3 feet, bool expected)
        {
            foreach (var observer in new[] { route, bypass })
            {
                bool legacy = observer == bypass;
                Call(observer, legacy ? "OnLanding" : "Landed", Actor, station, 1u, 0u, feet, safe);
                var departed = (HashSet<ulong>)Get(observer, legacy ? "crossed" : "departed");
                Assert.That(departed.Contains(Actor), Is.EqualTo(expected), observer.GetType().Name);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void RegisteredEntranceCannonsRecognizeTheSameSafeFirstWire(int station)
            => LandAndExpect(station, true, landingFeet, true);

        [TestCase(4)] [TestCase(5)] [TestCase(-1)] [TestCase(6)]
        public void UnregisteredOrInvalidCannonsDoNotMarkDeparture(int station)
            => LandAndExpect(station, true, landingFeet, false); // Index 5 deliberately shares an allowed name, not its reference.

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void UnsafeLandingDoesNotMarkDeparture(int station)
            => LandAndExpect(station, false, landingFeet, false);

        [Test]
        public void SafeLandingOnAnotherSurfaceDoesNotMarkDeparture()
            => LandAndExpect(1, true, landingFeet + Vector3.forward * 4, false);

        [Test]
        public void DisabledFirstWireDoesNotMarkDeparture()
        {
            landing.GetComponent<BoxCollider>().enabled = false;
            LandAndExpect(1, true, landingFeet, false);
        }

        [TestCase(false)] [TestCase(true)]
        public void LegacySceneWithNoAdditionalArrayKeepsOnlyTheOriginalCannon(bool missingArray)
        {
            foreach (var observer in new[] { route, bypass })
                Set(observer, "additionalFirstCannons", missingArray ? null : StationArray(Array.Empty<Component>()));
            LandAndExpect(1, true, landingFeet, false);
            LandAndExpect(0, true, landingFeet, true);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void BypassRecognizesTheActualAdditionalCannonSeat(int station)
        {
            Call(cannon, "ResetSeats");
            var runtime = ((Array)Get(cannon, "seats")).GetValue(station);
            var state = Get(runtime, "State");
            var board = Enum.Parse(T("CurtainCall.HumanCannon.CannonAction"), "Board");
            Assert.That(Call(state, "TryApply", board, Actor, 0u), Is.True);
            var player = Node("Unspawned seat query actor").AddComponent(T("CurtainCall.Network.PlayerSync.NetworkPlayer"));
            Set(player, "OwnerClientId", Actor);
            Assert.That(Call(bypass, "AtGun", player), Is.True);
            Assert.That((Vector3)Call(bypass, "GunPosition", player), Is.EqualTo(stations[station].transform.position));
            Assert.That((Vector3)Call(bypass, "GunPosition", player), Is.Not.EqualTo(stations[0].transform.position));
        }
    }
}
