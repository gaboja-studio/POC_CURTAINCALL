using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 플레이어를 비추는 평면 거울(2026-10-30 확정). 거울 유리 렌더러에 붙인다.
    /// 메인 카메라를 거울면에 대칭시킨 카메라로 한 프레임 그려 화면 공간으로 입힌다.
    /// 거울이 보이고 가까울 때만 그리므로 멀리서는 비용이 없다. 거울면 법선은 가장 얇은 축으로 잡는다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class PlanarMirror : MonoBehaviour
    {
        [SerializeField] Vector2Int resolution = new(640, 360);
        [Tooltip("이보다 멀면 그리지 않는다(m).")]
        [SerializeField, Min(1f)] float activeDistance = 14f;
        [SerializeField] Color tint = new(.82f, .86f, .9f, 1f);
        [SerializeField] float clipOffset = .02f;

        Renderer glass;
        Camera mirrorCamera;
        RenderTexture texture;
        Material material;
        bool rendering;

        void Awake()
        {
            glass = GetComponent<Renderer>();
            var shader = Shader.Find("CurtainCall/PlanarMirror");
            if (shader == null) { Debug.LogWarning("[PlanarMirror] CurtainCall/PlanarMirror shader missing", this); enabled = false; return; }
            texture = new RenderTexture(resolution.x, resolution.y, 16) { name = name + "_Mirror" };
            material = new Material(shader) { name = name + "_MirrorMat", hideFlags = HideFlags.DontSave };
            material.SetTexture("_ReflectionTex", texture); material.SetColor("_Tint", tint);
            glass.sharedMaterial = material;
            var go = new GameObject(name + "_MirrorCamera") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            mirrorCamera = go.AddComponent<Camera>();
            mirrorCamera.enabled = false; mirrorCamera.targetTexture = texture;
            mirrorCamera.clearFlags = CameraClearFlags.SolidColor; mirrorCamera.backgroundColor = Color.black;
            mirrorCamera.nearClipPlane = .05f; mirrorCamera.farClipPlane = 60f;
        }

        void OnEnable() { RenderPipelineManager.beginCameraRendering += Begin; RenderPipelineManager.endCameraRendering += End; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= Begin; RenderPipelineManager.endCameraRendering -= End; if (mirrorCamera) mirrorCamera.enabled = false; }

        void LateUpdate()
        {
            if (rendering) { mirrorCamera.enabled = false; rendering = false; }
            var main = Camera.main;
            if (main == null || !glass.isVisible) return;
            var position = glass.bounds.center;
            if (Vector3.Distance(main.transform.position, position) > activeDistance) return;
            var normal = Normal(main.transform.position, position);
            if (Vector3.Dot(main.transform.position - position, normal) <= 0f) return; // 뒷면에서는 그리지 않는다

            float d = -Vector3.Dot(normal, position) - clipOffset;
            var plane = new Vector4(normal.x, normal.y, normal.z, d);
            var reflection = Reflection(plane);
            mirrorCamera.fieldOfView = main.fieldOfView; mirrorCamera.aspect = main.aspect;
            mirrorCamera.transform.position = reflection.MultiplyPoint(main.transform.position);
            var forward = reflection.MultiplyVector(main.transform.forward); var up = reflection.MultiplyVector(main.transform.up);
            mirrorCamera.transform.rotation = Quaternion.LookRotation(forward, up);
            mirrorCamera.worldToCameraMatrix = main.worldToCameraMatrix * reflection;
            var clip = CameraSpacePlane(mirrorCamera, position, normal, 1f);
            mirrorCamera.projectionMatrix = main.CalculateObliqueMatrix(clip);
            mirrorCamera.enabled = true; rendering = true;
        }

        // 대칭 카메라는 좌우가 뒤집히므로 그 동안만 컬링을 반대로 한다.
        void Begin(ScriptableRenderContext context, Camera camera) { if (camera == mirrorCamera) GL.invertCulling = true; }
        void End(ScriptableRenderContext context, Camera camera) { if (camera == mirrorCamera) GL.invertCulling = false; }

        Vector3 Normal(Vector3 viewer, Vector3 center)
        {
            var t = transform; var s = t.lossyScale; var size = glass.localBounds.size;
            var extents = new Vector3(Mathf.Abs(size.x * s.x), Mathf.Abs(size.y * s.y), Mathf.Abs(size.z * s.z));
            var axis = extents.x <= extents.y && extents.x <= extents.z ? t.right : extents.y <= extents.z ? t.up : t.forward;
            return Vector3.Dot(viewer - center, axis) >= 0f ? axis : -axis;
        }

        Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal, float sign)
        {
            var offset = position + normal * clipOffset; var m = camera.worldToCameraMatrix;
            var p = m.MultiplyPoint(offset); var n = m.MultiplyVector(normal).normalized * sign;
            return new Vector4(n.x, n.y, n.z, -Vector3.Dot(p, n));
        }

        static Matrix4x4 Reflection(Vector4 plane)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * plane[0] * plane[0]; m.m01 = -2f * plane[0] * plane[1]; m.m02 = -2f * plane[0] * plane[2]; m.m03 = -2f * plane[3] * plane[0];
            m.m10 = -2f * plane[1] * plane[0]; m.m11 = 1f - 2f * plane[1] * plane[1]; m.m12 = -2f * plane[1] * plane[2]; m.m13 = -2f * plane[3] * plane[1];
            m.m20 = -2f * plane[2] * plane[0]; m.m21 = -2f * plane[2] * plane[1]; m.m22 = 1f - 2f * plane[2] * plane[2]; m.m23 = -2f * plane[3] * plane[2];
            return m;
        }

        void OnDestroy()
        {
            if (mirrorCamera) Destroy(mirrorCamera.gameObject);
            if (texture) { texture.Release(); Destroy(texture); }
            if (material) Destroy(material);
        }
    }
}
