using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    public sealed partial class PerformanceCheerWindow : EditorWindow
    {
        [SerializeField] int trickIndex, difficulty=1;
        [SerializeField] bool equipment, placing, paths=true;
        [SerializeField] float planeHeight;
        [SerializeField] Vector3 pivot;
        [SerializeField] float yaw=15,pitch=18,distance=24;
        PerformanceCheerPoint selected;
        readonly Dictionary<PerformanceCheerPoint,CheerPointPresentation> previews=new();
        PerformanceCheerPoint[] points=Array.Empty<PerformanceCheerPoint>();
        CheerPointStyle previewStyle;
        Camera previewCamera;
        RenderTexture texture;
        Vector2 scroll;
        Rect previewRect;
        bool dragging;
        int dragGroup;
        double nextFrame,collectStarted=-1;
        string Trick => trickIndex==0 ? "Tightrope" : "HumanCannon";
        string ScenePath => trickIndex==0 ? PerformanceEquipmentAuthoring.RopeScene : PerformanceEquipmentAuthoring.CannonScene;
        Scene CourseScene => SceneManager.GetSceneByPath(ScenePath);
        PerformanceEquipmentSettings Settings => GameSettings.PerformanceEquipment;
        public PerformanceCheerPoint Selected => selected;

        [MenuItem("CurtainCall/Performance/Cheer Point Placement")]
        [MenuItem("Tools/CurtainCall/공연/환호 포인트 배치")]
        [MenuItem("Tools/CurtainCall/공연/공연 배치")]
        public static PerformanceCheerWindow Open()
        {
            var window=GetWindow<PerformanceCheerWindow>("공연 배치");
            window.minSize=new Vector2(1060,660); window.Show(); return window;
        }
        void OnEnable()
        {
            EditorApplication.update+=Tick; SceneView.duringSceneGui+=DrawScene;
            EditorApplication.playModeStateChanged+=PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload+=Cleanup;
            Undo.undoRedoPerformed+=Refresh; Selection.selectionChanged+=SelectionChanged;
            EditorSceneManager.sceneSaving+=BeforeSave; EditorSceneManager.sceneSaved+=AfterSave;
        }
        void OnDisable()
        {
            EditorApplication.update-=Tick; SceneView.duringSceneGui-=DrawScene;
            EditorApplication.playModeStateChanged-=PlayChanged;
            AssemblyReloadEvents.beforeAssemblyReload-=Cleanup;
            Undo.undoRedoPerformed-=Refresh; Selection.selectionChanged-=SelectionChanged;
            EditorSceneManager.sceneSaving-=BeforeSave; EditorSceneManager.sceneSaved-=AfterSave;
            Cleanup();
        }
        void PlayChanged(PlayModeStateChange _) => Cleanup();
        void SelectionChanged()
        {
            var point=Selection.activeGameObject ? Selection.activeGameObject.GetComponent<PerformanceCheerPoint>() : null;
            if(point && point.trickId==Trick) { selected=point; collectStarted=-1; Repaint(); }
        }
        void Refresh() { collectStarted=-1; Repaint(); SceneView.RepaintAll(); }
        void Cleanup()
        {
            RestoreLayoutPreview(); ClearRopePreview();
            foreach(var view in previews.Values) if(view) DestroyImmediate(view.gameObject);
            previews.Clear();
            if(previewCamera) DestroyImmediate(previewCamera.gameObject);
            if(texture) { texture.Release(); DestroyImmediate(texture); }
            texture=null; previewCamera=null;
        }
        public void SetContext(string trick,int level)
        {
            RestoreLayoutPreview(); device=null; ropeSelection=-1;
            trickIndex=trick=="HumanCannon" ? 1 : 0; difficulty=Mathf.Clamp(level,1,3);
            selected=null; Cleanup(); Refresh();
        }
        public void OpenCourse()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!CourseScene.isLoaded)
            {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Cleanup(); EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            }
            SceneManager.SetActiveScene(CourseScene);
            FindPoints(); FrameAll(); SceneView.RepaintAll();
        }
        void FindPoints()
        {
            points=CourseScene.isLoaded ? CourseScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PerformanceCheerPoint>(true))
                .Where(p=>p.trickId==Trick && p.AppliesToDifficulty(difficulty)).OrderBy(p=>p.name).ToArray() : Array.Empty<PerformanceCheerPoint>();
        }
        void Tick()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<nextFrame) return;
            nextFrame=EditorApplication.timeSinceStartup+1d/30;
            FindPoints();
            if(previewStyle!=Settings.cheerStyle) { Cleanup(); previewStyle=Settings.cheerStyle; }
            foreach(var point in previews.Keys.ToArray()) if(!point || !points.Contains(point))
            { if(previews[point]) DestroyImmediate(previews[point].gameObject); previews.Remove(point); }
            if(!CourseScene.isLoaded) return;
            ApplyLayoutPreview();
            EnsureCamera();
            UpdateRopePreview();
            foreach(var point in points)
            {
                if(!previews.TryGetValue(point,out var view) || !view)
                {
                    var prefab=Settings.Visual(Settings.cheerStyle); if(!prefab) continue;
                    var instance=Instantiate(prefab,point.transform,false); instance.name="CheerPlacementPreview";
                    foreach(var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags=HideFlags.HideAndDontSave;
                    view=instance.AddComponent<CheerPointPresentation>(); previews[point]=view;
                }
                bool collect=point==selected && collectStarted>=0;
                float time=(float)(collect ? EditorApplication.timeSinceStartup-collectStarted : EditorApplication.timeSinceStartup);
                view.Sample(time,collect,previewCamera);
                if(view.gameObject.activeSelf) foreach(var particle in view.GetComponentsInChildren<ParticleSystem>())
                    particle.Simulate(2f+time%8f,false,true,true);
            }
            previewCamera.Render(); Repaint(); SceneView.RepaintAll();
        }
        void EnsureCamera()
        {
            if(!previewCamera)
            {
                var go=new GameObject("CheerPlacementCamera") { hideFlags=HideFlags.HideAndDontSave };
                previewCamera=go.AddComponent<Camera>(); previewCamera.enabled=false;
                previewCamera.clearFlags=CameraClearFlags.Skybox; previewCamera.nearClipPlane=.1f; previewCamera.farClipPlane=800;
            }
            int width=Mathf.Clamp((int)previewRect.width,320,1600),height=Mathf.Clamp((int)previewRect.height,240,1000);
            if(!texture || texture.width!=width || texture.height!=height)
            {
                if(texture) { texture.Release(); DestroyImmediate(texture); }
                texture=new RenderTexture(width,height,24) { hideFlags=HideFlags.HideAndDontSave }; texture.Create();
                previewCamera.targetTexture=texture;
            }
            previewCamera.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            previewCamera.transform.position=pivot-previewCamera.transform.forward*distance;
        }
        void OnGUI()
        {
            // A scene can unload between the throttled preview tick and this repaint.
            FindPoints();
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                int trick=GUILayout.Toolbar(trickIndex,new[]{"외줄타기","인간 대포"},EditorStyles.toolbarButton,GUILayout.Width(230));
                int level=GUILayout.Toolbar(difficulty-1,new[]{"난이도 1","난이도 2","난이도 3"},EditorStyles.toolbarButton,GUILayout.Width(240))+1;
                if(EditorGUI.EndChangeCheck()) SetContext(trick==0 ? "Tightrope" : "HumanCannon",level);
                GUILayout.FlexibleSpace();
                if(GUILayout.Button("공연 씬 열기",EditorStyles.toolbarButton)) OpenCourse();
                using(new EditorGUI.DisabledScope(!CourseScene.isLoaded || EditorApplication.isPlaying))
                    if(GUILayout.Button("배치 저장",EditorStyles.toolbarButton)) { EditorSceneManager.SaveScene(CourseScene); AssetDatabase.SaveAssetIfDirty(Settings); AssetDatabase.SaveAssetIfDirty(GameSettings.Tightrope); }
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("실제 공연 테스트 중입니다. Play를 종료하면 편집으로 돌아옵니다.",MessageType.Info); return; }
            editMode=GUILayout.Toolbar(editMode,new[]{"환호 포인트", "공연 장치 · 장애물 · 착지 지점"});
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(220))) DrawList();
                using(new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true))) DrawPreview();
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(270))) DrawDetails();
            }
        }
        void DrawList()
        {
            if(editMode==1) { DrawDeviceList(); return; }
            GUILayout.Label($"{(trickIndex==0 ? "외줄타기" : "인간 대포")} · 난이도 {difficulty}",EditorStyles.boldLabel);
            GUILayout.Label($"배치 {points.Length}개  /  주황색: 기물 경로");
            using(new EditorGUI.DisabledScope(!CourseScene.isLoaded))
            {
                equipment=EditorGUILayout.Toggle("새 포인트: 기물 경로",equipment);
                planeHeight=EditorGUILayout.FloatField("클릭 배치 높이",planeHeight);
                placing=GUILayout.Toggle(placing,"클릭해서 추가", "Button");
                if(GUILayout.Button("뷰 중심에 추가")) AddAt(pivot);
                if(GUILayout.Button("전체 배치 보기")) FrameAll();
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(var point in points)
            {
                string shared=(point.difficultyMask & (point.difficultyMask-1))!=0 ? " [공유]" : "";
                if(GUILayout.Toggle(point==selected,point.name+shared,"Button"))
                    if(selected!=point) Select(point);
            }
            EditorGUILayout.EndScrollView();
        }
        void DrawPreview()
        {
            previewRect=GUILayoutUtility.GetRect(320,400,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(previewRect,new Color(.08f,.09f,.11f));
            if(texture) GUI.DrawTexture(previewRect,texture,ScaleMode.StretchToFill,false);
            if(!CourseScene.isLoaded) GUI.Label(previewRect,"상단의 ‘공연 씬 열기’를 누르세요.",EditorStyles.centeredGreyMiniLabel);
            else { DrawOverlay(); PreviewInput(); }
            GUILayout.Label("우클릭 회전 · 휠 확대 · 가운데 버튼 이동 · 표시 클릭 선택/드래그 이동",EditorStyles.miniLabel);
            paths=GUILayout.Toggle(paths,"예상 이동 경로 보기 (바람·입력·충돌 제외 참고선)");
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUI.DisabledScope(editMode==0 ? !selected : !device && ropeSelection==-1))
                {
                    if(GUILayout.Button("선택 대상 확대")) { if(editMode==0) FrameSelected(); else FrameDevice(); }
                    if(editMode==0 && GUILayout.Button(collectStarted<0 ? "획득 연출 재생" : "연출 되돌리기")) collectStarted=collectStarted<0 ? EditorApplication.timeSinceStartup : -1;
                }
                using(new EditorGUI.DisabledScope(!CourseScene.isLoaded))
                    if(GUILayout.Button("이 공연·난이도로 플레이")) CheerPlacementPlayPreview.Start(Trick,difficulty);
            }
        }
        void DrawDetails()
        {
            if(editMode==1) { DrawDeviceDetails(); return; }
            GUILayout.Label("공통 설정",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool enabled=EditorGUILayout.Toggle("환호 포인트 사용",Settings.cheerPointsEnabled);
            var style=(CheerPointStyle)EditorGUILayout.EnumPopup("외형",Settings.cheerStyle);
            if(EditorGUI.EndChangeCheck()) { Undo.RecordObject(Settings,"Cheer settings"); Settings.cheerPointsEnabled=enabled; Settings.cheerStyle=style; EditorUtility.SetDirty(Settings); }
            if(!enabled) EditorGUILayout.HelpBox("현재 게임 OFF. 편집 미리보기는 배치를 볼 수 있도록 계속 표시합니다.",MessageType.Info);
            if(GUILayout.Button("줄·회수 연출 설정 열기")) Selection.activeObject=Settings;
            GUILayout.Space(14); GUILayout.Label("선택 포인트",EditorStyles.boldLabel);
            if(!selected) { EditorGUILayout.HelpBox("목록이나 미리보기의 고리를 선택하세요.",MessageType.Info); return; }
            bool shared=(selected.difficultyMask & (selected.difficultyMask-1))!=0;
            if(shared)
            {
                EditorGUILayout.HelpBox("여러 난이도가 공유하는 배치입니다. 이 난이도만 옮기려면 먼저 분리하세요.",MessageType.Info);
                if(GUILayout.Button("현재 난이도 전용으로 분리")) SplitSelected();
            }
            var data=new SerializedObject(selected); data.Update();
            EditorGUI.BeginChangeCheck();
            string label=EditorGUILayout.TextField("이름",selected.name);
            if(EditorGUI.EndChangeCheck()) { Undo.RecordObject(selected.gameObject,"Rename cheer point"); selected.name=label; }
            EditorGUILayout.PropertyField(data.FindProperty("equipmentRoute"),new GUIContent("기물 활용 경로"));
            var mask=data.FindProperty("difficultyMask");
            mask.intValue=EditorGUILayout.MaskField("표시할 난이도",mask.intValue,new[]{"1","2","3"}) & 7;
            if(data.ApplyModifiedProperties()) EditorSceneManager.MarkSceneDirty(CourseScene);
            EditorGUI.BeginChangeCheck();
            var position=EditorGUILayout.Vector3Field("월드 위치",selected.transform.position);
            var rotation=EditorGUILayout.Vector3Field("회전",selected.transform.eulerAngles);
            if(EditorGUI.EndChangeCheck()) { Undo.RecordObject(selected.transform,"Move cheer point"); selected.transform.SetPositionAndRotation(position,Quaternion.Euler(rotation)); EditorSceneManager.MarkSceneDirty(CourseScene); }
            EditorGUI.BeginChangeCheck();
            float size=EditorGUILayout.FloatField("링 크기 배율",selected.transform.localScale.x);
            if(EditorGUI.EndChangeCheck()) SetSelectedScale(size);
            EditorGUILayout.LabelField("통과 지름",$"{Settings.hoopRadius*2*selected.transform.lossyScale.x:0.##} m");
            if(GUILayout.Button("현재 난이도에 복제")) DuplicateSelected();
            if(GUILayout.Button("이 난이도에서 삭제")) RemoveSelected();
            if(GUILayout.Button("Scene 뷰에서 편집 (W/E/F)")) { Selection.activeGameObject=selected.gameObject; SceneView.lastActiveSceneView?.Frame(new Bounds(selected.transform.position,Vector3.one*6),false); }
            EditorGUILayout.HelpBox("파란 방향이 고리 통과 방향입니다. 배치는 Undo/Redo를 지원하며 ‘배치 저장’으로 씬에 저장합니다.",MessageType.None);
        }
        void Select(PerformanceCheerPoint point) { selected=point; collectStarted=-1; Selection.activeGameObject=point.gameObject; planeHeight=point.transform.position.y; Repaint(); }
        public void SetSelectedScale(float size)
        {
            if(!selected || !float.IsFinite(size)) return;
            Undo.RecordObject(selected.transform,"Resize cheer ring");
            selected.transform.localScale=Vector3.one*Mathf.Max(.1f,size);
            EditorSceneManager.MarkSceneDirty(CourseScene); Refresh();
        }
        public PerformanceCheerPoint AddAt(Vector3 position)
        {
            if(!CourseScene.isLoaded) return null;
            var parent=CourseScene.GetRootGameObjects().FirstOrDefault(g=>g.name=="PerformanceCheerPoints");
            if(!parent) { parent=new GameObject("PerformanceCheerPoints"); SceneManager.MoveGameObjectToScene(parent,CourseScene); Undo.RegisterCreatedObjectUndo(parent,"Create cheer root"); }
            var point=PerformanceEquipmentAuthoring.AddPoint(parent.transform,Trick,parent.transform.childCount,position,Quaternion.Euler(0,yaw,0),equipment);
            point.difficultyMask=1 << (difficulty-1); Undo.RegisterCreatedObjectUndo(point.gameObject,"Add cheer point");
            EditorSceneManager.MarkSceneDirty(CourseScene); Select(point); FindPoints(); return point;
        }
        public PerformanceCheerPoint DuplicateSelected()
        {
            if(!selected) return null;
            var original=selected; var copy=AddAt(original.transform.position+Vector3.right*2);
            copy.transform.rotation=original.transform.rotation; copy.equipmentRoute=original.equipmentRoute;
            copy.transform.localScale=original.transform.localScale;
            return copy;
        }
        public void SplitSelected()
        {
            if(!selected) return;
            var original=selected; int bit=1 << (difficulty-1);
            if((original.difficultyMask & ~bit)==0) return;
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
            var copy=DuplicateSelected(); copy.transform.position=original.transform.position;
            Undo.RecordObject(original,"Split difficulty"); original.difficultyMask &= ~bit;
            Undo.CollapseUndoOperations(group); Refresh();
        }
        public void RemoveSelected()
        {
            if(!selected) return;
            Undo.RecordObject(selected,"Remove cheer point from difficulty");
            selected.difficultyMask &= ~(1 << (difficulty-1));
            if(selected.difficultyMask==0) Undo.DestroyObjectImmediate(selected.gameObject);
            selected=null; EditorSceneManager.MarkSceneDirty(CourseScene); FindPoints(); Refresh();
        }
        void FrameSelected() { if(selected) { pivot=selected.transform.position; distance=Mathf.Max(4,11*selected.transform.lossyScale.x); pitch=8; yaw=selected.transform.eulerAngles.y; } }
        void FrameAll()
        {
            if(editMode==1) { FrameDevices(); return; }
            if(points.Length==0) return;
            var bounds=new Bounds(points[0].transform.position,Vector3.one*5); foreach(var p in points) bounds.Encapsulate(p.transform.position);
            pivot=bounds.center; distance=Mathf.Max(12,bounds.size.magnitude*.85f); pitch=25;
        }
        Vector2 Project(Vector3 world)
        {
            var v=previewCamera.WorldToViewportPoint(world);
            return new Vector2(previewRect.x+v.x*previewRect.width,previewRect.y+(1-v.y)*previewRect.height);
        }
        void DrawOverlay()
        {
            if(!previewCamera) return;
            Handles.BeginGUI();
            if(paths) DrawPaths();
            if(editMode==1) { DrawDeviceOverlay(); Handles.EndGUI(); return; }
            foreach(var point in points)
            {
                var v=previewCamera.WorldToViewportPoint(point.transform.position);
                if(v.z<=0 || v.x<0 || v.x>1 || v.y<0 || v.y>1) continue;
                var p=Project(point.transform.position); Handles.color=point==selected ? Color.white : point.equipmentRoute ? new Color(1,.55f,.1f) : Color.cyan;
                Handles.DrawWireDisc(p,Vector3.forward,point==selected ? 13 : 9);
                GUI.Label(new Rect(p.x+12,p.y-10,200,20),point.name,EditorStyles.whiteMiniLabel);
            }
            Handles.EndGUI();
        }
        void DrawPaths()
        {
            Handles.color=new Color(.3f,1,.7f,.8f);
            var roots=CourseScene.GetRootGameObjects();
            if(trickIndex==1)
                foreach(var cannon in roots.SelectMany(r=>r.GetComponentsInChildren<HumanCannonStation>(true)))
                {
                    if(!cannon.gameObject.activeInHierarchy || editMode==1 && device && device.GetComponent<HumanCannonStation>() && cannon.transform!=device) continue;
                    Vector3 last=cannon.Muzzle.position;
                    for(int i=1;i<=60;i++)
                    {
                        float t=i*.1f; Vector3 next=cannon.Muzzle.position+cannon.LaunchVelocity*t+Vector3.down*(.5f*GameSettings.Base.Character.Gravity*t*t);
                        DrawProjectedLine(last,next); last=next;
                    }
                }
            else
                foreach(var course in roots.SelectMany(r=>r.GetComponentsInChildren<TightropeCourse>(true)))
                    for(int lane=0;lane<course.LaneCount;lane++) DrawProjectedLine(course.GetLanePosition(lane,0),course.GetLanePosition(lane,course.FinishDistance));
        }
        void DrawProjectedLine(Vector3 a,Vector3 b)
        {
            var av=previewCamera.WorldToViewportPoint(a); var bv=previewCamera.WorldToViewportPoint(b);
            if(av.z<=0 || bv.z<=0 || av.x<0 || av.x>1 || av.y<0 || av.y>1 || bv.x<0 || bv.x>1 || bv.y<0 || bv.y>1) return;
            Handles.DrawAAPolyLine(2,Project(a),Project(b));
        }
        void PreviewInput()
        {
            var e=Event.current; if(!previewCamera || (!previewRect.Contains(e.mousePosition) && !dragging)) return;
            if(e.type==EventType.ScrollWheel) { distance=Mathf.Clamp(distance*Mathf.Exp(e.delta.y*.07f),2,500); e.Use(); }
            else if(e.type==EventType.MouseDrag && (e.button==1 || e.alt)) { yaw+=e.delta.x*.4f; pitch=Mathf.Clamp(pitch+e.delta.y*.3f,-80,80); e.Use(); }
            else if(e.type==EventType.MouseDrag && e.button==2) { pivot+=(-previewCamera.transform.right*e.delta.x+previewCamera.transform.up*e.delta.y)*distance*.0015f; e.Use(); }
            else if(e.type==EventType.MouseDown && e.button==0 && !e.alt)
            {
                if(editMode==1) { SelectDeviceAt(e.mousePosition); dragging=true; Undo.IncrementCurrentGroup(); dragGroup=Undo.GetCurrentGroup(); e.Use(); return; }
                if(placing) { if(TryPlane(e.mousePosition,out var p)) AddAt(p); e.Use(); return; }
                var hit=points.Where(p=>previewCamera.WorldToViewportPoint(p.transform.position).z>0).OrderBy(p=>Vector2.Distance(Project(p.transform.position),e.mousePosition)).FirstOrDefault();
                if(hit && Vector2.Distance(Project(hit.transform.position),e.mousePosition)<24)
                {
                    Select(hit); if(e.clickCount==2) FrameSelected();
                    Undo.IncrementCurrentGroup(); dragGroup=Undo.GetCurrentGroup(); dragging=true; e.Use();
                }
            }
            else if(e.type==EventType.MouseDrag && e.button==0 && dragging && (selected || editMode==1))
            {
                if(editMode==1) { if(TryPlane(e.mousePosition,out var p)) MoveDevice(p); e.Use(); return; }
                if(TryPlane(e.mousePosition,out var position)) { Undo.RecordObject(selected.transform,"Drag cheer point"); selected.transform.position=position; EditorSceneManager.MarkSceneDirty(CourseScene); }
                e.Use();
            }
            else if(e.type==EventType.MouseUp && dragging) { dragging=false; Undo.CollapseUndoOperations(dragGroup); e.Use(); }
        }
        bool TryPlane(Vector2 mouse,out Vector3 position)
        {
            var ray=previewCamera.ViewportPointToRay(new Vector3((mouse.x-previewRect.x)/previewRect.width,1-(mouse.y-previewRect.y)/previewRect.height,0));
            bool hit=new Plane(Vector3.up,new Vector3(0,planeHeight,0)).Raycast(ray,out float t);
            position=hit ? ray.GetPoint(t) : default; return hit;
        }
        void DrawScene(SceneView view)
        {
            if(EditorApplication.isPlaying) return;
            if(editMode==1) { DrawDeviceHandles(); return; }
            foreach(var point in points) if(point)
            {
                Handles.color=point.equipmentRoute ? new Color(1,.55f,.1f) : Color.cyan;
                using(new Handles.DrawingScope(point.transform.localToWorldMatrix))
                    Handles.DrawWireDisc(Vector3.zero,Vector3.forward,Settings.hoopRadius);
                if(Handles.Button(point.transform.position,Quaternion.identity,.12f,.25f,Handles.SphereHandleCap)) Select(point);
            }
        }
    }

    [InitializeOnLoad]
    static class CheerPlacementPlayPreview
    {
        const string Key="CurtainCall.CheerPlacement.Play";
        static CheerPlacementPlayPreview() => EditorApplication.playModeStateChanged+=Changed;
        public static void Start(string trick,int difficulty)
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool(Key+".dev",DevMode.Enabled);
            SessionState.SetString(Key,trick); SessionState.SetInt(Key+".difficulty",difficulty);
            DevMode.SetEditorEnabled(true); EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            string trick=SessionState.GetString(Key,""); if(string.IsNullOrEmpty(trick)) return;
            if(state==PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall+=()=>
            {
                var launcher=DevStartLauncher.EnsureExists();
                if(!launcher || !launcher.TryLaunch(new RunStartPoint(1,RunStartPhase.Performance,0,trick,SessionState.GetInt(Key+".difficulty",1))))
                    Debug.LogWarning("환호 배치 테스트 시작 실패: "+launcher?.Status);
            };
            else if(state==PlayModeStateChange.EnteredEditMode)
            { DevMode.SetEditorEnabled(SessionState.GetBool(Key+".dev",false)); SessionState.EraseString(Key); }
        }
    }
}
