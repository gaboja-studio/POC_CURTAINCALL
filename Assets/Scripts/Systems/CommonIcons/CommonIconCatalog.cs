using System;
using UnityEngine;

namespace CurtainCall.CommonIcons
{
    public sealed class CommonIconCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public string label;
            public Sprite sprite;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();
        public int Count => entries.Length;
        public Entry GetEntry(int index) => entries[index];

        public Sprite GetSprite(string key)
        {
            foreach (var entry in entries)
                if (entry.key == key) return entry.sprite;
            throw new ArgumentException("등록되지 않은 아이콘: " + key, nameof(key));
        }

        public void Configure(string[] keys, string[] labels, Sprite[] sprites)
        {
            if (keys == null || labels == null || sprites == null ||
                keys.Length != labels.Length || keys.Length != sprites.Length)
                throw new ArgumentException("아이콘 목록의 길이가 다릅니다.");
            var next = new Entry[keys.Length];
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Length; i++)
            {
                if (string.IsNullOrEmpty(keys[i]) || !seen.Add(keys[i]) || sprites[i] == null)
                    throw new ArgumentException("아이콘 키 중복 또는 Sprite 누락: " + keys[i]);
                next[i] = new Entry { key = keys[i], label = labels[i], sprite = sprites[i] };
            }
            entries = next;
        }
    }
}
