using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>실제 Player 상태를 외형에 전달한다. 부상/이동/공연 판정은 소유하지 않는다.</summary>
    public sealed class HumanoidPlayerPresentation : MonoBehaviour
    {
        PlayerModelSlot slot;
        PlayerCondition condition;
        PlayerMover mover;
        PlayerBalance balance;
        NetworkPlayer network;
        HumanoidModel model;
        HumanoidModel detached;
        bool airborne;
        float jumpSince;
        float landUntil;
        Color color;
        public HumanoidModel Model => model;

        void OnEnable()
        {
            slot = GetComponent<PlayerModelSlot>();
            condition = GetComponent<PlayerCondition>();
            mover = GetComponent<PlayerMover>();
            balance = GetComponent<PlayerBalance>();
            network = GetComponent<NetworkPlayer>();
            slot.ModelChanged += BindModel;
            condition.Changed += Changed;
            NetworkPlayer.Restarted += Restarted;
            BindModel();
        }

        void BindModel()
        {
            var next = slot.CurrentModel != null ? slot.CurrentModel.GetComponent<HumanoidModel>() : null;
            if (model == next) return;
            if (model != null)
            {
                if (next != null) model.ClearPieces();
                model.StopMotion();
                detached = model;
            }
            model = next;
            airborne = false;
            landUntil = 0;
            if (model == null) return;
            model.Animator.enabled = true;
            color = GameSettings.Base.Character.GetPlayerColor(network != null ? network.Slot : 0);
            model.SetColor(color);
            model.Apply(condition.LostParts, condition.LostParts, false, mover.Body);
        }

        void Changed(BodyPart previous, BodyPart current)
        {
            BindModel();
            // 사망 이벤트가 먼저 모델을 분리해도 마지막 사지는 현재 포즈에서 분리한다.
            var target = model != null ? model : detached;
            if (target != null) target.Apply(previous, current, network == null || !network.IsApplyingInitialCondition, mover.Body);
        }

        void Restarted(NetworkPlayer player)
        {
            if (player.gameObject != gameObject) return;
            if (model != null) model.ClearPieces();
            if (detached != null) detached.ClearPieces();
        }

        void Update()
        {
            BindModel();
            if (model == null) return;
            Color next = GameSettings.Base.Character.GetPlayerColor(network != null ? network.Slot : 0);
            if (color != next) { color = next; model.SetColor(color); }
            model.Animator.SetLayerWeight(1, 0f);
            if (condition.AllLimbsLost) { model.Play("Death"); return; }
            if (balance != null && balance.HasFallen) return;
            bool moving = network != null ? network.PresentationMoving : mover.IsMoving;
            bool sprinting = network != null ? network.PresentationSprinting : mover.IsSprinting;
            JumpKind jump = network != null && network.IsSpawned && !network.IsOwner ? network.CurrentJump : mover.CurrentJump;
            bool inAir = jump != JumpKind.None || mover.IsAirborne;
            if (inAir && !airborne) jumpSince = Time.time;
            if (!inAir && airborne) landUntil = Time.time + .15f;
            airborne = inAir;
            if (network != null && network.IsSpawned && HumanCannonSystem.Current != null)
            {
                if (HumanCannonSystem.Current.IsFlying(network.OwnerClientId)) { model.Play("Flying"); return; }
                if (HumanCannonSystem.Current.IsSeated(network.OwnerClientId)) { model.Play("Crouch"); return; }
            }
            if (condition.LostLegCount == 2)
            {
                model.Play("Crawl", moving && !inAir ? 1f : .0001f);
                return;
            }
            if (inAir)
            {
                model.Play(jump == JumpKind.Fall ? "Fall" : Time.time - jumpSince < .12f ? "JumpStart" : "JumpLoop");
                return;
            }
            if (Time.time < landUntil) { model.Play("JumpLand"); return; }
            if (mover.IsCrouched) { model.Play(moving ? "CrouchWalk" : "Crouch"); return; }
            bool carrying = network != null && network.IsSpawned
                && ((PropCarry.Instance != null && PropCarry.Instance.Held(network.OwnerClientId).Count > 0)
                    || (HumanCannonTrampoline.Current != null && HumanCannonTrampoline.Current.Carrier == network.OwnerClientId));
            // 상체 전용 레이어로 지지 하체와 운반 동작을 분리한다.
            model.Animator.SetLayerWeight(1, carrying ? 1f : 0f);
            model.Play(moving ? balance != null && balance.IsActive ? "Tightrope" : sprinting ? "Run" : "Walk" : "Idle");
        }

        void OnDisable()
        {
            if (slot != null) slot.ModelChanged -= BindModel;
            if (condition != null) condition.Changed -= Changed;
            NetworkPlayer.Restarted -= Restarted;
            if (model != null) model.ClearPieces();
            if (detached != null) detached.ClearPieces();
            model = null;
            detached = null;
        }
    }
}
