using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed class EscapeBankPilot : MonoBehaviour
    {
        [SerializeField] EscapeCuttableCable[] strands;
        [SerializeField] Renderer litLens;

        void OnEnable()
        {
            if (strands == null || !litLens) return;
            foreach (var strand in strands)
                if (strand) strand.CutStateChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (strands == null) return;
            foreach (var strand in strands)
                if (strand) strand.CutStateChanged -= Refresh;
        }

        void Refresh()
        {
            foreach (var strand in strands)
                if (strand && !strand.IsCut) { litLens.enabled = true; return; }
            litLens.enabled = false;
        }
    }
}
