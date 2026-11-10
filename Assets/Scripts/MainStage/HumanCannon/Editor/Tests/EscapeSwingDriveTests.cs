using System.Reflection;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class EscapeSwingDriveTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ContinuousDriveKeepsRespondingAfterPointSixFiveSecondsAndReversesImmediately(int fps)
        {
            var go=new GameObject("Continuous swing fixture");try{
                var p=go.AddComponent<HumanCannonPole>();var f=BindingFlags.Instance|BindingFlags.NonPublic;
                Assert.IsNull(typeof(HumanCannonPole).GetField("continuousSwingInput",f));
                Assert.IsNull(typeof(HumanCannonPole).GetField("swingAcceleration",f));Assert.IsNull(typeof(HumanCannonPole).GetField("maxSwingAngle",f));Assert.IsNull(typeof(HumanCannonPole).GetField("maxSwingSpeed",f));Assert.IsNull(typeof(HumanCannonPole).GetField("swingDamping",f));
                Assert.IsNull(typeof(HumanCannonPole).GetField("hangLength",f));
                var input=new PoleSwingInput();float angle=0,speed=0;var peak=0f;
                for(int i=0;i<fps*2;i++){float drive=p.SwingDrive(ref input,1f,1f/fps);Assert.That(drive,Is.EqualTo(1));p.Step(ref angle,ref speed,drive,1f/fps,GameSettings.Base.Character.Gravity);peak=Mathf.Max(peak,angle);}
                Assert.That(peak,Is.GreaterThan(40));Assert.That(p.SwingDrive(ref input,-1f,1f/fps),Is.EqualTo(-1));
                for(int i=0;i<fps*2;i++)p.Step(ref angle,ref speed,p.SwingDrive(ref input,-1f,1f/fps),1f/fps,GameSettings.Base.Character.Gravity);
                Assert.That(angle,Is.LessThan(-35));Assert.That(Mathf.Abs(angle),Is.LessThanOrEqualTo(GameSettings.Poles.MaxSwingAngle));Assert.That(Mathf.Abs(speed),Is.LessThanOrEqualTo(GameSettings.Poles.MaxSwingSpeed));
                Assert.That(p.SwingDrive(ref input,float.NaN,1f/fps),Is.Zero);
                Assert.IsNull(typeof(HumanCannonPole).GetField("continuousSwingInput",f));input=default;for(int i=0;i<fps;i++)p.SwingDrive(ref input,1f,1f/fps);Assert.That(p.SwingDrive(ref input,1f,1f/fps),Is.EqualTo(1),"Generated stunt poles use the same held input as escape poles.");
                Assert.That(p.SwingDrive(ref input, 1, 0), Is.Zero);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
