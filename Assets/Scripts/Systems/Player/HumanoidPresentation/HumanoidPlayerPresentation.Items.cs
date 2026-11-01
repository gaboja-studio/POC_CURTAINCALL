using CurtainCall.ItemArt;
using CurtainCall.WaitingRoom;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Player
{
    public sealed partial class HumanoidPlayerPresentation
    {
        string itemAction, itemLayerState, shownItem, visualItem, gestureItem;
        Coroutine toolUse;
        float itemActionUntil, itemActionSince;
        float interactionMoveLockUntil;
        float interactionActionUntil;
        int activeInteractionKind = -1;
        bool activeInteractionAllowsMovement;
        public bool InteractionActionLocked => isActiveAndEnabled && Time.time < interactionActionUntil && activeInteractionKind >= 0 && !GameSettings.Base.Presentation.InteractionPolicy(activeInteractionKind).AllowInterrupt;
        public bool InteractionMovementLocked => isActiveAndEnabled && Time.time < interactionMoveLockUntil;
        static float InteractionDuration(int kind) => kind == 0 ? 1.2f : kind == 2 ? 1.6f : 1.4f;
        GameObject heldItemVisual;
        public GameObject HeldItemVisual => heldItemVisual;
        public string ItemMotion => itemAction;
        public void UseToolAtContact(IShopTarget target, IShopPresentation presentation, IExplorationTarget tool)
        {
            if (InteractionActionLocked || model == null || condition.AllLimbsLost) return;
            if (toolUse != null) { StopCoroutine(toolUse); toolUse = null; }
            int kind = tool.ToolId == ItemUses.StakePuller ? 0 : 1;
            RequestItemMotion(kind, presentation.PresentationPoint);
            toolUse = StartCoroutine(CompleteToolUse(target,presentation,tool,kind==0?.66f:.7f));
        }
        System.Collections.IEnumerator CompleteToolUse(IShopTarget target, IShopPresentation presentation, IExplorationTarget tool, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            bool exists = target is not Object obj || obj != null;
            if (exists && presentation.IsAvailable && !condition.AllLimbsLost && target.Reach(transform.position)>=0 && ExplorationTargets.LocalHolds(tool.ToolId))
                target.Interact();
            yield return new WaitForSeconds(.5f);
            toolUse = null;
        }
        public void RequestItemMotion(int kind, Vector3 point)
        {
            if (kind < 0 || kind > 4 || model == null || condition.AllLimbsLost || InteractionActionLocked) return;
            if (toolUse != null) { StopCoroutine(toolUse); toolUse = null; }
            activeInteractionKind = kind;
            activeInteractionAllowsMovement = GameSettings.Base.Presentation.InteractionPolicy(kind).AllowMovement;
            interactionMoveLockUntil = activeInteractionAllowsMovement ? 0f : Time.time + InteractionDuration(kind);
            interactionActionUntil = itemActionUntil = Time.time + InteractionDuration(kind);
            ReceiveItemMotion(kind, point, activeInteractionAllowsMovement);
            if (network != null && network.IsSpawned) network.SendItemGesture(kind, point, activeInteractionAllowsMovement);
        }
        public void ReceiveItemMotion(int kind, Vector3 point, bool allowMovement)
        {
            if (kind < 0 || kind > 4 || model == null || condition.AllLimbsLost) return;
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
            var a = model.Animator; int layer = a.GetLayerIndex("ItemRightHand");
            if (layer < 0) return false;
            string item = SelectedHandItem();
            if (item != shownItem)
            {
                string previous = shownItem; shownItem = item;
                if (item != null)
                {
                    if (heldItemVisual != null) Destroy(heldItemVisual);
                    heldItemVisual = ItemVisualAssets.Create(item); visualItem = item;
                    if (heldItemVisual != null) heldItemVisual.name = "RightHand_" + item;
                }
                if (Time.time >= itemActionUntil && (item != null || previous != null))
                {
                    itemAction = item != null ? "ItemEquip" : "ItemStow";
                    itemActionSince = Time.time; itemActionUntil = Time.time + .6f; itemLayerState = null;
                }
            }
            bool action = Time.time < itemActionUntil;
            if (!action && shownItem == null && heldItemVisual != null) { Destroy(heldItemVisual); heldItemVisual = null; visualItem = null; }
            string motion = action ? itemAction : item == ItemUses.UvLamp ? "ItemAim" : item != null ? "ItemHold" : null;
            // The surviving right arm is the locomotion arm. Do not mask it out with
            // a standing hold pose; the crawl clip carries the item through its cycle.
            if(model.Stage>=2&&moving&&!inAir && !action)
            {
                a.SetLayerWeight(layer,0);itemLayerState=null;
                model.Play(ItemVisualAssets.State("ItemCrawl",model.Stage));
                return true;
            }
            if (motion == null) { a.SetLayerWeight(layer, 0); itemLayerState = null; itemAction = null; return false; }
            string state = ItemVisualAssets.State(motion, model.Stage);
            bool fullBody = !inAir && !moving && ((action && !activeInteractionAllowsMovement) || (!action && model.Stage >= 2));
            a.SetLayerWeight(layer, fullBody ? 0 : 1);
            string layerState=state+"/"+(fullBody?0:layer);
            if (fullBody)
            {
                model.Play(state);
                if (itemLayerState != layerState) a.Play(state, 0, action ? Mathf.Clamp01((Time.time-itemActionSince)/(itemActionUntil-itemActionSince)) : 0);
            }
            else if (itemLayerState != layerState) a.Play(state, layer, 0f);
            itemLayerState = layerState;
            return fullBody;
        }
        void ApplyItemAlignment()
        {
            if (model == null || condition.AllLimbsLost || IsHanging || (network != null && network.IsSpawned && network.State != PlayerState.Normal))
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
        void ClearItemVisual()
        {
            if (toolUse != null) StopCoroutine(toolUse); toolUse = null;
            if (heldItemVisual != null) Destroy(heldItemVisual);
            heldItemVisual = null; shownItem = itemAction = itemLayerState = gestureItem = null; itemActionUntil = 0; interactionActionUntil = 0; interactionMoveLockUntil = 0; activeInteractionKind = -1; activeInteractionAllowsMovement = false;
        }
    }
}
