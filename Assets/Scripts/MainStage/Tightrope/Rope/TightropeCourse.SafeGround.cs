using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCourse
    {
        // 2026-11-13 담당자 요청: 인간 대포 안전지대와 같은 초록 지면을 외줄 시작·도착 발판 윗면에 깐다.
        // 외형 전용(충돌체 없음)이며, 재질을 비워 두면(프리팹 기본) 깔지 않아 다른 씬은 바뀌지 않는다.
        [Header("안전 지면 초록 (비우면 없음)")]
        [Tooltip("시작·도착 발판 윗면에 까는 초록 재질. 인간 대포 초록 지면과 같은 Color 아틀라스 재질.")]
        [SerializeField] Material safeGroundMaterial;
        [Tooltip("재질 아틀라스에서 초록 칸의 UV.")]
        [SerializeField] Vector2 safeGroundUv = new(0.5347f, 0.2872f);
        // 발판 윗면과 겹쳐 깜빡이지 않도록 띄우는 높이(m).
        const float SafeGroundLift = 0.01f;

        /// <summary>발판 외형(준비본 포함)의 실제 윗면에 같은 가로·세로의 초록 판을 덧붙인다.</summary>
        void AddSafeGround(GameObject platform, Bounds floor)
        {
            if (safeGroundMaterial == null || platform == null) return;
            float top = floor.max.y;
            foreach (var renderer in platform.GetComponentsInChildren<Renderer>())
                if (!TightropeOutline.IsOutlineRenderer(renderer))
                    top = Mathf.Max(top, generated.InverseTransformPoint(renderer.bounds.max).y);
            var green = new GameObject(platform.name + "SafeGround");
            green.transform.SetParent(generated, false);
            green.transform.localPosition = new Vector3(floor.center.x, top + SafeGroundLift, floor.center.z);
            float x = floor.extents.x, z = floor.extents.z;
            var mesh = new Mesh { name = green.name };
            mesh.SetVertices(new[] { new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z), new Vector3(x, 0f, z), new Vector3(x, 0f, -z) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.SetUVs(0, new[] { safeGroundUv, safeGroundUv, safeGroundUv, safeGroundUv });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.RecalculateBounds();
            green.AddComponent<MeshFilter>().sharedMesh = mesh;
            var view = green.AddComponent<MeshRenderer>();
            view.sharedMaterial = safeGroundMaterial;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
