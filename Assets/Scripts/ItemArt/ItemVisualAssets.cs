using System.Collections.Generic;
using CurtainCall.Player;
using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.ItemArt
{
    public static class ItemVisualAssets
    {
        public static readonly string[] Ids = { "rescue-gloves", "sticky-gloves", "balloon-pants", "spring-shoes", "magician-hairpin", "stake-puller", "uv-lamp", "route-key" };
        public static readonly string[] Motions = { "ItemStrike", "ItemManipulate", "ItemPull", "ItemAim", "ItemHold", "ItemEquip", "ItemStow", "ItemCrawl" };
        static readonly Dictionary<string, Sprite> icons = new();
        static readonly Dictionary<string, GameObject> prefabs = new();
        public static bool IsTool(string id) => ItemCatalog.Get(id)?.kind == ItemKind.Tool;
        public static string State(string motion, int stage) => motion + (stage > 0 ? "_Cut" + stage : "");
        public static Sprite Icon(string id, bool shop = false)
        {
            if (ExplorationKeys.IsKey(id)) id = ExplorationKeys.VisualId;
            string key = id + (shop ? "-shop" : "-slot");
            if (!icons.TryGetValue(key, out var icon)) icons[key] = icon = Resources.Load<Sprite>("ItemArt/Icons/" + key);
            return icon;
        }
        public static GameObject Create(string id, Transform parent = null)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (ExplorationKeys.IsKey(id)) id = ExplorationKeys.VisualId;
            if (!prefabs.TryGetValue(id, out var prefab)) prefabs[id] = prefab = Resources.Load<GameObject>("ItemArt/Prefabs/" + id);
            return prefab != null ? Object.Instantiate(prefab, parent) : null;
        }
        public static Vector3 Grip(string id) => id == "uv-lamp" ? new Vector3(-.125f,.14f,0) : id == "magician-hairpin" || ExplorationKeys.IsKey(id) ? new Vector3(0,.05f,0) : Vector3.zero;
        public static string DisplayName(string id) => ItemCatalog.Get(id)?.displayName??id;
        public static void AimLamp(Animator a,Vector3 forward,bool crawling)
        {
            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=a.transform.forward;
            if(!crawling)
            {
                var chest=a.GetBoneTransform(HumanBodyBones.Chest);
                var right=Vector3.Cross(Vector3.up,forward);
                HumanoidPlayerPresentation.AlignArm(a,false,chest.position+forward*.45f+right*.18f-Vector3.up*.035f);
            }
            var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
            var fingers=(a.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position-hand.position).normalized;
            var across=(a.GetBoneTransform(HumanBodyBones.RightIndexProximal).position-a.GetBoneTransform(HumanBodyBones.RightLittleProximal).position).normalized;
            var frame=Quaternion.LookRotation(Vector3.Cross(across,fingers),across);
            var desired=Quaternion.LookRotation(Vector3.Cross(Vector3.up,forward),Vector3.up);
            hand.rotation=desired*Quaternion.Inverse(frame)*hand.rotation;
        }
        // The frame is derived from the actual retargeted hand, so all four avatars share a grip.
        public static void Attach(GameObject prop, Animator animator, string id)
        {
            if (ExplorationKeys.IsKey(id)) id = ExplorationKeys.VisualId;
            if (prop == null || animator == null || !animator.isHuman) return;
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            if (hand == null || middle == null || index == null || little == null) return;
            Vector3 fingers = (middle.position - hand.position).normalized;
            Vector3 cross = (index.position - little.position).normalized;
            Vector3 normal = Vector3.Cross(cross, fingers).normalized;
            var rotation = Quaternion.LookRotation(normal, cross);
            // The lamp housing must sit ahead of the palm, not between hand and torso.
            if(id=="uv-lamp")rotation*=Quaternion.AngleAxis(180,Vector3.up);
            // Bone origins lie inside the palm. Place the shaft in the curled-finger
            // pocket, on the palmar side of the knuckles, never at the palm origin.
            float radius = id == "uv-lamp" ? .017f : id == "magician-hairpin" || id=="route-key" ? .009f : .020f;
            float width = Vector3.Distance(index.position, little.position);
            float palmPadding = .024f + Mathf.Clamp((width-.075f)*.5f,0,.007f);
            Vector3 gripCenter = middle.position - fingers * .028f - normal * (radius + palmPadding);
            ItemHandGrip.Apply(animator, id, gripCenter, fingers, normal, radius);
            prop.transform.SetPositionAndRotation(gripCenter - rotation * Grip(id), rotation);
            prop.transform.localScale = Vector3.one;
        }
        public static void Bend(Animator animator, Vector3 target, float weight)
        {
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null || weight <= 0) return;
            Vector3 delta = animator.transform.InverseTransformDirection(target - chest.position);
            float pitch = -Mathf.Clamp(Mathf.Atan2(delta.y, Mathf.Max(.15f, new Vector2(delta.x,delta.z).magnitude)) * Mathf.Rad2Deg, -24, 24);
            chest.rotation = Quaternion.AngleAxis(pitch * weight, animator.transform.right) * chest.rotation;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { icons.Clear(); prefabs.Clear(); }
    }
}
