using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class FinaleHazardTests
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        readonly List<GameObject> roots=new();
        static Type Find(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("CurtainCall.HumanCannon."+name)).First(t=>t!=null);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
        Component Create(string name)
        {
            var root=new GameObject(name);root.SetActive(false);roots.Add(root);return root.AddComponent(Find(name));
        }
        [TearDown] public void Cleanup(){foreach(var root in roots)if(root!=null)Object.DestroyImmediate(root);roots.Clear();}
        Component Cue()
        {
            var cue=Create("HumanCannonHazardCue");
            Set(cue,"origin",new Vector3(0,8,0));Set(cue,"destination",new Vector3(0,1.5f,0));
            Set(cue,"warningSeconds",1f);Set(cue,"activeSeconds",2f);Set(cue,"restSeconds",3f);Set(cue,"falling",true);
            return cue;
        }
        [Test] public void SharedCueSeparatesWarningActiveRestAndResetsWithoutAccumulatedFrames()
        {
            var cue=Cue();
            Assert.That(Call(cue,"Live",.99d),Is.False);
            Assert.That(Call(cue,"Live",1d),Is.True);Assert.That(Call(cue,"Live",2.99d),Is.True);
            Assert.That(Call(cue,"Live",3d),Is.False);Assert.That(Call(cue,"Live",6d),Is.False);
            Assert.That((Vector3)Call(cue,"Position",2d),Is.EqualTo(new Vector3(0,6.375f,0)));
            Assert.That(Call(cue,"Position",6002d),Is.EqualTo(Call(cue,"Position",2d)));
            Assert.That(Call(cue,"Live",double.NaN),Is.False);
            Assert.That(Call(cue,"Position",0d),Is.EqualTo(new Vector3(0,8,0)));
        }
        [Test] public void AuthoredBoxHazardUsesOriginalContactOnlyDuringItsActiveWindow()
        {
            var cue=Cue();var hazard=Create("HumanCannonHazard");
            Set(hazard,"cue",cue);Set(hazard,"kind",Enum.Parse(Find("CannonHazardKind"),"FallingLoad"));
            Set(hazard,"boxSize",new Vector3(4,3,4));
            Assert.That(Call(hazard,"Touches",new Vector3(0,7.2f,0),1.73f,.3f,.5d,1f),Is.False);
            Assert.That(Call(hazard,"Touches",new Vector3(0,5.575f,0),1.73f,.3f,2d,1f),Is.True);
            Assert.That(Call(hazard,"Touches",new Vector3(0,.7f,0),1.73f,.3f,4d,1f),Is.False);
        }
        [Test] public void BoxContactHandlesStandingCrawlingCornersAndInvalidInput()
        {
            var method=Find("CannonHazardGeometry").GetMethod("BoxTouches",BindingFlags.Public|BindingFlags.Static);
            bool Touch(Vector3 feet,float height)=> (bool)method.Invoke(null,new object[]{new Vector3(0,1,0),new Vector3(2,2,2),feet,height,.3f});
            Assert.That(Touch(new Vector3(1.2f,0,0),1.73f),Is.True);
            Assert.That(Touch(new Vector3(1.4f,0,0),1.73f),Is.False);
            Assert.That(Touch(new Vector3(1.25f,0,1.25f),1.73f),Is.False);
            Assert.That(Touch(Vector3.zero,.6f),Is.True);
            Assert.That(Touch(new Vector3(0,3,0),1.73f),Is.False);
            Assert.That(Touch(new Vector3(float.NaN,0,0),1.73f),Is.False);
        }
        [Test] public void OriginalHazardWithoutCueRetainsItsSinusoidalMotion()
        {
            var hazard=Create("HumanCannonHazard");
            Set(hazard,"travel",new Vector3(4,0,0));Set(hazard,"periodSeconds",8f);
            Assert.That(Vector3.Distance((Vector3)Call(hazard,"Position",2d,1f),new Vector3(4,0,0)),Is.LessThan(.001f));
            var course=Create("HumanCannonHazardCourse");
            Assert.That(course.GetType().GetField("useAuthoredHazardCount",Flags).GetValue(course),Is.False);
        }
    }
}
