using CurtainCall.Settings;
using UnityEngine;
namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPilot
    {
        public bool BalloonPantsActive => player != null && player.BalloonEquipmentActive;
        Vector3 ApplyBalloonPants(Vector3 displacement)
        {
            if(!BalloonPantsActive || velocity.y>=0) return displacement;
            velocity.y=Mathf.Max(velocity.y,-GameSettings.PerformanceEquipment.balloonFallSpeed);
            displacement.y=Mathf.Max(displacement.y,velocity.y*Time.deltaTime);
            return displacement;
        }
        public bool TryEquipmentJump(float height)
        {
            if(!IsFlying || IsHoldingPole || awaitingBounce || awaitingLanding || height<=0) return false;
            velocity.y=Mathf.Sqrt(2f*Mathf.Max(.01f,GameSettings.Base.Character.Gravity)*height);
            return true;
        }
    }
}
