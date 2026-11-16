using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public static class PursuitStageCorridorAuthoring
    {
        [MenuItem("Tools/Curtain Call/Pursuit/Register stage corridor")]
        public static void Register()
        {
            var director = Object.FindFirstObjectByType<PursuitDirector>();
            var zones = Object.FindFirstObjectByType<ExplorationTimerZones>();
            var corridor = GameObject.Find("WaitingRoom_LayoutA/S06_StageCorridor");
            if (director == null || zones == null || corridor == null) throw new System.InvalidOperationException("Open the integrated WaitingRoom scene.");
            if (director.Rooms.Any(r => r != null && r.name == "S06_StageCorridor")) return;
            var floor = corridor.transform.Find("StageProcessionalRoute/GreyboxComposition/Floor").GetComponent<Collider>().bounds;
            // Include the approach from the lobby, ending at the actual performance curtain.
            float entry = corridor.transform.position.z;
            var min = floor.min; var max = floor.max;
            min.z = Mathf.Min(min.z, entry); min.y = floor.max.y - .5f; max.y = floor.max.y + 10;
            var bounds = new Bounds(); bounds.SetMinMax(min, max);
            var areaObject = new GameObject("Area_S06_StageCorridor"); Undo.RegisterCreatedObjectUndo(areaObject, "공연장 복도 구역 등록");
            areaObject.transform.SetParent(zones.Zones.First().transform.parent, false);
            areaObject.transform.position = bounds.center;
            var area = areaObject.AddComponent<BoxCollider>(); area.isTrigger = true; area.size = bounds.size;
            var roomObject = new GameObject("S06_StageCorridor"); Undo.RegisterCreatedObjectUndo(roomObject, "공연장 복도 추격 설정");
            roomObject.transform.SetParent(director.transform, false);
            var room = roomObject.AddComponent<PursuitRoom>(); room.Area = area;
            room.Lights = corridor.GetComponentsInChildren<Light>(); room.UsePlacements = true; room.Count = 0;
            var anchor = new GameObject("FloorReference"); anchor.transform.SetParent(room.transform, false);
            anchor.transform.position = new Vector3(bounds.center.x, floor.max.y, bounds.center.z);
            room.SpawnPoints = new[] { anchor.transform }; room.PatrolPoints = System.Array.Empty<Transform>();
            Undo.RecordObject(director, "공연장 복도 추가"); director.Rooms = director.Rooms.Append(room).ToArray();
            // Previously an unregistered safe space. Keep that default, now explicitly editable.
            Undo.RecordObject(zones, "공연장 복도 안전지대"); zones.SetSafety(new[] { area }, true);
            EditorUtility.SetDirty(director); EditorUtility.SetDirty(zones); EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        }
    }
}
