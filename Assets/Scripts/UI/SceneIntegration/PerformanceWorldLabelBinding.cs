using TMPro;
using CurtainCall.UI.WorldLabelsV2;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>공간의 기존 표지를 따라간다. 묘기별 위치/거리 문자열은 원래 씬의 표지가 소유한다.</summary>
    public sealed class PerformanceWorldLabelBinding : MonoBehaviour
    {
        [SerializeField] TMP_Text source;
        [SerializeField] WorldLabelView view;
        bool sourceEnabled;
        string previous;
        public TMP_Text Source => source;
        public WorldLabelView View => view;
        public void Configure(TMP_Text anchor, WorldLabelView label)
        { source = anchor; view = label; sourceEnabled = source != null && source.enabled; previous = null; }
        void OnEnable() { if (source != null) sourceEnabled = source.enabled; }
        void LateUpdate()
        {
            if (source == null || view == null) { if (view != null) view.SetVisible(false); return; }
            if (previous != source.text)
            {
                if (!view.TrySetContent(source.text)) { source.enabled = sourceEnabled; view.SetVisible(false); return; }
                previous = source.text;
            }
            source.enabled = false;
            transform.SetPositionAndRotation(source.transform.position + source.transform.forward * -.025f, source.transform.rotation);
            view.SetVisible(sourceEnabled && source.gameObject.activeInHierarchy);
        }
        void OnDisable()
        {
            if (source != null) source.enabled = sourceEnabled;
            if (view != null) view.SetVisible(false);
        }
    }
}
