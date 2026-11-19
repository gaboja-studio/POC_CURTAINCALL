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
    /// <summary>누를 수 있는 동작 없이 설명만 보여 주는 대상(닫힌 숙소 문 등, 2026-11-17 사용자). 안내에 키 버튼을 그리지 않는다.</summary>
    public interface IShopHintOnly { }
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
    /// <summary>F를 길게 눌러 쓰는 기능이 따로 있는 대상(2026-11-15: 숙소 TV의 테이프 꺼내기).</summary>
    public interface IShopHoldTarget
    {
        bool WantsHold { get; }
        void HoldInteract();
    }

    public static class ShopReach
    {
        static readonly List<IShopTarget> targets = new();
        public static IReadOnlyList<IShopTarget> Targets => targets;
        public static bool HasExternalPresentation { get; set; }
        /// <summary>대기실 상호작용을 모두 막는다(첫 공연 전 리허설 단계, 대기실 튜토리얼 때 해금 — Task-097).</summary>
        public static bool Locked { get; set; }
        internal static bool RoomInteractionsLocked => string.IsNullOrEmpty(FloorPropsView.CurrentSpace)
            && (Locked || WaitingRoomRehearsal.IsPreTutorialRestricted);
        static PlayerInputReader offlineReader;
        static int pumpedFrame = -1;

        /// <summary>이번 프레임에 F 안내를 띄울 대상(없으면 null).</summary>
        public static IShopTarget Focused { get; private set; }

        /// <summary>맨몸으로 꾹 눌러 부수는 중인 대상과 부수기 전체 진행(0~1, 다 차면 부서진다). 대상의 F 안내가 읽는다.</summary>
        public static IShopTarget BareHoldTarget { get; private set; }
        public static float BareHoldProgress { get; private set; }
        static float bareHeld;
        static bool holdWithTool;
        const int BareGaugeCells = 10;
        /// <summary>
        /// 부수기 안내 꼬리말(맨몸 2026-11-13, 뽑개 2026-11-16 사용자): 횟수 없이 게이지 하나만 보여 준다. 게이지는 부수기 전체 진행이고 다 차면 부서진다.
        /// 아직 손대지 않았으면 "꾹 누르기". <paramref name="withTool"/>이면 뽑개 기준으로 센다.
        /// </summary>
        public static string BareHoldSuffix(IShopTarget target, bool withTool = false)
        {
            if (ExplorationTargets.StrikePower(withTool) <= 0f) return "";
            float progress = ReferenceEquals(BareHoldTarget, target) ? BareHoldProgress
                : target is IBareHandTarget hand ? ExplorationTargets.StrikeHoldProgress(hand, 0f, withTool) : 0f;
            if (progress <= 0f) return " · " + WaitingRoomText.Get("prompt.bare-hold", "F키로 상호작용 하기");
            int filled = Mathf.Clamp(Mathf.CeilToInt(progress * BareGaugeCells), 0, BareGaugeCells);
            return " " + new string('■', filled) + new string('□', BareGaugeCells - filled);
        }

        /// <summary>기물 내려놓기 키(기본 T)를 눌렀다(대상과 상관없이). 2026-11-16 상호작용 고도화: F 길게(부수기)와 겹치지 않게 분리.</summary>
        public static event System.Action DropPressed;
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
            if (RoomInteractionsLocked) return;

            var reader = LocalReader();
            if (reader == null || !reader.isActiveAndEnabled || InputGate.IsBlocked || !PlayerInteraction.CanInteract(reader)) return;

            // 2026-11-16 상호작용 고도화: 가장 가까운 대상이 아니라 공통 선택(우선순위·화면 기준점)이 고른 대상 하나만 안내·F를 받는다.
            if (CommonInteractionSelection.Selected(reader) is IShopTarget chosen && targets.Contains(chosen)) Focused = chosen;
            if (!ReferenceEquals(previous, Focused) && Focused is ClueInspectable clue) ClueFocused?.Invoke(clue);
            var actorForGate = reader.GetComponent<HumanoidPlayerPresentation>();
            if (reader.Current.DropItemPressed && (actorForGate == null || !actorForGate.InteractionActionLocked)) DropPressed?.Invoke();
            // 벽 부수기는 연타로 진행한다(내려치는 중에도 다시 누르면 동작을 초기화해 친다).
            if (actorForGate != null && actorForGate.InteractionActionLocked && Focused is not BreakableWall) return;
            // 부수기 홀드(맨몸 2026-11-13, 말뚝 뽑개 2026-11-16 사용자): F를 길게 누르고 있어야 한다. 게이지 하나가 천천히 차고, 다 차면 부서진다.
            // 누르는 동안 타격 동작을 느리게 보여 준다. 손을 떼면 게이지는 그대로 멈춘다. 연타로는 부수지 않는다.
            // 전체 시간 = 대상 내구도 ÷ 힘(맨몸: 설정 "맨몸 부수기 힘", 뽑개: 기물 목록 뽑개 효과 수치). ExplorationTargets.StrikeHoldTotalSeconds.
            // 진행은 한 단계(전체 ÷ 횟수)가 찰 때마다 판정 화면에 기록하고, 뽑개 내구도는 부서질 때만 1 줄어든다.
            bool withTool = ExplorationTargets.UsesToolHold(Focused) && ExplorationTargets.LocalHolds(((IExplorationTarget)Focused).ToolId);
            // 한 단계가 너무 짧으면 판정 화면의 연속 요청 제한(0.08초)에 걸리므로 0.1초 아래로 내리지 않는다.
            float holdNeed = Focused is IBareHandTarget sized && ExplorationTargets.StrikeHoldSeconds(sized, withTool) is var step && step > 0f
                ? Mathf.Max(.1f, step) : 0f;
            bool strikeHold = holdNeed > 0f && Focused is IBareHandTarget hand && (withTool || hand.AllowsBareHands && !ExplorationTargets.LocalHolds(hand.ToolId))
                && Focused is IShopPresentation shown && shown.IsAvailable && (Focused is not ExplorationGate bareGate || bareGate.ConditionsMet);
            if (strikeHold)
            {
                var holdTarget = (IBareHandTarget)Focused;
                if (!ReferenceEquals(BareHoldTarget, Focused) || holdWithTool != withTool)
                { BareHoldTarget = Focused; holdWithTool = withTool; bareHeld = 0f; BareHoldProgress = 0f; }
                bool swinging = reader.Current.InteractHeld && (actorForGate == null
                    || actorForGate.HoldSlowStrike(MotionKind(Focused), ((IShopPresentation)Focused).PresentationPoint, bareHeld / holdNeed));
                if (swinging) bareHeld += Time.deltaTime;
                if (bareHeld >= holdNeed) { bareHeld -= holdNeed; Focused.Interact(); }
                // 기록이 돌아오기 전에 게이지가 잠깐 줄어 보이지 않게, 같은 대상에서는 줄이지 않는다.
                BareHoldProgress = Mathf.Max(BareHoldProgress, ExplorationTargets.StrikeHoldProgress(holdTarget, bareHeld / holdNeed, withTool));
                // 짧게 누르기·길게 누르기 신호는 부수기 대상 앞에서는 쓰지 않는다(기물 내려놓기는 T로 분리).
                return;
            }
            BareHoldTarget = null; bareHeld = 0f; BareHoldProgress = 0f;
            if (Focused != null && reader.Current.InteractTapped && CommonInteractionSelection.Consume(reader, Focused))
            {
                // 해금 불가능(조건·열쇠·도구 부족) 탐색 대상은 안내 문구만 보여 주고 요청을 보내지 않는다(2026-11-16 우선순위 7순위).
                if (Focused is ExplorationGate or ToolTarget or BreakableWall
                    && CommonInteractionSelection.Category(Focused) == CurtainCall.Settings.InteractionCategory.Locked) return;
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
            // 길게 눌러 쓰는 대상(숙소 TV의 테이프 꺼내기)만 F 길게를 받는다. 기물 내려놓기는 T(DropPressed)로 분리했다.
            if (reader.Current.InteractLongPressed && Focused is IShopHoldTarget hold && hold.WantsHold
                && CommonInteractionSelection.Consume(reader, Focused)) hold.HoldInteract();
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
            DropPressed = null; ClueFocused = null;
        }
    }
}
