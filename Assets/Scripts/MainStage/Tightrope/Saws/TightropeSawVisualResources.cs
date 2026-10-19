using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    /// <summary>생성된 톱날 계층이 재질을 소유한다. 기본 도형끼리 공유하며 재생성·씬 종료 때 해제한다.</summary>
    [DisallowMultipleComponent]
    public sealed class TightropeSawVisualResources : MonoBehaviour
    {
        readonly Dictionary<(Material source, Color color, bool luminous), Material> colored = new();
        readonly List<Material> owned = new();

        public Material Colored(Material source, Color color, bool luminous = false)
        {
            var key = (source, color, luminous);
            if (colored.TryGetValue(key, out var existing)) return existing;
            var material = Clone(source);
            material.color = color;
            // 조명 아래의 명암은 남기되 어두운 구간에서도 날과 회전 표식을 읽을 수 있게 한다.
            if (luminous && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.45f);
            }
            colored.Add(key, material);
            return material;
        }

        public Material Clone(Material source)
        {
            var material = new Material(source)
            {
                name = source.name + " (Tightrope Saw runtime)", hideFlags = HideFlags.HideAndDontSave
            };
            owned.Add(material);
            return material;
        }

        void OnDestroy()
        {
            foreach (var material in owned)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            owned.Clear();
            colored.Clear();
        }
    }
}
