using UnityEngine;
using CurtainCall.Network.Session;

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
        float transitionAmount;
        double transitionAt;
        double Clock => NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.NetworkManager != null
            && NetworkSessionManager.Instance.NetworkManager.IsListening
            ? NetworkSessionManager.Instance.NetworkManager.ServerTime.Time : Time.unscaledTimeAsDouble;
        public bool DeploymentRequested => deployed;
        public float DeploymentFraction => amount;
        public bool SupportsFeet(Vector3 feet)
        {
            if(!Traversable)return false;
            foreach(var deck in deckColliders) if(deck!=null && deck.enabled &&
                deck.Raycast(new Ray(feet+Vector3.up*.2f,Vector3.down),out _, .45f))return true;
            return false;
        }
        public bool Traversable => deployed && amount >= 1;
        public float AmountAt(double now) => Mathf.MoveTowards(transitionAmount, deployed ? 1 : 0,
            (float)System.Math.Max(0, now - transitionAt) / Mathf.Max(.1f, deploySeconds));

        public void SetDeployed(bool value)
        {
            if (deployed != value)
            {
                transitionAmount = AmountAt(Clock); transitionAt = Clock; deployed = value;
                if(CurtainCall.Network.Session.NetworkSessionManager.Instance?.IsHost==true) CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Mechanism(null,this,"DRAWBRIDGE",value?"DEPLOYING":"RETRACTING",transform.position,true);
            }
            amount = AmountAt(Clock); Present();
        }
        // Snapshot time is server time, so delayed arrival catches up instead of restarting the bridge.
        public void SynchronizeDeployment(bool value, float atSnapshot, double snapshotTime)
        {
            deployed = value; transitionAmount = atSnapshot; transitionAt = snapshotTime;
            amount = AmountAt(Clock); Present();
        }
        void OnEnable() { amount = transitionAmount = 0; transitionAt = Clock; Present(); }
        void Update()
        {
            amount = AmountAt(Clock);
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
