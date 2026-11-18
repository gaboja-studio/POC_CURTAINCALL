#if UNITY_EDITOR
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    public class PerformanceEquipmentTests
    {
        [Test,Category("CommonProgressionRegression")]
        public void FourUseEffectsHaveVisibleSafePrefabReferences()
        {
            foreach(var id in new[]{"spring-shoes","rescue-gloves","sticky-gloves","balloon-pants"})
            {
                var prefab=GameSettings.PerformanceEquipment.UseVisual(id); Assert.NotNull(prefab,id);
                var effect=prefab.GetComponent<EquipmentUseVisual>();Assert.NotNull(effect,id);
                Assert.GreaterOrEqual(effect.strokes.Length,2,id);
                foreach(var line in effect.strokes) {Assert.NotNull(line);Assert.NotNull(line.sharedMaterial);Assert.IsTrue(line.sharedMaterial.shader.isSupported);}
                Assert.AreEqual(0,prefab.GetComponentsInChildren<Collider>(true).Length,id);
                foreach(var ps in prefab.GetComponentsInChildren<ParticleSystem>(true)) Assert.IsFalse(ps.collision.enabled,id);
                if(id=="balloon-pants")Assert.NotNull(effect.model);
            }
        }
        [TestCase(0f,-1f,1f,true)]
        [TestCase(1.3f,-1f,1f,false)]
        [TestCase(1.25f,-1f,1f,false)]
        [TestCase(0f,-10f,10f,true)]
        [TestCase(0f,1f,-1f,true)]
        [TestCase(0f,0f,1f,false)]
        [TestCase(0f,-2f,-1f,false)]
        [Category("CommonProgressionRegression")]
        public void CrossingMustPassInsideOpening(float x,float from,float to,bool expected)
            => Assert.AreEqual(expected,PerformanceCheerPoint.CrossesOpening(new Vector3(x,0,from),new Vector3(x,0,to),1.25f));

        [Test,Category("CommonProgressionRegression")]
        public void StickyHoldDelayReplicatesWithoutDelayingExternalHazard()
        {
            var host=new PoleGateState(); var client=new PoleGateState();
            Assert.IsTrue(host.TryGrab(7,0,10,40,true));
            Assert.IsTrue(host.DelayHold(0,3));
            Assert.AreEqual(53,host.HoldUntil); Assert.AreEqual(50,host.RearmAt);
            Assert.IsTrue(client.Receive(host.Revision,host.Occupant,host.Phase,host.HoldUntil,host.RearmAt));
            host.Tick(48,2);
            Assert.IsTrue(client.Receive(host.Revision,host.Occupant,host.Phase,host.HoldUntil,host.RearmAt));
            Assert.AreEqual(PoleSawPhase.Warning,client.Phase);
            host.Tick(50,2); Assert.AreEqual(PoleSawPhase.Active,host.Phase);
        }

        [Test,Category("CommonProgressionRegression")]
        public void ThreeVisualsAreConnectedAndCannotDealDamage()
        {
            var settings=GameSettings.PerformanceEquipment;
            foreach(CheerPointStyle style in System.Enum.GetValues(typeof(CheerPointStyle)))
            {
                var visual=settings.Visual(style); Assert.NotNull(visual);
                Assert.AreEqual(0,visual.GetComponentsInChildren<Collider>(true).Length);
                Assert.IsFalse(System.Array.Exists(visual.GetComponentsInChildren<Transform>(true),t=>t.name=="Suspension"));
                foreach(var ps in visual.GetComponentsInChildren<ParticleSystem>(true)) Assert.IsFalse(ps.collision.enabled);
            }
        }
    }
}
#endif
