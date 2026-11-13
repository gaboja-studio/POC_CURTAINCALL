using System;
using System.IO;
using NUnit.Framework;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace CurtainCall.UI.Wayfinding.Editor
{
    public sealed class SilhouetteOutlineTests
    {
        GameObject root;
        Camera camera;
        Material surface;
        SilhouetteOutline outline;
        const string Evidence="Tasks/Active/Task-20261113-122-rehearsal-navigation-art/Captures/Outline/";
        [SetUp] public void Open()
        {
            root=new GameObject("Outline render fixture"){hideFlags=HideFlags.HideAndDontSave};root.transform.position=new Vector3(1000,1000,1000);
            var cameraObject=new GameObject("Outline test camera");cameraObject.transform.SetParent(root.transform,false);
            camera=cameraObject.AddComponent<Camera>();camera.transform.localPosition=new Vector3(0,0,-8);
            camera.orthographic=true;camera.orthographicSize=2;camera.nearClipPlane=.1f;camera.farClipPlane=20;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.allowHDR=false;camera.allowMSAA=false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            surface=new Material(Shader.Find("Universal Render Pipeline/Unlit"));surface.SetColor("_BaseColor",new Color(.2f,.2f,.2f,1));
            Directory.CreateDirectory(Evidence);
        }
        GameObject Box(Transform parent,Vector3 p,Vector3 size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(parent,false);
            go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=surface;return go;
        }
        Texture2D Render(string name=null)
        {
            var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            var image=new Texture2D(512,512,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();
                if(name!=null)File.WriteAllBytes(Evidence+name+".png",image.EncodeToPNG());
                return image;
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);}
        }
        static bool Green(Color c)=>c.g>c.r+.2f&&c.g>c.b+.2f;
        [Test] public void CompositeHierarchyHasOnlyExteriorPixelsAndRespectsCameraAndOcclusion()
        {
            var target=new GameObject("Overlapping parts");target.transform.SetParent(root.transform,false);
            Box(target.transform,new Vector3(-.45f,0,.2f),new Vector3(1.5f,2,1));
            Box(target.transform,new Vector3(.45f,.2f,-.2f),new Vector3(1.5f,1.7f,1));
            var baseline=Render();outline=new SilhouetteOutline(target.transform,Color.green,5){Viewer=camera};
            var result=Render("union-exterior");
            try
            {
                var original=baseline.GetPixels();var pixels=result.GetPixels();int green=0,interior=0;
                for(int i=0;i<pixels.Length;i++)if(Green(pixels[i])){green++;if(original[i].r>.08f)interior++;}
                Assert.Greater(green,100,"Renderer feature must actually draw the contour.");
                Assert.Less(interior,8,"No outline may be drawn on internal overlaps or the object's surface.");
                Assert.AreEqual(2,target.GetComponentsInChildren<Renderer>().Length,"Source geometry must not acquire shells.");
                var blocker=Box(root.transform,new Vector3(0,0,-2),new Vector3(4,4,.2f));
                var blocked=Render("occluded");
                try{Assert.IsFalse(Array.Exists(blocked.GetPixels(),Green),"Opaque foreground must occlude the outline.");}
                finally{Object.DestroyImmediate(blocked);Object.DestroyImmediate(blocker);}
                outline.Viewer=null; // This explicitly untagged camera is not the local player's camera.
                var otherCamera=Render();
                try{Assert.IsFalse(Array.Exists(otherCamera.GetPixels(),Green),"Other cameras must not inherit the highlight.");}
                finally{Object.DestroyImmediate(otherCamera);}
            }
            finally{Object.DestroyImmediate(baseline);Object.DestroyImmediate(result);}
        }
        [Test] public void ObjectEndpointKeepsArrivalWaveAndRestoresCoordinateBeacon()
        {
            var prefab=Resources.Load<GameObject>("Prefabs/UIs/Navigation/V2/RehearsalNavigationPulse");
            var view=Object.Instantiate(prefab,root.transform).GetComponent<RehearsalNavigationView>();
            var pulse=view.GetComponent<NavigationPulseSequence>();pulse.AutomaticPlayback=false;
            view.SetPath(new[]{root.transform.position,root.transform.position+Vector3.forward*6});
            pulse.SetEndpointPresentation(true,true,root.transform.position);pulse.EvaluateAt(pulse.TravelSeconds+.1f);
            Assert.IsTrue(pulse.DestinationBeaconHidden);Assert.Greater(pulse.ArrivalEcho.Opacity,0);
            Assert.AreEqual(0,view.Destination.Find("Beacon core").GetComponent<NavigationPulseGlyph>().Opacity);
            Assert.IsTrue(pulse.DepartureEcho.gameObject.activeSelf);
            pulse.EvaluateAt(.1f);Assert.Greater(pulse.DepartureEcho.Opacity,0);
            var slope=GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.transform.SetParent(root.transform,false);slope.transform.localPosition=Vector3.down*.15f;
            slope.transform.localScale=new Vector3(4,.2f,4);slope.transform.localRotation=Quaternion.Euler(0,0,15);
            Physics.SyncTransforms();pulse.SetEndpointPresentation(true,true,root.transform.position);
            Assert.Greater(Vector3.Dot(pulse.DepartureEcho.transform.up,slope.transform.up),.999f,"Departure wave must follow the ground normal.");
            Object.DestroyImmediate(slope);
            pulse.SetEndpointPresentation(false,false,root.transform.position);pulse.EvaluateAt(.1f);
            Assert.IsFalse(pulse.DestinationBeaconHidden);Assert.IsFalse(pulse.DepartureEcho.gameObject.activeSelf);
        }
        [Test] public void SkinnedPoseUpdatesAndDisposalPreservesOriginalRenderer()
        {
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh=Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(primitive);
            var skinObject=new GameObject("Animated target");skinObject.transform.SetParent(root.transform,false);
            var bone=new GameObject("Bone").transform;bone.SetParent(skinObject.transform,false);
            var weights=new BoneWeight[mesh.vertexCount];for(int i=0;i<weights.Length;i++)weights[i]=new BoneWeight{boneIndex0=0,weight0=1};
            mesh.boneWeights=weights;mesh.bindposes=new[]{bone.worldToLocalMatrix*skinObject.transform.localToWorldMatrix};
            var skin=skinObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=new[]{bone};skin.rootBone=bone;
            skin.sharedMaterial=surface;skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*4);
            outline=new SilhouetteOutline(skinObject.transform,Color.green,4){Viewer=camera};
            Texture2D before=null,after=null;
            try
            {
                before=Render();bone.localPosition=Vector3.right*.7f;after=Render("skinned-pose");
                float Center(Texture2D image)
                {float sum=0;int count=0;var pixels=image.GetPixels();for(int i=0;i<pixels.Length;i++)if(Green(pixels[i])){sum+=i%512;count++;}Assert.Greater(count,20);return sum/count;}
                Assert.Greater(Center(after)-Center(before),50,"The contour must follow the posed skin, not its bind pose.");
                outline.Dispose();outline=null;
                Assert.AreSame(mesh,skin.sharedMesh);Assert.AreSame(surface,skin.sharedMaterial);
            }
            finally{if(before!=null)Object.DestroyImmediate(before);if(after!=null)Object.DestroyImmediate(after);Object.DestroyImmediate(mesh);}
        }
        [TearDown] public void Close()
        {
            outline?.Dispose();outline=null;
            if(root!=null)Object.DestroyImmediate(root);if(surface!=null)Object.DestroyImmediate(surface);
        }
    }
}
