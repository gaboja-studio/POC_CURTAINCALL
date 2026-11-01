using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class EscapeBodyHudTests
    {
        const string Prefab = "Assets/Resources/Prefabs/UIs/EscapeRoute/EscapeBodyStatusHud.prefab";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);

        [TestCase("Rabbit")] [TestCase("Bear")] [TestCase("BluePierrot")] [TestCase("PinkGirl")]
        public void OriginalAvatarArmsCanBeBakedWithoutEditingTheSource(string avatar)
        {
            var preview=EditorSceneManager.NewPreviewScene();Mesh left=null,right=null;GameObject owner=null;
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Resources/Prefabs/Characters/Players/Models/{avatar}/{avatar}.prefab");
                var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview);
                var human=root.GetComponent(T("CurtainCall.Player.HumanoidModel"));
                Assert.That(human,Is.Not.Null);
                var animator=(Animator)human.GetType().GetField("Animator").GetValue(human);animator.Rebind();animator.Update(0);
                var limbs=(SkinnedMeshRenderer[])human.GetType().GetField("Limbs").GetValue(human);
                var sourceMeshes=limbs.Select(l=>l.sharedMesh).ToArray();
                var liveBones=root.GetComponentsInChildren<Transform>(true);var boneRotations=liveBones.Select(b=>b.localRotation).ToArray();
                var bake=T("CurtainCall.EscapeRoute.EscapeFirstPersonArms").GetMethod("BakeArm",BindingFlags.Static|BindingFlags.NonPublic);
                left=(Mesh)bake.Invoke(null,new object[]{limbs[2],animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),animator.GetBoneTransform(HumanBodyBones.LeftHand),root.transform.forward});
                right=(Mesh)bake.Invoke(null,new object[]{limbs[3],animator.GetBoneTransform(HumanBodyBones.RightUpperArm),animator.GetBoneTransform(HumanBodyBones.RightHand),root.transform.forward});
                foreach(var mesh in new[]{left,right}){Assert.That(mesh,Is.Not.Null);Assert.That(mesh.vertexCount,Is.GreaterThan(0));Assert.That(mesh.bounds.size.z,Is.InRange(.35f,.85f));}
                owner=new GameObject("Local arms fixture");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner,preview);
                var cameraObject=new GameObject("Local arms fixture camera",typeof(Camera));cameraObject.transform.SetParent(owner.transform,false);
                var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;
                var armsType=T("CurtainCall.EscapeRoute.EscapeFirstPersonArms");var arms=owner.AddComponent(armsType);
                armsType.GetField("viewCamera",Flags).SetValue(arms,camera);
                armsType.GetMethod("BuildArms",Flags).Invoke(arms,new object[]{human});
                Assert.That(armsType.GetProperty("ArmMeshCount").GetValue(arms),Is.EqualTo(2));
                var parts=T("CurtainCall.Player.BodyPart");var visible=armsType.GetMethod("SetVisible",Flags);
                var phase=armsType.GetField("phase",Flags);phase.SetValue(arms,Mathf.PI*1.5f);
                visible.Invoke(arms,new[]{(object)true,Enum.ToObject(parts,1)});
                var renderers=(Renderer[])armsType.GetField("renderers",Flags).GetValue(arms);
                Assert.That(renderers[0].enabled,Is.False);Assert.That(renderers[1].enabled,Is.True);
                visible.Invoke(arms,new[]{(object)true,Enum.ToObject(parts,3)});
                Assert.That(camera.enabled,Is.False,"Both missing arms must disable the overlay.");
                visible.Invoke(arms,new[]{(object)true,Enum.ToObject(parts,0)});
                Assert.That(renderers[0].enabled,Is.False);Assert.That(renderers[1].enabled,Is.True,"A reset restores the current right-hand swing.");
                for(int step=0;step<48;step++)
                {
                    float angle=step*Mathf.PI/24;phase.SetValue(arms,angle);
                    visible.Invoke(arms,new[]{(object)true,Enum.ToObject(parts,0)});
                    Assert.That(renderers.Count(r=>r.enabled),Is.LessThanOrEqualTo(1),"Left and right must never appear together.");
                    if(Mathf.Sin(angle)>.1f)Assert.That(renderers[0].enabled,Is.True);
                    if(Mathf.Sin(angle)<-.1f)Assert.That(renderers[1].enabled,Is.True);
                }
                phase.SetValue(arms,Mathf.PI*.5f);visible.Invoke(arms,new[]{(object)true,Enum.ToObject(parts,1)});
                Assert.That(renderers.All(r=>!r.enabled),Is.True,"A lost left hand stays absent during its turn.");
                visible.Invoke(arms,new[]{(object)false,Enum.ToObject(parts,0)});
                Assert.That(camera.enabled,Is.False);
                for(int i=0;i<limbs.Length;i++)Assert.That(limbs[i].sharedMesh,Is.SameAs(sourceMeshes[i]));
                for(int i=0;i<liveBones.Length;i++)Assert.That(Quaternion.Angle(liveBones[i].localRotation,boneRotations[i]),Is.LessThan(.001f),"Live avatar pose must be unchanged.");
            }
            finally {if(owner!=null)Object.DestroyImmediate(owner);if(left!=null)Object.DestroyImmediate(left);if(right!=null)Object.DestroyImmediate(right);EditorSceneManager.ClosePreviewScene(preview);}
        }

        [Test]
        public void HandOverlayClearsDepthAndRestoresOnlyItsOwnCameraLayer()
        {
            var root=new GameObject("Overlay fixture");
            try
            {
                var camera=root.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=-1;
                var armsType=T("CurtainCall.EscapeRoute.EscapeFirstPersonArms");var arms=root.AddComponent(armsType);
                Assert.That(armsType.GetMethod("CreateViewCamera",Flags).Invoke(arms,new object[]{camera}),Is.True);
                var overlay=(Camera)armsType.GetField("viewCamera",Flags).GetValue(arms);
                var dataType=T("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                var data=overlay.GetComponent(dataType);
                Assert.That(dataType.GetProperty("clearDepth").GetValue(data),Is.True,"Walls must not clip the view model.");
                Assert.That(overlay.cullingMask,Is.EqualTo(1<<31));
                camera.cullingMask &= ~(1<<7);
                armsType.GetMethod("ClearView",Flags).Invoke(arms,null);
                Assert.That(camera.cullingMask,Is.EqualTo(-1 & ~(1<<7)),"Keep unrelated camera mask changes.");
                var baseData=root.GetComponent(dataType);
                Assert.That(((IEnumerable)dataType.GetProperty("cameraStack").GetValue(baseData)).Cast<object>(),Has.No.Member(overlay));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ViewToggleOverridesCannonAndReleasesOnlyItsOwnRequest()
        {
            var playerType=T("CurtainCall.Network.PlayerSync.NetworkPlayer");
            var directorType=T("CurtainCall.Cameras.CameraViewDirector");
            var toggleType=T("CurtainCall.EscapeRoute.EscapePerspectiveToggle");
            var gate=T("CurtainCall.Player.InputGate");
            var oldLocal=playerType.GetProperty("Local").GetValue(null);
            var oldDirector=directorType.GetProperty("Current").GetValue(null);
            var oldToggle=toggleType.GetProperty("Current").GetValue(null);
            var gateFields=new[]{"rearming","checkedFrame","blockedFrame"}.Select(n=>gate.GetField(n,BindingFlags.NonPublic|BindingFlags.Static)).ToArray();
            var oldGate=gateFields.Select(f=>f.GetValue(null)).ToArray();
            var frame=new GameObject("View preference fixture");frame.SetActive(false);
            var actor=new GameObject("View actor fixture");actor.SetActive(false);
            var rig=new GameObject("View rig fixture");rig.SetActive(false);
            Component toggle=null;Object ropeScheme=null;GameObject hudRoot=null;
            try
            {
                var content=frame.AddComponent(T("CurtainCall.EscapeRoute.EscapeCampaignContent"));
                var body=new GameObject("Active content");body.transform.SetParent(frame.transform,false);
                content.GetType().GetField("content",Flags).SetValue(content,body);
                toggle=frame.AddComponent(toggleType);frame.SetActive(true);
                toggleType.GetField("content",Flags).SetValue(toggle,content);
                toggleType.GetProperty("Current").SetValue(null,toggle);
                var player=actor.AddComponent(playerType);
                playerType.GetProperty("IsSpawned",Flags).SetValue(player,true);
                actor.SetActive(true);playerType.GetProperty("Local").SetValue(null,player);
                var director=rig.AddComponent(directorType);rig.SetActive(true);
                directorType.GetProperty("Current").SetValue(null,director);
                directorType.GetMethod("SetLocalPlayer").Invoke(director,new object[]{player});
                toggleType.GetField("director",Flags).SetValue(toggle,director);
                hudRoot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
                var hud=hudRoot.GetComponent(T("CurtainCall.EscapeRoute.EscapeBodyStatusHud"));
                var refreshHud=hud.GetType().GetMethod("LateUpdate",Flags);
                refreshHud.Invoke(hud,null);
                Assert.That(hud.GetType().GetProperty("IsVisible").GetValue(hud),Is.True,"The silhouette must be visible in ordinary third person.");
                Assert.That(hud.GetType().GetProperty("IsAimDotVisible").GetValue(hud),Is.False);
                var settings=Resources.Load("Settings/Camera/CameraViewSettings");
                var first=settings.GetType().GetField("FirstPerson").GetValue(settings);
                var third=settings.GetType().GetField("DefaultThirdPerson").GetValue(settings);
                toggleType.GetField("firstPersonView",Flags).SetValue(toggle,first);
                directorType.GetMethod("RequestView").Invoke(director,new[]{actor,third,(object)200,Vector3.forward});
                gateFields[0].SetValue(null,false);gateFields[1].SetValue(null,-1);gateFields[2].SetValue(null,-1);
                Assert.That(toggleType.GetMethod("TryToggleView").Invoke(toggle,null),Is.True);
                // EditMode does not run Cinemachine's Play-only pipeline; supply its resolved camera state.
                directorType.GetField("firstPerson",Flags).SetValue(director,true);
                refreshHud.Invoke(hud,null);
                Assert.That(hud.GetType().GetProperty("IsAimDotVisible").GetValue(hud),Is.True);
                var resolver=directorType.GetField("requests",Flags).GetValue(director);
                object[] resolve={null,null,null};
                Assert.That(resolver.GetType().GetMethod("TryResolve").Invoke(resolver,resolve),Is.True);
                Assert.That(resolve[0],Is.SameAs(toggle),"Preference wins over the cannon's 200-point request.");
                var controllerType=T("CurtainCall.Player.PlayerController");
                var controller=actor.AddComponent(controllerType);
                ropeScheme=ScriptableObject.CreateInstance(T("CurtainCall.Player.TightropeControlScheme"));
                controllerType.GetProperty("Scheme").SetValue(controller,ropeScheme);
                toggleType.GetMethod("RefreshRequest",Flags).Invoke(toggle,null);
                resolve=new object[]{null,null,null};resolver.GetType().GetMethod("TryResolve").Invoke(resolver,resolve);
                Assert.That(resolve[0],Is.SameAs(actor),"Rope must retain its authored third-person view.");
                directorType.GetField("firstPerson",Flags).SetValue(director,false);
                refreshHud.Invoke(hud,null);Assert.That(hud.GetType().GetProperty("IsVisible").GetValue(hud),Is.True,"Rope view must not hide the silhouette.");
                Assert.That(hud.GetType().GetProperty("IsAimDotVisible").GetValue(hud),Is.False,"First-person preference must not show a dot during actual third-person stunts.");
                Assert.That(toggleType.GetProperty("FirstPersonSelected").GetValue(toggle),Is.True,"Rope does not erase the selected first-person mode.");
                controllerType.GetProperty("Scheme").SetValue(controller,null);
                toggleType.GetMethod("RefreshRequest",Flags).Invoke(toggle,null);
                resolve=new object[]{null,null,null};resolver.GetType().GetMethod("TryResolve").Invoke(resolver,resolve);
                Assert.That(resolve[0],Is.SameAs(toggle),"Leaving rope restores first person automatically.");
                directorType.GetField("firstPerson",Flags).SetValue(director,true);
                refreshHud.Invoke(hud,null);
                Assert.That(hud.GetType().GetProperty("IsAimDotVisible").GetValue(hud),Is.True);
                Assert.That(toggleType.GetMethod("TryToggleView").Invoke(toggle,null),Is.True);
                resolve=new object[]{null,null,null};resolver.GetType().GetMethod("TryResolve").Invoke(resolver,resolve);
                Assert.That(resolve[0],Is.SameAs(actor),"Returning to third person preserves the underlying stunt view.");
                directorType.GetField("firstPerson",Flags).SetValue(director,false);
                refreshHud.Invoke(hud,null);
                Assert.That(hud.GetType().GetProperty("IsAimDotVisible").GetValue(hud),Is.False);
            }
            finally
            {
                if(toggle!=null){var preset=toggleType.GetField("ownedPreset",Flags).GetValue(toggle) as Object;toggleType.GetField("ownedPreset",Flags).SetValue(toggle,null);if(preset!=null)Object.DestroyImmediate(preset);}
                playerType.GetProperty("IsSpawned",Flags).SetValue(actor.GetComponent(playerType),false);
                if(hudRoot!=null)Object.DestroyImmediate(hudRoot);Object.DestroyImmediate(frame);Object.DestroyImmediate(actor);Object.DestroyImmediate(rig);if(ropeScheme!=null)Object.DestroyImmediate(ropeScheme);
                playerType.GetProperty("Local").SetValue(null,oldLocal);directorType.GetProperty("Current").SetValue(null,oldDirector);toggleType.GetProperty("Current").SetValue(null,oldToggle);
                for(int i=0;i<gateFields.Length;i++)gateFields[i].SetValue(null,oldGate[i]);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(8)] [TestCase(15)]
        public void LocalLimbDisplayMapsTheFourFlagsAndRestores(int mask)
        {
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),preview);
                var hud=root.GetComponent(T("CurtainCall.EscapeRoute.EscapeBodyStatusHud"));
                var parts=T("CurtainCall.Player.BodyPart");
                var present=hud.GetType().GetMethod("Present");present.Invoke(hud,new[]{Enum.ToObject(parts,mask)});
                var diagram=root.GetComponentInChildren(T("CurtainCall.EscapeRoute.HumanBodyDiagramGraphic"),true);
                Assert.That(Convert.ToInt32(diagram.GetType().GetProperty("LostParts").GetValue(diagram)),Is.EqualTo(mask));
                Assert.That(root.GetComponentsInChildren(T("TMPro.TMP_Text"),true),Is.Empty,"No injury text remains.");
                var images=root.GetComponentsInChildren(T("UnityEngine.UI.Image"),true);
                Assert.That(images.Length,Is.EqualTo(1),"Only the aiming dot is an Image; the injury diagram has no panel.");
                Assert.That(images[0].name,Is.EqualTo("AimDot"));
                Assert.That(root.GetComponentsInChildren(T("UnityEngine.UI.Graphic"),true).Length,Is.EqualTo(2));
                present.Invoke(hud,new[]{Enum.ToObject(parts,0)});
                Assert.That(Convert.ToInt32(hud.GetType().GetProperty("DisplayedParts").GetValue(hud)),Is.Zero);
                Assert.That(Convert.ToInt32(diagram.GetType().GetProperty("LostParts").GetValue(diagram)),Is.Zero);
                Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [TestCase(1,-1)] [TestCase(2,1)] [TestCase(4,-1)] [TestCase(8,1)]
        public void SilhouetteMarksLostLimbsOnThePlayersScreenSide(int mask,int side)
        {
            var go=new GameObject("Diagram mesh fixture",typeof(RectTransform));var mesh=new Mesh();object vh=null;
            try
            {
                var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(100,182);
                var graphic=go.AddComponent(T("CurtainCall.EscapeRoute.HumanBodyDiagramGraphic"));
                graphic.GetType().GetMethod("Present").Invoke(graphic,new[]{Enum.ToObject(T("CurtainCall.Player.BodyPart"),mask)});
                vh=Activator.CreateInstance(T("UnityEngine.UI.VertexHelper"));
                graphic.GetType().GetMethod("OnPopulateMesh",Flags|BindingFlags.DeclaredOnly).Invoke(graphic,new[]{vh});
                vh.GetType().GetMethod("FillMesh").Invoke(vh,new object[]{mesh});
                var verts=mesh.vertices;var colors=mesh.colors32;
                var red=Enumerable.Range(0,verts.Length).Where(i=>colors[i].r>200&&colors[i].g<140).ToArray();
                Assert.That(red.Length,Is.GreaterThan(0));
                Assert.That(red.Average(i=>verts[i].x)*side,Is.GreaterThan(0),"Player left must be screen-left; player right must be screen-right.");
                Assert.That(mesh.triangles.Length,Is.GreaterThan(0));
                Assert.That(verts.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)),Is.True);
            }
            finally { (vh as IDisposable)?.Dispose();Object.DestroyImmediate(mesh);Object.DestroyImmediate(go); }
        }
    }
}
