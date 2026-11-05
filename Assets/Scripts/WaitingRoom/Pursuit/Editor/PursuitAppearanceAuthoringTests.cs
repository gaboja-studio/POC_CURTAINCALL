using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitAppearanceAuthoringTests
    {
        const string PrefabPath = "Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/Pursuit_Male_02.prefab";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        Scene scene;
        PursuitRoom room;
        GameObject prefab;
        [SetUp] public void Setup()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            room = new GameObject("AppearanceRoom").AddComponent<PursuitRoom>();
            room.Area = room.gameObject.AddComponent<BoxCollider>(); room.Area.isTrigger = true;
            room.Area.center = Vector3.up * 2; room.Area.size = new Vector3(10, 4, 10);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
        }
        [TearDown] public void Teardown()
        {
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        }
        static object Call(PursuitDesignerWindow window, string name, params object[] args)
            => typeof(PursuitDesignerWindow).GetMethod(name, Private).Invoke(window, args);

        [Test] public void ChoiceAndPatternUndoRedoAndSceneReloadPreservePlacement()
        {
            var item = PursuitAuthoring.Add(room, prefab, new Vector3(1, .015f, 2), false);
            var route = PursuitAuthoring.AddPoint(item, new Vector3(3, .015f, 4));
            item.Size = PursuitPlacement.BodySize.Medium; item.ActivationStage = 3;
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            var so = new SerializedObject(item);
            so.FindProperty("Appearance").enumValueIndex = (int)PursuitAppearanceChoice.HeavyBlood;
            so.FindProperty("AppearancePattern").intValue = 9; so.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.AreEqual(PursuitAppearanceChoice.Varied, item.Appearance); Assert.AreEqual(0, item.AppearancePattern);
            Undo.PerformRedo(); Assert.AreEqual(PursuitSurfaceState.HeavyBlood, item.ResolvedSurface);
            int seed = item.ResolvedSurfaceSeed; var routePosition = route.position;
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Tests/PursuitAppearanceFixture.unity");
            try {
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, path));
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                item = Object.FindFirstObjectByType<PursuitPlacement>();
                Assert.AreEqual(PursuitAppearanceChoice.HeavyBlood, item.Appearance); Assert.AreEqual(9, item.AppearancePattern);
                Assert.AreEqual(seed, item.ResolvedSurfaceSeed); Assert.AreSame(prefab, item.Prefab);
                Assert.AreEqual(new Vector3(1, .015f, 2), item.transform.position);
                Assert.AreEqual(routePosition, item.PatrolPoints.Single().position);
                Assert.AreEqual(3, item.ActivationStage); Assert.AreEqual(PursuitPlacement.BodySize.Medium, item.Size);
            } finally {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(path);
            }
        }
        [Test] public void AutomaticVariationIsDeterministicAcrossReplicaRootsAndMovement()
        {
            var choices = new HashSet<PursuitSurfaceState>(); var seeds = new HashSet<int>();
            for (int i = 0; i < 20; i++) {
                var item = PursuitAuthoring.Add(room, prefab, new Vector3(i, 0, 0), false);
                choices.Add(item.ResolvedSurface); seeds.Add(item.ResolvedSurfaceSeed);
            }
            Assert.GreaterOrEqual(choices.Count, 4); Assert.GreaterOrEqual(seeds.Count, 18);
            var container = new GameObject("ReplicaRoot");
            var clone = Object.Instantiate(room.gameObject, container.transform); clone.name = room.name;
            var copy = clone.GetComponent<PursuitRoom>();
            for (int i = 0; i < room.Placements.Length; i++) {
                Assert.AreEqual(room.Placements[i].ResolvedSurface, copy.Placements[i].ResolvedSurface);
                Assert.AreEqual(room.Placements[i].ResolvedSurfaceSeed, copy.Placements[i].ResolvedSurfaceSeed);
            }
            var first = room.Placements[0]; int before = first.ResolvedSurfaceSeed;
            var state = first.ResolvedSurface;
            first.transform.position += Vector3.one * 20; first.transform.Rotate(0, 90, 0);
            Assert.AreEqual(before, first.ResolvedSurfaceSeed);
            first.AppearancePattern = 8;
            Assert.AreEqual(state, first.ResolvedSurface); Assert.AreNotEqual(before, first.ResolvedSurfaceSeed);
        }
        [Test] public void DesignerPreviewMatchesSelectionWithoutCloningMeshOrMaterial()
        {
            var a = PursuitAuthoring.Add(room, prefab, Vector3.zero, false);
            var b = PursuitAuthoring.Add(room, prefab, Vector3.right, false);
            a.Appearance = PursuitAppearanceChoice.Clean; b.Appearance = PursuitAppearanceChoice.HeavyBlood;
            var director = new GameObject("Director").AddComponent<PursuitDirector>();
            director.Rooms = new[] { room }; director.Mannequins = new[] { prefab };
            var window = ScriptableObject.CreateInstance<PursuitDesignerWindow>();
            try {
                Call(window, "Refresh"); Call(window, "EnsureView", new Rect(0, 0, 400, 400));
                Call(window, "PreparePreview", new Rect(0, 0, 400, 400));
                var previews = (Dictionary<PursuitPlacement, GameObject>)typeof(PursuitDesignerWindow).GetField("previews", Private).GetValue(window);
                var clean = previews[a].GetComponentInChildren<SkinnedMeshRenderer>();
                var bloody = previews[b].GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.AreSame(clean.sharedMesh, bloody.sharedMesh); Assert.AreSame(clean.sharedMaterial, bloody.sharedMaterial);
                var props = new MaterialPropertyBlock(); clean.GetPropertyBlock(props); Assert.AreEqual(0, props.GetFloat("_BaseBlood"));
                bloody.GetPropertyBlock(props); Assert.AreEqual(1, props.GetFloat("_BaseBlood"));
                Assert.AreEqual(b.ResolvedSurfaceSeed, props.GetFloat("_VariantSeed"));
                b.Appearance = PursuitAppearanceChoice.LightBlood; b.AppearancePattern = 4;
                Call(window, "PreparePreview", new Rect(0, 0, 400, 400));
                bloody.GetPropertyBlock(props); Assert.AreEqual(.25f, props.GetFloat("_BaseBlood"));
                Assert.AreEqual(b.ResolvedSurfaceSeed, props.GetFloat("_VariantSeed"));
                b.Appearance = PursuitAppearanceChoice.Prefab; b.AppearancePattern = 0;
                Call(window, "PreparePreview", new Rect(0, 0, 400, 400));
                var original = prefab.GetComponent<PursuitAppearance>();
                bloody.GetPropertyBlock(props); Assert.AreEqual(original.AuthoredBloodAmount, props.GetFloat("_BaseBlood"));
                Assert.AreEqual(original.SurfaceSeed, props.GetFloat("_VariantSeed"));
            } finally { Object.DestroyImmediate(window); }
        }

        [Test] public void IndependentLayersPersistAndUndoWithoutChangingLegacyChoices()
        {
            var item = PursuitAuthoring.Add(room, prefab, Vector3.zero, false);
            item.Appearance = PursuitAppearanceChoice.HeavyBlood; item.AppearancePattern = 6;
            Assert.IsFalse(item.CustomSurfaceLayers);
            Assert.AreEqual(.65f, item.ResolvedDirtAmount); Assert.AreEqual(1, item.ResolvedBloodAmount);
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            var so = new SerializedObject(item);
            so.FindProperty("CustomSurfaceLayers").boolValue = true;
            so.FindProperty("DirtAmount").floatValue = .9f; so.FindProperty("WearAmount").floatValue = .8f;
            so.FindProperty("BloodAmount").floatValue = .15f; so.FindProperty("WearPattern").intValue = 3;
            so.FindProperty("AppearancePattern").intValue = 12; so.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.IsFalse(item.CustomSurfaceLayers); Assert.AreEqual(6, item.AppearancePattern);
            Assert.AreEqual(1, item.ResolvedBloodAmount);
            Undo.PerformRedo(); Assert.IsTrue(item.CustomSurfaceLayers);
            int wearSeed = item.ResolvedWearSeed; int bloodSeed = item.ResolvedSurfaceSeed;
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Tests/PursuitLayerFixture.unity");
            try {
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, path));
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                item = Object.FindFirstObjectByType<PursuitPlacement>();
                Assert.AreEqual(PursuitAppearanceChoice.HeavyBlood, item.Appearance, "Keep the old preset as the base recipe.");
                Assert.AreEqual(.9f, item.ResolvedDirtAmount); Assert.AreEqual(.8f, item.ResolvedWearAmount);
                Assert.AreEqual(.15f, item.ResolvedBloodAmount);
                Assert.AreEqual(wearSeed, item.ResolvedWearSeed); Assert.AreEqual(bloodSeed, item.ResolvedSurfaceSeed);
                item.AppearancePattern = 1;
                Assert.AreEqual(wearSeed, item.ResolvedWearSeed, "Changing blood must not slide the wear pattern.");
                int changedBlood = item.ResolvedSurfaceSeed;
                item.WearPattern = 11;
                Assert.AreEqual(changedBlood, item.ResolvedSurfaceSeed, "Changing wear must not slide blood.");
            } finally {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(path);
            }
        }
        [Test] public void LayerCombinationAndBloodHidePreserveSkinWearAndSharedAssets()
        {
            var item = PursuitAuthoring.Add(room, prefab, Vector3.zero, false); item.CustomSurfaceLayers = true;
            var go = Object.Instantiate(prefab);
            try {
                var look = go.GetComponent<PursuitAppearance>(); var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
                var mesh = skin.sharedMesh; var material = skin.sharedMaterial;
                var props = new MaterialPropertyBlock();
                foreach(float dirt in new[]{0f,.9f}) foreach(float wear in new[]{0f,.8f}) foreach(float blood in new[]{0f,.7f}) {
                    item.DirtAmount=dirt; item.WearAmount=wear; item.BloodAmount=blood;
                    item.ApplyAppearance(look); skin.GetPropertyBlock(props);
                    Assert.AreEqual(dirt,props.GetFloat("_DirtAmount")); Assert.AreEqual(wear,props.GetFloat("_WearAmount"));
                    Assert.AreEqual(blood,props.GetFloat("_BaseBlood")); Assert.AreSame(mesh,skin.sharedMesh); Assert.AreSame(material,skin.sharedMaterial);
                }
                item.WearPattern=3; item.AppearancePattern=9; item.ApplyAppearance(look);
                int wearSeed=item.ResolvedWearSeed; int bloodSeed=item.ResolvedSurfaceSeed;
                look.SetCaptureCount(3); look.RefreshSurface(false); skin.GetPropertyBlock(props);
                Assert.AreEqual(0,props.GetFloat("_BloodVisible")); Assert.AreEqual(0,props.GetFloat("_BloodAmount"));
                Assert.AreEqual(.9f,props.GetFloat("_DirtAmount")); Assert.AreEqual(.8f,props.GetFloat("_WearAmount"));
                Assert.AreEqual(wearSeed,props.GetFloat("_WearSeed")); Assert.AreEqual(bloodSeed,props.GetFloat("_VariantSeed"));
            } finally {Object.DestroyImmediate(go);}
        }
    }
}
