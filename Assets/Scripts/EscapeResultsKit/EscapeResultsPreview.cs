using UnityEngine;

namespace CurtainCall.UI.EscapeResultsKit
{
    // Present only in the preview scene, never on the production Canvas prefab.
    public sealed class EscapeResultsPreview : MonoBehaviour
    {
        public EscapeResultsView View;
        int requests;
        void EnsureInputModule()
        {
            var events=UnityEngine.EventSystems.EventSystem.current;
            if(events==null || events.GetComponent<UnityEngine.EventSystems.BaseInputModule>()!=null)return;
            var inputType=System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if(inputType!=null)events.gameObject.AddComponent(inputType);
            else events.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        void Start()
        {
            EnsureInputModule();
            View.SetVisible(true); View.SetReturnAllowed(true);
            View.ReturnRequested.AddListener(PreviewReturn);
        }
        void OnDestroy() { if(View!=null)View.ReturnRequested.RemoveListener(PreviewReturn); }
        void PreviewReturn()
        {
            requests++;
            Debug.Log("EscapeResults kit preview return request: "+requests+" (no scene transition)");
            View.SetReturnAllowed(true,"예시 데이터 · 로비 이동은 연결되지 않았습니다");
        }
    }
}
