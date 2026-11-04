using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    // Decorative machines on one client share the same local gameplay or spectator camera.
    internal static class EscapeSceneryView
    {
        static readonly Plane[] Planes = new Plane[6];
        static int cameraFrame = -1;
        static int planesFrame = -1;
        static Camera viewer;

        internal static Camera LocalCamera
        {
            get
            {
                int frame = Time.frameCount;
                if (cameraFrame != frame)
                {
                    cameraFrame = frame;
                    viewer = Camera.main;
                }
                return viewer;
            }
        }

        internal static bool InRange(Bounds bounds, float maxDistance)
        {
            var camera = LocalCamera;
            if (!camera || bounds.SqrDistance(camera.transform.position) > maxDistance * maxDistance)
                return false;

            int frame = Time.frameCount;
            if (planesFrame != frame)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, Planes);
                planesFrame = frame;
            }
            return GeometryUtility.TestPlanesAABB(Planes, bounds);
        }
    }
}
