using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>
    /// 봉의 고정 기준점에 왕복 톱을 맞춘다. 움직임·동기화·접촉 부상은 기존 HazardCourse가 처리한다.
    /// 흔들리는 VisualBar나 매달린 플레이어를 따라가지 않아 W/S 반동으로 톱 평면을 벗어날 수 있다.
    /// </summary>
    [DefaultExecutionOrder(-25), DisallowMultipleComponent, RequireComponent(typeof(HumanCannonHazard))]
    public sealed class HumanCannonPoleSaw : MonoBehaviour
    {
        [SerializeField] HumanCannonPole pole;
        [SerializeField] HumanCannonHazard hazard;
        [Tooltip("반동하지 않는 발 위치에서 몸통 중심까지 높이(m). 0이면 기본 캐릭터 캡슐 높이의 절반."), Min(0f)]
        [SerializeField] float bodyCenterHeight;
        [Tooltip("봉의 X 위치와 무관하게 코스 중앙을 기준으로 좌우 왕복한다.")]
        [SerializeField] bool acrossMap;
        [SerializeField] float mapCenterX;

        public HumanCannonPole Pole => pole;
        public HumanCannonHazard Hazard => hazard != null ? hazard : GetComponent<HumanCannonHazard>();
        public float BodyCenterHeight => bodyCenterHeight > 0 ? bodyCenterHeight : GameSettings.Base.Character.CapsuleHeight * 0.5f;
        public Vector3 RestCenter => pole != null ? pole.HangingPosition(0) + Vector3.up * BodyCenterHeight : transform.position;
        public Vector3 RailCenter => acrossMap ? new Vector3(mapCenterX, RestCenter.y, RestCenter.z) : RestCenter;

        /// <summary>씬 작성 시 참조를 연결하고 저장할 고정 레일 위치를 즉시 맞춘다.</summary>
        public void Configure(HumanCannonPole source, HumanCannonHazard linkedHazard, float centerHeight = 0f)
        {
            pole = source;
            hazard = linkedHazard != null ? linkedHazard : GetComponent<HumanCannonHazard>();
            bodyCenterHeight = centerHeight;
            acrossMap = false;
            AlignToPole();
        }

        /// <summary>현재 인간 대포 코스의 월드 X 양 끝을 잇는다. 앞뒤 반동과 톱의 고정 평면은 분리한다.</summary>
        public void ConfigureAcrossMap(HumanCannonPole source, HumanCannonHazard linkedHazard,
            float leftX, float rightX, float seconds)
        {
            if (!float.IsFinite(leftX) || !float.IsFinite(rightX) || rightX <= leftX
                || !float.IsFinite(seconds) || seconds < 0.1f)
                throw new System.ArgumentOutOfRangeException(nameof(rightX));
            Configure(source, linkedHazard, bodyCenterHeight);
            acrossMap = true;
            mapCenterX = (leftX + rightX) * 0.5f;
            if (!AlignToPole()) throw new System.InvalidOperationException("봉과 독립 왕복 톱 연결이 필요합니다.");
            var target = Hazard;
            target.ConfigureSweep(target.transform.InverseTransformVector(Vector3.right * ((rightX - leftX) * 0.5f)), seconds);
            if (target.TryGetComponent(out LineRenderer rail))
            {
                rail.positionCount = 2;
                Vector3 half = target.Travel;
                rail.SetPosition(0, rail.useWorldSpace ? RailCenter - half : rail.transform.InverseTransformPoint(RailCenter - half));
                rail.SetPosition(1, rail.useWorldSpace ? RailCenter + half : rail.transform.InverseTransformPoint(RailCenter + half));
            }
        }

        public bool AlignToPole()
        {
            var target = Hazard;
            if (pole == null || target == null || !float.IsFinite(bodyCenterHeight) || bodyCenterHeight < 0
                || pole.Grip == target.transform || pole.Grip.IsChildOf(target.transform)) return false;
            Vector3 forward = acrossMap ? Vector3.forward : pole.Forward;
            if (forward.sqrMagnitude < 0.0001f) return false;
            target.transform.SetPositionAndRotation(RailCenter, Quaternion.LookRotation(forward, Vector3.up));
            return true;
        }

        void OnEnable() => AlignToPole();
        void LateUpdate() => AlignToPole();
    }
}
