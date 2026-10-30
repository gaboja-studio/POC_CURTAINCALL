using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class SwitchInteractionTests
    {
        GameObject actor, button;
        Component interaction;
        Type rule;
        Mesh mesh;
        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Call(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.Public).Invoke(null, args);
        Vector3 ActorCenter => (Vector3)Call(interaction.GetType(), "Center", actor.transform);
        Vector3 ButtonCenter => (Vector3)Call(rule, "Center", button.transform);
        bool InRange => (bool)Call(rule, "InRange", actor.transform, button.transform, 2.5f);

        [SetUp]
        public void SetUp()
        {
            rule = T("CurtainCall.EscapeRoute.EscapeSwitchInteraction");
            actor = new GameObject("Switch reach actor");
            interaction = actor.AddComponent(T("CurtainCall.Player.PlayerInteraction"));
            button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.transform.position = new Vector3(7000, 40, 7000);
        }
        [TearDown]
        public void Cleanup() { Object.DestroyImmediate(actor); Object.DestroyImmediate(button); if (mesh != null) Object.DestroyImmediate(mesh); }
        void Place(Vector3 offset) { actor.transform.position += ButtonCenter + offset - ActorCenter; }

        [TestCase(1,0,0)] [TestCase(-1,0,0)] [TestCase(0,1,0)] [TestCase(0,-1,0)]
        [TestCase(0,0,1)] [TestCase(0,0,-1)] [TestCase(1,1,1)] [TestCase(-1,-1,-1)]
        public void ButtonSphere_HasTheSameBoundaryInEveryDirection(float x, float y, float z)
        {
            var direction = new Vector3(x,y,z).normalized;
            Place(direction * 2.49f); Assert.That(InRange, Is.True);
            actor.transform.rotation = Quaternion.Euler(0, 180, 0); Assert.That(InRange, Is.True);
            Place(direction * 2.51f); Assert.That(InRange, Is.False);
        }

        [Test]
        public void ButtonSphere_UsesVisibleButtonCenterInsteadOfItsOffsetPivot()
        {
            var filter = button.GetComponent<MeshFilter>(); mesh = Object.Instantiate(filter.sharedMesh);
            mesh.vertices = mesh.vertices.Select(v => v + Vector3.right * 4).ToArray(); mesh.RecalculateBounds(); filter.sharedMesh = mesh;
            button.transform.rotation = Quaternion.Euler(15, 37, 0); button.transform.localScale = new Vector3(2, 3, .5f);
            Assert.That(Vector3.Distance(ButtonCenter, button.transform.position), Is.GreaterThan(7));
            Place(Vector3.forward * 2.49f); Assert.That(InRange, Is.True);
            actor.transform.position += button.transform.position - ActorCenter; Assert.That(InRange, Is.False);
        }

        [Test]
        public void ButtonSphere_IgnoresArmReachPenaltyButRejectsCornersAndInactiveButtons()
        {
            Place(new Vector3(2,0,2)); Assert.That(InRange, Is.False);
            interaction.GetType().GetProperty("ReachMultiplier").SetValue(interaction, .5f);
            Place(Vector3.right * 1.24f); Assert.That(InRange, Is.True);
            Place(Vector3.right * 2.49f); Assert.That(InRange, Is.True);
            Place(Vector3.right * 2.51f); Assert.That(InRange, Is.False);
            interaction.GetType().GetProperty("ReachMultiplier").SetValue(interaction, 0f);
            Place(Vector3.right * 2f); Assert.That(InRange, Is.True);
            interaction.GetType().GetProperty("ReachMultiplier").SetValue(interaction, 1f);
            button.SetActive(false); Assert.That(InRange, Is.False);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void RaisedSwitch_RemainsReachableWithShortenedLegsAndCutArms(int lostCount)
        {
            var conditionType = T("CurtainCall.Player.PlayerCondition");
            var condition = actor.AddComponent(conditionType);
            conditionType.GetMethod("SetLostParts").Invoke(condition, new[] { Call(conditionType, "PartsForLostCount", lostCount) });
            var moverType = T("CurtainCall.Player.PlayerMover");
            var mover = actor.AddComponent(moverType);
            var body = actor.GetComponent<CharacterController>();
            moverType.GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mover, body);
            body.height = .6f; body.center = Vector3.up * .3f;
            interaction.GetType().GetProperty("ReachMultiplier").SetValue(interaction, .5f);
            // The lower player is also shortened: upper feet are only .6m above the floor.
            actor.transform.position = ButtonCenter - Vector3.up * 3f + Vector3.right;
            Assert.That(Vector3.Distance(ActorCenter, ButtonCenter), Is.GreaterThan(2.5f));
            Assert.That(InRange, Is.True);
            actor.transform.position += Vector3.right * 3f;
            Assert.That(InRange, Is.False);
        }
    }
}
