using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class EscapePoleFeedbackTests
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        static Type T(string n)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n)).First(t=>t!=null);
        static void Set(object o,string n,object v){var p=o.GetType().GetProperty(n,F);if(p!=null)p.SetValue(o,v);else o.GetType().GetField(n,F).SetValue(o,v);}
        static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);

        [Test]
        public void LiveFeedbackMatchesFInteractionForRangeWallAndOccupiedSeat()
        {
            var pt=T("CurtainCall.Network.PlayerSync.NetworkPlayer");var st=T("CurtainCall.HumanCannon.HumanCannonPoleSystem");
            var ct=T("CurtainCall.HumanCannon.HumanCannonSystem");var gate=T("CurtainCall.Player.InputGate");
            var oldLocal=pt.GetProperty("Local",S).GetValue(null);var oldSystem=st.GetProperty("Current",S).GetValue(null);var oldCannon=ct.GetProperty("Current",S).GetValue(null);
            var gateFields=new[]{"rearming","checkedFrame","blockedFrame"}.Select(n=>gate.GetField(n,S)).ToArray();var oldGate=gateFields.Select(f=>f.GetValue(null)).ToArray();
            var root=new GameObject("Feedback eligibility fixture");root.SetActive(false);var actor=new GameObject("Feedback entrant");actor.SetActive(false);
            var origin=new Vector3(21000,30,21000);Component player=null;Material material=null;GameObject wall=null;
            try
            {
                root.transform.position=origin+new Vector3(0,1,2);actor.transform.position=origin;
                var pole=root.AddComponent(T("CurtainCall.HumanCannon.HumanCannonPole"));Set(pole,"requireClearGrabPath",true);
                var bar=new GameObject("Bar",typeof(MeshRenderer));bar.transform.SetParent(root.transform,false);Set(pole,"visualBar",bar.transform);
                var renderer=bar.GetComponent<Renderer>();material=new Material(Shader.Find("Universal Render Pipeline/Lit"));renderer.sharedMaterial=material;
                var system=root.AddComponent(st);var poles=Array.CreateInstance(pole.GetType(),1);poles.SetValue(pole,0);Set(system,"poles",poles);Set(system,"allowPlatformJumpGrab",true);Set(system,"independentInteraction",true);
                var rt=st.GetNestedType("Runtime",BindingFlags.NonPublic);var states=Array.CreateInstance(rt,1);states.SetValue(Activator.CreateInstance(rt,new object[]{1}),0);Set(system,"states",states);
                var cannon=root.AddComponent(ct);Call(cannon,"ResetSeats");
                player=actor.AddComponent(pt);Call(actor.GetComponent(T("CurtainCall.Player.PlayerMover")),"Awake");Set(player,"IsSpawned",true);Set(player,"OwnerClientId",42UL);
                root.SetActive(true);actor.SetActive(true);pt.GetProperty("Local",S).SetValue(null,player);st.GetProperty("Current",S).SetValue(null,system);ct.GetProperty("Current",S).SetValue(null,cannon);
                gateFields[0].SetValue(null,false);gateFields[1].SetValue(null,-1);gateFields[2].SetValue(null,-1);
                var feedback=root.AddComponent(T("CurtainCall.EscapeRoute.EscapePoleGrabFeedback"));Set(feedback,"poles",poles);Call(feedback,"OnEnable");
                bool Green(){Call(feedback,"LateUpdate");var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);return block.GetColor("_BaseColor").g>.9f;}
                Physics.SyncTransforms();Assert.That((int)Call(system,"FindGrabbable",player),Is.Zero);Assert.That(Green(),Is.True);
                actor.transform.position=origin+Vector3.back*8;Physics.SyncTransforms();Assert.That((int)Call(system,"FindGrabbable",player),Is.EqualTo(-1));Assert.That(Green(),Is.False);
                actor.transform.position=origin;
                wall=new GameObject("Blocking world wall",typeof(BoxCollider));wall.transform.position=origin+new Vector3(0,1,1);wall.GetComponent<BoxCollider>().size=new Vector3(4,3,.2f);
                Physics.SyncTransforms();Assert.That((int)Call(system,"FindGrabbable",player),Is.EqualTo(-1));Assert.That(Green(),Is.False);
                wall.SetActive(false);Physics.SyncTransforms();Assert.That(Green(),Is.True);
                var state=Call(system,"State",0);Assert.That((bool)Call(state,"TryGrab",99UL,0U,0d,0f,false,0),Is.True);
                Assert.That((int)Call(system,"FindGrabbable",player),Is.EqualTo(-1));Assert.That(Green(),Is.False);
            }
            finally
            {
                if(player!=null)Set(player,"IsSpawned",false);if(wall!=null)Object.DestroyImmediate(wall);Object.DestroyImmediate(actor);Object.DestroyImmediate(root);if(material!=null)Object.DestroyImmediate(material);
                pt.GetProperty("Local",S).SetValue(null,oldLocal);st.GetProperty("Current",S).SetValue(null,oldSystem);ct.GetProperty("Current",S).SetValue(null,oldCannon);
                for(int i=0;i<gateFields.Length;i++)gateFields[i].SetValue(null,oldGate[i]);
            }
        }
        [Test]
        public void GreenFeedbackRestoresOriginalPropertyBlockAndNeverEditsSharedMaterial()
        {
            var root=new GameObject("Feedback fixture");root.SetActive(false);Material material=null;
            try
            {
                var pole=root.AddComponent(T("CurtainCall.HumanCannon.HumanCannonPole"));
                var bar=new GameObject("Visual bar",typeof(MeshRenderer));bar.transform.SetParent(root.transform,false);
                pole.GetType().GetField("visualBar",F).SetValue(pole,bar.transform);
                var renderer=bar.GetComponent<Renderer>();material=new Material(Shader.Find("Universal Render Pipeline/Lit"));renderer.sharedMaterial=material;
                var original=new MaterialPropertyBlock();original.SetColor("_BaseColor",Color.yellow);original.SetFloat("_Smoothness",.37f);renderer.SetPropertyBlock(original);
                var type=T("CurtainCall.EscapeRoute.EscapePoleGrabFeedback");var feedback=root.AddComponent(type);
                var poles=Array.CreateInstance(pole.GetType(),1);poles.SetValue(pole,0);type.GetField("poles",F).SetValue(feedback,poles);
                type.GetMethod("OnEnable",F).Invoke(feedback,null);
                var present=type.GetMethod("Present",F);present.Invoke(feedback,new object[]{pole});
                var actual=new MaterialPropertyBlock();renderer.GetPropertyBlock(actual);
                Assert.That(actual.GetColor("_BaseColor").g,Is.EqualTo(1));Assert.That(actual.GetColor("_BaseColor").r,Is.LessThan(.2f));
                Assert.That(actual.GetFloat("_Smoothness"),Is.EqualTo(.37f));Assert.That(renderer.sharedMaterial,Is.SameAs(material));
                present.Invoke(feedback,new object[]{null});renderer.GetPropertyBlock(actual);
                Assert.That(actual.GetColor("_BaseColor"),Is.EqualTo(Color.yellow));
                present.Invoke(feedback,new object[]{pole});type.GetMethod("OnDisable",F).Invoke(feedback,null);renderer.GetPropertyBlock(actual);
                Assert.That(actual.GetColor("_BaseColor"),Is.EqualTo(Color.yellow));
            }
            finally {Object.DestroyImmediate(root);if(material!=null)Object.DestroyImmediate(material);}
        }
    }
}
