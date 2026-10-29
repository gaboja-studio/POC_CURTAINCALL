using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static void CompleteVariantC()
        {
            var group=BeginVariantCompletion("C"); var background=group.parent;
            var spaces=background.Find(TraversalSpacePrefix+"C");
            var first=spaces.Find("03_OffsetMaintenanceShelves");
            first.Find("하부 점검층 하강").gameObject.SetActive(false);
            first.Find("진입 정비 선반").gameObject.SetActive(false);
            foreach(Transform part in first)
                if(part.name.StartsWith("정비대") && part.position.x<35.9f) part.gameObject.SetActive(false);
            var second=spaces.Find("05_DescendingMaintenancePlatforms");
            second.Find("첫 하강 정비 계단").gameObject.SetActive(false);
            second.Find("도착부 하강 정비 계단").gameObject.SetActive(false);
            var entry=UnityEngine.Object.FindObjectsByType<HumanCannonStation>(FindObjectsSortMode.None).OrderBy(s=>s.transform.position.x).First();
            Vector3 firstTarget=new Vector3(39.5f,0,74.5f);
            entry.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(firstTarget-entry.transform.position,Vector3.up));
            PrefabUtility.RecordPrefabInstancePropertyModifications(entry.transform);
            AimCompletionCannon(entry,firstTarget,1.5f);
            CompletionDeck(group,"꺾인 외줄 뒤 대포 정비대",38.5f,42.5f,85,89,0);
            CompletionCannon(group,"C03_DiagonalCannon",new Vector3(40.5f,0,87),new Vector3(56,-6,75.5f),1.8f);
            var rope1=CompletionRope(group,"C03_TurningWire",new Vector3(40.5f,0,77),new Vector3(40.5f,0,86));
            // Open the north-facing rail only where the new rope leaves the original upper shelf.
            foreach(Transform part in first)
                if(part.name.StartsWith("정비대") && part.position.z>77.7f && part.position.x>39 && part.position.x<42) part.gameObject.SetActive(false);
            var rope2=CompletionRope(group,"C05_DropWire",new Vector3(99.5f,0,25),new Vector3(106.5f,0,25));
            var trampoline=UnityEngine.Object.FindAnyObjectByType<HumanCannonTrampoline>();
            trampoline.transform.position=new Vector3(109,-4,25);
            var oldLanding=background.Find(BrutalGroup+"/05_TransferChasm/8m 아래 도착 정비대");
            oldLanding.gameObject.SetActive(false);
            CompletionDeck(group,"C 하부 출구 정비대",122,127.5f,15,45,-8);
            var preview=EditorSceneManager.OpenPreviewScene(OriginalCannonPath);
            try
            {
                var source=preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HumanCannonPole>(true)).First();
                OriginalPole(group,source,"C05_ReboundTurningSwing",new Vector3(114.5f,1.5f,25),new Vector3(9,0,5).normalized,20);
                OriginalPole(group,source,"C05_ExitTurningSwing",new Vector3(119,-1,30),new Vector3(5,0,5).normalized,20);
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            FusionFloorMarks(group,new Vector3(40.5f,.02f,76),Vector3.forward);
            FusionFloorMarks(group,new Vector3(98.5f,.02f,25),Vector3.right);
            FusionFloorMarks(group,new Vector3(124,-7.98f,35),Vector3.forward);
            BLight(group,new Vector3(36,7,85),new Vector3(40.5f,0,87),360,18);
            BLight(group,new Vector3(103,4,22),new Vector3(109,-3.5f,25),400,23);
            BLight(group,new Vector3(126,0,38),new Vector3(124,-8,35),380,23);
            FinishVariantCompletion(group,new[]{rope1,rope2});
        }
    }
}
