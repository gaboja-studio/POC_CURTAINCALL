using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void BuildLabyrinthEntrance(EscapePursuerRoute route)
        {
            if (!route || !labyrinthFrame || !labyrinthRoot)
                throw new InvalidOperationException("Initialize the copied labyrinth before rebuilding the entrance.");
            if (labyrinthRoot.Find("Labyrinth 00 pressure chamber and process gallery"))
                throw new InvalidOperationException("The entrance has already been rebuilt.");
            var background = labyrinthFrame.Find("EscapeBackground");
            if (!background || !background.Find("17_BrutalistRoute/01_Understage")
                || !background.Find("17_BrutalistRoute/02_CompressionGallery"))
                throw new InvalidOperationException("The original entrance shell is required.");
            LAnchor("Labyrinth 00 pressure chamber and process gallery", new Vector3(15, 0, 11));

            // Preserve the continuous Y0 floors, countdown/encounter triggers, first cannon,
            // X15/Z-10..13 emergence sweep, and the X11..19/Y0..3.8 low-door opening at Z21.
            // These exact static names are old overhead boxes, not gameplay or pursuit roots.
            LCutNamedBox("무대 리프트 기초 천장", new Bounds(new Vector3(15, 12, 11.25f), new Vector3(34, 10, 28)));
            LCutNamedBox("무대 하중 전이 보", new Bounds(new Vector3(15, 7, 11), new Vector3(34, 6, 26)));
            LCutNamedBox("무대 하중 교각", new Bounds(new Vector3(4, 6, 10), new Vector3(6, 14, 16)));
            LCutNamedBox("엇갈린 거대 벽기둥", new Bounds(new Vector3(8.6f, 12, 49), new Vector3(3.8f, 32, 42)));
            LCutNamedBox("낮은 횡단 보", new Bounds(new Vector3(16, 17, 49), new Vector3(21, 4, 42)));
            LEntranceClearLooseScenery(background);

            BuildLabyrinthPressureChamber();
            BuildLabyrinthProcessGallery();
            BuildLabyrinthIntakeEndReturns();
            FitLabyrinthArrivalWindowBoundary(background);
            LEntranceFitRetainedLights(background);
        }

        static void BuildLabyrinthPressureChamber()
        {
            // A single high enclosure replaces the fragmented low beam stack. The west
            // machine block occupies the original pier footprint; every upper mass bears
            // on it, the retained perimeter walls, or the two full-height east piers.
            LBox("Labyrinth arrival sealed pressure chamber roof", new Vector3(15, 13, 11.25f), new Vector3(32, 2, 24.5f), bConcrete);
            LBox("Labyrinth arrival full-height west pressure block", new Vector3(4, 6, 10), new Vector3(5, 12, 15), bConcrete);
            foreach (float z in new[] { 3f, 19f })
                LBox("Labyrinth arrival east roof bearing pier", new Vector3(28.8f, 6, z), new Vector3(2.4f, 12, 1.8f), bConcrete);
            foreach (float z in new[] { 6f, 16f })
                LBox("Labyrinth arrival high transverse pressure rib", new Vector3(15, 11.4f, z), new Vector3(32, 1.2f, 1.4f), wornSteel);

            // The housing is a closed vertical face, never a sequence of low reachable steps.
            LBox("Labyrinth arrival machine sealed service face", new Vector3(6.59f, 6, 10), new Vector3(.18f, 9.8f, 12), wornSteel, false);
            foreach (float z in new[] { 4.3f, 10f, 15.7f })
            {
                LBox("Labyrinth arrival machine structural strap", new Vector3(6.71f, 6, z), new Vector3(.08f, 10.4f, .22f), bSteel, false);
                foreach (float y in new[] { 1.1f, 10.9f })
                    LBox("Labyrinth arrival machine coarse anchor", new Vector3(6.79f, y, z), new Vector3(.08f, .3f, .35f), maintenancePaint, false);
            }
            LHallMountedLamp("Labyrinth arrival north portal task light", new Vector3(15, 10.72f, 16), new Vector3(15.8f, 0, 20.4f), 235);
            LHallMountedLamp("Labyrinth arrival roof reveal task light", new Vector3(24, 10.72f, 6), new Vector3(15, 1.5f, 1), 125);

            // Keep the low portal's original collision envelope and its pursuer approach.
            // The new steel face sits on its south face, entirely outside the clear opening.
            foreach (float x in new[] { 10.85f, 19.15f })
                LBox("Labyrinth arrival exit upright face", new Vector3(x, 1.9f, 19.46f), new Vector3(.3f, 3.8f, .08f), wornSteel, false);
            LBox("Labyrinth arrival exit lintel face", new Vector3(15, 4.05f, 19.46f), new Vector3(8.6f, .5f, .08f), wornSteel, false);
        }

        static void BuildLabyrinthProcessGallery()
        {
            // Original corridor floor is X10..22.5/Z22.5..83. The west gutter remains
            // open down to its existing Y-15 floor. New buttresses end at X9.5; no floor,
            // shelf or horizontal catch surface is introduced beside or below the route.
            foreach (float z in new[] { 28f, 52f, 68f })
            {
                LBox("Labyrinth process gallery full-height west buttress", new Vector3(8.65f, 1.15f, z), new Vector3(1.7f, 32.3f, 1.8f), bConcrete);
                LBox("Labyrinth process gallery overhead transfer beam", new Vector3(15.45f, 16.5f, z), new Vector3(13.6f, 1.6f, 1.8f), bConcrete);
                // The retained upper concrete mass begins at Y29. Each beam also hangs
                // from that mass, so the east end needs no column in the player/service lane.
                foreach (float x in new[] { 14f, 20f })
                {
                    LBox("Labyrinth process gallery roof suspension", new Vector3(x, 23.15f, z), new Vector3(.26f, 11.7f, .4f), wornSteel);
                    LBox("Labyrinth process gallery cast-in roof anchor", new Vector3(x, 29.12f, z), new Vector3(1.1f, .24f, 1.3f), wornSteel, false);
                }
                LHallMountedLamp("Labyrinth gallery direction task light", new Vector3(16.4f, 15.6f, z),
                    new Vector3(z < 60 ? 14.5f : 19, 0, z + 7), z < 60 ? 250 : 300);
            }
            // Opposed human-intake and effluent reveals remain between the frame bays.
            // Keep glass X9.91, intake X25/Z33, the cart/NavMeshLink at X11/Z45.25,
            // and the concealed departure/first-cannon service structures unchanged.
        }

        static void BuildLabyrinthIntakeEndReturns()
        {
            // The window cut leaves knee-high concrete stubs at its ends. They look
            // climbable but lead nowhere: replace them with full-height wall returns,
            // flush with the X22.5 edge of the continuous public gallery floor.
            foreach (var end in new[] { (z: 23.9f, depth: 2.8f), (z: 41.4f, depth: 1.4f) })
            {
                LCutNamedBox("Concrete remainder", new Bounds(new Vector3(23.6f, .6f, end.z),
                    new Vector3(2.22f, 1.22f, end.depth + .02f)));
                LBox("Labyrinth intake full-height closed window return", new Vector3(23.6f, 4.55f, end.z),
                    new Vector3(2.2f, 9.1f, end.depth), bConcrete);
            }
        }

        static void FitLabyrinthArrivalWindowBoundary(Transform background)
        {
            var frames = background.Find("51_ObservationSafetyFrames");
            if (!frames) throw new InvalidOperationException("The retained arrival observation frame is required.");
            // The old grille sits one metre behind a low sill, leaving a pointless
            // jumpable shelf. Bring its visible frame to the public floor edge instead.
            foreach (var renderer in frames.GetComponentsInChildren<MeshRenderer>(true))
            {
                Vector3 center = labyrinthFrame.InverseTransformPoint(renderer.bounds.center);
                if (center.x < 30.8f || center.x > 31.2f || center.z < 1 || center.z > 22
                    || renderer.bounds.size.x > .5f) continue;
                renderer.transform.position += labyrinthFrame.TransformVector(new Vector3(-1.05f, 0, 0));
                if (!renderer.name.StartsWith("시작 전망창 쇠창살 ") || renderer.GetComponent<Collider>()) continue;
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var barrier = renderer.gameObject.AddComponent<BoxCollider>();
                barrier.center = mesh.bounds.center;
                barrier.size = mesh.bounds.size;
            }
        }

        static void LEntranceClearLooseScenery(Transform background)
        {
            var service = background.Find("18_WorkingFacilityFinish/01_02_ServiceRuns");
            if (service)
                foreach (var item in service.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == "이동 정비 카트" || t.name == "사용한 케이블 릴"
                        || t.name == "임시 적치 팔레트" || t.name == "적치한 보수 자재").ToArray())
                {
                    var p = labyrinthFrame.InverseTransformPoint(item.position);
                    if (p.x > 7 && p.x < 22 && p.z > 2 && p.z < 60
                        && item.GetComponentsInChildren<MonoBehaviour>(true).Length == 0)
                        Object.DestroyImmediate(item.gameObject);
                }
            var pallet = background.Find("15_ServiceContext/정비용 팔레트");
            if (pallet && pallet.GetComponentsInChildren<MonoBehaviour>(true).Length == 0)
                Object.DestroyImmediate(pallet.gameObject);
        }

        static void LEntranceFitRetainedLights(Transform background)
        {
            // Cutting the intake wall otherwise leaves its old low fixture floating in
            // the widened walking space. Reattach it to the new grille, keeping references.
            foreach (int index in new[] { 1, 2 })
            {
                // The legacy reveal root contains several fixtures with the same name.
                // Select the abandoned floor-level fixtures, not the first named child.
                foreach (var fixture in background.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.parent && t.parent.name == "50_ActualRouteReveal"
                        && t.name == "창 변경 후 벽 직부 작업등 0" + index
                        && labyrinthFrame.InverseTransformPoint(t.position).y < 1))
                {
                    float z = index == 1 ? 30 : 48;
                    fixture.position = LW(new Vector3(index == 1 ? 25.28f : 22.5f, 4.6f, z));
                    foreach (var light in fixture.GetComponentsInChildren<Light>(true))
                        light.transform.rotation = Quaternion.LookRotation(LW(new Vector3(17, 0, z + 4)) - light.transform.position);
                }
            }
            foreach (var fixture in background.GetComponentsInChildren<Transform>(true)
                .Where(t => t.parent && t.parent.name == "50_ActualRouteReveal"
                    && t.name == "창 변경 후 벽 직부 작업등 01"
                    && Mathf.Abs(labyrinthFrame.InverseTransformPoint(t.position).z - 41.4f) < .1f))
            {
                fixture.SetPositionAndRotation(LW(new Vector3(22.5f, 3.5f, 41.4f)),
                    labyrinthFrame.rotation * Quaternion.LookRotation(Vector3.left));
            }
        }
    }
}
