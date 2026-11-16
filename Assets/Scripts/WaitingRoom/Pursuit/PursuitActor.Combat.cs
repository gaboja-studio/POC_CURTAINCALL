using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitActor
    {
        public int Health { get; private set; }
        public int HitRevision { get; private set; }
        public int SpawnRevision { get; private set; }
        public bool IsDead => Health <= 0;
        public bool IsStaggered => Time.time < hitUntil;
        public bool IsRecoveringFromHit => combatRecovering || IsStaggered;
        public float HitRemaining => Mathf.Max(0, hitUntil - Time.time);
        public float RespawnRemaining => IsDead ? Mathf.Max(0, respawnAt - Time.time) : 0;
        public int MaxHealth => Mathf.Max(1, Placement != null && Placement.Size == PursuitPlacement.BodySize.Medium
            ? GameSettings.WaitingRoom.Pursuit.MediumHitsToDie : GameSettings.WaitingRoom.Pursuit.SmallHitsToDie);
        float hitUntil, respawnAt, fallSpeed;
        Vector3 recoilVelocity;
        bool combatFalling, combatRecovering, hitPoseApplied;
        Quaternion hitChestRest;
        readonly RaycastHit[] combatHits = new RaycastHit[32];

        void InitializeCombat() { Health = MaxHealth; }
        void ResetCombat()
        {
            RestoreHitPose(); Health = MaxHealth; hitUntil = respawnAt = fallSpeed = 0;
            recoilVelocity = Vector3.zero; combatFalling = combatRecovering = false; IsCrouched = false;
        }

        /// <summary>Called only by the authoritative saw sweep. One consumed projectile means one hit.</summary>
        public bool ReceiveSawHit(Vector3 direction)
        {
            if (remote || IsDead || director == null || !director.HasCombatAuthority) return false;
            var rules = GameSettings.WaitingRoom.Pursuit;
            director.ReleaseCombatVictim(this);
            Health = rules.SawCanKill ? Health - 1 : Mathf.Max(1, Health - 1); HitRevision++;
            hitUntil = Time.time + Mathf.Max(.1f, rules.HitReactionSeconds);
            // Non-knockback impacts keep traversal and navigation intact. The additive
            // hit pose still works while walking, crouching or climbing.
            if (!rules.SawKnockback && !IsDead) { director.ActorStateChanged(); return true; }
            combatRecovering = true;
            recoilVelocity = rules.SawKnockback ? Vector3.ProjectOnPlane(direction, Vector3.up).normalized
                * (2 * rules.HitKnockback / Mathf.Max(.1f, rules.HitReactionSeconds)) : Vector3.zero;
            combatFalling = climbing || perched;
            if (agent != null) { if (agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink) agent.ResetPath(); agent.enabled = false; }
            // Do not FinishClimb: it teleports to the landing. A hit interrupts at the current wall position.
            ResetClimb(); target = null; ClearNavigation();
            if (IsDead) {
                respawnAt = Time.time + Mathf.Max(0, rules.RespawnSeconds);
                SetMotion(PursuitMotion.Death, true);
            }
            director.ActorStateChanged();
            return true;
        }

        bool TickCombat()
        {
            if (!IsDead && !combatRecovering) return false;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (IsStaggered) {
                MoveCombat(recoilVelocity * dt);
                combatFalling = true; // Recheck support after every push, including walking off an edge.
                recoilVelocity = Vector3.MoveTowards(recoilVelocity, Vector3.zero,
                    2 * GameSettings.WaitingRoom.Pursuit.HitKnockback / Mathf.Pow(Mathf.Max(.1f, GameSettings.WaitingRoom.Pursuit.HitReactionSeconds), 2) * dt);
            }
            if (combatFalling) {
                fallSpeed += 18 * dt;
                var feet = transform.position;
                int count = Physics.RaycastNonAlloc(feet + Vector3.up * .15f, Vector3.down, combatHits,
                    fallSpeed * dt + .2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity; Vector3 ground = feet;
                for (int i = 0; i < count; i++) if (!PursuitDirectClimb.Ignored(combatHits[i].collider, transform)
                    && combatHits[i].normal.y > .6f && combatHits[i].distance < nearest) {
                    nearest = combatHits[i].distance; ground = combatHits[i].point;
                }
                if (float.IsFinite(nearest)) { transform.position = ground; combatFalling = false; fallSpeed = 0; }
                else MoveCombat(Vector3.down * fallSpeed * dt);
            }
            if (IsDead) {
                if (Time.time >= respawnAt) { SpawnRevision++; ResetActor(); director.ActorStateChanged(); }
                return true;
            }
            if (!IsStaggered && !combatFalling) {
                combatRecovering = false;
                climbTo = transform.position; Land();
                SetMotion(IsCrouched ? PursuitMotion.Crouch : PursuitMotion.Awaken, true);
            }
            return true;
        }

        void MoveCombat(Vector3 delta)
        {
            float length = delta.magnitude;
            if (length < .00001f) return;
            var feet = transform.position;
            float height = IsCrouched ? CrouchBodyHeight : 1.65f;
            int count = Physics.CapsuleCastNonAlloc(feet + Vector3.up * .3f, feet + Vector3.up * (height - .24f),
                .23f, delta / length, combatHits, length + .02f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (!PursuitDirectClimb.Ignored(combatHits[i].collider, transform))
                length = Mathf.Min(length, Mathf.Max(0, combatHits[i].distance - .02f));
            transform.position += delta.normalized * length;
        }

        // Additive recoil preserves the current walk/run/crouch/climb pose instead of snapping upright.
        void RestoreHitPose()
        {
            if (hitPoseApplied && Chest != null) Chest.localRotation = hitChestRest;
            hitPoseApplied = false;
        }
        void ApplyHitPose()
        {
            if (!IsStaggered || Chest == null) return;
            hitChestRest = Chest.localRotation; hitPoseApplied = true;
            float t = 1 - HitRemaining / Mathf.Max(.1f, GameSettings.WaitingRoom.Pursuit.HitReactionSeconds);
            float weight = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            Chest.rotation = Quaternion.AngleAxis(-24 * weight, transform.right) * Chest.rotation;
        }

        public void ReceiveCombat(int health, int revision, int spawn, float hit, float respawn, Vector3 position, Quaternion rotation)
        {
            Health = health; HitRevision = revision;
            hitUntil = Time.time + Mathf.Max(0, hit); respawnAt = Time.time + Mathf.Max(0, respawn);
            if (spawn != SpawnRevision) {
                SpawnRevision = spawn; poseBuffer.Clear(); transform.SetPositionAndRotation(position, rotation);
                remotePosition = position; remoteRotation = rotation;
            }
        }
    }
}
