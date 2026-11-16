using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class IndustrialVignetteTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        static Type Vignette => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeIndustrialVignette")).First(t => t != null);
        static object Call(string name, params object[] args) => Vignette.GetMethod(name, Static).Invoke(null, args);

        [Test] public void SharedClockHasTheSamePhaseAfterLongUptimeAndSupportsAuthoredStagger()
        {
            var phase = Call("PhaseAt", 3.25d, 8f, 1.5f);
            Assert.That(Call("PhaseAt", 80000003.25d, 8f, 1.5f), Is.EqualTo(phase));
            Assert.That(Call("PhaseAt", -4.75d, 8f, 1.5f), Is.EqualTo(phase));
            Assert.That(Call("PhaseAt", 3.25d, 8f, 5.5f), Is.Not.EqualTo(phase));
        }

        [Test] public void EmissionWindowsIncludeContinuousAndWrappedBeatsWithoutEmissionAtClosedEnds()
        {
            Assert.That(Call("InsideWindow", .99f, 0f, 1f), Is.True);
            Assert.That(Call("InsideWindow", .5f, .5f, .75f), Is.True);
            Assert.That(Call("InsideWindow", .75f, .5f, .75f), Is.False);
            Assert.That(Call("InsideWindow", .05f, .9f, .1f), Is.True);
            Assert.That(Call("InsideWindow", .5f, .9f, .1f), Is.False);
            Assert.That(Call("InsideWindow", .5f, .5f, .5f), Is.False);
        }

        [Test] public void AudioCueFiresOnceAcrossACycleAndDoesNotReplayOnLateJoinOrClockRewind()
        {
            Assert.That(Call("CrossedCue", double.NaN, 5d, 8f, 0f, .5f), Is.False);
            Assert.That(Call("CrossedCue", 3.9d, 4d, 8f, 0f, .5f), Is.True);
            Assert.That(Call("CrossedCue", 4d, 4.1d, 8f, 0f, .5f), Is.False);
            Assert.That(Call("CrossedCue", 11.9d, 12.1d, 8f, 0f, .5f), Is.True);
            Assert.That(Call("CrossedCue", 12.1d, 3.9d, 8f, 0f, .5f), Is.False);
        }

        [Test] public void DisablingVignetteRestoresDedicatedRotorAndLightWithoutTogglingObjects()
        {
            var root = new GameObject("Industrial vignette restore"); root.SetActive(false);
            try
            {
                var component = root.AddComponent(Vignette);
                var rotor = new GameObject("Cosmetic impeller").transform; rotor.SetParent(root.transform);
                var initial = Quaternion.Euler(11, 22, 33); rotor.localRotation = initial;
                var rotorType = Vignette.GetNestedType("Rotor"); var entry = Activator.CreateInstance(rotorType);
                rotorType.GetField("target").SetValue(entry, rotor);
                rotorType.GetField("localAxis").SetValue(entry, Vector3.forward);
                rotorType.GetField("degreesPerSecond").SetValue(entry, 90f);
                var rotors = Array.CreateInstance(rotorType, 1); rotors.SetValue(entry, 0);
                Vignette.GetField("rotors").SetValue(component, rotors);
                var light = new GameObject("Machine accent").AddComponent<Light>(); light.transform.SetParent(root.transform); light.intensity = 85;
                Vignette.GetField("pulseLight").SetValue(component, light);
                Vignette.GetMethod("Capture", Instance).Invoke(component, null);
                Vignette.GetMethod("Sample", Instance).Invoke(component, new object[] { 2d });
                Assert.That(Quaternion.Angle(initial, rotor.localRotation), Is.GreaterThan(170));
                Assert.That(light.intensity, Is.InRange(85f * .75f, 85f * 1.25f));
                Vignette.GetMethod("OnDisable", Instance).Invoke(component, null);
                Assert.That(Quaternion.Angle(initial, rotor.localRotation), Is.LessThan(.001f));
                Assert.That(light.intensity, Is.EqualTo(85));
                Assert.That(rotor.gameObject.activeSelf && light.gameObject.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
