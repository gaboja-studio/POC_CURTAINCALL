using UnityEditor;

namespace CurtainCall.UI.Wayfinding.Editor
{
    sealed class NavigationStairMeshImport : AssetPostprocessor
    {
        // StairModulePrepared uses this model as its collision surface. Runtime NavMesh baking
        // needs CPU access to those triangles, including in the built player.
        const string StairMesh = "Assets/_ThirdParty/Synty/PolygonGeneric/Models/Base/SM_Bld_Base_Stairs_01.fbx";

        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (assetPath == StairMesh) ((ModelImporter)assetImporter).isReadable = true;
        }
    }
}
