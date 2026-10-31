using CurtainCall.Tightrope;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Player
{
    public sealed partial class HumanoidPlayerPresentation
    {
        TightropeHangBook.Hang rescueHang;
        bool rescueReceiver, wasRescuer;
        string rescueMotion;
        float rescueExit;
        Vector3 rescueLastOffset;
        public string RescueMotion => rescueMotion;

        void ResetRescueMotion()
        {
            rescueHang=null;rescueMotion=null;wasRescuer=false;rescueExit=0;rescueLastOffset=Vector3.zero;
        }

        bool PresentRopeRescue()
        {
            rescueHang=null;
            var hangs=TightropeHangSystem.Current;
            if(network!=null && network.IsSpawned && network.State==PlayerState.Normal && hangs!=null)
                foreach(var h in hangs.Hangs)
                    if(h.IsPulling && (h.Id==network.OwnerClientId || h.Rescuer==network.OwnerClientId))
                    { rescueHang=h; rescueReceiver=h.Id==network.OwnerClientId; break; }
            if(rescueHang==null)
            {
                if(wasRescuer)rescueExit=.30f;
                wasRescuer=false;rescueMotion=null;
                return false;
            }
            ResetCircusMotion();airborne=false;landUntil=0;
            IsHanging=rescueReceiver;rescueExit=0;wasRescuer=!rescueReceiver;
            string next=CircusVariant(rescueReceiver?"RopeLift":"ItemPull");
            float progress=hangs.PullProgress(rescueHang);
            model.Play(next,(rescueReceiver?2f:1.6f)/Mathf.Max(.1f,GameSettings.Tightrope.Hang.PullSeconds));
            if(rescueMotion!=next)
            {
                model.Animator.Play(next,0,Mathf.Min(.999f,progress));
                rescueMotion=next;
            }
            // The same replicated progress drives late joiners and both roles. Avoid drift on pauses/lag.
            var state=model.Animator.GetCurrentAnimatorStateInfo(0);
            if(state.IsName(next)&&Mathf.Abs(state.normalizedTime-progress)>.12f)
                model.Animator.Play(next,0,Mathf.Min(.999f,progress));
            return true;
        }

        bool ApplyRescueAlignment()
        {
            if(model==null || !model.Animator.enabled || !model.Animator.isHuman || condition.AllLimbsLost)return false;
            if(rescueHang==null)
            {
                if(rescueExit<=0 || IsHanging || (network!=null && network.State!=PlayerState.Normal))return false;
                var root=model.transform;var before=root.localPosition;
                root.localPosition+=rescueLastOffset*Mathf.SmoothStep(0,1,rescueExit/.30f);
                alignedModel=root;alignmentOffset=root.localPosition-before;
                rescueExit=Mathf.Max(0,rescueExit-Time.deltaTime);return true;
            }
            var hangs=TightropeHangSystem.Current;if(hangs==null)return false;
            Vector3 grip=hangs.GripPoint(rescueHang);
            NetworkPlayer helper=null;
            foreach(var p in NetworkPlayer.All)if(p.OwnerClientId==rescueHang.Rescuer){helper=p;break;}
            Vector3 direction=helper!=null?Vector3.ProjectOnPlane(grip-helper.transform.position,Vector3.up):transform.forward;
            if(direction.sqrMagnitude<.001f)direction=transform.forward;direction.Normalize();
            float progress=hangs.PullProgress(rescueHang),u=Mathf.SmoothStep(0,1,progress);
            Vector3 contact=grip+Vector3.up*(.12f+.65f*u);
            var a=model.Animator;var rootModel=model.transform;var beforePosition=rootModel.localPosition;
            alignmentRotation=rootModel.localRotation;rotationAligned=true;
            rootModel.rotation=Quaternion.LookRotation(rescueReceiver?-direction:direction,Vector3.up);
            bool left=(condition.LostParts&BodyPart.LeftArm)==0,right=(condition.LostParts&BodyPart.RightArm)==0;
            if(!rescueReceiver)left=false;
            int count=(left?1:0)+(right?1:0);if(count==0)return false;
            if(rescueReceiver)
            {
                Vector3 center=((left?Palm(a,true):Vector3.zero)+(right?Palm(a,false):Vector3.zero))/count;
                rootModel.position+=contact-center+Vector3.up*.06f;
            }
            else
            {
                Vector3 wanted=grip-direction*.30f;
                Vector3 horizontal=Vector3.ProjectOnPlane(wanted-rootModel.position,Vector3.up);
                rootModel.position+=horizontal*Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.15f));
            }
            Vector3 axis=rootModel.right;float span=count==2?.15f:0;
            if(left)AlignArm(a,true,contact-axis*span);
            if(right)AlignArm(a,false,contact+axis*span);
            alignedModel=rootModel;alignmentOffset=rootModel.localPosition-beforePosition;
            if(!rescueReceiver)rescueLastOffset=alignmentOffset;
            return true;
        }
    }
}
