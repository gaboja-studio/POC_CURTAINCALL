using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonRouteSystem
    {
        OwnerContactChannel ownerFireContacts;
        void TickOwnerFireContacts()
        {
            ownerFireContacts ??= new OwnerContactChannel("CurtainCall.HumanCannon.Route.FireContact.v2",
                () => fires.Length, index => fires[index] != null ? fires[index].ArmedAt : -1,
                (index, at, feet, height, radius) => fires[index] != null && fires[index].KillOnContact
                    && fires[index].HistoricalThreatens(feet, radius, at),
                (player, _) => player.ServerKill());
            ownerFireContacts.Tick(session, Clock, !resetBarrier.Waiting
                && (HumanCannonDestination.Current == null || !HumanCannonDestination.Current.IsResolved));
        }
    }
}
