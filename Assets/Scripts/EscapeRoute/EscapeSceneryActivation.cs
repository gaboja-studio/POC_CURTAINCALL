using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Prepares background modules and static geometry while the team walks the enclosed approach.</summary>
    public sealed class EscapeSceneryActivation
    {
        readonly List<(GameObject item, int objects)> chunks = new();
        readonly List<GameObject> modules = new();
        static readonly ProfilerMarker Marker = new("Escape.ActivateSceneryChunk");
        int cursor, moduleCursor;
        bool suspended;
        public bool Ready => !suspended || moduleCursor == modules.Count && cursor == chunks.Count;

        public void Prepare(GameObject background)
        {
            Restore();
            chunks.Clear(); modules.Clear();
            if (background == null) return;
            // Each authored module keeps its hierarchy and component order. Only pure geometry inside it is split.
            foreach (Transform child in background.transform)
            {
                if (!child.gameObject.activeSelf) continue;
                modules.Add(child.gameObject);
                if (!HasLogic(child.gameObject))
                    foreach (Transform part in child) Collect(part.gameObject);
            }
        }

        static bool HasLogic(GameObject item)
        {
            foreach (var behaviour in item.GetComponents<MonoBehaviour>())
                if (behaviour == null || !(behaviour is UniversalAdditionalLightData)) return true;
            return item.GetComponent<Rigidbody>() != null || item.GetComponent<Animator>() != null
                || item.GetComponent<AudioSource>() != null || item.GetComponent<ParticleSystem>() != null;
        }

        void Collect(GameObject item)
        {
            if (!item.activeSelf || HasLogic(item)) return;
            var tree = item.GetComponentsInChildren<Transform>(true);
            bool scenery = tree.Length <= 128;
            if (scenery)
                foreach (var node in tree)
                    if (HasLogic(node.gameObject)) { scenery = false; break; }
            if (scenery && item.GetComponentInChildren<Renderer>(true) != null)
            {
                chunks.Add((item, tree.Length));
                return;
            }
            foreach (Transform child in item.transform) Collect(child.gameObject);
        }

        public void Suspend()
        {
            if (suspended) return;
            cursor = 0; moduleCursor = 0; suspended = true;
            foreach (var module in modules) if (module != null) module.SetActive(false);
            foreach (var chunk in chunks) if (chunk.item != null) chunk.item.SetActive(false);
        }

        public bool Advance(int objectBudget = 256)
        {
            if (Ready) return true;
            using var sample = Marker.Auto();
            if (moduleCursor < modules.Count)
            {
                var module = modules[moduleCursor++];
                if (module != null) module.SetActive(true);
                return Ready;
            }
            int objects = 0;
            while (cursor < chunks.Count)
            {
                var chunk = chunks[cursor];
                if (objects > 0 && objects + chunk.objects > objectBudget) break;
                cursor++; objects += chunk.objects;
                if (chunk.item != null) chunk.item.SetActive(true);
            }
            return Ready;
        }

        // Call after the parent roots are dormant, including when a transition is cancelled.
        public void Restore()
        {
            if (!suspended) return;
            foreach (var chunk in chunks) if (chunk.item != null) chunk.item.SetActive(true);
            foreach (var module in modules) if (module != null) module.SetActive(true);
            cursor = 0; moduleCursor = 0; suspended = false;
        }
    }
}
