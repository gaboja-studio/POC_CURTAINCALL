using System.Reflection;
using CurtainCall.Damage;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeInjuryFeedbackTests
    {
        [TestCase(BodyPart.None, 1f)]
        [TestCase(BodyPart.LeftArm, 1f)]
        [TestCase(BodyPart.LeftLeg, 0.9f)]
        [TestCase(BodyPart.LeftLeg | BodyPart.RightLeg, 0.65f)]
        public void InjuredSpeedIsCommonBeforeAndAfterLeavingRope(BodyPart lost, float expected)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var go = new GameObject("Injury floor test");
            var policy = ScriptableObject.CreateInstance<TightropeBodyPenalty>();
            try
            {
                var condition = go.AddComponent<PlayerCondition>();
                var mover = go.AddComponent<PlayerMover>();
                var balance = go.AddComponent<PlayerBalance>();
                var effects = go.AddComponent<BodyDamageEffects>();
                typeof(BodyDamageEffects).GetMethod("Awake", flags).Invoke(effects, null);
                condition.SetLostParts(lost);
                effects.SetPolicy(policy);
                typeof(BodyDamageEffects).GetMethod("Update", flags).Invoke(effects, null);
                Assert.That(mover.SpeedMultiplier, Is.EqualTo(expected).Within(0.0001f));
                Assert.That(mover.JumpHeightMultiplier, Is.EqualTo(1f));
                Assert.That(go.GetComponent<PlayerInteraction>().ReachMultiplier, Is.EqualTo(1f));
                Assert.That(balance.SwayMultiplier, Is.EqualTo(GameSettings.Tightrope.RopeMovement.NaturalSwayMultiplier).Within(.0001f),
                    "부상과 관계없이 같은 외줄 기본 흔들림을 쓴다.");
                effects.SetPolicy(null);
                typeof(BodyDamageEffects).GetMethod("Update", flags).Invoke(effects, null);
                Assert.That(mover.SpeedMultiplier, Is.EqualTo(GameSettings.Base.BodyDamage.MoveMultiplier(condition.LostLegCount)));
                Assert.That(balance.SwayMultiplier, Is.EqualTo(1f), "외줄 정책 해제 시 다른 콘텐츠에 배율을 남기지 않는다.");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(policy); }
        }
    }
}
