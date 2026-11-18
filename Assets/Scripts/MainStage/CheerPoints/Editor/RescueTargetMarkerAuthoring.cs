#if UNITY_EDITOR
using System;
using CurtainCall.Settings;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    public static class RescueTargetMarkerAuthoring
    {
        public const string Path="Assets/Resources/Prefabs/UIs/Roles/Retained/Interaction/RescueTargetMarker.prefab";
        const string World="Assets/_ThirdParty/curtaincall_world_ui_v1/Sprites/";
        const string Hud="Assets/_ThirdParty/curtaincall_performance_hud_v1/Assets/CurtainCallUI/PerformanceHUD/Sprites/";
        static readonly Color Gold=new Color32(232,194,123,255);
        static readonly Color Ink=new Color32(19,29,32,250);

        [MenuItem("CurtainCall/Performance/Build Rescue Target Marker")]
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var root=new GameObject("RescueTargetMarker",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            try
            {
                var cv=root.GetComponent<Canvas>();cv.renderMode=RenderMode.ScreenSpaceOverlay;cv.sortingOrder=220;
                var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
                var anchor=Rect("TargetAnchor",root.transform,Vector2.zero,new Vector2(160,88));
                var motion=Rect("Motion",anchor,Vector2.zero,new Vector2(160,88));
                var group=motion.gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
                Image("Shadow",motion,new Vector2(0,44),new Vector2(76,76),World+"CircleFill.png",new Color(0,0,0,.5f));
                var halo=Image("BreathingHalo",motion,new Vector2(0,48),new Vector2(78,78),World+"CircleFrame.png",Gold);
                Image("Medallion",motion,new Vector2(0,48),new Vector2(66,66),World+"CircleFill.png",Ink);
                Image("BrassRim",motion,new Vector2(0,48),new Vector2(66,66),World+"CircleFrame.png",Gold);
                Image("InnerRim",motion,new Vector2(0,48),new Vector2(56,56),World+"CircleFrame.png",new Color(.91f,.76f,.48f,.25f));
                Image("RescueGloves",motion,new Vector2(0,49),new Vector2(74,74),"Assets/Resources/ItemArt/Icons/rescue-gloves-slot.png",Color.white);
                // The shared world UI arrow is kept separate from the medallion for easy art replacement.
                var pointer=Image("TargetPointer",motion,new Vector2(0,5),new Vector2(12,10),World+"Arrowhead.png",Gold);
                pointer.rectTransform.localRotation=Quaternion.Euler(0,0,-90);
                var key=Rect("Keycap",motion,new Vector2(22,23),new Vector2(30,30));key.pivot=new Vector2(0,.5f);
                StretchImage("Body",key,Hud+"ui_keycap_basic_body_normal@2x.png",Ink);
                StretchImage("Frame",key,Hud+"ui_keycap_basic_frame_normal@2x.png",Gold);
                var label=Rect("Binding",key,Vector2.zero,Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
                Stretch(label.rectTransform);label.rectTransform.offsetMin=new Vector2(5,2);label.rectTransform.offsetMax=new Vector2(-5,-2);
                label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Interaction/Pretendard-Medium SDF.asset");
                label.fontSize=18;label.enableAutoSizing=true;label.fontSizeMin=9;label.fontSizeMax=18;label.fontStyle=FontStyles.Bold;
                label.alignment=TextAlignmentOptions.Center;label.color=new Color32(245,237,214,255);label.text="Q";
                label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
                label.richText=false;label.raycastTarget=false;
                root.AddComponent<RescueTargetMarkerView>().Configure(cv,anchor,motion,group,halo,key,label);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Path);
                if(!prefab)throw new InvalidOperationException("Could not save rescue marker.");
                var settings=AssetDatabase.LoadAssetAtPath<PerformanceEquipmentSettings>(PerformanceEquipmentAuthoring.SettingsPath);
                settings.rescueTargetVisual=prefab;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
                return Path;
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }

        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);
            rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        static Image Image(string name,Transform parent,Vector2 position,Vector2 size,string path,Color color)
        {
            var rect=Rect(name,parent,position,size);rect.pivot=new Vector2(.5f,.5f);
            var image=rect.gameObject.AddComponent<Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(!image.sprite)throw new InvalidOperationException("Missing sprite: "+path);
            image.color=color;image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        static void StretchImage(string name,RectTransform parent,string path,Color color)
        {
            var image=Image(name,parent,Vector2.zero,Vector2.zero,path,color);
            Stretch(image.rectTransform);image.type=UnityEngine.UI.Image.Type.Sliced;image.preserveAspect=false;
        }
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
    }
}
#endif
