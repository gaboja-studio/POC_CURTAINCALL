#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CurtainCall.ItemArt;
using CurtainCall.UI.SceneIntegration;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    /// <summary>Captures real slot templates and real item icons at deterministic tween times.</summary>
    public static class InventoryFxPreviewCapture
    {
        public static async Task<string> Capture(string directory)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture outside play mode.");
            Directory.CreateDirectory(directory);
            var previous = SceneManager.GetActiveScene();
            var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var source = EditorSceneManager.OpenScene("Assets/Scenes/UI/CommonMenuUI.unity", OpenSceneMode.Additive);
            try
            {
                var template = source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InventorySlotUseEffect>(true)).Single();
                SceneManager.SetActiveScene(preview);
                var cam = new GameObject("InventoryPreviewCamera").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.025f,.031f,.046f);
                cam.cullingMask = 1 << 5; cam.orthographic = true; cam.orthographicSize = 450; cam.farClipPlane = 100;
                var canvas = new GameObject("ActualSlotComparison", typeof(RectTransform)).AddComponent<Canvas>();
                canvas.gameObject.layer = 5;
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = cam;
                canvas.transform.position = new Vector3(0,0,10);
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(1600,900);
                string[] stages = { "READY", "DURABILITY -1", "LIFT", "DISSOLVE", "EMPTY" };
                float[] times = { 0, .09f, .22f, .7f, 1.1f };
                string[] items = { "spring-shoes", "magician-hairpin" };
                Label(canvas.transform, "INVENTORY  /  ALL IN 1 SPRITE SHADER", new Vector2(0,380), 30);
                for (int col=0; col<5; col++) Label(canvas.transform, stages[col], new Vector2(-620+col*310,290), 22);
                for (int row=0; row<2; row++)
                {
                    float y = row == 0 ? 110 : -180;
                    Label(canvas.transform, row == 0 ? "PERFORMANCE  /  SPRING SHOES" : "EXPLORATION  /  MAGICIAN HAIRPIN", new Vector2(0,y-130), 20);
                    for (int col=0; col<5; col++)
                    {
                        var slot = UnityEngine.Object.Instantiate(template.gameObject, canvas.transform, false);
                        slot.name = items[row]+"_"+stages[col]; slot.SetActive(true);
                        foreach (var t in slot.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
                        var rect = (RectTransform)slot.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
                        rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = new Vector2(-620+col*310,y); rect.sizeDelta = new Vector2(64,64); rect.localScale = Vector3.one*2.6f;
                        foreach (var text in slot.GetComponentsInChildren<TMP_Text>(true)) text.text = "";
                        var effect = slot.GetComponent<InventorySlotUseEffect>(); var serialized = new SerializedObject(effect);
                        var icon = (Image)serialized.FindProperty("icon").objectReferenceValue;
                        var flash = (Image)serialized.FindProperty("flash").objectReferenceValue;
                        var ghost = (Image)serialized.FindProperty("breakGhost").objectReferenceValue;
                        var sprite = ItemVisualAssets.Icon(items[row]);
                        if (!sprite) throw new InvalidOperationException("Missing actual item icon: "+items[row]);
                        icon.sprite = sprite; icon.enabled = col < 2; icon.color = Color.white; ghost.enabled = false;
                        var flashColor = flash.color; flashColor.a=0; flash.color=flashColor;
                        var bar = slot.GetComponentInChildren<InventorySlotDurabilityBar>(true);
                        var barFields = new SerializedObject(bar);
                        ((Image)barFields.FindProperty("segmentTemplate").objectReferenceValue).gameObject.SetActive(false);
                        int max = row == 0 ? 2 : 5; bar.Show(col==0 ? max : col==1 ? max-1 : 0,max,false);
                        if (col>=2) bar.Hide();
                        if (col==1) { effect.PlayUse(); DOTween.Goto(effect,times[col],false); DOTween.Goto(icon.rectTransform,times[col],false); DOTween.Goto(flash,times[col],false); }
                        if (col>=2) { effect.PlayBreak(sprite); DOTween.Goto(effect,times[col],false); DOTween.Goto(flash,times[col],false); }
                    }
                }
                Label(canvas.transform, "Actual shared slot template + item sprites. Captured at fixed animation times.", new Vector2(0,-390), 19);
                await Task.Delay(150);
                Canvas.ForceUpdateCanvases();
                string path = Path.Combine(directory,"inventory-durability-sequence.png");
                CheerPointConceptBuilder.Render(cam,path); return path;
            }
            finally
            {
                EditorSceneManager.CloseScene(source,true); EditorSceneManager.CloseScene(preview,true);
                SceneManager.SetActiveScene(previous);
            }
        }

        static void Label(Transform parent,string value,Vector2 position,float size)
        {
            var label = new GameObject("Label",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent,false); label.gameObject.layer=5;
            label.font = Resources.Load<TMP_FontAsset>("Fonts/CommonButtons/Pretendard-SemiBold SDF");
            label.text=value; label.fontSize=size; label.alignment=TextAlignmentOptions.Center;
            label.color=new Color(.84f,.78f,.65f); label.raycastTarget=false;
            label.rectTransform.sizeDelta=new Vector2(1500,55); label.rectTransform.anchoredPosition=position;
        }
    }
}
#endif
