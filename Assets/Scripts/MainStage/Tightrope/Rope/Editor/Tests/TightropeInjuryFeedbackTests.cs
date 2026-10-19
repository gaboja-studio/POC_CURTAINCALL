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
        [TestCase(BodyPart.LeftLeg, 0.8f)]
        [TestCase(BodyPart.LeftLeg | BodyPart.RightLeg, 0.8f)]
        public void FinalInjuredSpeedKeepsEightyPercentAndRestoresCommonRules(BodyPart lost, float expected)
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
                Assert.That(mover.JumpHeightMultiplier,
                    Is.EqualTo(GameSettings.Base.BodyDamage.JumpHeightForLostLegs(condition.LostLegCount)));
                Assert.That(balance.SwayMultiplier, Is.EqualTo(GameSettings.Tightrope.RopeMovement.NaturalSwayMultiplier
                    * (1f + GameSettings.Tightrope.BodyPenalty.SwayPerLostLeg * condition.LostLegCount)).Within(.0001f),
                    "건강할 때의 외줄 흔들림과 부상 증가분을 함께 적용한다.");
                effects.SetPolicy(null);
                typeof(BodyDamageEffects).GetMethod("Update", flags).Invoke(effects, null);
                Assert.That(mover.SpeedMultiplier, Is.EqualTo(condition.LostLegCount >= 2
                    ? GameSettings.Base.BodyDamage.CrawlSpeedMultiplier : 1f));
                Assert.That(balance.SwayMultiplier, Is.EqualTo(1f), "외줄 정책 해제 시 다른 콘텐츠에 배율을 남기지 않는다.");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(policy); }
        }
    }
}
