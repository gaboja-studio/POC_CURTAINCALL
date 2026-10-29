using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        [MenuItem("Tools/CurtainCall/탈출로/마지막 질주 원본 기믹 복구")]
        public static void ConnectFinaleHazards()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("현재 A 편집 씬이 필요합니다.");
            var root = GameObject.Find(RootName).transform.Find("25_FinaleHazards_A");
            var drafts = root.GetComponentsInChildren<EscapeSoloHazard>(true);
            if (drafts.Length != 14) throw new InvalidOperationException("이전 종합 배치 14개가 필요합니다.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restore finale with original host hazards");
            var course = Ensure<HumanCannonHazardCourse>(root.gameObject);
            var hazards = new List<HumanCannonHazard>(); var winds = new List<HumanCannonWindZone>();
            foreach (var draft in drafts)
            {
                Undo.RecordObject(draft,"Disable offline replacement"); draft.enabled = false;
                var logic = draft.transform.Find("OriginalMechanism");
                if (logic == null) logic = ChildAt(draft.transform,"OriginalMechanism",draft.origin,draft.visual.rotation);
                var cue = Ensure<HumanCannonHazardCue>(logic.gameObject);
                Undo.RecordObject(cue,"Shared cue timing");
                cue.origin = draft.origin; cue.destination = draft.destination;
                cue.warning = draft.warning; cue.visual = draft.visual;
                cue.warningSeconds = draft.warningSeconds; cue.activeSeconds = draft.travelSeconds;
                cue.restSeconds = draft.restSeconds; cue.phase = draft.phase;
                cue.falling = draft.kind == EscapeSoloHazard.Kind.Drop; cue.showWhileWarning = cue.falling;
                if (draft.kind == EscapeSoloHazard.Kind.CrossBlades || draft.kind == EscapeSoloHazard.Kind.Fire || draft.kind == EscapeSoloHazard.Kind.Wind)
                    cue.destination = cue.origin;
                cue.controlsVisual = draft.kind == EscapeSoloHazard.Kind.Wind;
                draft.visual.position = draft.origin; draft.visual.gameObject.SetActive(true);
                if (draft.warning != null) draft.warning.gameObject.SetActive(true);
                if (draft.kind == EscapeSoloHazard.Kind.Wind)
                {
                    var wind = Ensure<HumanCannonWindZone>(logic.gameObject);
                    var box = Ensure<BoxCollider>(logic.gameObject); box.isTrigger = true; box.size = draft.size;
                    var data = new SerializedObject(wind);
                    data.FindProperty("area").objectReferenceValue = box;
                    data.FindProperty("cue").objectReferenceValue = cue;
                    data.FindProperty("direction").vector3Value = logic.InverseTransformDirection(draft.destination.normalized);
                    data.FindProperty("affectsGround").boolValue = true; data.FindProperty("affectsAir").boolValue = true;
                    data.ApplyModifiedProperties(); winds.Add(wind);
                }
                else
                {
                    var hazard = Ensure<HumanCannonHazard>(logic.gameObject);
                    var kind = draft.kind == EscapeSoloHazard.Kind.Mace ? CannonHazardKind.SpikeMace
                        : draft.kind == EscapeSoloHazard.Kind.CrossBlades ? CannonHazardKind.RotatingBlades
                        : draft.kind == EscapeSoloHazard.Kind.Drop ? CannonHazardKind.FallingLoad
                        : draft.kind == EscapeSoloHazard.Kind.Fire ? CannonHazardKind.FireJet : CannonHazardKind.GroundSaw;
                    var data = new SerializedObject(hazard);
                    data.FindProperty("kind").enumValueIndex = (int)kind;
                    data.FindProperty("visual").objectReferenceValue = draft.visual;
                    data.FindProperty("cue").objectReferenceValue = cue;
                    data.FindProperty("boxSize").vector3Value = draft.size;
                    data.FindProperty("radius").floatValue = draft.radius;
                    data.FindProperty("bladeHalfLength").floatValue = draft.radius;
                    data.FindProperty("bladeHalfThickness").floatValue = .14f;
                    data.FindProperty("travel").vector3Value = Vector3.zero;
                    data.FindProperty("horizontalBlade").boolValue = false;
                    data.FindProperty("rotationSpeed").floatValue = draft.kind == EscapeSoloHazard.Kind.CrossBlades ? 130 : 480;
                    data.ApplyModifiedProperties(); hazards.Add(hazard);
                }
            }
            var courseData = new SerializedObject(course);
            SetFinaleArray(courseData,"hazards",hazards.Cast<UnityEngine.Object>().ToArray());
            SetFinaleArray(courseData,"windZones",winds.Cast<UnityEngine.Object>().ToArray());
            courseData.FindProperty("useAuthoredHazardCount").boolValue = true;
            courseData.FindProperty("difficulty").intValue = 1;
            courseData.ApplyModifiedProperties();
            Undo.RecordObject(root.gameObject,"Enable finale"); root.gameObject.SetActive(true);
            var roster = UnityEngine.Object.FindAnyObjectByType<CurtainCall.Network.PlayerSync.PlayerSpawnPoints>();
            var points = new SerializedObject(roster).FindProperty("points");
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (point == null) throw new InvalidOperationException("출발점 누락");
                Undo.RecordObject(point,"Start from full route entrance");
                point.SetPositionAndRotation(new Vector3(13.5f+i,.05f,6),Quaternion.identity);
            }
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
        }
        static void SetFinaleArray(SerializedObject data,string name,UnityEngine.Object[] values)
        {
            var array = data.FindProperty(name); array.arraySize = values.Length;
            for (int i=0;i<values.Length;i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        public static string ValidateFinaleHazards()
        {
            int passed=0,failed=0; var report=new StringBuilder();
            void Check(string label,bool value) { if(value)passed++; else {failed++;report.AppendLine("FAIL "+label);} }
            var root=GameObject.Find(RootName).transform.Find("25_FinaleHazards_A");
            var course=root.GetComponent<HumanCannonHazardCourse>();
            Check("finale root active",root.gameObject.activeInHierarchy);
            Check("one original host hazard course",course!=null&&UnityEngine.Object.FindObjectsByType<HumanCannonHazardCourse>(FindObjectsSortMode.None).Length==1);
            var hazards=root.GetComponentsInChildren<HumanCannonHazard>();
            var cues=root.GetComponentsInChildren<HumanCannonHazardCue>();
            Check("13 damage hazards and one wind",hazards.Length==13&&root.GetComponentsInChildren<HumanCannonWindZone>().Length==1);
            Check("14 shared-time cues",cues.Length==14);
            Check("old offline logic disabled",root.GetComponentsInChildren<EscapeSoloHazard>(true).All(h=>!h.enabled));
            if(course!=null)
            {
                Check("all finale hazards registered", hazards.All(h => Enumerable.Range(0,course.HazardCount).Any(i => course.Hazard(i)==h)));
                for(int i=0;i<course.HazardCount;i++)Check("registered enabled "+i,course.Hazard(i)!=null&&course.IsHazardEnabled(i));
            }
            foreach(var hazard in hazards)
            {
                var cue=hazard.GetComponent<HumanCannonHazardCue>();
                Check(hazard.transform.parent.name+" refs",cue!=null&&cue.visual!=null&&cue.warning!=null);
                if(cue==null)continue;
                double warning=.1; double live=cue.warningSeconds+cue.activeSeconds*.5-cue.phase;
                // 무해한 예고와 작동 중 실제 형상 접촉을 분리해 확인한다.
                Check(hazard.transform.parent.name+" harmless warning",!hazard.Touches(cue.origin-Vector3.up*.8f,1.73f,.3f,Math.Max(0,warning-cue.phase),1));
                Vector3 position=hazard.Position(live,1);
                Check(hazard.transform.parent.name+" live contact",hazard.Touches(position-Vector3.up*.8f,1.73f,.3f,live,1));
                Check(hazard.transform.parent.name+" reset pose",Vector3.Distance(hazard.Position(0,1),cue.origin)<.01f);
            }
            Check("falling loads 3",hazards.Count(h=>h.Kind==CannonHazardKind.FallingLoad)==3);
            Check("fire jets 2",hazards.Count(h=>h.Kind==CannonHazardKind.FireJet)==2);
            Check("cutting devices 8",hazards.Count(h=>!h.KillsOnContact)==8);
            var roster=UnityEngine.Object.FindAnyObjectByType<CurtainCall.Network.PlayerSync.PlayerSpawnPoints>();
            var points=new SerializedObject(roster).FindProperty("points");
            Physics.SyncTransforms();
            for(int i=0;i<points.arraySize;i++)
            {
                var p=points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                Check("entrance spawn "+i,p!=null&&p.position.z<10&&Physics.Raycast(p.position,Vector3.down,.2f));
                Check("entrance headroom "+i,p!=null&&!Physics.CheckCapsule(p.position+Vector3.up*.35f,p.position+Vector3.up*1.45f,.3f,~0,QueryTriggerInteraction.Ignore));
            }
            report.AppendLine($"Passed={passed}; Failed={failed}. Shared original host damage / static contact only.");
            return report.ToString();
        }
    }
}
