if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Stop play mode first");
for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new System.Exception("Unsaved scene changes");
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Tests/MainStage/HumanCannon/CircusMotionGallery.unity");
var floor=scene.GetRootGameObjects().First(g=>g.name=="Gallery Floor");
UnityEditor.Undo.RecordObject(floor.transform,"Align gallery support floor");
var p=floor.transform.position;p.y=-floor.transform.localScale.y*.5f;floor.transform.position=p;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))throw new System.Exception("Could not save gallery");
return new{scene=scene.path,floorTop=floor.transform.position.y+floor.transform.localScale.y*.5f};
