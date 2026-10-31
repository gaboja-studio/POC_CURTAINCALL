using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CurtainCall.ItemArt.EditorTools
{
    public static class ItemArtBuilder
    {
        public const string Root = "Assets/Resources/ItemArt";
        public const string ScenePath = "Assets/Scenes/Tests/ItemArt/ItemInteractionGallery.unity";
        public static string ImportArt()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var palette = new Dictionary<string, Material>();
            foreach (string id in ItemVisualAssets.Ids)
            {
                string path = Root + "/Models/" + id + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importCameras = importer.importLights = importer.importAnimation = false;
                importer.SaveAndReimport();
                // Keep the FBX axis conversion on a child. Runtime attachment rotates
                // a canonical Unity Y-up root instead of overwriting the import rotation.
                var obj = new GameObject(id);
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),obj.transform,false);
                model.name="Model";
                try
                {
                    obj.name = id;
                    foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i=0;i<materials.Length;i++)
                        {
                            var original = materials[i]; string name = original.name;
                            if (!palette.TryGetValue(name,out var material))
                            {
                                string mp = Root+"/Materials/"+name+".mat";
                                material = AssetDatabase.LoadAssetAtPath<Material>(mp);
                                if(material==null)
                                {
                                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name=name;
                                    material.SetColor("_BaseColor",original.color); material.SetFloat("_Smoothness",.25f);
                                    if(name.Contains("Steel")||name.Contains("Brass"))material.SetFloat("_Metallic",.65f);
                                    if(name.Contains("Violet")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.35f,.02f,.8f)*2);}
                                    AssetDatabase.CreateAsset(material,mp);
                                }
                                palette[name]=material;
                            }
                            materials[i]=material;
                        }
                        renderer.sharedMaterials=materials;
                    }
                    PrefabUtility.SaveAsPrefabAsset(obj,Root+"/Prefabs/"+id+".prefab");
                }
                finally{UnityEngine.Object.DestroyImmediate(obj);}
                foreach(string suffix in new[]{"-shop","-slot"})
                {
                    var ti=(TextureImporter)AssetImporter.GetAtPath(Root+"/Icons/"+id+suffix+".png");
                    ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;
                    ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.spritePixelsPerUnit=512;ti.maxTextureSize=512;
                    ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
                }
            }
            AssetDatabase.SaveAssets();return ItemVisualAssets.Ids.Length+" model prefabs / "+ItemVisualAssets.Ids.Length*2+" sprites";
        }
        public static string ImportMotions()
        {
            var reference=(ModelImporter)AssetImporter.GetAtPath("Assets/Resources/Models/Characters/CannonMotion/TrampolineCarry.fbx");
            if(reference.sourceAvatar==null)throw new InvalidOperationException("Missing verified Rabbit source avatar");
            foreach(string motion in ItemVisualAssets.Motions)for(int stage=0;stage<4;stage++)
            {
                string name=ItemVisualAssets.State(motion,stage),path=Root+"/Motions/"+name+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar=reference.sourceAvatar;importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;
                importer.animationCompression=ModelImporterAnimationCompression.Off;
                importer.SaveAndReimport();
                var clips=importer.defaultClipAnimations;
                if(clips.Length!=1)throw new InvalidOperationException(name+" expected one take");
                var c=clips[0];c.name=name;c.loopTime=motion=="ItemAim"||motion=="ItemHold"||motion=="ItemCrawl";c.loopPose=c.loopTime;
                c.keepOriginalOrientation=true;c.keepOriginalPositionY=true;c.keepOriginalPositionXZ=true;
                c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;c.heightFromFeet=false;
                importer.clipAnimations=clips;importer.SaveAndReimport();
                var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(x=>!x.name.StartsWith("__preview"));
                if(!clip.humanMotion)throw new InvalidOperationException("Non-humanoid clip "+name);
            }
            var controllers=new HashSet<AnimatorController>();
            foreach(string character in CurtainCall.HumanCannon.CircusMotionGallery.Characters)
            {
                var p=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Prefabs/Characters/Players/Models/{character}/{character}.prefab");
                controllers.Add((AnimatorController)p.GetComponent<HumanoidModel>().Animator.runtimeAnimatorController);
            }
            var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(Root+"/ItemRightHand.mask");
            if(mask==null)
            {
                mask=new AvatarMask();
                AssetDatabase.CreateAsset(mask,Root+"/ItemRightHand.mask");
            }
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm,true);mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,true);EditorUtility.SetDirty(mask);
            foreach(var controller in controllers)
            {
                int index=Array.FindIndex(controller.layers,l=>l.name=="ItemRightHand");
                if(index<0){controller.AddLayer("ItemRightHand");index=controller.layers.Length-1;}
                var layers=controller.layers;layers[index].avatarMask=mask;layers[index].defaultWeight=0;controller.layers=layers;
                foreach(string motion in ItemVisualAssets.Motions)for(int stage=0;stage<4;stage++)
                {
                    string name=ItemVisualAssets.State(motion,stage);
                    var clip=AssetDatabase.LoadAllAssetsAtPath(Root+"/Motions/"+name+".fbx").OfType<AnimationClip>().Single(x=>!x.name.StartsWith("__preview"));
                    foreach(int layer in new[]{0,index})
                    {
                        var machine=controller.layers[layer].stateMachine;var state=machine.states.FirstOrDefault(x=>x.state.name==name).state;
                        if(state==null)state=machine.AddState(name);state.motion=clip;state.writeDefaultValues=true;
                    }
                }
                EditorUtility.SetDirty(controller);
            }
            InstallShopIcons();AssetDatabase.SaveAssets();return ItemVisualAssets.Motions.Length*4+" humanoid clips, "+controllers.Count+" controllers";
        }
        static void InstallShopIcons()
        {
            string path="Assets/Resources/Prefabs/UIs/ShopUi/ShopPanel.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponentInChildren<CurtainCall.UI.ShopV1.ShopPanelView>(true);var so=new SerializedObject(view);var list=so.FindProperty("iconBindings");
                foreach(string id in ItemVisualAssets.Ids)
                {
                    int index=-1;for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue==id)index=i;
                    if(index<0){index=list.arraySize;list.InsertArrayElementAtIndex(index);}
                    var item=list.GetArrayElementAtIndex(index);item.FindPropertyRelative("key").stringValue=id;
                    item.FindPropertyRelative("sprite").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Icons/"+id+"-shop.png");
                }
                so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        [MenuItem("CurtainCall/Validation/Open item interaction gallery")]
        public static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(ScenePath))BuildGallery();else EditorSceneManager.OpenScene(ScenePath);
        }
        public static string BuildGallery()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes first");
            if(File.Exists(ScenePath))throw new InvalidOperationException("Gallery exists; open it");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var gallery=new GameObject("Item Interaction Gallery").AddComponent<ItemInteractionGallery>();
            gallery.CharacterPrefabs=CurtainCall.HumanCannon.CircusMotionGallery.Characters.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Prefabs/Characters/Players/Models/{n}/{n}.prefab")).ToArray();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Rehearsal floor";floor.transform.position=new Vector3(4.5f,-.12f,4);floor.transform.localScale=new Vector3(16,.24f,18);
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=new Color(.09f,.12f,.15f);AssetDatabase.CreateAsset(mat,Root+"/Materials/GalleryFloor.mat");floor.GetComponent<Renderer>().sharedMaterial=mat;
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=8.7f;
            camera.transform.SetPositionAndRotation(new Vector3(4.5f,16,-13),Quaternion.Euler(45,0,0));camera.backgroundColor=new Color(.04f,.05f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;gallery.ViewCamera=camera;
            var light=new GameObject("Key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(45,-30,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.7f);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);return ScenePath;
        }
        [MenuItem("CurtainCall/Validation/Open item gameplay playground")]
        public static void OpenPlayground()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            string path="Assets/Scenes/Tests/ItemArt/ItemGameplayPlayground.unity";
            if(!File.Exists(path))BuildPlayground();else EditorSceneManager.OpenScene(path);
        }
        public static string BuildPlayground()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes");
            string path="Assets/Scenes/Tests/ItemArt/ItemGameplayPlayground.unity";if(File.Exists(path))throw new InvalidOperationException("Playground exists");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var playground=new GameObject("Item Gameplay Playground").AddComponent<ItemGameplayPlayground>();
            playground.CharacterPrefabs=CurtainCall.HumanCannon.CircusMotionGallery.Characters.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Prefabs/Characters/Players/Models/{n}/{n}.prefab")).ToArray();
            playground.Player=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Prefabs/Characters/Players/Player"));
            playground.Player.transform.position=new Vector3(0,.05f,-1.2f);
            var balance=new SerializedObject(playground.Player.GetComponent<PlayerBalance>());balance.FindProperty("activeOnStart").boolValue=false;balance.ApplyModifiedPropertiesWithoutUndo();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Playground floor";floor.transform.position=new Vector3(0,-.12f,1);floor.transform.localScale=new Vector3(24,.24f,20);floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/GalleryFloor.mat");
            new GameObject("Actual floor item views").AddComponent<CurtainCall.WaitingRoom.FloorPropsView>();
            new GameObject("Actual UV light").AddComponent<CurtainCall.WaitingRoom.UvFlashlight>();
            playground.Stations=new Transform[3];
            for(int i=0;i<3;i++)
            {
                var station=new GameObject(new[]{"Strike wall","Locked chest","Door"}[i]);station.transform.position=new Vector3((i-1)*4,0,1);playground.Stations[i]=station.transform;
                var leaf=GameObject.CreatePrimitive(PrimitiveType.Cube);leaf.transform.SetParent(station.transform,false);leaf.transform.localPosition=new Vector3(0,i==1?.45f:.9f,0);leaf.transform.localScale=i==1?new Vector3(1,.8f,.7f):new Vector3(1.4f,1.8f,.16f);
                leaf.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Kit_Red.mat");
                if(i<2)
                {
                    Component target=i==0?station.AddComponent<CurtainCall.WaitingRoom.BreakableWall>():station.AddComponent<CurtainCall.WaitingRoom.ToolTarget>();
                    var so=new SerializedObject(target);so.FindProperty("targetId").stringValue="item-art-088-"+i;so.FindProperty("blocker").objectReferenceValue=leaf.GetComponent<Collider>();
                    if(i==0){so.FindProperty("requiredHits").intValue=3;var a=so.FindProperty("pieces");a.arraySize=1;a.GetArrayElementAtIndex(0).objectReferenceValue=leaf.transform;}
                    else so.FindProperty("hinge").objectReferenceValue=leaf.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else station.AddComponent<ItemPracticeDoor>().Leaf=leaf.transform;
            }
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=48;camera.backgroundColor=new Color(.05f,.06f,.09f);camera.clearFlags=CameraClearFlags.SolidColor;playground.ViewCamera=camera;
            var light=new GameObject("Key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(45,-30,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.7f);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,path);return path;
        }
    }
}
