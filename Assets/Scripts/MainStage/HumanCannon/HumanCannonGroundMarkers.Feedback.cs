using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonGroundMarkers
    {
        [Tooltip("발밑의 실제 안전 착지면에 정렬됐을 때 초록 테두리. 낙하 궤적의 도착 예측은 아니다.")]
        [SerializeField] bool showSafeLandingFeedback;
        [Tooltip("짧은 착지 강조를 표시할 실제 외줄 충돌체. 크기/충돌/성공 판정은 바꾸지 않는다.")]
        [SerializeField] BoxCollider[] feedbackRopes = System.Array.Empty<BoxCollider>();
        MaterialPropertyBlock tint;
        static readonly int RimColor = Shader.PropertyToID("_RimColor");
        static readonly Color SafeColor = new(.08f, 1f, .3f, 1f);
        static readonly Color NeutralColor = new(.85f, .94f, 1f, 1f);
        Transform landingTarget;

        bool HasSafeLandingBelow(PlayerMover mover, Vector3 feet, Vector3 point, Collider surface)
        {
            if (mover == null || surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy
                || !HumanCannonSystem.IsSafeLandingSurface(mover, point)) return false;
            // 유효한 바닥 위에 다른 구조물이 가로막고 있으면 초록으로 안내하지 않는다.
            var origin = feet + Vector3.up * .05f;
            float distance = origin.y - point.y;
            int count;
            do
            {
                count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                System.Array.Resize(ref hits, hits.Length * 2);
            } while (true);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != surface && hits[i].distance < distance - .03f
                    && hits[i].collider.GetComponentInParent<NetworkPlayer>() == null) return false;
            return true;
        }

        void Tint(Transform marker, bool safe)
        {
            tint ??= new MaterialPropertyBlock();
            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(tint);
            tint.SetColor(RimColor, safe ? SafeColor : NeutralColor);
            renderer.SetPropertyBlock(tint);
        }

        bool ShowLandingTarget(BoxCollider rope, Vector3 point)
        {
            if (!showSafeLandingFeedback || rope == null || !rope.enabled || !rope.gameObject.activeInHierarchy
                || System.Array.IndexOf(feedbackRopes, rope) < 0) return false;
            if (landingTarget == null)
            {
                landingTarget = CreateMarker(ulong.MaxValue);
                landingTarget.name = "LocalRopeLandingTarget";
                Tint(landingTarget, true);
                tint.SetColor("_FillColor", new Color(SafeColor.r, SafeColor.g, SafeColor.b, .65f));
                landingTarget.GetComponent<MeshRenderer>().SetPropertyBlock(tint);
            }
            // 원래 지지면 안에서 최대 2.4m만 강조한다. 외줄 끝에서 낭떠러지로 돌출하지 않는다.
            var scale = rope.transform.lossyScale;
            float halfLength = Mathf.Min(rope.size.z * .5f, 1.2f / Mathf.Abs(scale.z));
            var local = rope.transform.InverseTransformPoint(point);
            local.x = rope.center.x;
            local.y = rope.center.y + rope.size.y * .5f;
            local.z = Mathf.Clamp(local.z, rope.center.z - rope.size.z * .5f + halfLength,
                rope.center.z + rope.size.z * .5f - halfLength);
            landingTarget.SetPositionAndRotation(rope.transform.TransformPoint(local) + Vector3.up * .035f,
                rope.transform.rotation);
            landingTarget.localScale = new Vector3(Mathf.Min(.12f, rope.size.x * Mathf.Abs(scale.x) * .45f),
                1, halfLength * Mathf.Abs(scale.z));
            landingTarget.gameObject.SetActive(true);
            return true;
        }
    }
}
