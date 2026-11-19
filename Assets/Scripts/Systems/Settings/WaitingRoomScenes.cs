using UnityEngine;
using UnityEngine.SceneManagement;
namespace CurtainCall.Settings
{
    public static class WaitingRoomScenes
    {
        public const string Name = "WaitingRoom";
        public const string Path = "Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity";
        public static bool IsWaitingRoom(string name) => name == Name;
        public static bool IsWaitingRoom(Scene scene) => scene.IsValid() && IsWaitingRoom(scene.name);
        public static Transform FindPracticeRoom()
        {
            var active = SceneManager.GetActiveScene();
            var roomScene = IsWaitingRoom(active) ? active : SceneManager.GetSceneByName(GameSettings.Base.SpaceTransition.WaitingRoomScene);
            return FindPracticeRoom(roomScene);
        }
        public static Transform FindPracticeRoom(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "WaitingRoom_LayoutA" || !root.activeInHierarchy) continue;
                // PursuitSystem also contains a room marker named S09_PracticeRoom.
                var room = root.transform.Find("S09_PracticeRoom");
                if (room != null && room.gameObject.activeInHierarchy) return room;
            }
            return null;
        }
        public static bool IsLoaded => SceneManager.GetSceneByName(Name).isLoaded;
        public static bool PursuitEnabled => GameSettings.WaitingRoom.Pursuit.Enabled;
    }
}
