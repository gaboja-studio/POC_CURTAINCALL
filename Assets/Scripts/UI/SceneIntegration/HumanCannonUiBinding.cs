using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.UI.GuidesV1;
using CurtainCall.UI.TooltipsV1;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>공간의 실제 상호작용 가능 여부를 공통 C7/C15에 투영한다. 입력/판정은 소유하지 않는다.</summary>
    [DefaultExecutionOrder(250)]
    [RequireComponent(typeof(PerformanceSceneBinding))]
    public sealed class HumanCannonUiBinding : MonoBehaviour
    {
        [SerializeField] HumanCannonCourseSections legacy;
        [SerializeField] HumanCannonCourseSections.Boundaries boundaries;
        [SerializeField] TMP_Text sectionLabel;
        PerformanceSceneBinding space;
        CommonPerformanceUI owned;
        CommonInteractionUI interaction;
        GuideView practiceGuide;
        TooltipView practiceTooltip;
        public RectTransform PracticeTooltipExclusion { get; set; }
        readonly Vector3[] exclusionCorners = new Vector3[4];
        GuideView Guide => practiceGuide != null ? practiceGuide : owned != null ? owned.Guide : null;
        TooltipView Tooltip => practiceTooltip != null ? practiceTooltip : owned != null ? owned.Tooltip : null;
        bool legacyEnabled;
        string shownGuide, shownTarget;
        static readonly string[] Sections = { "현재 구간 · 출발", "현재 구간 · 1구간", "현재 구간 · 쉼터", "현재 구간 · 2구간", "현재 구간 · 최종구간" };

        public void Configure(HumanCannonCourseSections previous, HumanCannonCourseSections.Boundaries values, TMP_Text section)
        { legacy = previous; boundaries = values; sectionLabel = section; }

        /// <summary>Reuse the original contextual views in an unscored practice course, without changing the waiting-room UI owner.</summary>
        public void ConfigurePractice(GuideView guide, TooltipView tooltip)
        { practiceGuide = guide; practiceTooltip = tooltip; }

        void OnEnable()
        {
            space = GetComponent<PerformanceSceneBinding>();
            if (legacy != null) { legacyEnabled = legacy.enabled; legacy.enabled = false; }
        }

        void OnDisable()
        {
            Clear();
            if (legacy != null) legacy.enabled = legacyEnabled;
        }

        void Clear()
        {
            HideGuide();
            if (owned != null) owned.ReleaseHints(this);
            if (practiceGuide != null) practiceGuide.HideImmediate();
            if (practiceTooltip != null) practiceTooltip.HideImmediate();
            owned = null; shownGuide = shownTarget = null;
            if (sectionLabel != null) sectionLabel.gameObject.SetActive(false);
            HideSection(); ResetLessons();
        }

        void LateUpdate()
        {
            bool practice = practiceGuide != null && practiceTooltip != null;
            if (practice)
            {
                if (HumanCannonSystem.Current == null || !HumanCannonSystem.Current.PracticeMode) { Clear(); return; }
            }
            else
            {
                if (CommonUiSceneLoader.CurrentSpace != space || CommonPerformanceUI.Current == null) { Clear(); return; }
                if (owned != CommonPerformanceUI.Current) { Clear(); owned = CommonPerformanceUI.Current; }
                owned.ClaimHints(this);
            }
            var player = NetworkPlayer.Local;
            var cannon = HumanCannonSystem.Current;
            bool available = player != null && player.IsSpawned && player.State == PlayerState.Normal
                && cannon != null && !InputGate.IsBlocked;
            if (sectionLabel && available)
            {
                string section=Sections[(int)HumanCannonCourseSections.SectionAt(player.transform.position,boundaries)];
                sectionLabel.gameObject.SetActive(false);
                if(section!=lastSection)
                {
                    lastSection=section;
                    HudUIManager.Current.ShowStatus(section,transform,bottomCenter:true);
                }
            }
            else HideSection();
            if (!practice && (player == null || player.State != PlayerState.Normal || HumanCannonDestination.Current?.PerformanceOpen != true)) ResetLessons();
            if (!available)
            {
                if (practice) { HideGuide(); HideTooltip(); }
                else { HideGuide(); owned.HideHints(); shownTarget = null; }
                return;
            }
            var pilot = player.GetComponent<HumanCannonPilot>();
            var trampoline = HumanCannonTrampoline.Current;
            var carried = HumanCannonCarry.Held(player.OwnerClientId);
            var carryTarget = HumanCannonCarry.FindGrabbable(player);
            var route = HumanCannonRouteSystem.Current;
            var poles = HumanCannonPoleSystem.Current;
            bool riding = route != null && route.IsRiding(player.OwnerClientId);
            bool seated = cannon.IsSeated(player.OwnerClientId);
            bool carrying = carried != null;
            bool holding = pilot != null && pilot.IsHoldingPole;
            var reader = player.GetComponent<PlayerInputReader>();
            bool holdingF = reader != null && reader.Current.InteractHoldProgress > 0;
            bool hasInteractTarget = poles != null && poles.FindGrabbable(player) >= 0
                || carryTarget != null || cannon.FindBoardableStation(player) >= 0;
            if (!practice && !space.PresentationOnly && HumanCannonDestination.Current?.PerformanceOpen == true)
            {
                var id=HumanCannonDestination.Current.PerformanceId;
                if (id != lessonRun || player != lessonPlayer) { ResetLessons();lessonRun=id;lessonPlayer=player; }
                var profile=PerformanceHudProfile.Current;
                if (holding) Teach(profile.cPole,reader);
                else if (seated) Teach(profile.cFire,reader);
                else if (carried is HumanCannonTrampoline) Teach(profile.cCarry,reader);
                else if (carryTarget is HumanCannonTrampoline) Teach(profile.cGrab,reader);
                else if (cannon.FindBoardableStation(player)>=0) Teach(profile.cBoard,reader);
            }
            if (space != null && !space.PresentationOnly && !holding && !seated && !cannon.IsFlying(player.OwnerClientId)
                && (carrying || riding || !hasInteractTarget) && ShowTransportHint(player, route, carried is HumanCannonTrampoline))
            { HideGuide(); return; }
            if (interaction == null) interaction = FindFirstObjectByType<CommonInteractionUI>();
            bool commonPrompt = interaction != null && interaction.isActiveAndEnabled && interaction.Prompt != null;
            if (commonPrompt && (holding || seated || carrying)) HideGuide();
            else if (holding) ShowAction(poles?.Pole(pilot.HeldPole)?.transform,reader != null ? reader.BindingLabel("Action", "Space") : "Space", "봉 놓기");
            else if (riding) HideGuide();
            else if (carrying) ShowAction(carried.Handle,(reader != null ? reader.BindingLabel("Interact", "F") : "F") + " 길게", carried.CarryName + " 놓기");
            else if (seated) ShowAction(cannon.SeatedStation(player.OwnerClientId)?.Seat,(reader != null ? reader.BindingLabel("Interact", "F") : "F") + (holdingF ? " 길게" : ""), holdingF ? "내리기" : "발사");
            else HideGuide();

            int pole = poles != null ? poles.FindGrabbable(player) : -1;
            if (holding || riding || seated || carrying || hasInteractTarget && commonPrompt) HideTooltip();
            else if (pole >= 0)
            {
                var physical = poles.Pole(pole);
                int crewCapacity = Mathf.Min(physical != null ? physical.RiderCapacity : 1,
                    Mathf.Max(1, NetworkSessionManager.Instance?.PlayerCount ?? 1));
                string title = physical != null && physical.RiderCapacity > 1
                    ? $"협동 그네 {poles.OccupantCount(pole)}/{crewCapacity}" : "봉";
                ShowTooltip("pole:" + pole + ":" + title, title, "[F] 잡기", poles.InteractionPoint(pole, player));
            }
            else if (carryTarget != null) ShowTooltip("carry:" + carryTarget.CarryRoot.GetInstanceID(), carryTarget.CarryName, "[F] 잡기", carryTarget.Handle.position);
            else
            {
                int index = cannon.FindBoardableStation(player);
                if (index >= 0) ShowTooltip("cannon:" + index, "대포", "[F] 타기", cannon.Station(index).Seat.position);
                else HideTooltip();
            }
        }

        bool ShowTransportHint(NetworkPlayer player, HumanCannonRouteSystem route, bool carrying)
        {
            if (route == null) return false;
            int nearest = -1;
            float best = 4f * 4f;
            Vector3 point = Vector3.zero;
            for (int i = 0; i < route.TransportCount; i++)
            {
                var transport = route.Transport(i);
                if (transport == null || !transport.isActiveAndEnabled || !transport.IsConfigured) continue;
                var state = route.TransportState(i);
                bool ownRide = state.Phase == DeparturePhase.Moving && state.Rider == player.OwnerClientId;
                if (state.Phase == DeparturePhase.Moving && !ownRide) continue;
                Vector3 local = Quaternion.Inverse(transport.Rotation) * (player.transform.position - transport.Platform.position);
                var edge = new Vector3(Mathf.Clamp(local.x, -transport.HalfSize.x, transport.HalfSize.x), 0,
                    Mathf.Clamp(local.z, -transport.HalfSize.y, transport.HalfSize.y));
                Vector3 anchor = transport.Platform.position + transport.Rotation * edge;
                float distance = (anchor - player.transform.position).sqrMagnitude;
                if (!ownRide && distance >= best) continue;
                nearest = i; best = distance; point = anchor + Vector3.up;
                if (ownRide) break;
            }
            if (nearest < 0) return false;
            var phase = route.TransportState(nearest).Phase;
            bool live = practiceTooltip == null && HumanCannonDestination.Current?.PerformanceOpen == true;
            string text;
            if (route.TrapOpen(nearest)) text = "발판이 열려 있습니다. 접근에 주의하세요";
            else if (phase == DeparturePhase.Moving) text = "이동 중 · 도착할 때까지 대기하세요";
            else if (phase == DeparturePhase.Arrived) text = "도착 · 걸어서 내리세요";
            else if (phase == DeparturePhase.Returning) text = "출발점으로 돌아가는 중";
            else if (!carrying)
            {
                text = live ? "탑승 조건 · 트램펄린 필요" : "트램펄린 운반자가 이용할 수 있습니다";
                if (live) Teach(PerformanceHudProfile.Current.liftCarry,player.GetComponent<PlayerInputReader>());
            }
            else if (nearest == 1 && !route.ShelterReady) text = "단원들이 쉼터에 도착할 때까지 대기하세요";
            else if (route.CanStart(player, nearest))
            {
                if (live) Teach(PerformanceHudProfile.Current.liftBoard,player.GetComponent<PlayerInputReader>());
                text = live ? (route.IsBoarding(nearest) ? $"자동 출발까지 {route.BoardingRemaining(nearest):0.0}초" : "자동 출발 준비") : route.IsBoarding(nearest)
                ? $"자동 출발까지 {route.BoardingRemaining(nearest):0.0}초 · 발판에서 대기하세요"
                : "자동 출발 준비 · 발판에서 대기하세요";
            }
            else
            {
                text = live ? "탑승 조건 · 운반자 1명" : "트램펄린을 들고 혼자 발판에 올라가세요";
                if (live) Teach(PerformanceHudProfile.Current.liftCarry,player.GetComponent<PlayerInputReader>());
            }
            ShowTooltip("transport:" + nearest + ":" + text, "리프트", text, point);
            return true;
        }

        string lessonRun;
        NetworkPlayer lessonPlayer;
        readonly System.Collections.Generic.HashSet<PerformanceHudProfile.Cue> seenLessons = new();
        readonly System.Collections.Generic.List<HudRoleCard> lessons = new();
        void Teach(PerformanceHudProfile.Cue cue,PlayerInputReader reader)
        {
            if (!seenLessons.Add(cue)) return;
            lessons.RemoveAll(c=>!c || c.Closing);
            lessons.Add(HudUIManager.Current.ShowGuide(PerformanceHudProfile.Format(cue.body,reader),transform,
                PerformanceHudProfile.Format(cue.title,reader),key:PerformanceHudProfile.Format(cue.badge,reader)));
        }
        void ResetLessons()
        {
            foreach(var card in lessons) if(card && !card.Closing)card.Close(true);
            lessons.Clear(); seenLessons.Clear(); lessonRun=null; lessonPlayer=null;
        }
        string lastSection;
        HudRoleCard fallbackAction;
        void HideSection() { if(lastSection!=null)HudUIManager.DismissOwned(transform,HudRole.Status);lastSection=null; }
        void ShowAction(Transform target,string key,string action)
        {
            if (!target || !Camera.main) { HideGuide(); return; }
            string id = target.GetInstanceID() + key + action;
            if (shownGuide == id) return;
            HideGuide();
            fallbackAction = HudUIManager.Current.ShowInteraction(action, target,key,Camera.main);
            shownGuide = id;
        }
        void HideGuide() { if (fallbackAction != null) fallbackAction.Close(true); fallbackAction=null; shownGuide=null; }
        void HideTooltip() { if (Tooltip != null) Tooltip.HideImmediate(); shownTarget = null; }

        public static Vector2 ClampTooltip(Vector2 target, Vector2 size, Vector2 viewport)
        {
            const float margin = 24;
            return new Vector2(Mathf.Clamp(target.x - size.x * .5f, margin, Mathf.Max(margin, viewport.x - size.x - margin)),
                Mathf.Clamp(target.y - size.y - margin, margin, Mathf.Max(margin, viewport.y - size.y - margin)));
        }

        void ShowTooltip(string id, string title, string description, Vector3 position)
        {
            var reader = NetworkPlayer.Local != null ? NetworkPlayer.Local.GetComponent<PlayerInputReader>() : null;
            description = CommonInteractionUI.FormatControlLabels(description,
                reader != null ? reader.BindingLabel("Interact", "F") : "F", reader != null ? reader.BindingLabel("Action", "Space") : "Space");
            id += "|" + description;
            var view = Tooltip;
            var camera = Camera.main;
            if (view == null || camera == null) { HideTooltip(); return; }
            Vector3 screen = camera.WorldToScreenPoint(position);
            // 가까운 장치의 발밑 앵커는 화면 아래로 나갈 수 있다. 앞쪽 대상은 화면 안으로 보정한다.
            if (screen.z <= 0)
            { HideTooltip(); return; }
            var parent = view.transform.parent as RectTransform;
            if (parent == null) { HideTooltip(); return; }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local)) { HideTooltip(); return; }
            var target = new Vector2(local.x - parent.rect.xMin, parent.rect.yMax - local.y);
            if (shownTarget != id || !view.HasRuntimePresentation)
            {
                if (!view.ShowNearby(title, description, Vector2.zero)) { HideTooltip(); return; }
                shownTarget = id;
            }
            var topLeft = ClampTooltip(target, view.PresentationSizeUi + new Vector2(0, 4), parent.rect.size);
            if (practiceTooltip != null && PracticeTooltipExclusion != null)
            {
                PracticeTooltipExclusion.GetWorldCorners(exclusionCorners);
                Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var corner in exclusionCorners)
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                        RectTransformUtility.WorldToScreenPoint(null, corner), null, out var localCorner))
                    {
                        var point = new Vector2(localCorner.x - parent.rect.xMin, parent.rect.yMax - localCorner.y);
                        min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                    }
                var occupied = new Rect(min, max - min);
                if (new Rect(topLeft, view.PresentationSizeUi).Overlaps(occupied))
                    topLeft.y = Mathf.Max(24f, occupied.yMin - view.PresentationSizeUi.y - 8f);
            }
            view.SetPosition(topLeft);
            view.SetTailCenter(target.x - topLeft.x);
        }
    }
}
