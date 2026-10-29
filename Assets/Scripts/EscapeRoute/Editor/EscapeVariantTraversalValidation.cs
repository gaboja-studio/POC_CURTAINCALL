using System;
using System.Linq;
using System.Text;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ValidateVariantTraversal()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode required");
            Physics.SyncTransforms(); var scene=SceneManager.GetActiveScene();
            var report=new StringBuilder(); int passed=0,failed=0;
            void Check(string label,bool value){if(value)passed++;else{failed++;report.AppendLine("FAIL "+label);}}
            var stations=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HumanCannonStation>()).ToArray();
            foreach(var station in stations)
            {
                var anchor=station.transform.Find("AuthoredLanding");
                Vector3 target=anchor!=null?anchor.position:new Vector3(85,0,25);
                Vector3 position=station.Muzzle.position,velocity=station.LaunchVelocity;
                float radius=GameSettings.Base.Character.CapsuleRadius,height=GameSettings.Base.Character.CapsuleHeight;
                RaycastHit hit=default;
                for(int i=0;i<720;i++)
                {
                    var step=BallisticMotion.Step(ref velocity,Vector3.zero,GameSettings.Base.Character.Gravity,1f/120);
                    // Include a 1cm contact skin so an exact end-of-step floor touch cannot become an initial overlap next step.
                    if(Physics.CapsuleCast(position+Vector3.up*radius,position+Vector3.up*(height-radius),radius,step.normalized,out hit,step.magnitude+.01f,~0,QueryTriggerInteraction.Ignore))break;
                    position+=step;
                }
                bool landing=hit.collider!=null&&hit.normal.y>.95f&&hit.collider.GetComponent<HumanCannonLandingZone>()!=null;
                Check(station.name+" unobstructed first safe landing",landing);
                Check(station.name+" authored landing",hit.collider!=null&&Vector3.Distance(hit.point,target)<2);
                var board=station.BoardingPoint.position;
                Check(station.name+" boarding floor",Physics.Raycast(board+Vector3.up*.3f,Vector3.down,.6f,~0,QueryTriggerInteraction.Ignore));
                report.AppendLine(station.name+" first contact "+hit.point+" "+hit.collider?.name);
            }
            var poles=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HumanCannonPole>()).ToArray();
            bool feedback=GameObject.Find(RootName).transform.Find(FeedbackGroup)!=null;
            void Swing(string name,Vector3 target)
            {
                var p=poles.Single(x=>x.name==name);Check(name+" landing",StaticSwingReach(p,target,true,out var proof)); report.AppendLine(name+": "+proof);
            }
            if(scene.path==VariantBPath)
            {
                if(feedback)
                {
                    Swing("B03_MidRelaySwing",new Vector3(44,.5f,82));
                    Check("B three long directed cannon legs",stations.Count(s=>s.transform.position.x<70)==3
                        &&stations.Where(s=>s.transform.position.x<70).All(s=>Vector3.ProjectOnPlane(s.transform.Find("AuthoredLanding").position-s.Muzzle.position,Vector3.up).magnitude>18));
                }
                else
                {
                    Swing("B03_ClimbSwing",new Vector3(47,5,78));
                    Swing("B03_DescentSwing",new Vector3(66,-6,84));
                }
                Swing("B05_ReboundSwing",new Vector3(105.5f,-2.5f,27.5f));
                Swing("B05_FinalSwing",new Vector3(119,-8,29));
                var trampoline=UnityEngine.Object.FindAnyObjectByType<HumanCannonTrampoline>();
                var pole=poles.Single(p=>p.name=="B05_ReboundSwing");
                float closest=float.PositiveInfinity;
                for(float t=0;t<2.4f;t+=.02f)
                {
                    var direction=Vector3.ProjectOnPlane(pole.Grip.position-trampoline.Surface,Vector3.up).normalized;
                    var feet=trampoline.Surface+direction*trampoline.CarryMoveSpeed*t+Vector3.up*(trampoline.UpwardSpeed*t-.5f*GameSettings.Base.Character.Gravity*t*t);
                    closest=Mathf.Min(closest,Vector3.Distance(feet+Vector3.up*GameSettings.Base.Character.CapsuleHeight*.5f,pole.Grip.position));
                }
                Check("B trampoline original reach",closest<3.45f);report.AppendLine("B rebound nearest="+closest.ToString("F2"));
            }
            if(scene.path==VariantCPath)
            {
                var first=poles.Single(p=>p.name=="C05_ReboundTurningSwing");
                var second=poles.Single(p=>p.name=="C05_ExitTurningSwing");
                Check("C turning transfer",StaticSwingReach(first,second.Grip.position,false,out var proof));report.AppendLine("C turning transfer: "+proof);
                if(feedback)
                {
                    var third=poles.Single(p=>p.name=="C05_ChainThirdSwing");var last=poles.Single(p=>p.name=="C05_ChainFinalSwing");
                    Check("C second to third swing",StaticSwingReach(second,third.Grip.position,false,out proof));report.AppendLine("C second-third: "+proof);
                    Check("C third to fourth swing",StaticSwingReach(third,last.Grip.position,false,out proof));report.AppendLine("C third-fourth: "+proof);
                    Swing("C05_ChainFinalSwing",new Vector3(125,-8,43.5f));
                }
                else Swing("C05_ExitTurningSwing",new Vector3(124,-8,35));
                var trampoline=UnityEngine.Object.FindAnyObjectByType<HumanCannonTrampoline>();
                float closest=float.PositiveInfinity;
                for(float t=0;t<2.4f;t+=.02f)
                {
                    var direction=Vector3.ProjectOnPlane(first.Grip.position-trampoline.Surface,Vector3.up).normalized;
                    var feet=trampoline.Surface+direction*trampoline.CarryMoveSpeed*t+Vector3.up*(trampoline.UpwardSpeed*t-.5f*GameSettings.Base.Character.Gravity*t*t);
                    closest=Mathf.Min(closest,Vector3.Distance(feet+Vector3.up*GameSettings.Base.Character.CapsuleHeight*.5f,first.Grip.position));
                }
                Check("C trampoline original reach",closest<3.45f);report.AppendLine("C rebound nearest="+closest.ToString("F2"));
            }
            report.AppendLine("Passed="+passed+"; Failed="+failed+". Static feasibility only; manual input timing remains untested.");return report.ToString();
        }
    }
}
