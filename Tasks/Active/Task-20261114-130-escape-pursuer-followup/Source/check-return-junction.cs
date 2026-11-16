using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CurtainCall.EscapeRoute.EditorTools;
public static class CheckReturnJunction
{
    public static string LaunchBays(){EscapeBackgroundBuilder.RefineLabyrinthLaunchBays();return Audit();}
    public static string Finish(){EscapeBackgroundBuilder.FinishLabyrinthLaunchBays();var result=Audit();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return result;}
    public static string Choices(){EscapeBackgroundBuilder.BuildIndependentConveyorChoices();var result=Audit();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return result;}
    public static string FinishChoices(){EscapeBackgroundBuilder.FinishIndependentConveyorChoices();var result=Audit();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return result;}
    public static string Apply()
    {
        if(SceneManager.GetActiveScene().path!=EscapeBackgroundBuilder.LabyrinthScenePath)
        {
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Unsaved scene");
            EditorSceneManager.OpenScene(EscapeBackgroundBuilder.LabyrinthScenePath);
        }
        EscapeBackgroundBuilder.RefineLabyrinthReturnJunction();
        return Audit();
    }
    public static string Audit()
    {
        Physics.SyncTransforms();
        var frame=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
        var report=EscapeLabyrinthAudit.Audit(frame.Find("EscapeBackground/70_LabyrinthRemake"),frame);
        File.WriteAllText(".harness/local/pursuer-space-20261116/return-audit.json",JsonUtility.ToJson(report,true));
        return "failures="+report.failures.Count+" warnings="+report.warnings.Count+" colliders="+report.colliders+" ropes="+report.ropes+"\n"+string.Join("\n",report.failures);
    }
    public static string AuditAndSave(){var result=Audit();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return result;}
}
