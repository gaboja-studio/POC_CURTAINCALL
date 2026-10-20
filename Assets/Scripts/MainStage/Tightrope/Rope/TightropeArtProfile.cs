using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>Opt-in visual references. Existing course geometry and gameplay remain authoritative.</summary>
    [CreateAssetMenu(menuName = "CurtainCall/Tightrope Art Profile")]
    public sealed class TightropeArtProfile : ScriptableObject
    {
        public GameObject floor, rope, safetyNet;
        public GameObject relayRope;
        public GameObject barX, barY, cableY;
    }
}
