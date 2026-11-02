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
                typeof(HumanCannonPole).GetField("continuousSwingInput",f).SetValue(p,true);
                typeof(HumanCannonPole).GetField("swingAcceleration",f).SetValue(p,210f);typeof(HumanCannonPole).GetField("maxSwingAngle",f).SetValue(p,50f);typeof(HumanCannonPole).GetField("maxSwingSpeed",f).SetValue(p,140f);typeof(HumanCannonPole).GetField("swingDamping",f).SetValue(p,.25f);
                typeof(HumanCannonPole).GetField("hangLength",f).SetValue(p,4.2f);
                var input=new PoleSwingInput();float angle=0,speed=0;var peak=0f;
                for(int i=0;i<fps*2;i++){float drive=p.SwingDrive(ref input,1f,1f/fps);Assert.That(drive,Is.EqualTo(1));p.Step(ref angle,ref speed,drive,1f/fps,GameSettings.Base.Character.Gravity);peak=Mathf.Max(peak,angle);}
                Assert.That(peak,Is.GreaterThan(40));Assert.That(p.SwingDrive(ref input,-1f,1f/fps),Is.EqualTo(-1));
                for(int i=0;i<fps*2;i++)p.Step(ref angle,ref speed,p.SwingDrive(ref input,-1f,1f/fps),1f/fps,GameSettings.Base.Character.Gravity);
                Assert.That(angle,Is.LessThan(-35));Assert.That(Mathf.Abs(angle),Is.LessThanOrEqualTo(50));Assert.That(Mathf.Abs(speed),Is.LessThanOrEqualTo(140));
                Assert.That(p.SwingDrive(ref input,float.NaN,1f/fps),Is.Zero);
                typeof(HumanCannonPole).GetField("continuousSwingInput",f).SetValue(p,false);input=default;for(int i=0;i<fps;i++)p.SwingDrive(ref input,1f,1f/fps);Assert.That(p.SwingDrive(ref input,1f,1f/fps),Is.EqualTo(1),"Generated stunt poles use the same held input as escape poles.");
                Assert.That(p.SwingDrive(ref input, 1, 0), Is.Zero);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
