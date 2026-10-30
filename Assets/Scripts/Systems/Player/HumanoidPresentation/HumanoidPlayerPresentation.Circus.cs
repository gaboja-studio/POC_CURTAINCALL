using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Player
{
    public sealed partial class HumanoidPlayerPresentation
    {
        readonly CannonMotionPhase circusPhase = new();
        Vector3 previousPresentationPosition, presentationVelocity;
        bool presentationSampleReady;
        HumanCannonStation seatedStation;
        string carryMotion;
        bool wasSeated;
        Vector3 lastSeatedModelPosition, launchVisualOffset;
        float launchBlend;
        string CircusVariant(string motion) => model != null && model.Stage > 0 && model.Stage < 4 ? motion + "_Cut" + model.Stage : motion;
        public string CircusMotion => seatedStation != null ? CircusVariant("CannonLoaded") : circusPhase.Motion == null ? null : CircusVariant(circusPhase.Motion);
        public Vector3 LeftCarryContact { get; private set; }
        public Vector3 RightCarryContact { get; private set; }
        public bool HasCarryContacts { get; private set; }

        void SampleFlightVelocity()
        {
            var p = transform.position;
            if (presentationSampleReady && Time.deltaTime > .00001f)
            {
                var change = p - previousPresentationPosition;
                // Reject teleports; normal remote motion is already NetworkTransform-interpolated.
                if (change.sqrMagnitude < 25f)
                    presentationVelocity = Vector3.Lerp(presentationVelocity, change / Time.deltaTime,
                        1f - Mathf.Exp(-18f * Time.deltaTime));
            }
            previousPresentationPosition = p;
            presentationSampleReady = true;
            var pilot = GetComponent<HumanCannonPilot>();
            if (pilot != null && pilot.IsFlying && (network == null || network.IsOwner))
                presentationVelocity = pilot.FlightVelocity;
        }

        void ResetCircusMotion()
        {
            circusPhase.Reset(); seatedStation = null; wasSeated = false;
            launchBlend = 0; HasCarryContacts = false; carryMotion = null;
        }

        bool PresentCircusMotion()
        {
            if (network == null || !network.IsSpawned || network.State != PlayerState.Normal)
            { ResetCircusMotion(); return false; }
            var system = HumanCannonSystem.Current;
            seatedStation = system != null ? system.SeatedStation(network.OwnerClientId) : null;
            if (seatedStation != null)
            {
                circusPhase.Reset(); airborne = false; landUntil = 0;
                model.Play(CircusVariant("CannonLoaded")); wasSeated = true;
                return true;
            }
            CannonFlightState.Flight flight = default;
            bool flying = system != null && system.TryGetFlight(network.OwnerClientId, out flight);
            if (wasSeated)
            {
                if (flying)
                {
                    launchVisualOffset = lastSeatedModelPosition - model.transform.position;
                    launchBlend = 1f;
                }
                wasSeated = false;
            }
            // Pole entries keep their existing ascent presentation until their first trampoline bounce.
            if (flying && flight.StationIndex == CannonFlightState.PoleEntrySource && flight.Segment == 0)
            { circusPhase.Reset(); model.Play("Flying"); return true; }
            string motion = circusPhase.Step(flying, flight.StationIndex, flight.LaunchRevision, flight.Segment,
                presentationVelocity.y, Time.deltaTime);
            if (motion == null) return false;
            airborne = false; landUntil = 0;
            model.Play(CircusVariant(motion));
            return true;
        }

        bool CarryingTrampoline(out HumanCannonTrampoline trampoline)
        {
            trampoline = network != null ? HumanCannonCarry.Held(network.OwnerClientId) as HumanCannonTrampoline : null;
            return trampoline != null && network != null && network.IsSpawned && network.State == PlayerState.Normal
                && trampoline.Carrier == network.OwnerClientId;
        }

        void PresentCarryLayer(bool carrying)
        {
            if (!carrying) { carryMotion = null; return; }
            string next = CarryingTrampoline(out _) ? CircusVariant("TrampolineCarry") : "Carrying";
            if (carryMotion == next || !model.Animator.HasState(1, Animator.StringToHash(next))) return;
            model.Animator.CrossFadeInFixedTime(next, .16f, 1);
            carryMotion = next;
        }

        void ApplyCircusAlignment()
        {
            HasCarryContacts = false;
            if (IsHanging || model == null || !model.Animator.isHuman || !model.Animator.enabled
                || condition.AllLimbsLost || (network != null && network.State != PlayerState.Normal)) return;
            if (seatedStation != null)
            {
                var head = model.Animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null) return;
                var root = model.transform;
                Vector3 before = root.localPosition;
                // Padded muzzle center equals the existing launch anchor. Keep the skull behind its lip;
                // the lifted face projects forward while chest/arms remain inside the 25-degree barrel.
                Vector3 direction = seatedStation.transform.TransformDirection(new Vector3(0, .4226183f, .9063078f));
                Vector3 target = seatedStation.Muzzle.position - direction * .10f - Vector3.up * .10f;
                root.position += target - head.position;
                alignedModel = root; alignmentOffset = root.localPosition - before;
                lastSeatedModelPosition = root.position;
                return;
            }
            if (launchBlend > 0f)
            {
                var root = model.transform; Vector3 before = root.localPosition;
                root.position += launchVisualOffset * Mathf.SmoothStep(0f, 1f, launchBlend);
                alignedModel = root; alignmentOffset = root.localPosition - before;
                launchBlend = Mathf.Max(0, launchBlend - Time.deltaTime / .22f);
            }
            if (!CarryingTrampoline(out var t)) return;
            // The padded bar center is 0.60m above the legacy anchor; palms rest on its top.
            // Keep the functional anchor/physics envelope unchanged; solve the visible palms instead.
            Vector3 center = t.Handle.position + t.transform.up * (.65f * t.transform.lossyScale.y);
            Vector3 axis = Vector3.Cross(t.transform.up, t.Surface - t.Handle.position).normalized;
            if (Vector3.Dot(axis, transform.right) < 0) axis = -axis;
            bool left = (condition.LostParts & BodyPart.LeftArm) == 0;
            bool right = (condition.LostParts & BodyPart.RightArm) == 0;
            float spacing = left && right ? .24f : 0f;
            LeftCarryContact = center - axis * spacing;
            RightCarryContact = center + axis * spacing;
            if (left) AlignArm(model.Animator, true, LeftCarryContact);
            if (right) AlignArm(model.Animator, false, RightCarryContact);
            HasCarryContacts = left || right;
        }
    }
}
