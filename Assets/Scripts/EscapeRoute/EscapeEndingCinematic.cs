using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    [DefaultExecutionOrder(700)]
    public sealed class EscapeEndingCinematic : MonoBehaviour
    {
        [SerializeField] Transform pathStart;
        [SerializeField] Transform[] meadowTiles;
        [SerializeField] CinemachineCamera cinematicCamera;
        [SerializeField] Transform cameraTarget;
        [SerializeField] GameObject cinematicScenery;
        [SerializeField] GameObject[] sceneryToHide;
        readonly List<NetworkPlayer> survivors = new();
        readonly List<Vector3> tileHomes = new();
        readonly List<GameObject> hidden = new();
        readonly List<Light> lights = new();
        readonly List<CurtainCall.Cameras.CameraModelVisibility> visibility = new();
        bool fogBefore;
        Color fogColorBefore, ambientBefore, cameraColorBefore;
        float fogDensityBefore;
        FogMode fogModeBefore;
        UnityEngine.Rendering.AmbientMode ambientModeBefore;
        Camera renderCamera;
        CameraClearFlags clearBefore;
        EscapeEndingRunner runner;
        NetworkPlayer localActor;
        bool playing;
        public bool Playing => playing;

        public void Begin(IReadOnlyCollection<ulong> escaped)
        {
            if (playing || pathStart == null || cinematicCamera == null) return;
            playing = true;
            foreach (var helper in FindObjectsByType<CurtainCall.Cameras.CameraModelVisibility>(FindObjectsSortMode.None))
                if (helper.enabled) { visibility.Add(helper); helper.enabled = false; }
            foreach (var item in sceneryToHide)
                if (item != null && item.activeSelf) { hidden.Add(item); item.SetActive(false); }
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>())
                    if (light.type == LightType.Directional && light.enabled) { lights.Add(light); light.enabled = false; }
            fogBefore = RenderSettings.fog; fogColorBefore = RenderSettings.fogColor;
            fogModeBefore = RenderSettings.fogMode; fogDensityBefore = RenderSettings.fogDensity;
            ambientBefore = RenderSettings.ambientLight; ambientModeBefore = RenderSettings.ambientMode;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = .004f; RenderSettings.fogColor = new Color(.027f,.043f,.075f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.25f,.30f,.43f);
            renderCamera = Camera.main;
            if (renderCamera != null)
            {
                clearBefore = renderCamera.clearFlags; cameraColorBefore = renderCamera.backgroundColor;
                renderCamera.clearFlags = CameraClearFlags.SolidColor;
                renderCamera.backgroundColor = RenderSettings.fogColor;
            }
            cinematicScenery.SetActive(true);
            survivors.Clear(); tileHomes.Clear();
            foreach (var tile in meadowTiles) tileHomes.Add(tile.localPosition);
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && Contains(escaped, player.OwnerClientId)) survivors.Add(player);
            survivors.Sort((a,b) => a.Slot.CompareTo(b.Slot));
            float slowest = 1;
            foreach (var actor in survivors)
                if (actor.TryGetComponent(out PlayerMover motor)) slowest = Mathf.Min(slowest, motor.SpeedMultiplier);
            for (int i = 0; i < survivors.Count; i++)
            {
                var actor = survivors[i];
                if (!actor.IsOwner || !actor.TryGetComponent(out PlayerMover motor)) continue;
                var position = pathStart.position + pathStart.right * ((i - (survivors.Count - 1) * .5f) * 1.35f)
                    - pathStart.forward * (i % 2 * .6f);
                motor.Relocate(position, pathStart.rotation);
                if (actor.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
                runner = gameObject.AddComponent<EscapeEndingRunner>();
                localActor = actor;
                runner.Begin(motor, pathStart.forward, motor.SpeedMultiplier > 0 ? slowest / motor.SpeedMultiplier : 0);
            }
            UpdateTarget();
            var follow = cinematicCamera.GetComponent<CinemachineFollow>();
            if (follow != null) follow.FollowOffset = pathStart.TransformDirection(new Vector3(0, 1.05f, 10));
            cinematicCamera.Follow = cameraTarget; cinematicCamera.LookAt = cameraTarget;
            cinematicCamera.enabled = true;
            cinematicCamera.PreviousStateIsValid = false;
        }
        static bool Contains(IReadOnlyCollection<ulong> ids, ulong id)
        { foreach (var candidate in ids) if (candidate == id) return true; return false; }
        void LateUpdate()
        {
            if (!playing) return;
            PaceLocalRunner();
            UpdateTarget();
            // Recycle only scenic tiles behind the party. Actors keep running forward on real colliders.
            float travel = Vector3.Dot(cameraTarget.position - pathStart.position, pathStart.forward);
            foreach (var tile in meadowTiles)
                while (Vector3.Dot(tile.position - pathStart.position, pathStart.forward) + 120 < travel - 35)
                    tile.position += pathStart.forward * (120 * meadowTiles.Length);
        }
        void PaceLocalRunner()
        {
            if (runner == null || localActor == null || !localActor.TryGetComponent(out PlayerMover motor)) return;
            float localProgress = Progress(localActor, survivors.IndexOf(localActor));
            float tail = localProgress, slowest = motor.SpeedMultiplier;
            for (int i = 0; i < survivors.Count; i++)
            {
                var actor = survivors[i];
                if (actor == null || !actor.IsSpawned || !OnMeadow(actor)) continue;
                tail = Mathf.Min(tail, Progress(actor, i));
                if (actor.TryGetComponent(out PlayerMover otherMotor))
                    slowest = Mathf.Min(slowest, otherMotor.SpeedMultiplier);
            }
            // Owners can start on different frames or advance at different rates. Close gaps
            // with input on the leading actor; keep the actual injury speed and animation.
            float lead = Mathf.Max(0f, localProgress - tail - .30f);
            float pace = motor.SpeedMultiplier > 0f ? slowest / motor.SpeedMultiplier : 0f;
            runner.SetThrottle(pace * Mathf.Clamp01(1f - lead / 2f));
        }
        float Progress(NetworkPlayer actor, int slotIndex)
            => Vector3.Dot(actor.transform.position - pathStart.position, pathStart.forward) + slotIndex % 2 * .6f;

        bool OnMeadow(NetworkPlayer actor)
        {
            Vector3 delta = actor.transform.position - pathStart.position;
            return Mathf.Abs(Vector3.Dot(delta, pathStart.right)) < 6f && Mathf.Abs(delta.y) < 4f;
        }
        void UpdateTarget()
        {
            Vector3 center = Vector3.zero; int count = 0;
            foreach (var actor in survivors)
                if (actor != null && actor.IsSpawned && OnMeadow(actor)) { center += actor.transform.position; count++; }
            cameraTarget.position = count > 0 ? center / count : pathStart.position;
        }
        public void Stop()
        {
            if (runner != null) { runner.Stop(); Destroy(runner); runner = null; }
            localActor = null;
            if (cinematicCamera != null) cinematicCamera.enabled = false;
            if (meadowTiles != null)
                for (int i = 0; i < tileHomes.Count; i++) if (meadowTiles[i] != null) meadowTiles[i].localPosition = tileHomes[i];
            if (playing)
            {
                RenderSettings.fog = fogBefore; RenderSettings.fogMode = fogModeBefore;
                RenderSettings.fogColor = fogColorBefore; RenderSettings.fogDensity = fogDensityBefore;
                RenderSettings.ambientMode = ambientModeBefore; RenderSettings.ambientLight = ambientBefore;
                if (renderCamera != null) { renderCamera.clearFlags = clearBefore; renderCamera.backgroundColor = cameraColorBefore; }
            }
            foreach (var item in hidden) if (item != null) item.SetActive(true);
            foreach (var light in lights) if (light != null) light.enabled = true;
            foreach (var helper in visibility) if (helper != null) helper.enabled = true;
            visibility.Clear();
            hidden.Clear(); lights.Clear();
            if (cinematicScenery != null) cinematicScenery.SetActive(false);
            survivors.Clear(); tileHomes.Clear(); playing = false;
        }
        void OnDisable() => Stop();
    }
}
