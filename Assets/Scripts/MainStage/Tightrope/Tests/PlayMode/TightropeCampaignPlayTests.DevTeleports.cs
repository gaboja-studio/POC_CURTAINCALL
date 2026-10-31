using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        /// <summary>Task-089: F9 치트의 외줄 지점(출발·구간 시작·도착)이 나오고, 구간 시작 지점은 실제 쓸 수 있는 줄 위다.</summary>
        [UnityTest]
        public IEnumerator DevCheats_ListTightropePoints_AndTeleportOntoUsableRope()
        {
            var devCache = GameType("CurtainCall.Dev.DevMode").GetField("_enabled", BindingFlags.NonPublic | BindingFlags.Static);
            object before = devCache.GetValue(null);
            devCache.SetValue(null, true);
            try
            {
                var cheats = GameType("CurtainCall.Dev.DevCheats");
                var pointType = GameType("CurtainCall.Dev.DevCheats+TeleportPoint");
                var points = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pointType));
                cheats.GetMethod("CollectTeleportPoints").Invoke(null, new object[] { player, points });
                string Label(object p) => (string)pointType.GetField("Label").GetValue(p);
                var labels = points.Cast<object>().Select(Label).ToArray();
                CollectionAssert.Contains(labels, "외줄 · 출발");
                CollectionAssert.Contains(labels, "외줄 · 도착");
                var sections = points.Cast<object>().Where(p => Label(p).StartsWith("외줄 · ") && Label(p).EndsWith(" 시작")).ToArray();
                Assert.IsNotEmpty(sections, string.Join(", ", labels));
                foreach (var point in sections)
                {
                    var position = (Vector3)pointType.GetField("Position").GetValue(point);
                    float distance = (float)Call(course, "GetDistance", position);
                    float side = (float)Call(course, "GetSide", position);
                    int lane = (int)Call(Get(course, "Layout"), "GetLaneSlotAt", side);
                    Assert.IsTrue((bool)Call(course, "IsLaneUsable", lane, distance), $"{Label(point)}: 끊긴 줄 위로 보내지 않는다");
                    var teleport = new object[] { player, position, Quaternion.identity, null };
                    Assert.IsTrue((bool)cheats.GetMethod("Teleport").Invoke(null, teleport), (string)teleport[3]);
                    yield return null;
                    var flat = Vector3.ProjectOnPlane(player.transform.position - position, Vector3.up);
                    Assert.Less(flat.magnitude, .5f, Label(point));
                }
                var start = points.Cast<object>().First(p => Label(p) == "외줄 · 출발");
                var back = new object[] { player, (Vector3)pointType.GetField("Position").GetValue(start), Quaternion.identity, null };
                Assert.IsTrue((bool)cheats.GetMethod("Teleport").Invoke(null, back), (string)back[3]);
            }
            finally { devCache.SetValue(null, before); }
        }
    }
}
