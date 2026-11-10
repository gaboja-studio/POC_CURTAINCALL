using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static partial class HudRolePrefabBuilder
    {
        const string OldRoot="Assets/Resources/Prefabs/UIs";
        const string Kept=OldRoot+"/Roles/Retained";
        [MenuItem("Curtain Call/UI/Migrate retained HUD and control badges")]
        public static void MigrateRetainedHud()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("편집 모드에서 실행하세요.");
            var stage=UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if(stage!=null && stage.scene.isDirty)throw new InvalidOperationException("열린 프리팹을 먼저 저장하세요: "+stage.assetPath);
            void Folder(string path){if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
            void Move(string from,string to)
            {
                if(!File.Exists(from) && !AssetDatabase.IsValidFolder(from))return;
                string guid=AssetDatabase.AssetPathToGUID(from);
                string error=AssetDatabase.MoveAsset(from,to);
                if(!string.IsNullOrEmpty(error) || AssetDatabase.AssetPathToGUID(to)!=guid)throw new IOException(from+": "+error);
            }
            Folder(Kept);
            foreach(var group in new[]{"BackstageHud","Captions","CheerGauge","Feedback","Guides","Interaction","Notifications","PerformanceHud","Player","RestTimerV28","ResultsUi","Spectator","Tooltips","WorldLabels","BasicPanels","ScheduleUi","ShopUi","TransitionCurtain","TitleLogo","SettingsUi","SettingsUiV2"})
                Move(OldRoot+"/"+group,Kept+"/"+group);
            // The third-party BasicPanelBuilder accepts only these four historical button paths.
            // The game and its scenes use the relocated originals. Only the vendor builder uses these aliases.
            Folder(Kept+"/CommonButtons");
            foreach(var name in new[]{"Button_General","Button_Confirm","Button_Cancel","Button_Settings"})
            {
                string tail="/CommonButtons/"+name+".prefab",original=Kept+tail,alias=OldRoot+tail;
                if(!File.Exists(original))Move(alias,original);
                if(File.Exists(alias))continue;
                var button=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(original));
                try{PrefabUtility.SaveAsPrefabAsset(button,alias,out bool ok);if(!ok)throw new IOException(alias);}
                finally{UnityEngine.Object.DestroyImmediate(button);}
            }
            Folder(Kept+"/DayClock");
            foreach(var file in new[]{"W1_DayClock_v2_7.prefab","W1_DayClock_v2_7_HourOutline.mat","W1_DayClock_v2_2_Schedule.asset"})
                Move(OldRoot+"/DayClock/"+file,Kept+"/DayClock/"+file);
            Build();
            var path=Kept+"/Interaction/InteractionPrompt.prefab";
            var prompt=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var sprites=Resources.Load<GameObject>("Prefabs/UIs/Roles/Guide").GetComponentsInChildren<Image>(true);
                bool changed=false;
                foreach(var im in prompt.GetComponentsInChildren<Image>(true).Where(i=>i.name=="40_KeyBody" || i.name=="41_KeyFrame"))
                {
                    var src=sprites.Single(i=>i.name==im.name);
                    if(im.sprite==src.sprite)continue;
                    im.sprite=src.sprite;im.type=src.type;im.pixelsPerUnitMultiplier=src.pixelsPerUnitMultiplier;changed=true;
                }
                if(changed)PrefabUtility.SaveAsPrefabAsset(prompt,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(prompt);}
            Debug.Log("Retained HUD: GUID migration, badge and font upgrade completed.");
        }

        static void UpgradeBadgeAndFont(string path,HudRole role)
        {
            if(role!=HudRole.Guide && role!=HudRole.Interaction && role!=HudRole.SystemEvent)return;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<HudRoleCard>().layoutVersion>=4)return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var c=root.GetComponent<HudRoleCard>();
                if(role==HudRole.SystemEvent)
                {
                    var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/ShopUi/Pretendard-Medium SDF.asset");
                    if(!font || !font.HasCharacters("시스템 알림"))throw new InvalidOperationException("한국어 미리보기 글리프 누락");
                    foreach(var t in new[]{c.title,c.body,root.GetComponent<HudEventAppearance>().line})
                    { t.font=font;t.fontSharedMaterial=font.material; }
                }
                else
                {
                    var art=c.motion.surface.Find("Artwork");
                    var badge=root.AddComponent<HudControlBadge>();badge.rows=c.rows;
                    var fill=art.Find("40_KeyBody").GetComponent<Image>();var rim=art.Find("41_KeyFrame").GetComponent<Image>();
                    if(role==HudRole.Interaction)
                    {
                        // Reuse the guide's actual authored keycap geometry. A separate template keeps {title} editable.
                        var guide=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Guide.prefab").transform.Find("Surface/Artwork");
                        foreach(var im in new[]{fill,rim})
                        {
                            var src=guide.Find(im.name).GetComponent<Image>();im.sprite=src.sprite;im.type=src.type;im.pixelsPerUnitMultiplier=src.pixelsPerUnitMultiplier;
                            var a=src.rectTransform;var b=im.rectTransform;
                            b.anchorMin=a.anchorMin;b.anchorMax=a.anchorMax;b.pivot=a.pivot;b.anchoredPosition=a.anchoredPosition;b.sizeDelta=a.sizeDelta;
                        }
                        var label=UnityEngine.Object.Instantiate(guide.Find("Guide badge").gameObject,art,false);
                        label.name="Guide badge";label.GetComponent<TMP_Text>().text="F";
                        var templates=Node("Key template",c.motion.surface);templates.SetActive(false);
                        c.title.transform.SetParent(templates.transform,false);c.rows.padding.left=67;
                        UnityEngine.Object.DestroyImmediate(root.GetComponent<HudKeycapArt>());
                    }
                    badge.label=art.Find("Guide badge").GetComponent<TMP_Text>();
                    badge.label.richText=false;badge.label.textWrappingMode=TextWrappingModes.NoWrap;
                    badge.label.overflowMode=TextOverflowModes.Ellipsis;
                    badge.plate=fill.rectTransform;badge.frame=rim.rectTransform;
                }
                c.layoutVersion=4;PrefabUtility.SaveAsPrefabAsset(root,path,out bool ok);if(!ok)throw new IOException(path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
