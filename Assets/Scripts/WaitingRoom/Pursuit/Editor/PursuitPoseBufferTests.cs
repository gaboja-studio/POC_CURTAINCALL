using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitPoseBufferTests
    {
        [Test] public void BurstLossAndReorderedPacketsNeverRewindOrTeleport()
        {
            var buffer = new PursuitPoseBuffer();
            var previous = Vector3.zero; float maxStep = 0;
            // Host runs at 7.5m/s, packets 20Hz, jitter burst and a 400ms outage.
            for (int frame = 0; frame < 360; frame++) {
                float now = frame / 60f;
                if (frame % 3 == 0 && (now < 2 || now >= 2.4f)) {
                    float host = Mathf.Max(0, now - (frame % 15 == 0 ? .16f : .08f));
                    buffer.Push(host, Vector3.forward * host * 7.5f, Quaternion.Euler(0,host*15,0));
                    Assert.IsFalse(buffer.Push(host-.2, Vector3.back*100,Quaternion.identity));
                }
                Assert.IsTrue(buffer.Sample(1/60f,out var p,out var rotation));
                Assert.GreaterOrEqual(p.z,previous.z-.0001f);
                maxStep=Mathf.Max(maxStep,Vector3.Distance(previous,p)); previous=p;
                Assert.IsFalse(float.IsNaN(rotation.w));
            }
            Assert.Less(maxStep,.3f,"No multi-metre snap when a delayed burst arrives.");
        }
        [Test] public void LongOutageStopsExtrapolationAndExplicitResetAcceptsNewClock()
        {
            var buffer=new PursuitPoseBuffer();buffer.Push(20,Vector3.zero,Quaternion.identity);buffer.Push(20.1,Vector3.forward,Quaternion.identity);
            Vector3 p=default;
            for(int i=0;i<600;i++)buffer.Sample(1/60f,out p,out _);
            Assert.That(p.z,Is.InRange(1f,2.01f));
            Assert.IsFalse(buffer.Push(19,Vector3.right*100,Quaternion.identity));
            buffer.Clear();Assert.IsTrue(buffer.Push(1,Vector3.right*4,Quaternion.identity));
            buffer.Sample(0,out p,out _);Assert.AreEqual(Vector3.right*4,p);
        }
        [Test] public void InvalidSamplesAreRejectedAndCueColoursTrackBehaviour()
        {
            var buffer=new PursuitPoseBuffer();Assert.IsFalse(buffer.Push(double.NaN,Vector3.zero,Quaternion.identity));
            Assert.IsFalse(buffer.Push(1,Vector3.one*float.PositiveInfinity,Quaternion.identity));
            Assert.AreEqual(0,PursuitAppearance.CueState(PursuitBehaviour.Patrol));
            Assert.AreEqual(0,PursuitAppearance.CueState(PursuitBehaviour.Return));
            foreach(var state in new[]{PursuitBehaviour.Listen,PursuitBehaviour.Investigate,PursuitBehaviour.Search}) Assert.AreEqual(1,PursuitAppearance.CueState(state));
            Assert.AreEqual(2,PursuitAppearance.CueState(PursuitBehaviour.Prepare));
            Assert.AreEqual(2,PursuitAppearance.CueState(PursuitBehaviour.Chase));
            Assert.AreEqual(2,PursuitAppearance.CueState(PursuitBehaviour.Capture));
        }
    }
}
