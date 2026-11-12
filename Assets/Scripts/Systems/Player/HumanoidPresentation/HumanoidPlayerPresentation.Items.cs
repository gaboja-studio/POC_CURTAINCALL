using CurtainCall.HumanCannon;
using CurtainCall.ItemArt;
using CurtainCall.WaitingRoom;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Player
{
    public sealed partial class HumanoidPlayerPresentation
    {
        /// <summary>상호작용 동작 레이어(몸통·머리·양팔). 이동 중에도 상체 전체로 손을 뻗는다.</summary>
        public const string ItemUpperBodyLayer = "ItemRightHand";
        /// <summary>계속 든 자세(들기·조준·꺼내기·넣기) 레이어(오른팔만). 몸통·왼팔은 이동 동작을 따른다.</summary>
        public const string ItemRightArmLayer = "ItemRightArm";
        string itemAction, itemLayerState, shownItem, visualItem, gestureItem;
        Coroutine toolUse;
        int itemActionRevision;
        float itemActionUntil, itemActionSince;
        float interactionMoveLockUntil;
        float interactionActionUntil;
        int activeInteractionKind = -1;
        bool activeInteractionAllowsMovement;
        public bool InteractionActionLocked => isActiveAndEnabled && Time.time < interactionActionUntil && activeInteractionKind >= 0
            && !GameSettings.Base.Presentation.InteractionPolicy(activeInteractionKind).AllowInterrupt && CanUseItemHands;
        public bool InteractionMovementLocked => isActiveAndEnabled && Time.time < interactionMoveLockUntil && CanUseItemHands;
        bool CanUseItemHands => (balance == null || !balance.HasFallen)
            && (network == null || !network.IsSpawned || network.State == PlayerState.Normal && !HumanCannonCarry.HasOccupiedHands(network));
        static float InteractionDuration(int kind) => kind == 0 ? 1.2f : kind == 2 ? 1.6f : 1.4f;
        GameObject heldItemVisual;
        public GameObject HeldItemVisual => heldItemVisual;
        public string ItemMotion => itemAction;
        /// <summary>벽 부수기 연타 간격(초). 누를 때마다 내려치기 동작을 처음부터 다시 재생하고 바로 한 번 친다(2026-11-02 사용자).</summary>
        const float WallMashGap = .18f, WallMashImpact = .12f;
        float lastWallMash = -1f;
        public void UseToolAtContact(IShopTarget target, IShopPresentation presentation, IExplorationTarget tool)
        {
            if (model == null || condition.AllLimbsLost || !CanUseItemHands) return;
            if (target is BreakableWall)
            {
                // 연타: 앞 동작이 끝나기를 기다리지 않고 동작을 초기화해 다시 내려친다. 앞 타격 판정은 그대로 살린다.
                if (Time.time - lastWallMash < WallMashGap) return;
                lastWallMash = Time.time;
                interactionActionUntil = 0f;
                RequestItemMotion(0, presentation.PresentationPoint);
                StartCoroutine(CompleteToolUse(target, presentation, tool, WallMashImpact, tracked: false));
                return;
            }
            if (InteractionActionLocked) return;
            if (toolUse != null) { StopCoroutine(toolUse); toolUse = null; }
            int kind = tool.ToolId == ItemUses.StakePuller ? 0 : 1;
            RequestItemMotion(kind, presentation.PresentationPoint);
            toolUse = StartCoroutine(CompleteToolUse(target, presentation, tool, kind == 0 ? .66f : .7f, tracked: true));
        }
        System.Collections.IEnumerator CompleteToolUse(IShopTarget target, IShopPresentation presentation, IExplorationTarget tool, float seconds, bool tracked)
        {
            int revision = itemActionRevision;
            yield return new WaitForSeconds(seconds);
            bool exists = target is not Object obj || obj != null;
            if (revision == itemActionRevision && CanUseItemHands && exists && presentation.IsAvailable
                && !condition.AllLimbsLost && target.Reach(transform.position, this) >= 0
                && (ExplorationTargets.LocalHolds(tool.ToolId) || ExplorationTargets.AllowsBareHands(tool)))
                target.Interact();
            if (!tracked) yield break;
            yield return new WaitForSeconds(.5f);
            if (revision == itemActionRevision) toolUse = null;
        }
        public void RequestItemMotion(int kind, Vector3 point) => RequestItemMotion(kind, point, kind);
        /// <summary>kind는 재생할 동작, policyKind는 이동·스킵 허용을 읽을 설정 항목(예: 열쇠 줍기 = 줍기 동작 + 열쇠 설정).</summary>
        public void RequestItemMotion(int kind, Vector3 point, int policyKind)
        {
            if (!ExplorationKeys.Enabled && (kind == 3 || policyKind == 3)) return;
            if (kind < 0 || kind > 4 || policyKind < 0 || policyKind > 4 || model == null || condition.AllLimbsLost
                || !CanUseItemHands || InteractionActionLocked) return;
            if (toolUse != null) { StopCoroutine(toolUse); toolUse = null; }
            activeInteractionAllowsMovement = GameSettings.Base.Presentation.InteractionPolicy(policyKind).AllowMovement;
            interactionMoveLockUntil = activeInteractionAllowsMovement ? 0f : Time.time + InteractionDuration(kind);
            interactionActionUntil = itemActionUntil = Time.time + InteractionDuration(kind);
            ReceiveItemMotion(kind, point, activeInteractionAllowsMovement);
            activeInteractionKind = policyKind;
            if (network != null && network.IsSpawned) network.SendItemGesture(kind, point, activeInteractionAllowsMovement);
        }
        public void ReceiveItemMotion(int kind, Vector3 point, bool allowMovement)
        {
            if (!ExplorationKeys.Enabled && kind == 3) return;
            if (kind < 0 || kind > 4 || model == null || condition.AllLimbsLost || !CanUseItemHands) return;
            activeInteractionKind = kind;
            activeInteractionAllowsMovement = allowMovement;
            gestureItem=kind==3?"route-key":null;
            itemAction = ItemVisualAssets.Motions[kind>=3?1:kind];
            itemActionSince = Time.time; itemActionUntil = Time.time + InteractionDuration(kind);
            itemLayerState = null;
            // Locomotion stays on the base layer; PresentItemMotion starts the action layer.
        }
        string SelectedHandItem()
        {
            if(gestureItem!=null&&Time.time<itemActionUntil)return gestureItem;
            var carry = PropCarry.Instance;
            if (carry == null) return null;
            ulong id = network != null && network.IsSpawned ? network.OwnerClientId : carry.LocalClientId;
            bool uv = network != null && network.IsSpawned ? network.UvOn : false;
            if (network == null || !network.IsSpawned || network.IsOwner)
            {
                var reader = GetComponent<PlayerInputReader>();
                if (reader != null && reader.Current.UvLightHeld && !InputGate.IsBlocked)
                    foreach (var item in carry.Held(id)) if (item.ItemId == ItemUses.UvLamp) uv = true;
            }
            if (uv) return ItemUses.UvLamp;
            var selected = carry.Selected(id);
            return selected.Uid != 0 && ItemVisualAssets.IsTool(selected.ItemId) ? selected.ItemId : null;
        }
        bool PresentItemMotion(bool moving, bool inAir)
        {
            var a = model.Animator;
            if (!a.isActiveAndEnabled) { itemLayerState = null; return false; }
            int upper = a.GetLayerIndex(ItemUpperBodyLayer);
            if (upper < 0) return false;
            int arm = a.GetLayerIndex(ItemRightArmLayer);
            string item = SelectedHandItem();
            if (item != shownItem)
            {
                string previous = shownItem; shownItem = item;
                if (Time.time >= itemActionUntil && (item != null || previous != null))
                {
                    itemAction = item != null ? "ItemEquip" : "ItemStow";
                    itemActionSince = Time.time; itemActionUntil = Time.time + .6f; itemLayerState = null;
                }
            }
            if (item != null && (heldItemVisual == null || visualItem != item))
            {
                if (heldItemVisual != null) Destroy(heldItemVisual);
                // The player survives scene travel. Its hand visual must share that lifetime,
                // rather than being destroyed with the scene that was active when it was created.
                heldItemVisual = ItemVisualAssets.Create(item, transform); visualItem = item;
                if (heldItemVisual != null) heldItemVisual.name = "RightHand_" + item;
            }
            bool action = Time.time < itemActionUntil;
            if (!action && shownItem == null && heldItemVisual != null) { Destroy(heldItemVisual); heldItemVisual = null; visualItem = null; }
            string motion = action ? itemAction : item == ItemUses.UvLamp ? "ItemAim" : item != null ? "ItemHold" : null;
            // The surviving right arm is the locomotion arm. Do not mask it out with
            // a standing hold pose; the crawl clip carries the item through its cycle.
            ClearItemLayers(a);
            if(model.Stage>=2&&moving&&!inAir && !action)
            {
                itemLayerState=null;
                PlayLocomotion(ItemVisualAssets.State("ItemCrawl",model.Stage));
                return true;
            }
            if (motion == null) { itemLayerState = null; itemAction = null; return false; }
            string state = ItemVisualAssets.State(motion, model.Stage);
            bool fullBody = !inAir && !moving && ((action && !activeInteractionAllowsMovement) || (!action && model.Stage >= 2));
            // Only interactions reach with the torso. Holding keeps the locomotion torso and left arm.
            bool interaction = action && itemAction != "ItemEquip" && itemAction != "ItemStow";
            int layer = interaction || arm < 0 ? upper : arm;
            if (!fullBody) a.SetLayerWeight(layer, 1);
            string layerState=state+"/"+(fullBody?0:layer);
            int targetLayer = fullBody ? 0 : layer;
            bool playing = a.GetCurrentAnimatorStateInfo(targetLayer).IsName(state)
                || (a.IsInTransition(targetLayer) && a.GetNextAnimatorStateInfo(targetLayer).IsName(state));
            bool restoreState = itemLayerState != layerState || !playing;
            if (fullBody)
            {
                model.Play(state);
                if (restoreState) a.Play(state, 0, action ? Mathf.Clamp01((Time.time-itemActionSince)/(itemActionUntil-itemActionSince)) : 0);
            }
            else if (restoreState) a.Play(state, layer, action ? Mathf.Clamp01((Time.time-itemActionSince)/(itemActionUntil-itemActionSince)) : 0f);
            itemLayerState = layerState;
            return fullBody;
        }
        static void ClearItemLayers(Animator a)
        {
            int upper = a.GetLayerIndex(ItemUpperBodyLayer), arm = a.GetLayerIndex(ItemRightArmLayer);
            if (upper >= 0) a.SetLayerWeight(upper, 0);
            if (arm >= 0) a.SetLayerWeight(arm, 0);
        }
        void ApplyItemAlignment()
        {
            if (model == null || !model.Animator.isActiveAndEnabled || condition.AllLimbsLost || IsHanging || !CanUseItemHands)
            { if (heldItemVisual != null) heldItemVisual.SetActive(false); return; }
            if (shownItem == ItemUses.UvLamp && !(itemAction != null && Time.time < itemActionUntil))
            {
                var lamp = FindFirstObjectByType<UvFlashlight>();
                Vector3 forward = network != null && network.IsSpawned && !network.IsOwner ? network.UvForward : lamp != null ? lamp.LastForward : transform.forward;
                bool crawling=model.Stage>=2&&(network!=null?network.PresentationMoving:mover.IsMoving);
                if (!crawling && forward.sqrMagnitude > .01f)
                {
                    // Rotate the upper body as one unit. The authored shoulder, elbow and wrist pose remains intact.
                    AimUpperBody(model.Animator, transform.forward, forward);
                }
            }
            if (heldItemVisual != null)
            { heldItemVisual.SetActive(!IsHanging && seatedStation == null && rescueHang == null); ItemVisualAssets.Attach(heldItemVisual, model.Animator, visualItem); }
        }
        static void AimUpperBody(Animator animator, Vector3 facing, Vector3 direction)
        {
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null && facing.sqrMagnitude > .01f && direction.sqrMagnitude > .01f)
                spine.rotation = Quaternion.FromToRotation(facing.normalized, direction.normalized) * spine.rotation;
        }
        internal void CancelItemAction()
        {
            if (toolUse != null) StopCoroutine(toolUse); toolUse = null;
            itemActionRevision++;
            if (gestureItem != null) shownItem = null;
            itemAction = itemLayerState = gestureItem = null;
            itemActionUntil = interactionActionUntil = interactionMoveLockUntil = 0f;
            activeInteractionKind = -1;
            activeInteractionAllowsMovement = false;
        }

        void ClearItemVisual()
        {
            if (heldItemVisual != null) Destroy(heldItemVisual);
            heldItemVisual = null;
            shownItem = visualItem = null;
        }
    }
}
