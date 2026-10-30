using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
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
        bool legacyEnabled;
        string shownGuide, shownTarget;
        static readonly string[] Sections = { "현재 구간 · 출발", "현재 구간 · 1구간", "현재 구간 · 쉼터", "현재 구간 · 2구간", "현재 구간 · 최종구간" };

        public void Configure(HumanCannonCourseSections previous, HumanCannonCourseSections.Boundaries values, TMP_Text section)
        { legacy = previous; boundaries = values; sectionLabel = section; }

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
            if (owned != null) owned.ReleaseHints(this);
            owned = null; shownGuide = shownTarget = null;
            if (sectionLabel != null) sectionLabel.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (CommonUiSceneLoader.CurrentSpace != space || CommonPerformanceUI.Current == null) { Clear(); return; }
            if (owned != CommonPerformanceUI.Current) { Clear(); owned = CommonPerformanceUI.Current; }
            owned.ClaimHints(this);
            var player = NetworkPlayer.Local;
            var cannon = HumanCannonSystem.Current;
            bool available = player != null && player.IsSpawned && player.State == PlayerState.Normal
                && cannon != null && !InputGate.IsBlocked;
            if (sectionLabel != null)
            {
                sectionLabel.gameObject.SetActive(available);
                if (available) sectionLabel.text = Sections[(int)HumanCannonCourseSections.SectionAt(player.transform.position, boundaries)];
            }
            if (!available) { owned.HideHints(); shownGuide = shownTarget = null; return; }
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
            if (!holding && !seated && !cannon.IsFlying(player.OwnerClientId)
                && (carrying || riding || !hasInteractTarget) && ShowTransportHint(player, route, carried is HumanCannonTrampoline))
            { HideGuide(); return; }
            if (holding) ShowGuide("Space", "봉 놓기", GuideKeyWidth.Wide);
            else if (riding) HideGuide();
            else if (carrying) ShowGuide("F 길게", carried.CarryName + " 놓기", GuideKeyWidth.ExtraWide);
            else if (seated) ShowGuide(holdingF ? "F 길게" : "F", holdingF ? "내리기" : "발사", holdingF ? GuideKeyWidth.ExtraWide : GuideKeyWidth.Normal);
            else HideGuide();

            int pole = poles != null ? poles.FindGrabbable(player) : -1;
            if (interaction == null) interaction = FindFirstObjectByType<CommonInteractionUI>();
            bool commonPrompt = interaction != null && interaction.isActiveAndEnabled && interaction.Prompt != null;
            if (holding || riding || seated || carrying || hasInteractTarget && commonPrompt) HideTooltip();
            else if (pole >= 0)
            {
                var physical = poles.Pole(pole);
                string title = physical != null && physical.RiderCapacity > 1
                    ? $"협동 그네 {poles.OccupantCount(pole)}/{physical.RiderCapacity}" : "봉";
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
            string text;
            if (route.TrapOpen(nearest)) text = "발판이 열려 있습니다. 접근에 주의하세요";
            else if (phase == DeparturePhase.Moving) text = "이동 중 · 도착할 때까지 대기하세요";
            else if (phase == DeparturePhase.Arrived) text = "도착 · 걸어서 내리세요";
            else if (!carrying) text = "트램펄린 운반자가 이용할 수 있습니다";
            else if (nearest == 1 && !route.ShelterReady) text = "단원들이 쉼터에 도착할 때까지 대기하세요";
            else if (route.CanStart(player, nearest)) text = route.IsBoarding(nearest)
                ? $"자동 출발까지 {route.BoardingRemaining(nearest):0.0}초 · 발판에서 대기하세요"
                : "자동 출발 준비 · 발판에서 대기하세요";
            else text = "트램펄린을 들고 혼자 발판에 올라가세요";
            ShowTooltip("transport:" + nearest + ":" + text, "리프트", text, point);
            return true;
        }

        void ShowGuide(string key, string action, GuideKeyWidth width)
        {
            if (owned.Guide == null) return;
            string id = key + action;
            if (shownGuide != id || !owned.Guide.HasRuntimePresentation)
                if (owned.Guide.ShowKey(key, action, width)) shownGuide = id;
        }
        void HideGuide() { if (owned.Guide != null) owned.Guide.HideImmediate(); shownGuide = null; }
        void HideTooltip() { if (owned.Tooltip != null) owned.Tooltip.HideImmediate(); shownTarget = null; }

        public static Vector2 ClampTooltip(Vector2 target, Vector2 size, Vector2 viewport)
        {
            const float margin = 24;
            return new Vector2(Mathf.Clamp(target.x - size.x * .5f, margin, Mathf.Max(margin, viewport.x - size.x - margin)),
                Mathf.Clamp(target.y - size.y - margin, margin, Mathf.Max(margin, viewport.y - size.y - margin)));
        }

        void ShowTooltip(string id, string title, string description, Vector3 position)
        {
            var view = owned.Tooltip;
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
            view.SetPosition(topLeft);
            view.SetTailCenter(target.x - topLeft.x);
        }
    }
}
