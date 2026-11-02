using System;
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

        [Test]
        public void BodyHudStaysVisibleWithoutAPerspectiveControllerAndHidesOutsideActiveEscape()
        {
            var contentType=T("CurtainCall.EscapeRoute.EscapeCampaignContent");
            var playerType=T("CurtainCall.Network.PlayerSync.NetworkPlayer");
            var oldContent=contentType.GetProperty("Current").GetValue(null);
            var oldLocal=playerType.GetProperty("Local").GetValue(null);
            var frame=new GameObject("Escape HUD context fixture");frame.SetActive(false);
            var actor=new GameObject("Escape HUD actor fixture");actor.SetActive(false);
            GameObject hudRoot=null;Component player=null;
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var content=frame.AddComponent(contentType);
                var body=new GameObject("Facility content");body.transform.SetParent(frame.transform,false);
                contentType.GetField("content",Flags).SetValue(content,body);
                frame.SetActive(true);contentType.GetProperty("Current").SetValue(null,content);
                player=actor.AddComponent(playerType);
                playerType.GetProperty("IsSpawned",Flags).SetValue(player,true);
                playerType.GetProperty("Local").SetValue(null,player);
                hudRoot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
                var hud=hudRoot.GetComponent(T("CurtainCall.EscapeRoute.EscapeBodyStatusHud"));
                var refresh=hud.GetType().GetMethod("LateUpdate",Flags);
                var visible=hud.GetType().GetProperty("IsVisible");
                refresh.Invoke(hud,null);Assert.That(visible.GetValue(hud),Is.True,"No view-mode component or camera is required.");
                body.SetActive(false);refresh.Invoke(hud,null);Assert.That(visible.GetValue(hud),Is.False);
                body.SetActive(true);refresh.Invoke(hud,null);Assert.That(visible.GetValue(hud),Is.True);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(frame,preview);
                refresh.Invoke(hud,null);Assert.That(visible.GetValue(hud),Is.False,"A loaded but inactive scene must not show its HUD.");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(frame,hudRoot.scene);
                refresh.Invoke(hud,null);Assert.That(visible.GetValue(hud),Is.True);
                playerType.GetProperty("Local").SetValue(null,null);
                refresh.Invoke(hud,null);Assert.That(visible.GetValue(hud),Is.False);
            }
            finally
            {
                if(player!=null)playerType.GetProperty("IsSpawned",Flags).SetValue(player,false);
                if(hudRoot!=null)Object.DestroyImmediate(hudRoot);
                Object.DestroyImmediate(frame);Object.DestroyImmediate(actor);
                contentType.GetProperty("Current").SetValue(null,oldContent);
                playerType.GetProperty("Local").SetValue(null,oldLocal);
                EditorSceneManager.ClosePreviewScene(preview);
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
                Assert.That(images,Is.Empty,"The body diagram needs neither a reticle nor a background panel.");
                Assert.That(root.GetComponentsInChildren(T("UnityEngine.UI.Graphic"),true).Length,Is.EqualTo(1));
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
