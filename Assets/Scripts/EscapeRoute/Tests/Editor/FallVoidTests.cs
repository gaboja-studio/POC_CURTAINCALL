using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.Tests
{
    public sealed class FallVoidTests
    {
        [TestCase(67, -18.5f, -17, false)] // 그네 탑승 높이
        [TestCase(66, -22, 14, false)] // 기존 진입 대포 준비대
        [TestCase(46, -29.9f, -74, false)]
        [TestCase(46, -30, -74, true)]
        [TestCase(67, -500, -17, true)] // 빠른 낙하도 아래쪽 한계로 놓치지 않음
        [TestCase(20, -31, -82, true)]
        [TestCase(146, -31, 18, true)]
        [TestCase(19.9f, -31, -74, false)]
        [TestCase(146.1f, -31, -74, false)]
        [TestCase(46, -31, -82.1f, false)]
        [TestCase(46, -31, 18.1f, false)] // 인접 시설 침범 방지
        [TestCase(float.NaN, -31, -74, false)]
        public void FallRegionExcludesTraversalAndOtherSections(float x, float y, float z, bool lethal)
        {
            var root = new GameObject("Offline fall bounds") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.EscapeRoute.EscapeFallVoid")).First(t => t != null);
                var component = root.AddComponent(type);
                Assert.That(type.GetMethod("Contains").Invoke(component, new object[] { new Vector3(x, y, z) }), Is.EqualTo(lethal));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
