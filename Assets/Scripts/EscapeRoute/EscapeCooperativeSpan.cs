using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>A two-leaf service bridge. The lock owns progress; this component only presents that state.</summary>
    [DefaultExecutionOrder(-80)]
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
            bool changed = Rotate(leftLeaf, raisedAngle * (1 - amount));
            changed |= Rotate(rightLeaf, -raisedAngle * (1 - amount));
            // 전개 여부는 통행 준비 상태다. 눈에 보이는 철판의 물리를 끄지 않는다.
            foreach (var collider in deckColliders)
                if (collider != null && (!collider.enabled || collider.isTrigger))
                {
                    collider.enabled = true;
                    collider.isTrigger = false;
                    changed = true;
                }
            if (changed) Physics.SyncTransforms();
        }

        static bool Rotate(Transform leaf, float angle)
        {
            if (leaf == null) return false;
            Quaternion rotation = Quaternion.Euler(0, 0, angle);
            if (Quaternion.Angle(leaf.localRotation, rotation) < .0001f) return false;
            leaf.localRotation = rotation;
            return true;
        }
    }
}
