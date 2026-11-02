using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed partial class PursuitDesignerWindow
    {
        struct LiveWall
        {
            public Bounds Bounds;
            public Vector3 A, B, C, D;
        }
        readonly List<LiveWall> liveWalls = new();
        readonly Vector3[] liveWallQuad = new Vector3[4];
        int liveGeometryScene;

        void CacheLiveWalls()
        {
            liveWalls.Clear(); liveGeometryScene = director.gameObject.scene.handle;
            foreach (var box in FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)) {
                if (box.gameObject.scene != director.gameObject.scene || !box.enabled || box.isTrigger || box.attachedRigidbody != null) continue;
                // Only authored fixed structure; doors, furniture and actor colliders
                // must not become stale navigation barriers in the observation map.
                string name = box.name;
                if (name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("Column", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (box.bounds.size.y < 1) continue;
                var c = box.center; var e = box.size * .5f; var t = box.transform;
                liveWalls.Add(new LiveWall {
                    Bounds = box.bounds,
                    A = t.TransformPoint(c + new Vector3(-e.x, 0, -e.z)),
                    B = t.TransformPoint(c + new Vector3(-e.x, 0, e.z)),
                    C = t.TransformPoint(c + new Vector3(e.x, 0, e.z)),
                    D = t.TransformPoint(c + new Vector3(e.x, 0, -e.z))
                });
            }
        }
        void DrawLiveWalls(Rect canvas)
        {
            Handles.color = new Color(.48f, .57f, .61f, .8f);
            foreach (var wall in liveWalls) {
                if (wall.Bounds.min.y > ActiveFloor + 1.2f || wall.Bounds.max.y < ActiveFloor + .4f) continue;
                var a = Project(wall.Bounds.min, viewBounds, canvas); var b = Project(wall.Bounds.max, viewBounds, canvas);
                if (!Rect.MinMaxRect(a.x, b.y, b.x, a.y).Overlaps(canvas)) continue;
                liveWallQuad[0] = Project(wall.A, viewBounds, canvas); liveWallQuad[1] = Project(wall.B, viewBounds, canvas);
                liveWallQuad[2] = Project(wall.C, viewBounds, canvas); liveWallQuad[3] = Project(wall.D, viewBounds, canvas);
                Handles.DrawAAConvexPolygon(liveWallQuad);
            }
        }
    }
}
