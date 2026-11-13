using CurtainCall.Network.PlayerSync;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapePursuerBoss
    {
        [SerializeField] ParticleSystem blood, landingDust;
        byte presentedMask;
        double presentedRound = -1, landedAt = -1;
        MaterialPropertyBlock tint;
        GUIStyle statusStyle;

        void Present(double clock)
        {
            float age = Mathf.Max(0, (float)(clock - state.Started));
            bool remote = NetworkManager.Singleton && !NetworkManager.Singleton.IsServer;
            if (remote && state.Phase == Phase.Leaping)
            {
                float t = Mathf.Clamp01(age / Tuning.bossLeapSeconds);
                transform.position = Vector3.Lerp(state.From, state.Goal, t) + Vector3.up * (4 * Tuning.bossLeapHeight * t * (1-t));
            }
            if (remote && state.Phase == Phase.Dashing)
                transform.position = Vector3.Lerp(state.From, state.Goal, Mathf.SmoothStep(0, 1, age / Tuning.bossDashSeconds));
            if (state.Phase == Phase.LeapWarning) chase.SampleJump(age / Tuning.bossLeapWarning * chase.ContactTime * .4f, state.From, state.From);
            else if (state.Phase == Phase.Leaping) chase.SampleJumpPose(chase.ContactTime * Mathf.Lerp(.5f, .95f, age / Tuning.bossLeapSeconds));
            else if (state.Phase == Phase.Landing) chase.SampleJumpPose(chase.ContactTime + age);
            if (landingRing)
            {
                landingRing.enabled = state.Phase == Phase.LeapWarning || state.Phase == Phase.Leaping || state.Phase == Phase.DashWarning;
                if (landingRing.enabled)
                {
                    landingRing.transform.position = state.Goal + Vector3.up * .06f;
                    landingRing.transform.localScale = Vector3.one * (state.Phase == Phase.DashWarning ? 1.8f : 4f);
                }
            }
            if (state.Phase == Phase.Ignition && ignitionLever)
            {
                ignitionLever.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, 70, Mathf.InverseLerp(.3f, .65f, age)));
                chase.PresentIgnition(ignitionLever.position);
            }
            else if (state.Phase == Phase.Waiting && ignitionLever) ignitionLever.localRotation = Quaternion.identity;
            if (state.Phase == Phase.Landing && landedAt != state.Started)
            {
                landedAt = state.Started;
                if (landingDust) { landingDust.transform.position = transform.position; landingDust.Play(); }
                if (remote) chase.PlayStep(.6f);
            }
            bool sameRound = presentedRound == state.Round;
            if (tint == null) tint = new MaterialPropertyBlock();
            for (int i = 0; i < parts.Length && i < 8; i++)
            {
                bool lost = (state.Mask & (1 << i)) != 0;
                if (lost && sameRound && (presentedMask & (1 << i)) == 0) SeverEffect(parts[i]);
                foreach (var mesh in parts[i].meshes)
                {
                    if (!mesh) continue;
                    mesh.enabled = !lost;
                    var color = CombatStarted && Exposed(state.Mask, i) ? new Color(.85f, .35f, .25f) : mesh is SkinnedMeshRenderer ? Color.white : new Color(.35f, .37f, .39f);
                    tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color); mesh.SetPropertyBlock(tint);
                }
            }
            presentedMask = state.Mask; presentedRound = state.Round;
            // Do not sink the surviving opposite foot below the floor.
            float lower = (state.Mask & 48) == 48 ? 1.55f : (state.Mask & 3) == 3 ? .1f : 0;
            if (CombatStarted) chase.BossBody(!Defeated, lower);
            rig.localPosition = rigRest - Vector3.up * lower;
            rig.localRotation = rigRotation;
            if (Defeated)
            {
                float t = state.Phase == Phase.Dead ? 1 : Mathf.SmoothStep(0, 1, age / 1.8f);
                rig.localRotation = rigRotation * Quaternion.Euler(75f * t, 0, -12f * t);
                rig.localPosition -= Vector3.up * (.6f * t);
            }
        }
        void SeverEffect(SeverablePart part)
        {
            if (blood) { blood.transform.position = state.HitPoint; blood.Emit(42); }
            foreach (var renderer in part.meshes)
            {
                if (!renderer) continue;
                Mesh detached;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    detached = new Mesh();
                    // Compensate the skin transform scale; the detached object inherits it below.
                    skin.BakeMesh(detached, true);
                    Destroy(detached, 4);
                }
                else if (renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh) detached = filter.sharedMesh;
                else continue;
                var go = new GameObject("Severed " + part.label, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(Rigidbody));
                go.layer = 2;
                go.AddComponent<EscapePursuerDebris>();
                go.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                go.transform.localScale = renderer.transform.lossyScale;
                go.GetComponent<MeshFilter>().sharedMesh = detached;
                go.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                // Partitioned skins retain the source vertex buffer; unused vertices must not
                // turn a severed hand's collider into a whole-body box.
                var vertices = detached.vertices;
                var indices = detached.triangles;
                var bounds = new Bounds(vertices[indices[0]], Vector3.zero);
                foreach (int index in indices) bounds.Encapsulate(vertices[index]);
                var collider = go.GetComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                foreach (var own in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(collider, own);
                foreach (var player in NetworkPlayer.All)
                    if (player) foreach (var own in player.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(collider, own);
                var body = go.GetComponent<Rigidbody>(); body.mass = 2; body.linearVelocity = transform.right * Random.Range(-3f, 3f) + Vector3.up * 3;
                body.angularVelocity = Random.insideUnitSphere * 6;
                Destroy(go, 4);
            }
        }
        void OnGUI()
        {
            var local = NetworkPlayer.Local;
            if (!CombatStarted || !local || !InsideArena(local.transform.position) || chase.InShowcase) return;
            if (statusStyle == null) statusStyle = new GUIStyle(GUI.skin.label) { font = labelFont, fontSize = 18, alignment = TextAnchor.MiddleCenter };
            string text = Defeated ? "추격자 제압 · 배선을 끊어 출구를 여세요" : $"추격자 절단 {Cuts}/8  ·  붉은 손·발 → 팔·다리\n쓰러뜨리지 않아도 배선 절단 가능";
            GUI.Label(new Rect(Screen.width * .5f - 280, 58, 560, 52), text, statusStyle);
            foreach (var saw in EscapeExitSaw.Devices)
                if (saw && saw.FinaleBoss == this && saw.Carrier == local.OwnerClientId)
                {
                    GUI.Label(new Rect(Screen.width * .5f - 12, Screen.height * .5f - 14, 24, 28), "+", statusStyle);
                    break;
                }
        }
    }
}
