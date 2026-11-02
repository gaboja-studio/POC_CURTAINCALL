using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitActor
    {
        Renderer[] hiddenRenderers;
        bool[] priorRenderSuppression;
        public bool IsRendered => hiddenRenderers == null;
        public void SetRendered(bool draw)
        {
            if (draw) { RestoreRenderers(); return; }
            if (hiddenRenderers != null) return;
            hiddenRenderers = GetComponentsInChildren<Renderer>(true);
            priorRenderSuppression = new bool[hiddenRenderers.Length];
            for (int i = 0; i < hiddenRenderers.Length; i++) {
                priorRenderSuppression[i] = hiddenRenderers[i].forceRenderingOff;
                hiddenRenderers[i].forceRenderingOff = true;
            }
        }
        void RestoreRenderers()
        {
            if (hiddenRenderers == null) return;
            for (int i = 0; i < hiddenRenderers.Length; i++) if (hiddenRenderers[i] != null) hiddenRenderers[i].forceRenderingOff = priorRenderSuppression[i];
            hiddenRenderers = null; priorRenderSuppression = null;
        }
        void OnDisable() { RestoreRenderers(); brain?.End(); restartBrain = true; }
        public void ReceiveBehaviour(PursuitBehaviour value) => Behaviour = value;
    }
}
