using System;
using System.Collections.Generic;
using CurtainCall.EscapeRoute;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Dev
{
    /// <summary>Scene-authored order for developer teleport and team recovery. Positions are relative to this transform.</summary>
    [DisallowMultipleComponent]
    public sealed class DevCheckpointRoute : MonoBehaviour
    {
        [Serializable]
        public sealed class Point
        {
            public string label = "체크포인트";
            public Vector3 position;
            public float yaw;
            public bool teleportOnly;
            public bool requiresOpenEscapeExit;
        }

        [SerializeField] List<Point> points = new();
        public IReadOnlyList<Point> Points => points;

        public Pose PoseFor(Point point) => new(transform.TransformPoint(point.position),
            transform.rotation * Quaternion.Euler(0, point.yaw, 0));

        public static DevCheckpointRoute In(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<DevCheckpointRoute>() is { } route && route.isActiveAndEnabled) return route;
            return null;
        }

        internal void Collect(List<DevCheats.TeleportPoint> target)
        {
            foreach (var point in points)
            {
                if (point == null || string.IsNullOrWhiteSpace(point.label)
                    || (point.requiresOpenEscapeExit && EscapeCampaignContent.Current?.ExitOpen != true)) continue;
                var pose = PoseFor(point);
                target.Add(new DevCheats.TeleportPoint(point.label.Trim(), pose.position, pose.rotation, !point.teleportOnly));
            }
        }
    }
}
