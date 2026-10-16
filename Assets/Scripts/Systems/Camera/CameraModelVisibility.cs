using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Cameras
{
    /// <summary>게임 카메라가 렌더링하는 동안만 내 현재 모델을 숨긴다. 부상 계산의 Renderer.enabled는 보존한다.</summary>
    [RequireComponent(typeof(CameraViewDirector))]
    public sealed class CameraModelVisibility : MonoBehaviour
    {
        CameraViewDirector director;
        readonly List<Renderer> renderers = new();
        readonly Dictionary<Renderer, bool> originals = new();

        void Awake() => director = GetComponent<CameraViewDirector>();

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            Camera.onPreCull += BeginBuiltin;
            Camera.onPostRender += EndBuiltin;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Camera.onPreCull -= BeginBuiltin;
            Camera.onPostRender -= EndBuiltin;
            Restore();
        }

        void BeginCamera(ScriptableRenderContext context, Camera camera) => Hide(camera);
        void EndCamera(ScriptableRenderContext context, Camera camera) => Restore();

        void BeginBuiltin(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) Hide(camera);
        }

        void EndBuiltin(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) Restore();
        }

        void Hide(Camera camera)
        {
            Restore();
            if (camera != Camera.main || director == null || !director.HideLocalModel || director.LocalPlayer == null) return;
            var slot = director.LocalPlayer.GetComponent<PlayerModelSlot>();
            var model = slot != null ? slot.CurrentModel : null;
            if (model == null || !model.transform.IsChildOf(director.LocalPlayer.transform)) return;
            model.GetComponentsInChildren(true, renderers);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                originals[renderer] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }
            renderers.Clear();
        }

        void Restore()
        {
            foreach (var pair in originals)
                if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            originals.Clear();
        }
    }
}
