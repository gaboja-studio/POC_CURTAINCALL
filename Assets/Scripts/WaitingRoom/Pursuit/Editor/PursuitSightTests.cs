using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitSightTests
    {
        [Test]
        public void ClearLineIncludesOccludersEvenWhenNonAllocBufferIsFull()
        {
            var root = new GameObject("sight-test");
            try {
                var director = root.AddComponent<PursuitDirector>();
                var target = new GameObject("target"); target.transform.SetParent(root.transform);
                for (int i = 0; i < 40; i++) {
                    var child = new GameObject("ignored-target-collider"); child.transform.SetParent(target.transform);
                    child.transform.position = new Vector3(1000 + i * .2f, 1000, 1000);
                    child.AddComponent<BoxCollider>().size = Vector3.one * .1f;
                }
                Physics.SyncTransforms();
                var from = new Vector3(999, 1000, 1000); var to = new Vector3(1009, 1000, 1000);
                Assert.IsTrue(director.ClearLine(from, to, target.transform));
                var wall = new GameObject("wall"); wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector3(1008.5f, 1000, 1000); wall.AddComponent<BoxCollider>();
                Physics.SyncTransforms();
                Assert.IsFalse(director.ClearLine(from, to, target.transform));
            } finally { Object.DestroyImmediate(root); }
        }
    }
}
