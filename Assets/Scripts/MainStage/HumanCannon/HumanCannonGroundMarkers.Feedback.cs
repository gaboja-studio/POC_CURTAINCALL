using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonGroundMarkers
    {
        [Tooltip("켜면 내 캐릭터(관전 대상)가 안전 체크 높이 안에서 아래 '외줄 착지 강조' 목록의 외줄에 안전하게 내려설 때, 외줄 위에 짧은 안전 색 강조를 띄운다. 그림자 외곽선의 안전/비안전 색은 이 값과 상관없이 공통 설정(Base Game Settings > 착지 표시)을 따른다.")]
        [SerializeField] bool showSafeLandingFeedback;
        [Tooltip("안전 체크 높이 안에서 안전 착지일 때 짧은 착지 강조를 표시할 실제 외줄 충돌체. 크기/충돌/성공 판정은 바꾸지 않는다.")]
        [SerializeField] BoxCollider[] feedbackRopes = System.Array.Empty<BoxCollider>();
        const float TargetOutlineWidth = .17f; // 외줄 강조 표시의 기존 테두리 폭(반지름 1 기준)
        Transform landingTarget;

        protected override bool ShowFocusTarget(bool safe, Collider surface, Vector3 point)
            => ShowLandingTarget(safe ? surface as BoxCollider : null, point);

        protected override void HideFocusTarget()
        {
            if (landingTarget != null) landingTarget.gameObject.SetActive(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (landingTarget != null) Destroy(landingTarget.gameObject);
        }

        bool ShowLandingTarget(BoxCollider rope, Vector3 point)
        {
            if (!showSafeLandingFeedback || rope == null || !rope.enabled || !rope.gameObject.activeInHierarchy
                || System.Array.IndexOf(feedbackRopes, rope) < 0) return false;
            if (landingTarget == null)
            {
                landingTarget = CreateMarker(ulong.MaxValue);
                landingTarget.name = "LocalRopeLandingTarget";
            }
            var safeColor = GameSettings.Base.LandingMarker.SafeColor;
            Paint(landingTarget, new Color(safeColor.r, safeColor.g, safeColor.b, 1), TargetOutlineWidth, 0,
                new Color(safeColor.r, safeColor.g, safeColor.b, .65f));
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
