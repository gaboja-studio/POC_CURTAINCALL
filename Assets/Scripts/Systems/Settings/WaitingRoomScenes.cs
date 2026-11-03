using UnityEngine.SceneManagement;
namespace CurtainCall.Settings
{
    public enum WaitingRoomSceneVariant { Base, Pursuit, Integrated }
    public static class WaitingRoomScenes
    {
        public const string BaseName = "WaitingRoomBase";
        public const string PursuitName = "WaitingRoomPursuit";
        public const string IntegratedName = "WaitingRoom";
        public const string BaseGuid = "0ce9037bd7c44c8b9d57785c26620388";
        public const string PursuitGuid = "9d8832c4cb77f9343a74c3dc35c1d3a3";
        public const string IntegratedGuid = "34921be9f5b47a844b7245a681db88e7";
        public static string BasePath => Path(WaitingRoomSceneVariant.Base);
        public static string PursuitPath => Path(WaitingRoomSceneVariant.Pursuit);
        public static string IntegratedPath => Path(WaitingRoomSceneVariant.Integrated);
        public static string Name(WaitingRoomSceneVariant variant) => variant switch {
            WaitingRoomSceneVariant.Integrated => IntegratedName,
            WaitingRoomSceneVariant.Pursuit => PursuitName,
            _ => BaseName
        };
        public static string Path(WaitingRoomSceneVariant variant)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GUIDToAssetPath(variant switch {
                WaitingRoomSceneVariant.Integrated => IntegratedGuid,
                WaitingRoomSceneVariant.Pursuit => PursuitGuid,
                _ => BaseGuid
            });
#else
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++) {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == Name(variant)) return path;
            }
            return string.Empty;
#endif
        }
        public static bool IsWaitingRoom(string name) => name == BaseName || HasPursuit(name);
        public static bool HasPursuit(string name) => name == PursuitName || name == IntegratedName;
        public static bool IsWaitingRoom(Scene scene) => scene.IsValid() && IsWaitingRoom(scene.name);
        public static bool IsLoaded => SceneManager.GetSceneByName(BaseName).isLoaded || SceneManager.GetSceneByName(PursuitName).isLoaded
            || SceneManager.GetSceneByName(IntegratedName).isLoaded;
        public static bool PursuitEnabled => HasPursuit(GameSettings.Base.SpaceTransition.WaitingRoomScene) && GameSettings.WaitingRoom.Pursuit.Enabled;
        public static string Resolve(string legacyName, WaitingRoomSceneVariant variant) => IsWaitingRoom(legacyName) ? Name(variant) : legacyName;
    }
}
