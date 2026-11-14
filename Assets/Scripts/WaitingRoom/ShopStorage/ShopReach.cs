using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>게임 표시 도형과 실제 이름을 UI에 전달한다. UI는 거래/소유 상태를 변경하지 않는다.</summary>
    public readonly struct ShopWorldLabel
    {
        public readonly Transform Anchor;
        public readonly string Text;
        public readonly Vector3 Offset;
        public readonly Transform OcclusionRoot;
        public ShopWorldLabel(Transform anchor, string text, Vector3 offset, Transform occlusionRoot = null)
        { Anchor = anchor; Text = text; Offset = offset; OcclusionRoot = occlusionRoot != null ? occlusionRoot : anchor; }
    }
    public interface IShopPresentationDetails { string HelperLabel { get; } }
    /// <summary>바닥이나 배송구에서 기물을 소지품으로 가져오는 상호작용.</summary>
    public interface IItemPickupTarget { }
    /// <summary>외곽선을 그리지 않을 수 있는 대상(숨겨진 벽 등). F 안내는 그대로 나온다.</summary>
    public interface IShopOutlineOptional { bool HideOutline { get; } }
    /// <summary>표시 어댑터가 사용하는 실제 대상. 이동/생성/삭제에 따라 매 프레임 읽는다.</summary>
    public interface IShopPresentation
    {
        Transform VisualRoot { get; }
        Vector3 PresentationPoint { get; }
        string ActionLabel { get; }
        bool IsAvailable { get; }
    }
    /// <summary>상점 F 상호작용 대상(수화기·배송구 물건·바닥 기물).</summary>
    public interface IShopTarget
    {
        /// <summary>내 발 위치에서 닿는 거리. 닿지 않으면 음수.</summary>
        float Reach(Vector3 feet, Component actor = null);

        /// <summary>F를 짧게 눌렀다(가장 가까운 대상 하나만 받는다).</summary>
        void Interact();
    }

    /// <summary>
    /// 내 캐릭터 기준 상점 상호작용 선택. 수화기 바로 아래에 배송구가 있어 둘 다 닿으므로, 가장 가까운 대상 하나에만 F를 준다.
    /// 접속 중이면 <see cref="NetworkPlayer.Local"/>, 접속하지 않은 씬 확인에서는 씬에 놓인 플레이어를 쓴다.
    /// </summary>
    public static class ShopReach
    {
        static readonly List<IShopTarget> targets = new();
        public static IReadOnlyList<IShopTarget> Targets => targets;
        public static bool HasExternalPresentation { get; set; }
        /// <summary>대기실 상호작용을 모두 막는다(첫 공연 전 리허설 단계, 대기실 튜토리얼 때 해금 — Task-097).</summary>
        public static bool Locked { get; set; }
        static PlayerInputReader offlineReader;
        static int pumpedFrame = -1;

        /// <summary>이번 프레임에 F 안내를 띄울 대상(없으면 null).</summary>
        public static IShopTarget Focused { get; private set; }

        /// <summary>맨몸으로 꾹 눌러 부수는 중인 대상과 부수기 전체 진행(0~1, 다 차면 부서진다). 대상의 F 안내가 읽는다.</summary>
        public static IShopTarget BareHoldTarget { get; private set; }
        public static float BareHoldProgress { get; private set; }
        static float bareHeld;
        const int BareGaugeCells = 10;
        /// <summary>
        /// 맨몸 부수기 안내 꼬리말(2026-11-13 사용자): 횟수 없이 게이지 하나만 보여 준다. 게이지는 부수기 전체 진행이고 다 차면 부서진다.
        /// 아직 손대지 않았으면 "꾹 누르기".
        /// </summary>
        public static string BareHoldSuffix(IShopTarget target)
        {
            if (CurtainCall.Settings.GameSettings.WaitingRoom.BareHandHoldSeconds <= 0f) return "";
            float progress = ReferenceEquals(BareHoldTarget, target) ? BareHoldProgress
                : target is IBareHandTarget hand ? ExplorationTargets.BareProgress(hand, 0f) : 0f;
            if (progress <= 0f) return " · " + WaitingRoomText.Get("prompt.bare-hold", "F키로 상호작용 하기");
            int filled = Mathf.Clamp(Mathf.CeilToInt(progress * BareGaugeCells), 0, BareGaugeCells);
            return " " + new string('■', filled) + new string('□', BareGaugeCells - filled);
        }

        /// <summary>F를 길게 눌렀다(대상과 상관없이, 기물 내려놓기).</summary>
        public static event System.Action LongPressed;
        internal static event System.Action<ClueInspectable> ClueFocused;

        public static void Add(IShopTarget target)
        {
            if (!targets.Contains(target)) targets.Add(target);
        }

        public static void Remove(IShopTarget target)
        {
            targets.Remove(target);
            if (Focused == target) Focused = null;
        }

        /// <summary>대상들의 Update에서 부른다. 프레임마다 한 번만 고르고 F를 전달한다.</summary>
        public static void Pump()
        {
            if (pumpedFrame == Time.frameCount) return;
            pumpedFrame = Time.frameCount;
            var previous = Focused;
            Focused = null;
            if (Locked || WaitingRoomRehearsal.IsPreTutorialRestricted) return;

            var reader = LocalReader();
            if (reader == null || !reader.isActiveAndEnabled || InputGate.IsBlocked || !PlayerInteraction.CanInteract(reader)) return;

            Vector3 feet = reader.transform.position;
            float best = float.MaxValue;
            foreach (var target in targets)
            {
                float distance = PlayerInteraction.RestrictReach(target.Reach(feet, reader), reader);
                if (distance < 0f || distance >= best) continue;
                best = distance;
                Focused = target;
            }
            if (!ReferenceEquals(previous, Focused) && Focused is ClueInspectable clue) ClueFocused?.Invoke(clue);
            var actorForGate = reader.GetComponent<HumanoidPlayerPresentation>();
            // 벽 부수기는 연타로 진행한다(내려치는 중에도 다시 누르면 동작을 초기화해 친다).
            if (actorForGate != null && actorForGate.InteractionActionLocked && Focused is not BreakableWall) return;
            if (CommonInteractionSelection.Selected(reader) is IShopTarget selected && targets.Contains(selected)) Focused = selected;
            // 맨몸 부수기(2026-11-13 사용자): 도구 없이 부술 때는 F를 길게 누르고 있어야 한다. 게이지 하나가 천천히 차고, 다 차면 부서진다.
            // 누르는 동안 타격 동작을 느리게 보여 준다. 손을 떼면 게이지는 그대로 멈춘다. 연타는 도구를 들었을 때만 된다.
            // 전체 시간 = 설정 "맨몸 부수기 누르는 시간" × 대상의 맨몸 횟수. 진행은 한 대 분량이 찰 때마다 판정 화면에 기록한다.
            float bareNeed = CurtainCall.Settings.GameSettings.WaitingRoom.BareHandHoldSeconds;
            bool bare = bareNeed > 0f && Focused is IBareHandTarget hand && hand.AllowsBareHands && !ExplorationTargets.LocalHolds(hand.ToolId)
                && Focused is IShopPresentation shown && shown.IsAvailable && (Focused is not ExplorationGate bareGate || bareGate.ConditionsMet);
            if (bare)
            {
                var bareTarget = (IBareHandTarget)Focused;
                if (!ReferenceEquals(BareHoldTarget, Focused)) { BareHoldTarget = Focused; bareHeld = 0f; BareHoldProgress = 0f; }
                bool swinging = reader.Current.InteractHeld && (actorForGate == null
                    || actorForGate.HoldSlowStrike(MotionKind(Focused), ((IShopPresentation)Focused).PresentationPoint, bareHeld / bareNeed));
                if (swinging) bareHeld += Time.deltaTime;
                if (bareHeld >= bareNeed) { bareHeld -= bareNeed; Focused.Interact(); }
                // 기록이 돌아오기 전에 게이지가 잠깐 줄어 보이지 않게, 같은 대상에서는 줄이지 않는다.
                BareHoldProgress = Mathf.Max(BareHoldProgress, ExplorationTargets.BareProgress(bareTarget, bareHeld / bareNeed));
                // 짧게 누르기·길게 누르기(기물 내려놓기) 신호는 맨몸 부수기 대상 앞에서는 쓰지 않는다.
                return;
            }
            BareHoldTarget = null; bareHeld = 0f; BareHoldProgress = 0f;
            if (Focused != null && reader.Current.InteractTapped && CommonInteractionSelection.Consume(reader, Focused))
            {
                if (Focused is IShopPresentation presentation && presentation.IsAvailable)
                {
                    bool allowed = Focused is not IExplorationTarget tool || string.IsNullOrEmpty(tool.ToolId) || ExplorationTargets.LocalHolds(tool.ToolId)
                        || ExplorationTargets.AllowsBareHands(tool); // 맨몸 겸용 대상은 도구 없이도 친다(CC-255)
                    if (Focused is ExplorationGate gate && gate.UsesKey) allowed &= gate.ConditionsMet && gate.LocalHasKey;
                    var actor = reader.GetComponent<HumanoidPlayerPresentation>();
                    if (allowed && actor != null && Focused is IExplorationTarget usedTool &&
                        (Focused is ToolTarget || Focused is BreakableWall))
                    {
                        actor.UseToolAtContact(Focused, presentation, usedTool);
                        return;
                    }
                    if (allowed)
                        reader.GetComponent<HumanoidPlayerPresentation>()?.RequestItemMotion(MotionKind(Focused), presentation.PresentationPoint, PolicyKind(Focused));
                }
                Focused.Interact();
            }
            if (reader.Current.InteractLongPressed
                && CommonInteractionSelection.Consume(reader, CommonInteractionSelection.Selected(reader) is IShopTarget ? Focused : CommonInteractionSelection.Idle)) LongPressed?.Invoke();
        }

        /// <summary>
        /// 재생할 상호작용 동작(설정 InteractionPolicy 순서): 0 타격, 1 조작, 2 당기기, 3 열쇠 사용, 4 줍기.
        /// 바닥·배송구 기물과 처음 놓인 열쇠는 줍기 동작을 재생한다.
        /// </summary>
        public static int MotionKind(IShopTarget target) =>
            target is IItemPickupTarget || target is ExplorationGate { IsKeyPickup: true } ? 4 :
            target is ExplorationGate keyGate && keyGate.UsesKey && keyGate.ConditionsMet ? 3 :
            target is IExplorationTarget pry && pry.ToolId == ItemUses.StakePuller ? 0 : 1;

        /// <summary>이동·스킵 허용을 읽을 설정 항목. 열쇠는 줍기도 사용도 "열쇠(key)" 설정을 따르고, 나머지는 동작과 같다.</summary>
        public static int PolicyKind(IShopTarget target) =>
            target is ExplorationGate { IsKeyPickup: true } || target is FloorPropsView.Item { IsKey: true } ? 3 : MotionKind(target);

        /// <summary>내 캐릭터 발 위치.</summary>
        public static bool TryLocalFeet(out Vector3 feet)
        {
            var reader = LocalReader();
            feet = reader != null ? reader.transform.position : default;
            return reader != null;
        }

        static PlayerInputReader LocalReader()
        {
            if (NetworkPlayer.All.Count > 0)
                return NetworkPlayer.Local != null ? NetworkPlayer.Local.GetComponent<PlayerInputReader>() : null;
            if (offlineReader == null) offlineReader = CurtainCall.Player.OfflineLocalPlayer.Reader;
            return offlineReader;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            targets.Clear();
            HasExternalPresentation = false;
            offlineReader = null;
            pumpedFrame = -1;
            Focused = null;
            LongPressed = null; ClueFocused = null;
        }
    }
}
