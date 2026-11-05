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
            return Mathf.Abs(feet.y - b.max.y) <= .35f && feet.x >= b.min.x + .15f && feet.x <= b.max.x - .15f
                && feet.z >= b.min.z + .15f && feet.z <= b.max.z - .15f;
        }

        public GameObject CreateFinishWalls()
        {
            var b = FinishFloor.bounds;
            return RehearsalArrivalWalls.Create(transform, "CannonArrivalWalls", b);
        }
    }
}
