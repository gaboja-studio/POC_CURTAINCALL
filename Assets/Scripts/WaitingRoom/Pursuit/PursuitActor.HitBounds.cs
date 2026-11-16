using System.Linq;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitActor
    {
        static readonly string[] HitBones = { "Hips", "Chest", "Head", "UpperArm_L", "Forearm_L", "Hand_L", "UpperArm_R", "Forearm_R", "Hand_R", "Thigh_L", "Shin_L", "Foot_L", "Thigh_R", "Shin_R", "Foot_R" };
        static readonly int[] HitParents = { 0, 0, 1, 1, 3, 4, 1, 6, 7, 0, 9, 10, 0, 12, 13 };
        Transform[] hitBones;

        /// <summary>Small bounds around the animated skeleton; imported renderer bounds stay in the bind pose.</summary>
        public bool SweepSaw(Vector3 from, Vector3 direction, float distance, float radius, float thickness, out float hitDistance)
        {
            if (hitBones == null) {
                var bones = GetComponentsInChildren<Transform>(true);
                hitBones = HitBones.Select(n => bones.FirstOrDefault(t => t.name == n)).ToArray();
            }
            hitDistance = distance; bool found = false;
            var ray = new Ray(from, direction);
            var normal = Quaternion.LookRotation(direction, Vector3.up) * Vector3.up;
            float Extent(float n) => radius * Mathf.Sqrt(Mathf.Max(0, 1 - n * n)) + thickness * Mathf.Abs(n);
            var disc = new Vector3(Extent(normal.x), Extent(normal.y), Extent(normal.z));
            for (int i = 0; i < hitBones.Length; i++) {
                var a = hitBones[i]; var b = hitBones[HitParents[i]];
                if (a == null || b == null) continue;
                float flesh = (i <= 2 ? .15f : .075f) * (Placement != null ? Placement.SizeScale : 1);
                var bounds = new Bounds(a.position, Vector3.zero); bounds.Encapsulate(b.position);
                bounds.Expand((disc + Vector3.one * flesh) * 2);
                float d = 0;
                if (!bounds.Contains(from) && !bounds.IntersectRay(ray, out d)) continue;
                if (d < 0 || d > hitDistance) continue;
                hitDistance = d; found = true;
            }
            return found;
        }
    }
}
