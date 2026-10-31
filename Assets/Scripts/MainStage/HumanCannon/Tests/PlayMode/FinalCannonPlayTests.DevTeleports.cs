using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        /// <summary>Task-089: F9 치트의 인간 대포 지점(대포별 탑승 위치·최종 목적지)이 나오고 그 자리로 옮긴다.</summary>
        [UnityTest]
        public IEnumerator DevCheats_ListCannonPoints_AndTeleportToBoardingAndDestination()
        {
            var devCache = GameType("CurtainCall.Dev.DevMode").GetField("_enabled", BindingFlags.NonPublic | BindingFlags.Static);
            object before = devCache.GetValue(null);
            devCache.SetValue(null, true);
            try
            {
                DisableEnhancementThreats();
                var cheats = GameType("CurtainCall.Dev.DevCheats");
                var pointType = GameType("CurtainCall.Dev.DevCheats+TeleportPoint");
                var points = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pointType));
                cheats.GetMethod("CollectTeleportPoints").Invoke(null, new object[] { player, points });
                string Label(object p) => (string)pointType.GetField("Label").GetValue(p);
                var labels = points.Cast<object>().Select(Label).ToArray();
                int stations = (int)Get(system, "StationCount");
                Assert.AreEqual(stations, labels.Count(l => l.StartsWith("대포 ") && l.EndsWith(" 탑승 위치")), string.Join(", ", labels));
                CollectionAssert.Contains(labels, "대포 · 최종 목적지");
                foreach (string label in new[] { "대포 1 탑승 위치", "대포 · 최종 목적지" })
                {
                    var point = points.Cast<object>().First(p => Label(p) == label);
                    var position = (Vector3)pointType.GetField("Position").GetValue(point);
                    var teleport = new object[] { player, position, Quaternion.identity, null };
                    Assert.IsTrue((bool)cheats.GetMethod("Teleport").Invoke(null, teleport), (string)teleport[3]);
                    yield return new WaitForSeconds(.3f);
                    Assert.Less(Vector3.ProjectOnPlane(player.transform.position - position, Vector3.up).magnitude, .6f, label);
                    Assert.Greater(player.transform.position.y, position.y - .6f, $"{label}: 바닥 아래로 빠지지 않는다");
                    Assert.AreEqual("Normal", Get(player, "State").ToString(), label);
                }
            }
            finally { devCache.SetValue(null, before); }
        }
    }
}
