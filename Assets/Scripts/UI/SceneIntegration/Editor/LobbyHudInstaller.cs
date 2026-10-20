#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.StartLobby;
using CurtainCall.UI.BackstageHudV1;
using CurtainCall.UI.GuidesV1;
using CurtainCall.UI.NotificationsV1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class LobbyHudInstaller
    {
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save first.");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Start_Lobby/Lobby.unity");
            var room=UnityEngine.Object.FindFirstObjectByType<LobbyRoom>();
            if(UnityEngine.Object.FindFirstObjectByType<LobbyHudBinding>()!=null) throw new InvalidOperationException("Already installed.");
            var root=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="UI") ?? new GameObject("UI");
            var owner=root.AddComponent<LobbyHudBinding>();owner.room=room;
            owner.board=UnityEngine.Object.FindFirstObjectByType<LobbyReadyBoard>();owner.lever=UnityEngine.Object.FindFirstObjectByType<LobbyCurtainLever>();
            owner.info=Instance("BackstageHud/LobbyHud",root.transform,30).GetComponentInChildren<LobbyRoomInfoView>(true);
            owner.guide=Instance("Guides/Guides",root.transform,31).GetComponentInChildren<GuideView>(true);
            owner.banner=Instance("Notifications/SystemBanner",root.transform,32).GetComponentInChildren<SystemBannerView>(true);
            owner.toast=Instance("Notifications/Toast",root.transform,33).GetComponentInChildren<ToastView>(true);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SettingsUi/Pretendard-SemiBold SDF.asset");
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            {text.font=font;text.fontSharedMaterial=font.material;PrefabUtility.RecordPrefabInstancePropertyModifications(text);}
            // Remove only superseded screen UI; the physical ready board and lever retain their live labels.
            var oldCanvas=room.roomCode.GetComponentInParent<Canvas>().gameObject;
            room.roomCode=null;room.players=null;room.copyCode=null;room.prompt=null;
            room.banner=null;room.bannerSub=null;room.bannerBack=null;
            UnityEngine.Object.DestroyImmediate(oldCanvas);
            var es=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if(es!=null)es.transform.SetParent(root.transform,false);
            var hub=UnityEngine.Object.FindFirstObjectByType<CurtainCall.SpaceLink.SpaceLinkHub>();
            if(hub!=null)
            {
                var serialized=new SerializedObject(hub);var list=serialized.FindProperty("hubUi");
                list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=root;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(room);
            foreach(var component in root.GetComponentsInChildren<Component>(true))
                if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        static GameObject Instance(string path,Transform parent,int order)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/"+path+".prefab");
            if(prefab==null)throw new InvalidOperationException(path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            var canvas=go.GetComponent<Canvas>();canvas.sortingOrder=order;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            return go;
        }
    }
}
#endif
