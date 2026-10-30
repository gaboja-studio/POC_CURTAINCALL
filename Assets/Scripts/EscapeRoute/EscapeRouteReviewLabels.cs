using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>배치 검토용 라벨은 에디터에 보존하고 플레이 화면에서는 숨긴다.</summary>
    [DisallowMultipleComponent]
    public sealed class EscapeRouteReviewLabels : MonoBehaviour
    {
        [Tooltip("플레이 중에도 Label_로 시작하는 제작 라벨을 표시한다. 변경 후 컴포넌트를 다시 켜면 적용된다.")]
        [SerializeField] bool showReviewLabelsInPlayMode;
        readonly Dictionary<Renderer, bool> previous = new();

        void OnEnable()
        {
            if (!Application.isPlaying || showReviewLabelsInPlayMode) return;
            foreach (var text in GetComponentsInChildren<TextMeshPro>(true))
            {
                if (!text.name.StartsWith("Label_", StringComparison.Ordinal)) continue;
                foreach (var renderer in text.GetComponentsInChildren<Renderer>(true))
                {
                    if (!previous.ContainsKey(renderer)) previous.Add(renderer, renderer.enabled);
                    renderer.enabled = false;
                }
            }
        }

        void OnDisable()
        {
            foreach (var item in previous)
                if (item.Key != null) item.Key.enabled = item.Value;
            previous.Clear();
        }
    }
}
