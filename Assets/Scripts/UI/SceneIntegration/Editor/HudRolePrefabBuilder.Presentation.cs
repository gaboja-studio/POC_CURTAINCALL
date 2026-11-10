using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static partial class HudRolePrefabBuilder
    {
        [MenuItem("Curtain Call/UI/공통 HUD 표시 설정")]
        public static void SelectPresentation(){Selection.activeObject=RulesAsset();}
        static HudPresentationSettings RulesAsset()
        {
            const string folder="Assets/Resources/Settings/UI";
            const string path=folder+"/HudPresentationSettings.asset";
            var rules=AssetDatabase.LoadAssetAtPath<HudPresentationSettings>(path);
            if(rules)return rules;
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Resources/Settings","UI");
            rules=ScriptableObject.CreateInstance<HudPresentationSettings>();
            AssetDatabase.CreateAsset(rules,path);return rules;
        }
        static void ConnectRules(string path)
        {
            var rules=RulesAsset();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<HudUIManager>().presentation)return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try{root.GetComponent<HudUIManager>().presentation=rules;PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        static void RefinePlate(string path,HudRole role)
        {
            if(role!=HudRole.Interaction && role!=HudRole.ZoneName)return;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<HudRoleCard>().layoutVersion>=5)return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var c=root.GetComponent<HudRoleCard>();
                c.rows.childForceExpandHeight=false;
                if(role==HudRole.Interaction)
                {
                    c.minHeight=66;c.rows.childAlignment=TextAnchor.MiddleLeft;
                    c.rows.padding.top=c.rows.padding.bottom=15;
                    c.rows.padding.right=21;
                    c.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,c.minHeight);
                }
                else
                {
                    var art=c.motion.surface.Find("Artwork");
                    var guide=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Guide.prefab").transform.Find("Surface/Artwork");
                    foreach(string name in new[]{"00_Shadow","10_Body","20_Frame","30_Corners"})
                    {
                        var source=guide.Find(name).GetComponent<Image>();var im=art.Find(name).GetComponent<Image>();
                        im.sprite=source.sprite;im.type=source.type;im.color=source.color;im.pixelsPerUnitMultiplier=source.pixelsPerUnitMultiplier;
                    }
                    c.minWidth=122;c.minHeight=48;c.rows.childAlignment=TextAnchor.MiddleCenter;
                    c.rows.padding=new RectOffset(30,30,10,10);
                    c.body.fontSize=20;c.body.color=new Color32(246,224,181,255);c.body.alignment=TextAlignmentOptions.Center;
                    c.body.fontStyle=FontStyles.Bold;
                    foreach(bool left in new[]{true,false})
                    {
                        var im=Node(left?"Left brass pin":"Right brass pin",art).AddComponent<Image>();
                        im.raycastTarget=false;im.color=new Color32(202,159,92,255);
                        var r=im.rectTransform;r.anchorMin=r.anchorMax=new Vector2(left?0:1,.5f);r.pivot=Vector2.one*.5f;
                        r.sizeDelta=new Vector2(4,4);r.anchoredPosition=new Vector2(left?16:-16,0);r.localRotation=Quaternion.Euler(0,0,45);
                    }
                    c.Rect.sizeDelta=new Vector2(c.minWidth,c.minHeight);
                }
                c.layoutVersion=5;PrefabUtility.SaveAsPrefabAsset(root,path,out bool saved);if(!saved)throw new IOException(path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
