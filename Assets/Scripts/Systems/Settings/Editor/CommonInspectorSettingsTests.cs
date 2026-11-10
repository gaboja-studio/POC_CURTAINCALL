using System.Reflection;
using CurtainCall.HumanCannon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.Settings.EditorTools.Tests
{
    [Category("CommonInspectorSettings")]
    public sealed class CommonInspectorSettingsTests
    {
        [TestCase(1)] [TestCase(3)] [TestCase(4)]
        public void CreatedPoleServiceUsesConfiguredCapacity(int capacity)
        {
            var rules = GameSettings.Poles;
            string saved = JsonUtility.ToJson(rules);
            var root = new GameObject("inspector capacity"); root.SetActive(false);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"capacity\":" + capacity + "}", rules);
                var pole = root.AddComponent<HumanCannonPole>();
                var service = root.AddComponent<HumanCannonPoleSystem>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(HumanCannonPoleSystem).GetField("poles", flags).SetValue(service, new[] { pole });
                typeof(HumanCannonPoleSystem).GetMethod("InitializeStates", flags).Invoke(service, null);
                Assert.AreEqual(capacity, pole.RiderCapacity);
                var state = service.State(0);
                Assert.AreEqual(capacity, state.Capacity);
                for (int seat = 0; seat < capacity; seat++)
                    Assert.IsTrue(state.TryGrab((ulong)(seat + 1), state.Revision, 0, 30, false, seat));
                Assert.IsFalse(state.HasFreeSeat);
                Assert.IsFalse(state.TryGrab(99, state.Revision, 0, 30, false, capacity));
                if (capacity > 1)
                    Assert.That(Vector3.Distance(pole.GrabPoint(0, capacity - 1),
                        pole.HangingPosition(0, capacity - 2) + Vector3.up * pole.HangLength * .5f), Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(root); JsonUtility.FromJsonOverwrite(saved, rules); }
        }

        [TestCase(0, 1)] [TestCase(20, 4)]
        public void CapacityStaysInsideNetworkSupport(int entered, int applied)
        {
            var rules = ScriptableObject.CreateInstance<PoleSettings>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"capacity\":" + entered + "}", rules);
                Assert.AreEqual(applied, rules.Capacity);
            }
            finally { Object.DestroyImmediate(rules); }
        }

        [Test]
        public void PoleAndTrampolineInspectorFieldsExplainTheirEffect()
        {
            foreach (var asset in new ScriptableObject[] { GameSettings.Poles, GameSettings.Trampoline })
            {
                var serialized = new SerializedObject(asset);
                var inspector = Editor.CreateEditor(asset);
                Assert.IsInstanceOf<KoreanRewardSettingsEditor>(inspector, "한글 라벨을 실제 Inspector에서 사용");
                Object.DestroyImmediate(inspector);
                foreach (var field in asset.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (field.GetCustomAttribute<SerializeField>() == null) continue;
                    Assert.IsNotNull(serialized.FindProperty(field.Name), field.Name + " must be editable");
                    Assert.IsNotNull(field.GetCustomAttribute<InspectorNameAttribute>(), field.Name + " Korean name");
                    Assert.That(field.GetCustomAttribute<TooltipAttribute>()?.tooltip?.Length, Is.GreaterThan(15), field.Name);
                    var content = KoreanRewardSettingsEditor.LabelFor(serialized.FindProperty(field.Name), asset.GetType());
                    Assert.AreEqual(field.GetCustomAttribute<InspectorNameAttribute>().displayName, content.text);
                    Assert.AreEqual(field.GetCustomAttribute<TooltipAttribute>().tooltip, content.tooltip);
                }
            }
        }

        [Test]
        public void CommonRuleDefaultsKeepCurrentGameplayAndExposeRopeGravity()
        {
            var rope = GameSettings.Tightrope.RopeMovement;
            string saved = JsonUtility.ToJson(rope);
            try
            {
                Assert.AreEqual(2, GameSettings.Poles.Capacity);
                Assert.IsTrue(GameSettings.Poles.TopRiderOnlySwing);
                Assert.IsTrue(GameSettings.Poles.PreventReleaseWithPassengers);
                Assert.AreEqual(1, GameSettings.Base.BodyDamage.HazardCutCount);
                Assert.AreEqual(2, GameSettings.Base.BodyDamage.FallInjuryCutCount);
                Assert.IsTrue(GameSettings.Base.Interaction.PrioritizeRescue);
                Assert.AreEqual(1f, rope.JumpGravityMultiplier);
                float height = rope.JumpHeight;
                JsonUtility.FromJsonOverwrite("{\"jumpGravityMultiplier\":2}", rope);
                Assert.AreEqual(2f, rope.JumpGravityMultiplier);
                Assert.AreEqual(height, rope.JumpHeight, "중력 배율은 설정한 도약 높이를 바꾸지 않는다.");
            }
            finally { JsonUtility.FromJsonOverwrite(saved, rope); }
        }
    }
}
