using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static partial class HudRolePrefabBuilder
    {
        static readonly string[] sources={"Tooltips/Tooltips","Notifications/SystemBanner","Guides/Guides","Interaction/InteractionPrompt",
            "Notifications/Toast","BackstageHud/InventorySlotTemplate","BasicPanels/BasicPanels","ResultsUi/ResultsPanel","WorldLabels/WorldLabelWood"};
        static Sprite Glyph(string name)
        {
            var path=Root+"EventIcons/"+name+".png";
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            if(imp.textureType!=TextureImporterType.Sprite || imp.spriteImportMode!=SpriteImportMode.Single)
            {
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
                imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        // Restore the original role-specific details after importing the shared panel skins.
        static void FinishRoleArt(string path,HudRole role)
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<HudRoleCard>().layoutVersion>=3)return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var c=root.GetComponent<HudRoleCard>();var art=c.motion.surface.Find("Artwork");
                var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Roles/Retained/"+sources[(int)role]+".prefab");
                Image Copy(string name)
                {
                    var src=original.GetComponentsInChildren<Image>(true).First(i=>i.name==name);
                    var im=Node(name,art).AddComponent<Image>();im.sprite=src.sprite;im.color=src.color;im.raycastTarget=false;
                    im.type=src.type;im.pixelsPerUnitMultiplier=src.pixelsPerUnitMultiplier;return im;
                }
                void PlaceArt(RectTransform r,Vector2 min,Vector2 max,Vector2 pivot,Vector2 pos,Vector2 size)
                {r.anchorMin=min;r.anchorMax=max;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=pos;}
                var left=new Vector2(0,.5f);
                if(role==HudRole.Guide)
                {
                    c.rows.padding.left=67;
                    var bg=Copy("40_KeyBody");
                    var rim=Copy("41_KeyFrame");
                    var label=Text("Guide badge",art,c.body.font,15,new Color32(251,222,172,255));
                    label.text="TIP";label.alignment=TextAlignmentOptions.Center;label.fontStyle=FontStyles.Bold;
                    foreach(var r in new[]{bg.rectTransform,rim.rectTransform,label.rectTransform})
                        PlaceArt(r,left,left,left,new Vector2(13,0),new Vector2(42,34));
                }
                else if(role==HudRole.Objective)
                {
                    var edgeL=Copy("40_BottomEdgeLeft").rectTransform;
                    var edgeR=Copy("41_BottomEdgeRight").rectTransform;
                    PlaceArt(edgeL,Vector2.zero,Vector2.right,Vector2.zero,new Vector2(9,0),new Vector2(-50,4));
                    PlaceArt(edgeR,Vector2.right,Vector2.right,Vector2.right,new Vector2(-8,0),new Vector2(12,4));
                    var tail=Copy("50_TailBody").rectTransform;
                    PlaceArt(tail,Vector2.right,Vector2.right,Vector2.right,new Vector2(-20,-8),new Vector2(21,12));
                    tail.SetSiblingIndex(art.Find("51_TailFrame").GetSiblingIndex());
                }
                else if(role==HudRole.Interaction)
                {
                    var strip=Node("Key and action",c.motion.surface).AddComponent<HorizontalLayoutGroup>();
                    strip.spacing=12;strip.childAlignment=TextAnchor.MiddleLeft;
                    strip.childControlWidth=strip.childControlHeight=true;strip.childForceExpandWidth=strip.childForceExpandHeight=false;
                    c.title.transform.SetParent(strip.transform,false);c.body.transform.SetParent(strip.transform,false);
                    c.title.margin=new Vector4(8,0,8,0);
                    c.title.textWrappingMode=TextWrappingModes.NoWrap;
                }
                else if(role==HudRole.SystemEvent || role==HudRole.Feedback)
                {
                    var im=Copy("40_Accent");
                    PlaceArt(im.rectTransform,Vector2.zero,Vector2.one,Vector2.one*.5f,Vector2.zero,Vector2.zero);
                    im.transform.SetSiblingIndex(2);
                }
                c.layoutVersion=3;
                PrefabUtility.SaveAsPrefabAsset(root,path,out bool success);
                if(!success)throw new InvalidOperationException("Could not save HUD artwork: "+path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void ApplyRoleArt(string path,HudRole role)
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<HudRoleCard>().layoutVersion>=2)return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var c=root.GetComponent<HudRoleCard>();var surface=c.motion.surface;
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Roles/Retained/"+sources[(int)role]+".prefab").transform;
                if(role==HudRole.Vote)source=source.GetComponentsInChildren<Transform>(true).First(t=>t.name=="LeaveConfirmation");
                var images=source.GetComponentsInChildren<Image>(true);
                var backing=surface.GetComponent<Image>();backing.enabled=false;
                if(surface.TryGetComponent<Outline>(out var outline))Object.DestroyImmediate(outline);
                var art=(RectTransform)Node("Artwork",surface).transform;
                art.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
                art.SetAsFirstSibling();
                void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
                Stretch(art);
                Image Layer(string name)
                {
                    var original=images.FirstOrDefault(i=>i.name==name && i.sprite);
                    if(!original)return null;
                    var im=Node(name,art).AddComponent<Image>();im.sprite=original.sprite;im.type=original.type;
                    im.pixelsPerUnitMultiplier=original.pixelsPerUnitMultiplier;
                    im.color=original.color;im.raycastTarget=false;Stretch(im.rectTransform);return im;
                }
                void Badge(Image im,Vector2 anchor,Vector2 offset,Vector2 size)
                {
                    if(!im)return;var r=im.rectTransform;r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=offset;r.sizeDelta=size;im.preserveAspect=true;
                }
                Layer("00_Shadow");
                if(!Layer("10_Body"))throw new InvalidOperationException("Missing role artwork: "+source.name);
                Layer("20_Frame");Layer("30_Corners");
                switch(role)
                {
                    case HudRole.Objective:
                        c.rows.padding=new RectOffset(20,18,12,12);
                        Badge(Layer("51_TailFrame"),new Vector2(1,0),new Vector2(-20,-8),new Vector2(21,12));
                        break;
                    case HudRole.Guide:
                        c.rows.padding=new RectOffset(20,16,12,12);
                        break;
                    case HudRole.Interaction:
                        var cap=Layer("40_KeyBody");var rim=Layer("41_KeyFrame");
                        // Keycap tracks the text label even when the prefab spacing/font changes.
                        var badge=root.AddComponent<HudKeycapArt>();badge.key=c.title;badge.surface=surface;badge.fill=cap.rectTransform;badge.frame=rim.rectTransform;
                        c.title.margin=new Vector4(8,0,0,0);c.rows.padding=new RectOffset(14,16,10,10);
                        break;
                    case HudRole.Feedback:
                        Badge(Layer("50_Icon"),new Vector2(0,.5f),new Vector2(12,0),new Vector2(25,25));
                        c.rows.padding=new RectOffset(47,18,10,10);break;
                    case HudRole.Status:
                        c.rows.padding=new RectOffset(14,14,10,10);break;
                    case HudRole.Vote:
                        c.rows.padding=new RectOffset(24,24,16,16);break;
                    case HudRole.Outcome:
                        var ribbon=Layer("00_TitleRibbon");if(ribbon){var r=ribbon.rectTransform;r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(-12,39);r.anchoredPosition=new Vector2(0,-4);}
                        c.title.alignment=c.body.alignment=TextAlignmentOptions.Center;c.rows.padding=new RectOffset(26,26,9,16);c.rows.spacing=12;
                        break;
                    case HudRole.ZoneName:
                        Place(c.Rect,new Vector2(.5f,1),new Vector2(0,-16));c.motion.displaySeconds=0;
                        c.rows.padding=new RectOffset(20,20,4,4);c.body.fontSize=18;c.maxWidth=340;c.body.alignment=TextAlignmentOptions.Center;break;
                    case HudRole.SystemEvent:
                        c.avoidPreviousLane=true;c.maxWidth=760;c.title.fontSize=29;c.body.fontSize=24;
                        c.rows.padding=new RectOffset(20,20,10,10);
                        var templates=Node("Text templates",surface);templates.SetActive(false);
                        foreach(var t in new[]{c.title,c.body,c.condition})t.transform.SetParent(templates.transform,false);
                        var style=root.AddComponent<HudEventAppearance>();
                        var strip=Node("Icon and message",surface);style.strip=strip.AddComponent<HorizontalLayoutGroup>();
                        style.strip.spacing=10;style.strip.childAlignment=TextAnchor.UpperLeft;style.strip.childControlHeight=style.strip.childControlWidth=true;
                        style.strip.childForceExpandHeight=style.strip.childForceExpandWidth=false;
                        style.symbol=Node("Event icon",strip.transform).AddComponent<Image>();style.symbol.raycastTarget=false;style.symbol.preserveAspect=true;
                        var iconSize=style.symbol.gameObject.AddComponent<LayoutElement>();iconSize.minWidth=iconSize.preferredWidth=iconSize.minHeight=iconSize.preferredHeight=30;
                        style.line=Text("Message",strip.transform,c.body.font,24,Color.white);style.line.richText=true;
                        style.line.overflowMode=TextOverflowModes.Overflow;style.line.textWrappingMode=TextWrappingModes.Normal;
                        var size=style.line.gameObject.AddComponent<LayoutElement>();size.minWidth=0;size.preferredWidth=0;size.flexibleWidth=1;
                        style.notice=new HudEventAppearance.Theme{icon=Glyph("Notice"),color=new Color32(255,216,83,255)};
                        style.warning=new HudEventAppearance.Theme{icon=Glyph("Warning"),color=new Color32(255,96,89,255)};
                        style.reward=new HudEventAppearance.Theme{icon=Glyph("Reward"),color=new Color32(104,225,145,255)};
                        style.symbol.sprite=style.notice.icon;style.symbol.color=style.notice.color;style.line.text="시스템 알림";
                        break;
                }
                c.layoutVersion=2;PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
