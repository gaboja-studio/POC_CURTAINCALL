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
            var startFloor=GameObject.Find("시작홀 연속 마감 바닥").GetComponent<Renderer>().bounds;
            Physics.SyncTransforms();
            for(int i=0;i<points.arraySize;i++)
            {
                var p=points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                Check("entrance spawn "+i,p!=null&&p.position.x>startFloor.min.x+.3f&&p.position.x<startFloor.max.x-.3f
                    &&p.position.z>startFloor.min.z+.3f&&p.position.z<startFloor.max.z-.3f&&Physics.Raycast(p.position,Vector3.down,.2f));
                Check("entrance headroom "+i,p!=null&&!Physics.CheckCapsule(p.position+Vector3.up*.35f,p.position+Vector3.up*1.45f,.3f,~0,QueryTriggerInteraction.Ignore));
            }
            report.AppendLine($"Passed={passed}; Failed={failed}. Shared original host damage / static contact only.");
            return report.ToString();
        }
    }
}
