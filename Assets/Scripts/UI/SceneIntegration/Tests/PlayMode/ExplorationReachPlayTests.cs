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
