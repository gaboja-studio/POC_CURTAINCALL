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
            var route = HumanCannonRouteSystem.Current;
            var poles = HumanCannonPoleSystem.Current;
            bool riding = route != null && route.IsRiding(player.OwnerClientId);
            bool seated = cannon.IsSeated(player.OwnerClientId);
            bool carrying = trampoline != null && trampoline.isActiveAndEnabled && trampoline.Carrier == player.OwnerClientId;
            bool holding = pilot != null && pilot.IsHoldingPole;
            var reader = player.GetComponent<PlayerInputReader>();
            bool holdingF = reader != null && reader.Current.InteractHoldProgress > 0;
            if (holding) ShowGuide("Space", "봉 놓기", GuideKeyWidth.Wide);
            else if (riding) HideGuide();
            else if (carrying) ShowGuide("F 길게", "트램펄린 놓기", GuideKeyWidth.ExtraWide);
            else if (seated) ShowGuide(holdingF ? "F 길게" : "F", holdingF ? "내리기" : "발사", holdingF ? GuideKeyWidth.ExtraWide : GuideKeyWidth.Normal);
            else HideGuide();

            int pole = poles != null ? poles.FindGrabbable(player) : -1;
            if (holding || riding || seated || carrying) HideTooltip();
            else if (pole >= 0) ShowTooltip("pole:" + pole, "봉", "[F] 잡기", poles.HangingPosition(pole));
            else if (trampoline != null && trampoline.CanGrab(player)) ShowTooltip("trampoline", "트램펄린", "[F] 잡기", trampoline.Handle.position);
            else
            {
                int index = cannon.FindBoardableStation(player);
                if (index >= 0) ShowTooltip("cannon:" + index, "대포", "[F] 타기", cannon.Station(index).BoardingPoint.position);
                else HideTooltip();
            }
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
