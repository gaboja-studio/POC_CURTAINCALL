using System.Collections.Generic;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>원본 운반 조작·카메라를 공유한다. 반동/절단 등 장치 고유 판정은 각 장치가 맡는다.</summary>
    public interface IHumanCannonCarry
    {
        ulong Carrier { get; }
        Transform CarryRoot { get; }
        Transform Handle { get; }
        Vector3 GrabPoint { get; }
        Vector3 CarryFocus { get; }
        Vector2 CarryHalfSize { get; }
        float CarryMoveSpeed { get; }
        string CarryName { get; }
        bool CanGrab(NetworkPlayer player);
        void RequestGrab();
        void RequestDrop();
        void RequestUse();
        void ServerReleaseForInspection(ulong actor);
    }

    public static class HumanCannonCarry
    {
        static Collider[] obstacles = new Collider[64];

        // Both carried devices use the actual body, including the swept translation and intermediate turns.
        public static bool CanMove(Transform root, Collider body, Vector3 position, Quaternion rotation, NetworkPlayer carrier, bool sweep = true)
        {
            if (body == null) return false;
            var bounds = body is BoxCollider box ? new Bounds(box.center, box.size)
                : body is MeshCollider mesh && mesh.sharedMesh != null ? mesh.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            Vector3 localCenter = root.InverseTransformPoint(body.transform.TransformPoint(bounds.center));
            Vector3 localBody = root.InverseTransformPoint(body.transform.position);
            Vector3 half = Vector3.Scale(bounds.extents, body.transform.lossyScale);
            float skin = CurtainCall.Settings.GameSettings.Base.CarryDevices.CollisionSkin;
            half = new Vector3(Mathf.Max(.001f, Mathf.Abs(half.x) - skin), Mathf.Max(.001f, Mathf.Abs(half.y) - skin), Mathf.Max(.001f, Mathf.Abs(half.z) - skin));
            Quaternion relative = Quaternion.Inverse(root.rotation) * body.transform.rotation;
            int steps = sweep ? Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(root.position, position) / .05f),
                Mathf.CeilToInt(Quaternion.Angle(root.rotation, rotation) / 3f)) : 1;
            if (steps > 256) return false; // A teleport must be reconciled, not swept through an entire room.
            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                var pose = sweep ? Quaternion.Slerp(root.rotation, rotation, t) : rotation;
                var at = (sweep ? Vector3.Lerp(root.position, position, t) : position) + pose * Vector3.Scale(localCenter, root.lossyScale);
                int count;
                do {
                    count = Physics.OverlapBoxNonAlloc(at, half, obstacles, pose * relative, ~0, QueryTriggerInteraction.Ignore);
                    if (count < obstacles.Length) break;
                    System.Array.Resize(ref obstacles, obstacles.Length * 2);
                } while (true);
                for (int i = 0; i < count; i++)
                {
                    var hit = obstacles[i];
                    if (hit == null || hit.transform.IsChildOf(root) || carrier != null && hit.transform.IsChildOf(carrier.transform)) continue;
                    var bodyPosition = (sweep ? Vector3.Lerp(root.position, position, t) : position) + pose * Vector3.Scale(localBody, root.lossyScale);
                    if (Physics.ComputePenetration(body, bodyPosition, pose * relative, hit, hit.transform.position, hit.transform.rotation,
                        out _, out float depth) && depth > skin) return false;
                }
            }
            return true;
        }

        static readonly List<IHumanCannonCarry> devices = new();
        public static void Register(IHumanCannonCarry device) { if (!devices.Contains(device)) devices.Add(device); }
        public static void Unregister(IHumanCannonCarry device) => devices.Remove(device);
        static bool Active(IHumanCannonCarry device) => device is Behaviour source && source != null && source.isActiveAndEnabled;
        public static IHumanCannonCarry Held(ulong actor)
        {
            if (actor != CannonSeatState.Nobody) foreach (var device in devices)
                if (Active(device) && device.Carrier == actor) return device;
            return null;
        }
        public static IHumanCannonCarry FindGrabbable(NetworkPlayer player)
        {
            if (player == null || Held(player.OwnerClientId) != null) return null;
            IHumanCannonCarry result = null;
            float nearest = float.PositiveInfinity;
            foreach (var device in devices)
            {
                if (!Active(device) || !device.CanGrab(player)) continue;
                float distance = (device.GrabPoint - PlayerInteraction.Center(player)).sqrMagnitude;
                if (distance < nearest) { nearest = distance; result = device; }
            }
            return result;
        }

        public static bool TryFollowPose(NetworkPlayer carrier, Transform root, Transform handle, float handleHeight,
            bool alignToCarrier, out Vector3 position, out Quaternion rotation, out Quaternion carrierRotation)
        {
            position = root.position; rotation = root.rotation; carrierRotation = carrier.transform.rotation;
            Vector3 offset = Vector3.Scale(root.InverseTransformPoint(handle.position), root.lossyScale);
            Vector3 facing = alignToCarrier || !carrier.IsLocal ? carrier.transform.forward
                : Vector3.ProjectOnPlane(root.position - handle.position, Vector3.up);
            var view = CameraViewDirector.Current;
            if (carrier.IsLocal && !HumanCannonBotWorker.IsWorkerProcess && !InputGate.IsBlocked
                && view != null && view.LocalPlayer == carrier && view.IsFreeThirdPerson) facing = view.ViewForward;
            if (!TrampolineCarryPose.TryCalculate(carrier.transform.position + Vector3.up * Mathf.Max(0, handleHeight),
                facing, offset, out position, out rotation)) return false;
            carrierRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(facing, Vector3.up));
            return true;
        }
    }
}
