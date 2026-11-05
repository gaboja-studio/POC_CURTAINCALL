using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CurtainCall.WaitingRoom.Pursuit;
using CurtainCall.WaitingRoom.Pursuit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Newtonsoft.Json.Linq;

namespace CurtainCall.Telemetry.Pipeline.Editor
{
    /// <summary>Read-only scene capture using the pursuit designer's floor clipping and world projection.</summary>
    public static class SpatialMapExporter
    {
        public static void ExportEscapeSections()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Levels/escape_route.unity",OpenSceneMode.Single);
            var sources=new[] {
                ("EscapeArrivalStairs","합류 계단",-2f), ("01_Understage","시작홀",-12f),
                ("02_CompressionGallery","진입 복도 · 첫 대포",-12f), ("03_04A_CannonWireDescent","착지 외줄 · 하강",-12f),
                ("04B_ConveyorCatch","이동 그네 · 하부 처리장",-25f), ("05_CooperativeReboundAndExit","소실 외줄 · 트램폴린",-12f),
                ("06_FlamesSwingCannonAndUpperWire","화염 · 상승 대포",-12f), ("01_RunUp_140_164","첫 반출교 · 추격 시작",-12f),
                ("02_SideDischarge_164_226","협동교 · 방출 구역",-12f), ("03_OverheadLoads_226_276","상부 설비 구역",-12f),
                ("04_SwingChasm_276_298","그네 단절 구역",-12f), ("05_LastSprint_298_366","마지막 질주",-12f),
                ("06_Exit_366_400","절단기 · 출구",-12f), ("56_ExitMeadow","갈대밭",-10f) };
            var transforms=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var regions=new List<(string name,Bounds bounds,float top)>();
            foreach(var source in sources){var t=transforms.FirstOrDefault(t=>t.name==source.Item1);if(t==null)continue;
                var rs=t.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.bounds.size.sqrMagnitude>.001f).ToArray();if(rs.Length==0)continue;
                var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);b.Expand(new Vector3(3,0,3));regions.Add((source.Item2,b,source.Item3));}
            if(regions.Count<10)throw new InvalidOperationException("Authored escape sections missing: "+regions.Count);
            var whole=regions[0].bounds;foreach(var r in regions.Skip(1))whole.Encapsulate(r.bounds);
            var rooms=regions.Select(r=>new{name=r.name,x=r.bounds.center.x,z=r.bounds.center.z,width=r.bounds.size.x,depth=r.bounds.size.z,route=Array.Empty<object>()}).ToArray();
            var manifest=JObject.Parse(File.ReadAllText("Assets/Resources/TelemetrySpatialMaps.json"));var maps=(JArray)manifest["maps"];
            foreach(var m in maps.Where(m=>(string)m["scene"]==scene.name).ToArray())m.Remove();
            var captures=new[]{("전체 탈출로",whole,-12f)}.Concat(regions.Select(r=>(r.name,r.bounds,r.top))).ToArray();
            for(int index=0;index<captures.Length;index++){
                var (label,b,top)=captures[index];float aspect=b.size.x/Mathf.Max(1,b.size.z);int w=aspect>=1?2400:Mathf.RoundToInt(2400*aspect),h=aspect>=1?Mathf.RoundToInt(2400/aspect):2400;
                var go=new GameObject("__EscapeMapCamera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.enabled=false;cam.cameraType=CameraType.Preview;cam.orthographic=true;
                var fill=go.AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=2.2f;fill.shadows=LightShadows.None;
                cam.orthographicSize=b.size.z/2;cam.aspect=aspect;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.065f,.075f,.09f);cam.cullingMask=~(1<<5);cam.allowHDR=false;
                var data=cam.GetUniversalAdditionalCameraData();data.renderShadows=false;data.renderPostProcessing=false;data.antialiasing=AntialiasingMode.None;
                cam.transform.SetPositionAndRotation(new Vector3(b.center.x,100,b.center.z),Quaternion.Euler(90,0,0));cam.nearClipPlane=100-top;cam.farClipPlane=100-Mathf.Max(-150,b.min.y-2);
                var target=new RenderTexture(w,h,24);var prior=RenderTexture.active;var image=new Texture2D(w,h,TextureFormat.RGB24,false);byte[] bytes;
                try{cam.targetTexture=target;cam.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();bytes=image.EncodeToJPG(85);}
                finally{RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);}
                string id=scene.name+"_section"+index+"_"+DurableOutbox.Sha256(Convert.ToBase64String(bytes)).Substring(0,12);File.WriteAllBytes(".harness/local/telemetry-maps/"+id+".jpg",bytes);
                maps.Add(JObject.FromObject(new{id,scene=scene.name,floorY=0,sectionId=index,sectionName=label,minX=b.min.x,maxX=b.max.x,minZ=b.min.z,maxZ=b.max.z,minY=b.min.y,maxY=top,width=w,height=h,rooms=index==0?rooms:rooms.Where(r=>r.name==label).ToArray()}));
                Debug.Log("Escape map: "+label+" => "+id);
            }
            File.WriteAllText("Assets/Resources/TelemetrySpatialMaps.json",manifest.ToString(Newtonsoft.Json.Formatting.None));File.WriteAllText(".harness/local/telemetry-maps/manifest.json",manifest.ToString(Newtonsoft.Json.Formatting.None));AssetDatabase.Refresh();
        }
        public static void AuditEscape()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Levels/escape_route.unity",OpenSceneMode.Single);
            var rows=new List<object>();
            foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
                if(t.gameObject.scene!=scene||t.childCount==0||!(t.name.StartsWith("Escape")||System.Text.RegularExpressions.Regex.IsMatch(t.name,@"^\d\d_")))continue;
                var renderers=t.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();if(renderers.Length==0)continue;
                var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);
                rows.Add(new{name=t.name,parent=t.parent!=null?t.parent.name:"",position=DetailLogIds.Position(t.position),local=DetailLogIds.Position(t.localPosition),min=DetailLogIds.Position(b.min),max=DetailLogIds.Position(b.max),count=renderers.Length});
            }
            File.WriteAllText(".harness/local/escape-map-audit.json",LogJson.Serialize(rows));
        }
        [MenuItem("Tools/Curtain Call/Telemetry/Export dashboard floor maps")]
        public static void ExportAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before exporting maps.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Save open scene changes before exporting maps.");
            var maps = new List<object>();
            string output = ".harness/local/telemetry-maps"; Directory.CreateDirectory(output);
            try {
                foreach (var path in new[] {
                    "Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity",
                    "Assets/Scenes/InGame/WaitingRoom/WaitingRoomPursuit.unity",
                    "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity",
                    "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity" }) {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var rooms = UnityEngine.Object.FindObjectsByType<PursuitRoom>(FindObjectsSortMode.None)
                        .Where(r => r.Area != null && r.gameObject.scene == scene).ToArray();
                    float Floor(PursuitRoom room) => Mathf.Round(PursuitAuthoring.FloorReference(room));
                    var floors = rooms.Select(Floor).Distinct().OrderBy(y => y).ToArray();
                    if (floors.Length == 0) floors = new[] { 0f };
                    foreach (float floor in floors) {
                        var local = rooms.Where(r => Floor(r) == floor).ToArray();
                        var bounds = local.Select(r => r.Area.bounds).ToArray();
                        if (bounds.Length == 0) bounds = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                            .Where(r => r.gameObject.scene == scene && r.enabled && r.gameObject.layer != 5 && r.bounds.size.sqrMagnitude < 4000000)
                            .Select(r => r.bounds).ToArray();
                        if (bounds.Length == 0) continue;
                        var b = bounds[0]; foreach (var next in bounds.Skip(1)) b.Encapsulate(next);
                        b.Expand(new Vector3(4, 0, 4));
                        float aspect = Mathf.Clamp(b.size.x / Mathf.Max(1, b.size.z), .2f, 5);
                        int width = aspect >= 1 ? 2400 : Mathf.RoundToInt(2400 * aspect);
                        int height = aspect >= 1 ? Mathf.RoundToInt(2400 / aspect) : 2400;
                        float spanZ = Mathf.Max(b.size.z, b.size.x / aspect), spanX = spanZ * aspect;
                        var go = new GameObject("__TelemetryMapCapture") { hideFlags = HideFlags.HideAndDontSave };
                        var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                        camera.cameraType = CameraType.Preview; camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = new Color(.065f,.075f,.09f); camera.cullingMask = ~(1 << 5); camera.allowHDR = false;
                        var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false;
                        data.renderShadows = false; data.antialiasing = AntialiasingMode.None;
                        bool layered = rooms.Length > 0;
                        float top = layered ? Mathf.Min(floor + 2.4f, floors.Where(y=>y>floor+.75f).DefaultIfEmpty(float.PositiveInfinity).Min()-.15f) : b.max.y+2;
                        float cameraY = Mathf.Max(floor+60,top+10);
                        camera.transform.SetPositionAndRotation(new Vector3(b.center.x,cameraY,b.center.z),Quaternion.Euler(90,0,0));
                        camera.nearClipPlane = cameraY-top; camera.farClipPlane = cameraY-(layered ? floor-.35f : b.min.y-2);
                        camera.orthographicSize = spanZ/2; camera.aspect = aspect;
                        var target = new RenderTexture(width,height,24); var prior = RenderTexture.active;
                        var image = new Texture2D(width,height,TextureFormat.RGB24,false);
                        byte[] bytes;
                        try {
                            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); bytes=image.EncodeToJPG(85);
                        } finally { RenderTexture.active=prior; UnityEngine.Object.DestroyImmediate(image); target.Release();
                            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(go); }
                        string id = scene.name+"_"+floor.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+"_"+DurableOutbox.Sha256(Convert.ToBase64String(bytes)).Substring(0,12);
                        File.WriteAllBytes(output+"/"+id+".jpg",bytes);
                        maps.Add(new { id, scene=scene.name, floorY=floor, width, height, minX=b.center.x-spanX/2, maxX=b.center.x+spanX/2,
                            minZ=b.center.z-spanZ/2, maxZ=b.center.z+spanZ/2,
                            rooms=local.Select(r=>new {name=r.name, x=r.Area.bounds.center.x,z=r.Area.bounds.center.z,width=r.Area.bounds.size.x,depth=r.Area.bounds.size.z,
                                route=(r.PatrolPoints??Array.Empty<Transform>()).Where(t=>t!=null).Select(t=>new{x=t.position.x,y=t.position.y,z=t.position.z}).ToArray()}).ToArray() });
                        Debug.Log("Telemetry map exported: "+id+" / "+local.Length+" rooms");
                    }
                }
                Directory.CreateDirectory("Assets/Resources");
                string json=LogJson.Serialize(new { maps });
                File.WriteAllText("Assets/Resources/TelemetrySpatialMaps.json",json);
                File.WriteAllText(output+"/manifest.json",json);
                ExportEscapeSections();
                AssetDatabase.Refresh();
            } finally {
                if (setup.Length > 0 && setup.All(s=>!string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
