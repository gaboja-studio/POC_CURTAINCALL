using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Cameras.Editor
{
    /// <summary>Shared coverage rules for the authored Base/Pursuit map. Does not rebuild room geometry.</summary>
    public static class WaitingRoomCameraCollisionAuthoring
    {
        // Reviewed against all active mesh renderers in both scenes. Exact names deliberately exclude
        // signs, light housings, pipes, furniture and effects (e.g. GoldenLightWall).
        internal static readonly HashSet<string> StructureNames = new(StringComparer.Ordinal) {
            "WallSurfaces", "OuterWall", "DrumWall", "DomeCap", "Ceiling", "CeilingPanel",
            "RoofSlab", "RoofSheet", "PassageRoof", "PassageFloorCeiling", "BranchFloorCeiling",
            "CeilingBeam", "RoofGirderWeb", "RoofGirderFlange", "CanvasRoof", "CageRoof", "SlumpedConeTent",
            "PassageWall", "BranchWall", "GalleryWall", "GalleryRoof", "ApproachWall",
            "StageApproachRoof", "StageApproachSpandrel", "StagePortalHeader", "Vault",
            "SealedActualVaultTransition", "SealedDoorway", "BackWall", "SideWall", "WallPanel",
            "LiftShaftBack", "LiftShaftSide", "LiftShaftCap", "CarRoof", "CarBack", "CarSide", "ShaftAbove",
            "UpperInfill", "PortalHeader", "ShaftFacePlate", "CargoDockHeader", "CargoOuterReturn", "FormerEntryClosure",
            "LogisticsEast", "LogisticsWest", "LogisticsNorthInfill", "LogisticsPortalHeader",
            "LoadingBayHeader", "LowLogisticsDivider", "OpaqueLowerPanel", "Partition", "HiddenPanel",
            "TransomPanel", "ServiceDoorLeft", "ServiceDoorRight", "LockedDoorLeafL", "LockedDoorLeafR",
            "DockShutter", "EscapeDoor", "AssemblyDepartureDoor", "Corridor wall", "Enclosed ceiling",
            // Upper floors/decks also act as ceilings when the player is underneath them.
            "Floor", "Floor_BrokenConcrete", "RoundFloor", "GalleryFloor", "CarFloor", "ApproachFloor",
            "DeckPlate", "TopLanding", "MidLanding", "Landing", "LandingBranch", "PlatformTop",
            "RestoredFactoryDeckGrating", "DoorwayFloorInfill", "HiddenPassageThreshold",
            "TornCanopy"
        };

        public static MeshFilter[] Collect(Scene scene) => scene.GetRootGameObjects()
            .Where(root => root.name == "WaitingRoom_LayoutA" || root.name == "BasementEscapeConnection")
            .SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true))
            .Where(IsArchitecture).ToArray();

        static bool IsArchitecture(MeshFilter filter)
        {
            if (!filter.gameObject.activeInHierarchy || filter.sharedMesh == null) return false;
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null || !renderer.enabled) return false;
            if (StructureNames.Contains(filter.name) || filter.name.StartsWith("CeilingRepairPanel_", StringComparison.Ordinal)) return true;
            string path = AnimationUtility.CalculateTransformPath(filter.transform, null);
            return (filter.name == "Canvas" && path.Contains("_PatchedCeiling/"))
                || (filter.name == "Door" && path.Contains("/Restroom/Fixtures/StallDoor_"))
                || (filter.name == "Header" && (path.Contains("/StageProscenium/") || path.Contains("/NightMaintenance/")));
        }

        public static string Apply(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before authoring camera collision.");
            if (!WaitingRoomScenes.IsWaitingRoom(scene)) throw new InvalidOperationException("Select a Base or Pursuit waiting room scene.");
            var targets = Collect(scene);
            var unreadable = targets.Where(f => f.sharedMesh.name != "Cube" && !f.sharedMesh.isReadable).ToArray();
            if (unreadable.Length != 0) throw new InvalidOperationException("Unreadable camera geometry: " + string.Join(", ", unreadable.Select(f => f.name)));
            int added = 0, changed = 0;
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Repair waiting room camera collision");
            foreach (var filter in targets) {
                var surface = filter.GetComponent<CameraCollisionSurface>();
                bool mesh = filter.sharedMesh.name != "Cube";
                Bounds bounds = filter.sharedMesh.bounds;
                if (surface == null) { surface = Undo.AddComponent<CameraCollisionSurface>(filter.gameObject); added++; }
                else if (surface.enabled && surface.UseMeshGeometry == mesh && surface.LocalBounds == bounds) continue;
                else changed++;
                Undo.RecordObject(surface, "Fit camera collision to structure");
                surface.enabled = true; surface.UseMeshGeometry = mesh; surface.LocalBounds = bounds;
                EditorUtility.SetDirty(surface);
                if (PrefabUtility.IsPartOfPrefabInstance(surface)) PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
            }
            Undo.CollapseUndoOperations(undo);
            if (added + changed != 0) EditorSceneManager.MarkSceneDirty(scene);
            return $"{scene.name}: reviewed={targets.Length}, added={added}, updated={changed}, mesh={targets.Count(f => f.sharedMesh.name != "Cube")}; {ApplyPhysicalCeilings(scene)}";
        }

        // Physical ceilings are separate from camera coverage: never fill hollow passages with a bounds box.
        public static string ApplyPhysicalCeilings(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !WaitingRoomScenes.IsWaitingRoom(scene))
                throw new InvalidOperationException("Open a waiting room outside Play mode.");
            var names = new HashSet<string> { "Ceiling", "CeilingPanel", "RoofSlab", "RoofSheet", "PassageRoof",
                "PassageFloorCeiling", "BranchFloorCeiling", "DomeCap", "CanvasRoof", "GalleryRoof", "StageApproachRoof",
                "LiftShaftCap", "CarRoof", "Enclosed ceiling", "Vault", "SealedActualVaultTransition" };
            int count = 0;
            foreach (var filter in Collect(scene).Where(f => names.Contains(f.name)))
            {
                if (filter.GetComponents<Collider>().Any(c => c.enabled && !c.isTrigger)) continue;
                if (filter.sharedMesh.name == "Cube")
                {
                    var collider = Undo.AddComponent<BoxCollider>(filter.gameObject);
                    collider.center = filter.sharedMesh.bounds.center;
                    collider.size = filter.sharedMesh.bounds.size;
                }
                else
                {
                    string folder = "Assets/Resources/Models/WaitingRoomCollision";
                    if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources/Models", "WaitingRoomCollision");
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh, out string guid, out long localId);
                    string path = folder + "/roof-" + guid + "-" + localId + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                    {
                        mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                        mesh.name = filter.sharedMesh.name + " physical two sided";
                        var front = mesh.triangles;
                        var both = new int[front.Length * 2];
                        Array.Copy(front, both, front.Length);
                        for (int i = 0; i < front.Length; i += 3) {
                            both[front.Length + i] = front[i + 2]; both[front.Length + i + 1] = front[i + 1]; both[front.Length + i + 2] = front[i];
                        }
                        mesh.subMeshCount = 1; mesh.triangles = both;
                        AssetDatabase.CreateAsset(mesh, path);
                    }
                    var collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                    collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;
                    collider.sharedMesh = mesh;
                }
                if (PrefabUtility.IsPartOfPrefabInstance(filter)) PrefabUtility.RecordPrefabInstancePropertyModifications(filter.gameObject);
                count++;
            }
            if (count != 0) EditorSceneManager.MarkSceneDirty(scene);
            return $"{scene.name}: physical ceilings added={count}";
        }

        [MenuItem("Tools/CurtainCall/Camera/현재 대기실 벽·천장 충돌 보완")]
        static void ApplyCurrent() => Debug.Log(Apply(SceneManager.GetActiveScene()));

        [MenuItem("Tools/CurtainCall/Camera/현재 대기실 벽·천장 충돌 보완", true)]
        static bool CanApply() => !EditorApplication.isPlayingOrWillChangePlaymode
            && WaitingRoomScenes.IsWaitingRoom(SceneManager.GetActiveScene());
    }
}
