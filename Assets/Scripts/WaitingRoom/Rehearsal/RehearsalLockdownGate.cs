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
        const float Height = 3.4f, BarSpacing = .2f, MoveSeconds = 1.6f;

        /// <summary>리허설 단계(첫 공연 전) 동선 제한 중인가. <see cref="WaitingRoomRehearsal"/>가 매 프레임 정한다.</summary>
        public static bool Restricted { get; set; }

        Transform bars;
        BoxCollider block;
        float closed = -1f;

        /// <summary>바닥의 두 점 사이에 철창을 세운다(높이 3.4m).</summary>
        public static RehearsalLockdownGate Create(Transform parent, string name, Vector3 from, Vector3 to, Material iron)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            Vector3 along = to - from; along.y = 0f;
            root.transform.SetPositionAndRotation((from + to) * .5f, Quaternion.LookRotation(Vector3.Cross(along.normalized, Vector3.up)));
            var gate = root.AddComponent<RehearsalLockdownGate>();
            gate.Build(along.magnitude, iron);
            return gate;
        }

        void Build(float width, Material iron)
        {
            bars = new GameObject("Bars").transform;
            bars.SetParent(transform, false);
            int count = Mathf.Max(2, Mathf.CeilToInt(width / BarSpacing) + 1);
            for (int i = 0; i < count; i++)
            {
                float x = -width * .5f + width * i / (count - 1);
                Part(PrimitiveType.Cylinder, new Vector3(x, Height * .5f, 0f), new Vector3(.05f, Height * .5f, .05f), iron);
            }
            foreach (float y in new[] { .12f, Height * .5f, Height - .08f })
                Part(PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(width, .08f, .1f), iron);
            block = gameObject.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, Height * .5f, 0f);
            block.size = new Vector3(width, Height, .3f);
        }

        void Part(PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(bars, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
        }

        void Update()
        {
            float target = Restricted ? 1f : 0f;
            // 처음 만들 때는 움직이지 않고 지금 상태로 둔다.
            if (closed < 0f) closed = target;
            closed = Mathf.MoveTowards(closed, target, Time.deltaTime / MoveSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, closed);
            bars.gameObject.SetActive(closed > 0f);
            bars.localPosition = new Vector3(0f, (1f - eased) * Height, 0f);
            block.enabled = closed > .5f;
        }
    }
}
