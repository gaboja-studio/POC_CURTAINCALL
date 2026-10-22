using System.Collections.Generic;
using CurtainCall.Cameras;
using UnityEngine;

namespace CurtainCall.Telemetry.Pipeline
{
    /// <summary>Same-client monotonic intervals. Unobserved origins remain unknown, never zero.</summary>
    public sealed class WaitingRoomTiming
    {
        readonly HashSet<string> reached = new();
        double? spawned, ready, firstLeft, alarm;
        public void Reset() { spawned = ready = firstLeft = alarm = null; reached.Clear(); }
        public void Spawn(double now) { spawned = now; reached.Clear(); }
        public void Ready(bool value, double now) => ready = value ? now : null;
        public void BeginBreak() { firstLeft = alarm = null; }
        public void FirstLeft(double now) { firstLeft ??= now; }
        public void Alarm(double now) { alarm ??= now; }
        static double? Elapsed(double? from, double now) => from.HasValue && now >= from.Value ? (now - from.Value) * 1000d : null;
        public double? FirstArrival(string zone, double now) => spawned.HasValue && reached.Add(zone) ? Elapsed(spawned, now) : null;
        public double? Entrance(double now) { var elapsed = Elapsed(ready, now); ready = null; return elapsed; }
        public (double? FirstLeftMs, double? AlarmMs) Assembly(double now) => (Elapsed(firstLeft, now), Elapsed(alarm, now));
    }
    /// <summary>Uses the scene's existing room camera volumes, without changing their gameplay or camera behavior.</summary>
    public static class WaitingRoomZoneMap
    {
        public readonly struct Region
        {
            public readonly string Type, Source;
            public readonly BoxCollider Boundary;
            public Region(string type, string source, BoxCollider boundary = null) { Type = type; Source = source; Boundary = boundary; }
        }

        public static string RoomType(string volumeName) => volumeName switch
        {
            "Zone_Dormitory" => "BEDROOM", "Zone_Restroom" => "RESTROOM",
            "Zone_PropRoom" => "STORAGE", "Zone_JunkStorage" => "JUNK_STORAGE",
            "Zone_Corridor" => "HALLWAY", "Zone_ExpertRoom" => "EXPERT_ROOM", _ => null
        };

        public static bool Contains(BoxCollider volume, Vector3 feet)
        {
            if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy) return false;
            var point = volume.transform.InverseTransformPoint(feet + Vector3.up * .2f) - volume.center;
            var half = volume.size * .5f;
            return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y && Mathf.Abs(point.z) <= half.z;
        }

        public static Region Find(IEnumerable<CameraViewZone> volumes, Vector3 feet)
        {
            BoxCollider match = null;
            string type = null;
            foreach (var view in volumes)
            {
                if (view == null || !view.isActiveAndEnabled || view.transform.parent == null
                    || view.transform.parent.name != "CameraZones") continue;
                string room = RoomType(view.name);
                if (room == null || !view.TryGetComponent(out BoxCollider boundary) || !boundary.isTrigger || !Contains(boundary, feet)) continue;
                // Ambiguous authored volumes must not silently attribute a visit to an arbitrary room.
                if (match != null) return new("WAITINGROOM", "AMBIGUOUS_CAMERA_VOLUMES");
                type = room; match = boundary;
            }
            return match != null ? new(type, "CAMERA_ZONE_VOLUME", match) : new("WAITINGROOM", "SCENE_FALLBACK");
        }
    }
}
