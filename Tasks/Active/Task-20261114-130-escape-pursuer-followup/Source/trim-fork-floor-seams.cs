using System.IO;
using CurtainCall.EscapeRoute.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// run_script: ForkFloorSeamRepair.Inspect() then ForkFloorSeamRepair.Apply().
// No scene opening, asset edits, NavMesh bake or shared-mesh rebatching.
public static class ForkFloorSeamRepair
{
    const string Output = ".harness/local/pursuer-space-20261116/fork-floor-seams.json";
    public static object Inspect() => Write(EscapeBackgroundBuilder.InspectLabyrinthForkFloorSeams());
    public static object Apply()
    {
        var report = EscapeBackgroundBuilder.TrimLabyrinthForkFloorSeams();
        if (report.applied) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return Write(report);
    }
    static object Write(EscapeBackgroundBuilder.ForkFloorSeamReport report)
    {
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        return report;
    }
}
