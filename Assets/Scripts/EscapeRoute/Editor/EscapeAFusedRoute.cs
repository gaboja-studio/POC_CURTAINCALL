using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string AFusedGroup = "34_AFusedRoute";

        [MenuItem("Tools/CurtainCall/탈출로/A 연속 묘기와 순환 그네 연결")]
        public static void BuildAContinuousTraversal()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("Saved A edit scene required");
            var bg = GameObject.Find(RootName).transform;
            if (bg.Find(AFusedGroup) != null) throw new InvalidOperationException("Already installed; edit the saved traversal");
            CompletionMaterials();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A continuous stunt route");
            var root = VerticalChild(bg, AFusedGroup);
            root.localScale = new Vector3(1 / bg.lossyScale.x, 1 / bg.lossyScale.y, 1 / bg.lossyScale.z);
            var course = UnityEngine.Object.FindAnyObjectByType<TightropeCourse>();
            var oldRopes = new SerializedObject(course).FindProperty("sceneRopes");
            var ropes = new List<BoxCollider>();
            for (int i = 0; i < oldRopes.arraySize; i++)
            {
                var rope = oldRopes.GetArrayElementAtIndex(i).objectReferenceValue as BoxCollider;
                if (i == 0) { Undo.RecordObject(rope, "Replace A03 landing wire"); rope.enabled = false; }
                else ropes.Add(rope);
            }
            var preview = EditorSceneManager.OpenPreviewScene(OriginalCannonPath);
            try
            {
                var source = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HumanCannonPole>(true)).First();
                BuildAFusedDescent(root, bg, source, ropes);
                BuildAConveyorHall(root, bg, source, ropes);
                BuildAFusionCoopAndAscent(root, bg, source, ropes);
                RefineAFinaleGripSpacing(root, bg);
                OpenAFusedWirePassages(root, bg);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            foreach (var rope in ropes) Ensure<HumanCannonLandingZone>(rope.gameObject);
            var cd = new SerializedObject(course);
            SetFinaleArray(cd, "sceneRopes", ropes.ToArray());
            cd.FindProperty("independentSceneRopes").boolValue = true;
            var shape = cd.FindProperty("sceneShape");
            shape.FindPropertyRelative("laneCount").intValue = ropes.Count;
            shape.FindPropertyRelative("finishDistance").floatValue = ropes.Max(r => r.size.z);
            var ends = shape.FindPropertyRelative("laneEndDistances"); ends.arraySize = ropes.Count;
            for (int i = 0; i < ropes.Count; i++) ends.GetArrayElementAtIndex(i).floatValue = ropes[i].size.z;
            cd.ApplyModifiedProperties(); course.Rebuild();
            var sw = new SerializedObject(course.GetComponent<CourseControlSwitcher>());
            sw.FindProperty("allowCannonPoleExit").boolValue = true;
            sw.FindProperty("cannonPoleExitDistance").floatValue = 2;
            sw.ApplyModifiedProperties();
            var system = UnityEngine.Object.FindAnyObjectByType<HumanCannonSystem>();
            var data = new SerializedObject(system);
            SetFinaleArray(data, "stations", UnityEngine.Object.FindObjectsByType<HumanCannonStation>(FindObjectsSortMode.None)
                .Where(s => s.gameObject.scene == scene).OrderBy(s => s.name, StringComparer.Ordinal).ToArray());
            data.ApplyModifiedProperties(); RegisterAllPoles();
            var hazardCourse = UnityEngine.Object.FindAnyObjectByType<HumanCannonHazardCourse>();
            data = new SerializedObject(hazardCourse);
            SetFinaleArray(data, "hazards", UnityEngine.Object.FindObjectsByType<HumanCannonHazard>(FindObjectsSortMode.None)
                .Where(h => h.gameObject.scene == scene).OrderBy(h => h.name, StringComparer.Ordinal).ToArray());
            data.ApplyModifiedProperties();
            CompleteEscapePresentation(); Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
        }

        static void AFloor(Transform root, string name, float x1, float x2, float z1, float z2, float y)
        {
            BFloor(root, name, x1, x2, z1, z2, y, .8f);
            Ensure<HumanCannonLandingZone>(root.Find(name).gameObject);
        }

        static HumanCannonPole AFusedPole(Transform root, HumanCannonPole source, string name,
            Vector3 grip, Vector3 forward, float cap = 5.2f)
        {
            var pole = OriginalPole(root, source, name, grip, forward.normalized, grip.y + 4.2f);
            TuneFlowPole(root, pole, grip, 4.2f, 38, cap);
            var data = new SerializedObject(pole); data.FindProperty("holdSeconds").floatValue = 0;
            data.ApplyModifiedProperties(); return pole;
        }

        static void HideAFusedPole(Transform bg, HumanCannonPole pole)
        {
            foreach (var t in bg.GetComponentsInChildren<Transform>(true).Where(t => t != pole.transform &&
                (t.name.StartsWith(pole.name + "_", StringComparison.Ordinal) || t.name == pole.name + " 천장 매립 하중보")).ToArray())
                PreserveFusionGuide(t);
            PreserveFusionGuide(pole.transform);
        }

        static void BuildAFusedDescent(Transform root, Transform bg, HumanCannonPole source, List<BoxCollider> ropes)
        {
            var group = VerticalChild(root, "03_04A_CannonWireDescent");
            PreserveFusionGuide(bg.Find("30_SealedJoints/하행 천장 맞댐"));
            PreserveFusionGuide(bg.Find("30_SealedJoints/하행 집수층 맞댐"));
            PreserveFusionGuide(bg.Find(FusionGroup + "/03_CannonLandingToRope"));
            var old = bg.Find(VerticalGroup + "/02_Route_0_to_minus6_to_plus2");
            foreach (Transform t in old)
                if (t.position.x > 33) PreserveFusionGuide(t);
            foreach (Transform t in GameObject.Find(CannonGameplay).transform)
                if (t.name == "착지 발판 경계 도색") PreserveFusionGuide(t);
            foreach (var p in UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None)
                .Where(p => p.name.StartsWith("Descent_", StringComparison.Ordinal)).ToArray()) HideAFusedPole(bg, p);
            PreserveFusionGuide(bg.Find(CompletionGroup + "/04_StairSwingDescent"));
            foreach (var t in bg.Find("31_FlowRefinement").Cast<Transform>().Where(t => t.name.StartsWith("Descent_", StringComparison.Ordinal)))
                PreserveFusionGuide(t);
            // Original 04A structure was disabled with the old stairs. Keep its enclosure, but not the walking loop.
            BWallZ(group, 56.5f, 6, 62.5f, -86, 36, 3);
            BWallZ(group, 74, 6, 60.5f, -86, 36, 3);
            BFloor(group, "하강 심부 집수정", 58, 72.5f, 6, 62.5f, -86, 3);
            BMass(group, "하강 공동 덮개", new Vector3(65, 37.5f, 34), new Vector3(20, 3, 59));

            ropes.Add(CompletionRope(group, "A03_CannonLandingWire", new Vector3(44, -6, 75), new Vector3(60, -6, 75)));
            FeedbackAim(GameObject.Find("03_Cannon").GetComponent<HumanCannonStation>(), new Vector3(46, -6, 75), 1.7f);
            AFusedPole(group, source, "A04A_01_CavernExit", new Vector3(61, -4.5f, 75), new Vector3(6, 0, -10));
            ropes.Add(CompletionRope(group, "A04A_UpperWire", new Vector3(67, -10, 65), new Vector3(67, -10, 53)));
            AFusedPole(group, source, "A04A_02_DeepTransfer", new Vector3(67, -8.5f, 52), Vector3.back);
            ropes.Add(CompletionRope(group, "A04A_LowerWire", new Vector3(67, -16, 42), new Vector3(67, -16, 27)));
            AFusedPole(group, source, "A04A_03_PumpEntry", new Vector3(67, -14.5f, 26), Vector3.back);
            AFloor(group, "대포 포획 준비대", 63, 71, 9, 18, -22);
            foreach (var at in new[] { new Vector3(67, -10, 63), new Vector3(67, -16, 40), new Vector3(67, -22, 14) })
            {
                BLight(group, at + new Vector3(4, 8, -2), at, 420, 24);
                BMass(group, "층위를 드러내는 벽체 받침", new Vector3(72, at.y - 2, at.z), new Vector3(1.2f, 4, 4), bRecess);
            }
            // Short approach: keep the huge former under-stage as architecture behind a closed service gate.
            var points = UnityEngine.Object.FindAnyObjectByType<PlayerSpawnPoints>();
            var pd = new SerializedObject(points).FindProperty("points");
            for (int i = 0; i < pd.arraySize; i++)
            {
                var p = pd.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                Undo.RecordObject(p, "Short meaningful approach"); p.SetPositionAndRotation(new Vector3(13.5f + i, .05f, 64), Quaternion.identity);
            }
            BMass(group, "후방 폐쇄 정비문", new Vector3(15, 2, 60), new Vector3(12, 4, .55f), wornSteel);
        }

        static void BuildAConveyorHall(Transform root, Transform bg, HumanCannonPole source, List<BoxCollider> ropes)
        {
            var group = VerticalChild(root, "04B_ConveyorCatch");
            var old = bg.Find(DepthGroup + "/04B_PumpHall_Perimeter");
            PreserveFusionGuide(bg.Find("30_SealedJoints/서측 하부 갤러리 맞댐"));
            foreach (Transform t in bg.Find(DepthGroup + "/07_ServiceFinish"))
                if (t.position.z < 10 && t.position.y > -30 && (t.name.Contains("갤러리")
                    || t.name.Contains("정비 작업 바닥") || t.name.Contains("바닥 보수") || t.name.Contains("마모된 설비 경계")))
                    PreserveFusionGuide(t);
            // These pipes, flange joints, brackets and wall marks belonged to the former X56.5 wall.
            // Move the complete attachment set with the widened envelope; never leave pipes across the loop.
            foreach (Transform t in bg.Find(DepthGroup + "/07_ServiceFinish"))
                if (t.position.x >= 57.5f && t.position.x <= 62.5f && t.position.z < 6)
                { Undo.RecordObject(t, "Move old west-wall services with the envelope"); t.position += Vector3.left * 17.5f; }
            // Preserve the immense core. Replace only the west envelope and disused service walkways.
            foreach (Transform t in old)
            {
                if (t.name.Contains("갤러리") || t.name.Contains("횡단") || t.name.Contains("회랑") || t.name.Contains("복귀") ||
                    t.name.Contains("난간") || t.name.Contains("손잡이") || t.name.Contains("중간대") || t.name.Contains("도색") ||
                    t.name == "서측 갤러리 교각") PreserveFusionGuide(t);
                var box = t.GetComponent<BoxCollider>();
                if (box != null && box.bounds.size.y > 100 && box.bounds.size.z > 60 && box.bounds.center.x < 60)
                    PreserveFusionGuide(t);
                if (t.name == "펌프동 압축 덮개" || t.name == "86m 아래 집수조") PreserveFusionGuide(t);
            }
            BWallZ(group, 39, -68, 7, -86, 36, 3);
            BWallX(group, -68, 39, 56.5f, -86, 36, 4);
            BWallX(group, 7, 39, 58, -86, 36, 2);
            BFloor(group, "확장 집수정", 40.5f, 118, -66, 6, -86, 3);
            BMass(group, "확장 설비동 덮개", new Vector3(79, 38, -31), new Vector3(84, 4, 76));
            for (int i = 0; i < 3; i++)
            {
                float z = -6 - i * 23;
                BMass(group, "거친 외벽 버트레스", new Vector3(42, -25, z), new Vector3(3, 118, 4), bRecess);
                BLight(group, new Vector3(43, -9, z), new Vector3(56, -22, z - 4), 800, 32);
                VerticalCylinder(group, "설비동 수직 주관", new Vector3(44, -30, z - 8), 1.1f, 100, wornPipe);
            }
            var capture = CompletionCannon(group, "A04B_ConveyorCannon", new Vector3(67, -22, 13), new Vector3(67, -18.5f, -9), 1.9f);
            FeedbackAim(capture, new Vector3(67, -18.5f, -9), 1.9f);
            var center = new Vector3(57, -17, -20);
            HumanCannonPoleLoop firstLoop = null;
            for (int i = 0; i < 6; i++)
            {
                var track = ChildAt(group, "LoopTrack_" + i, center, Quaternion.identity);
                var carriage = VerticalChild(track, "MovingCarriage");
                var loop = track.gameObject.AddComponent<HumanCannonPoleLoop>();
                loop.Configure(carriage, Vector3.zero, 22, 10, 5, i / 6f);
                loop.EvaluatePose(0, out var at, out var rotation, out _, out _);
                carriage.SetPositionAndRotation(at, rotation);
                AFusedPole(carriage, source, "A04B_CirculatingGrip_" + i, at, rotation * Vector3.forward, 5.2f);
                if (i == 0) firstLoop = loop;
            }
            // Visible closed overhead rail; straight capture/disembark zones and smooth return arcs.
            const float period = (44 + 20 * Mathf.PI) / 5;
            for (int i = 0; i < 72; i++)
            {
                firstLoop.EvaluatePose(period * i / 72, out var a, out _, out _, out _);
                firstLoop.EvaluatePose(period * (i + 1) / 72, out var b, out _, out _, out _);
                VerticalCable(group, "폐회로 운반 레일 " + i, a + Vector3.up * 4.7f, b + Vector3.up * 4.7f, .35f, wornSteel);
            }
            VerticalCable(group, "발사 포획 구간 레일 도색", new Vector3(67,-12.3f,-9), new Vector3(67,-12.3f,-15), .4f, maintenancePaint);
            VerticalCable(group, "외줄 하차 구간 레일 도색", new Vector3(47,-12.3f,-29), new Vector3(47,-12.3f,-22), .4f, maintenancePaint);
            for (int i = 0; i < 5; i++)
            {
                double distance = 44 + 10 * Math.PI + 2 + i * .8;
                firstLoop.EvaluatePose(distance / 5, out var a, out _, out _, out _);
                firstLoop.EvaluatePose((distance + .8) / 5, out var b, out _, out _, out _);
                VerticalCable(group, "작업대 하차 레일 도색 " + i, a + Vector3.up * 4.7f, b + Vector3.up * 4.7f, .4f, maintenancePaint);
            }
            foreach (var at in new[] { new Vector3(67, -12.3f, -9), new Vector3(47, -12.3f, -25), new Vector3(47, -12.3f, -3) })
            {
                BMass(group, "레일 고정 하중보", at + new Vector3(0, 1, 0), new Vector3(7, 1.2f, 1), bRecess, false);
                VerticalCable(group, "레일 수직 인장재", at + Vector3.up, new Vector3(at.x, 35, at.z), .14f, wornSteel);
                BLight(group, at + Vector3.up * 2, at + Vector3.down * 9, 450, 20);
            }
            ropes.Add(CompletionRope(group, "A04B_PrecisionExitWire", new Vector3(47, -23, -26), new Vector3(47, -23, -11)));
            ropes.Add(CompletionRope(group, "A04B_ExitTurnWire", new Vector3(47, -23, -11), new Vector3(51, -23, -7)));
            AFusedPole(group, source, "A04B_WireExitGrip", new Vector3(52, -21.5f, -6), new Vector3(6, 0, 6), 7.2f);
            AFloor(group, "후속 하차 작업대", 44.5f, 53, -1, 6, -23);
            AFusedPole(group, source, "A04B_FloorExitGrip", new Vector3(52.5f, -21.5f, 4), new Vector3(8, 0, -4), 5.2f);
            AFloor(group, "두 하차 경로 합류대", 56, 62, -3, 4, -23);
            ropes.Add(CompletionRope(group, "A04B_SawTransferWire", new Vector3(62, -23, 0), new Vector3(70, -23, 0)));
            AFusedPole(group, source, "A04B_ReturnCannonGrip", new Vector3(71.5f, -21.5f, 0), Vector3.right, 7.2f);
            AFloor(group, "압착기 통과와 복귀 대포대", 79, 91, -4, 4, -22);
            var returnStation = GameObject.Find("04_ReturnCannon").GetComponent<HumanCannonStation>();
            Undo.SetTransformParent(returnStation.transform, group, "Connect return cannon");
            returnStation.transform.position = new Vector3(87, -22, 0);
            FeedbackAim(returnStation, new Vector3(85, 0, 16.5f), 2.8f);
            PreserveFusionGuide(bg.Find(DepthGroup + "/04C_OriginalCannonReturn"));
            ReuseAConveyorHazards(group, bg);
            FusionFloorMarks(group, new Vector3(58, -22.98f, 0), Vector3.right);
            BLight(group, new Vector3(87, -13, -3), new Vector3(84, -22, 0), 650, 25);
        }

        static void ReuseAConveyorHazards(Transform group, Transform bg)
        {
            var old = bg.Find(MidEncounterGroup);
            var saws = old.Find("01_CrossSaw_StopAndGo").GetComponentsInChildren<HumanCannonHazard>();
            for (int i = 0; i < saws.Length; i++)
            {
                float x = 64 + i * 4;
                MoveAFusedHazard(saws[i], group, new Vector3(x, -21.9f, i == 0 ? -4 : 4), new Vector3(x, -21.9f, i == 0 ? 4 : -4));
            }
            var presses = old.Find("02_DropPress_ReadAndSprint").GetComponentsInChildren<HumanCannonHazard>();
            MoveAFusedHazard(presses[0], group, new Vector3(83.5f, -10, 0), new Vector3(83.5f, -20.5f, 0));
            foreach (var name in new[] { "01_CrossSaw_StopAndGo", "02_DropPress_ReadAndSprint", "03_LowDuct_Crouch", "Wayfinding_앉아서 통과", "Wayfinding_대기 포켓 표식", "VisualsFinished" })
                PreserveFusionGuide(old.Find(name));
            // Enclose the mechanisms instead of leaving floating original trap props.
            foreach (float x in new[] { 64f, 68f }) foreach (float z in new[] { -4.6f, 4.6f })
                BMass(group, "외줄 횡단 톱 회수함", new Vector3(x, -21.6f, z), new Vector3(2.8f, 2.8f, 1.1f), bRecess);
            foreach (float z in new[] { -4.5f, 4.5f })
                BMass(group, "복귀 압착기 가이드", new Vector3(83.5f, -15, z), new Vector3(.7f, 16, .8f), wornSteel);
            BMass(group, "복귀 압착기 실린더보", new Vector3(83.5f, -6, 0), new Vector3(4, 2, 10), bRecess);
        }

        static void MoveAFusedHazard(HumanCannonHazard hazard, Transform parent, Vector3 from, Vector3 to)
        {
            var cue = hazard.GetComponent<HumanCannonHazardCue>(); var delta = from - cue.origin;
            var group = hazard.transform.parent;
            Undo.SetTransformParent(group, parent, "Reuse hazard in fused route");
            Undo.RecordObject(group, "Move original hazard"); group.position += delta;
            Undo.RecordObject(cue, "Move world-space hazard cue"); cue.origin = from; cue.destination = to;
            EditorUtility.SetDirty(cue);
        }

        static void RefineAFinaleGripSpacing(Transform root, Transform bg)
        {
            var poles = UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None)
                .Where(p => p.Grip.position.x > 270).OrderBy(p => p.Grip.position.x).ToArray();
            if (poles.Length != 3) throw new InvalidOperationException("Expected saved finale grip chain");
            HideAFusedPole(bg, poles[1]);
            // The middle 7m-spaced bar was skippable. Keep two deliberate maximum-swing transfers.
            foreach (var pole in new[] { poles[0], poles[2] })
            {
                foreach (var t in bg.GetComponentsInChildren<Transform>(true).Where(t =>
                    t.name.StartsWith(pole.name + "_", StringComparison.Ordinal)).ToArray()) PreserveFusionGuide(t);
                var at = pole.Grip.position;
                if (pole == poles[2]) at.x = 293;
                TuneFlowPole(root, pole, at, 5, 38, 7.2f);
            }
        }

        static void OpenAFusedWirePassages(Transform root, Transform bg)
        {
            var group = VerticalChild(root, "WirePassageOpenings");
            CutAFusedWall(group, bg.Find(VerticalGroup + "/01_Shell_50x50_134m/남측 하부 외벽"), 62, 72, -15, -6);
            CutAFusedWall(group, bg.Find(BrutalGroup + "/05_TransferChasm/진입측 짧은 흉벽"), 83.8f, 86.2f, -3, 3);
        }

        static void CutAFusedWall(Transform parent, Transform wall, float x1, float x2, float y1, float y2)
        {
            if (wall == null) throw new InvalidOperationException("Expected original wall for wire passage");
            var bounds = wall.GetComponent<BoxCollider>().bounds;
            var material = wall.GetComponent<Renderer>().sharedMaterial;
            PreserveFusionGuide(wall);
            void Piece(string name, float left, float right, float bottom, float top)
            {
                if (right-left < .01f || top-bottom < .01f) return;
                BMass(parent, wall.name + "_" + name, new Vector3((left+right)*.5f,(bottom+top)*.5f,bounds.center.z),
                    new Vector3(right-left,top-bottom,bounds.size.z), material);
            }
            Piece("하부", bounds.min.x, bounds.max.x, bounds.min.y, Mathf.Max(bounds.min.y,y1));
            Piece("상부", bounds.min.x, bounds.max.x, Mathf.Min(bounds.max.y,y2), bounds.max.y);
            Piece("좌측", bounds.min.x, Mathf.Max(bounds.min.x,x1), Mathf.Max(bounds.min.y,y1), Mathf.Min(bounds.max.y,y2));
            Piece("우측", Mathf.Min(bounds.max.x,x2), bounds.max.x, Mathf.Max(bounds.min.y,y1), Mathf.Min(bounds.max.y,y2));
        }
    }
}
