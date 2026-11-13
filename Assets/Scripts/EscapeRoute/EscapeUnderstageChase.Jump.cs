using Unity.AI.Navigation;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeUnderstageChase
    {
        [SerializeField] NavMeshLink cartLink;
        [SerializeField] AnimationClip jumpClip, landingClip;
        bool jumping;
        bool serviceJump;
        float jumpTime;
        Vector3 takeoff, touchdown;
        Quaternion jumpFacing;
        uint landingNumber;
        int routeAreas;

        internal float ContactTime => jumpClip.length;
        internal float JumpDuration => ContactTime + landingClip.length;
        internal NavMeshLink CartLink => cartLink;
        internal Pose ResumePose => new(jumping ? (jumpTime < ContactTime * .725f ? takeoff : touchdown) : transform.position, transform.rotation);

        void SetRouteAreas(Vector3 goal, bool sheltered)
        {
            if (!cartLink) return;
            bool cartSide = Mathf.Abs(cartLink.transform.InverseTransformPoint(goal).x) <= 2f;
            int mask = !sheltered && cartSide ? routeAreas : routeAreas & ~(1 << cartLink.area);
            if (agent.areaMask != mask) agent.areaMask = mask;
        }

        void BeginCartJump()
        {
            serviceJump = false;
            var link = agent.currentOffMeshLinkData;
            takeoff = link.startPos + Vector3.up * agent.baseOffset;
            touchdown = link.endPos + Vector3.up * agent.baseOffset;
            if ((transform.position - touchdown).sqrMagnitude < (transform.position - takeoff).sqrMagnitude)
                (takeoff, touchdown) = (touchdown, takeoff);
            var forward = Vector3.ProjectOnPlane(touchdown - takeoff, Vector3.up);
            jumpFacing = Quaternion.LookRotation(forward, Vector3.up);
            agent.updatePosition = agent.updateRotation = false;
            transform.position = takeoff;
            RestPose();
            SetBodyHeight(0f);
            body.enabled = true;
            jumpTime = 0f;
            jumping = true;
        }

        void BeginServiceJump(Vector3 start, Vector3 end)
        {
            serviceJump = true;
            takeoff = start; touchdown = end;
            jumpFacing = Quaternion.LookRotation(Vector3.ProjectOnPlane(end-start, Vector3.up));
            RestPose(); SetBodyHeight(0); body.enabled = true;
            jumpTime = 0; jumping = true;
        }

        void AdvanceCartJump(float dt)
        {
            float before = jumpTime;
            jumpTime = Mathf.Min(jumpTime + dt, JumpDuration);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, jumpFacing, 270f * dt);
            SampleJump(jumpTime, takeoff, touchdown);
            if (before < ContactTime && jumpTime >= ContactTime)
            {
                landingNumber++;
                PlayStep(.8f);
            }
            if (jumpTime < JumpDuration) return;
            if (serviceJump) fullRoute.CompleteJump();
            else agent.CompleteOffMeshLink();
            agent.updatePosition = agent.updateRotation = true;
            jumping = false;
            previousPosition = transform.position;
            nextRepath = 0f;
        }

        internal void SampleJump(float time, Vector3 start, Vector3 end)
        {
            float flight = Mathf.InverseLerp(ContactTime * .45f, ContactTime, time);
            transform.position = Vector3.Lerp(start, end, flight) + Vector3.up * (4.4f * flight * (1f - flight));
            SampleJumpPose(time);
        }

        internal void SampleJumpPose(float time)
        {
            if (time < ContactTime) SampleAction(jumpClip, time);
            else SampleAction(landingClip, Mathf.Min(time - ContactTime, landingClip.length));
        }
    }
}
