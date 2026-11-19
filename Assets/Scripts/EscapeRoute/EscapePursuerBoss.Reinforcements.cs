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
            state.Revision++;
            EnsureReinforcements(state.Reinforcements);
            foreach (var boss in reinforcements)
            {
                boss.state.Round = state.Round;
                boss.Enter(Phase.Battle, now, boss.transform.position);
                boss.nextDash = now + 2 + .4 * boss.EncounterIndex;
            }
        }

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
                clone.transform.SetPositionAndRotation(SpawnReinforcement(index), transform.rotation);
                reinforcements.Add(boss);
            }
            reinforcementRoot.gameObject.SetActive(true);
            foreach (var boss in reinforcements)
                if (boss && !boss.gameObject.activeSelf) boss.gameObject.SetActive(true);
        }

        Vector3 SpawnReinforcement(int index)
        {
            float firstAngle = (index - 1) * Mathf.PI * .5f;
            for (int ring = 1; ring <= 4; ring++)
                for (int turn = 0; turn < 8; turn++)
                {
                    float angle = firstAngle + turn * Mathf.PI * .25f;
                    Vector3 offset = arena.transform.right * Mathf.Cos(angle) + arena.transform.forward * Mathf.Sin(angle);
                    Vector3 feet = ClampArena(landing.position + offset * (4f * ring));
                    if ((feet - landing.position).sqrMagnitude < 12f || !EscapePursuerNavigation.Supported(feet)) continue;
                    if (reinforcements.Exists(boss => boss && (boss.transform.position - feet).sqrMagnitude < 12f)) continue;
                    if (EscapePursuerNavigation.Clear(transform, feet, feet)) return feet;
                }
            return landing.position + arena.transform.right * (index % 2 == 0 ? -4f : 4f) * ((index + 1) / 2);
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
