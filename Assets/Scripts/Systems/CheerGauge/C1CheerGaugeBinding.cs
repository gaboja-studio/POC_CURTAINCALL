using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.CheerGauge
{
    /// <summary>공유 보상 상태를 읽기만 하는 HUD 연결.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(C1CheerGaugeView))]
    public sealed class C1CheerGaugeBinding : MonoBehaviour
    {
        [SerializeField] C1CheerGaugeView view;
        readonly C1GaugeProjection projection = new();
        PerformanceRewards source;
        string identity;

        void Awake() { if (view == null) view = GetComponent<C1CheerGaugeView>(); }
        void OnEnable() => Bind();
        void Start() => Refresh(false);
        void Update()
        {
            if (!ReferenceEquals(source, PerformanceRewards.Instance)) Bind();
        }
        void OnDisable()
        {
            Unsubscribe();
            projection.Reset();
            identity = null;
        }

        void Bind()
        {
            Unsubscribe();
            projection.Reset();
            identity = null;
            source = PerformanceRewards.Instance;
            if (source != null) source.Changed += OnChanged;
            Refresh(false);
        }
        void Unsubscribe()
        {
            if (!ReferenceEquals(source, null)) source.Changed -= OnChanged;
            source = null;
        }
        void OnChanged() => Refresh(true);
        void Refresh(bool animate)
        {
            if (view == null) return;
            if (source == null || !projection.TryProject(source.State, out var frame))
            {
                identity = null;
                view.SetUnavailable();
                return;
            }
            bool same = identity == frame.Identity;
            view.SetScores(frame.Scores, frame.Capacity, animate && same);
            identity = frame.Identity;
        }
    }
}
