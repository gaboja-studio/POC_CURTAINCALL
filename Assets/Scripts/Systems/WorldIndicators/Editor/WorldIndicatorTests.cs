using System.Linq;
using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace CurtainCall.WorldIndicators.EditorTools
{
    public sealed class WorldIndicatorTests
    {
        WorldUiSettings original, settings;
        GameObject owner;
        readonly FieldInfo link=typeof(GameSettings).GetField("worldUi",BindingFlags.Instance|BindingFlags.NonPublic);
        [SetUp] public void Setup()
        {
            original=(WorldUiSettings)link.GetValue(GameSettings.Current);
            settings=Object.Instantiate(original); settings.maxDistance=100000; settings.enabled=true;
            link.SetValue(GameSettings.Current,settings); owner=new GameObject("World UI test");
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(owner); link.SetValue(GameSettings.Current,original); Object.DestroyImmediate(settings);
        }
        Mesh Mesh(WorldIndicatorView view,string layer)=>view.transform.Find(layer).GetComponent<MeshFilter>().sharedMesh;
        [Test] public void CanonicalKitHasLinkedIndependentLayersAndNoPhysicsOrShadowCasters()
        {
            Assert.That(original,Is.Not.Null); Assert.That(settings.style.layers.Length,Is.EqualTo(9));
            Assert.That(settings.style.layers.Select(AssetDatabase.GetAssetPath).Distinct().Count(),Is.EqualTo(9));
            foreach(var prefab in new WorldIndicatorView[]{settings.circlePrefab,settings.rectanglePrefab,settings.pathPrefab,settings.volumePrefab,settings.trajectoryPrefab,settings.targetPrefab})
            {
                Assert.That(prefab,Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).All(r=>r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off),Is.True);
                Assert.That(prefab.transform.Find("Arrow"),Is.Not.Null);
            }
        }
        [TestCase(false)][TestCase(true)]
        public void BodyClearancePreservesProjectedReticleAndProgress(bool orthographic)
        {
            var existing=Camera.main;
            bool wasEnabled=existing && existing.enabled;
            if(existing) existing.enabled=false;
            try
            {
                var cameraObject=new GameObject("Target projection camera"); cameraObject.transform.SetParent(owner.transform);
                cameraObject.tag="MainCamera";
                var camera=cameraObject.AddComponent<Camera>(); camera.orthographic=orthographic;
                var view=WorldIndicatorFactory.Target(owner.transform);
                Vector3 aim=new(2,1,8);
                view.Show(aim,.5f,false);
                foreach(string layer in new[]{"Boundary","Progress"})
                {
                    view.Show(aim,.5f,false);
                    var originalPoints=Mesh(view,layer).vertices.Select(camera.WorldToViewportPoint).ToArray();
                    view.Show(aim,.5f,false,1.5f);
                    var pulled=Mesh(view,layer).vertices.Select(camera.WorldToViewportPoint).ToArray();
                    Assert.That(pulled.Length,Is.EqualTo(originalPoints.Length));
                    for(int i=0;i<pulled.Length;i++)
                    {
                        Assert.That(Vector2.Distance(pulled[i],originalPoints[i]),Is.LessThan(.0001f));
                        Assert.That(pulled[i].z,Is.LessThan(originalPoints[i].z));
                    }
                }
            }
            finally { if(existing) existing.enabled=wasEnabled; }
        }

        [Test] public void CountdownChangesProgressButNeverDamageBounds()
        {
            var view=WorldIndicatorFactory.Area(owner.transform,true);
            view.Show(new Vector3(0,2,0),Quaternion.identity,Vector2.one*4,.25f);
            var bound=Mesh(view,"Boundary").vertices; var fill=Mesh(view,"Fill").bounds;
            int first=Mesh(view,"Progress").vertexCount;
            view.Show(new Vector3(0,2,0),Quaternion.identity,Vector2.one*4,.95f);
            Assert.That(Mesh(view,"Boundary").vertices,Is.EqualTo(bound));
            Assert.That(Mesh(view,"Fill").bounds,Is.EqualTo(fill));
            Assert.That(fill.size.x,Is.EqualTo(4).Within(.001));
            Assert.That(Mesh(view,"Progress").vertexCount,Is.GreaterThan(first));
            Assert.That(view.transform.Find("Icon"),Is.Null);
        }
        [Test] public void AirborneSweepPreservesElevationRadiusAndHidesCountdownDuringStrike()
        {
            var view=WorldIndicatorFactory.Volume(owner.transform);
            view.Show(new Vector3(-4,3,0),new Vector3(4,3,0),.5f,.5f);
            var b=Mesh(view,"Boundary").bounds;
            Assert.That(b.min.y,Is.GreaterThan(2.4)); Assert.That(b.max.y,Is.LessThan(3.7));
            Assert.That(b.min.x,Is.EqualTo(-4.5f).Within(.05)); Assert.That(b.max.x,Is.EqualTo(4.5f).Within(.05));
            view.Show(new Vector3(1,3,0),new Vector3(4,3,0),.5f,1,false);
            Assert.That(Mesh(view,"Boundary").bounds.min.x,Is.GreaterThan(.45));
            Assert.That(Mesh(view,"Progress").vertexCount,Is.Zero);
        }
        [Test] public void DisabledAndInvalidViewsCannotLeaveStaleGeometryVisible()
        {
            var view=WorldIndicatorFactory.Volume(owner.transform);
            view.Show(Vector3.up,Vector3.up+Vector3.right,1,0);
            Assert.That(view.Visible,Is.True);
            settings.enabled=false; view.Show(Vector3.up,Vector3.up+Vector3.right,1,0);
            Assert.That(view.gameObject.activeSelf,Is.False);
            settings.enabled=true; view.Show(Vector3.up,Vector3.up+Vector3.right,1,0);
            view.Show(Vector3.up,Vector3.up,-1,0); Assert.That(view.Visible,Is.False);
        }
        [Test] public void InvalidTrajectoryClearsPreviousDisplayAndValidPathHasOneArrow()
        {
            var view=WorldIndicatorFactory.Trajectory(owner.transform);
            view.Show(new[]{Vector3.up,Vector3.up+Vector3.right*2,Vector3.up+Vector3.right*4});
            Assert.That(Mesh(view,"Arrow").vertexCount,Is.EqualTo(4));
            view.Show(new[]{Vector3.up,new Vector3(float.NaN,0,0)});
            Assert.That(view.Visible,Is.False);
            view.Show(new[]{Vector3.up,Vector3.up}); Assert.That(view.Visible,Is.False);
        }
        [Test] public void RepeatedPresentationReusesLayersAndRuntimeMeshes()
        {
            var view=WorldIndicatorFactory.Area(owner.transform,false);
            view.Show(Vector3.zero,Quaternion.identity,new Vector2(4,2),0);
            var mesh=Mesh(view,"Boundary"); int count=view.transform.childCount;
            for(int i=0;i<120;i++) view.Show(Vector3.forward*i*.01f,Quaternion.identity,new Vector2(4,2),i/120f);
            Assert.That(Mesh(view,"Boundary"),Is.SameAs(mesh)); Assert.That(view.transform.childCount,Is.EqualTo(count));
        }
        [Test] public void DropAdapterUsesPhysicalFootprintAndClearsWhenWarningEndsOrSupportDisappears()
        {
            var floor=new GameObject("Floor",typeof(BoxCollider)); floor.transform.SetParent(owner.transform);
            floor.transform.position=new Vector3(1000,-.5f,1000); floor.GetComponent<BoxCollider>().size=new Vector3(20,1,20);
            var go=new GameObject("Drop"); go.transform.SetParent(owner.transform);
            var hazard=go.AddComponent<CurtainCall.HumanCannon.HumanCannonHazard>();
            var data=new SerializedObject(hazard); data.FindProperty("kind").enumValueIndex=(int)CurtainCall.HumanCannon.CannonHazardKind.FallingLoad;
            data.FindProperty("boxSize").vector3Value=new Vector3(4,1,2); data.ApplyModifiedPropertiesWithoutUndo();
            var cue=go.AddComponent<CurtainCall.HumanCannon.HumanCannonHazardCue>();
            cue.origin=new Vector3(1000,5,1000); cue.destination=new Vector3(1000,.5f,1000); cue.warningSeconds=1;
            var presenter=go.AddComponent<HazardWorldIndicator>(); Physics.SyncTransforms();
            Assert.That(presenter.Present(hazard,cue,true,.5),Is.True);
            var view=go.GetComponentInChildren<AreaIndicatorView>(true); Assert.That(view.Visible,Is.True);
            Assert.That(Mesh(view,"Fill").bounds.size.x,Is.EqualTo(4).Within(.001));
            Assert.That(Mesh(view,"Fill").bounds.size.z,Is.EqualTo(2).Within(.001));
            presenter.Present(hazard,cue,true,1.5); Assert.That(view.Visible,Is.False);
            floor.SetActive(false); Physics.SyncTransforms();
            typeof(HazardWorldIndicator).GetField("nextSupport",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(presenter,0f);
            presenter.Present(hazard,cue,true,.5); Assert.That(view.Visible,Is.False);
        }
    }
}
