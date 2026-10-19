using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Settings
{
    /// <summary>활성 공간의 일반 점프 높이. 저장된 공통 값과 부상 배율은 바꾸지 않는다.</summary>
    public static class CharacterJumpHeightOverrides
    {
        sealed class Entry : IDisposable
        {
            internal Behaviour Owner;
            internal Func<float> ReadHeight;
            public void Dispose() => entries.Remove(this);
        }

        static readonly List<Entry> entries = new();

        /// <summary>해제하거나 소유자가 비활성/파괴되면 이전 공간의 설정으로 돌아간다.</summary>
        public static IDisposable Register(Behaviour owner, Func<float> readHeight)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (readHeight == null) throw new ArgumentNullException(nameof(readHeight));
            var entry = new Entry { Owner = owner, ReadHeight = readHeight };
            entries.Add(entry);
            return entry;
        }

        internal static float Resolve(float defaultHeight)
        {
            var scene = SceneManager.GetActiveScene();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry.Owner == null) { entries.RemoveAt(i); continue; }
                if (!entry.Owner.isActiveAndEnabled || entry.Owner.gameObject.scene != scene) continue;
                float height = entry.ReadHeight();
                if (float.IsFinite(height) && height >= 0f) return height;
            }
            return defaultHeight;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => entries.Clear();
    }
}
