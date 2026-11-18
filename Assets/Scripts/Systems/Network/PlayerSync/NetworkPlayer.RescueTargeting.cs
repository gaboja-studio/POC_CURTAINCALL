using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    public sealed partial class NetworkPlayer
    {
        public NetworkPlayer RescuePreviewTarget
        {
            get
            {
                if(!IsOwner || !IsSpawned || IsBeingRescued || State!=PlayerState.Normal || InputGate.IsBlocked || equipmentPending!=0
                    || PropCarry.Instance?.EquipmentReady(OwnerClientId,ItemUses.RescueGloves)!=true) return null;
                return FindEquipmentRescue(out _);
            }
        }
        public bool HasOutgoingRescue => All.Any(p=>p && p.NetworkManager==NetworkManager && p.rescueCaster.Value==NetworkObjectId);
        NetworkPlayer FindEquipmentRescue(out Vector3 landing) => FindRescueCandidate(out landing,ulong.MaxValue,true);

        NetworkPlayer FindRescueCandidate(out Vector3 landing,ulong requested,bool checkView)
        {
            landing=default;
            if(IsBeingRescued || HasOutgoingRescue || IsAirborne || HoldingEquipmentSurface || HumanCannonSystem.Current?.IsSeated(OwnerClientId)==true
                || !WalkableSupport.IsSafeLanding(mover,transform.position)) return null;
            var settings=GameSettings.PerformanceEquipment;
            Vector3 forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            NetworkPlayer closest=null; float best=settings.rescueRange;
            foreach(var other in All)
            {
                if(!other || other==this || other.NetworkManager!=NetworkManager || !other.IsSpawned || other.IsBeingRescued || other.State!=PlayerState.Normal
                    || requested!=ulong.MaxValue && other.NetworkObjectId!=requested
                    || !(other.IsAirborne || other.HoldingEquipmentSurface || HumanCannonSystem.Current?.IsFlying(other.OwnerClientId)==true)) continue;
                Vector3 delta=other.transform.position-transform.position;
                if(delta.magnitude>best || Vector3.Angle(forward,delta)>settings.rescueHalfAngle) continue;
                Vector3 from=transform.position+Vector3.up*.8f, to=other.transform.position+Vector3.up*.8f;
                if(checkView && Camera.main)
                {
                    var v=Camera.main.WorldToViewportPoint(to);
                    if(v.z<=0 || v.x<0 || v.x>1 || v.y<0 || v.y>1) continue;
                    if(RescueBlocked(Camera.main.transform.position,to,other)) continue;
                }
                if(RescueBlocked(from,to,other)) continue;
                foreach(var offset in new[]{forward*1.1f,transform.right*1.1f,-transform.right*1.1f})
                {
                    Vector3 top=transform.position+offset+Vector3.up*.8f;
                    if(!Physics.Raycast(top,Vector3.down,out var ground,1.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || ground.normal.y<.6f
                        || ground.collider.gameObject.layer==LayerMask.NameToLayer(GamePhysicsLayers.TrickHazard)) continue;
                    float radius=other.mover.BodyRadius;
                    Vector3 feet=ground.point+Vector3.up*.035f;
                    if(Physics.OverlapCapsule(feet+Vector3.up*(radius+.05f),feet+Vector3.up*(other.mover.BodyHeight-radius),radius*.9f,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(c=>c!=ground.collider && !c.transform.IsChildOf(other.transform))) continue;
                    if(!RescuePathClear(other,feet)) continue;
                    closest=other; landing=feet; best=delta.magnitude; break;
                }
            }
            return closest;
        }
        bool RescueBlocked(Vector3 from,Vector3 to,NetworkPlayer target) => Physics.RaycastAll(from,(to-from).normalized,Vector3.Distance(from,to),
            Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(h=>!h.collider.transform.IsChildOf(transform) && !h.collider.transform.IsChildOf(target.transform));

        bool RescuePathClear(NetworkPlayer target,Vector3 feet)
        {
            float radius=target.mover.BodyRadius*.9f;
            var from=target.transform.position; var delta=feet-from;
            return !Physics.CapsuleCastAll(from+Vector3.up*(radius+.06f),from+Vector3.up*(target.mover.BodyHeight-radius),radius,
                delta.normalized,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                .Any(h=>!h.collider.transform.IsChildOf(transform) && !h.collider.transform.IsChildOf(target.transform));
        }
    }
}
