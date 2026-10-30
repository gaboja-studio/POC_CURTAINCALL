using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>구조 진행 값만 연결하는 Image. 스프라이트와 채움 방식은 프리팹 인스펙터에서 편집한다.</summary>
    [ExecuteAlways]
    public sealed class RescueProgressRing : Image
    {
        public Sprite Sprite { get => sprite; set => sprite = value; }
        public float Progress { get => fillAmount; set => fillAmount = Mathf.Clamp01(value); }
    }
}
