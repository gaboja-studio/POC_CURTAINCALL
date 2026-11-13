using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Drives the real owner-controlled actor through PlayerMover; NGO continues to own replication.</summary>
    [DefaultExecutionOrder(-25)]
    public sealed class EscapeEndingRunner : MonoBehaviour
    {
        PlayerMover mover;
        Vector3 forward;
        float throttle;
        bool controlBefore, freeBefore, airBefore;
        Vector3? axisBefore;
        public void Begin(PlayerMover target, Vector3 direction, float inputScale)
        {
            Stop();
            mover = target; forward = direction.normalized; throttle = Mathf.Clamp01(inputScale);
            controlBefore = mover.ControlEnabled; freeBefore = mover.FreeMovement;
            airBefore = mover.FreeAirControl; axisBefore = mover.CourseForwardOverride;
            mover.ResetExternalMotion(false);
        }
        public void SetThrottle(float value) => throttle = Mathf.Clamp01(value);
        void Update()
        {
            if (mover == null) return;
            // The input gate continues to block human controls. Only this authored locomotion feeds the motor.
            mover.FreeMovement = mover.FreeAirControl = true;
            mover.CourseForwardOverride = forward;
            mover.SetControlEnabled(true);
            mover.SetMoveInput(0, throttle);
            mover.SetLocomotionInput(true, false);
        }
        public void Stop()
        {
            if (mover == null) return;
            mover.ClearPendingInput(); mover.ResetExternalMotion(false);
            mover.FreeMovement = freeBefore; mover.FreeAirControl = airBefore;
            mover.CourseForwardOverride = axisBefore; mover.SetControlEnabled(controlBefore);
            mover = null;
        }
        void OnDisable() => Stop();
    }
}
