using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 플레이어 겉모습(모델) 슬롯. 모델 프리팹을 바꿔 끼워도 조작·충돌(CharacterController)은 그대로다.
    /// 모델은 슬롯 아래에 생성되며, 선택하면 캐릭터 키에 맞춰 크기를 맞추고 발을 바닥(슬롯 원점)에 붙인다.
    /// 모델 프리팹 위치: Assets/Resources/Prefabs/Characters/Players/Models/
    /// </summary>
    public class PlayerModelSlot : MonoBehaviour
    {
        [Tooltip("모델이 붙을 위치. 비워 두면 이 오브젝트 아래에 'ModelSlot'을 만든다. 원점이 발 위치.")]
        [SerializeField] Transform slot;

        [Tooltip("시작할 때 끼울 모델 프리팹. 비워 두면 슬롯에 이미 있는 모델을 그대로 쓴다.")]
        [SerializeField] GameObject modelPrefab;

        [Tooltip("모델 크기를 캐릭터 키에 맞출지. 끄면 프리팹 크기 그대로.")]
        [SerializeField] bool fitToHeight = true;

        [Tooltip("맞출 캐릭터 키(m). 기획 1.73.")]
        [SerializeField, Min(0.1f)] float characterHeight = 1.73f;

        /// <summary>모델이 붙는 위치(원점 = 발). 기울기 등 겉모습 연출은 이 슬롯을 돌린다.</summary>
        public Transform Slot
        {
            get
            {
                EnsureSlot();
                return slot;
            }
        }

        /// <summary>지금 끼워진 모델 인스턴스. 없으면 null.</summary>
        public GameObject CurrentModel { get; private set; }

        /// <summary>지금 끼워진 모델 프리팹. 슬롯에 원래 있던 모델이면 null.</summary>
        public GameObject CurrentPrefab { get; private set; }

        /// <summary>슬롯에서 떼어 낸 모델을 넘겨준다(래그돌 등). 슬롯은 비고, 다시 끼우려면 <see cref="SetModel"/>을 부른다.</summary>
        public GameObject DetachModel()
        {
            var model = CurrentModel;
            if (model != null) model.transform.SetParent(null, true);
            CurrentModel = null;
            return model;
        }

        /// <summary>모델을 바꾼다. 기존 모델은 지운다. null이면 모델을 비운다.</summary>
        public void SetModel(GameObject prefab)
        {
            EnsureSlot();
            for (int i = slot.childCount - 1; i >= 0; i--)
                Destroy(slot.GetChild(i).gameObject);

            CurrentPrefab = prefab;
            CurrentModel = null;
            if (prefab == null) return;

            CurrentModel = Instantiate(prefab, slot, false);
            CurrentModel.name = prefab.name;

            // 충돌은 CharacterController가 맡는다. 모델의 콜라이더는 끄고 물리는 멈춰 둔다(추락 래그돌 때 켠다)
            foreach (var c in CurrentModel.GetComponentsInChildren<Collider>())
                c.enabled = false;
            foreach (var body in CurrentModel.GetComponentsInChildren<Rigidbody>())
                body.isKinematic = true;

            if (fitToHeight)
            {
                // 기울어진 상태에서 바꿔도 키를 정확히 재도록 잠시 슬롯을 세운다
                Quaternion tilt = slot.localRotation;
                slot.localRotation = Quaternion.identity;
                FitToHeight(CurrentModel.transform);
                slot.localRotation = tilt;
            }
        }

        void Awake()
        {
            EnsureSlot();
            if (modelPrefab != null)
                SetModel(modelPrefab);
            else if (slot.childCount > 0)
                CurrentModel = slot.GetChild(0).gameObject;
        }

        void EnsureSlot()
        {
            if (slot != null) return;
            slot = new GameObject("ModelSlot").transform;
            slot.SetParent(transform, false);
        }

        /// <summary>모델 전체 높이를 캐릭터 키에 맞추고, 바닥을 슬롯 원점에 붙인다.</summary>
        void FitToHeight(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = WorldBounds(renderers);
            float height = bounds.size.y;
            if (height < 0.0001f) return;

            model.localScale *= characterHeight / height;

            bounds = WorldBounds(renderers);
            Vector3 feet = slot.position;
            model.position += new Vector3(feet.x - bounds.center.x, feet.y - bounds.min.y, feet.z - bounds.center.z);
        }

        static Bounds WorldBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
