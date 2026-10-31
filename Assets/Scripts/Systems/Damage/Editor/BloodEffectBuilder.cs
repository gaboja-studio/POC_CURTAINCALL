using System.IO;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Damage.EditorTools
{
    /// <summary>
    /// 혈흔 파티클 기본 에셋을 만든다(Task-090): 방울 텍스처·재질·프리팹. 런타임은 프리팹을 Resources에서 읽는다.
    /// 프리팹이 이미 있으면 덮어쓰지 않는다(아트에서 다듬은 모양 보존). 다시 만들려면 프리팹을 지운 뒤 실행한다.
    /// </summary>
    static class BloodEffectBuilder
    {
        const string Folder = "Assets/Resources/Effects/Blood";
        const string TexturePath = Folder + "/BloodDrop.png";
        const string MaterialPath = Folder + "/BloodParticle.mat";
        const string PrefabPath = Folder + "/BloodSpurt.prefab";

        [MenuItem("Tools/CurtainCall/Effects/혈흔 파티클 기본 에셋 만들기")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log($"[BloodEffect] {PrefabPath}가 이미 있어 그대로 둡니다. 다시 만들려면 지운 뒤 실행하세요.");
                return;
            }
            Directory.CreateDirectory(Folder);

            var drop = InjuryBloodEffects.CreateDropTexture(64);
            File.WriteAllBytes(TexturePath, drop.EncodeToPNG());
            Object.DestroyImmediate(drop);
            AssetDatabase.ImportAsset(TexturePath);
            if (AssetImporter.GetAtPath(TexturePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = InjuryBloodEffects.CreateMaterial(texture);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var go = InjuryBloodEffects.BuildDefault(material, Vector3.zero, Quaternion.identity);
            try { PrefabUtility.SaveAsPrefabAsset(go, PrefabPath); }
            finally { Object.DestroyImmediate(go); }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BloodEffect] 혈흔 기본 에셋 생성: {PrefabPath}");
        }
    }
}
