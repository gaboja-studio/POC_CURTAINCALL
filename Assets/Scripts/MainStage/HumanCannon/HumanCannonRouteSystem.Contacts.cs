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
                (player, index) => player.ServerKill(CurtainCall.Network.PlayerSync.NetworkPlayer.DeathOrigin.FirePillar,CurtainCall.Telemetry.Pipeline.DetailLogIds.ObjectId(fires[index],"OB_")));
            ownerFireContacts.Tick(session, Clock, !resetBarrier.Waiting
                && (HumanCannonDestination.Current == null || !HumanCannonDestination.Current.IsResolved));
        }
    }
}
