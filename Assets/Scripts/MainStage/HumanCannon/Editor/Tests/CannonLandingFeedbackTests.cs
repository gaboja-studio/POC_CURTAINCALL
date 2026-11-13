using System.Reflection;
using CurtainCall.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class CannonLandingFeedbackTests
    {
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
        GameObject root, actor;
        HumanCannonGroundMarkers markers;
        PlayerMover mover;
        BoxCollider floor;
        readonly Vector3 origin=new(11000,0,11000);
        object Call(string name,params object[] args)=>typeof(HumanCannonGroundMarkers).GetMethod(name,Fields).Invoke(markers,args);
        void Set(string name,object value)=>typeof(HumanCannonGroundMarkers).GetField(name,Fields).SetValue(markers,value);

        [SetUp] public void SetUp()
        {
            root=new GameObject("Landing feedback fixture");markers=root.AddComponent<HumanCannonGroundMarkers>();markers.enabled=false;
            var tile=new GameObject("Safe tile");tile.transform.SetParent(root.transform);tile.transform.position=origin-Vector3.up*.25f;
            floor=tile.AddComponent<BoxCollider>();floor.size=new Vector3(.6f,.5f,8);
            var zone=tile.AddComponent<HumanCannonLandingZone>();
            typeof(HumanCannonLandingZone).GetMethod("OnEnable",Fields).Invoke(zone,null);
            actor=new GameObject("Feedback body");actor.transform.SetParent(root.transform);actor.transform.position=origin+Vector3.right*20;
            mover=actor.AddComponent<PlayerMover>();
            Set("courseFloors",new Collider[]{floor});Set("feedbackRopes",new[]{floor});Set("showSafeLandingFeedback",true);
            Set("markerMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/MainStage/HumanCannon/Materials/human-cannon-ground-marker.mat"));
            Physics.SyncTransforms();
        }
        [TearDown] public void TearDown()
        {
            var target=(Transform)typeof(HumanCannonGroundMarkers).GetField("landingTarget",Fields).GetValue(markers);
            var mesh=(Mesh)typeof(HumanCannonGroundMarkers).GetField("quad",Fields).GetValue(markers);
            Set("landingTarget",null);Set("quad",null);
            if(target!=null)Object.DestroyImmediate(target.gameObject);if(mesh!=null)Object.DestroyImmediate(mesh);
            foreach(var zone in root.GetComponentsInChildren<HumanCannonLandingZone>())typeof(HumanCannonLandingZone).GetMethod("OnDisable",Fields).Invoke(zone,null);
            Object.DestroyImmediate(root);Physics.SyncTransforms();
        }
        bool Safe()=> (bool)Call("HasSafeLandingBelow",mover,origin+Vector3.up*10,origin,floor);
        [Test] public void SafeAlignmentRejectsOcclusionUnsafeFloorsAndDisabledSurfaces()
        {
            Assert.That(Safe(),Is.True);
            var obstacle=new GameObject("Blocking structure");obstacle.transform.SetParent(root.transform);obstacle.transform.position=origin+Vector3.up*3;
            var blocker=obstacle.AddComponent<BoxCollider>();blocker.size=new Vector3(2,.4f,2);Physics.SyncTransforms();
            Assert.That(Safe(),Is.False,"Do not show green through a blocking asset.");
            blocker.isTrigger=true;Physics.SyncTransforms();Assert.That(Safe(),Is.True);
            floor.enabled=false;Physics.SyncTransforms();Assert.That(Safe(),Is.False);
            floor.enabled=true;var zone=floor.GetComponent<HumanCannonLandingZone>();zone.enabled=false;
            Assert.That(Safe(),Is.True,"A walkable floor is safe without a cannon-only landing tag.");
            Assert.That(markers.TryProjectGround(origin+new Vector3(4,10,0),out _,out _),Is.False,"No surface is invented in the void.");
        }
        [TestCase(-3.99f)] [TestCase(3.99f)]
        public void LocalTargetStaysInsideTheAuthoredRopeAtBothEnds(float along)
        {
            floor.transform.rotation=Quaternion.Euler(0,37,0);var oldSize=floor.size;Physics.SyncTransforms();
            var point=floor.transform.TransformPoint(floor.center+new Vector3(0,.25f,along));
            Assert.That(Call("ShowLandingTarget",floor,point),Is.EqualTo(true));
            var target=(Transform)typeof(HumanCannonGroundMarkers).GetField("landingTarget",Fields).GetValue(markers);
            foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})
            {var p=floor.transform.InverseTransformPoint(target.TransformPoint(new Vector3(x,0,z)))-floor.center;
             Assert.That(Mathf.Abs(p.x),Is.LessThanOrEqualTo(floor.size.x*.5f+.001f));Assert.That(Mathf.Abs(p.z),Is.LessThanOrEqualTo(floor.size.z*.5f+.001f));}
            Assert.That(floor.size,Is.EqualTo(oldSize));Assert.That(target.GetComponents<Collider>(),Is.Empty);
            typeof(HumanCannonGroundMarkers).GetMethod("HideAll",Fields).Invoke(markers,null);Assert.That(target.gameObject.activeSelf,Is.False);
        }
        [Test] public void TintUsesPropertyBlocksAndOptOutKeepsTheOriginalProjection()
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/MainStage/HumanCannon/Materials/human-cannon-ground-marker.mat");
            var original=mat.GetColor("_RimColor");
            Assert.That(Call("ShowLandingTarget",floor,origin),Is.EqualTo(true));
            var target=(Transform)typeof(HumanCannonGroundMarkers).GetField("landingTarget",Fields).GetValue(markers);
            var renderer=target.GetComponent<MeshRenderer>();var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor("_RimColor").g,Is.EqualTo(1));Assert.That(block.GetColor("_RimColor").r,Is.LessThan(.2f));
            Assert.That(mat.GetColor("_RimColor"),Is.EqualTo(original));
            Set("showSafeLandingFeedback",false);Assert.That(Call("ShowLandingTarget",floor,origin),Is.EqualTo(false));
            Assert.That(markers.TryProjectGround(origin+Vector3.up*10,out var p,out _),Is.True);Assert.That(p.y,Is.EqualTo(0).Within(.001f));
        }
    }
}
