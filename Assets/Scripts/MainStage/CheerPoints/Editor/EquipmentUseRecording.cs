#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    /// <summary>Opt-in capture tooling. Records live gameplay; never runs in a player build.</summary>
    public sealed class EquipmentUseRecording
    {
        public static readonly string Output = Path.GetFullPath("Temp/PerformanceEquipment/UseVideos");
        static EquipmentUseRecording current;
        GameObject gameObject;
        RecorderController recorder;
        RecorderControllerSettings config;
        MovieRecorderSettings movie;
        Camera camera;
        Camera[] previousCameras;
        Transform actor;
        Vector3 focus, offset;
        bool follow;
        GameObject canvas;
        TextMeshProUGUI text;
        TMP_FontAsset captionFont;
        string clip, heading;

        public static void Begin(string name, GameObject player, Vector3 center, Vector3 cameraOffset, bool followActor, string title)
        {
            if(current != null) throw new System.InvalidOperationException("Recording already active");
            Directory.CreateDirectory(Output);
            current = new EquipmentUseRecording();
            current.gameObject = new GameObject("Equipment gameplay recording");
            current.Setup(name,player,center,cameraOffset,followActor,title);
            EditorApplication.update += current.LateUpdate;
        }
        void Setup(string name,GameObject player,Vector3 center,Vector3 cameraOffset,bool followActor,string title)
        {
            clip=name; heading=title; actor=player.transform; focus=center; offset=cameraOffset; follow=followActor;
            var source=Camera.main;
            previousCameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled && c.targetTexture==null).ToArray();
            camera=gameObject.AddComponent<Camera>(); if(source)camera.CopyFrom(source);
            camera.tag="MainCamera";
            camera.targetTexture=null; camera.rect=new Rect(0,0,1,1); camera.depth=99; camera.fieldOfView=48;
            camera.nearClipPlane=.05f; camera.farClipPlane=600;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            foreach(var c in previousCameras)c.enabled=false;
            canvas=new GameObject("Recording caption",typeof(Canvas),typeof(CanvasScaler));
            var cv=canvas.GetComponent<Canvas>(); cv.renderMode=RenderMode.ScreenSpaceOverlay;cv.sortingOrder=32000;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            var panel=new GameObject("Caption",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(0,0);rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(30,105);rect.sizeDelta=new Vector2(560,105);
            panel.GetComponent<Image>().color=new Color(.025f,.025f,.035f,.87f);
            var label=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(panel.transform,false);
            captionFont=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/Pretendard-Medium/Pretendard-Medium 1"));
            text=label.GetComponent<TextMeshProUGUI>();text.font=captionFont;text.fontSize=23;text.color=new Color(1,.91f,.72f);
            var tr=text.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(18,10);tr.offsetMax=new Vector2(-12,-10);
            Caption("발동 전 · 실제 공연 씬 / 자동 조작 시연"); LateUpdate();
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            config=ScriptableObject.CreateInstance<RecorderControllerSettings>();
            config.SetRecordModeToManual();config.FrameRate=30;config.FrameRatePlayback=FrameRatePlayback.Variable;config.CapFrameRate=true;
            movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();movie.name="Equipment "+name;movie.Enabled=true;
            movie.EncoderSettings=new CoreEncoderSettings{EncodingQuality=CoreEncoderSettings.VideoEncodingQuality.High,Codec=CoreEncoderSettings.OutputCodec.MP4};
            movie.ImageInputSettings=new GameViewInputSettings{OutputWidth=1280,OutputHeight=720};movie.CaptureAudio=false;
            movie.OutputFile=Path.Combine(Output,name);config.AddRecorderSettings(movie);
            recorder=new RecorderController(config);recorder.PrepareRecording();recorder.StartRecording();
        }
        public static void Caption(string message) { if(current != null)current.text.text=$"<b>{current.heading}</b>\n<size=19>{message}</size>"; }
        public static void Snapshot(string suffix) { if(current != null) ScreenCapture.CaptureScreenshot(Path.Combine(Output,current.clip+"-"+suffix+".png")); }
        public static void End()
        {
            if(current == null)return;
            EditorApplication.update -= current.LateUpdate;
            current.recorder?.StopRecording();
            foreach(var c in current.previousCameras)if(c)c.enabled=true;
            Object.Destroy(current.canvas);Object.Destroy(current.captionFont);Object.Destroy(current.movie);Object.Destroy(current.config);Object.Destroy(current.gameObject);current=null;
        }
        void LateUpdate()
        {
            var at=follow && actor ? actor.position+Vector3.up*.8f : focus;
            camera.transform.position=at+offset;camera.transform.LookAt(at);
        }
    }
}
#endif
