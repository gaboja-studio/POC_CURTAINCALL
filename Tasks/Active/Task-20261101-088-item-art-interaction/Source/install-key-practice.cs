if(UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop play mode");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path!="Assets/Scenes/Tests/ItemArt/ItemGameplayPlayground.unity")throw new System.InvalidOperationException("Wrong scene");
var p=UnityEngine.Object.FindFirstObjectByType<CurtainCall.ItemArt.ItemGameplayPlayground>();
if(p.KeyPickup==null)
{
    var stand=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);stand.name="Key pedestal";stand.transform.position=new UnityEngine.Vector3(-4,.3f,-4);stand.transform.localScale=new UnityEngine.Vector3(.5f,.6f,.5f);
    var key=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);key.name="Waiting-room key pickup";key.transform.position=new UnityEngine.Vector3(-4,.65f,-4);key.transform.localScale=UnityEngine.Vector3.one*.08f;
    var gate=key.AddComponent<CurtainCall.WaitingRoom.ExplorationGate>();gate.ConfigureRoute(CurtainCall.WaitingRoom.StructureAKeyRoute.Key1);
    var serialized=new UnityEditor.SerializedObject(gate);serialized.FindProperty("label").stringValue="열쇠 줍기";serialized.ApplyModifiedPropertiesWithoutUndo();p.KeyPickup=key.transform;
}
if(p.KeyDoor==null)
{
    var door=new UnityEngine.GameObject("Waiting-room key lock practice");door.transform.position=new UnityEngine.Vector3(-1,0,-4);
    var leaf=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);leaf.transform.SetParent(door.transform,false);leaf.transform.localPosition=new UnityEngine.Vector3(0,.9f,0);leaf.transform.localScale=new UnityEngine.Vector3(1.2f,1.8f,.12f);
    leaf.GetComponent<UnityEngine.Renderer>().sharedMaterial=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Resources/ItemArt/Materials/Kit_Red.mat");
    var slot=new UnityEngine.GameObject("Keyhole");slot.transform.SetParent(door.transform,false);slot.transform.localPosition=new UnityEngine.Vector3(.3f,.9f,-.09f);
    var gate=door.AddComponent<CurtainCall.WaitingRoom.ExplorationGate>();gate.ConfigureRoute("item-art-088:key-door",new[]{CurtainCall.WaitingRoom.StructureAKeyRoute.Key1});
    var serialized=new UnityEditor.SerializedObject(gate);serialized.FindProperty("label").stringValue="열쇠로 문 열기";serialized.FindProperty("interactionPoint").objectReferenceValue=slot.transform;serialized.FindProperty("blocker").objectReferenceValue=leaf.GetComponent<UnityEngine.Collider>();var hide=serialized.FindProperty("hideOnOpen");hide.arraySize=1;hide.GetArrayElementAtIndex(0).objectReferenceValue=leaf;serialized.ApplyModifiedPropertiesWithoutUndo();p.KeyDoor=door.transform;
}
UnityEditor.EditorUtility.SetDirty(p);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{key=p.KeyPickup.name,door=p.KeyDoor.name};
