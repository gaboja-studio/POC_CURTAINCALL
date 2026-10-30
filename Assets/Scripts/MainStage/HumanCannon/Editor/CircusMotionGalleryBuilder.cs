using System;
using System.IO;
using CurtainCall.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    public static class CircusMotionGalleryBuilder
    {
        public const string ScenePath="Assets/Scenes/Tests/MainStage/HumanCannon/CircusMotionGallery.unity";
        [MenuItem("CurtainCall/Validation/Open circus motion gallery (44 clips)")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath))Build();else EditorSceneManager.OpenScene(ScenePath);
        }
        public static string Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save existing scenes first.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("Gallery already exists; open the saved scene.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Circus Motion Gallery");var gallery=root.AddComponent<CircusMotionGallery>();
            gallery.CharacterPrefabs=new GameObject[4];
            for(int c=0;c<4;c++)
            {
                string name=CircusMotionGallery.Characters[c];
                gallery.CharacterPrefabs[c]=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Prefabs/Characters/Players/Models/{name}/{name}.prefab");
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Gallery Floor";
            floor.transform.position=new Vector3(4.5f,-.12f,5.1f);floor.transform.localScale=new Vector3(14,.2f,16);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=10;
            camera.transform.SetPositionAndRotation(new Vector3(4.5f,19,-13),Quaternion.Euler(48,0,0));
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.1f,.15f);
            gallery.ViewCamera=camera;
            var light=new GameObject("Gallery Key Light").AddComponent<Light>();light.type=LightType.Directional;
            light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.64f,.72f);

            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Gallery scene save failed.");
            Selection.activeGameObject=root;return ScenePath;
        }
    }
}
