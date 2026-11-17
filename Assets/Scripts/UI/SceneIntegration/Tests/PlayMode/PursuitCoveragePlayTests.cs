using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// 2026-11-17 사용자 보고 구역을 실제 통합 대기실에서 추격한다: 숙소 환풍구→지하 계단, 계단실 경사로,
    /// 무대 장치실 화물 통로가 꺾이는 곳의 1.1m 난간(바닥으로 내려가지 않고 바로 넘기).
    /// </summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        static readonly (string name, Vector3 from, Vector3 to, float minY)[] CoverageRoutes = {
            ("vent-utility-stairs", new Vector3(22.8f, 2.32f, 71.2f), new Vector3(25.3f, -4.78f, 73.4f), float.NegativeInfinity),
            ("stairwell-flight-ramp", new Vector3(7.0f, .04f, 104.8f), new Vector3(3.3f, -4.96f, 102.0f), float.NegativeInfinity),
            ("cargo-northwest-rail", new Vector3(-58.6f, 5.64f, 74.0f), new Vector3(-53f, 5.64f, 78.6f), 5f),
            ("cargo-southeast-rail", new Vector3(-42f, 5.64f, 29.8f), new Vector3(-28.5f, 5.64f, 36f), 5f),
        };

        [UnityTest]
        public IEnumerator PursuitCoverage_StairsVentAndRailCornersReachTarget()
        {
            // 실제 게임이 쓰는 통합 대기실(굽힌 NavMesh 포함). 옛 추격 테스트 씬에는 NavMesh가 없다.
            WaitingVariantField.SetValue(WaitingSpaces, System.Enum.Parse(WaitingVariantField.FieldType, "Integrated"));
            yield return PursuitHost();
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance"); Call(run, "DebugSetRemaining", .01f);
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            float until = Time.time + 8;
            while ((float)Get(director, "Elapsed") < 3 && Time.time < until) yield return null;
            var rules = Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit");
            var stages = (System.Array)Field(rules, "Stages");
            Assert.IsTrue((bool)Call(director, "DebugSetStage", stages.Length - 1));
            yield return null; yield return null;
            ((Behaviour)director).enabled = false;
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var candidates = (IList)PursuitPrivate(director, "candidates");
            var agent = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var target = new GameObject("CoverageTargetFixture");
            var report = new System.Text.StringBuilder();
            int failures = 0;
            try {
                foreach (var route in CoverageRoutes) {
                    candidates.Clear(); Call(actor, "ResetActor");
                    agent.enabled = false; actor.transform.position = route.from;
                    target.transform.position = route.to; candidates.Add(target);
                    float minY = float.PositiveInfinity, closest = float.PositiveInfinity;
                    bool vaulted = false, reached = false;
                    until = Time.time + 30;
                    while (Time.time < until) {
                        Call(actor, "ServerTick", 600f, false);
                        yield return null;
                        var p = actor.transform.position;
                        minY = Mathf.Min(minY, p.y);
                        vaulted |= (bool)Get(actor, "IsVaulting");
                        var d = route.to - p; float flat = new Vector2(d.x, d.z).magnitude;
                        closest = Mathf.Min(closest, flat);
                        // 겹친 계단참(1.4m 위아래)을 도착으로 보지 않도록 같은 층만 인정한다.
                        if (flat < 1.6f && Mathf.Abs(d.y) < .6f) { reached = true; break; }
                    }
                    bool ok = reached && minY >= route.minY - .3f;
                    if (!ok) failures++;
                    report.AppendLine($"{route.name}: reached={reached} closest={closest:0.00} minY={minY:0.00} vaulted={vaulted} end={actor.transform.position} motion={Get(actor, "State")} behaviour={Get(actor, "Behaviour")} agent={agent.enabled}/{(agent.enabled && agent.isOnNavMesh)} activated={Get(actor, "IsActivated")} navFailed={Get(actor, "NavigationFailed")} target={actor.GetType().GetField("target", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(actor) != null}");
                }
            } finally { candidates.Clear(); Object.Destroy(target); }
            Debug.Log("[PursuitCoverage]\n" + report);
            Assert.AreEqual(0, failures, report.ToString());
        }
    }
}
