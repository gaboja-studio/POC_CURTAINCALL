using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        [SerializeField] EscapePursuerMascot mascot;
        bool gaitResting = true;
        int sampledAction, sampledFrame = -1;
        float sampledTime;
        AttackCue previewAttack;
        double previewAttackTime;
        internal struct AppearancePose
        {
            public int Action;
            public float Time, Stride, Run, Duck;
            public bool Resting, Look, Stretch;
            public Vector3 Aim, Hand;
        }
        internal AppearancePose Appearance
        {
            get
            {
                double now = EscapeRouteTimer.Current ? EscapeRouteTimer.Current.Clock : 0;
                bool remote = NetworkManager.Singleton && !NetworkManager.Singleton.IsServer;
                var cue = remote && hasSnapshot ? latest.Attack : attack;
                if (InShowcase) { cue = previewAttack; now = previewAttackTime; }
                var intent = remote && hasSnapshot ? latest.Threat : threat;
                float age = Mathf.Max(0, (float)(now - cue.Started));
                var pose = new AppearancePose { Stride = stride, Run = runWeight, Duck = duckWeight, Resting = gaitResting,
                    Look = cue.Phase != AttackPhase.None, Aim = cue.Aim };
                if (remote && hasSnapshot) { pose.Stride = latest.Stride; pose.Resting = latest.Stage == Stage.Idle || latest.Run == 0 && latest.Position == previous.Position; }
                if (intent.Phase == ThreatPhase.ChargeWarning || intent.Phase == ThreatPhase.DebrisWarning)
                {
                    pose.Action = intent.Phase == ThreatPhase.ChargeWarning ? 4 : 6;
                    pose.Time = Mathf.Lerp(.05f, .4f, Mathf.Clamp01((float)(now - intent.Started) / (intent.Phase == ThreatPhase.ChargeWarning ? intent.Duration : Tuning.debrisWarning)));
                    pose.Look = true; pose.Aim = intent.To;
                }
                else if (cue.Phase == AttackPhase.Warning || cue.Phase == AttackPhase.Strike || cue.Phase == AttackPhase.Retract)
                {
                    pose.Action = 5;
                    pose.Time = cue.Phase == AttackPhase.Warning ? Mathf.Lerp(0, .3f, age / Warning(cue.Reach)) : Mathf.Lerp(.3f, .7f, age / StrikeTime);
                }
                else if (cue.Phase == AttackPhase.Throw)
                {
                    pose.Action = 6;
                    pose.Time = Mathf.Lerp(.1f, .8f, Mathf.Clamp01(age / SwingTime));
                    pose.Aim = cue.Drop;
                }
                if (graspingHand && graspingHand.gameObject.activeSelf)
                { pose.Stretch = true; pose.Hand = graspingHand.position; }
                if (jumping || remote && hasSnapshot && latest.Stage == Stage.CartJump)
                {
                    float time = jumping ? jumpTime : latest.JumpTime;
                    pose.Action = time < ContactTime ? 7 : 8;
                    pose.Time = time < ContactTime ? time / ContactTime : (time - ContactTime) / Mathf.Max(.1f, landingClip.length);
                }
                else if (sampledFrame == Time.frameCount && sampledAction > 0)
                {
                    pose.Action = sampledAction;
                    pose.Time = sampledTime;
                }
                return pose;
            }
        }
    }
}
