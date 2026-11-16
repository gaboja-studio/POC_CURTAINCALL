using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>지정 대상이 열리면 옆으로 밀려 열리는 판(숨겨진 문). 상태는 ExplorationState를 따른다.</summary>
    public sealed class ExplorationSlide : MonoBehaviour
    {
        [SerializeField] string openedBy;
        [SerializeField] Vector3 openOffset;
        [SerializeField, Min(.1f)] float seconds = 1.2f;
        [Tooltip("지정하면 이 문(대상)의 열림 상태를 그대로 따른다. 통과 판정(막음 충돌)과 보이는 문짝이 어긋나지 않게 한다(2026-11-14 사용자: 닫혀 보이는데 지나갈 수 있던 탈출로 문).")]
        [SerializeField] ExplorationGate followGate;
        [Tooltip("이 대상이 열리는 순간(회로 고장) 잠깐 열렸다 닫혔다 오작동한 뒤 완전히 열린 채로 굳는다(2026-11-14 사용자). 비우면 오작동 없음.")]
        [SerializeField] string glitchBy = "";
        [SerializeField, Min(0f)] float glitchSeconds = 3f;
        Vector3 closed;
        float t, glitchUntil;
        bool glitchSeen, glitchWas;

        public bool FullyOpen => t >= 1f;
        void Awake() { closed = transform.localPosition; CaptureParts(); }

        // ---- 문틀 밖으로 밀려난 부분 숨기기(2026-11-16 버그 리포트: 위로 열리는 잠긴 문이 천장·지형을 뚫고 보임) ----
        // 밀려 움직이는 방식은 그대로 두고, 닫혔을 때 문짝이 차지하던 범위(문틀 안)를 벗어난 부분만 그리지 않는다.
        // 메시 꼭짓점을 범위 끝면으로 눌러 범위 밖 부분을 잘라 그린다(메시 사본만 바꾸고 원본 에셋은 건드리지 않는다).
        // 읽을 수 없는 메시는 자를 수 없어 기존처럼 움직이기만 한다.
        // 닫힐 때는 같은 계산이 거꾸로 적용되어 문틀 가장자리에서 문짝이 나오며 닫히고, 다 닫히면 원래 메시로 돌아간다.
        struct Part { public Renderer view; public MeshFilter filter; public Mesh source, work; public Vector3[] verts, buffer; public float lo, hi; }
        Part[] parts = System.Array.Empty<Part>();
        Vector3 slideDir; float allowLo, allowHi, clippedAt = -1f;
        void CaptureParts()
        {
            var world = transform.parent != null ? transform.parent.TransformVector(openOffset) : openOffset;
            if (world.sqrMagnitude < 1e-6f) return;
            slideDir = world.normalized;
            var list = new System.Collections.Generic.List<Part>();
            allowLo = float.PositiveInfinity; allowHi = float.NegativeInfinity;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is not MeshRenderer) continue;
                Along(r.bounds, out float lo, out float hi);
                allowLo = Mathf.Min(allowLo, lo); allowHi = Mathf.Max(allowHi, hi);
                var filter = r.GetComponent<MeshFilter>(); var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;
                list.Add(new Part { view = r, filter = filter, source = mesh, verts = mesh.vertices, lo = lo, hi = hi });
            }
            parts = list.ToArray();
        }
        void OnDestroy() { foreach (var p in parts) if (p.work != null) Destroy(p.work); }
        void Along(Bounds b, out float lo, out float hi)
        {
            float c = Vector3.Dot(b.center, slideDir), e = Mathf.Abs(b.extents.x * slideDir.x) + Mathf.Abs(b.extents.y * slideDir.y) + Mathf.Abs(b.extents.z * slideDir.z);
            lo = c - e; hi = c + e;
        }
        void Clip(float moved)
        {
            if (parts.Length == 0 || Mathf.Approximately(moved, clippedAt)) return;
            clippedAt = moved;
            for (int i = 0; i < parts.Length; i++)
            {
                ref var p = ref parts[i];
                if (p.view == null || p.filter == null) continue;
                float lo = p.lo + moved, hi = p.hi + moved;
                // 다 닫혔거나 범위 안에 그대로 있으면 원래 메시를 그대로 그린다.
                if (moved <= 0f || (lo >= allowLo - .001f && hi <= allowHi + .001f))
                { if (p.filter.sharedMesh != p.source) p.filter.sharedMesh = p.source; p.view.forceRenderingOff = false; continue; }
                // 다 밀려나 범위 밖에 있으면 그리지 않는다.
                if (Mathf.Min(hi, allowHi) - Mathf.Max(lo, allowLo) <= .002f) { p.view.forceRenderingOff = true; continue; }
                p.view.forceRenderingOff = false;
                if (p.work == null) { p.work = Instantiate(p.source); p.work.name = p.source.name + " (clipped)"; p.work.MarkDynamic(); p.buffer = new Vector3[p.verts.Length]; }
                var toWorld = p.filter.transform.localToWorldMatrix; var toLocal = p.filter.transform.worldToLocalMatrix;
                for (int v = 0; v < p.verts.Length; v++)
                {
                    var w = toWorld.MultiplyPoint3x4(p.verts[v]);
                    float a = Vector3.Dot(w, slideDir), c = Mathf.Clamp(a, allowLo, allowHi);
                    p.buffer[v] = c == a ? p.verts[v] : toLocal.MultiplyPoint3x4(w + slideDir * (c - a));
                }
                p.work.vertices = p.buffer; p.work.RecalculateBounds();
                if (p.filter.sharedMesh != p.work) p.filter.sharedMesh = p.work;
            }
        }
        void Update()
        {
            bool open = followGate != null ? followGate.Opened : ExplorationGate.IsAnyOpen(openedBy); // "|"로 여러 대상을 적으면 하나만 열려도 열린다
            float speed = 1f;
            if (!string.IsNullOrEmpty(glitchBy) && ExplorationState.Instance != null)
            {
                // 들어왔을 때 이미 고장 나 있던 문은 오작동 없이 열린 채로 둔다.
                bool broken = ExplorationGate.IsAnyOpen(glitchBy);
                if (glitchSeen && broken && !glitchWas) glitchUntil = Time.time + glitchSeconds;
                glitchSeen = true; glitchWas = broken;
                float left = glitchUntil - Time.time;
                // 불규칙하게 닫혔다 열렸다 한다(모든 화면이 비슷한 박자). 마지막 0.4초는 열리는 쪽으로 간다.
                if (left > 0f) { open = left < .4f || Mathf.PerlinNoise(left * 2.3f, 7.7f) > .5f; speed = 4f; }
            }
            t = Mathf.MoveTowards(t, open ? 1f : 0f, Time.deltaTime / seconds * speed);
            float k = Mathf.SmoothStep(0f, 1f, t);
            transform.localPosition = closed + openOffset * k;
            if (parts.Length > 0)
            {
                var world = transform.parent != null ? transform.parent.TransformVector(openOffset) : openOffset;
                Clip(Vector3.Dot(world, slideDir) * k);
            }
        }
    }
}
