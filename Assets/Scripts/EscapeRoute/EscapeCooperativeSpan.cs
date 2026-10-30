using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>A two-leaf service bridge. The lock owns progress; this component only presents that state.</summary>
    public sealed class EscapeCooperativeSpan : MonoBehaviour
    {
        [SerializeField] Transform leftLeaf, rightLeaf;
        [SerializeField] Collider[] deckColliders = System.Array.Empty<Collider>();
        [SerializeField, Min(.1f)] float deploySeconds = 1.4f;
        [SerializeField, Range(10, 85)] float raisedAngle = 68;
        bool deployed;
        float amount;
        public bool Traversable => deployed && amount >= 1;

        public void SetDeployed(bool value) { deployed = value; Present(); }
        void OnEnable() { amount = 0; Present(); }
        void Update()
        {
            amount = Mathf.MoveTowards(amount, deployed ? 1 : 0, Time.deltaTime / Mathf.Max(.1f, deploySeconds));
            Present();
        }
        void Present()
        {
            if (leftLeaf != null) leftLeaf.localRotation = Quaternion.Euler(0, 0, raisedAngle * (1 - amount));
            if (rightLeaf != null) rightLeaf.localRotation = Quaternion.Euler(0, 0, -raisedAngle * (1 - amount));
            foreach (var collider in deckColliders) if (collider != null) collider.enabled = Traversable;
        }
    }
}
