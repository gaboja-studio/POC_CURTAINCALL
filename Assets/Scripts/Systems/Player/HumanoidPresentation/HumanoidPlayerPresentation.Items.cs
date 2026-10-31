using CurtainCall.ItemArt;
using CurtainCall.WaitingRoom;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Player
{
    public sealed partial class HumanoidPlayerPresentation
    {
        string itemAction, itemLayerState, shownItem, visualItem, gestureItem;
        Coroutine toolUse;
        float itemActionUntil, itemActionSince;
        float interactionMoveLockUntil;
        public bool InteractionMovementLocked => isActiveAndEnabled && Time.time < interactionMoveLockUntil;
        static float InteractionDuration(int kind) => kind == 0 ? 1.2f : kind == 2 ? 1.6f : 1.4f;
        Vector3 itemTarget;
        GameObject heldItemVisual;
        public GameObject HeldItemVisual => heldItemVisual;
        public string ItemMotion => itemAction;
        public void UseToolAtContact(IShopTarget target, IShopPresentation presentation, IExplorationTarget tool)
        {
            if (toolUse != null || model == null || condition.AllLimbsLost) return;
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
            if (kind < 0 || kind > 3 || model == null || condition.AllLimbsLost) return;
            interactionMoveLockUntil = Time.time + InteractionDuration(kind);
            if (network != null && network.IsSpawned) network.SendItemGesture(kind, point);
            else ReceiveItemMotion(kind, point);
        }
        public void ReceiveItemMotion(int kind, Vector3 point)
        {
            if (kind < 0 || kind > 3 || model == null || condition.AllLimbsLost) return;
            gestureItem=kind==3?"route-key":null;
            itemAction = ItemVisualAssets.Motions[kind==3?1:kind]; itemTarget = point;
            itemActionSince = Time.time; itemActionUntil = Time.time + InteractionDuration(kind);
            itemLayerState = null;
            model.Animator.Play(ItemVisualAssets.State(itemAction, model.Stage), 0, 0);
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
            if(model.Stage>=2&&moving&&!inAir)
            {
                a.SetLayerWeight(layer,0);itemLayerState=null;
                model.Play(ItemVisualAssets.State("ItemCrawl",model.Stage));
                return true;
            }
            if (motion == null) { a.SetLayerWeight(layer, 0); itemLayerState = null; itemAction = null; return false; }
            string state = ItemVisualAssets.State(motion, model.Stage);
            bool fullBody = !inAir && !moving && (action || model.Stage >= 2);
            a.SetLayerWeight(layer, fullBody ? 0 : 1);
            string layerState=state+"/"+(fullBody?0:layer);
            if (fullBody)
            {
                model.Play(state);
                if (itemLayerState != layerState) a.Play(state, 0, action ? Mathf.Clamp01((Time.time-itemActionSince)/(itemActionUntil-itemActionSince)) : 0);
            }
            else if (itemLayerState != layerState) a.CrossFadeInFixedTime(state, .12f, layer);
            itemLayerState = layerState;
            return fullBody;
        }
        void ApplyItemAlignment()
        {
            if (model == null || condition.AllLimbsLost || IsHanging || (network != null && network.IsSpawned && network.State != PlayerState.Normal))
            { if (heldItemVisual != null) heldItemVisual.SetActive(false); return; }
            if (shownItem == ItemUses.UvLamp)
            {
                Vector3 forward = network != null && network.IsSpawned ? network.UvForward : transform.forward;
                bool crawling=model.Stage>=2&&(network!=null?network.PresentationMoving:mover.IsMoving);
                if(!crawling)ItemVisualAssets.Bend(model.Animator, model.Animator.GetBoneTransform(HumanBodyBones.Chest).position + forward * 3, .8f);
                ItemVisualAssets.AimLamp(model.Animator,forward,crawling);
            }
            bool moving=network!=null?network.PresentationMoving:mover.IsMoving;
            if (!(model.Stage>=2&&moving) && itemAction != null && Time.time < itemActionUntil && (itemAction == "ItemManipulate" || itemAction == "ItemPull"))
            {
                float weight = Mathf.Sin(Mathf.Clamp01((Time.time-itemActionSince)/(itemActionUntil-itemActionSince))*Mathf.PI);
                ItemVisualAssets.Bend(model.Animator, itemTarget, weight);
                var palm = Palm(model.Animator, false);
                AlignArm(model.Animator, false, Vector3.Lerp(palm, itemTarget, weight * .65f));
            }
            if (heldItemVisual != null)
            { heldItemVisual.SetActive(!IsHanging && seatedStation == null && rescueHang == null); ItemVisualAssets.Attach(heldItemVisual, model.Animator, visualItem); }
        }
        void ClearItemVisual()
        {
            if (toolUse != null) StopCoroutine(toolUse); toolUse = null;
            if (heldItemVisual != null) Destroy(heldItemVisual);
            heldItemVisual = null; shownItem = itemAction = itemLayerState = gestureItem = null; itemActionUntil = 0; interactionMoveLockUntil = 0;
        }
    }
}
