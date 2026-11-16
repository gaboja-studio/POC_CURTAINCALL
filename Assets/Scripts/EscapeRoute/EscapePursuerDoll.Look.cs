using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapePursuerDoll
    {
        [Header("Player look (after authored animation)")]
        public Transform neck, head;
        public Vector3 neckAim = Vector3.forward, headAim = Vector3.forward;
        [Range(0, 1)] public float neckShare = .35f;
        [Range(0, 90)] public float lookYaw = 80f, lookUp = 50f, lookDown = 65f;
        [Min(0)] public float lookResponse = 12f;
        Vector3 lookDirection;
        float lookWeight;
        Quaternion sampledNeck, sampledHead;
        bool lookApplied, hasLookDirection;

        void OnDisable()
        {
            RestoreLookPose();
            lookWeight = 0;
            hasLookDirection = false;
        }

        void RestoreLookPose()
        {
            if (!lookApplied) return;
            if (neck) neck.localRotation = sampledNeck;
            if (head) head.localRotation = sampledHead;
            lookApplied = false;
        }

        Vector3 LimitLook(Vector3 direction)
        {
            var local = transform.InverseTransformDirection(direction.normalized);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -lookYaw, lookYaw);
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -lookUp, lookDown);
            return transform.TransformDirection(Quaternion.Euler(pitch, yaw, 0) * Vector3.forward);
        }

        // Smooth the desired direction, not a small fraction of the freshly sampled pose.
        // Otherwise animation sampling would prevent the head from ever reaching its target.
        public void ApplyLook(Vector3? point, float deltaTime)
        {
            RestoreLookPose();
            if (!neck || !head || deltaTime <= 0) return;
            bool valid = point.HasValue && float.IsFinite(point.Value.sqrMagnitude)
                && (point.Value - head.position).sqrMagnitude > .0001f;
            lookWeight = Mathf.MoveTowards(lookWeight, valid ? 1 : 0, deltaTime * 6f);
            if (!hasLookDirection)
            {
                if (!valid) return;
                lookDirection = head.TransformDirection(headAim).normalized;
                hasLookDirection = true;
            }
            if (valid)
                lookDirection = Vector3.Slerp(lookDirection, LimitLook(point.Value - head.position),
                    1 - Mathf.Exp(-lookResponse * deltaTime)).normalized;
            if (lookWeight <= 0) { hasLookDirection = false; return; }
            var desired = LimitLook(lookDirection);
            sampledNeck = neck.localRotation;
            sampledHead = head.localRotation;
            var turn = Quaternion.FromToRotation(neck.TransformDirection(neckAim), desired);
            neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, neckShare * lookWeight) * neck.rotation;
            // Recalculate after the neck moves, so parent and child do not double the turn.
            turn = Quaternion.FromToRotation(head.TransformDirection(headAim), desired);
            head.rotation = Quaternion.Slerp(Quaternion.identity, turn, lookWeight) * head.rotation;
            lookApplied = true;
        }
    }
}
