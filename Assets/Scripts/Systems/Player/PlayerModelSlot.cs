using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 플레이어 겉모습(모델) 슬롯. 모델 프리팹을 바꿔 끼워도 조작·충돌(CharacterController)은 그대로다.
    /// 모델은 슬롯 아래에 생성되며, 선택하면 캐릭터 키에 맞춰 크기를 맞추고 발을 바닥(슬롯 원점)에 붙인다.
    /// 모델 프리팹 위치: Assets/Resources/Prefabs/Characters/Players/Models/
    /// 맞출 키는 게임 기본 세팅(캐릭터 · 모델 맞춤 키)에서 모델을 끼울 때 읽는다.
    /// 앉으면(<see cref="PlayerMover.PostureHeightScale"/>) 슬롯을 발 기준으로 위아래만 줄여 겉모습 높이를 낮춘다(전용 애니메이션 없음, 2026-10-19 PM).
    /// 원래 슬롯 크기를 기준으로 매 프레임 다시 정하므로 누적되지 않고, 모델을 재거나 떼어 낼 때는 원래 크기로 돌려 둔다.
    /// 이 축소는 앉기 애니메이션이 생기기 전 임시 표현이다. 애니메이션을 넣으면 <see cref="squashWhenCrouched"/>를 끄고
    /// 애니메이터는 <see cref="PlayerMover.IsCrouched"/>(남의 캐릭터도 온라인으로 맞춰짐)를 읽는다. 속도·충돌 높이·눈높이는 그대로 이동 부품이 맡는다.
    /// </summary>
    public class PlayerModelSlot : MonoBehaviour
    {
        [Tooltip("모델이 붙을 위치. 비워 두면 이 오브젝트 아래에 'ModelSlot'을 만든다. 원점이 발 위치.")]
        [SerializeField] Transform slot;

        [Tooltip("시작할 때 끼울 모델 프리팹. 비워 두면 슬롯에 이미 있는 모델을 그대로 쓴다.")]
        [SerializeField] GameObject modelPrefab;

        [Tooltip("모델 크기를 캐릭터 키(세팅 · 모델 맞춤 키)에 맞출지. 끄면 프리팹 크기 그대로.")]
        [SerializeField] bool fitToHeight = true;

        [Tooltip("앉으면 겉모습을 위아래로 줄여 낮출지(임시 표현). 앉기 애니메이션을 넣으면 끈다. 꺼도 속도·충돌 높이·1인칭 눈높이는 그대로 낮아진다.")]
        [SerializeField] bool squashWhenCrouched = true;

        PlayerMover mover;
        Vector3 neutralScale = Vector3.one; // 자세로 줄이기 전 슬롯 크기
        bool scaleCaptured;

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

        /// <summary>외형 생성·분리·교체가 끝났을 때. 현재 모델은 null일 수 있다.</summary>
        public event System.Action ModelChanged;

        /// <summary>슬롯에서 떼어 낸 모델을 넘겨준다(래그돌 등). 슬롯은 비고, 다시 끼우려면 <see cref="SetModel"/>을 부른다.</summary>
        public GameObject DetachModel()
        {
            var model = CurrentModel;
            if (model != null)
            {
                // 앉은 채 떼어 내도 줄어든 크기가 떨어진 모델(래그돌)에 남지 않게 원래 크기로 돌린 뒤 뗀다
                SetPostureScale(1f);
                model.transform.SetParent(null, true);
            }
            CurrentModel = null;
            ModelChanged?.Invoke();
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
            if (prefab == null) { ModelChanged?.Invoke(); return; }

            CurrentModel = Instantiate(prefab, slot, false);
            CurrentModel.name = prefab.name;

            // 충돌은 CharacterController가 맡는다. 모델의 콜라이더는 끄고 물리는 멈춰 둔다(추락 래그돌 때 켠다)
            foreach (var c in CurrentModel.GetComponentsInChildren<Collider>())
                c.enabled = false;
            foreach (var body in CurrentModel.GetComponentsInChildren<Rigidbody>())
                body.isKinematic = true;

            if (fitToHeight)
            {
                // 기울어지거나 앉은 상태에서 바꿔도 키를 정확히 재도록 잠시 슬롯을 세우고 원래 크기로 둔다
                Quaternion tilt = slot.localRotation;
                slot.localRotation = Quaternion.identity;
                SetPostureScale(1f);
                FitToHeight(CurrentModel.transform);
                slot.localRotation = tilt;
                ApplyPosture();
            }
            ModelChanged?.Invoke();
        }

        void Awake()
        {
            mover = GetComponent<PlayerMover>();
            EnsureSlot();
            if (modelPrefab != null)
                SetModel(modelPrefab);
            else if (slot.childCount > 0)
                CurrentModel = slot.GetChild(0).gameObject;
        }

        void EnsureSlot()
        {
            if (slot == null)
            {
                slot = new GameObject("ModelSlot").transform;
                slot.SetParent(transform, false);
            }
            if (!scaleCaptured)
            {
                neutralScale = slot.localScale;
                scaleCaptured = true;
            }
        }

        // 이동(Update) 뒤, 균형 기울기 등 다른 겉모습 연출과 같은 프레임에 반영한다. 기울기(회전)는 건드리지 않는다
        void LateUpdate() => ApplyPosture();

        void OnDisable()
        {
            if (slot != null) SetPostureScale(1f);
        }

        void ApplyPosture() => SetPostureScale(squashWhenCrouched && mover != null ? mover.PostureHeightScale : 1f);

        /// <summary>슬롯의 위아래 크기만 원래 크기 × <paramref name="heightScale"/>로 둔다. 슬롯 원점이 발이므로 발은 그대로다.</summary>
        void SetPostureScale(float heightScale)
        {
            if (slot == null) return;
            Vector3 scale = neutralScale;
            scale.y *= Mathf.Clamp(heightScale, 0.01f, 1f);
            if (slot.localScale != scale) slot.localScale = scale;
        }

        /// <summary>모델 전체 높이를 캐릭터 키에 맞추고, 바닥을 슬롯 원점에 붙인다.</summary>
        void FitToHeight(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = WorldBounds(renderers);
            float height = bounds.size.y;
            if (height < 0.0001f) return;

            model.localScale *= GameSettings.Base.Character.ModelHeight / height;

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
