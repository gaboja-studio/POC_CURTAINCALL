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
        Vector3 CarryFocus { get; }
        Vector2 CarryHalfSize { get; }
        float CarryMoveSpeed { get; }
        string CarryName { get; }
        bool CanGrab(NetworkPlayer player);
        void RequestGrab();
        void RequestDrop();
        void ServerReleaseForInspection(ulong actor);
    }

    public static class HumanCannonCarry
    {
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
                float distance = (device.Handle.position - player.transform.position).sqrMagnitude;
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
