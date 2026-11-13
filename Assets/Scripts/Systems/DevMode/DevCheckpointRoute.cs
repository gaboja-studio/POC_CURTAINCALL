using System;
using System.Collections.Generic;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Dev
{
    /// <summary>Scene-authored, ordered Dev-only checkpoints. The list also supplies F9 teleport destinations.</summary>
    [DisallowMultipleComponent]
    public sealed class DevCheckpointRoute : MonoBehaviour
    {
        [Serializable]
        public sealed class Point
        {
            public string label = "체크포인트";
            public Vector3 position;
            public float yaw;
            [Min(.1f)] public float radius = 2f;
            public bool teleportOnly;
            public bool requiresOpenEscapeExit;
            public PlayerSpawnPoints sceneSpawn;
            public TightropeCourse rope;
            public RopePlacement ropePlacement;
        }

        public enum RopePlacement { Fixed, Start, Section, Finish }
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

        internal static bool Available(Point point) => point != null && !string.IsNullOrWhiteSpace(point.label)
            && (!point.requiresOpenEscapeExit || EscapeCampaignContent.Current?.ExitOpen == true);

        internal bool TryPose(Point point, int slot, out Pose pose)
        {
            pose = PoseFor(point);
            if (point.sceneSpawn != null)
            {
                if (!point.sceneSpawn.TryGetSlotPose(slot, out var position, out var rotation))
                    return point.sceneSpawn.TryGetSlotPose(0, out _, out _);
                if (point.sceneSpawn.TryGetSlotPose(0, out var reference, out var referenceRotation))
                    pose = new Pose(pose.position + position - reference, rotation * Quaternion.Inverse(referenceRotation) * pose.rotation);
            }
            var rope = point.rope;
            if (point.ropePlacement == RopePlacement.Fixed || rope == null) return true;
            if (!rope.isActiveAndEnabled || rope.LaneCount == 0) return false;
            if (point.ropePlacement == RopePlacement.Section)
            {
                float along = rope.GetDistance(pose.position);
                int authored = rope.Layout.GetLaneSlotAt(rope.GetSide(pose.position));
                for (int i = 0; i < rope.LaneCount; i++)
                {
                    int lane = (Mathf.Max(0, slot) + i) % rope.LaneCount;
                    if (!rope.IsLaneUsable(lane, along)) continue;
                    pose.position += rope.GetLanePosition(lane, along) - rope.GetLanePosition(authored, along);
                    return true;
                }
                return false;
            }
            var own = point.ropePlacement == RopePlacement.Start ? rope.GetStartPose(slot) : rope.GetFinishPose(slot);
            var anchor = point.ropePlacement == RopePlacement.Start ? rope.GetStartPose(0) : rope.GetFinishPose(0);
            pose.position += own.position - anchor.position;
            return true;
        }

        internal bool TryRecognize(Point point, Vector3 feet, int slot, out int matchedSlot)
        {
            // Recognize any playable lane, since players can switch lanes during a course.
            int lanes = point.ropePlacement == RopePlacement.Fixed || point.rope == null ? 0 : point.rope.LaneCount;
            float best = Mathf.Max(.1f, point.radius);
            best *= best;
            matchedSlot = -1;
            for (int i = -1; i < lanes; i++)
            {
                int candidate = i < 0 ? slot : i;
                if (i >= 0 && candidate == slot || !TryPose(point, candidate, out var pose)) continue;
                float distance = (feet - pose.position).sqrMagnitude;
                if (distance > best || matchedSlot >= 0 && distance == best) continue;
                best = distance;
                matchedSlot = candidate;
            }
            return matchedSlot >= 0;
        }

        internal void Collect(int slot, List<DevCheats.TeleportPoint> target)
        {
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (!Available(point) || !TryPose(point, slot, out var pose)) continue;
                target.Add(new DevCheats.TeleportPoint(point.label.Trim(), pose.position, pose.rotation, !point.teleportOnly, i));
            }
        }
    }
}
