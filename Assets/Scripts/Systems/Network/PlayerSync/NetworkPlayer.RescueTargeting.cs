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
            NetworkPlayer closest=null; float best=float.PositiveInfinity;
            foreach(var other in All)
            {
                if(!other || other==this || other.NetworkManager!=NetworkManager || !other.IsSpawned
                    || requested!=ulong.MaxValue && other.NetworkObjectId!=requested || !other.CanBeRescuePulled) continue;
                // 범위는 수평 거리·높이 차이를 따로 본다. 방향도 수평으로만 재서, 높이 있는 동료(외줄·비행)가 각도 밖으로 빠지지 않게 한다.
                Vector3 delta=other.transform.position-transform.position;
                Vector3 flat=Vector3.ProjectOnPlane(delta,Vector3.up);
                if(flat.magnitude>settings.rescueRange || Mathf.Abs(delta.y)>settings.rescueHeightRange) continue;
                if(flat.sqrMagnitude>.04f && Vector3.Angle(forward,flat)>settings.rescueHalfAngle) continue;
                if(delta.magnitude>=best) continue;
                var support=other.RescueSupport();
                Vector3 from=transform.position+Vector3.up*.8f, to=other.transform.position+Vector3.up*.8f;
                if(checkView && Camera.main)
                {
                    var v=Camera.main.WorldToViewportPoint(to);
                    if(v.z<=0 || v.x<0 || v.x>1 || v.y<0 || v.y>1) continue;
                    if(RescueBlocked(Camera.main.transform.position,to,other,support)) continue;
                }
                if(RescueBlocked(from,to,other,support)) continue;
                int mask=RescueMask(other);
                foreach(var offset in new[]{forward*1.1f,transform.right*1.1f,-transform.right*1.1f})
                {
                    Vector3 top=transform.position+offset+Vector3.up*.8f;
                    if(!Physics.Raycast(top,Vector3.down,out var ground,1.5f,mask,QueryTriggerInteraction.Ignore) || ground.normal.y<.6f
                        || ground.collider.gameObject.layer==LayerMask.NameToLayer(GamePhysicsLayers.TrickHazard)) continue;
                    float radius=other.mover.BodyRadius;
                    Vector3 feet=ground.point+Vector3.up*.035f;
                    if(Physics.OverlapCapsule(feet+Vector3.up*(radius+.05f),feet+Vector3.up*(other.mover.BodyHeight-radius),radius*.9f,
                        mask,QueryTriggerInteraction.Ignore).Any(c=>c!=ground.collider && RescueObstacle(c,other,support))) continue;
                    if(!RescuePathClear(other,feet,support)) continue;
                    closest=other; landing=feet; best=delta.magnitude; break;
                }
            }
            return closest;
        }

        /// <summary>
        /// 구조 장갑으로 당길 수 있는 상태(2026-11-18 사용자). 범위 안이면 외줄 위·점프·앉기·대포 비행·트램펄린 반동·달리기·추락 중 모두 된다.
        /// 제외: 죽음(Normal이 아님)·추격자에게 잡힘·잠·상호작용 중(대포 탑승·그네 잡기·트램펄린 들기·부수기 등 동작 중)·이미 구조되는 중.
        /// </summary>
        public bool CanBeRescuePulled
        {
            get
            {
                if(!IsSpawned || State!=PlayerState.Normal || IsBeingRescued || RestingBed>=0) return false;
                if(CurtainCall.WaitingRoom.Pursuit.PursuitDirector.IsCaptured(this)) return false;
                if(HumanCannonSystem.Current?.IsSeated(OwnerClientId)==true) return false;
                if(HumanCannonPoleSystem.Current!=null && HumanCannonPoleSystem.Current.HeldBy(OwnerClientId)>=0) return false;
                if(HumanCannonCarry.Held(OwnerClientId)!=null) return false;
                var presentation=GetComponent<HumanoidPlayerPresentation>();
                return presentation==null || !presentation.InteractionActionLocked;
            }
        }

        // 발밑에 딛고 있는 것(외줄의 줄 등). 당겨 내려올 때 막는 벽으로 보지 않는다.
        Collider RescueSupport() => Physics.Raycast(transform.position+Vector3.up*.3f,Vector3.down,out var hit,1.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform) ? hit.collider : null;

        // 대상 캐릭터가 실제로 부딪히는 레이어만 장애물로 본다(끌려오는 움직임은 CharacterController로 옮긴다).
        static int RescueMask(NetworkPlayer target)
        {
            int layer=target.mover.Body!=null ? target.mover.Body.gameObject.layer : target.gameObject.layer, mask=0;
            for(int i=0;i<32;i++) if(!Physics.GetIgnoreLayerCollision(layer,i)) mask|=1<<i;
            return mask & Physics.DefaultRaycastLayers;
        }

        // 나·대상·다른 플레이어의 몸, 대상이 딛고 있는 것은 막는 것으로 보지 않는다.
        bool RescueObstacle(Collider c,NetworkPlayer target,Collider support) => c!=support && !c.transform.IsChildOf(transform)
            && !c.transform.IsChildOf(target.transform) && c.GetComponentInParent<NetworkPlayer>()==null;

        bool RescueBlocked(Vector3 from,Vector3 to,NetworkPlayer target,Collider support) => Physics.RaycastAll(from,(to-from).normalized,Vector3.Distance(from,to),
            RescueMask(target),QueryTriggerInteraction.Ignore).Any(h=>RescueObstacle(h.collider,target,support));

        bool RescuePathClear(NetworkPlayer target,Vector3 feet,Collider support)
        {
            float radius=target.mover.BodyRadius*.9f;
            var from=target.transform.position; var delta=feet-from;
            if(delta.sqrMagnitude<.0001f) return true;
            // 출발 자리에서 이미 겹친 것(거리 0)은 지금 서 있는 자리라 막는 것으로 보지 않는다.
            return !Physics.CapsuleCastAll(from+Vector3.up*(radius+.06f),from+Vector3.up*(target.mover.BodyHeight-radius),radius,
                delta.normalized,delta.magnitude,RescueMask(target),QueryTriggerInteraction.Ignore)
                .Any(h=>h.distance>0f && RescueObstacle(h.collider,target,support));
        }
    }
}
