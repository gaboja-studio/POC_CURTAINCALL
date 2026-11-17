using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitActor
    {
        CapsuleCollider separationBody;
        static int syncedSeparationFrame = -1;
        float nextSeparationRetry;
        public int SeparationCount { get; private set; }
        void ResolveObstruction()
        {
            var rules = GameSettings.WaitingRoom.Pursuit;
            if (remote || director == null || !director.HasCombatAuthority || IsDead || !rules.ObstructionSeparation || Time.time < nextSeparationRetry) return;
            if (syncedSeparationFrame != Time.frameCount) { Physics.SyncTransforms(); syncedSeparationFrame = Time.frameCount; }
            if (separationBody == null) {
                var probe = new GameObject("Pursuit separation probe") { hideFlags = HideFlags.DontSave };
                probe.transform.SetParent(transform, false);
                separationBody = probe.AddComponent<CapsuleCollider>(); separationBody.enabled = false;
            }
            var scale = transform.lossyScale;
            separationBody.transform.localScale = new Vector3(1 / Mathf.Max(.01f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.01f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.01f, Mathf.Abs(scale.z)));
            separationBody.transform.rotation = Quaternion.identity;
            float height = IsCrouched ? CrouchBodyHeight : 1.8f * (Placement != null ? Placement.SizeScale : 1);
            float bottom = climbing ? PursuitClimbPath.BodyBottom : .08f;
            separationBody.radius = .24f; separationBody.height = Mathf.Max(.5f, height - bottom);
            separationBody.center = Vector3.up * (bottom + separationBody.height * .5f);
            nextSeparationRetry = Time.time + .04f;
            // 펜스를 넘는 중에는 넘는 난간 자체가 몸과 겹친다. 오르기처럼 움직이는 물체만 밀어낸다.
            if (!PursuitSeparation.TryResolve(separationBody, transform.position, transform, Mathf.Max(.5f, rules.SeparationDistance), climbing || vaulting, out var position)) return;
            if (agent != null) agent.enabled = false; // Avoid ResetPath completing an active link.
            bool falling = climbing;
            ResetClimb(); transform.position = position;
            if (agent != null && NavMesh.SamplePosition(position, out var hit, .35f, agent.areaMask)
                && Mathf.Abs(hit.position.y - position.y) < .2f && PursuitSeparation.Clear(separationBody, hit.position, transform)) {
                transform.position = hit.position; agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(hit.position);
            } else if (falling) { combatRecovering = combatFalling = true; fallSpeed = 0; }
            ClearNavigation(); replanRequested = true; stuckFor = 0;
            SeparationCount++;
        }
    }
}
