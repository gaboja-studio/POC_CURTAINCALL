using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>Scene copies retain the Start-connected performance's complete device settings and art overrides.</summary>
    public sealed class RehearsalCannonCourse : MonoBehaviour
    {
        public HumanCannonSystem Cannon;
        public HumanCannonRouteSystem Route;
        public HumanCannonStation[] Stations;
        public HumanCannonTrampoline Trampoline;
        public HumanCannonTransport Transport;
        public Transform StartDeck, MiddleDeck, LowerDeck, FinishDeck;
        public Collider StartFloor, MiddleFloor, LowerFloor, FinishFloor;
        public CurtainCall.UI.GuidesV1.GuideView Guide;
        public CurtainCall.UI.TooltipsV1.TooltipView Tooltip;
        public CurtainCall.UI.SceneIntegration.HumanCannonUiBinding DeviceUi;
        public const float Elevation = 2f;
        [Tooltip("최종 도착대 윗면에 까는 초록(공연 안전지대 초록 지면과 같은 재질). 비우면 깔지 않는다.")]
        public Material SafeGroundMaterial;
        [Tooltip("재질 아틀라스에서 초록 칸의 UV(공연 초록 지면 메시와 같은 값).")]
        public Vector2 SafeGroundUv = new(0.5347f, 0.2872f);
        GameObject safeGround;

        public void Prepare(Vector3 origin)
        {
            origin += Vector3.up * Elevation;
            transform.position = origin;
            // Raise every deck/device together; native launch speed and relative layout stay intact.
            PlaceDeck(StartDeck, new Vector3(-5, 2, -16), new Vector2(12, 8));
            PlaceDeck(MiddleDeck, new Vector3(-4, 2, 6), new Vector2(16, 10));
            PlaceDeck(LowerDeck, new Vector3(7, .5f, -8), new Vector2(12, 14));
            // Keep the native transport's boarding deck clear of the raised finish floor.
            PlaceDeck(FinishDeck, new Vector3(10, 3.8f, -2), new Vector2(8, 10));
            AddSafeGround();
            for (int i = 0; i < Stations.Length; i++)
            {
                bool second = i >= 2;
                Stations[i].transform.SetPositionAndRotation(transform.TransformPoint(new Vector3(i % 2 == 0 ? -7 : -3, 2, second ? 5 : -15)),
                    transform.rotation * Quaternion.LookRotation(second ? new Vector3(.6f, 0, -.8f) : Vector3.forward));
            }
            // Both first shots must land before touching the parked fabric, including its original outline radius.
            Trampoline.transform.SetPositionAndRotation(transform.TransformPoint(new Vector3(1.5f, 2, 8)), transform.rotation * Quaternion.Euler(0, -90, 0));
            Transport.SetCourseRoute(transform.TransformPoint(new Vector3(6, 2, 7)), transform.TransformPoint(new Vector3(7, .5f, -5)), transform.rotation);
            Cannon.ConfigurePractice(Stations, origin.y + .15f);
            Route.ConfigurePractice(Transport, origin.y + .15f);
            DeviceUi.ConfigurePractice(Guide, Tooltip);
        }

        // 2026-11-15 사용자: 리허설의 마지막 도착 장소도 공연처럼 초록 안전지역으로 표시한다. 외형 전용(충돌 없음).
        void AddSafeGround()
        {
            if (SafeGroundMaterial == null || FinishDeck == null || safeGround != null) return;
            safeGround = new GameObject("FinishSafeGround");
            safeGround.transform.SetParent(FinishDeck, false);
            // 원본 발판은 4×4m, 윗면이 로컬 y=0이다. 발판 배율을 그대로 따라 윗면 전체를 덮는다.
            safeGround.transform.localPosition = new Vector3(0f, .01f, 0f);
            var mesh = new Mesh { name = "FinishSafeGround", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new[] { new Vector3(-2f, 0f, -2f), new Vector3(-2f, 0f, 2f), new Vector3(2f, 0f, 2f), new Vector3(2f, 0f, -2f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.SetUVs(0, new[] { SafeGroundUv, SafeGroundUv, SafeGroundUv, SafeGroundUv });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.RecalculateBounds();
            safeGround.AddComponent<MeshFilter>().sharedMesh = mesh;
            var view = safeGround.AddComponent<MeshRenderer>();
            view.sharedMaterial = SafeGroundMaterial;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void PlaceDeck(Transform deck, Vector3 position, Vector2 size)
        {
            deck.localPosition = position;
            deck.localRotation = Quaternion.identity;
            // Original FloorModulePrepared is four metres square, with its upper surface at local y=0.
            deck.localScale = new Vector3(size.x / 4f, 1f, size.y / 4f);
        }

        public Pose CheckpointPose(int checkpoint, int slot)
        {
            var floor = checkpoint > 0 ? MiddleFloor : StartFloor;
            var b = floor.bounds;
            float x = b.center.x - 3f + (slot % 4) * 1.5f;
            float z = b.center.z - b.extents.z + 1.4f + (slot / 4) * 1.2f;
            return new Pose(new Vector3(x, b.max.y, z), transform.rotation);
        }

        public static bool Contains(Collider floor, Vector3 feet)
        {
            if (floor == null) return false;
            var b = floor.bounds;
            return Mathf.Abs(feet.y - b.max.y) <= .35f && feet.x >= b.min.x && feet.x <= b.max.x
                && feet.z >= b.min.z && feet.z <= b.max.z;
        }

        public bool IsRecoveryDeck(Vector3 feet) => Contains(StartFloor, feet) || Contains(MiddleFloor, feet)
            || Contains(LowerFloor, feet) || Contains(FinishFloor, feet);

        public GameObject CreateFinishWalls()
        {
            var b = FinishFloor.bounds;
            return RehearsalArrivalWalls.Create(transform, "CannonArrivalWalls", b);
        }
    }
}
