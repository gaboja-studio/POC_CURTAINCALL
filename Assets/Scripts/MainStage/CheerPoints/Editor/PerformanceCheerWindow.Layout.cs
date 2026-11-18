using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Saws;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    public sealed partial class PerformanceCheerWindow
    {
        [SerializeField] int editMode;
        Transform device;
        GameObject deviceTemplate;
        PerformanceLayout previewLayout;
        int ropeSelection=-1;
        Vector2 deviceScroll,detailScroll;
        GameObject ropePreview;
        string ropePreviewKey;
        Material diagramMaterial;
        static readonly Type[] DeviceTypes={typeof(HumanCannonStation),typeof(HumanCannonHazard),
            typeof(HumanCannonPole),typeof(HumanCannonLandingZone),typeof(HumanCannonTrampoline),
            typeof(HumanCannonTransport),typeof(HumanCannonSawGate),typeof(HumanCannonFireWall),typeof(HumanCannonGroundFireCycle)};
        IEnumerable<Transform> Devices => !CourseScene.isLoaded ? Array.Empty<Transform>() : CourseScene.GetRootGameObjects()
            .SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true))
            .Where(c=>c && DeviceTypes.Contains(c.GetType())).Select(c=>c.transform).Distinct().OrderBy(t=>t.name);
        TightropeCourse RopeCourse => CourseScene.isLoaded ? CourseScene.GetRootGameObjects()
            .SelectMany(g=>g.GetComponentsInChildren<TightropeCourse>(true)).FirstOrDefault() : null;
        Vector3 RopeOrigin => RopeCourse ? RopeCourse.transform.position : Vector3.zero;
        SawSettings RopeSaws => GameSettings.Tightrope.SawsFor(difficulty);
        PerformanceLayout Layout(bool create=false)
        {
            if(!CourseScene.isLoaded) return null;
            var layout=CourseScene.GetRootGameObjects().Select(g=>g.GetComponent<PerformanceLayout>()).FirstOrDefault(c=>c);
            if(!layout && create)
            {
                var go=new GameObject("PerformanceDifficultyLayout"); SceneManager.MoveGameObjectToScene(go,CourseScene);
                layout=go.AddComponent<PerformanceLayout>(); layout.trickId=Trick;
                Undo.RegisterCreatedObjectUndo(go,"Create performance layout");
            }
            return layout;
        }
        void ApplyLayoutPreview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            previewLayout=Layout(); if(previewLayout) previewLayout.Apply(difficulty);
        }
        void RestoreLayoutPreview() { if(previewLayout && !Application.isPlaying) previewLayout.Restore(); previewLayout=null; }
        void BeforeSave(Scene scene,string path) { if(scene==CourseScene) RestoreLayoutPreview(); }
        void AfterSave(Scene scene) { if(scene==CourseScene) ApplyLayoutPreview(); }

        public void SetDeviceContext(string trick,int level) { SetContext(trick,level); editMode=1; }
        public void SelectDevice(Transform target)
        {
            device=target; ropeSelection=-1;
            if(target) planeHeight=target.position.y;
            // Keep Unity's native transform tool from writing preview poses into the base scene.
            Selection.activeObject=null; Repaint();
        }
        public void SetDevicePose(Transform target,Vector3 world,Quaternion rotation,Vector3 scale,bool active)
        {
            var layout=Layout(true); if(!layout || !target) return;
            Undo.RecordObject(layout,"Edit difficulty placement");
            var entry=layout.Track(target); var pose=entry.levels[difficulty-1];
            pose.position=target.parent ? target.parent.InverseTransformPoint(world) : world;
            pose.euler=(target.parent ? Quaternion.Inverse(target.parent.rotation)*rotation : rotation).eulerAngles;
            pose.scale=scale; pose.active=active; entry.levels[difficulty-1]=pose;
            EditorUtility.SetDirty(layout); EditorSceneManager.MarkSceneDirty(CourseScene); ApplyLayoutPreview();
        }
        public GameObject AddDevice(GameObject template,Vector3 world)
        {
            if(!template || !CourseScene.isLoaded || !template.GetComponentsInChildren<MonoBehaviour>(true).Any(c=>c && DeviceTypes.Contains(c.GetType()))) return null;
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
            var copy=PrefabUtility.IsPartOfPrefabAsset(template) ? (GameObject)PrefabUtility.InstantiatePrefab(template,CourseScene) : Instantiate(template);
            copy.name=GameObjectUtility.GetUniqueNameForSibling(null,template.name+"_배치");
            if(copy.scene!=CourseScene) SceneManager.MoveGameObjectToScene(copy,CourseScene);
            copy.transform.position=world; copy.SetActive(true);
            Undo.RegisterCreatedObjectUndo(copy,"Add performance device");
            var layout=Layout(true); Undo.RecordObject(layout,"Add difficulty placement");
            var entry=layout.Track(copy.transform);
            for(int i=0;i<3;i++) { var pose=entry.levels[i]; pose.active=i==difficulty-1; entry.levels[i]=pose; }
            RegisterDevices(copy); EditorUtility.SetDirty(layout); EditorSceneManager.MarkSceneDirty(CourseScene);
            Undo.CollapseUndoOperations(group); SelectDevice(copy.transform); ApplyLayoutPreview(); return copy;
        }
        void RegisterDevices(GameObject copy)
        {
            // Preserve existing ordering: station and hazard messages use indices. Append each new reference once.
            var components=CourseScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c).ToArray();
            Append(typeof(HumanCannonSystem),"stations",copy.GetComponentsInChildren<HumanCannonStation>(true));
            Append(typeof(HumanCannonHazardCourse),"hazards",copy.GetComponentsInChildren<HumanCannonHazard>(true));
            Append(typeof(HumanCannonPoleSystem),"poles",copy.GetComponentsInChildren<HumanCannonPole>(true));
            Append(typeof(HumanCannonRouteSystem),"transports",copy.GetComponentsInChildren<HumanCannonTransport>(true));
            Append(typeof(HumanCannonRouteSystem),"fires",copy.GetComponentsInChildren<HumanCannonFireWall>(true));
            void Append(Type ownerType,string field,Component[] added)
            {
                if(added.Length==0) return;
                var owner=components.FirstOrDefault(c=>c.GetType()==ownerType);
                if(!owner) throw new InvalidOperationException(ownerType.Name+" service is missing from the course.");
                var data=new SerializedObject(owner); var list=data.FindProperty(field);
                foreach(var item in added)
                {
                    bool exists=false; for(int i=0;i<list.arraySize;i++) if(list.GetArrayElementAtIndex(i).objectReferenceValue==item) exists=true;
                    if(!exists) { int index=list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue=item; }
                }
                data.ApplyModifiedProperties();
            }
        }
        void DrawDeviceList()
        {
            GUILayout.Label($"{(trickIndex==0?"외줄타기":"인간 대포")} · 난이도 {difficulty}",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("이 탭의 위치·삭제는 현재 난이도에만 적용됩니다.",MessageType.None);
            if(GUILayout.Button("전체 배치 보기")) FrameDevices();
            if(trickIndex==1)
            {
                deviceTemplate=(GameObject)EditorGUILayout.ObjectField("새 장치 원본",deviceTemplate,typeof(GameObject),true);
                using(new EditorGUI.DisabledScope(!CourseScene.isLoaded || !deviceTemplate))
                    if(GUILayout.Button("원본을 뷰 중심에 추가")) AddDevice(deviceTemplate,pivot);
            }
            else using(new EditorGUI.DisabledScope(!CourseScene.isLoaded))
                if(GUILayout.Button("가로 톱날 추가")) AddSaw(Mathf.Max(0,pivot.z-RopeOrigin.z));
            deviceScroll=EditorGUILayout.BeginScrollView(deviceScroll);
            if(trickIndex==0)
            {
                foreach(var row in RopeMarkers())
                    if(GUILayout.Toggle(ropeSelection==row.id,row.label,"Button")) { ropeSelection=row.id; device=null; planeHeight=row.position.y; }
            }
            foreach(var target in Devices)
                if(GUILayout.Toggle(device==target,(target.gameObject.activeInHierarchy?"":"[제외] ")+target.name,"Button") && device!=target) SelectDevice(target);
            EditorGUILayout.EndScrollView();
        }
        void DrawDeviceDetails()
        {
            detailScroll=EditorGUILayout.BeginScrollView(detailScroll);
            if(trickIndex==0 && !device) DrawRopeDetails();
            else if(device)
            {
                GUILayout.Label(device.name,EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                bool active=EditorGUILayout.Toggle("이 난이도에서 사용",device.gameObject.activeSelf);
                var pos=EditorGUILayout.Vector3Field("월드 위치",device.position);
                var rot=EditorGUILayout.Vector3Field("회전",device.eulerAngles);
                var scale=EditorGUILayout.Vector3Field("크기",device.localScale);
                if(EditorGUI.EndChangeCheck()) SetDevicePose(device,pos,Quaternion.Euler(rot),scale,active);
                if(GUILayout.Button("현재 난이도에 복제")) AddDevice(device.gameObject,device.position+Vector3.right*2);
                if(GUILayout.Button("이 난이도에서 삭제")) SetDevicePose(device,device.position,device.rotation,device.localScale,false);
                if(GUILayout.Button("Scene 뷰에서 이동·회전")) SceneView.lastActiveSceneView?.Frame(new Bounds(device.position,Vector3.one*8),false);
                EditorGUILayout.HelpBox("삭제한 장치는 [제외]로 남습니다. ‘사용’을 켜면 복원됩니다. 연결된 다른 장치의 참조는 유지됩니다.",MessageType.None);
                if(device.GetComponent<HumanCannonStation>()) EditorGUILayout.HelpBox("초록 선은 이 대포의 예상 탄도입니다. 실제 입력·바람·충돌은 플레이 버튼으로 확인하세요.",MessageType.Info);
                if(GUILayout.Button("원본 동작 설정 (공통) 열기")) Selection.activeGameObject=device.gameObject;
            }
            else EditorGUILayout.HelpBox("목록이나 지도에서 장치를 고르세요. 기존 장치 복제 또는 프리팹을 추가할 수 있습니다.",MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        void EnsureRopePlacement()
        {
            Undo.RecordObject(GameSettings.Tightrope,"Edit tightrope difficulty");
            GameSettings.Tightrope.CreatePlacement(difficulty); EditorUtility.SetDirty(GameSettings.Tightrope);
        }
        public void AddSaw(float distance)
        {
            EnsureRopePlacement(); var settings=RopeSaws;
            settings.PreserveBaseCount();
            int length=settings.horizontalSaws.Length; Array.Resize(ref settings.horizontalSaws,length+1);
            settings.horizontalSaws[length]=new HorizontalSawEntry(distance,SawStartSide.Left); ropeSelection=length;
            // New entries must be started by the same sequence as the former last entry.
            foreach(var step in settings.steps) if(step.lastSaw==length) step.lastSaw=length+1;
        }
        void DrawRopeDetails()
        {
            GUILayout.Label("난이도별 외줄 배치",EditorStyles.boldLabel);
            if(GameSettings.Tightrope.PlacementFor(difficulty)==null)
            {
                EditorGUILayout.HelpBox("기존 코스를 사용 중입니다. 편집하면 이 난이도의 코스와 톱날만 독립됩니다.",MessageType.Info);
                if(GUILayout.Button("이 난이도 배치 편집 시작")) EnsureRopePlacement();
                return;
            }
            var data=new SerializedObject(GameSettings.Tightrope); data.Update();
            var root=data.FindProperty("placementStages").GetArrayElementAtIndex(difficulty-1);
            if(ropeSelection>=0 && ropeSelection<RopeSaws.horizontalSaws.Length)
            {
                var array=root.FindPropertyRelative("saws.horizontalSaws");
                EditorGUILayout.PropertyField(array.GetArrayElementAtIndex(ropeSelection),new GUIContent("선택 톱날"),true);
                if(GUILayout.Button("선택 톱날 삭제")) { array.GetArrayElementAtIndex(ropeSelection).FindPropertyRelative("excluded").boolValue=true; ropeSelection=-1; }
            }
            EditorGUILayout.PropertyField(root.FindPropertyRelative("course"),new GUIContent("줄 · 도착대 · 봉 구간"),true);
            EditorGUILayout.PropertyField(root.FindPropertyRelative("saws"),new GUIContent("톱날 배치 · 작동 순서"),true);
            if(data.ApplyModifiedProperties()) { ropePreviewKey=null; Repaint(); }
            EditorGUILayout.HelpBox("지도 표시는 배치 확인용입니다. 시간에 따라 움직이는 장치와 협동 구간은 실제 플레이로 확인하세요.",MessageType.None);
        }
        IEnumerable<(int id,string label,Vector3 position)> RopeMarkers()
        {
            var c=GameSettings.Tightrope.CourseFor(difficulty); var origin=RopeOrigin;
            yield return (-2,"도착 지점",origin+Vector3.forward*c.FinishDistance);
            yield return (-3,"공중 봉 구간",origin+Vector3.forward*c.TrapezeStart);
            yield return (-4,"봉 교대 관문",origin+Vector3.forward*c.SwingGapStart);
            var saws=RopeSaws.horizontalSaws;
            for(int i=0;i<saws.Length;i++) if(!saws[i].excluded) yield return (i,$"톱날 {i+1} · {saws[i].distance:0.#}m",origin+Vector3.forward*saws[i].distance);
        }
        void MoveDevice(Vector3 world)
        {
            if(device) SetDevicePose(device,world,device.rotation,device.localScale,device.gameObject.activeSelf);
            else if(trickIndex==0 && ropeSelection!=-1)
            {
                EnsureRopePlacement(); float d=Mathf.Max(0,world.z-RopeOrigin.z);
                if(ropeSelection>=0) { var e=RopeSaws.horizontalSaws[ropeSelection]; e.distance=d; RopeSaws.horizontalSaws[ropeSelection]=e; }
                else
                {
                    var data=new SerializedObject(GameSettings.Tightrope);
                    var course=data.FindProperty("placementStages").GetArrayElementAtIndex(difficulty-1).FindPropertyRelative("course");
                    course.FindPropertyRelative(ropeSelection==-2?"finishDistance":ropeSelection==-3?"trapezeStart":"swingGapStart").floatValue=d;
                    data.ApplyModifiedProperties();
                }
                ropePreviewKey=null;
            }
        }
        void FrameDevice()
        {
            if(device) pivot=device.position;
            else if(trickIndex==0 && ropeSelection!=-1) pivot=RopeMarkers().First(r=>r.id==ropeSelection).position;
            distance=20; pitch=25;
        }
        void FrameDevices()
        {
            var positions=Devices.Select(t=>t.position).Concat(trickIndex==0?RopeMarkers().Select(r=>r.position):Array.Empty<Vector3>()).ToArray();
            if(positions.Length==0) return;
            var bounds=new Bounds(positions[0],Vector3.one*10); foreach(var p in positions) bounds.Encapsulate(p);
            pivot=bounds.center; distance=Mathf.Max(20,bounds.size.magnitude*.8f); pitch=40; yaw=15;
        }
        void DrawDeviceOverlay()
        {
            if(trickIndex==0) foreach(var row in RopeMarkers()) Marker(row.position,row.label,ropeSelection==row.id,Color.yellow);
            foreach(var target in Devices) Marker(target.position,target.name,device==target,target.gameObject.activeInHierarchy?Color.cyan:Color.gray);
            void Marker(Vector3 world,string label,bool chosen,Color color)
            {
                var v=previewCamera.WorldToViewportPoint(world); if(v.z<=0 || v.x<0 || v.x>1 || v.y<0 || v.y>1) return;
                var p=Project(world); Handles.color=chosen?Color.white:color; Handles.DrawWireDisc(p,Vector3.forward,chosen?13:7);
                if(chosen || Vector2.Distance(p,Event.current.mousePosition)<18) GUI.Label(new Rect(p.x+10,p.y-10,230,20),label,EditorStyles.whiteMiniLabel);
            }
        }
        void SelectDeviceAt(Vector2 mouse)
        {
            float best=22; Transform found=null; int saw=-1;
            foreach(var target in Devices) if(previewCamera.WorldToViewportPoint(target.position).z>0)
            { float d=Vector2.Distance(Project(target.position),mouse); if(d<best) {best=d;found=target;} }
            if(trickIndex==0) foreach(var row in RopeMarkers()) if(previewCamera.WorldToViewportPoint(row.position).z>0)
            { float d=Vector2.Distance(Project(row.position),mouse); if(d<best) {best=d;found=null;saw=row.id;} }
            if(found) SelectDevice(found);
            else if(saw!=-1) {device=null;ropeSelection=saw;planeHeight=RopeOrigin.y;}
        }
        void DrawDeviceHandles()
        {
            if(!device && ropeSelection==-1) return;
            Vector3 p=device?device.position:RopeMarkers().First(r=>r.id==ropeSelection).position;
            EditorGUI.BeginChangeCheck(); var next=Handles.PositionHandle(p,Quaternion.identity);
            if(EditorGUI.EndChangeCheck()) MoveDevice(next);
            if(device && Tools.current==Tool.Rotate)
            {
                EditorGUI.BeginChangeCheck(); var r=Handles.RotationHandle(device.rotation,device.position);
                if(EditorGUI.EndChangeCheck()) SetDevicePose(device,device.position,r,device.localScale,device.gameObject.activeSelf);
            }
        }
        void ClearRopePreview()
        {
            if(ropePreview) DestroyImmediate(ropePreview); if(diagramMaterial) DestroyImmediate(diagramMaterial);
            ropePreview=null; diagramMaterial=null; ropePreviewKey=null;
        }
        void UpdateRopePreview()
        {
            if(trickIndex!=0) { ClearRopePreview(); return; }
            var c=GameSettings.Tightrope.CourseFor(difficulty);
            string key=c.ToJson()+JsonUtility.ToJson(RopeSaws)+RopeOrigin;
            if(ropePreview && ropePreviewKey==key) return;
            ClearRopePreview(); ropePreviewKey=key;
            ropePreview=new GameObject("TightropePlacementDiagram") { hideFlags=HideFlags.HideAndDontSave };
            diagramMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")) {hideFlags=HideFlags.HideAndDontSave};
            diagramMaterial.color=new Color(.38f,.63f,.58f);
            if(RopeCourse) RopeCourse.ApplyStage(difficulty);
            for(int lane=0;lane<c.LaneCount;lane++)
            {
                float x=(lane-(c.LaneCount-1)*.5f)*c.LaneSpacing;
                IEnumerable<Vector2> spans=RopeCourse ? RopeCourse.GetUsableSpans(lane) : new[]{new Vector2(0,c.FinishDistance)};
                foreach(var span in spans) Part(PrimitiveType.Cube,new Vector3(x,0,(span.x+span.y)*.5f),new Vector3(.1f,.1f,span.y-span.x));
            }
            float width=c.LaneCount*c.LaneSpacing;
            Part(PrimitiveType.Cube,new Vector3(0,-.25f,-2),new Vector3(width,.5f,4));
            Part(PrimitiveType.Cube,new Vector3(0,-.25f,c.FinishDistance+2),new Vector3(width,.5f,4));
            foreach(var row in RopeMarkers()) if(row.id>=0)
                Part(PrimitiveType.Cylinder,row.position-RopeOrigin+Vector3.up*.3f,new Vector3(RopeSaws.horizontalVisualDiameter,.06f,RopeSaws.horizontalVisualDiameter));
            void Part(PrimitiveType type,Vector3 pos,Vector3 size)
            {
                var go=GameObject.CreatePrimitive(type); go.hideFlags=HideFlags.HideAndDontSave;
                go.transform.SetParent(ropePreview.transform); go.transform.position=RopeOrigin+pos; go.transform.localScale=size;
                DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial=diagramMaterial;
            }
        }
    }
}
