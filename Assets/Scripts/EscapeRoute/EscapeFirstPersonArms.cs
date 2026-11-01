using System.Collections.Generic;
using System.Linq;
using CurtainCall.Cameras;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Local, cosmetic first-person arms, baked from the current avatar. No gameplay or network objects.</summary>
    [DefaultExecutionOrder(2000)]
    public sealed class EscapeFirstPersonArms : MonoBehaviour
    {
        const int ViewLayer = 31;
        const int ViewMask = 1 << ViewLayer;
        readonly Mesh[] meshes = new Mesh[2];
        readonly Transform[] arms = new Transform[2];
        readonly Renderer[] renderers = new Renderer[2];
        Camera mainCamera, viewCamera;
        UniversalAdditionalCameraData baseData;
        GameObject sourceModel;
        int originalLayerBit;
        float phase, amplitude;
        MaterialPropertyBlock materialBlock;
        public int ArmMeshCount => (meshes[0] != null ? 1 : 0) + (meshes[1] != null ? 1 : 0);

        void LateUpdate()
        {
            var player = NetworkPlayer.Local;
            var director = CameraViewDirector.Current;
            bool first = EscapePerspectiveToggle.Current != null && EscapePerspectiveToggle.Current.InEscapeScene
                && director != null && director.isActiveAndEnabled && director.IsFirstPerson && !SceneCurtainTransition.IsActive
                && player != null && player.IsSpawned && player.State == PlayerState.Normal;
            if (!first) { SetVisible(false, BodyPart.None); return; }
            var mover = player.GetComponent<PlayerMover>();
            bool running = !InputGate.IsBlocked && mover != null && mover.IsMoving && mover.IsSprinting && !mover.IsAirborne;
            if (!running)
            {
                SetVisible(false, player.LostParts);
                amplitude = phase = 0;
                return;
            }
            var slot = player.GetComponent<PlayerModelSlot>();
            var model = slot != null ? slot.CurrentModel : null;
            if (model == null || Camera.main == null) { SetVisible(false, BodyPart.None); return; }
            if (mainCamera != Camera.main)
            {
                ClearView();
                if (!CreateViewCamera(Camera.main)) return;
            }
            if (sourceModel != model)
            {
                ClearMeshes(); sourceModel = model;
                var human = model.GetComponent<HumanoidModel>();
                if (human != null) BuildArms(human);
            }
            // Dedicated hand poses for tools/poles are separate from this locomotion view model.
            var pilot = player.GetComponent<HumanCannonPilot>();
            bool occupied = HumanCannonCarry.Held(player.OwnerClientId) != null
                || pilot != null && (pilot.IsHoldingPole || pilot.IsFlying)
                || HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsSeated(player.OwnerClientId);
            if (occupied) { SetVisible(false, player.LostParts); amplitude = phase = 0; return; }
            amplitude = Mathf.MoveTowards(amplitude, 1, Time.deltaTime * 5);
            phase = Mathf.Repeat(phase + Time.deltaTime * 12, Mathf.PI * 2);
            Pose(0, amplitude, phase); Pose(1, amplitude, phase + Mathf.PI);
            SetVisible(true, player.LostParts);
        }

        bool CreateViewCamera(Camera target)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                || LayerMask.LayerToName(ViewLayer) != "FirstPersonArms") return false;
            mainCamera = target;
            originalLayerBit = target.cullingMask & ViewMask;
            baseData = target.GetUniversalAdditionalCameraData();
            var go = new GameObject("Local first-person arms camera");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(target.transform, false);
            viewCamera = go.AddComponent<Camera>();
            viewCamera.enabled = false;
            viewCamera.fieldOfView = 60;
            viewCamera.nearClipPlane = .01f; viewCamera.farClipPlane = 3;
            viewCamera.cullingMask = ViewMask;
            viewCamera.allowHDR = target.allowHDR; viewCamera.allowMSAA = target.allowMSAA;
            var data = viewCamera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay; data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            baseData.cameraStack.Add(viewCamera);
            return true;
        }

        void BuildArms(HumanoidModel human)
        {
            if (human.Animator == null || !human.Animator.isHuman || human.Limbs == null || human.Limbs.Length < 4) return;
            materialBlock ??= new MaterialPropertyBlock();
            for (int side = 0; side < 2; side++)
            {
                var source = human.Limbs[side + 2];
                var upper = human.Animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var hand = human.Animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                if (source == null || source.sharedMesh == null || upper == null || hand == null) continue;
                meshes[side] = BakeRunningArm(human, side);
                if (meshes[side] == null) continue;
                var go = new GameObject(side == 0 ? "Local left arm" : "Local right arm", typeof(MeshFilter), typeof(MeshRenderer));
                go.hideFlags = HideFlags.DontSave; go.layer = ViewLayer;
                go.transform.SetParent(viewCamera.transform, false);
                arms[side] = go.transform;
                go.GetComponent<MeshFilter>().sharedMesh = meshes[side];
                var renderer = go.GetComponent<MeshRenderer>(); renderers[side] = renderer;
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.enabled = false;
                for (int material = 0; material < source.sharedMaterials.Length; material++)
                { source.GetPropertyBlock(materialBlock, material); renderer.SetPropertyBlock(materialBlock, material); materialBlock.Clear(); }
                Pose(side, 0, 0);
            }
        }

        static Mesh BakeRunningArm(HumanoidModel human, int side)
        {
            // Only transforms are copied. Never pose the live player's animator or duplicate gameplay components.
            var bones = new Dictionary<Transform, Transform>();
            var root = CopySkeleton(human.Animator.transform, null, bones);
            try
            {
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.localScale = Vector3.one;
                using (var handler = new HumanPoseHandler(human.Animator.avatar, root))
                {
                    var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                    System.Array.Clear(pose.muscles, 0, pose.muscles.Length);
                    var names = HumanTrait.MuscleName;
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (names[i].Contains("Forearm Stretch")) pose.muscles[i] = -.25f;
                        else if (names[i].Contains("Arm Down-Up")) pose.muscles[i] = -.5f;
                        else if (names[i].Contains("Stretched"))
                            pose.muscles[i] = names[i].Contains("Thumb") ? -.4f : -.95f;
                    }
                    handler.SetHumanPose(ref pose);
                }
                var source = human.Limbs[side + 2];
                var copy = bones[source.transform].gameObject.AddComponent<SkinnedMeshRenderer>();
                copy.enabled = false; copy.sharedMesh = source.sharedMesh;
                copy.bones = source.bones.Select(b => bones[b]).ToArray();
                copy.rootBone = source.rootBone != null ? bones[source.rootBone] : null;
                var upper = bones[human.Animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm)];
                var hand = bones[human.Animator.GetBoneTransform(side == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand)];
                return BakeArm(copy, upper, hand, root.forward);
            }
            finally { DestroyOwned(root.gameObject); }
        }

        static Transform CopySkeleton(Transform source, Transform parent, Dictionary<Transform, Transform> bones)
        {
            var go = new GameObject(source.name); go.hideFlags = HideFlags.HideAndDontSave;
            var copy = go.transform; copy.SetParent(parent, false);
            copy.localPosition = source.localPosition; copy.localRotation = source.localRotation; copy.localScale = source.localScale;
            bones.Add(source, copy);
            foreach (Transform child in source) CopySkeleton(child, copy, bones);
            return copy;
        }

        internal static Mesh BakeArm(SkinnedMeshRenderer source, Transform upper, Transform hand, Vector3 referenceUp)
        {
            var mesh = new Mesh { name = "Local view arm snapshot", hideFlags = HideFlags.DontSave };
            source.BakeMesh(mesh);
            var axis = hand.position - upper.position;
            if (axis.sqrMagnitude < .00001f) { DestroyOwned(mesh); return null; }
            if (Mathf.Abs(Vector3.Dot(axis.normalized, referenceUp.normalized)) > .98f) referenceUp = upper.up;
            var pose = Matrix4x4.TRS(upper.position, Quaternion.LookRotation(axis, referenceUp), Vector3.one);
            var matrix = pose.inverse * source.transform.localToWorldMatrix;
            var normal = matrix.inverse.transpose;
            float scale = .52f / axis.magnitude;
            var vertices = mesh.vertices; var normals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]) * scale;
            for (int i = 0; i < normals.Length; i++) normals[i] = normal.MultiplyVector(normals[i]).normalized;
            mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        void Pose(int side, float amount, float angle)
        {
            var arm = arms[side]; if (arm == null) return;
            float sign = side == 0 ? -1 : 1;
            var rest = new Vector3(sign * .42f, -.49f, .15f);
            float bob = Mathf.Sin(angle);
            arm.localPosition = rest + new Vector3(-sign * bob * .018f, bob * .025f, bob * .07f) * amount;
            // The returning arm drops below the screen before it yields to the opposite hand.
            arm.localPosition += Vector3.down * ((1 - Mathf.Max(0, bob)) * .22f);
            var direction = new Vector3(-sign * .08f, .28f, .50f);
            arm.localRotation = Quaternion.LookRotation(direction, Vector3.up)
                * Quaternion.Euler(bob * 10 * amount, 0, sign * (-12 + bob * 5 * amount));
            arm.localScale = Vector3.one;
        }

        void SetVisible(bool visible, BodyPart lost)
        {
            bool any = false;
            for (int i = 0; i < 2; i++)
                if (renderers[i] != null)
                {
                    bool intact = (lost & (i == 0 ? BodyPart.LeftArm : BodyPart.RightArm)) == 0;
                    bool forwardSwing = Mathf.Sin(phase + (i == 0 ? 0 : Mathf.PI)) > .05f;
                    renderers[i].enabled = visible && intact && forwardSwing; any |= renderers[i].enabled;
                }
            if (viewCamera != null) viewCamera.enabled = any;
            if (mainCamera != null) mainCamera.cullingMask = any ? mainCamera.cullingMask & ~ViewMask
                : (mainCamera.cullingMask & ~ViewMask) | originalLayerBit;
        }
        void ClearMeshes()
        {
            SetVisible(false, BodyPart.None);
            for (int i = 0; i < 2; i++)
            {
                if (arms[i] != null) DestroyOwned(arms[i].gameObject);
                if (meshes[i] != null) DestroyOwned(meshes[i]);
                arms[i] = null; renderers[i] = null; meshes[i] = null;
            }
            sourceModel = null;
        }
        void ClearView()
        {
            ClearMeshes();
            if (baseData != null && viewCamera != null) baseData.cameraStack.Remove(viewCamera);
            if (viewCamera != null) DestroyOwned(viewCamera.gameObject);
            viewCamera = null; baseData = null; mainCamera = null;
        }
        static void DestroyOwned(Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        void OnDisable() => ClearView();
    }
}
