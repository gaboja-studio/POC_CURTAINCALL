using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    public sealed class PerformancePlacementTests
    {
        [TestCase(CheerPointStyle.FireHoop),TestCase(CheerPointStyle.EncoreHalo),TestCase(CheerPointStyle.MaskApparition)]
        [Category("PerformancePlacement")]
        public void HangingVisualDipsRisesAndNeverDisappearsWhileVisible(CheerPointStyle style)
        {
            var root=Object.Instantiate(GameSettings.PerformanceEquipment.Visual(style));
            var view=root.AddComponent<CheerPointPresentation>();
            var camera=new GameObject("Test camera").AddComponent<Camera>();
            try
            {
                camera.transform.position=new Vector3(0,0,-10); camera.farClipPlane=100;
                view.Sample(0,false,camera); var origin=root.transform.position; var scale=root.transform.localScale;
                var rope=root.GetComponentInChildren<CheerHangingRope>();
                Assert.Greater(Vector3.Distance(rope.End,camera.transform.position),camera.farClipPlane);
                Assert.IsNotNull(rope.GetComponent<MeshFilter>().sharedMesh,"The owned braided rope is used, not a plain line.");
                Assert.IsTrue(GameSettings.PerformanceEquipment.cheerRopeMesh.isReadable,"Rope combining must work in a player build.");
                foreach(var particle in root.GetComponentsInChildren<ParticleSystem>())
                    if(particle.shape.shapeType==ParticleSystemShapeType.MeshRenderer)
                        Assert.IsTrue(particle.shape.meshRenderer.GetComponent<MeshFilter>().sharedMesh.isReadable,"Mesh-surface flames require readable geometry in a player build.");
                view.Sample(GameSettings.PerformanceEquipment.cheerDipSeconds,true,camera);
                Assert.Less(root.transform.position.y,origin.y); Assert.IsTrue(root.activeSelf);
                float rise=CheerPointPresentation.RiseOffset(3,GameSettings.PerformanceEquipment);
                camera.transform.position=new Vector3(0,rise,-10);
                view.Sample(3,true,camera);
                Assert.IsFalse(view.Hidden,"Even above minimum height, a camera still looking at the ring must see it.");
                Assert.AreEqual(scale,root.transform.localScale,"Collection cannot shrink the ring.");
                camera.transform.position=new Vector3(0,0,-10); view.Sample(3,true,camera);
                Assert.IsTrue(view.Hidden,"Once the ring, fire and cable leave the view, remove it.");
                view.Sample(0,false,camera); Assert.IsTrue(root.activeSelf); Assert.AreEqual(origin,root.transform.position);
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(camera.gameObject);}
        }

        [Test,Category("PerformancePlacement")]
        public void TightropeIndependentLayoutsPreserveStageAdditionsAndSurviveSerialization()
        {
            var settings=Object.Instantiate(GameSettings.Tightrope);
            var reload=ScriptableObject.CreateInstance<TightropeSettings>();
            try
            {
                string one=JsonUtility.ToJson(settings.SawsFor(1));
                string three=JsonUtility.ToJson(settings.SawsFor(3));
                int baseCount=settings.SawsFor(2).BaseHorizontalCount;
                var layout=settings.CreatePlacement(2);
                Assert.AreEqual(baseCount,layout.saws.BaseHorizontalCount);
                var first=layout.saws.horizontalSaws[0]; first.distance+=20; first.excluded=true; layout.saws.horizontalSaws[0]=first;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(settings),reload);
                Assert.AreEqual(one,JsonUtility.ToJson(reload.SawsFor(1)));
                Assert.AreEqual(three,JsonUtility.ToJson(reload.SawsFor(3)));
                Assert.IsTrue(reload.SawsFor(2).horizontalSaws[0].excluded);
                Assert.AreEqual(first.distance,reload.SawsFor(2).horizontalSaws[0].distance);
            }
            finally {Object.DestroyImmediate(settings);Object.DestroyImmediate(reload);}
        }

        [TestCase("Tightrope"),TestCase("HumanCannon"),Category("PerformancePlacement")]
        public void SixCheerContextsAddDuplicateRemoveWithoutChangingOtherDifficulties(string trick)
        {
            string path=trick=="Tightrope"?PerformanceEquipmentAuthoring.RopeScene:PerformanceEquipmentAuthoring.CannonScene;
            var prior=SceneManager.GetActiveScene(); var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            var window=ScriptableObject.CreateInstance<PerformanceCheerWindow>();
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
            try
            {
                for(int level=1;level<=3;level++)
                {
                    window.SetContext(trick,level);
                    var point=window.AddAt(new Vector3(level,10,20));
                    Assert.AreEqual(1<<(level-1),point.difficultyMask);
                    window.SetSelectedScale(.5f*level);
                    float radius=GameSettings.PerformanceEquipment.hoopRadius*.5f*level;
                    Assert.IsTrue(point.Crossed(point.transform.position+point.transform.right*(radius*.99f)-point.transform.forward,
                        point.transform.position+point.transform.right*(radius*.99f)+point.transform.forward));
                    Assert.IsFalse(point.Crossed(point.transform.position+point.transform.right*(radius*1.01f)-point.transform.forward,
                        point.transform.position+point.transform.right*(radius*1.01f)+point.transform.forward));
                    var duplicate=window.DuplicateSelected();
                    Assert.AreNotEqual(point.pointId,duplicate.pointId);
                    Assert.AreEqual(point.transform.localScale,duplicate.transform.localScale);
                    window.RemoveSelected(); Assert.IsTrue(duplicate==null);
                    Assert.IsTrue(point.AppliesToDifficulty(level));
                    Assert.IsFalse(point.AppliesToDifficulty(level%3+1));
                }
            }
            finally {Object.DestroyImmediate(window);Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(group);EditorSceneManager.CloseScene(scene,true);if(prior.isLoaded) SceneManager.SetActiveScene(prior);}
        }

        [Test,Category("PerformancePlacement")]
        public void DevicePlacementRegistersCannonAndPersistsIndependentPosesWithoutPreviewObjects()
        {
            var prior=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene(PerformanceEquipmentAuthoring.CannonScene,OpenSceneMode.Additive);
            var window=ScriptableObject.CreateInstance<PerformanceCheerWindow>();
            string temporary="Assets/Scenes/Tests/MainStage/CheerPoints/placement-test-"+Guid.NewGuid().ToString("N")+".unity";
            Scene saved=default;
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
            try
            {
                window.SetDeviceContext("HumanCannon",2);
                var transport=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HumanCannonTransport>(true)).First(t=>t.IsConfigured && t.gameObject.activeInHierarchy);
                Assert.IsTrue(transport.ContainsAirColumn(transport.Platform.position));
                transport.gameObject.SetActive(false);
                Assert.IsFalse(transport.ContainsAirColumn(transport.Platform.position),"Excluded lifts cannot claim passengers.");
                transport.gameObject.SetActive(true);
                var fire=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HumanCannonFireWall>(true)).First();
                bool fireWasActive=fire.gameObject.activeSelf; fire.gameObject.SetActive(false);
                Assert.IsFalse(fire.Available); Assert.IsFalse(fire.Advancing(1000)); Assert.IsFalse(fire.Threatens(fire.transform.position,.3f,1000));
                fire.gameObject.SetActive(fireWasActive);
                var station=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HumanCannonStation>(true)).First();
                var system=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HumanCannonSystem>(true)).Single();
                int count=system.StationCount;
                var added=window.AddDevice(station.gameObject,new Vector3(8,12,30));
                Assert.AreEqual(count+1,system.StationCount); Assert.AreSame(added.GetComponent<HumanCannonStation>(),system.Station(count));
                window.SetDevicePose(added.transform,new Vector3(9,14,31),Quaternion.Euler(0,30,0),Vector3.one,true);
                var layout=scene.GetRootGameObjects().Select(g=>g.GetComponent<PerformanceLayout>()).Single(c=>c);
                Assert.IsFalse(layout.Find(added.transform).levels[0].active); Assert.IsTrue(layout.Find(added.transform).levels[1].active);
                window.SetDeviceContext("HumanCannon",3); layout.Apply(3); Assert.IsFalse(added.activeSelf);
                window.SetDeviceContext("HumanCannon",2); layout.Apply(2); Assert.AreEqual(new Vector3(9,14,31),added.transform.position);
                EditorSceneManager.SaveScene(scene,temporary,true);
                Assert.IsFalse(System.IO.File.ReadAllText(temporary).Contains("CheerPlacementPreview"));
                saved=EditorSceneManager.OpenScene(temporary,OpenSceneMode.Additive);
                var loaded=saved.GetRootGameObjects().Select(g=>g.GetComponent<PerformanceLayout>()).Single(c=>c);
                loaded.Apply(2); var entry=loaded.entries.Last(); Assert.AreEqual(new Vector3(9,14,31),entry.target.position);
                loaded.Apply(1); Assert.IsFalse(entry.target.gameObject.activeSelf);
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); // Edits participate in the editor undo stack.
            }
            finally
            {
                Object.DestroyImmediate(window);Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(group);
                if(saved.isLoaded) EditorSceneManager.CloseScene(saved,true);
                EditorSceneManager.CloseScene(scene,true);AssetDatabase.DeleteAsset(temporary);if(prior.isLoaded) SceneManager.SetActiveScene(prior);
            }
        }
    }
}
