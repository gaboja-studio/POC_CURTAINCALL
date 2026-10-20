using UnityEngine;

namespace CurtainCall.Tightrope
{
    public static partial class TightropeArtFit
    {
        /// <summary>Repeat short rope modules without stretching one mesh across a whole section.</summary>
        public static bool Rope(Transform holder, GameObject source)
        {
            var existing = holder.Find(ArtName);
            var art = Model(holder, source);
            var original = holder.GetComponent<Renderer>();
            if (art == null) { if (original != null) original.enabled = true; return false; }
            if (existing == null)
            {
                float length = Mathf.Abs(holder.lossyScale.z);
                int count = Mathf.Max(1, Mathf.CeilToInt(length / 3f));
                var first = new GameObject("Rope module 1").transform;
                first.SetParent(art, false);
                art.Find("Model").SetParent(first, false);
                for (int i = 0; i < count; i++)
                {
                    var tile = i == 0 ? first : Object.Instantiate(first, art);
                    tile.name = $"Rope module {i + 1}";
                    float from = i * 3f, size = Mathf.Min(3f, length - from);
                    tile.localPosition = new Vector3(0, 0, length > 0 ? -.5f + (from + size * .5f) / length : 0);
                    tile.localScale = new Vector3(1, 1, length > 0 ? size / length : 1);
                }
            }
            if (original != null) original.enabled = false;
            return true;
        }
    }
}
