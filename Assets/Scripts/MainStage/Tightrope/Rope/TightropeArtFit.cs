using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>Fits visual-only assets without changing the holder's colliders or motion.</summary>
    public static class TightropeArtFit
    {
        const string ArtName = "Prepared Art";
        static readonly List<Renderer> renderers = new();
        static MaterialPropertyBlock tint;
        static Mesh cylinder;

        public static Mesh UnitCylinder
        {
            get
            {
                if (cylinder != null) return cylinder;
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                primitive.SetActive(false);
                cylinder = primitive.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying) Object.Destroy(primitive); else Object.DestroyImmediate(primitive);
                return cylinder;
            }
        }

        static bool VisualOnly(GameObject source) => source != null
            && source.GetComponentsInChildren<Collider>(true).Length == 0
            && source.GetComponentsInChildren<Rigidbody>(true).Length == 0
            && source.GetComponentsInChildren<MonoBehaviour>(true).Length == 0
            && source.GetComponentInChildren<MeshRenderer>(true) != null;

        public static Bounds LocalBounds(Transform root)
        {
            bool found = false; var result = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Renderer>() == null) continue;
                var bounds = filter.sharedMesh.bounds;
                var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var point = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!found) { result = new Bounds(point, Vector3.zero); found = true; } else result.Encapsulate(point);
                }
            }
            return result;
        }

        static Transform Model(Transform holder, GameObject source)
        {
            var existing = holder.Find(ArtName);
            if (source == null) { if (existing != null) existing.gameObject.SetActive(false); return null; }
            if (existing != null) { existing.gameObject.SetActive(true); return existing; }
            if (!VisualOnly(source)) return null;
            var model = Object.Instantiate(source, holder).transform;
            model.name = ArtName; model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
            var bounds = LocalBounds(model);
            if (bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0)
            {
                model.gameObject.SetActive(false);
                if (Application.isPlaying) Object.Destroy(model.gameObject); else Object.DestroyImmediate(model.gameObject);
                return null;
            }
            model.localScale = new Vector3(1 / bounds.size.x, 1 / bounds.size.y, 1 / bounds.size.z);
            var pivot = new GameObject(ArtName).transform;
            pivot.SetParent(holder, false);
            model.name = "Model"; model.SetParent(pivot, false);
            model.localPosition = -Vector3.Scale(bounds.center, model.localScale);
            return pivot;
        }

        public static bool Box(Transform holder, GameObject source, Color? color = null)
        {
            var model = Model(holder, source);
            var original = holder.GetComponent<Renderer>();
            if (model == null) { if (original != null) original.enabled = true; return false; }
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; model.localScale = Vector3.one;
            if (original != null) original.enabled = false;
            if (color.HasValue) Tint(model, color.Value);
            return true;
        }

        public static bool Line(LineRenderer line, GameObject source, Vector3 from, Vector3 to, Color color)
        {
            var model = Model(line.transform, source);
            if (model == null) { line.enabled = true; return false; }
            var span = to - from;
            if (span.sqrMagnitude <= .000001f) { model.gameObject.SetActive(false); line.enabled = false; return true; }
            model.SetPositionAndRotation((from + to) * .5f, Quaternion.FromToRotation(Vector3.up, span));
            model.localScale = new Vector3(line.startWidth, span.magnitude, line.startWidth);
            line.enabled = false; Tint(model, color); return true;
        }

        public static void Tint(Transform holder, Color color)
        {
            if (holder == null || holder.Find(ArtName) == null && holder.name != ArtName) return;
            tint ??= new MaterialPropertyBlock();
            renderers.Clear(); holder.GetComponentsInChildren(true, renderers);
            foreach (var renderer in renderers)
            {
                if (TightropeOutline.IsOutlineRenderer(renderer)) continue;
                renderer.GetPropertyBlock(tint); tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color); renderer.SetPropertyBlock(tint);
            }
            renderers.Clear();
        }
    }
}
