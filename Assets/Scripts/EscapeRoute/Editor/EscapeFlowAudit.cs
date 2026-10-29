using System;
using System.Linq;
using System.Text;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string AuditCurrentFlow()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode only");
            var scene=SceneManager.GetActiveScene();Physics.SyncTransforms();var report=new StringBuilder();int passed=0,failed=0;
            void Check(string text,bool value){if(value)passed++;else{failed++;report.AppendLine("FAIL "+text);}}
            var system=UnityEngine.Object.FindAnyObjectByType<HumanCannonSystem>();
            var poles=UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None);
            var poleSystem=UnityEngine.Object.FindAnyObjectByType<HumanCannonPoleSystem>();
            var registered=new SerializedObject(poleSystem).FindProperty("poles");
            Check("all active swings registered",registered.arraySize==poles.Length && poles.All(p=>Enumerable.Range(0,registered.arraySize).Any(i=>registered.GetArrayElementAtIndex(i).objectReferenceValue==p)));
            Check("platform jump grab",new SerializedObject(poleSystem).FindProperty("allowPlatformJumpGrab").boolValue);
            foreach(var p in poles)
            {
                Check(p.name+" bounded launch",p.MaxReleaseUpwardSpeed>0&&p.MaxReleaseUpwardSpeed<=7.2f&&p.SwingLength<=5.05f);
                Check(p.name+" art/interaction",p.GetComponentInChildren<Renderer>()!=null&&p.Grip!=null);
            }
            Check("same pace",UnityEngine.Object.FindObjectsByType<EscapeTraversalPace>(FindObjectsSortMode.None).Length==1);
            Check("original interaction UI binding",system.GetComponent<CurtainCall.UI.SceneIntegration.InteractionSceneBinding>()!=null);
            Check("no persistent control HUD",typeof(EscapeCannonReviewHud).GetMethod("OnGUI",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)==null);
            var fire=UnityEngine.Object.FindObjectsByType<HumanCannonHazard>(FindObjectsSortMode.None).Where(h=>h.Kind==CannonHazardKind.FireJet).Select(h=>h.GetComponent<HumanCannonHazardCue>()).ToArray();
            bool feedback=GameObject.Find(RootName).transform.Find(FeedbackGroup)!=null;
            Check("all flames have alternating cues",fire.Length==(feedback?14:6)&&fire.All(f=>f!=null&&Mathf.Approximately(f.warningSeconds,.65f)&&Mathf.Approximately(f.activeSeconds,1.6f)&&Mathf.Approximately(f.restSeconds,2.25f)));
            if(feedback)
            {
                var corridor=fire.Where(f=>f.origin.y<0&&f.origin.x>119&&f.origin.x<126).GroupBy(f=>f.origin.z).ToArray();
                Check("six directly opposed flame pairs",corridor.Length==6&&corridor.All(g=>g.Count()==2&&g.Any(f=>f.phase==0)&&g.Any(f=>f.phase==2.25f)));
                var trampoline=UnityEngine.Object.FindAnyObjectByType<HumanCannonTrampoline>();
                Check("trampoline carry clearance",Mathf.Approximately(trampoline.CarryHandleHeight,.8f)
                    &&trampoline.CarryHandleHeight-(trampoline.Handle.position.y-trampoline.transform.position.y)>.15f);
            }
            bool conflict=false;
            for(double t=0;t<4.5;t+=.01)
                if(fire.Any(f=>f.phase==0&&f.Live(t))&&fire.Any(f=>f.phase>0&&f.Live(t)))conflict=true;
            Check("left/right fire never overlap",!conflict);
            for(int n=0;n<3;n++)
            {
                var p=poles.Single(x=>x.name=="Descent_"+n+"_1");
                if(feedback)
                {
                    Check("single descent swing "+n,poles.Count(x=>x.name.StartsWith("Descent_"+n+"_",StringComparison.Ordinal))==1);
                    Check("standing descent grab "+n,Vector3.Distance(new Vector3(67,2-n*8-2.4f,60.5f-n*19-6.3f),p.BarPosition(0))<2.6f*1.25f);
                    Check("single descent recovery "+n,StaticSwingReach(p,new Vector3(67,2-n*8-6.4f,60.5f-n*19-15.2f),true,out _));
                }
                else
                {
                    var q=poles.Single(x=>x.name=="Descent_"+n+"_2");
                    Check("descent transfer "+n,StaticSwingReach(p,q.Grip.position,false,out _));
                    Check("descent recovery "+n,StaticSwingReach(q,new Vector3(67,2-n*8-6.4f,60.5f-n*19-15.2f),true,out _));
                }
            }
            var runPoles=poles.Where(p=>p.Grip.position.x>270).OrderBy(p=>p.Grip.position.x).ToArray();
            for(int i=0;i<runPoles.Length-1;i++)Check("finale swing chain "+i,StaticSwingReach(runPoles[i],runPoles[i+1].Grip.position,false,out _));
            if(scene.path==ScenePath)
            {
                var coop=UnityEngine.Object.FindAnyObjectByType<EscapeCooperativeRopes>();Check("A cooperative section",coop!=null);
                if(coop!=null)
                {
                    var data=new SerializedObject(coop);var supports=data.FindProperty("ropeSupports");
                    Check("four timed original ropes",supports.arraySize==4&&Enumerable.Range(0,4).All(i=>supports.GetArrayElementAtIndex(i).objectReferenceValue is BoxCollider c&&c.enabled&&Mathf.Abs(c.bounds.size.x-12)<.01f));
                    Check("four plus A03 original rope",new SerializedObject(UnityEngine.Object.FindAnyObjectByType<TightropeCourse>()).FindProperty("sceneRopes").arraySize==5);
                    Check("button and return gate",data.FindProperty("button").objectReferenceValue!=null&&data.FindProperty("returnGate").objectReferenceValue!=null);
                    Check("carried original trampoline",coop.Trampoline!=null&&new SerializedObject(coop.Trampoline).FindProperty("requireCarrierForBounce").boolValue);
                    var transfer=poles.Where(p=>p.Grip.position.x>100&&p.Grip.position.x<118).OrderBy(p=>p.Grip.position.x).ToArray();
                    for(int i=0;i<transfer.Length-1;i++)Check("A transfer chain "+i,StaticSwingReach(transfer[i],transfer[i+1].Grip.position,false,out _));
                    Check("A transfer landing",StaticSwingReach(transfer.Last(),new Vector3(119,-8,28),true,out _));
                }
            }
            Check("no missing scripts",scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0);
            report.AppendLine(scene.name+" Passed="+passed+" Failed="+failed+" (connections/geometry only; manual play not performed)");
            report.Append(ValidateVariantTraversal());return report.ToString();
        }
    }
}
