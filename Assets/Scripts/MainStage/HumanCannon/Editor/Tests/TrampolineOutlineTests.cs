using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class TrampolineOutlineTests
    {
        static readonly Bounds Shape = new Bounds(new Vector3(-.1998296f,-.175f,0),new Vector3(5.3503408f,.65f,4.9506816f));
        static bool Drop(float x,float z,out Vector3 contact) => TrampolineOutline.TryTouch(
            new Vector3(x,5,z),new Vector3(x,-4,z),Vector3.zero,Vector3.zero,
            Quaternion.identity,Quaternion.identity,Shape,.25f,1.7f,out contact);

        [TestCase(0f,0f,true)]
        [TestCase(2.5f,0f,true)]
        [TestCase(2.2f,2.2f,false)]
        [TestCase(-2.8f,.5f,true)]
        [TestCase(-3.2f,.8f,false)]
        public void SweptBodyFollowsCircleAndGripInsteadOfOldSquare(float x,float z,bool expected)
        {
            Assert.AreEqual(expected,Drop(x,z,out var p));
            if(expected)Assert.IsTrue(TrampolineOutline.Contains(p,Shape,.001f),"host accepts the actual contact point");
        }

        [Test]
        public void SideUndersideAndMovingRotatedDeviceKeepContinuousContact()
        {
            Assert.IsTrue(TrampolineOutline.TryTouch(new Vector3(-8,0,0),new Vector3(8,0,0),Vector3.zero,Vector3.zero,Quaternion.identity,Quaternion.identity,Shape,.25f,1.7f,out _));
            Assert.IsTrue(TrampolineOutline.TryTouch(new Vector3(0,-5,0),Vector3.up*5,Vector3.zero,Vector3.zero,Quaternion.identity,Quaternion.identity,Shape,.25f,1.7f,out _));
            var q=Quaternion.Euler(0,90,0);var at=new Vector3(7,2,4);
            Assert.IsTrue(TrampolineOutline.TryTouch(at+q*new Vector3(-2.8f,4,.5f),at+q*new Vector3(-2.8f,-4,.5f),at,at,q,q,Shape,.25f,1.7f,out var hit));
            Assert.IsTrue(TrampolineOutline.Contains(Quaternion.Inverse(q)*(hit-at),Shape,.001f));
            Assert.IsTrue(TrampolineOutline.TryTouch(Vector3.zero,Vector3.zero,Vector3.left*8,Vector3.right*8,Quaternion.identity,q,Shape,.25f,1.7f,out _));
        }
    }
}
