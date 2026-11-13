using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class PursuerScreenTests
    {
        static bool Cross(Vector3[] corners, Vector3 from, Vector3 to)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute.EscapePursuerScreens")).First(t => t != null);
            return (bool)type.GetMethod("CrossesPanel", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { corners, from, to });
        }
        static Vector3[] Panel => new[] { new Vector3(-2,0,0), new Vector3(2,0,0), new Vector3(2,6,0), new Vector3(-2,6,0) };

        [TestCase(0f, 1f, true)]
        [TestCase(2.2f, 1f, false)]
        [TestCase(0f, 7f, false)]
        public void OnlyThePanelBetweenCreatureAndRunnerCanOpen(float x, float y, bool expected)
            => Assert.That(Cross(Panel, new Vector3(x,y,-3), new Vector3(x,y,3)), Is.EqualTo(expected));

        [Test] public void ParallelOrBeyondTheRunnerDoesNotOpen()
        {
            Assert.That(Cross(Panel, new Vector3(-1,1,-3), new Vector3(1,1,-3)), Is.False);
            Assert.That(Cross(Panel, new Vector3(0,1,-3), new Vector3(0,1,-1)), Is.False);
        }
        [Test] public void SlopingBayUsesItsActualTrapezoid()
        {
            var panel = Panel; panel[1] += Vector3.up * 2; panel[2] += Vector3.up * 2;
            Assert.That(Cross(panel, new Vector3(1.8f,1,-3), new Vector3(1.8f,1,3)), Is.False);
            Assert.That(Cross(panel, new Vector3(1.8f,3,-3), new Vector3(1.8f,3,3)), Is.True);
        }
        [Test] public void EndShutterOpensWhenRunnerIsOnEitherSide()
        {
            Assert.That(Cross(Panel, new Vector3(0,3,-3), new Vector3(0,1,3)), Is.True);
            Assert.That(Cross(Panel.Reverse().ToArray(), new Vector3(0,3,3), new Vector3(0,1,-3)), Is.True);
        }
    }
}
