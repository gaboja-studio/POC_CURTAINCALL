using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>잠긴 문은 막고 열거나 비활성화하면 통행한다. 원본 문의 규칙은 변경하지 않는다.</summary>
    public sealed class PursuitDoorObstacle : MonoBehaviour
    {
        public Collider Source;
        public PursuitNoiseKind Noise = PursuitNoiseKind.Door;
        public static PursuitNoiseKind SuggestedNoise(Collider source)
        {
            if (source == null) return PursuitNoiseKind.None;
            foreach (var parent in source.GetComponentsInParent<Transform>(true))
                if (parent.name.EndsWith("VentCover", System.StringComparison.OrdinalIgnoreCase)) return PursuitNoiseKind.VentOpen;
            var name = source.name.ToLowerInvariant();
            return name.Contains("door") || name.Contains("gate") || name.Contains("shutter") || name == "leafblocker"
                ? PursuitNoiseKind.Door : PursuitNoiseKind.None;
        }
        NavMeshObstacle obstacle;
        bool observed, wasBlocking;
        Vector3 previousCenter, previousSize, lastMotion;
        Quaternion previousRotation;
        float movedAt = -10, invalidatedAt = -10;
        void Awake()
        {
            obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true; obstacle.carveOnlyStationary = false;
        }
        void LateUpdate()
        {
            if (obstacle == null || Source == null) return;
            bool blocking = Source.enabled && Source.gameObject.activeInHierarchy;
            var bounds = Source.bounds;
            var centerWorld = blocking ? bounds.center : previousCenter;
            var sizeWorld = blocking ? bounds.size : previousSize;
            var rotation = Source.transform.rotation;
            var motion = centerWorld - previousCenter + (sizeWorld - previousSize) * .5f;
            bool moved = blocking && (motion.sqrMagnitude > .000001f || Quaternion.Angle(rotation, previousRotation) > .05f);
            if (observed) {
                bool stateChanged = wasBlocking != blocking;
                bool motionStarted = moved && (Time.time - movedAt > .15f || Vector3.Dot(motion, lastMotion) < 0);
                if ((stateChanged || motionStarted) && (Noise != PursuitNoiseKind.VentOpen || !blocking))
                    PursuitStimuli.Emit(Noise, centerWorld, Source.transform);
                if ((stateChanged || moved) && Time.time >= invalidatedAt) {
                    invalidatedAt = Time.time + .15f;
                    PursuitNavigation.Invalidate(); PursuitClimbLinks.Invalidate();
                }
            }
            if (moved) { movedAt = Time.time; lastMotion = motion; }
            observed = true; wasBlocking = blocking;
            previousCenter = centerWorld; previousSize = sizeWorld; previousRotation = rotation;
            bool changed = false;
            if (obstacle.enabled != blocking) { obstacle.enabled = blocking; changed = true; }
            Vector3 center, size;
            if (Source is BoxCollider box) { center = box.center; size = box.size; }
            else { center = transform.InverseTransformPoint(Source.bounds.center); size = Source.bounds.size; }
            // 같은 값을 매 프레임 다시 넣으면 NavMesh를 계속 다시 깎아 추격자 경로가 무효화된다. 바뀐 때만 반영한다.
            if ((obstacle.center - center).sqrMagnitude > .0001f) { obstacle.center = center; changed = true; }
            if ((obstacle.size - size).sqrMagnitude > .0001f) { obstacle.size = size; changed = true; }
            // 문이 잠기거나 열린 때만 추격자들이 고정 목표 경로를 다시 계산한다.
            if (changed) { PursuitNavigation.Invalidate(); PursuitClimbLinks.Invalidate(); }
        }
        void OnDisable() {
            if (Application.isPlaying && observed && wasBlocking) PursuitStimuli.Emit(Noise, previousCenter, Source != null ? Source.transform : transform);
            wasBlocking = false;
            PursuitNavigation.Invalidate(); PursuitClimbLinks.Invalidate();
        }
    }
}
