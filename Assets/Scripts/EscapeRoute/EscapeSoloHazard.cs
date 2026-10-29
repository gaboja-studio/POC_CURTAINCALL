using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Offline rehearsal timing/contact. Production host damage remains unchanged.</summary>
    public sealed class EscapeSoloHazard : MonoBehaviour
    {
        public enum Kind { SideSaw, FrontSaw, GroundSaw, Drop, Mace, CrossBlades, Fire, Wind }
        public Kind kind;
        public EscapeSoloReview player;
        public Transform visual, warning;
        public Vector3 origin, destination, size = Vector3.one;
        public float warningSeconds = 1.4f, travelSeconds = 2, restSeconds = 2, phase;
        public float activateMinX, activateMaxX;
        public float radius = 1.1f;
        float clock;
        bool activeBefore;
        Vector3 previous;
        Quaternion initialRotation;
        Vector3 initialScale;

        void Start() { initialRotation = visual.rotation; initialScale = visual.localScale; ResetCycle(); }
        public void ResetCycle() { clock = 0; activeBefore = false; if (visual != null) visual.position = origin; }

        void Update()
        {
            if (player == null || visual == null || player.Paused) return;
            bool near = player.Feet.x >= activateMinX && player.Feet.x <= activateMaxX && player.Feet.z > 65 && player.Feet.z < 92;
            if (!near) { clock = 0; activeBefore = false; visual.gameObject.SetActive(false); if (warning != null) warning.gameObject.SetActive(false); return; }
            clock += Time.deltaTime;
            float period = warningSeconds + travelSeconds + restSeconds;
            float t = (clock + phase) % period;
            bool live = t >= warningSeconds && t < warningSeconds + travelSeconds;
            if (warning != null) warning.gameObject.SetActive(t < warningSeconds && (Mathf.FloorToInt(t * 6) % 2 == 0) || live);
            visual.gameObject.SetActive(live || kind == Kind.Drop && t < warningSeconds);
            float u = Mathf.Clamp01((t-warningSeconds) / travelSeconds);
            Vector3 now = kind == Kind.Fire || kind == Kind.Wind || kind == Kind.CrossBlades ? origin : Vector3.Lerp(origin,destination,kind == Kind.Drop ? u*u : u);
            visual.position = now;
            if (kind == Kind.SideSaw || kind == Kind.FrontSaw || kind == Kind.GroundSaw || kind == Kind.CrossBlades || kind == Kind.Mace)
                visual.rotation = initialRotation * Quaternion.Euler(0,0,clock * (kind == Kind.CrossBlades ? 130 : 480));
            if (kind == Kind.Fire) visual.localScale = Vector3.Scale(initialScale,new Vector3(1,.85f + .15f * Mathf.Sin(clock*26),1));
            if (!live) { activeBefore = false; previous = now; return; }
            if (!activeBefore) previous = now;
            if (!player.Flying)
            {
                if (kind == Kind.Wind)
                {
                    if (new Bounds(origin,size).Contains(player.Feet + Vector3.up * .7f)) player.Push(destination * Time.deltaTime);
                }
                else if (Touches(now)) player.Fail(kind == Kind.Drop ? "낙하물 — 예고된 바닥 밖으로 피하기" : kind == Kind.Fire ? "불 — 반대쪽 통로로 피하기" : "장애물 접촉 — 구간 재시작");
            }
            previous = now; activeBefore = true;
        }

        bool Touches(Vector3 now)
        {
            if (kind == Kind.Drop || kind == Kind.Fire)
            {
                // Sweep the moving box against the standing capsule's conservative bounds.
                var box = new Bounds((previous+now)*.5f,size + new Vector3(Mathf.Abs(now.x-previous.x),Mathf.Abs(now.y-previous.y),Mathf.Abs(now.z-previous.z)));
                return box.Intersects(new Bounds(player.Feet + Vector3.up*.75f,new Vector3(.6f,1.5f,.6f)));
            }
            if (kind == Kind.CrossBlades)
            {
                for(int i=0;i<2;i++)
                {
                    var axis = visual.rotation * (i == 0 ? Vector3.right : Vector3.up) * radius;
                    if (CannonHazardGeometry.SegmentTouches(now-axis,now+axis,.14f,player.Feet,1.5f,.3f)) return true;
                }
                return false;
            }
            return CannonHazardGeometry.SegmentTouches(previous,now,radius,player.Feet,1.5f,.3f);
        }
    }
}
