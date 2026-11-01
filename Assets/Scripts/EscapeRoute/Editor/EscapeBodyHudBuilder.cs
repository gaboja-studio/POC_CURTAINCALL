#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using CurtainCall.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.EscapeRoute.Editor
{
    public static class EscapeBodyHudBuilder
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/EscapeRoute/EscapeBodyStatusHud.prefab";
        [MenuItem("Tools/CurtainCall/Escape/Install First Person Body HUD")]
        public static void Install()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "escape_route" || scene.isDirty)
                throw new InvalidOperationException("Saved A scene in Edit Mode required.");
            var frame = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeCampaignContent>(true)).Single().transform;
            if (frame.GetComponentInChildren<EscapeBodyStatusHud>(true) != null) throw new InvalidOperationException("Body HUD already installed.");
            EnsureViewLayer();
            var toggle = frame.GetComponent<EscapePerspectiveToggle>();
            if (toggle == null) toggle = Undo.AddComponent<EscapePerspectiveToggle>(frame.gameObject);
            if (frame.GetComponent<EscapeFirstPersonArms>() == null) Undo.AddComponent<EscapeFirstPersonArms>(frame.gameObject);
            var settings = Resources.Load<CameraViewSettings>("Settings/Camera/CameraViewSettings");
            var toggleData = new SerializedObject(toggle);
            toggleData.FindProperty("firstPersonView").objectReferenceValue = settings.FirstPerson;
            toggleData.ApplyModifiedProperties();
            var hud = Build();
            EnsureFolder("Assets/Resources/Prefabs/UIs/EscapeRoute");
            if (File.Exists(PrefabPath)) throw new InvalidOperationException("Existing HUD prefab must be reviewed first.");
            PrefabUtility.SaveAsPrefabAssetAndConnect(hud, PrefabPath, InteractionMode.AutomatedAction);
            hud.transform.SetParent(frame, true);
            hud.transform.rotation = Quaternion.identity;
            hud.GetComponent<CanvasGroup>().alpha = 0;
            Undo.RegisterCreatedObjectUndo(hud, "Escape first-person body HUD");
            Undo.FlushUndoRecordObjects(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("A scene save failed.");
            Selection.activeGameObject = hud;
        }
        static GameObject Build()
        {
            var root = new GameObject("EscapeBodyStatusHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(EscapeBodyStatusHud));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder = 35;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var group = root.GetComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = group.interactable = false;
            var figure=Rect(root.transform,"HumanSilhouette",new Vector2(40,28),new Vector2(84,156)).gameObject.AddComponent<HumanBodyDiagramGraphic>();
            figure.raycastTarget=false;
            var so=new SerializedObject(root.GetComponent<EscapeBodyStatusHud>());
            so.FindProperty("visibility").objectReferenceValue=group; so.FindProperty("diagram").objectReferenceValue=figure;
            so.FindProperty("aimDot").objectReferenceValue=EnsureAimDot(root.transform);
            so.ApplyModifiedPropertiesWithoutUndo();return root;
        }
        public static void SimplifyExisting()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode required.");
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var diagram=root.GetComponentInChildren<HumanBodyDiagramGraphic>(true);
                if(diagram==null)throw new InvalidOperationException("Missing human diagram.");
                var figure=(RectTransform)diagram.transform;
                figure.SetParent(root.transform,false);figure.anchorMin=figure.anchorMax=figure.pivot=Vector2.zero;
                figure.anchoredPosition=new Vector2(40,28);figure.sizeDelta=new Vector2(84,156);
                var dot=EnsureAimDot(root.transform);
                foreach(var child in root.transform.Cast<Transform>().ToArray())
                    if(child!=figure && child!=dot.transform)UnityEngine.Object.DestroyImmediate(child.gameObject);
                var so=new SerializedObject(root.GetComponent<EscapeBodyStatusHud>());
                so.FindProperty("diagram").objectReferenceValue=diagram;
                so.FindProperty("visibility").objectReferenceValue=root.GetComponent<CanvasGroup>();
                so.FindProperty("aimDot").objectReferenceValue=dot;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
            Undo.FlushUndoRecordObjects();EditorSceneManager.SaveOpenScenes();
        }
        static CanvasGroup EnsureAimDot(Transform parent)
        {
            var existing=parent.Find("AimDot");
            var go=existing!=null?existing.gameObject:new GameObject("AimDot",typeof(RectTransform),typeof(CanvasGroup),typeof(Image),typeof(Outline));
            var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(5,5);rect.localScale=Vector3.one;
            var image=go.GetComponent<Image>();
            image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if(image.sprite==null)throw new InvalidOperationException("Missing round aiming-dot sprite.");
            image.color=new Color(1,1,1,.9f);image.raycastTarget=false;
            var outline=go.GetComponent<Outline>();outline.effectColor=new Color(.05f,.05f,.05f,.9f);outline.effectDistance=new Vector2(1,-1);
            var group=go.GetComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=group.interactable=false;
            return group;
        }
        static RectTransform Rect(Transform parent,string name,Vector2 at,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.zero;rect.anchoredPosition=at;rect.sizeDelta=size;return rect;
        }
        static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        public static void EnsureViewLayer()
        {
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer=tags.FindProperty("layers").GetArrayElementAtIndex(31);
            if(!string.IsNullOrEmpty(layer.stringValue)&&layer.stringValue!="FirstPersonArms")
                throw new InvalidOperationException("Layer 31 is already reserved by another feature.");
            layer.stringValue="FirstPersonArms";tags.ApplyModifiedProperties();
        }
    }
}
#endif
