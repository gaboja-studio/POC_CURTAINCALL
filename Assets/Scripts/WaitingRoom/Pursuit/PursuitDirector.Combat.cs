using System.Linq;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitDirector
    {
        public bool HasCombatAuthority => run != null && run.HasAuthority && IsPursuitPhase(run.State.Phase);

        internal void ReleaseCombatVictim(PursuitActor actor)
        {
            if (actor.Victim == null) return;
            foreach (var pair in victims.Where(p => p.Value.Object == actor.Victim && !p.Value.Retired).ToArray()) {
                if (pair.Value.Pose != null) Destroy(pair.Value.Pose);
                victims.Remove(pair.Key);
            }
            actor.FinishCapture();
        }

        /// <summary>Sweep a horizontal saw through the animated body, after world occlusion limited its travel.</summary>
        public bool TryHitSaw(Vector3 from, Vector3 to, Vector3 direction, float radius, float halfThickness, out Vector3 contact)
        {
            contact = to;
            if (!HasCombatAuthority) return false;
            float nearest = Vector3.Distance(from, to);
            PursuitActor struck = null;
            foreach (var actor in actors) {
                if (actor == null || actor.IsDead) continue;
                if (actor.SweepSaw(from, direction, nearest, radius, halfThickness, out float distance)) {
                    nearest = distance; struck = actor;
                }
            }
            if (struck == null || !struck.ReceiveSawHit(direction)) return false;
            contact = from + direction * nearest;
            return true;
        }
    }
}
