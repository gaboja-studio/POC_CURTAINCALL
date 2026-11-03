using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public static class PursuitAppearanceReview
    {
        public static string ExportStage(int stage) => ExportDetail(stage, false);
        public static string ExportCaptureStains() => ExportDetail(1, true);
        static string ExportDetail(int stage, bool stains)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var sheet = new Texture2D(1600, 600, TextureFormat.RGB24, false);
            var target = new RenderTexture(400, 600, 24); var previous = RenderTexture.active;
            try {
                var cameraObject = new GameObject("Stage detail camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 1.3f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f,.11f,.12f); camera.targetTexture = target;
                for (int i = 0; i < 2; i++) {
                    var lightObject = new GameObject("Stage detail light", typeof(Light)); SceneManager.MoveGameObjectToScene(lightObject, scene);
                    var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = i == 0 ? 1.8f : .8f;
                    light.transform.rotation = Quaternion.Euler(i == 0 ? 35 : 20, i == 0 ? 145 : -35, 0);
                }
                bool advanced = stage >= CurtainCall.Settings.GameSettings.WaitingRoom.Pursuit.AdvancedAppearanceStage;
                for (int view = 0; view < 4; view++) {
                    var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_01.prefab"));
                    SceneManager.MoveGameObjectToScene(go, scene); var look = go.GetComponent<PursuitAppearance>(); var actor = go.GetComponent<PursuitActor>();
                    foreach (var detail in look.AdvancedDetails) detail.SetActive(advanced);
                    foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                        var props = new MaterialPropertyBlock(); props.SetFloat("_Threat", advanced ? 1 : 0);
                        props.SetFloat("_BloodAmount", stains ? Mathf.Clamp01(view * CurtainCall.Settings.GameSettings.WaitingRoom.Pursuit.BloodPerCapture) : 0);
                        skin.SetPropertyBlock(props);
                    }
                    var motion = stains || view < 2 ? PursuitMotion.Display : view == 2 ? PursuitMotion.Walk : PursuitMotion.Run;
                    var clip = advanced && view >= 2 && !stains ? (view == 2 ? look.AdvancedWalk : look.AdvancedRun) : actor.Clips[(int)motion];
                    clip.SampleAnimation(go, view < 2 || stains ? 0 : clip.length * .22f);
                    var rotation = Quaternion.Euler(0, stains ? 30 : view == 0 ? 0 : 90, 0);
                    var focus = new Vector3(0,.87f, !stains && view >= 2 ? .2f : 0);
                    camera.transform.position = focus + rotation * new Vector3(0,.16f,4);
                    camera.transform.LookAt(focus); camera.Render(); RenderTexture.active = target;
                    sheet.ReadPixels(new Rect(0,0,400,600), view*400, 0); Object.DestroyImmediate(go);
                }
                sheet.Apply(); string path = "Assets/_ThirdParty/curtaincall/Pursuit/Reclaimed/Review/" + (stains ? "capture-stains.png" : $"stage-{stage:00}.png");
                File.WriteAllBytes(path, sheet.EncodeToPNG()); AssetDatabase.ImportAsset(path); return path;
            } finally { RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(sheet); EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static string Export()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var sheet=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            var target=new RenderTexture(400,500,24);var previous=RenderTexture.active;
            try {
                var cameraObject=new GameObject("Appearance review camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1.15f;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.11f,.12f);camera.targetTexture=target;
                var lightObject=new GameObject("Review light",typeof(Light));SceneManager.MoveGameObjectToScene(lightObject,scene);
                var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(35,145,0);
                var fillObject=new GameObject("Review fill",typeof(Light));SceneManager.MoveGameObjectToScene(fillObject,scene);
                var fill=fillObject.GetComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.transform.rotation=Quaternion.Euler(20,-35,0);
                for(int row=0;row<2;row++) {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_01.prefab");
                    var go=Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(go,scene);
                    var look=go.GetComponent<PursuitAppearance>();
                    foreach(var detail in look.AdvancedDetails) detail.SetActive(row==1);
                    foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                        var props=new MaterialPropertyBlock();props.SetFloat("_Threat",row);props.SetFloat("_BloodAmount",row*.7f);skin.SetPropertyBlock(props);
                    }
                    if(row==1) look.AdvancedWalk.SampleAnimation(go,.2f);
                    for(int view=0;view<4;view++) {
                        var rotation=Quaternion.Euler(0,view*90,0);camera.transform.position=new Vector3(0,.87f,0)+rotation*new Vector3(0,.16f,4);
                        camera.transform.LookAt(new Vector3(0,.87f,0));camera.Render();RenderTexture.active=target;
                        sheet.ReadPixels(new Rect(0,0,400,500),view*400,(1-row)*500);
                    }
                    Object.DestroyImmediate(go);
                }
                sheet.Apply();string path="Assets/_ThirdParty/curtaincall/Pursuit/Reclaimed/unity-four-view-review.png";
                File.WriteAllBytes(path,sheet.EncodeToPNG());AssetDatabase.ImportAsset(path);return path;
            } finally {RenderTexture.active=previous;Object.DestroyImmediate(target);Object.DestroyImmediate(sheet);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
