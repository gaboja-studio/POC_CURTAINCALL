using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Local art layer over the existing running actors, colliders and cinematic rig.</summary>
    [DefaultExecutionOrder(750)]
    public sealed class EscapeEndingArt : MonoBehaviour
    {
        [SerializeField] GameObject fieldTile;
        [SerializeField] Transform sky;
        [SerializeField] Shader[] sourceShaders;
        [SerializeField] Shader[] wornShaders;
        readonly List<Renderer> hidden = new();
        readonly List<Light> hiddenLights = new();
        readonly List<GameObject> tiles = new();
        readonly Dictionary<Renderer, Material[]> originals = new();
        readonly Dictionary<Material, Material> copies = new();
        readonly HashSet<Material> generated = new();
        readonly Dictionary<Material, Material> sceneryMaterials = new();
        Transform target;
        Material skyMaterial;
        bool running;

        public void Begin(Transform path, Transform follow, Transform[] tileRoots,
            IReadOnlyList<NetworkPlayer> actors, EscapeRouteSettings.EndingLighting look)
        {
            target = follow;
            transform.SetPositionAndRotation(path.position, path.rotation);
            // New route variants can have scenery outside the original hide-list.
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
                    if (!renderer.enabled || renderer.transform.IsChildOf(transform.parent)) continue;
                    bool actorRenderer = false;
                    foreach (var actor in actors) if (renderer.transform.IsChildOf(actor.transform)) { actorRenderer = true; break; }
                    if (!actorRenderer) { hidden.Add(renderer); renderer.enabled = false; }
                }
            foreach (var renderer in transform.parent.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.IsChildOf(transform) && renderer.enabled)
                { hidden.Add(renderer); renderer.enabled = false; }
            foreach (var light in transform.parent.GetComponentsInChildren<Light>(true))
                if (light.type != LightType.Directional && light.enabled)
                { hiddenLights.Add(light); light.enabled = false; }
            foreach (var tile in tileRoots)
            {
                var detail = Instantiate(fieldTile, tile);
                // The existing tile's real running collider is centred at (60,-.1,78).
                detail.transform.SetPositionAndRotation(tile.TransformPoint(new Vector3(60,-.1f,78)), path.rotation);
                tiles.Add(detail);
                ApplySceneryLighting(detail, look.ambientColor);
            }
            ApplySceneryLighting(gameObject, look.ambientColor);
            foreach (var actor in actors)
                foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    originals[renderer] = renderer.sharedMaterials;
            skyMaterial = new Material(sky.GetComponent<Renderer>().sharedMaterial);
            skyMaterial.SetColor("_SkyColor", look.skyColor);
            skyMaterial.SetColor("_HorizonColor", look.fogColor);
            sky.GetComponent<Renderer>().sharedMaterial = skyMaterial;
            running = true;
            LateUpdate();
        }

        void ApplySceneryLighting(GameObject root, Color ambient)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var source = renderer.sharedMaterial;
                if (source == null || !source.HasProperty("_AmbientFill")) continue;
                if (!sceneryMaterials.TryGetValue(source, out var material))
                {
                    material = new Material(source);
                    material.SetColor("_AmbientFill", ambient);
                    sceneryMaterials.Add(source, material);
                }
                renderer.sharedMaterial = material;
            }
        }

        void LateUpdate()
        {
            if (!running) return;
            if (sky != null && target != null) sky.position = target.position;
            // Presentation can reapply an injury surface. Clone that source, keeping property blocks
            // and its palette intact; never change the player's condition or original material asset.
            foreach (var pair in originals)
            {
                if (pair.Key == null) continue;
                var materials = pair.Key.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null || generated.Contains(source)) continue;
                    int shaderIndex = System.Array.IndexOf(sourceShaders, source.shader);
                    if (shaderIndex < 0) continue;
                    if (i < pair.Value.Length) pair.Value[i] = source;
                    if (!copies.TryGetValue(source, out var worn))
                    {
                        worn = new Material(source) { name = source.name + " (ending wear)", shader = wornShaders[shaderIndex] };
                        copies[source] = worn; generated.Add(worn);
                    }
                    materials[i] = worn; changed = true;
                }
                if (changed) pair.Key.sharedMaterials = materials;
            }
            foreach (var material in generated)
                material.SetFloat("_EndingBlood", CurtainCall.Damage.InjuryBloodEffects.IsVisible ? .55f : 0);
        }

        public void Restore()
        {
            if (!running) return;
            running = false;
            foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            foreach (var renderer in hidden) if (renderer != null) renderer.enabled = true;
            foreach (var light in hiddenLights) if (light != null) light.enabled = true;
            foreach (var tile in tiles) if (tile != null) Destroy(tile);
            foreach (var material in generated) Destroy(material);
            foreach (var material in sceneryMaterials.Values) Destroy(material);
            sceneryMaterials.Clear();
            if (skyMaterial != null) Destroy(skyMaterial);
            originals.Clear(); hidden.Clear(); hiddenLights.Clear(); tiles.Clear(); copies.Clear(); generated.Clear();
        }
        void OnDisable() => Restore();
    }
}
