using System.Collections.Generic;
using CurtainCall.Damage;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Tightrope
{
    /// <summary>받기 준비는 남아 있는 팔을 앞으로, 성공은 위로 펼친다. 이동·충돌·절단된 팔다리는 건드리지 않는다.</summary>
    [DefaultExecutionOrder(600)]
    public sealed class TightropeCatchPoseView : MonoBehaviour
    {
        sealed class Pose
        {
            public NetworkPlayer Player;
            public GameObject Model;
            public Transform Left, Right;
            public Quaternion LeftBefore, RightBefore;
            public bool LeftApplied, RightApplied;
        }

        readonly Dictionary<NetworkPlayer, Pose> poses = new();
        readonly List<NetworkPlayer> removed = new();
        TightropeCatchSystem catches;
        public int PosedPlayerCount { get; private set; }

        void Awake()
        {
            catches = GetComponent<TightropeCatchSystem>();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) enabled = false;
        }

        // 애니메이터가 이번 프레임의 자세를 만들기 전에 지난 프레임의 임시 오프셋을 해제한다.
        void Update() => Restore();

        void LateUpdate()
        {
            PosedPlayerCount = 0;
            if (catches == null || !catches.isActiveAndEnabled) return;
            removed.Clear();
            foreach (var pair in poses)
                if (pair.Key == null || !pair.Key.IsSpawned || pair.Value.Model == null) removed.Add(pair.Key);
            foreach (var player in removed) poses.Remove(player);
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal
                    || !player.TryGetComponent(out PlayerModelSlot slot) || slot.CurrentModel == null) continue;
                bool success = catches.SuccessRemaining(player) > 0f
                    || (TightropeRelayGate.Current != null && TightropeRelayGate.Current.IsHeld(player))
                    || (TightropeTrapeze.Current != null && TightropeTrapeze.Current.IsHeld(player))
                    || (TightropeSwingPole.Current != null && TightropeSwingPole.Current.IsHeld(player));
                if (!success && !catches.IsReady(player)) continue;
                if (!poses.TryGetValue(player, out var pose) || pose.Model != slot.CurrentModel)
                {
                    pose = new Pose { Player = player, Model = slot.CurrentModel,
                        Left = ModelLimbs.Find(slot.CurrentModel, BodyPart.LeftArm),
                        Right = ModelLimbs.Find(slot.CurrentModel, BodyPart.RightArm) };
                    poses[player] = pose;
                }
                pose.LeftApplied = Apply(pose, pose.Left, BodyPart.LeftArm, success, out pose.LeftBefore);
                pose.RightApplied = Apply(pose, pose.Right, BodyPart.RightArm, success, out pose.RightBefore);
                if (pose.LeftApplied || pose.RightApplied) PosedPlayerCount++;
            }
        }

        static bool Available(Pose pose, Transform arm, BodyPart part) => pose.Player != null
            && pose.Player.State == PlayerState.Normal && (pose.Player.LostParts & part) == 0
            && arm != null && pose.Model != null && arm.IsChildOf(pose.Model.transform);

        static bool Apply(Pose pose, Transform arm, BodyPart part, bool success, out Quaternion before)
        {
            before = Quaternion.identity;
            if (!Available(pose, arm, part)) return false;
            before = arm.localRotation;
            float side = part == BodyPart.LeftArm ? -1f : 1f;
            Vector3 along = -arm.up;
            // 자식 뼈가 없는 막대형 팔은 메시의 긴 축을 팔 방향으로 사용한다.
            if (arm.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
            {
                Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, arm.lossyScale);
                Vector3 axis = Mathf.Abs(size.x) >= Mathf.Abs(size.y) && Mathf.Abs(size.x) >= Mathf.Abs(size.z)
                    ? Vector3.right : Mathf.Abs(size.y) >= Mathf.Abs(size.z) ? Vector3.up : Vector3.forward;
                along = arm.TransformDirection(axis).normalized;
                if (Vector3.Dot(along, pose.Player.transform.right * side) < 0f) along = -along;
            }
            // 정식 휴머노이드와 BlockDoll의 부위 표시 모두 자식 방향을 기준으로 삼는다.
            for (int i = 0; i < arm.childCount; i++)
            {
                Vector3 delta = arm.GetChild(i).position - arm.position;
                if (delta.sqrMagnitude > .0001f) { along = delta.normalized; break; }
            }
            Vector3 desired = pose.Player.transform.forward * (success ? .2f : .85f)
                + pose.Player.transform.right * side * .55f + Vector3.up * (success ? 1f : .15f);
            arm.rotation = Quaternion.FromToRotation(along, desired.normalized) * arm.rotation;
            return true;
        }

        void Restore()
        {
            foreach (var pose in poses.Values)
            {
                if (pose.LeftApplied && Available(pose, pose.Left, BodyPart.LeftArm)) pose.Left.localRotation = pose.LeftBefore;
                if (pose.RightApplied && Available(pose, pose.Right, BodyPart.RightArm)) pose.Right.localRotation = pose.RightBefore;
                pose.LeftApplied = pose.RightApplied = false;
            }
        }

        void OnDisable() { Restore(); poses.Clear(); PosedPlayerCount = 0; }
    }
}
