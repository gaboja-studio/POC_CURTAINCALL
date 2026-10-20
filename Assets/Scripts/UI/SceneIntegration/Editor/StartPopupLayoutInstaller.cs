#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.StartLobby;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class StartPopupLayoutInstaller
    {
        const string StartPath = "Assets/Scenes/Start_Lobby/Start.unity";
        static readonly string[] Spaces = { StartPath, "Assets/Scenes/Start_Lobby/Lobby.unity",
            "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity",
            "Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity",
            "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity" };

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(StartPath);
                var menu = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StartMenu>(true)).Single();
                Panel(menu.joinPage, "방 코드로 참가", 320);
                Panel(menu.lanPage, "LAN 접속", 420);
                menu.joinConfirm = Replace(menu.joinConfirm, "참가", "Confirm", 32, -218, 240);
                menu.joinBack = Replace(menu.joinBack, "뒤로", "Cancel", 288, -218, 240);
                menu.lanHost = Replace(menu.lanHost, "LAN 방 만들기", "General", 32, -96, 496);
                menu.lanJoin = Replace(menu.lanJoin, "참가", "Confirm", 378, -228, 150);
                menu.lanBack = Replace(menu.lanBack, "뒤로", "Cancel", 32, -322, 496);
                Input(menu.code, 32, -128, 496);
                Input(menu.address, 32, -228, 330);
                var addressLabel=menu.lanPage.transform.Find("AddressLabel").GetComponent<TMP_Text>();
                addressLabel.text="주소 (IP:포트) · 같은 PC: 127.0.0.1:7777";
                Rect(addressLabel.rectTransform,32,-184,496,32);
                var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SettingsUi/Pretendard-SemiBold SDF.asset");
                foreach(var page in new[]{menu.joinPage,menu.lanPage})
                    foreach(var text in page.GetComponentsInChildren<TMP_Text>(true))
                    {text.font=font;text.fontSharedMaterial=font.material;PrefabUtility.RecordPrefabInstancePropertyModifications(text);}
                EditorUtility.SetDirty(menu);
                EditorSceneManager.SaveScene(scene);
                var paths=Spaces.Concat(AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes/UI"}).Select(AssetDatabase.GUIDToAssetPath));
                foreach(var path in paths)
                {
                    scene=EditorSceneManager.OpenScene(path);
                    foreach(var canvas in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)))
                    {
                        if(canvas.renderMode==RenderMode.WorldSpace) continue;
                        var scaler=canvas.GetComponent<CanvasScaler>();
                        if(scaler==null) scaler=canvas.gameObject.AddComponent<CanvasScaler>();
                        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution=new Vector2(1920,1080);
                        scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(scaler);
                        EditorUtility.SetDirty(scaler);
                    }
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        static void Panel(GameObject page,string title,float height)
        {
            if(page.transform.Find("PopupSkin")!=null) throw new InvalidOperationException("Already installed.");
            var oldImage=page.GetComponent<UnityEngine.UI.Image>();
            if(oldImage!=null) UnityEngine.Object.DestroyImmediate(oldImage);
            var rule=page.transform.Find("Rule");if(rule!=null)UnityEngine.Object.DestroyImmediate(rule.gameObject);
            var rect=(RectTransform)page.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=new Vector2(0,-150);rect.sizeDelta=new Vector2(560,height);
            var skin=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/BasicPanels/BasicPanels.prefab"),page.transform);
            skin.name="PopupSkin";skin.transform.SetAsFirstSibling();
            foreach(var script in skin.GetComponentsInChildren<MonoBehaviour>(true))
                if(!(script is UnityEngine.UI.Graphic) && !(script is UnityEngine.UI.LayoutGroup) && !(script is UnityEngine.UI.LayoutElement)) UnityEngine.Object.DestroyImmediate(script);
            var canvas=skin.GetComponent<Canvas>();if(canvas!=null)UnityEngine.Object.DestroyImmediate(canvas);
            UnityEngine.Object.DestroyImmediate(skin.transform.Find("70_ConfirmDialogs").gameObject);
            var panel=skin.transform.Find("60_Panels/Menu_C13");
            UnityEngine.Object.DestroyImmediate(panel.Find("50_Content").gameObject);
            foreach(var group in skin.GetComponentsInChildren<CanvasGroup>(true)) {group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
            Stretch((RectTransform)skin.transform);Stretch((RectTransform)panel);panel.gameObject.SetActive(true);
            skin.transform.Find("60_Panels").gameObject.SetActive(true);
            var heading=page.transform.Find("Heading").GetComponent<TMP_Text>();
            heading.text=title;heading.color=new Color32(240,238,220,255);heading.fontSize=28;
            Rect(heading.rectTransform,32,-28,496,46);
        }
        static UnityEngine.UI.Button Replace(UnityEngine.UI.Button old,string label,string kind,float x,float y,float width)
        {
            var parent=old.transform.parent;string name=old.name;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/CommonButtons/Button_"+kind+".prefab"),parent);
            go.name=name;Rect((RectTransform)go.transform,x,y,width,56);
            go.GetComponentInChildren<TMP_Text>(true).text=label;
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            return go.GetComponent<UnityEngine.UI.Button>();
        }
        static void Input(TMP_InputField field,float x,float y,float width)
        {
            Rect((RectTransform)field.transform,x,y,width,58);
            var image=field.GetComponent<UnityEngine.UI.Image>();image.color=new Color32(18,28,29,245);
            var colors=field.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.85f,.93f,.93f);colors.selectedColor=colors.highlightedColor;field.colors=colors;
            field.textComponent.color=new Color32(240,238,220,255);field.textComponent.fontSize=23;
            if(field.placeholder is TMP_Text placeholder){placeholder.color=new Color32(156,171,169,255);placeholder.fontSize=22;}
        }
        static void Rect(RectTransform r,float x,float y,float w,float h)
        {r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
        static void Stretch(RectTransform r)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;}
    }
}
#endif
