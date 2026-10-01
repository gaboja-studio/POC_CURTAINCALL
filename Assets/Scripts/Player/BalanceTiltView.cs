using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 겉모습 연출: 균형 값에 따라 모델 슬롯을 좌우로 기울인다(코스 앞 방향이 축, 발이 회전 중심).
    /// 모델 슬롯만 돌리므로 충돌·이동(CharacterController)에는 영향이 없다. 애니메이션은 슬롯 아래 모델에서 따로 돌아간다.
    /// 균형이 꺼져 있으면(줄 밖) 똑바로 선다. 추락하면 마지막 기울기를 유지한다.
    /// </summary>
    [RequireComponent(typeof(PlayerBalance))]
    [RequireComponent(typeof(PlayerModelSlot))]
    public class BalanceTiltView : MonoBehaviour
    {
        [Tooltip("균형이 끝(±100)일 때 기울기 각도(도).")]
        [SerializeField, Range(0f, 90f)] float maxTiltAngle = 25f;

        [Tooltip("목표 기울기를 따라가는 빠르기. 클수록 바로 따라가고 작을수록 부드럽다.")]
        [SerializeField, Min(0.1f)] float followSharpness = 10f;

        PlayerBalance balance;
        PlayerModelSlot modelSlot;
        float currentAngle;

        /// <summary>지금 기울기 각도(도). + 오른쪽, − 왼쪽. 디버그 표시용.</summary>
        public float CurrentAngle => currentAngle;

        void Awake()
        {
            balance = GetComponent<PlayerBalance>();
            modelSlot = GetComponent<PlayerModelSlot>();
        }

        void LateUpdate()
        {
            if (balance.HasFallen) return; // 무너진 자세 유지

            float target = balance.IsActive ? balance.Value / PlayerBalance.MaxValue * maxTiltAngle : 0f;
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            currentAngle = Mathf.Lerp(currentAngle, target, t);

            // 앞 방향(로컬 Z)을 축으로 회전. +Z 회전은 왼쪽으로 기울므로 부호를 뒤집는다
            modelSlot.Slot.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);
        }
    }
}
