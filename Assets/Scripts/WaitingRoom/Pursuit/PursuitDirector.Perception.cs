using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    public sealed partial class PursuitDirector
    {
        sealed class Footstep { public Vector3 Position; public float Distance; }
        readonly Dictionary<GameObject, Footstep> footsteps = new();
        readonly List<GameObject> observers = new();
        float nextFootstepSample, nextVisibilitySample;
        void SampleFootsteps()
        {
            if (!PlayerRecognitionEnabled) return;
            if (Time.time < nextFootstepSample) return;
            nextFootstepSample = Time.time + .1f;
            foreach (var player in candidates) {
                if (!IsAvailable(player)) { footsteps.Remove(player); continue; }
                var position = player.transform.position;
                if (!footsteps.TryGetValue(player, out var foot)) { footsteps[player] = new Footstep { Position = position }; continue; }
                var delta = position - foot.Position; foot.Position = position;
                var network = player.GetComponent<NetworkPlayer>();
                var mover = player.GetComponent<PlayerMover>();
                bool airborne = network != null && network.IsSpawned ? network.IsAirborne : mover != null && mover.CurrentJump != JumpKind.None;
                bool sprint = network != null && network.IsSpawned ? network.PresentationSprinting : mover != null && mover.IsSprinting;
                if (airborne || delta.magnitude > 3 || Mathf.Abs(delta.y) > .7f) { foot.Distance = 0; continue; }
                foot.Distance += new Vector2(delta.x, delta.z).magnitude;
                if (foot.Distance < (sprint ? 1.25f : .85f)) continue;
                foot.Distance = 0; EmitFootstep(position, sprint, player.transform);
            }
        }
        public void EmitFootstep(Vector3 position, bool running, Transform source)
        {
            if (!PlayerRecognitionEnabled || !active || Elapsed < 0 || (run != null && !run.HasAuthority)) return;
            var rules = CurrentStageRules;
            foreach (var actor in actors) {
                if (actor == null || !actor.IsActivated) continue;
                float radius = running ? rules.RunHearingRadius : rules.WalkHearingRadius;
                float distance = Vector3.Distance(actor.transform.position, position);
                if (radius <= 0 || distance > radius) continue;
                if (source != null && !ClearLine(actor.transform.position + Vector3.up, position + Vector3.up, source)) radius *= rules.OccludedHearingMultiplier;
                if (radius > 0 && distance <= radius) actor.Hear(position, distance / radius);
            }
        }
        public const float EyeHeight = 1.5f;
        const int CoverColumns = 3, CoverRows = 5;
        /// <summary>
        /// 추격자 눈에서 본 플레이어 몸통(현재 캡슐: 앉으면 낮아진 높이)의 가려진 비율(0~1).
        /// 시선에 수직인 면에 몸 폭×높이 격자(3×5) 지점을 두고, 지점마다 눈에서 직선이 막히는지 잰다.
        /// </summary>
        public float OccludedFraction(Vector3 eye, PlayerMover mover)
        {
            var body = mover.Body;
            var root = mover.transform;
            var scale = root.lossyScale;
            float height = (body != null ? body.height : 1) * Mathf.Abs(scale.y);
            float radius = (body != null ? body.radius : .3f) * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var bottom = body != null ? root.TransformPoint(body.center) - Vector3.up * height * .5f : root.position;
            var flat = Vector3.ProjectOnPlane(bottom - eye, Vector3.up);
            var side = flat.sqrMagnitude > .0001f ? Vector3.Cross(Vector3.up, flat.normalized) : root.right;
            int blocked = 0;
            for (int column = 0; column < CoverColumns; column++)
            for (int row = 0; row < CoverRows; row++) {
                float across = Mathf.Lerp(-.7f, .7f, column / (float)(CoverColumns - 1)) * radius;
                float up = Mathf.Lerp(.1f, .9f, row / (float)(CoverRows - 1)) * height;
                if (!ClearLine(eye, bottom + side * across + Vector3.up * up, root)) blocked++;
            }
            return blocked / (float)(CoverColumns * CoverRows);
        }
        public static float RenderDistance(PursuitSettings rules)
        {
            float speed = 0;
            if (rules.Stages != null) foreach (var stage in rules.Stages)
                if (stage != null) speed = Mathf.Max(speed, stage.PatrolSpeed, stage.ResolvedChaseSpeed);
            float playerSpeed = GameSettings.Base.Character.FreeMoveSpeed * Mathf.Max(1, GameSettings.Base.Character.SprintSpeedMultiplier);
            return (speed + playerSpeed) * (Mathf.Max(5, rules.EncounterSeconds) + .3f) + Mathf.Max(0, rules.RenderSafetyMargin);
        }
        void LateUpdate()
        {
            if (Time.unscaledTime < nextVisibilitySample) return;
            nextVisibilitySample = Time.unscaledTime + .2f;
            observers.Clear();
            foreach (var player in NetworkPlayer.All) if (player != null && player.IsSpawned) observers.Add(player.gameObject);
            if (observers.Count == 0) foreach (var player in FindObjectsByType<PlayerMover>(FindObjectsSortMode.None))
                if (player.isActiveAndEnabled) observers.Add(player.gameObject);
            var rules = GameSettings.WaitingRoom.Pursuit;
            float distance = RenderDistance(rules);
            var camera = Camera.main;
            var planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            foreach (var actor in actors) {
                if (actor == null) continue;
                bool draw = !rules.CullDistantRenderers || observers.Count == 0 || actor.Victim != null;
                foreach (var player in observers)
                    if ((actor.transform.position - player.transform.position).sqrMagnitude <= distance * distance) { draw = true; break; }
                // A distant visible actor or a spectator camera never pops out on screen.
                if (!draw && camera != null) draw = (actor.transform.position - camera.transform.position).sqrMagnitude < distance * distance
                    || GeometryUtility.TestPlanesAABB(planes, new Bounds(actor.transform.position + Vector3.up, new Vector3(2, 3, 2)));
                actor.SetRendered(draw);
            }
        }
    }
}
