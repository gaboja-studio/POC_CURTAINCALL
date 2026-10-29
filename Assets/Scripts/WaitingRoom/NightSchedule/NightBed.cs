using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 숙소 침대 F: 밤 정비 준비 완료/취소. M2에서 스케줄표의 준비 입력을 침대로 옮겼다(Task-079, 기준 M2 변경점 3-3).
    /// 전원이 준비하면 기존 밤 종료 흐름(<see cref="CampaignRun.SetReady"/>)으로 다음 날 아침이 된다. 스케줄표 확정은 조건이 아니다.
    /// 밤 정비 때만 닿는다. 침대 배정(누가 어느 침대)·눕는 연출은 기획 미정이라 어느 침대에서든 내 준비를 바꾼다.
    /// </summary>
    public sealed class NightBed : MonoBehaviour, IShopTarget, IShopPresentation
    {
        [Tooltip("닿는 거리를 재는 침대 몸체(없으면 이 오브젝트).")]
        [SerializeField] Transform body;

        [Tooltip("침대 가장자리에서 닿는 수평 거리(m).")]
        [SerializeField, Min(.3f)] float reach = 1.2f;

        Bounds footprint;
        bool inReach;

        public Transform VisualRoot => Body;
        public Vector3 PresentationPoint => footprint.center + Vector3.up * (footprint.extents.y + .3f);
        public bool IsAvailable => IsNight(out _);
        public string ActionLabel => !IsNight(out var run) ? "밤 정비 때 쓸 수 있습니다"
            : run.IsLocalReady ? $"일어나기 (준비 취소 · {run.State.ReadyCount}/{run.State.RequiredCount})"
            : $"침대에 눕기 (준비 완료 · {run.State.ReadyCount}/{run.State.RequiredCount})";

        Transform Body => body != null ? body : transform;
        public Bounds Footprint => footprint;
        public int BedIndex => System.Array.IndexOf(AllBeds(), this);
        public static NightBed[] AllBeds() => System.Linq.Enumerable.OrderBy(
            FindObjectsByType<NightBed>(FindObjectsSortMode.None), b => b.name, System.StringComparer.Ordinal).ToArray();

        void Awake() => footprint = Measure();

        void OnEnable() => ShopReach.Add(this);
        void OnDisable() => ShopReach.Remove(this);

        void Update()
        {
            ShopReach.Pump();
            inReach = ShopReach.Focused == (IShopTarget)this;
        }

        /// <summary>밤 정비 때만 닿는다. 낮에는 다른 대상의 F를 가로채지 않는다.</summary>
        public float Reach(Vector3 feet)
        {
            if (!IsNight(out _)) return -1f;
            float dx = Mathf.Max(0f, Mathf.Abs(feet.x - footprint.center.x) - footprint.extents.x);
            float dz = Mathf.Max(0f, Mathf.Abs(feet.z - footprint.center.z) - footprint.extents.z);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            return d <= reach ? d : -1f;
        }

        public void Interact()
        {
            if (!IsNight(out var run)) return;
            if (!run.IsLocalReady)
            {
                var player = CurtainCall.Network.PlayerSync.NetworkPlayer.Local;
                if (player != null) player.SetRestingBed(BedIndex);
                var rest = GetComponent<BedRestView>();
                if (rest == null) rest = gameObject.AddComponent<BedRestView>();
                rest.Begin(footprint, Body.rotation);
            }
            run.SetReady(!run.IsLocalReady);
        }

        static bool IsNight(out CampaignRun run)
        {
            run = CampaignRun.Instance;
            return run != null && run.State.Phase == CampaignPhase.Night;
        }

        Bounds Measure()
        {
            var renderers = Body.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Body.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        void OnGUI()
        {
            if (!inReach || ShopReach.HasExternalPresentation || InputGate.IsBlocked) return;
            GUI.Box(new Rect((Screen.width - 320) / 2f, Screen.height * .62f, 320, 28), "[F] " + ActionLabel);
        }
    }
}
