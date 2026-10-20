using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class PerformanceUpgradePlayTests
    {
        static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
        static object Get(object obj,string property)=>obj.GetType().GetProperty(property).GetValue(obj);
        GameObject label,source;
        [TestCase(true,79,1,100,"공연 성공")]
        [TestCase(true,80,1,100,"공연 대성공")]
        [TestCase(true,319,4,100,"공연 성공")]
        [TestCase(true,320,4,100,"공연 대성공")]
        [TestCase(false,400,4,100,"공연 실패")]
        public void GreatSuccessThresholdNeverOverridesFailure(bool success,long amount,int count,int cap,string expected)
        {
            var method=T("CurtainCall.UI.SceneIntegration.PerformanceEndingPresentation").GetMethod("ResultTitle");
            Assert.AreEqual(expected,method.Invoke(null,new object[]{success,amount,count,cap}));
        }
        [UnityTest]
        public IEnumerator WorldLabelTracksSourceTextVisibilityAndRestoresLegacyOnDisable()
        {
            source=new GameObject("Original",typeof(TextMeshPro));
            var text=source.GetComponent<TextMeshPro>(); text.text="FINISH";
            label=Object.Instantiate(Resources.Load<GameObject>("Prefabs/UIs/WorldLabels/WorldLabelCloth"));
            var view=label.GetComponent(T("CurtainCall.UI.WorldLabelsV2.WorldLabelView"));
            var binding=(Behaviour)label.AddComponent(T("CurtainCall.UI.SceneIntegration.PerformanceWorldLabelBinding"));
            binding.GetType().GetMethod("Configure").Invoke(binding,new object[]{text,view});
            binding.enabled=false; binding.enabled=true;
            source.transform.position=new Vector3(3,4,5);
            yield return null;
            Assert.IsFalse(text.enabled); Assert.AreEqual("FINISH",Get(view,"Title"));
            Assert.Less(Vector3.Distance(label.transform.position,source.transform.position),.03f);
            source.SetActive(false); yield return null;
            Assert.AreEqual(false,Get(view,"IsVisible"));
            source.SetActive(true); text.text="10 m"; yield return null;
            Assert.AreEqual(true,Get(view,"IsVisible")); Assert.AreEqual("10 m",Get(view,"Title"));
            binding.enabled=false;
            Assert.IsTrue(text.enabled); Assert.AreEqual(false,Get(view,"IsVisible"));
        }
        [UnityTearDown]
        public IEnumerator Cleanup() { if(label!=null) Object.Destroy(label); if(source!=null) Object.Destroy(source); yield return null; }
    }
}
