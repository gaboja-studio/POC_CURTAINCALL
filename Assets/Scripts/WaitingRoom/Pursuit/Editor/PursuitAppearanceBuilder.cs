using UnityEditor;
namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>Reapply the current art without regenerating existing motion clips.</summary>
    public static class PursuitAppearanceBuilder
    {
        [MenuItem("Tools/Curtain Call/Pursuit/Build reclaimed troupe appearance")]
        public static void Build() => PursuitTroupeV7Importer.Build();
    }
}
