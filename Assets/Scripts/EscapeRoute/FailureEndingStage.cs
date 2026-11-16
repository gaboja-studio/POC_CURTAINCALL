using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Local theatrical scenery only. Never modifies the network actors or their bodies.</summary>
    public sealed class FailureEndingStage : MonoBehaviour
    {
        [SerializeField] GameObject scenery;
        [SerializeField] Camera stageCamera;
        [SerializeField] Transform bulbPivot;
        [SerializeField] Light bulbLight;
        [SerializeField] Renderer filament;
        [SerializeField] Renderer bulbGlass;
        [SerializeField] Transform[] displaySlots;
        [SerializeField] GameObject[] masks;
        [SerializeField] GameObject[] costumeLimbs;
        [SerializeField, Range(0, 2)] float swayDegrees = .8f;
        [SerializeField, Min(1)] float swayPeriod = 7f;
        [SerializeField, Min(0)] float brightness = 3.5f;
        readonly List<GameObject> displayed = new();
        readonly List<Camera> hiddenCameras = new();
        readonly List<Canvas> hiddenCanvases = new();
        readonly List<Light> hiddenLights = new();
        MaterialPropertyBlock emission;
        MaterialPropertyBlock glassGlow;
        Quaternion pivotHome;
        bool oldFog;
        UnityEngine.Rendering.AmbientMode oldAmbientMode;
        Color oldAmbient;
        int lightingScene;
        double began;
        public bool Playing { get; private set; }
        public float CurrentIntensity => bulbLight != null ? bulbLight.intensity : 0;
        public float SwayAngle { get; private set; }

        public static float Flicker(double time)
        {
            // Sparse irregular low-intensity dips, never a rapid repeating full-screen flash.
            double cycle = time % 19.7;
            if (cycle > 4.3 && cycle < 4.46) return .035f;
            if (cycle > 4.64 && cycle < 4.76) return .25f;
            if (cycle > 11.8 && cycle < 12.03) return .06f;
            if (cycle > 17.1 && cycle < 17.22) return .4f;
            return .97f + .03f * Mathf.Sin((float)time * 1.9f);
        }

        public void Begin(IReadOnlyList<EscapeEndingResults.Person> people)
        {
            if (Playing) return;
            Playing = true; began = Time.unscaledTimeAsDouble;
            emission ??= new MaterialPropertyBlock();
            glassGlow ??= new MaterialPropertyBlock();
            lightingScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            oldFog = RenderSettings.fog; oldAmbient = RenderSettings.ambientLight; oldAmbientMode = RenderSettings.ambientMode;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.018f, .022f, .028f);
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.enabled && !light.transform.IsChildOf(transform)) { hiddenLights.Add(light); light.enabled = false; }
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.enabled && camera.targetTexture == null && camera != stageCamera)
                { hiddenCameras.Add(camera); camera.enabled = false; }
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled && canvas.sortingOrder < 600 && !canvas.transform.IsChildOf(transform.root))
                { hiddenCanvases.Add(canvas); canvas.enabled = false; }
            int count = Mathf.Min(people.Count, displaySlots.Length);
            for (int i = 0; i < displaySlots.Length; i++) displaySlots[i].gameObject.SetActive(i < count);
            for (int i = 0; i < count; i++)
            {
                int model = Mathf.Clamp(people[i].model, 0, masks.Length - 1);
                var mask = Instantiate(masks[model], displaySlots[i]);
                mask.transform.localScale = Vector3.one * (count == 1 ? 2f : count == 2 ? 1.75f : 1.35f);
                mask.transform.localRotation = Quaternion.Euler(8, i % 2 == 0 ? 22 : -20, i % 2 == 0 ? -9 : 7);
                TintCostume(mask, people[i]);
                mask.name = $"단원 {people[i].slot + 1} · 무대 가면";
                displayed.Add(mask);
                // Center the party in the visible right half for 1–4 participants.
                displaySlots[i].localPosition = new Vector3(2.2f + (i - (count - 1) * .5f) * 1.25f, 2.05f, .06f);
                FitSupport(displaySlots[i], mask.transform, "LeftHempSupport", "RopeLeftAttachment", new Vector3(-.018f,.92f,-.04f), new Vector3(-.30f,.02f,-.15f));
                FitSupport(displaySlots[i], mask.transform, "RightHempSupport", "RopeRightAttachment", new Vector3(.014f,.92f,-.04f), new Vector3(.30f,.09f,-.15f));
            }
            // The same costume geometry as the participants, frozen as local scenery props.
            int pieces = Mathf.Clamp(count * 2, 4, 8);
            for (int i = 0; i < pieces && count > 0; i++)
            {
                int model = Mathf.Clamp(people[i % count].model, 0, masks.Length - 1);
                int limb = new[] { 2, 0, 3, 1 }[i % 4];
                var piece = Instantiate(costumeLimbs[model * 4 + limb], scenery.transform);
                piece.transform.localPosition = new Vector3(.4f + (i % 4) * .9f, .015f, -1.05f - (i / 4) * .65f - (i % 2) * .35f);
                piece.transform.localRotation = Quaternion.Euler(0, new[] { -58f, 30f, 112f, -27f }[i % 4] + i / 4 * 35, 0);
                TintCostume(piece, people[i % count]);
                displayed.Add(piece);
            }
            pivotHome = bulbPivot.localRotation;
            scenery.SetActive(true); stageCamera.gameObject.SetActive(true);
        }

        static void TintCostume(GameObject prop, EscapeEndingResults.Person person)
        {
            int outfit = person.outfit >= 0 ? person.outfit : person.slot;
            var catalog = CurtainCall.CharacterArt.PlayerAppearanceCatalog.Current;
            var source = catalog != null ? catalog.PrefabFor(person.model, outfit) : null;
            var model = source != null ? source.GetComponentInChildren<CurtainCall.Player.HumanoidModel>(true) : null;
            var slots = model != null ? model.PlayerTintMaterialSlots : new[] { 1 };
            var color = CurtainCall.Settings.GameSettings.Base.Character.GetPlayerColor(outfit);
            var palette = catalog != null && outfit >= 0 && outfit < catalog.outfitColors.Length ? catalog.outfitColors[outfit] : color;
            var block = new MaterialPropertyBlock();
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < Mathf.Min(2, materials.Length); i++)
                {
                    renderer.GetPropertyBlock(block, i);
                    bool tint = slots == null || slots.Length == 0 || System.Array.IndexOf(slots, i) >= 0;
                    block.SetColor("_BaseColor", tint ? color : Color.white);
                    block.SetColor("_Color", tint ? color : Color.white);
                    block.SetColor("_VariantColor", palette);
                    block.SetFloat("_VariantPattern", catalog != null ? catalog.PatternFor(outfit) : outfit);
                    renderer.SetPropertyBlock(block, i);
                    block.Clear();
                }
            }
        }

        static void FitSupport(Transform slot, Transform mask, string ropeName, string attachmentName, Vector3 sourceStart, Vector3 sourceEnd)
        {
            var rope = slot.Find(ropeName);
            var attachment = mask.Find("FabricFace/" + attachmentName);
            if (rope == null || attachment == null) return;
            var end = slot.InverseTransformPoint(attachment.position);
            var source = sourceEnd - sourceStart;
            var destination = end - sourceStart;
            float scale = destination.magnitude / source.magnitude;
            var rotation = Quaternion.FromToRotation(source, destination);
            rope.localScale = Vector3.one * scale;
            rope.localRotation = rotation;
            rope.localPosition = sourceStart - rotation * (sourceStart * scale);
        }

        void LateUpdate()
        {
            if (!Playing) return;
            double elapsed = Time.unscaledTimeAsDouble - began;
            SwayAngle = swayDegrees * Mathf.Sin((float)(elapsed * 2 * Mathf.PI / swayPeriod));
            bulbPivot.localRotation = pivotHome * Quaternion.Euler(0, 0, SwayAngle);
            float strength = Flicker(elapsed);
            bulbLight.intensity = brightness * strength;
            // A single electrical pulse drives the source, envelope glow and room lighting.
            emission.SetColor("_EmissionColor", new Color(1f, .43f, .12f) * (7 * strength));
            filament.SetPropertyBlock(emission);
            if (bulbGlass != null)
            {
                glassGlow.SetFloat("_LampPower", strength);
                bulbGlass.SetPropertyBlock(glassGlow);
            }
        }

        public void Stop()
        {
            if (!Playing) return;
            Playing = false;
            stageCamera.gameObject.SetActive(false); scenery.SetActive(false);
            bulbPivot.localRotation = pivotHome;
            // A newly loaded lobby owns its own lighting; only restore the scene we changed.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle == lightingScene)
            { RenderSettings.fog = oldFog; RenderSettings.ambientMode = oldAmbientMode; RenderSettings.ambientLight = oldAmbient; }
            foreach (var camera in hiddenCameras) if (camera != null) camera.enabled = true;
            foreach (var canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
            foreach (var light in hiddenLights) if (light != null) light.enabled = true;
            hiddenCameras.Clear(); hiddenCanvases.Clear(); hiddenLights.Clear();
            foreach (var mask in displayed) if (mask != null) Destroy(mask);
            displayed.Clear();
        }
        void OnDisable() => Stop();
    }
}
