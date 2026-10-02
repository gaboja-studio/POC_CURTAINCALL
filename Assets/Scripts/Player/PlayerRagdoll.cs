using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 겉모습 연출: 균형이 무너져 추락하면 모델이 힘 빠진 래그돌이 되어 중력대로 무너진다.
    /// 모델만 몸통(CharacterController)에서 떨어져 나오고, 조작 잠금·대기·재시작 판단은 다른 기능이 한다.
    /// 래그돌은 다른 플레이어 몸통에 밀린다. 연출은 화면마다 따로 계산한다(물리 결과는 맞추지 않음).
    /// 균형을 되돌리면(재시작) 무너진 모델을 지우고 같은 모델을 다시 끼운다.
    /// 모델 준비: 부위마다 콜라이더 + Rigidbody(관절은 CharacterJoint). 없으면 모델 전체를 한 덩어리로 쓰러뜨린다.
    /// </summary>
    [RequireComponent(typeof(PlayerBalance))]
    [RequireComponent(typeof(PlayerModelSlot))]
    public class PlayerRagdoll : MonoBehaviour
    {
        [Tooltip("무너질 때 기울어 있던 쪽으로 밀어 주는 힘(질량 1당 m/s). 0이면 그냥 무너진다.")]
        [SerializeField, Min(0f)] float tiltPush = 1.5f;

        [Tooltip("Rigidbody가 없는 모델을 한 덩어리로 쓰러뜨릴 때의 질량.")]
        [SerializeField, Min(0.1f)] float fallbackMass = 5f;

        PlayerBalance balance;
        PlayerModelSlot modelSlot;
        CharacterController body;
        GameObject limpModel;
        GameObject limpPrefab;

        /// <summary>지금 래그돌 상태인지.</summary>
        public bool IsLimp => limpModel != null;

        /// <summary>모델을 래그돌로 무너뜨린다. 추락 신호에 자동으로 불린다.</summary>
        public void GoLimp()
        {
            if (IsLimp || modelSlot.CurrentModel == null) return;

            limpPrefab = modelSlot.CurrentPrefab;
            limpModel = modelSlot.DetachModel();

            var bodies = limpModel.GetComponentsInChildren<Rigidbody>();
            if (bodies.Length == 0)
            {
                var single = limpModel.AddComponent<Rigidbody>();
                single.mass = fallbackMass;
                bodies = new[] { single };
            }

            foreach (var c in limpModel.GetComponentsInChildren<Collider>())
            {
                c.enabled = true;
                // 자기 몸통과는 부딪히지 않는다. 다른 플레이어 몸통과는 부딪혀서 밀린다(PlayerMover가 민다, 2026-10-02 #16)
                if (body != null) Physics.IgnoreCollision(c, body);
            }

            // 기울어 있던 쪽(코스 기준 좌우)으로 살짝 밀어 그 방향으로 쓰러지게 한다
            Vector3 push = transform.right * (Mathf.Sign(balance.Value) * tiltPush);
            Vector3 carry = body != null ? body.velocity : Vector3.zero;
            foreach (var rb in bodies)
            {
                rb.isKinematic = false;
                rb.linearVelocity = carry + push;
            }
        }

        /// <summary>래그돌을 지우고 원래 모델을 다시 끼운다. 균형을 되돌리면 자동으로 불린다.</summary>
        public void Restore()
        {
            if (!IsLimp) return;
            Destroy(limpModel);
            limpModel = null;
            if (limpPrefab != null) modelSlot.SetModel(limpPrefab);
            else Debug.LogWarning("[PlayerRagdoll] 프리팹으로 끼운 모델이 아니라 되돌릴 수 없습니다. PlayerModelSlot의 Model Prefab을 지정하세요.", this);
        }

        void Awake()
        {
            balance = GetComponent<PlayerBalance>();
            modelSlot = GetComponent<PlayerModelSlot>();
            body = GetComponent<CharacterController>();
            balance.Fell += GoLimp;
            balance.BalanceReset += Restore;
        }

        void OnDestroy()
        {
            if (balance != null)
            {
                balance.Fell -= GoLimp;
                balance.BalanceReset -= Restore;
            }
            if (limpModel != null) Destroy(limpModel);
        }
    }
}
