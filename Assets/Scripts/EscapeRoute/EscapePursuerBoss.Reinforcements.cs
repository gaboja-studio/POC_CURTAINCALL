using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapePursuerBoss
    {
        const byte MaxReinforcements = 4;
        readonly List<EscapePursuerBoss> reinforcements = new();
        EscapePursuerBoss leader;
        Transform reinforcementRoot;
        const double ReinforcementSpacing = 2.2;
        readonly List<Renderer> hiddenReinforcementRenderers = new();
        readonly List<Collider> hiddenReinforcementHitboxes = new();
        bool reinforcementVisible = true;
        internal int EncounterIndex { get; private set; }
        internal bool IsReinforcement => EncounterIndex != 0;
        EscapePursuerBoss EncounterLeader => leader ? leader : this;
        public int EncounterSize => CombatStarted ? 1 + state.Reinforcements : 0;
        public int RemainingEnemies
        {
            get
            {
                if (!CombatStarted) return 0;
                int remaining = Defeated ? 0 : 1;
                foreach (var boss in reinforcements)
                    if (boss && boss.Defeated) remaining--;
                return remaining + state.Reinforcements;
            }
        }
        public bool EncounterDefeated => CombatStarted && RemainingEnemies == 0;

        void BeginReinforcements(double now)
        {
            int players = 0;
            var transfer = EscapeSceneTransfer.Current;
            foreach (var player in NetworkPlayer.All)
                if (player && player.IsSpawned && player.Slot != NetworkPlayer.NoSlot
                    && (!transfer || !transfer.HasDeparture || transfer.CanParticipate(player.OwnerClientId))) players++;
            state.Reinforcements = (byte)Mathf.Clamp(players, 1, MaxReinforcements);
            state.EncounterStarted = now;
            state.Revision++;
        }

        int ReinforcementsDue(double now) => Mathf.Clamp(
            (int)((now - state.EncounterStarted) / ReinforcementSpacing), 0, state.Reinforcements);

        void EnsureReinforcements(int count)
        {
            if (count <= reinforcements.Count || !landing || !arena) return;
            if (!reinforcementRoot)
            {
                var root = new GameObject("Arena pursuer reinforcements");
                root.SetActive(false);
                root.transform.SetParent(transform.parent, false);
                reinforcementRoot = root.transform;
            }
            while (reinforcements.Count < count)
            {
                int index = reinforcements.Count + 1;
                var clone = Instantiate(gameObject, reinforcementRoot);
                clone.SetActive(false);
                clone.name = "Arena pursuer " + (index + 1);
                var boss = clone.GetComponent<EscapePursuerBoss>();
                boss.EncounterIndex = index;
                boss.leader = this;
                boss.ignitionLever = null;
                boss.fire = null;
                if (boss.landingRing && !boss.landingRing.transform.IsChildOf(clone.transform)) boss.landingRing = null;
                ReinforcementLeap(index, out var launch, out var destination);
                clone.transform.SetPositionAndRotation(launch, Quaternion.LookRotation(destination - launch));
                if (Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsServer)
                {
                    boss.state.Round = state.Round;
                    boss.Enter(Phase.LeapWarning, state.EncounterStarted + index * ReinforcementSpacing, destination);
                }
                else boss.SetReinforcementVisible(false);
                reinforcements.Add(boss);
            }
            reinforcementRoot.gameObject.SetActive(true);
            foreach (var boss in reinforcements)
                if (boss && !boss.gameObject.activeSelf) boss.gameObject.SetActive(true);
        }

        void SetReinforcementVisible(bool visible)
        {
            if (visible == reinforcementVisible) return;
            reinforcementVisible = visible;
            if (!visible)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.forceRenderingOff) continue;
                    renderer.forceRenderingOff = true;
                    hiddenReinforcementRenderers.Add(renderer);
                }
                if (TryGetComponent(out CapsuleCollider capsule)) capsule.enabled = false;
                if (TryGetComponent(out UnityEngine.AI.NavMeshAgent agent)) agent.enabled = false;
                foreach (var part in parts)
                    if (part?.hitbox && part.hitbox.enabled)
                    {
                        part.hitbox.enabled = false;
                        hiddenReinforcementHitboxes.Add(part.hitbox);
                    }
                return;
            }
            foreach (var renderer in hiddenReinforcementRenderers)
                if (renderer) renderer.forceRenderingOff = false;
            hiddenReinforcementRenderers.Clear();
            foreach (var hitbox in hiddenReinforcementHitboxes)
                if (hitbox) hitbox.enabled = true;
            hiddenReinforcementHitboxes.Clear();
        }

        void ReinforcementLeap(int index, out Vector3 from, out Vector3 to)
        {
            // Four side entries pair distinct takeoffs with distinct landing pads.
            bool far = index > 2;
            float side = (index & 1) == 0 ? 1f : -1f;
            from = arena.transform.TransformPoint(new Vector3(far ? 11.5f : -12.5f, 0f, side * 11f));
            to = arena.transform.TransformPoint(new Vector3(far ? 6.5f : -7.5f, 0f, side * 8f));
        }

        bool CrowdsEncounter(Vector3 next)
        {
            var main = EncounterLeader;
            if (Crowds(main)) return true;
            foreach (var other in main.reinforcements)
                if (Crowds(other)) return true;
            for (int index = main.reinforcements.Count + 1; index <= main.state.Reinforcements; index++)
            {
                main.ReinforcementLeap(index, out var takeoff, out var landing);
                if (NearerWithoutRoom(takeoff) || NearerWithoutRoom(landing)) return true;
            }
            return false;

            bool Crowds(EscapePursuerBoss other)
            {
                if (!other || other == this || !other.CombatStarted || other.Defeated) return false;
                if (NearerWithoutRoom(other.transform.position)) return true;
                return (other.State == Phase.LeapWarning || other.State == Phase.Leaping)
                    && NearerWithoutRoom(other.state.Goal);
            }
            bool NearerWithoutRoom(Vector3 occupied)
            {
                float nextDistance = (next - occupied).sqrMagnitude;
                return nextDistance < 3.2f * 3.2f && nextDistance < (transform.position - occupied).sqrMagnitude;
            }
        }

        // Keep the step's length but drop its component toward the nearest teammate, so a blocked
        // pursuer walks around the one in front instead of standing still. Head-on steps turn to a
        // fixed side; each pursuer's side is mirrored, so the pair circles rather than re-colliding.
        Vector3 SlideAroundEncounter(Vector3 next)
        {
            Vector3 from = transform.position;
            Vector3 step = Vector3.ProjectOnPlane(next - from, Vector3.up);
            if (step.sqrMagnitude < .000001f || !arena) return from;
            var main = EncounterLeader;
            EscapePursuerBoss closest = null;
            float best = float.PositiveInfinity;
            Consider(main);
            foreach (var other in main.reinforcements) Consider(other);
            if (!closest) return from;
            Vector3 away = Vector3.ProjectOnPlane(from - closest.transform.position, Vector3.up);
            away = away.sqrMagnitude > .0001f ? away.normalized : transform.right;
            float approach = Vector3.Dot(step, away);
            if (approach >= 0) return from;
            Vector3 tangent = step - away * approach;
            if (tangent.sqrMagnitude < .0001f * step.sqrMagnitude) tangent = Vector3.Cross(Vector3.up, away);
            Vector3 slide = from + tangent.normalized * step.magnitude + Vector3.up * (next.y - from.y);
            Vector3 bounded = EscapePursuerNavigation.Clamp(arena, slide);
            if (Vector3.ProjectOnPlane(bounded - slide, Vector3.up).sqrMagnitude > .0001f) return from;
            return EscapePursuerNavigation.Supported(slide) && EscapePursuerNavigation.Clear(transform, from, slide) ? slide : from;

            void Consider(EscapePursuerBoss other)
            {
                if (!other || other == this || !other.CombatStarted || other.Defeated) return;
                float distance = (other.transform.position - from).sqrMagnitude;
                if (distance < best) { best = distance; closest = other; }
            }
        }

        void ClearReinforcements()
        {
            foreach (var boss in reinforcements)
                if (boss) boss.ClearSeveredVisuals();
            reinforcements.Clear();
            if (!reinforcementRoot) return;
            reinforcementRoot.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(reinforcementRoot.gameObject);
            else DestroyImmediate(reinforcementRoot.gameObject);
            reinforcementRoot = null;
        }

        internal bool BelongsTo(EscapePursuerBoss encounter) => EncounterLeader == encounter;
    }
}
