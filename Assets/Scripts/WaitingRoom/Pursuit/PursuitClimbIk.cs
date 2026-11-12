using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>Blender 동작 평가 뒤 적용하는 시각 전용 접점 IK. 게임 루트·본 길이·지형은 바꾸지 않는다.</summary>
    public sealed class PursuitClimbIk
    {
        sealed class Limb
        {
            public Transform upper, middle, end;
            public Vector3 contact;
            public Quaternion localFrame;
            public Quaternion upperPose, middlePose, endPose;
            public Anchor start, finish;
            public bool hand, left;
            public float weight;
        }
        struct Anchor
        {
            public Collider collider;
            public Vector3 point, normal;
            public bool Valid => collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
            public Vector3 Point => collider.transform.TransformPoint(point);
            public Vector3 Normal => collider.transform.TransformDirection(normal).normalized;
            public static Anchor From(RaycastHit hit) => new() { collider = hit.collider,
                point = hit.collider.transform.InverseTransformPoint(hit.point), normal = hit.collider.transform.InverseTransformDirection(hit.normal) };
        }
        readonly Transform root;
        readonly Limb[] limbs = new Limb[4];
        readonly RaycastHit[] hits = new RaycastHit[32];
        PhysicsScene physics;
        Vector3 forward, right;
        bool up, applied;
        public int LimbCount { get; private set; }
        public int ActiveContacts { get; private set; }
        public float MaxContactError { get; private set; }
        public int PlantedHands { get; private set; }
        public int PlantedFeet { get; private set; }

        public PursuitClimbIk(Transform actor, SkinnedMeshRenderer skin)
        {
            root = actor;
            if (skin == null || skin.sharedMesh == null || !skin.sharedMesh.isReadable) return;
            var mesh = skin.sharedMesh;
            var bones = skin.bones; var weights = mesh.boneWeights; var vertices = mesh.vertices; var bind = mesh.bindposes;
            Transform Find(string name) => Array.Find(bones, b => b != null && b.name == name);
            for (int i = 0; i < limbs.Length; i++)
            {
                bool hand = i < 2, left = i % 2 == 0;
                string side = left ? "_L" : "_R";
                var upper = Find((hand ? "UpperArm" : "Thigh") + side);
                var middle = Find((hand ? "Forearm" : "Shin") + side);
                var end = Find((hand ? "Hand" : "Foot") + side);
                if (upper == null || middle == null || end == null) continue;
                int index = Array.IndexOf(bones, end), count = 0;
                Vector3 center = Vector3.zero; float sole = float.PositiveInfinity;
                var localNormal = end.InverseTransformDirection(hand ? root.forward : root.up).normalized;
                var localForward = end.InverseTransformDirection(hand ? end.position - middle.position : root.forward).normalized;
                float Influence(BoneWeight w) => (w.boneIndex0 == index ? w.weight0 : 0) + (w.boneIndex1 == index ? w.weight1 : 0)
                    + (w.boneIndex2 == index ? w.weight2 : 0) + (w.boneIndex3 == index ? w.weight3 : 0);
                float peak = 0;
                foreach (var weight in weights) peak = Mathf.Max(peak, Influence(weight));
                if (peak <= .0001f) continue;
                // 일부 정상 발은 정강이와 가중치를 공유해 최대 영향도도 0.5 미만이다.
                for (int v = 0; v < vertices.Length; v++)
                {
                    if (Influence(weights[v]) < peak * .5f) continue;
                    var p = bind[index].MultiplyPoint3x4(vertices[v]);
                    center += p; sole = Mathf.Min(sole, Vector3.Dot(p, localNormal)); count++;
                }
                // 본은 남아 있어도 실제 손/발 메시 가중치가 없으면 결손 사지다.
                if (count == 0) continue;
                center /= count;
                // FBX 리그의 cm→m 스케일과 무관하게 손바닥 두께 보정은 월드 2.5cm다.
                float palmDepth = .025f / Mathf.Max(.00001f, end.TransformVector(localNormal).magnitude);
                center += localNormal * (hand ? -palmDepth : sole - Vector3.Dot(center, localNormal));
                limbs[i] = new Limb { upper = upper, middle = middle, end = end, hand = hand, left = left,
                    contact = center, localFrame = Quaternion.LookRotation(localForward, localNormal) };
                LimbCount++;
            }
        }

        public void Begin(Vector3 from, Vector3 to, bool ascending, PursuitSettings rules)
        {
            Restore();
            up = ascending; physics = root.gameObject.scene.GetPhysicsScene();
            forward = Vector3.ProjectOnPlane(to - from, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = root.forward;
            right = Vector3.Cross(Vector3.up, forward).normalized;
            var high = up ? to : from; var low = up ? from : to;
            var inward = up ? forward : -forward;
            foreach (var limb in limbs)
            {
                if (limb == null) continue;
                limb.start = limb.finish = default;
                float lateral = Vector3.Dot(limb.end.position - root.position, root.right);
                lateral = Mathf.Clamp(Mathf.Abs(lateral), .08f, limb.hand ? .32f : .18f) * (limb.left ? -1 : 1);
                if (limb.hand)
                {
                    // 아래쪽→위쪽을 훑어 NavMesh 내부 끝점 대신 실제 콜라이더 가장자리를 찾는다.
                    var outside = new Vector3(low.x, high.y, low.z) + right * lateral;
                    var inside = high + right * lateral;
                    for (int step = 0; step <= 16; step++)
                    {
                        var point = Vector3.Lerp(outside, inside, step / 16f) + inward * .025f;
                        if (!Probe(point, high.y, rules, out var hit)) continue;
                        limb.finish = Anchor.From(hit); break;
                    }
                }
                else
                {
                    if (Probe(from + right * lateral, from.y, rules, out var start)) limb.start = Anchor.From(start);
                    if (Probe(to + right * lateral, to.y, rules, out var finish)) limb.finish = Anchor.From(finish);
                }
            }
        }

        bool Probe(Vector3 point, float height, PursuitSettings rules, out RaycastHit nearest)
        {
            nearest = default;
            float reach = Mathf.Max(.05f, rules.ClimbIkProbeHeight);
            point.y = height + reach;
            int count = physics.Raycast(point, Vector3.down, hits, reach * 2, rules.ClimbIkSurfaceMask, QueryTriggerInteraction.Ignore);
            // 버퍼가 찬 경우 가려진 표면을 추측하지 않는다.
            if (count == hits.Length) return false;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i]; var c = h.collider;
                if (c == null || c.transform.IsChildOf(root) || c.GetComponentInParent<PursuitActor>() != null
                    || c.GetComponentInParent<NetworkPlayer>() != null || c.GetComponentInParent<PlayerMover>() != null
                    || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)) continue;
                if (h.distance >= distance) continue;
                distance = h.distance; nearest = h;
            }
            return nearest.collider != null && nearest.normal.y >= .65f;
        }

        /// <summary>접점 완전 고정 구간은 weight=1. 접근·해제 구간은 원본 포즈와 부드럽게 섞는다.</summary>
        public void Apply(float progress, PursuitSettings rules)
        {
            Restore(); ActiveContacts = PlantedHands = PlantedFeet = 0; MaxContactError = 0;
            if (!rules.ClimbContactIk) return;
            float t = Mathf.Clamp01(progress);
            foreach (var limb in limbs)
            {
                if (limb == null) continue;
                limb.upperPose = limb.upper.localRotation; limb.middlePose = limb.middle.localRotation; limb.endPose = limb.end.localRotation;
                limb.weight = 0;
            }
            applied = true;
            foreach (var limb in limbs)
            {
                if (limb == null) continue;
                float w; Anchor anchor;
                if (limb.hand)
                {
                    anchor = limb.finish;
                    // 오르기: 접촉(0.12)부터 무릎 넘기기(PursuitClimbPath.KneeOver)까지 가장자리를 짚고 넘어가며 놓는다.
                    w = up ? Window(t, .02f, .12f, PursuitClimbPath.KneeOver + .02f, .8f) : Window(t, 0, .02f, .08f, .22f);
                }
                else if (t < .3f)
                {
                    anchor = limb.start;
                    w = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.03f, up ? .18f : .2f, t));
                }
                else
                {
                    anchor = limb.finish;
                    // 내려오기는 구간 끝에 착지하므로 FinishClimb/Clear에서 해제한다.
                    w = up ? Window(t, limb.left ? .48f : .62f, limb.left ? .65f : .8f, .94f, 1)
                        : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, .85f, t));
                }
                if (w <= 0 || !anchor.Valid) continue;
                Vector3 normal = anchor.Normal;
                var tangent = Vector3.ProjectOnPlane(forward, normal).normalized;
                if (tangent.sqrMagnitude < .1f) continue;
                var rotation = Quaternion.LookRotation(tangent, normal) * Quaternion.Inverse(limb.localFrame);
                var offset = rotation * Quaternion.Inverse(limb.end.rotation) * limb.end.TransformVector(limb.contact);
                var contact = anchor.Point + normal * rules.ClimbIkSurfaceOffset;
                var wrist = contact - offset;
                float length = Vector3.Distance(limb.upper.position, limb.middle.position) + Vector3.Distance(limb.middle.position, limb.end.position);
                float distance = Vector3.Distance(limb.upper.position, wrist);
                // 손발이 닿지 않는 높이에서는 본을 늘리거나 게임 루트를 끌어당기지 않고 해제한다.
                w *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(length * .995f, length, distance));
                if (w <= .001f) continue;
                var originalRotation = limb.end.rotation;
                var pole = limb.middle.position - limb.upper.position;
                if (!Solve(limb.upper, limb.middle, limb.end, Vector3.Lerp(limb.end.position, wrist, w), pole)) continue;
                limb.end.rotation = Quaternion.Slerp(originalRotation, rotation, w);
                limb.weight = w; ActiveContacts++;
                if (w > .999f) {
                    if (limb.hand) PlantedHands++; else PlantedFeet++;
                    MaxContactError = Mathf.Max(MaxContactError, Vector3.Distance(limb.end.TransformPoint(limb.contact), contact));
                }
            }
        }

        public void Restore()
        {
            if (!applied) return;
            foreach (var limb in limbs)
            {
                if (limb == null) continue;
                limb.upper.localRotation = limb.upperPose; limb.middle.localRotation = limb.middlePose; limb.end.localRotation = limb.endPose;
            }
            applied = false;
        }
        public void Clear() { Restore(); ActiveContacts = PlantedHands = PlantedFeet = 0; MaxContactError = 0; foreach (var limb in limbs) if (limb != null) limb.start = limb.finish = default; }
        static float Window(float t, float begin, float planted, float release, float end)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(begin, planted, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(release, end, t)));

        /// <summary>회전만으로 푸는 2본 IK. 도달 불가 목표는 길이 범위로 제한한다.</summary>
        public static bool Solve(Transform upper, Transform middle, Transform end, Vector3 target, Vector3 pole)
        {
            var origin = upper.position; var delta = target - origin;
            float a = Vector3.Distance(origin, middle.position), b = Vector3.Distance(middle.position, end.position);
            if (a < .0001f || b < .0001f || delta.sqrMagnitude < .00000001f) return false;
            float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .00001f, a + b - .00001f);
            var direction = delta.normalized;
            var bend = Vector3.ProjectOnPlane(pole, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.Cross(direction, Mathf.Abs(direction.y) < .9f ? Vector3.up : Vector3.right);
            bend.Normalize();
            float x = (a * a - b * b + d * d) / (2 * d);
            var elbow = origin + direction * x + bend * Mathf.Sqrt(Mathf.Max(0, a * a - x * x));
            upper.rotation = Quaternion.FromToRotation(middle.position - origin, elbow - origin) * upper.rotation;
            var clamped = origin + direction * d;
            middle.rotation = Quaternion.FromToRotation(end.position - middle.position, clamped - middle.position) * middle.rotation;
            return true;
        }
    }
}
