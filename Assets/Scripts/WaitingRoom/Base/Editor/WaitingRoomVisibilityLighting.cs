using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>기존 전구·강조광은 유지하고 그레이박스에만 공간 확인용 반사광을 보충한다.</summary>
    public static class WaitingRoomVisibilityLighting
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        const string GroupName = "VisibilityLighting";

        [MenuItem("Tools/CurtainCall/대기실/공간 확인용 보조 조명 적용")]
        public static void ApplyCurrent() => Apply(SceneManager.GetActiveScene());

        public static GameObject Apply(Scene scene)
        {
            if (EditorApplication.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("WaitingRoomBase 편집 씬에서만 실행합니다.");
            var root = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoomGreybox").transform;
            var mood = root.Find("Mood");
            if (mood == null) throw new InvalidOperationException("먼저 대기실 분위기를 적용해야 합니다.");
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("대기실 공간 확인용 보조 조명");
            var old = mood.Find(GroupName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "대기실 보조광 생성");
            group.transform.SetParent(mood, false);

            // 배경은 낮은 채도의 차가운 빛으로 밝히고, 기존 따뜻한 전구와 기능 지점의 대비를 남긴다.
            var cool = new Color(.64f, .77f, .85f);
            var neutral = new Color(.75f, .79f, .82f);
            var warm = new Color(.94f, .85f, .69f);
            Fill(group.transform, "Lobby_West", new Vector3(-4, 3.2f, 0), cool, 10, 14);
            Fill(group.transform, "Lobby_East", new Vector3(4, 3.2f, 0), cool, 10, 14);
            Fill(group.transform, "Dormitory_Living", new Vector3(-5.5f, 3.2f, 12.5f), neutral, 9, 11);
            Fill(group.transform, "Dormitory_Beds", new Vector3(-3.8f, 3.2f, 18.5f), cool, 9, 11);
            Fill(group.transform, "Restroom", new Vector3(6.6f, 3.2f, 15), new Color(.74f, .84f, .91f), 8, 11);
            Fill(group.transform, "PropRoom_Storage", new Vector3(-5, 3.2f, -14.5f), neutral, 9, 11);
            Fill(group.transform, "PropRoom_Dressing", new Vector3(5, 3.2f, -16), neutral, 9, 11);
            Fill(group.transform, "JunkStorage_South", new Vector3(-16, 3.3f, -4.2f), cool, 6.5f, 11);
            Fill(group.transform, "JunkStorage_Archive", new Vector3(-16, 3.3f, 4.5f), cool, 6.5f, 11);
            Fill(group.transform, "ExpertRoom", new Vector3(14.3f, 3.2f, 8.5f), warm, 7, 9);
            Fill(group.transform, "Corridor_Near", new Vector3(16, 3.4f, 0), cool, 6.5f, 13);
            Fill(group.transform, "Corridor_Middle", new Vector3(29, 3.4f, 0), cool, 6.5f, 13);
            Fill(group.transform, "Corridor_Far", new Vector3(42, 3.4f, 0), cool, 6.5f, 13);
            Fill(group.transform, "StageEntrance", new Vector3(49, 3.6f, 0), warm, 7, 9);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
            Debug.Log("[WaitingRoomVisibilityLighting] 그레이박스 보조광 14개 추가, 기존 조명·프로토타입 유지");
            return group;
        }

        static void Fill(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }
    }
}
