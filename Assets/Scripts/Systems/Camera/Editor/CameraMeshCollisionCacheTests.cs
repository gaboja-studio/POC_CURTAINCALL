using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Cameras.EditorTools
{
    /// <summary>
    /// 2026-11-26 Task-204 그네 탑승 프레임 드랍: 움직이는 메시(그네 봉·조명 빔)가 카메라 시선에 걸려도
    /// 매 프레임 새 배열을 만들지 않고, 판정 결과는 이동한 위치를 그대로 따른다.
    /// </summary>
    public sealed class CameraMeshCollisionCacheTests
    {
        static readonly Type CollisionType = typeof(CameraOcclusionFade).Assembly.GetType("CurtainCall.Cameras.CameraMeshCollision");
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static Mesh Quad()
        {
            var mesh = new Mesh { name = "Camera test wall" };
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static bool Hit(object collision, Mesh mesh, Matrix4x4 matrix, Vector3 origin, bool deferrable, out float distance)
        {
            var args = new object[] { mesh, matrix, origin, Vector3.forward, .05f, 20f, 0f, deferrable };
            bool hit = (bool)CollisionType.GetMethod("Intersect", Any).Invoke(collision, args);
            distance = (float)args[6];
            return hit;
        }

        [Test]
        public void MovingMeshReusesItsTriangleArrayAndFollowsTheNewPose()
        {
            var mesh = Quad();
            try
            {
                var collision = Activator.CreateInstance(CollisionType, true);
                var at5 = Matrix4x4.TRS(new Vector3(0, 0, 5), Quaternion.identity, Vector3.one);
                Assert.IsTrue(Hit(collision, mesh, at5, Vector3.zero, false, out float first));
                Assert.That(first, Is.EqualTo(5f - .05f).Within(.01f));
                var triangles = CollisionType.GetField("triangles", Any).GetValue(collision);
                // 봉처럼 흔들려 위치가 바뀐다: 같은 배열을 다시 쓰고 새 위치에서 맞는다.
                var at8 = Matrix4x4.TRS(new Vector3(0, 0, 8), Quaternion.identity, Vector3.one);
                Assert.IsTrue(Hit(collision, mesh, at8, Vector3.zero, false, out float moved));
                Assert.That(moved, Is.EqualTo(8f - .05f).Within(.01f));
                Assert.AreSame(triangles, CollisionType.GetField("triangles", Any).GetValue(collision), "움직임마다 새 배열을 할당하지 않는다");
                var aside = Matrix4x4.TRS(new Vector3(5, 0, 8), Quaternion.identity, Vector3.one);
                Assert.IsFalse(Hit(collision, mesh, aside, Vector3.zero, false, out _), "시선에서 벗어난 위치는 가리지 않는다");
            }
            finally { Object.DestroyImmediate(mesh); CollisionType.GetMethod("ClearSharedCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); }
        }

        [Test]
        public void OcclusionBuildsAreSpreadAcrossFramesButCameraWallsAreNever()
        {
            var big = new Mesh { name = "Large decorative mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            int budget = (int)CollisionType.GetField("TriangleBudgetPerFrame", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
            int quads = budget / 2 + 10; // 예산을 넘는 삼각형 수
            var vertices = new Vector3[quads * 4]; var indices = new int[quads * 6];
            for (int q = 0; q < quads; q++)
            {
                float x = q * .001f;
                vertices[q * 4] = new Vector3(-1 + x, -1, 0); vertices[q * 4 + 1] = new Vector3(1 + x, -1, 0);
                vertices[q * 4 + 2] = new Vector3(1 + x, 1, 0); vertices[q * 4 + 3] = new Vector3(-1 + x, 1, 0);
                int v = q * 4; int i = q * 6;
                indices[i] = v; indices[i + 1] = v + 1; indices[i + 2] = v + 2; indices[i + 3] = v; indices[i + 4] = v + 2; indices[i + 5] = v + 3;
            }
            big.vertices = vertices; big.triangles = indices; big.RecalculateBounds();
            var small = Quad();
            try
            {
                var matrix = Matrix4x4.TRS(new Vector3(0, 0, 5), Quaternion.identity, Vector3.one);
                var first = Activator.CreateInstance(CollisionType, true);
                var second = Activator.CreateInstance(CollisionType, true);
                var wall = Activator.CreateInstance(CollisionType, true);
                Assert.IsTrue(Hit(first, small, matrix, Vector3.zero, true, out _), "이번 프레임 첫 메시는 바로 만든다");
                Assert.IsFalse(Hit(second, big, matrix, Vector3.zero, true, out _), "가림 표시는 예산을 넘으면 다음 프레임으로 미룬다");
                Assert.IsTrue(Hit(wall, big, matrix, Vector3.zero, false, out _), "카메라 벽 충돌은 예산과 관계없이 즉시 계산한다");
            }
            finally
            {
                Object.DestroyImmediate(big); Object.DestroyImmediate(small);
                CollisionType.GetMethod("ClearSharedCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            }
        }
    }
}
