using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 숙소 침대 F: 밤 정비 준비 완료/취소. M2에서 스케줄표의 준비 입력을 침대로 옮겼다(Task-079, 기준 M2 변경점 3-3).
    /// 일정과 무관하게 누울 수 있다. 살아 있는 전원이 누우면 기존 밤 종료 흐름(<see cref="CampaignRun.SetReady"/>)으로 다음 날 아침이 된다.
    /// 밤 정비 때만 닿는다. 침대 배정(누가 어느 침대)·눕는 연출은 기획 미정이라 어느 침대에서든 내 준비를 바꾼다.
    /// </summary>
    public sealed class NightBed : MonoBehaviour, IShopTarget, IShopPresentation
    {
        [Tooltip("닿는 거리를 재는 침대 몸체(없으면 이 오브젝트).")]
        [SerializeField] Transform body;

        [Tooltip("침대 가장자리에서 닿는 수평 거리(m).")]
        float CommonReach => CurtainCall.Settings.GameSettings.Base.Interaction.Distance;

        Bounds footprint;
        bool inReach;

        public Transform VisualRoot => Body;
        public Vector3 PresentationPoint => footprint.center + Vector3.up * (footprint.extents.y + .3f);
        public static string UnscheduledTitle => WaitingRoomText.Get("night.bed-title", "잠자기");
        public static string UnscheduledNotice => WaitingRoomText.Get("night.bed-unavailable", "리허설이나 포획 중에는 잠들 수 없습니다.");

        public bool IsAvailable => LocalCanInteract && IsNight(out var run) && (run.IsLocalReady || CanSleep);
        static bool LocalCanInteract => CurtainCall.Network.PlayerSync.NetworkPlayer.Local == null
            || PlayerInteraction.CanInteract(CurtainCall.Network.PlayerSync.NetworkPlayer.Local);
        public string ActionLabel => !IsNight(out var run) ? WaitingRoomText.Get("night.bed-not-night", "밤 정비 때 쓸 수 있습니다")
            : run.IsLocalReady ? WaitingRoomText.Format("night.bed-get-up", "일어나기 (준비 취소 · {ready}/{total})", ("ready", run.State.ReadyCount), ("total", run.State.RequiredCount))
            : WaitingRoomText.Format("night.bed-lie-down", "침대에 눕기 (준비 완료 · {ready}/{total})", ("ready", run.State.ReadyCount), ("total", run.State.RequiredCount));

        /// <summary>일정 호환 조회. 일정 선택은 수면을 막지 않는다.</summary>
        public static bool ScheduleReady => ScheduleBoard.Instance == null || ScheduleBoard.Instance.NightCanEnd();
        static bool CanSleep
        {
            get
            {
                var player = CurtainCall.Network.PlayerSync.NetworkPlayer.Local;
                return !WaitingRoomCannonRehearsal.IsActive && (player == null || player.State == CurtainCall.Network.PlayerSync.PlayerState.Normal
                    && !Pursuit.PursuitDirector.IsCaptured(player));
            }
        }

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
            bool wasInReach = inReach;
            inReach = ShopReach.Focused == (IShopTarget)this;
            // 침대에 다가갔을 때 아직 잘 수 없으면 바로 안내한다.
            if (inReach && !wasInReach && !IsAvailable && IsNight(out _)) WaitingRoomTutorial.ShowNotice(UnscheduledTitle, UnscheduledNotice);
        }

        /// <summary>밤 정비 때만 닿는다. 낮에는 다른 대상의 F를 가로채지 않는다.</summary>
        public float Reach(Vector3 feet, Component actor = null)
        {
            if (!IsNight(out _)) return -1f;
            return ExplorationReach.From(VisualRoot, PresentationPoint, feet, CommonReach, actor: actor);
        }

        public void Interact()
        {
            if (!LocalCanInteract || !IsNight(out var run)) return;
            if (!IsAvailable) { WaitingRoomTutorial.ShowNotice(UnscheduledTitle, UnscheduledNotice); return; }
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
            GUI.Box(new Rect((Screen.width - 320) / 2f, Screen.height * .62f, 320, 28), (IsAvailable ? "[F] " : "") + ActionLabel);
        }
    }
}
