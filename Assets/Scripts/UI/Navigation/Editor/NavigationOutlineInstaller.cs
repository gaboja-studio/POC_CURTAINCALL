using System.Linq;
using CurtainCall.UI.SceneIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.UI.Wayfinding.Editor
{
    public static class NavigationOutlineInstaller
    {
        [MenuItem("CurtainCall/UI/Navigation/Install silhouette renderer")]
        public static string Install()
        {
            int count=0;
            foreach(var path in new[]{"Assets/Settings/PC_Renderer.asset","Assets/Settings/Mobile_Renderer.asset"})
            {
                var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if(data==null)throw new System.InvalidOperationException("Missing renderer: "+path);
                if(!data.rendererFeatures.OfType<SilhouetteOutlineFeature>().Any())
                {
                    var feature=ScriptableObject.CreateInstance<SilhouetteOutlineFeature>();feature.name="Camera silhouette outlines";
                    AssetDatabase.AddObjectToAsset(feature,data);data.rendererFeatures.Add(feature);feature.Create();
                    EditorUtility.SetDirty(feature);EditorUtility.SetDirty(data);data.SetDirty();
                    count++;
                }
                // Keep URP's recovery map in step with the feature list and its subasset IDs.
                var serialized=new SerializedObject(data);
                var map=serialized.FindProperty("m_RendererFeatureMap");
                map.arraySize=data.rendererFeatures.Count;
                for(int i=0;i<data.rendererFeatures.Count;i++)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i],out string guid,out long id);
                    map.GetArrayElementAtIndex(i).longValue=id;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(data);
            }
            return "Silhouette renderer installed; added="+count;
        }
    }
}
