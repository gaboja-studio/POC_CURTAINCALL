using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>이 공간에서 공통 상호작용 표시 씬을 사용한다.</summary>
    public sealed class InteractionSceneBinding : MonoBehaviour
    {
        void OnEnable() => CommonUiSceneLoader.RegisterInteraction(this);
        void OnDisable() => CommonUiSceneLoader.UnregisterInteraction(this);
    }
}
