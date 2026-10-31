#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Cameras;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.EscapeRoute.Editor
{
    public static class EscapeWaitingRoomLinkBuilder
    {
        public const string WaitingPath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        public const string EscapePath = "Assets/Scenes/Levels/escape_route.unity";
        const string ViewPath = "Assets/Resources/Settings/Camera/EscapeEntryFirstPerson.asset";
        static Material concrete, metal;
        static TMP_FontAsset font;
        static Transform parent;

        [MenuItem("Tools/CurtainCall/Escape/Connect Waiting Room")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene: " + SceneManager.GetSceneAt(i).path);
            var waiting = EditorSceneManager.OpenScene(WaitingPath, OpenSceneMode.Single);
            var escape = EditorSceneManager.OpenScene(EscapePath, OpenSceneMode.Additive);
            if (Find<EscapeEntrance>(waiting) != null || Find<EscapeCampaignContent>(escape) != null)
                throw new InvalidOperationException("Connection already exists; inspect and update it in place.");
            concrete = escape.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true))
                .First(r => r.gameObject.name.Contains("콘크리트") && r.sharedMaterial != null).sharedMaterial;
            metal = waiting.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true))
                .First(r=>r.gameObject.name == "EscapeDoor" && r.sharedMaterial != null).sharedMaterial;
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Scenes/Levels/EscapeRouteMaterials/escape-labels.asset");

            var previousRoots = escape.GetRootGameObjects();
            SceneManager.SetActiveScene(escape);
            var frame = new GameObject("EscapeCampaignFrame");
            var body = new GameObject("FacilityContent"); body.transform.SetParent(frame.transform, false);
            var campaign = frame.AddComponent<EscapeCampaignContent>();
            parent = body.transform;
            // Open the back of the existing starting hall, preserving all later routes.
            CutWall(escape, new Bounds(new Vector3(-4, 4, 19.5f), new Vector3(10, 8, 3.6f)), "A entry opening");
            var entryJamb=previousRoots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="두꺼운 문틀 좌측");
            entryJamb.position=new Vector3(9.5f,4.5f,21.5f); entryJamb.localScale=new Vector3(3,9,2);
            Box("Entry threshold", new Vector3(-1, -.12f, 19.5f), new Vector3(3, .24f, 3.6f), concrete);
            var seal = Box("StandaloneEntranceSeal", new Vector3(-1.8f, 4, 19.5f), new Vector3(.2f, 8, 3.5f), metal);
            var disabledObjects = previousRoots.Where(r=>r.name == "Main Camera").ToList();
            disabledObjects.Add(seal);
            disabledObjects.AddRange(previousRoots.SelectMany(r=>r.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true)).Select(c=>c.gameObject));
            var disabledBehaviours = previousRoots.SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c is PlayerSpawnPoints
                || c is EscapeDevelopmentSession || c is EscapeInspectionSession || c is EscapeInspectionCamera || c is EscapeCannonReviewHud).Cast<Behaviour>().ToArray();
            foreach (var fall in previousRoots.SelectMany(r=>r.GetComponentsInChildren<EscapeFallVoid>(true))) Set(fall, "coordinateSpace", frame.transform);
            foreach (var fire in previousRoots.SelectMany(r=>r.GetComponentsInChildren<EscapePursuitFire>(true))) Set(fire, "coordinateSpace", frame.transform);
            foreach (var cannon in previousRoots.SelectMany(r=>r.GetComponentsInChildren<HumanCannonSystem>(true))) Set(cannon, "flightCoordinateSpace", frame.transform);
            var saw = previousRoots.SelectMany(r=>r.GetComponentsInChildren<EscapeExitSaw>(true)).First();
            var sawData = new SerializedObject(saw);
            var left = sawData.FindProperty("leftDoorHinge").objectReferenceValue as Transform;
            var right = sawData.FindProperty("rightDoorHinge").objectReferenceValue as Transform;
            if (left == null || right == null) throw new InvalidOperationException("Final exit hinges missing.");
            var doorCenter = (left.position + right.position) * .5f;
            // The route runs in original +X. Only actual passage beyond the opened doors counts.
            var arrival = Zone("FinalArrival", new Vector3(doorCenter.x + 4, doorCenter.y + 2, doorCenter.z), new Vector3(5, 5, 22));
            Set(campaign, "content", body); Set(campaign, "exitSaw", saw); Set(campaign, "arrivalArea", arrival);
            SetArray(campaign, "sceneRoots", previousRoots);
            SetArray(campaign, "activeRoots", previousRoots.Where(r=>r.activeSelf).ToArray());
            Set(campaign, "campaignPosition", new Vector3(-100, -16, 118.3f)); Set(campaign, "campaignEuler", new Vector3(0, 180, 0));
            SetArray(campaign, "campaignDisabledObjects", disabledObjects.Distinct().ToArray());
            SetArray(campaign, "campaignDisabledBehaviours", disabledBehaviours);

            SceneManager.SetActiveScene(waiting);
            var connector = new GameObject("BasementEscapeConnection"); parent = connector.transform;
            var entrance = connector.AddComponent<EscapeEntrance>();
            // The existing puzzle door has a solid shell wall behind it; cut only the opening.
            CutWall(waiting, new Bounds(new Vector3(-14.05f, -8.2f, 93.35f), new Vector3(1, 3.6f, 2.6f)), "Basement door opening");
            var doorAperture = new Bounds(new Vector3(-14.05f,-8.2f,93.35f),new Vector3(1,3.6f,2.6f));
            var shell = waiting.GetRootGameObjects().First(r=>r.name=="WaitingRoom_LayoutA").transform.Find("B1_EscapeEntrance/Shell");
            int meshIndex=0;
            foreach(var filter in shell.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<Renderer>();
                if(renderer==null || !renderer.bounds.Intersects(doorAperture) || (filter.name!="WallSurfaces" && filter.name!="SteelLedger")) continue;
                EscapeOpeningMesh.Cut(filter,doorAperture,$"Assets/Scenes/Levels/EscapeRouteMaterials/basement-entry-opening-{meshIndex++}.asset");
            }
            var room = shell.parent;
            var puzzle = room.Find("Exploration_A/escapeDoor").GetComponent<CurtainCall.WaitingRoom.ExplorationGate>();
            var gateData = new SerializedObject(puzzle); var hide = gateData.FindProperty("hideOnOpen");
            var hidden = Enumerable.Range(0,hide.arraySize).Select(i=>hide.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
            foreach(Transform part in room.Find("SealedDoor"))
                if(part.name=="EscapeDoor" || part.name=="DoorRib" || part.name=="LockWheel" || part.name=="Rope") hidden.Add(part.gameObject);
            SetArray(puzzle,"hideOnOpen",hidden.Where(o=>o!=null).Distinct().ToArray());
            // Existing key-slot door art and its ExplorationGate remain intact.
            Straight(new Vector3(-14.15f, -10, 93.35f), new Vector3(-21, -10, 93.35f), 4.6f, false);
            var area = Zone("FourPlayerAssembly", new Vector3(-17.65f, -8.2f, 93.35f), new Vector3(5.8f, 3.8f, 4.4f));
            var front = Box("AssemblyDepartureDoor", new Vector3(-20.8f, -7.7f, 93.35f), new Vector3(.2f, 4.6f, 4.5f), metal);
            var back = Box("DepartureReturnSeal", new Vector3(-14.3f, -8.2f, 93.35f), new Vector3(.2f, 3.6f, 2.6f), metal); back.SetActive(false);
            Straight(new Vector3(-21, -10, 93.35f), new Vector3(-27, -13, 93.35f), 3.4f, true);
            Landing(new Vector3(-28.7f, -13, 93.35f), true);
            Straight(new Vector3(-28.7f, -13, 95.05f), new Vector3(-28.7f, -13, 97.1f), 3.4f, false);
            Landing(new Vector3(-28.7f, -13, 98.8f), false);
            Straight(new Vector3(-30.4f, -13, 98.8f), new Vector3(-36.4f, -16, 98.8f), 3.4f, true);
            Straight(new Vector3(-36.4f, -16, 98.8f), new Vector3(-100.1f, -16, 98.8f), 3.4f, false);
            var firstPerson = Zone("FirstPersonAtStairEnd", new Vector3(-38, -14, 98.8f), new Vector3(3.2f, 4.3f, 3.3f));
            var view = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(ViewPath);
            if (view == null) { view = ScriptableObject.CreateInstance<CameraViewPreset>(); AssetDatabase.CreateAsset(view, ViewPath); }
            view.Kind = CameraViewKind.FirstPerson; view.FieldOfView = 70; view.TransitionDuration = .7f; EditorUtility.SetDirty(view);
            Box("Assembly sign backing",new Vector3(-20.72f,-5.8f,93.35f),new Vector3(.08f,1.3f,4.3f),metal);
            var label = Label("Assembly sign", new Vector3(-20.64f, -5.8f, 93.35f), Quaternion.Euler(0,-90,0), new Vector2(4.1f, 1.1f), 2.4f);
            label.text = "탈출 준비\n플레이 중인 전원이 모이면 출발";
            label.enableAutoSizing=true; label.fontSizeMin=1.4f; label.fontSizeMax=2.4f;
            var hud = BuildHud(out var hudText); hud.SetActive(false);
            Set(entrance, "assemblyArea", area);
            Set(entrance, "forwardBarrier", front); Set(entrance, "returnBarrier", back);
            Set(entrance, "worldLabel", label);
            Set(entrance, "hudRoot", hud); Set(entrance, "hudLabel", hudText);
            LightAt(new Vector3(-17.7f,-6.2f,93.35f)); LightAt(new Vector3(-24,-7.6f,93.35f));
            LightAt(new Vector3(-28.7f,-9.2f,96)); LightAt(new Vector3(-33.4f,-10.6f,98.8f)); LightAt(new Vector3(-38,-12.2f,98.8f));
            for(float x=-45;x>=-94;x-=7) LightAt(new Vector3(x,-12.2f,98.8f));
            // Save the facility outside the entire waiting room, rather than overlapping it until Play.
            campaign.PlaceCampaign();
            foreach(var camera in frame.GetComponentsInChildren<UnityEngine.Camera>(true)) camera.useOcclusionCulling=false;
            var settings = AssetDatabase.LoadAssetAtPath<BaseGameSettings>("Assets/Resources/Settings/GameSettings/BaseGameSettings.asset");
            var so = new SerializedObject(settings);
            so.FindProperty("spaceTransition.escapeRouteScene").stringValue = "escape_route";
            var voice = so.FindProperty("voiceChat.scenes");
            if (!Enumerable.Range(0,voice.arraySize).Any(i=>voice.GetArrayElementAtIndex(i).stringValue=="escape_route"))
            { voice.InsertArrayElementAtIndex(voice.arraySize); voice.GetArrayElementAtIndex(voice.arraySize-1).stringValue="escape_route"; }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(waiting); EditorSceneManager.MarkSceneDirty(escape);
            EditorSceneManager.SaveScene(waiting); EditorSceneManager.SaveScene(escape);
            AssetDatabase.SaveAssetIfDirty(settings); AssetDatabase.SaveAssetIfDirty(view);
            SceneManager.SetActiveScene(waiting);
            EscapeLoadingConnectionBuilder.Build();
        }

        static T Find<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        static GameObject Box(string name, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
            go.transform.position=center; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material; return go;
        }
        static BoxCollider Zone(string name, Vector3 center, Vector3 size)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.position=center;
            var box=go.AddComponent<BoxCollider>(); box.size=size; box.isTrigger=true; return box;
        }
        static void Straight(Vector3 a, Vector3 b, float width, bool stairs)
        {
            var delta=b-a; var flat=new Vector3(delta.x,0,delta.z); var side=Vector3.Cross(Vector3.up,flat.normalized);
            var floor=Box(stairs ? "Stair walking ramp" : "Connecting floor",(a+b)*.5f-Vector3.up*.12f,new Vector3(width,.24f,delta.magnitude+.04f),concrete);
            floor.transform.rotation=Quaternion.LookRotation(delta.normalized,Vector3.up);
            var roof=Box("Enclosed ceiling",(a+b)*.5f+Vector3.up*4.9f,new Vector3(width+.6f,.3f,delta.magnitude+.1f),concrete);
            roof.transform.rotation=floor.transform.rotation;
            for(int s=-1;s<=1;s+=2)
            {
                var wall=Box("Corridor wall",(a+b)*.5f+side*s*(width*.5f+.15f)+Vector3.up*2.4f,
                    new Vector3(.3f,4.9f+Mathf.Abs(delta.y),flat.magnitude+.05f),concrete);
                wall.transform.rotation=Quaternion.LookRotation(flat);
            }
            if (!stairs) return;
            int count=Mathf.CeilToInt(Mathf.Abs(delta.y)/.15f);
            for(int i=0;i<count;i++)
            {
                var point=Vector3.Lerp(a,b,(i+.5f)/count);
                var step=Box("Stair tread",point-Vector3.up*.12f,new Vector3(width,.24f,flat.magnitude/count),concrete);
                step.transform.rotation=Quaternion.LookRotation(flat); UnityEngine.Object.DestroyImmediate(step.GetComponent<Collider>());
            }
        }
        static void Landing(Vector3 c,bool first)
        {
            Box("Corner landing",c-Vector3.up*.12f,new Vector3(3.4f,.24f,3.4f),concrete);
            Box("Corner ceiling",c+Vector3.up*4.9f,new Vector3(4,.3f,4),concrete);
            Box("Corner outside wall X",c+new Vector3(first ? -1.85f : 1.85f,2.4f,0),new Vector3(.3f,4.9f,4),concrete);
            Box("Corner outside wall Z",c+new Vector3(0,2.4f,first ? -1.85f : 1.85f),new Vector3(4,4.9f,.3f),concrete);
        }
        static void CutWall(Scene scene,Bounds aperture,string label)
        {
            var walls=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BoxCollider>(true))
                .Where(c=>c.gameObject.activeInHierarchy && !c.isTrigger && c.enabled && c.bounds.Intersects(aperture)
                    && c.bounds.size.x<=5 && c.bounds.size.y>3.7f && c.bounds.size.z>=aperture.size.z).ToArray();
            foreach(var wall in walls)
            {
                var bounds=wall.bounds; var renderer=wall.GetComponent<Renderer>();
                wall.enabled=false; if(renderer!=null) renderer.enabled=false;
                Material mat=renderer!=null && renderer.sharedMaterial!=null ? renderer.sharedMaterial : concrete;
                void Part(string suffix,Vector3 min,Vector3 max)
                { var size=max-min; if(size.x>.01f && size.y>.01f && size.z>.01f)
                    { var part=Box(label+suffix,(min+max)*.5f,size,mat); if(renderer==null) part.GetComponent<Renderer>().enabled=false; } }
                Part(" left jamb",bounds.min,new Vector3(bounds.max.x,bounds.max.y,aperture.min.z));
                Part(" right jamb",new Vector3(bounds.min.x,bounds.min.y,aperture.max.z),bounds.max);
                Part(" sill foundation",new Vector3(bounds.min.x,bounds.min.y,Mathf.Max(bounds.min.z,aperture.min.z)),
                    new Vector3(bounds.max.x,aperture.min.y,Mathf.Min(bounds.max.z,aperture.max.z)));
                Part(" lintel",new Vector3(bounds.min.x,aperture.max.y,Mathf.Max(bounds.min.z,aperture.min.z)),
                    new Vector3(bounds.max.x,bounds.max.y,Mathf.Min(bounds.max.z,aperture.max.z)));
            }
        }
        static TMP_Text Label(string name,Vector3 position,Quaternion rotation,Vector2 size,float fontSize)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.SetPositionAndRotation(position,rotation);
            var t=go.AddComponent<TextMeshPro>(); t.font=font; t.fontSize=fontSize; t.alignment=TextAlignmentOptions.Center;
            t.color=new Color(.85f,.95f,1); t.rectTransform.sizeDelta=size; return t;
        }
        static GameObject BuildHud(out TMP_Text text)
        {
            var go=new GameObject("AssemblyCountdownHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)); go.transform.SetParent(parent,false);
            var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=45;
            var scale=go.GetComponent<CanvasScaler>(); scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scale.referenceResolution=new Vector2(1920,1080);
            var panel=new GameObject("AssemblyStatus",typeof(RectTransform),typeof(Image)); panel.transform.SetParent(go.transform,false);
            var rect=panel.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=new Vector2(.5f,1); rect.pivot=new Vector2(.5f,1); rect.anchoredPosition=new Vector2(0,-45); rect.sizeDelta=new Vector2(560,112);
            panel.GetComponent<Image>().color=new Color(.025f,.035f,.04f,.9f); panel.GetComponent<Image>().raycastTarget=false;
            var line=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI)); line.transform.SetParent(panel.transform,false);
            var lr=line.GetComponent<RectTransform>(); lr.anchorMin=Vector2.zero; lr.anchorMax=Vector2.one; lr.offsetMin=new Vector2(12,8); lr.offsetMax=new Vector2(-12,-8);
            text=line.GetComponent<TextMeshProUGUI>(); text.font=font; text.fontSize=25; text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false; return go;
        }
        static void LightAt(Vector3 position)
        {
            Box("Ceiling light housing",position+Vector3.up*.35f,new Vector3(.9f,.2f,.32f),metal);
            Box("Ceiling light support",position+Vector3.up*.7f,new Vector3(.08f,.6f,.08f),metal);
            var go=new GameObject("Stair light"); go.transform.SetParent(parent,false); go.transform.position=position;
            var light=go.AddComponent<Light>(); light.type=LightType.Point; light.range=7; light.intensity=2.2f; light.color=new Color(1,.84f,.63f); light.shadows=LightShadows.None;
        }
        static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
        { var so=new SerializedObject(target); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        static void Set(UnityEngine.Object target,string field,Vector3 value)
        { var so=new SerializedObject(target); so.FindProperty(field).vector3Value=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetArray(UnityEngine.Object target,string field,UnityEngine.Object[] values)
        { var so=new SerializedObject(target); var p=so.FindProperty(field); p.arraySize=values.Length; for(int i=0;i<values.Length;i++) p.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
#endif
