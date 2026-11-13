using UnityEngine;
using UnityEngine.EventSystems;

namespace CurtainCall.UI.EscapeResultsKit
{
    public sealed class EscapeResultsFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public UnityEngine.UI.Image Graphic;
        public void OnSelect(BaseEventData _) { if(Graphic!=null) Graphic.enabled=true; }
        public void OnDeselect(BaseEventData _) { if(Graphic!=null) Graphic.enabled=false; }
        void OnDisable() { if(Graphic!=null) Graphic.enabled=false; }
    }
}
