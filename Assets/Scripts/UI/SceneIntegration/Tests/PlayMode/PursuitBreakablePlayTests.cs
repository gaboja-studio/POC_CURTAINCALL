using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>
    /// 2026-11-17 사용자 요청: 부서지는 벽·상자·문을 부수기 전/후 모두 추격 경로를 새로 계산하고,
    /// 부수기 전 낮은 상자는 넘거나 올라설 수 있어야 한다. 실제 통합 대기실의 모든 대상을 디버그로 열어 비교한다.
    /// </summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        static readonly BindingFlags BreakFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        sealed class BreakCase
        {
            public string Id, Name; public Collider Blocker; public bool Low;
            public Vector3 Front, Back; public bool HasSides;
            public string Before, After;
        }

        [UnityTest]
        public IEnumerator PursuitBreakables_PathsRecomputeBeforeAndAfterOpening()
        {
            WaitingVariantField.SetValue(WaitingSpaces, System.Enum.Parse(WaitingVariantField.FieldType, "Integrated"));
            yield return PursuitHost();
            var run = Static("CurtainCall.Campaign.CampaignRun", "Instance"); Call(run, "DebugSetRemaining", .01f);
            var director = Find("CurtainCall.WaitingRoom.Pursuit.PursuitDirector");
            float until = Time.time + 8;
            while ((float)Get(director, "Elapsed") < 3 && Time.time < until) yield return null;
            var rules = Get(Static("CurtainCall.Settings.GameSettings", "WaitingRoom"), "Pursuit");
            var stages = (System.Array)Field(rules, "Stages");
            Assert.IsTrue((bool)Call(director, "DebugSetStage", stages.Length - 1));
            yield return new WaitForSeconds(1f); // 깎기·오르기/넘기 링크 생성 대기
            ((Behaviour)director).enabled = false;
            var actor = ((IEnumerable)Get(director, "Actors")).Cast<Component>().First();
            var candidates = (IList)PursuitPrivate(director, "candidates"); candidates.Clear();
            var agent = actor.GetComponent<NavMeshAgent>();
            var links = T("CurtainCall.WaitingRoom.Pursuit.PursuitClimbLinks");

            var cases = Collect();
            Assert.IsNotEmpty(cases, "부서지는 대상을 찾지 못했다.");
            var state = Static("CurtainCall.WaitingRoom.ExplorationState", "Instance");
            Assert.IsNotNull(state);
            var path = new NavMeshPath();
            var report = new System.Text.StringBuilder();
            int failures = 0;
            var target = new GameObject("BreakableTargetFixture");
            try {
                // 1) 부수기 전: 높은 벽·문은 막고, 낮은 상자 위 플레이어에게는 올라선다.
                foreach (var c in cases) c.Before = Measure(c, path);
                // 바닥에 놓여 양옆이 걸을 수 있는 상자를 이름순으로 고른다(실행마다 같은 대상).
                var box = cases.Where(c => c.Low && c.HasSides && !string.IsNullOrEmpty(c.Id) && c.Blocker.bounds.min.y < .3f)
                    .OrderBy(c => c.Id, System.StringComparer.Ordinal).FirstOrDefault();
                Assert.IsNotNull(box, "양옆이 열린 바닥 상자가 없다.");
                if (box != null) {
                    var top = box.Blocker.bounds.center; top.y = box.Blocker.bounds.max.y;
                    var start = box.Front;
                    Call(actor, "ResetActor"); agent.enabled = false; actor.transform.position = start;
                    target.transform.position = top; candidates.Clear(); candidates.Add(target);
                    bool reached = false; until = Time.time + 20;
                    while (Time.time < until) {
                        Call(actor, "ServerTick", 600f, false); yield return null;
                        var d = top - actor.transform.position;
                        if (new Vector2(d.x, d.z).magnitude < 1.2f && Mathf.Abs(d.y) < .5f) { reached = true; break; }
                    }
                    report.AppendLine($"[before] climb onto {box.Name}({box.Id}) reached={reached} end={actor.transform.position} motion={Get(actor, "State")}");
                    if (!reached) failures++;
                    candidates.Clear();
                }
                // 2) 모두 디버그로 연다(부수기). 장애물 해제·경로 버전·링크 재생성을 기다린다.
                var opened = new HashSet<string>();
                foreach (var c in cases) if (!string.IsNullOrEmpty(c.Id)) Call(state, "RequestDebugOpen", c.Id);
                yield return new WaitForSeconds(1.2f);
                foreach (var c in cases) if (!string.IsNullOrEmpty(c.Id) && (bool)Call(state, "IsOpen", c.Id)) opened.Add(c.Id);
                foreach (var c in cases) {
                    if (!opened.Contains(c.Id)) { report.AppendLine($"[skip] {c.Name}({c.Id}) 디버그로 열리지 않음(열쇠/협동 조건)"); continue; }
                    c.After = Measure(c, path);
                    bool blockerOff = !c.Blocker.enabled || !c.Blocker.gameObject.activeInHierarchy;
                    // 열어도 형태가 남는 상자(뚜껑만 열림, 예: steelBox)는 계속 막는 것이 맞다. 길만 계산되면 된다.
                    bool staysSolid = !blockerOff && c.Low;
                    bool open = !c.HasSides || staysSolid || c.After.StartsWith("Complete");
                    // 부순 상자 자리는 다시 걸을 수 있어야 한다(깎인 구멍이 남지 않음). 선반에 파묻힌 상자는 선반 윗면이 바닥이다.
                    var b = c.Blocker.bounds;
                    bool boxLike = Mathf.Min(b.size.x, b.size.z) >= .3f;
                    bool hole = c.Low && blockerOff && boxLike && !(NavMesh.SamplePosition(b.center, out var under, b.extents.y + .5f, NavMesh.AllAreas)
                        && new Vector2(under.position.x - b.center.x, under.position.z - b.center.z).magnitude < .45f);
                    bool ok = (blockerOff || staysSolid) && open && !hole;
                    if (!ok) failures++;
                    report.AppendLine($"{(ok ? "[ok]" : "[FAIL]")} {c.Name}({c.Id}) low={c.Low} before={c.Before} after={c.After} blockerOff={blockerOff} staysSolid={staysSolid} hole={hole}");
                }
                // 3) 부순 뒤 실제 추격: 열린 높은 벽/문을 지나 뒤편까지 간다.
                var wall = cases.FirstOrDefault(c => !c.Low && c.HasSides && opened.Contains(c.Id) && c.After.StartsWith("Complete")
                    && !c.Before.StartsWith("Complete<"));
                if (wall != null) {
                    Call(actor, "ResetActor"); agent.enabled = false; actor.transform.position = wall.Front;
                    target.transform.position = wall.Back; candidates.Clear(); candidates.Add(target);
                    bool reached = false; until = Time.time + 20;
                    var trace = new System.Text.StringBuilder(); float nextTrace = 0;
                    while (Time.time < until) {
                        Call(actor, "ServerTick", 600f, false); yield return null;
                        var d = wall.Back - actor.transform.position;
                        if (Time.time >= nextTrace) {
                            nextTrace = Time.time + 1;
                            trace.Append($" {actor.transform.position.ToString("F1")}:{Get(actor, "Behaviour")}/{Get(actor, "State")}/goal={Get(actor, "NavigationGoal")}");
                        }
                        if (new Vector2(d.x, d.z).magnitude < 1.2f && Mathf.Abs(d.y) < .6f) { reached = true; break; }
                    }
                    report.AppendLine($"[after] walk through {wall.Name}({wall.Id}) from={wall.Front} to={wall.Back} reached={reached} end={actor.transform.position} trace={trace}");
                    if (!reached) failures++;
                }
                report.AppendLine($"links={Get(null, links, "Count")} vaults={Get(null, links, "VaultCount")}");
            } finally { candidates.Clear(); Object.Destroy(target); }
            Debug.Log("[PursuitBreakables]\n" + report);
            Assert.AreEqual(0, failures, report.ToString());
        }

        static object Get(object obj, System.Type type, string name) => type.GetProperty(name, BindingFlags.Public | BindingFlags.Static).GetValue(obj);

        // 막는 충돌체 앞뒤의 같은 층 경로. "Complete<len>/<straight>" 또는 "Partial"/"None".
        static string Measure(BreakCase c, NavMeshPath path)
        {
            if (!c.HasSides) return "NoSides";
            if (!NavMesh.CalculatePath(c.Front, c.Back, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return path.status.ToString();
            float length = 0; var p = path.corners;
            for (int i = 1; i < p.Length; i++) length += Vector3.Distance(p[i - 1], p[i]);
            float straight = Vector3.Distance(c.Front, c.Back);
            return $"Complete{(length <= straight + 2 ? "<" : ">")}{length:0.0}/{straight:0.0}";
        }

        static List<BreakCase> Collect()
        {
            var result = new List<BreakCase>();
            void Add(Component owner, Collider blocker)
            {
                if (blocker == null || !blocker.enabled || !blocker.gameObject.activeInHierarchy) return;
                var id = owner.GetType().GetField("targetId", BreakFlags)?.GetValue(owner) as string;
                var kind = owner.GetType().GetField("kind", BreakFlags)?.GetValue(owner)?.ToString();
                if (kind == "Cooperative") return; // 탈출 문은 협동 조건이 있어 디버그 열기 대상이 아니다.
                var c = new BreakCase { Id = id, Name = owner.name, Blocker = blocker, Low = blocker.bounds.size.y <= 1.3f };
                // 얇은 수평 축이 통과 방향이다. 양쪽 같은 층 NavMesh를 찾는다.
                var t = blocker.transform; var scale = t.lossyScale;
                var size = blocker is BoxCollider box ? Vector3.Scale(box.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) : blocker.bounds.size;
                var axis = size.x < size.z ? t.right : t.forward; axis.y = 0; axis.Normalize();
                float half = (size.x < size.z ? size.x : size.z) * .5f;
                var center = blocker.bounds.center; center.y = blocker.bounds.min.y + .2f;
                c.HasSides = NavMesh.SamplePosition(center + axis * (half + .9f), out var a, .7f, NavMesh.AllAreas)
                    & NavMesh.SamplePosition(center - axis * (half + .9f), out var b, .7f, NavMesh.AllAreas);
                c.HasSides &= Mathf.Abs(a.position.y - center.y) < .8f && Mathf.Abs(b.position.y - center.y) < .8f;
                c.Front = a.position; c.Back = b.position;
                result.Add(c);
            }
            foreach (var typeName in new[] { "CurtainCall.WaitingRoom.BreakableWall", "CurtainCall.WaitingRoom.ExplorationGate" })
                foreach (Component owner in Object.FindObjectsByType(T(typeName), FindObjectsSortMode.None))
                    Add(owner, owner.GetType().GetField("blocker", BreakFlags)?.GetValue(owner) as Collider);
            return result;
        }
    }
}
