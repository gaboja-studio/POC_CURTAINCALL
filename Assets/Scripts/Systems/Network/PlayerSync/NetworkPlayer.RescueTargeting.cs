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
                // 공연 기물을 쓸 수 없는 곳(연습실 밖 대기실)에서는 대상을 찾지도, 표식을 띄우지도 않는다(2026-11-18 사용자).
                if(!IsOwner || !IsSpawned || IsBeingRescued || State!=PlayerState.Normal || InputGate.IsBlocked || equipmentPending!=0
                    || EquipmentPlaceBlocked(ItemUses.RescueGloves)
                    || PropCarry.Instance?.EquipmentReady(OwnerClientId,ItemUses.RescueGloves)!=true) return null;
                return FindEquipmentRescue(out _);
            }
        }
        public bool HasOutgoingRescue => All.Any(p=>p && p.NetworkManager==NetworkManager && p.rescueCaster.Value==NetworkObjectId);
        NetworkPlayer FindEquipmentRescue(out Vector3 landing) => FindRescueCandidate(out landing,ulong.MaxValue,true);

        /// <summary>
        /// 구조 대상 선택(2026-11-18 사용자): 사용 범위(수평·높이) 안에서 화면에 보이고 가리지 않은 동료 중,
        /// 화면 정중앙 조준점(InteractionCrosshair)과 화면 위치가 가장 가까운 한 명. 이동·시점 회전에 따라 매 프레임 바뀔 수 있다.
        /// 서버 재검사(checkView=false)는 클라이언트 화면을 모르므로 요청된 대상의 범위·가림·착지 공간만 다시 본다.
        /// </summary>
        NetworkPlayer FindRescueCandidate(out Vector3 landing,ulong requested,bool checkView)
        {
            landing=default;
            if(IsBeingRescued || HasOutgoingRescue || IsAirborne || HoldingEquipmentSurface || HumanCannonSystem.Current?.IsSeated(OwnerClientId)==true
                || !WalkableSupport.IsSafeLanding(mover,transform.position)) return null;
            var settings=GameSettings.PerformanceEquipment;
            Vector3 forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            var view=checkView ? Camera.main : null;
            Vector2 aim=view ? new Vector2(view.pixelWidth*.5f,view.pixelHeight*.5f) : default;
            NetworkPlayer closest=null; float best=float.PositiveInfinity;
            foreach(var other in All)
            {
                if(!other || other==this || other.NetworkManager!=NetworkManager || !other.IsSpawned
                    || requested!=ulong.MaxValue && other.NetworkObjectId!=requested || !other.CanBeRescuePulled) continue;
                // 범위는 수평 거리·높이 차이를 따로 본다(높이 있는 외줄·비행 동료도 닿게).
                Vector3 delta=other.transform.position-transform.position;
                Vector3 flat=Vector3.ProjectOnPlane(delta,Vector3.up);
                if(flat.magnitude>settings.rescueRange || Mathf.Abs(delta.y)>settings.rescueHeightRange) continue;
                var support=other.RescueSupport();
                Vector3 from=transform.position+Vector3.up*.8f, to=other.transform.position+Vector3.up*.8f;
                // 화면이 있으면 조준점과의 화면 거리, 없으면(서버 재검사·카메라 없음) 실제 거리로 고른다.
                float score=delta.magnitude;
                if(view)
                {
                    var screen=view.WorldToScreenPoint(to);
                    if(screen.z<=0 || screen.x<0 || screen.x>view.pixelWidth || screen.y<0 || screen.y>view.pixelHeight) continue;
                    score=Vector2.Distance(new Vector2(screen.x,screen.y),aim);
                }
                if(score>=best) continue;
                if(view && RescueBlocked(view.transform.position,to,other,support)) continue;
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
                    closest=other; landing=feet; best=score; break;
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
