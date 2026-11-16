using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 첫 공연 전(리허설 단계) 대기실 동선을 막는 철창(Task-097). 로비에서 숙소·소품실·상점 홀로 이어지는 입구처럼
    /// 기존 밤 정비 철창(<see cref="NightDoorLock"/>)이 없는 곳에 런타임으로 세운다. 위에서 내려와 닫히고, 대기실 튜토리얼 때
    /// (리허설 단계가 끝나면) 말려 올라가 사라진다. 닫힘 여부는 모두에게 같은 런 상태로만 정한다.
    /// </summary>
    public sealed class RehearsalLockdownGate : MonoBehaviour
    {
        const float BarSpacing = .2f, MoveSeconds = 1.6f, BarrierDepth = 1.2f;

        /// <summary>리허설 단계(첫 공연 전) 동선 제한 중인가. <see cref="WaitingRoomRehearsal"/>가 매 프레임 정한다.</summary>
        public static bool Restricted { get; set; }
        /// <summary>첫 공연 뒤 대기실 튜토리얼이 끝나기 전인가(<see cref="WaitingRoomRehearsal.IsFirstTutorialPending"/>).</summary>
        public static bool TutorialPending { get; set; }
        /// <summary>대기실 튜토리얼이 끝날 때까지 닫아 두는 철창(숙소, 작업실·소품실).</summary>
        bool holdThroughTutorial;
        bool Shut => Restricted || holdThroughTutorial && TutorialPending;

        Transform bars;
        BoxCollider block;
        float closed = -1f;
        float barHeight;

        /// <summary>바닥의 두 점 사이에 공통 높이의 철창을 세운다.</summary>
        public static RehearsalLockdownGate Create(Transform parent, string name, Vector3 from, Vector3 to, Material iron, bool holdThroughTutorial = false, Vector3? lobby = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            Vector3 along = to - from; along.y = 0f;
            root.transform.SetPositionAndRotation((from + to) * .5f, Quaternion.LookRotation(Vector3.Cross(along.normalized, Vector3.up)));
            var gate = root.AddComponent<RehearsalLockdownGate>();
            gate.holdThroughTutorial = holdThroughTutorial;
            // 로비 반대쪽(막는 구역 쪽)이 어느 방향인지: 투명 벽을 그쪽으로 두껍게 만든다.
            Vector3 toLobby = (lobby ?? root.transform.position) - root.transform.position; toLobby.y = 0f;
            gate.Build(along.magnitude, iron, Vector3.Dot(toLobby, root.transform.forward) > 0f ? -1f : 1f);
            return gate;
        }

        void Build(float width, Material iron, float inward)
        {
            // 천장·문틀보다 높게 세우면 철창이 지형을 뚫고 위층으로 솟는다(2026-11-16 버그 리포트). 바로 위 천장 아래까지만 세운다.
            barHeight = Mathf.Min(GameSettings.WaitingRoom.Tutorial.LockdownGateHeight,
                CeilingAbove(transform.position + Vector3.up * .3f, transform, GameSettings.WaitingRoom.Tutorial.LockdownGateHeight) + .3f);
            bars = new GameObject("Bars").transform;
            bars.SetParent(transform, false);
            int count = Mathf.Max(2, Mathf.CeilToInt(width / BarSpacing) + 1);
            for (int i = 0; i < count; i++)
            {
                float x = -width * .5f + width * i / (count - 1);
                Part(PrimitiveType.Cylinder, new Vector3(x, barHeight * .5f, 0f), new Vector3(.05f, barHeight * .5f, .05f), iron);
            }
            foreach (float y in new[] { .12f, barHeight * .5f, barHeight - .08f })
                Part(PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(width, .08f, .1f), iron);
            block = CreateBarrier(transform, 0f, 0f, width);
            // 얇은 벽(0.3m)은 목마를 타거나 내릴 때 캐릭터가 반대쪽에 놓이면 그대로 넘어가 버린다 → 막는 구역 쪽으로 두께를 늘려, 그 안에 놓이면 로비 쪽으로 밀려나게 한다(2026-11-15 사용자: 서비스 통로).
            block.center += new Vector3(0f, 0f, inward * (BarrierDepth - .3f) * .5f);
            block.size = new Vector3(block.size.x, block.size.y, BarrierDepth);
            block.enabled = Shut;
        }

        /// <summary>point에서 위로 가장 가까운 천장·문틀까지의 거리(없으면 max). ignore 아래 충돌체와 트리거는 건너뛴다. 2cm 여유를 둔다.</summary>
        internal static float CeilingAbove(Vector3 point, Transform ignore, float max)
        {
            float best = max;
            foreach (var hit in Physics.RaycastAll(point, Vector3.up, max, ~0, QueryTriggerInteraction.Ignore))
                if ((ignore == null || !hit.transform.IsChildOf(ignore)) && hit.collider.GetComponentInParent<CurtainCall.Network.PlayerSync.NetworkPlayer>() == null)
                    best = Mathf.Min(best, Mathf.Max(0f, hit.distance - .02f));
            return best;
        }

        internal static BoxCollider CreateBarrier(Transform gate, float centerX, float centerZ, float width)
        {
            float height = GameSettings.WaitingRoom.Tutorial.LockdownBarrierHeight;
            var collider = gate.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(centerX, (height - .2f) * .5f, centerZ);
            collider.size = new Vector3(width + .2f, height + .2f, .3f);
            return collider;
        }

        // 세로 창살(위아래로 잘라 보이는 길이를 줄인다)과 가로대(천장 위로 넘어가면 숨긴다).
        readonly System.Collections.Generic.List<(Transform part, bool vertical, float baseY)> parts = new();

        void Part(PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(bars, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            parts.Add((part.transform, type == PrimitiveType.Cylinder, position.y));
        }

        void Update()
        {
            float target = Shut ? 1f : 0f;
            // 처음 만들 때는 움직이지 않고 지금 상태로 둔다.
            if (closed < 0f) closed = target;
            closed = Mathf.MoveTowards(closed, target, Time.deltaTime / MoveSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, closed);
            bars.gameObject.SetActive(closed > 0f);
            Lift((1f - eased) * barHeight);
            block.enabled = Shut;
        }

        // 철창은 위로 올라가며 열리고 내려오며 닫힌다. 천장(barHeight) 위로 넘어간 부분은 그리지 않는다
        // (2026-11-16 버그 리포트: 올라가는 철창이 천장·위층 바닥을 뚫고 보임). 닫힐 때는 천장 속에서 내려오는 모습이 보인다.
        float lifted = -1f;
        void Lift(float offset)
        {
            if (Mathf.Approximately(offset, lifted)) return;
            lifted = offset;
            foreach (var (part, vertical, baseY) in parts)
            {
                if (part == null) continue;
                if (vertical)
                {
                    float visible = barHeight - offset;
                    part.gameObject.SetActive(visible > .02f);
                    var scale = part.localScale; scale.y = Mathf.Max(.01f, visible * .5f); part.localScale = scale;
                    var p = part.localPosition; p.y = offset + visible * .5f; part.localPosition = p;
                }
                else
                {
                    float y = baseY + offset;
                    part.gameObject.SetActive(y + .04f <= barHeight);
                    var p = part.localPosition; p.y = y; part.localPosition = p;
                }
            }
        }
    }
}
