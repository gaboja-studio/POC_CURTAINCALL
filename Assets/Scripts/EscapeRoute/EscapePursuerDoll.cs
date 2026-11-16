using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Samples the authored non-Humanoid rig without retargeting its fingers or sockets.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed partial class EscapePursuerDoll : MonoBehaviour
    {
        // Preserve serialized Revision5 indices; Roar is a compatibility slot for ChargeWarning.
        public enum Motion { Idle, Walk, Run, GrabStretch, Throw, WallStrike, JumpLand, Duck, HandFlex, CrouchWalk, Roar,
            GrabHold, CaptureTear, CarryWalk, GrabMiss, ReachLong, ReachFullRange, ChargeWarning, Dash,
            HitLeft, HitRight, TurnLeft, TurnRight, Ignition, Death, MaimedCrawl }
        public int revision = 5;
        public EscapeUnderstageChase chase;
        public EscapePursuerBoss boss;
        public AnimationClip[] clips;
        public Transform[] bones;
        public Transform grip, release, wrist, forearm, stretchIsolation;
        public Renderer[] legacyMeshes;
        public BoxCollider[] hitboxes;
        public Transform[] starts, middles, ends;
        public float walkCycleDistance = 1.65625f, runCycleDistance = 3.34884f, crouchCycleDistance = 3.6f;
        // Source frame 1 is Unity time zero. These values are populated from the manifest.
        public float graspTime = 34f / 30f, releaseTime = 34f / 30f, impactTime = 26f / 30f;
        public float takeoffTime = 16f / 30f, landingTime = 44f / 30f;
        Vector3[] positions, scales;
        Quaternion[] rotations;

        public Transform[] sleeves, folds;
        public float forearmRestLength = .8794461f, foldPanelLength = .275f;
        public Vector3 foldPerpendicular = Vector3.forward;

        // Keep the hand, sewn shell pieces and fold bones at their authored scale.
        // Length comes from translating stored cloth, never stretching skin or fingers.
        void UnfoldTo(Vector3 target)
        {
            if (!forearm || !wrist || !grip || sleeves == null || folds == null || folds.Length < 2
                || !float.IsFinite(target.sqrMagnitude)) return;
            Vector3 pivot = forearm.position, arm = wrist.position - pivot, offset = grip.position - wrist.position;
            if (arm.sqrMagnitude < .000001f) return;
            Quaternion turn = Quaternion.identity;
            Vector3 desired = wrist.position;
            for (int i = 0; i < 12; i++)
            {
                desired = target - turn * offset;
                if ((desired - pivot).sqrMagnitude < .000001f) return;
                turn = Quaternion.FromToRotation(arm, desired - pivot);
            }
            forearm.rotation = turn * forearm.rotation;
            // One final correction is exact even for targets very close to the elbow.
            wrist.position = target - (grip.position - wrist.position);
            Vector3 axis = (wrist.position - pivot).normalized;
            float distance = Vector3.Distance(pivot, wrist.position);
            float stored = Mathf.Min(forearmRestLength, distance), extension = Mathf.Max(0, distance - forearmRestLength);
            var perpendicular = Vector3.ProjectOnPlane(forearm.TransformDirection(foldPerpendicular), axis).normalized;
            int count = folds.Length - 1;
            float half = .5f * Mathf.Sqrt(Mathf.Max(0, foldPanelLength * foldPanelLength - stored * stored / (count * count)));
            for (int i = 0; i < sleeves.Length; i++)
                sleeves[i].position = pivot + axis * (stored * (i + .5f) / sleeves.Length + extension);
            for (int i = 0; i <= count; i++)
            {
                float opened = Mathf.Max(0, extension - foldPanelLength * (count - i));
                folds[i].position = pivot + axis * (stored * i / count + opened) + perpendicular * (half * (i % 2 == 0 ? 1 : -1));
            }
        }

        void Initialize()
        {
            if (positions != null && positions.Length == bones.Length) return;
            positions = new Vector3[bones.Length]; scales = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length];
        }

        public float Normalized(Motion motion, float seconds) => seconds / clips[(int)motion].length;
        public void Sample(Motion motion, float normalized)
        {
            RestoreLookPose();
            if (stretchIsolation) { stretchIsolation.localPosition = Vector3.zero; stretchIsolation.localScale = Vector3.one; }
            clips[(int)motion].SampleAnimation(gameObject, Mathf.Clamp01(normalized) * clips[(int)motion].length);
        }

        public static Transform PrepareRig(Transform model)
        {
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
                if (bone.name == "Fold00.R") return null; // Revision7 has no inherited stretch scale.
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
                if (bone.name == "Hand.R" && bone.parent && bone.parent.name == "Forearm.R")
                {
                    var isolation = new GameObject("Hand stretch isolation").transform;
                    isolation.SetParent(bone.parent, false); bone.SetParent(isolation, false); return isolation;
                }
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
                if (bone.name == "Hand stretch isolation") return bone;
            throw new System.InvalidOperationException("Revision5 right wrist is missing.");
        }

        public void StretchTo(Vector3 target)
        {
            if (revision >= 7) { UnfoldTo(target); return; }
            Vector3 pivot = forearm.position, arm = wrist.position - pivot, offset = grip.position - wrist.position;
            if (!stretchIsolation || arm.sqrMagnitude < .0001f || (target - pivot).sqrMagnitude < .0001f) return;
            var turn = Quaternion.FromToRotation(grip.position - pivot, target - pivot);
            Vector3 desired = wrist.position;
            for (int i = 0; i < 8; i++)
            {
                desired = target - turn * offset;
                turn = Quaternion.FromToRotation(arm, desired - pivot);
            }
            float factor = Mathf.Clamp(Vector3.Distance(desired, pivot) / arm.magnitude, .05f, 45f);
            forearm.rotation = turn * forearm.rotation;
            var scale = forearm.localScale; scale.y *= factor; forearm.localScale = scale;
            // Cancel only the added scale before the wrist's rotation. The source FBX already
            // bakes Blender's inherit_scale=NONE for its authored stretch. This preserves that
            // matrix and the fingers, while the weighted forearm reaches the same wrist seam.
            stretchIsolation.localScale = new Vector3(1, 1 / factor, 1);
            stretchIsolation.localPosition = new Vector3(0, wrist.localPosition.y * (1 - 1 / factor), 0);
        }

        void Blend(Motion motion, float normalized, float weight)
        {
            if (weight <= 0) return;
            if (weight >= 1) { Sample(motion, normalized); return; }
            Initialize();
            for (int i = 0; i < bones.Length; i++)
            { positions[i] = bones[i].localPosition; rotations[i] = bones[i].localRotation; scales[i] = bones[i].localScale; }
            Sample(motion, normalized);
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = Vector3.Lerp(positions[i], bones[i].localPosition, weight);
                bones[i].localRotation = Quaternion.Slerp(rotations[i], bones[i].localRotation, weight);
                bones[i].localScale = Vector3.Lerp(scales[i], bones[i].localScale, weight);
            }
        }

        internal void SampleCurrent()
        {
            if (!chase) return;
            var pose = chase.DollPose;
            if (pose.Action >= 0)
            {
                ClearLocomotionDisplay();
                SampleAction((Motion)pose.Action, pose.Time);
                if (pose.CrouchBlend > 0) Blend(Motion.CrouchWalk, pose.Stride, pose.CrouchBlend);
                if (pose.WalkBlend > 0) Blend(Motion.Walk, pose.Stride, pose.WalkBlend);
                if (pose.IdleBlend > 0) Blend(Motion.Idle, 0, pose.IdleBlend);
                if (pose.WallContactWeight > 0 && grip)
                    StretchTo(Vector3.Lerp(grip.position, pose.Aim, pose.WallContactWeight));
            }
            else
            {
                if (revision >= 7 && pose.Resting)
                {
                    ClearLocomotionDisplay();
                    if (pose.Duck > .05f) Sample(Motion.Duck, .3f * pose.Duck);
                    else if (turning)
                    {
                        Sample(turnLeft ? Motion.TurnLeft : Motion.TurnRight, turnPhase);
                        // The navigation root already performed the authored turn in world space.
                        if (poseRoot) poseRoot.localRotation = poseRootRotation;
                    }
                    else
                    {
                        float idle = Mathf.Repeat(Time.time, 12f);
                        Sample(idle >= 10 ? Motion.HandFlex : Motion.Idle, idle >= 10 ? (idle - 10) / 2 : Mathf.Repeat(idle / 4, 1));
                    }
                    return;
                }
                if (pose.Resting)
                {
                    ClearLocomotionDisplay();
                    Sample(Motion.Idle, Mathf.Repeat(Time.time / 4, 1));
                    Blend(Motion.CrouchWalk, pose.Stride, pose.Duck);
                }
                else SampleDisplayedLocomotion(pose.Stride, pose.Duck, pose.Run, Time.time, Time.frameCount);
            }
        }

        static Motion LocomotionMotion(float run) => run >= .5f ? Motion.Run : Motion.Walk;

        internal void SampleLocomotion(float phase, float duck, float run)
        {
            // Walk and Run have different contact schedules. Blending their joint
            // rotations lifts a planted foot; use one authored gait and its stride.
            // Selection is deterministic from the replicated presentation weight.
            Sample(revision >= 7 ? LocomotionMotion(run) : Motion.Walk, phase);
            if (revision < 7) Blend(Motion.Run, phase, run * (1 - duck));
            Blend(Motion.CrouchWalk, phase, duck);
        }

        const float GaitTransitionSeconds = .16f;
        Vector3[] displayedPositions, displayedScales, transitionPositions, transitionScales;
        Quaternion[] displayedRotations, transitionRotations;
        Transform[] gaitThighs, gaitShins, gaitFeet;
        readonly Vector3[] displayedFeet = new Vector3[2];
        Motion displayedMotion;
        bool hasDisplayedLocomotion, transitioningGait;
        int displayFrame = int.MinValue, supportFoot = -1;
        float displayTime, transitionStarted;
        Vector3 supportAnchor;

        void ClearLocomotionDisplay()
        {
            hasDisplayedLocomotion = transitioningGait = false;
            displayFrame = int.MinValue;
            supportFoot = -1;
        }

        void InitializeLocomotionDisplay()
        {
            if (displayedPositions != null && displayedPositions.Length == bones.Length) return;
            displayedPositions = new Vector3[bones.Length]; displayedScales = new Vector3[bones.Length];
            displayedRotations = new Quaternion[bones.Length];
            transitionPositions = new Vector3[bones.Length]; transitionScales = new Vector3[bones.Length];
            transitionRotations = new Quaternion[bones.Length];
            gaitThighs = new Transform[2]; gaitShins = new Transform[2]; gaitFeet = new Transform[2];
            foreach (var bone in bones)
                for (int side = 0; side < 2; side++)
                {
                    string suffix = side == 0 ? ".L" : ".R";
                    if (bone.name == "Thigh" + suffix) gaitThighs[side] = bone;
                    else if (bone.name == "Shin" + suffix) gaitShins[side] = bone;
                    else if (bone.name == "Foot" + suffix) gaitFeet[side] = bone;
                }
            ClearLocomotionDisplay();
        }

        internal void SampleDisplayedLocomotion(float phase, float duck, float run, float now, int frame)
        {
            if (revision < 7) { SampleLocomotion(phase, duck, run); return; }
            InitializeLocomotionDisplay();
            // HandPosition/SocketAt can restore the visible pose several times in
            // one frame. A frame owns one clock sample, never another blend step.
            if (displayFrame != frame) { displayFrame = frame; displayTime = now; }
            Motion motion = LocomotionMotion(run);
            if (hasDisplayedLocomotion && motion != displayedMotion && duck < .05f)
            {
                System.Array.Copy(displayedPositions, transitionPositions, bones.Length);
                System.Array.Copy(displayedRotations, transitionRotations, bones.Length);
                System.Array.Copy(displayedScales, transitionScales, bones.Length);
                transitionStarted = displayTime;
                transitioningGait = true;
                // The controller requests gait changes just after a footfall.
                // Keep that support in world space while the root keeps moving.
                supportFoot = Mathf.Repeat(phase, .5f) <= .12f ? Mathf.Repeat(phase, 1f) < .5f ? 0 : 1 : -1;
                if (supportFoot >= 0) supportAnchor = displayedFeet[supportFoot];
            }
            if (duck >= .05f) { transitioningGait = false; supportFoot = -1; }
            SampleLocomotion(phase, duck, run);
            if (transitioningGait)
            {
                float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01((displayTime - transitionStarted) / GaitTransitionSeconds));
                Transform foot = supportFoot >= 0 ? gaitFeet[supportFoot] : null;
                Vector3 target = foot ? Vector3.Lerp(supportAnchor, foot.position, weight) : default;
                Quaternion rotation = foot ? foot.rotation : Quaternion.identity;
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i].localPosition = Vector3.Lerp(transitionPositions[i], bones[i].localPosition, weight);
                    bones[i].localRotation = Quaternion.Slerp(transitionRotations[i], bones[i].localRotation, weight);
                    bones[i].localScale = Vector3.Lerp(transitionScales[i], bones[i].localScale, weight);
                }
                if (foot) KeepDisplayedSupport(supportFoot, target, rotation);
                if (weight >= 1) { transitioningGait = false; supportFoot = -1; }
            }
            for (int i = 0; i < bones.Length; i++)
            {
                displayedPositions[i] = bones[i].localPosition;
                displayedRotations[i] = bones[i].localRotation;
                displayedScales[i] = bones[i].localScale;
            }
            for (int side = 0; side < 2; side++)
                if (gaitFeet[side]) displayedFeet[side] = gaitFeet[side].position;
            displayedMotion = motion;
            hasDisplayedLocomotion = true;
        }

        void KeepDisplayedSupport(int side, Vector3 target, Quaternion rotation)
        {
            var thigh = gaitThighs[side]; var shin = gaitShins[side]; var foot = gaitFeet[side];
            if (!thigh || !shin || !foot) return;
            Vector3 hip = thigh.position, knee = shin.position, ankle = foot.position;
            float upper = Vector3.Distance(hip, knee), lower = Vector3.Distance(knee, ankle);
            Vector3 delta = target - hip;
            if (upper < .001f || lower < .001f || delta.sqrMagnitude < .000001f) return;
            Vector3 axis = delta.normalized;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upper - lower) + .0001f, upper + lower - .0001f);
            float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
            float height = Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(knee - hip, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(transform.forward, axis);
            if (bend.sqrMagnitude < .000001f) return;
            Vector3 wantedKnee = hip + axis * along + bend.normalized * height;
            Vector3 wantedAnkle = hip + axis * distance;
            thigh.rotation = Quaternion.FromToRotation(knee - hip, wantedKnee - hip) * thigh.rotation;
            shin.rotation = Quaternion.FromToRotation(foot.position - shin.position, wantedAnkle - shin.position) * shin.rotation;
            foot.rotation = rotation;
        }

        internal Vector3 HandPosition
        {
            get { SampleCurrent(); return chase.DollPose.Action == (int)Motion.Throw ? release.position : grip.position; }
        }
        internal Vector3 SocketAt(Motion motion, float seconds, bool throwing)
        {
            SampleAction(motion, Normalized(motion, seconds));
            Vector3 point = (throwing ? release : grip).position;
            SampleCurrent();
            return point;
        }

        public Transform[] stanceBones;
        public Vector3[] crawlOffsets;
        public Quaternion[] crawlTurns;
        public float crawlHipHeight, deadHipHeight;

        internal void SampleAction(Motion motion, float normalized)
        {
            Sample(motion, normalized);
            if (revision < 7 || !boss || !boss.IsCut(4) || !boss.IsCut(5) || motion == Motion.MaimedCrawl
                || stanceBones == null || stanceBones.Length != 3) return;
            // Keep the authored upper-body reaction while the surviving torso stays supported.
            // Fade this stance into the already grounded final death pose, without a second root tip.
            float fade = motion == Motion.Death ? Mathf.SmoothStep(0, 1, normalized) : 0;
            for (int i = 0; i < stanceBones.Length; i++)
            {
                stanceBones[i].localPosition += crawlOffsets[i] * (1 - fade);
                stanceBones[i].localRotation = Quaternion.Slerp(Quaternion.identity, crawlTurns[i], 1 - fade) * stanceBones[i].localRotation;
            }
            if (motion == Motion.Death && poseRoot)
            {
                float height = transform.InverseTransformPoint(stanceBones[0].position).y;
                float support = Mathf.Lerp(crawlHipHeight, deadHipHeight, fade);
                poseRoot.position -= transform.TransformVector(Vector3.up * Mathf.Max(0, height - support));
            }
        }

        void LateUpdate()
        {
            if (!chase) return;
            ObserveTurn();
            SampleCurrent();
            // The old rig still owns entrance/root movement, but never renders over the doll.
            foreach (var mesh in legacyMeshes) if (mesh) mesh.enabled = false;
            if (chase.DollStretch(out Vector3 target))
                StretchTo(target);
            ApplyLook(chase.DollLookTarget(out var lookPoint) ? lookPoint : (Vector3?)null, Time.deltaTime);
            UpdateHitboxes();
        }

        public float CycleDistance(float duck, float run)
        {
            float upright = revision >= 7
                ? LocomotionMotion(run) == Motion.Run ? runCycleDistance : walkCycleDistance
                : Mathf.Lerp(walkCycleDistance, runCycleDistance, run);
            return Mathf.Lerp(upright, revision >= 7 ? crouchCycleDistance : upright, duck);
        }

        public Transform poseRoot;
        public Quaternion poseRootRotation = Quaternion.identity;
        Vector3 lastFacing;
        bool turning, turnLeft;
        float turnPhase;
        void ObserveTurn()
        {
            if (revision < 7) return;
            var facing = chase.transform.forward;
            float yaw = lastFacing.sqrMagnitude > .5f ? Vector3.SignedAngle(lastFacing, facing, Vector3.up) : 0;
            lastFacing = facing;
            turning = Mathf.Abs(yaw) > Time.deltaTime * 8f;
            if (!turning) { turnPhase = 0; return; }
            bool left = yaw < 0;
            if (left != turnLeft) turnPhase = 0;
            turnLeft = left;
            turnPhase = Mathf.Repeat(turnPhase + Mathf.Abs(yaw) / 90f, 1);
        }

        public void UpdateHitboxes()
        {
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (!hitboxes[i] || !starts[i] || !ends[i]) continue;
                var bounds = new Bounds(starts[i].position, Vector3.zero);
                bounds.Encapsulate(ends[i].position);
                if (middles[i]) bounds.Encapsulate(middles[i].position);
                bounds.Expand(i < 4 ? 1.05f : 1.25f);
                hitboxes[i].transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
                Vector3 scale = hitboxes[i].transform.lossyScale;
                hitboxes[i].center = Vector3.zero;
                hitboxes[i].size = new Vector3(bounds.size.x / Mathf.Abs(scale.x), bounds.size.y / Mathf.Abs(scale.y), bounds.size.z / Mathf.Abs(scale.z));
                hitboxes[i].enabled = !boss || !boss.IsCut(i);
            }
        }
    }
}
