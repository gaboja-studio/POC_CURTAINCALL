using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        [SerializeField] HumanCannonStation conveyorCannon;
        [SerializeField] BoxCollider conveyorWaitDeck;
        readonly Dictionary<ulong, ConveyorWait> conveyorWaits = new();
        double conveyorWaitRound = double.NaN;

        internal struct ConveyorWait
        {
            double approachUntil, seatUntil;
            bool arrived, boarded, launched;

            internal bool Sample(double now, bool onDeck, bool seated, bool flying, float approach, float firstSeat)
            {
                if (boarded && flying) launched = true;
                if (launched || flying || !onDeck && !seated) return false;
                if (!arrived)
                {
                    arrived = true;
                    approachUntil = now + approach;
                }
                if (seated && !boarded)
                {
                    boarded = true;
                    if (now < approachUntil) seatUntil = now + firstSeat;
                }
                return now < approachUntil || seated && now < seatUntil;
            }
        }

        readonly HashSet<ulong> conveyorWaiting = new();

        void SampleConveyorWait()
        {
            conveyorWaiting.Clear();
            if (InShowcase || !conveyorCannon || !conveyorWaitDeck || !HumanCannonSystem.Current) return;
            var timer = EscapeRouteTimer.Current;
            if (conveyorWaitRound != timer.StartedAt)
            {
                conveyorWaitRound = timer.StartedAt;
                conveyorWaits.Clear();
            }
            var cannon = HumanCannonSystem.Current;
            foreach (var player in NetworkPlayer.All)
            {
                if (!player || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                var id = player.OwnerClientId;
                bool seated = cannon.SeatedStation(id) == conveyorCannon;
                bool onDeck = AtConveyorDeck(player.transform.position);
                if (!seated && !onDeck && !conveyorWaits.ContainsKey(id)) continue;
                conveyorWaits.TryGetValue(id, out var wait);
                if (wait.Sample(timer.Clock, onDeck, seated, cannon.IsFlying(id),
                    Tuning.conveyorApproachSeconds, Tuning.conveyorFirstSeatSeconds))
                    conveyorWaiting.Add(id);
                conveyorWaits[id] = wait;
            }
        }

        bool AtConveyorDeck(Vector3 point)
        {
            if (!conveyorWaitDeck) return false;
            Vector3 local = conveyorWaitDeck.transform.InverseTransformPoint(point) - conveyorWaitDeck.center;
            Vector3 half = conveyorWaitDeck.size * .5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        bool WaitingForConveyor(ulong actor) => conveyorWaiting.Contains(actor);
    }
}
