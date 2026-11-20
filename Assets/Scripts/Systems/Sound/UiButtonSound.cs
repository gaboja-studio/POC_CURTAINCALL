using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CurtainCall.Sound
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonSound : MonoBehaviour, IPointerEnterHandler
    {
        Button button;

        public static void Bind(Transform root)
        {
            foreach (var item in root.GetComponentsInChildren<Button>(true)) Bind(item);
        }

        public static void Bind(Button item)
        {
            if (item != null && !item.TryGetComponent<UiButtonSound>(out _))
                item.gameObject.AddComponent<UiButtonSound>();
        }

        void Awake() => button = GetComponent<Button>();
        void OnEnable() => button.onClick.AddListener(PlayClick);
        void OnDisable() => button.onClick.RemoveListener(PlayClick);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button.IsActive() && button.IsInteractable())
                SoundManager.Play(SoundSituations.ButtonHover);
        }

        void PlayClick() => SoundManager.Play(SoundSituations.ButtonClick);
    }
}
