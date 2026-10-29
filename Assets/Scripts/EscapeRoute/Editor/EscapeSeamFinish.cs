using System;
using System.Linq;
using System.Text;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static void FinishEscapeJoints()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode required");
            var background=GameObject.Find(RootName).transform;
            if(background.Find("30_SealedJoints")!=null)throw new InvalidOperationException("Already finished; edit existing joints");
            CompletionMaterials();var p=VerticalChild(background,"30_SealedJoints");
            p.localScale=new Vector3(1/background.lossyScale.x,1/background.lossyScale.y,1/background.lossyScale.z);
            // Fill measured shell discontinuities, outside all traversal and launch apertures.
            BWallZ(p,74,60.5f,61,-86,36,3);
            BWallX(p,6,75.5f,76,-86,36,3);
            BWallX(p,45,127.5f,139.5f,-55,34,3);
            BWallZ(p,141,68,74,0,8,3); BWallZ(p,141,82,85,0,8,3);
            var descent=background.Find(CompletionGroup+"/04_StairSwingDescent");
            descent.Find("하행 설비동 덮개").gameObject.SetActive(false);
            descent.Find("하행 공동 집수 바닥").gameObject.SetActive(false);
            BMass(p,"하행 천장 맞댐",new Vector3(65,37.5f,34.25f),new Vector3(22,3,54.5f));
            BFloor(p,"하행 집수층 맞댐",56.5f,72.5f,6,62.5f,-86,3);
            // Remove same-height double faces beneath the last tread and the recovery slab.
            background.Find(DepthGroup+"/04B_PumpHall_Perimeter/서측 긴 하부 갤러리").gameObject.SetActive(false);
            BFloor(p,"서측 하부 갤러리 맞댐",65,71,-61,1.5f,-22,1.5f);
            var scene=SceneManager.GetActiveScene();
            if(scene.path==VariantBPath) FinishBRecoveryJoint(p,background);
            if(scene.path==VariantCPath)
            {
                var first=background.Find(TraversalSpacePrefix+"C/03_OffsetMaintenanceShelves");
                foreach(Transform rail in first)
                    if(rail.name.StartsWith("정비대") && rail.position.z>77.7f && rail.position.x>=36 && rail.position.x<=43)rail.gameObject.SetActive(false);
                SpaceEdgeRail(p,new Vector3(36,0,77.9f),new Vector3(39.5f,0,77.9f));
                SpaceEdgeRail(p,new Vector3(41.5f,0,77.9f),new Vector3(43,0,77.9f));
                var scale=background.Find(BrutalGroup).Cast<Transform>().FirstOrDefault(t=>t.name=="ScaleReference_1.73m");
                if(scale!=null)scale.position=new Vector3(124,-7.82f,35);
                // The old landing's 12m parapet would hang over the newly opened chasm.
                background.Find(BrutalGroup+"/05_TransferChasm/도착측 흉벽").gameObject.SetActive(false);
                BMass(p,"C 도착측 흉벽",new Vector3(124.75f,-7.4f,15),new Vector3(5.5f,1.2f,.5f));
            }
            if(scene.path==ScenePath)
            {
                var transfer=background.Find(FusionGroup+"/05_RopeDropTrampolineSwing");
                foreach(var rail in transfer.GetComponentsInChildren<Transform>().Where(t=>t.name=="1.1m 점검 난간" && Mathf.Abs(t.position.x-93)<.1f).ToArray())
                    rail.gameObject.SetActive(false);
                SpaceEdgeRail(p,new Vector3(93,-4.6f,25),new Vector3(93,-4.6f,31));
            }
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);
        }

        static void FinishBRecoveryJoint(Transform p,Transform background)
        {
            CompletionMaterials();
            var mechanics=background.Find("29_B_OriginalMechanics");
            foreach(Transform rail in mechanics)
                if(rail.name.StartsWith("정비대")&&Mathf.Abs(rail.position.x-115)<.1f&&rail.position.z>=24.4f&&rail.position.z<=28.1f)rail.gameObject.SetActive(false);
            BFloor(p,"B 계단 옆 회복 통로 확폭",115,116,24.5f,34.5f,-5.5f,1.6f);
            Ensure<HumanCannonLandingZone>(p.Find("B 계단 옆 회복 통로 확폭").gameObject);
            SpaceEdgeRail(p,new Vector3(116,-5.5f,24.5f),new Vector3(116,-5.5f,34.5f));
            FusionFloorMarks(p,new Vector3(113,-5.48f,25.3f),Vector3.right);
            FusionFloorMarks(p,new Vector3(115,-5.48f,27),Vector3.forward);
        }

        public static string ValidateEscapeJoints()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Static edit mode checks");
            Physics.SyncTransforms();int passed=0,failed=0;var log=new StringBuilder();
            void Check(string label,bool ok){if(ok)passed++;else{failed++;if(failed<30)log.AppendLine("FAIL "+label);}}
            void Floor(Vector3 feet,float tolerance=.035f)
            {
                bool exists=Physics.Raycast(feet+Vector3.up*.26f,Vector3.down,out var hit,.6f,~0,QueryTriggerInteraction.Ignore);
                Check("floor joint "+feet,exists&&Mathf.Abs(hit.point.y-feet.y)<tolerance);
                if(!exists)return;
                float radius=GameSettings.Base.Character.CapsuleRadius,height=GameSettings.Base.Character.CapsuleHeight;
                var body=Physics.OverlapCapsule(hit.point+Vector3.up*(radius+.26f),hit.point+Vector3.up*(height-radius+.26f),radius,~0,QueryTriggerInteraction.Ignore)
                    .Where(c=>c.bounds.max.y>hit.point.y+.26f).ToArray();
                Check("body joint "+feet+" "+string.Join(",",body.Select(c=>c.name)),body.Length==0);
            }
            void Walk(Vector3 from,Vector3 to)
            {int steps=Mathf.CeilToInt(Vector3.Distance(from,to)/.2f);for(int i=0;i<=steps;i++)Floor(Vector3.Lerp(from,to,i/(float)steps),.21f);}
            foreach(float x in new[]{66f,68f,70f})
            {
                Walk(new Vector3(x,2,65),new Vector3(x,2,60.5f));
                for(int f=0;f<3;f++)
                {
                    float z=60.5f-f*19,y=2-f*8;
                    for(int i=0;i<12;i++)Floor(new Vector3(x,y-(i+1)*.2f,z-(i+.5f)*.35f));
                    for(int i=0;i<8;i++)Floor(new Vector3(x,y-6.4f-(i+1)*.2f,z-16.2f-(i+.5f)*.35f));
                }
            }
            Walk(new Vector3(68,-22,3.4f),new Vector3(68,-22,-2));
            Walk(new Vector3(124,-8,44),new Vector3(124,-8,50));
            Walk(new Vector3(135,0,78),new Vector3(144,0,78));
            for(float z=59.5f;z<=63.5f;z+=.1f)foreach(float y in new[]{-20f,3f,12f})
                Check("03/04 side closure "+z+" "+y,Physics.Raycast(new Vector3(71.9f,y,z),Vector3.right,4,~0,QueryTriggerInteraction.Ignore));
            for(float x=128;x<=139;x+=.25f)foreach(float y in new[]{-20f,2f,20f})
                Check("05/06 back closure "+x+" "+y,Physics.Raycast(new Vector3(x,y,48.2f),Vector3.back,5,~0,QueryTriggerInteraction.Ignore));
            string path=SceneManager.GetActiveScene().path;
            if(path==VariantBPath)
            {
                for(int i=0;i<14;i++)Floor(new Vector3(113,-2.5f-(i+1)*.2f,31-(i+.5f)*.35f));
                Walk(new Vector3(113,-5.5f,25.5f),new Vector3(115,-5.5f,25.5f));
                Walk(new Vector3(115,-5.5f,25.5f),new Vector3(115,-5.5f,29));
            }
            if(path==VariantCPath)
            {
                Walk(new Vector3(58,-6,77.3f),new Vector3(63.6f,-6,77.3f));
                Walk(new Vector3(63.6f,-6,77.3f),new Vector3(63.6f,-6,80));
                Walk(new Vector3(88,0,25),new Vector3(99.5f,0,25));
                Walk(new Vector3(124,-8,35),new Vector3(124,-8,44));
            }
            log.AppendLine("Passed="+passed+"; Failed="+failed+". Sampled floor/body joins and shell seams; intended stunt gaps preserved.");return log.ToString();
        }
    }
}
