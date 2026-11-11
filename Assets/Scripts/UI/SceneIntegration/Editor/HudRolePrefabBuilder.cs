using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static partial class HudRolePrefabBuilder
    {
        const string Root = "Assets/Resources/Prefabs/UIs/Roles/";
        static GameObject Node(string name, Transform parent = null)
        {
            var obj=new GameObject(name,typeof(RectTransform));
            if(parent) obj.transform.SetParent(parent,false);
            return obj;
        }
        static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,float size,Color color)
        {
            var t=Node(name,parent).AddComponent<TextMeshProUGUI>();
            t.font=font; t.fontSize=size; t.color=color; t.richText=false;
            t.alignment=TextAlignmentOptions.TopLeft; t.raycastTarget=false; t.text="";
            var r=t.rectTransform; r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            return t;
        }
        [MenuItem("Curtain Call/UI/Migrate Tightrope control guide")]
        public static void MigrateTightrope()
        {
            Build();
            const string path="Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity";
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened=!scene.isLoaded;
            if(opened) scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                foreach(var root in scene.GetRootGameObjects())
                foreach(var owner in root.GetComponentsInChildren<TightropeControlsPanel>(true))
                {
                    if(owner.name!="ContextualControlsBanner") continue;
                    var replacement=new GameObject("TightropeControlGuides",typeof(TightropeControlsPanel));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(replacement,scene);
                    replacement.transform.SetParent(owner.transform.parent,false);
                    var copy=new SerializedObject(replacement.GetComponent<TightropeControlsPanel>());
                    copy.FindProperty("sawDistance").floatValue=new SerializedObject(owner).FindProperty("sawDistance").floatValue;
                    copy.ApplyModifiedPropertiesWithoutUndo();
                    Object.DestroyImmediate(owner.gameObject);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new IOException(path);
                }
            }
            finally { if(opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true); }
        }
        [MenuItem("Curtain Call/UI/Create missing role HUD prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/BasicPanels/Pretendard-Medium SDF.asset");
            if (!font) throw new InvalidOperationException("HUD Korean font not found");
            var roles=(HudRole[])Enum.GetValues(typeof(HudRole));
            var assets=new HudRoleCard[roles.Length];
            foreach(var role in roles)
            {
                var path=Root+role+".prefab";
                if (!File.Exists(path))
                {
                    var root=Node(role.ToString());
                    try
                    {
                        var card=root.AddComponent<HudRoleCard>(); card.role=role;
                        card.motion=root.AddComponent<HudRoleMotion>();
                        var surface=Node("Surface",root.transform);
                        var r=(RectTransform)surface.transform; r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one;
                        r.offsetMin=r.offsetMax=Vector2.zero;
                        card.motion.surface=r; card.motion.group=surface.AddComponent<CanvasGroup>();
                        var panel=surface.AddComponent<Image>(); panel.color=new Color32(23,43,44,244); panel.raycastTarget=false;
                        var outline=surface.AddComponent<Outline>(); outline.effectColor=new Color32(159,144,111,255);
                        outline.effectDistance=new Vector2(1,-1);
                        card.title=Text("Title",r,font,25,new Color32(246,201,133,255));
                        card.body=Text("Content",r,font,21,Color.white);
                        card.condition=Text("Condition",r,font,17,new Color32(214,212,201,255));
                        card.maxWidth=role==HudRole.Outcome || role==HudRole.SystemEvent ? 600 : 450;
                        var m=card.motion;
                        m.enter=role==HudRole.Guide ? HudEffect.Slide : role==HudRole.Outcome ? HudEffect.Zoom :
                            role==HudRole.Feedback ? HudEffect.Pop : role==HudRole.Interaction ? HudEffect.Fade : HudEffect.Rise;
                        m.exit=role==HudRole.Guide ? HudEffect.Slide : HudEffect.Fade;
                        m.displaySeconds=role==HudRole.Guide ? 5 : role==HudRole.SystemEvent || role==HudRole.Outcome ? 4 : role==HudRole.Feedback ? 3 : 0;
                        if(role==HudRole.Vote)
                        {
                            card.buttons=(RectTransform)Node("Choices",r).transform;
                            card.buttons.anchorMin=card.buttons.anchorMax=card.buttons.pivot=new Vector2(0,1);
                            var row=card.buttons.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing=8; row.childForceExpandWidth=true;
                            var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Roles/Retained/CommonButtons/Button_General.prefab");
                            card.choices=new Button[2];
                            for(int i=0;i<2;i++) card.choices[i]=((GameObject)PrefabUtility.InstantiatePrefab(template,card.buttons)).GetComponent<Button>();
                        }
                        PrefabUtility.SaveAsPrefabAsset(root,path);
                    } finally { Object.DestroyImmediate(root); }
                }
                UpgradeLayout(path,role);
                ApplyRoleArt(path,role);
                FinishRoleArt(path,role);
                UpgradeBadgeAndFont(path,role);
                RefinePlate(path,role);
                assets[(int)role]=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<HudRoleCard>();
            }
            var managerPath=Root+"HudUIManager.prefab";
            if (!File.Exists(managerPath))
            {
                var root=Node("HudUIManager");
                try
                {
                    var canvas=root.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=340;
                    var scale=root.AddComponent<CanvasScaler>(); scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scale.referenceResolution=new Vector2(1920,1080); scale.matchWidthOrHeight=.5f;
                    root.AddComponent<GraphicRaycaster>(); root.AddComponent<HudUIManager>().prefabs=assets;
                    PrefabUtility.SaveAsPrefabAsset(root,managerPath);
                } finally { Object.DestroyImmediate(root); }
            }
            // Cards may outlive an additive UI scene. Let their manager own the font copies too.
            var saved=AssetDatabase.LoadAssetAtPath<GameObject>(managerPath);
            if (!saved.GetComponent<UiRuntimeFontFallback>())
            {
                var contents=PrefabUtility.LoadPrefabContents(managerPath);
                try
                {
                    var fonts=contents.AddComponent<UiRuntimeFontFallback>();
                    fonts.Configure(Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF"));
                    const string dir="Assets/_ThirdParty/curtaincall_tooltips_v1/Source/Fonts/";
                    fonts.ConfigureSources(AssetDatabase.LoadAssetAtPath<Font>(dir+"Pretendard-Medium.ttf"),
                        AssetDatabase.LoadAssetAtPath<Font>(dir+"Pretendard-SemiBold.ttf"));
                    PrefabUtility.SaveAsPrefabAsset(contents,managerPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            const string coursePath=Root+"CourseStatus.prefab";
            if(!File.Exists(coursePath))
            {
                var status=(GameObject)PrefabUtility.InstantiatePrefab(assets[(int)HudRole.Status].gameObject);
                try
                {
                    var c=status.GetComponent<HudRoleCard>();
                    Place(c.Rect,new Vector2(.5f,0),new Vector2(0,48));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c.Rect);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                    PrefabUtility.SaveAsPrefabAsset(status,coursePath);
                }
                finally { Object.DestroyImmediate(status); }
            }
            if(!saved.GetComponent<HudUIManager>().courseStatus)
            {
                var contents=PrefabUtility.LoadPrefabContents(managerPath);
                try
                {
                    contents.GetComponent<HudUIManager>().courseStatus=AssetDatabase.LoadAssetAtPath<GameObject>(coursePath).GetComponent<HudRoleCard>();
                    PrefabUtility.SaveAsPrefabAsset(contents,managerPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            if(saved.GetComponent<HudUIManager>().prefabs.Length < assets.Length)
            {
                var contents=PrefabUtility.LoadPrefabContents(managerPath);
                try {
                    var m=contents.GetComponent<HudUIManager>(); var refs=m.prefabs;
                    int count=refs.Length; Array.Resize(ref refs,assets.Length);
                    for(int i=count;i<refs.Length;i++)refs[i]=assets[i];
                    m.prefabs=refs; PrefabUtility.SaveAsPrefabAsset(contents,managerPath);
                } finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            ConnectRules(managerPath);
            AssetDatabase.SaveAssets();
            var manager=Resources.Load<HudUIManager>("Prefabs/UIs/Roles/HudUIManager");
            foreach(var role in roles)
            {
                var c=manager.prefabs[(int)role];
                if(c.role!=role || !c.title.font || !c.body || !c.condition || !c.motion.surface || !c.motion.group ||
                    role==HudRole.Vote && (c.choices.Length!=2 || !c.choices[1]))
                    throw new InvalidOperationException("Invalid role prefab: "+role);
            }
            Debug.Log("HUD_ROLE_PREFABS_VERIFIED: 9 roles and manager");
        }
        static void Place(RectTransform r,Vector2 anchor,Vector2 offset)
        {
            r.anchorMin=r.anchorMax=r.pivot=anchor;
            r.anchoredPosition=offset;
        }

        // One-time migration. Running the menu again must preserve authored text and layout.
        static void UpgradeLayout(string path,HudRole role)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<HudRoleCard>();
            if(source.layoutVersion>=1) return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var c=root.GetComponent<HudRoleCard>();
                (Vector2 anchor,Vector2 offset) pos=role switch {
                    HudRole.Guide=>(new Vector2(0,1),new Vector2(32,-304)), // 20 below the waiting-room timer footer.
                    HudRole.Objective or HudRole.Vote=>(Vector2.one,new Vector2(-32,-260)),
                    HudRole.Feedback=>(Vector2.one,new Vector2(-32,-95)),
                    HudRole.Status=>(new Vector2(1,0),new Vector2(-32,30)),
                    HudRole.SystemEvent=>(new Vector2(.5f,1),new Vector2(0,-24)),
                    HudRole.Interaction=>(new Vector2(.5f,.5f),new Vector2(0,24)),
                    _=>(new Vector2(.5f,.5f),Vector2.zero)
                };
                Place(c.Rect,pos.anchor,pos.offset);
                c.avoidPreviousLane=role==HudRole.Vote || role==HudRole.Objective;
                c.rows=c.motion.surface.gameObject.AddComponent<VerticalLayoutGroup>();
                c.rows.padding=new RectOffset(16,16,12,12); c.rows.spacing=5;
                c.rows.childAlignment=TextAnchor.UpperLeft;
                c.rows.childControlWidth=c.rows.childControlHeight=c.rows.childForceExpandWidth=true;
                c.rows.childForceExpandHeight=false;
                c.title.text="{title}"; c.body.text="{content}"; c.condition.text="{condition}";
                c.condition.transform.SetAsLastSibling();
                if(c.buttons)
                {
                    var size=c.buttons.gameObject.AddComponent<LayoutElement>();
                    size.preferredHeight=44; size.preferredWidth=c.maxWidth-c.rows.padding.horizontal;
                    foreach(var button in c.choices)
                    {
                        var label=button.GetComponentInChildren<TMP_Text>(true);
                        label.text="{choice}";
                        label.enableAutoSizing=true;
                        label.fontSizeMin=10;
                        label.fontSizeMax=28;
                        label.textWrappingMode=TextWrappingModes.Normal;
                        label.overflowMode=TextOverflowModes.Overflow;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                    }
                }
                c.Rect.sizeDelta=new Vector2(c.maxWidth,role==HudRole.Vote ? 180 : 110);
                c.layoutVersion=1;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
