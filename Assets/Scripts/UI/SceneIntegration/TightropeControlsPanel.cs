using CurtainCall.Tightrope.UI;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    public sealed class TightropeControlsPanel : MonoBehaviour
    {
        public CanvasGroup group;
        public TMP_Text text;
        void OnEnable()=>TightropeControlHints.ExternalPresentation=true;
        void LateUpdate()
        {
            string value=TightropeControlHints.CurrentText();
            group.alpha=string.IsNullOrEmpty(value)?0:1;
            group.interactable=group.blocksRaycasts=false;
            if(text.text!=value)text.text=value??"";
        }
        void OnDisable(){TightropeControlHints.ExternalPresentation=false;if(group!=null)group.alpha=0;}
    }
}
