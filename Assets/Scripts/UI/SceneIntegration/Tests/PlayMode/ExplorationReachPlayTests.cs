using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed class ExplorationReachPlayTests
    {
        [UnityTest]
        public IEnumerator Shelf_RejectsOtherFloorsAndPartitionsForSelectionAndAuthority()
        {
            var root = new GameObject("shelf-reach-fixture"); root.SetActive(false);
            var at = new Vector3(1000, 1000, 1000); root.transform.position = at;
            for (int i = 0; i < 4; i++)
            {
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "Board_" + i; board.transform.SetParent(root.transform, false);
                board.transform.localPosition = Vector3.up * (.4f + i * .4f);
                board.transform.localScale = new Vector3(2, .1f, .5f);
            }
            var shelf = root.AddComponent(T("HandoverShelfView"));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = at + new Vector3(0, 1, .6f);
            wall.transform.localScale = new Vector3(4, 4, .1f);
            try
            {
                root.SetActive(true); Physics.SyncTransforms();
                var type = shelf.GetType();
                float Local(Vector3 feet) => (float)type.GetMethod("Reach").Invoke(shelf, new object[] { feet });
                bool Host(Vector3 feet) => (bool)type.GetMethod("CanUse").Invoke(null, new object[] { feet });
                var front = at + Vector3.forward;
                foreach (var feet in new[] { front, front + Vector3.down * 5, front + Vector3.up * 5 })
                { Assert.Less(Local(feet), 0); Assert.IsFalse(Host(feet)); }
                wall.SetActive(false); Physics.SyncTransforms();
                Assert.GreaterOrEqual(Local(front), 0, "The shelf's own boards must not block use");
                Assert.IsTrue(Host(front));
                foreach (var feet in new[] { front + Vector3.down * 5, front + Vector3.up * 5, at + Vector3.forward * 4 })
                { Assert.Less(Local(feet), 0); Assert.IsFalse(Host(feet)); }
                root.SetActive(false);
                Assert.IsFalse(Host(front), "Inactive shelves cannot accept network requests");
            }
            finally { Object.Destroy(root); Object.Destroy(wall); }
            yield return null;
        }

        static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.WaitingRoom." + name)).First(t => t != null);
        static float Reach(Component c, Vector3 feet) => (float)T("IExplorationTarget").GetMethod("Reach").Invoke(c, new object[] { feet });
        [UnityTest]
        public IEnumerator ExplorationReach_RejectsOtherFloorsAndWallsForEveryTargetKind()
        {
            var at = new Vector3(1000, 1000, 1000);
            foreach (var name in new[] { "ClueInspectable", "ToolTarget", "UvMark", "BreakableWall", "ExplorationGate" })
            {
                var go = new GameObject("reach-fixture-" + name); go.transform.position = at;
                var target = go.AddComponent(T(name));
                var own = go.AddComponent<BoxCollider>(); own.center = Vector3.up * .6f; own.size = new Vector3(.4f, 1.2f, .4f);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = at + new Vector3(-.5f, 1, 0); wall.transform.localScale = new Vector3(.12f, 4, 3);
                try
                {
                    Physics.SyncTransforms();
                    Assert.Less(Reach(target, at + Vector3.down * 5), 0, name + ": lower storey");
                    Assert.Less(Reach(target, at + Vector3.up * 5), 0, name + ": upper storey");
                    Assert.Less(Reach(target, at + Vector3.left), 0, name + ": opaque partition");
                    wall.SetActive(false); Physics.SyncTransforms();
                    Assert.GreaterOrEqual(Reach(target, at + Vector3.left), 0, name + ": own collider must not block use");
                    Assert.Less(Reach(target, at + Vector3.left * 10), 0, name + ": distance");
                }
                finally { Object.Destroy(go); Object.Destroy(wall); }
                yield return null;
            }
        }
    }
}
