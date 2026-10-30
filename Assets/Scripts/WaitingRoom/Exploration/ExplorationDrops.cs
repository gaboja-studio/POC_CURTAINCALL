using System;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>열림/파괴 대상에 붙이는 지정 드랍 목록. 빈 목록은 빈 보상. 확률표는 범위 밖.</summary>
    public sealed class ExplorationDrops : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string itemId;
            [Range(1, 99)] public int uidOffset = 70;
            public Transform point;
        }
        [Tooltip("기존 단일 보상 대신 이 목록 사용. UID는 같은 세대의 모든 발견품과 겹치지 않게 지정.")]
        public Entry[] entries = Array.Empty<Entry>();
        [Tooltip("보상 미정이면 기존 단일 보상도 지급하지 않고 설정을 기다린다.")]
        [SerializeField] bool pending;
        public bool Pending => pending;
        public void ConfigureEmpty() { entries = Array.Empty<Entry>(); pending = false; }
        public void ConfigurePending() => pending = true;
        public bool TryConfigure(Entry[] rewards, out string error)
        {
            if (!Validate(rewards, out error)) return false;
            entries = rewards != null ? (Entry[])rewards.Clone() : Array.Empty<Entry>();
            pending = false;
            return true;
        }
        public bool TryValidate(out string error) => Validate(entries, out error);
        static bool Validate(Entry[] rewards, out string error)
        {
            var ids = new System.Collections.Generic.HashSet<int>();
            if (rewards != null) foreach (var reward in rewards)
            {
                if (reward == null || string.IsNullOrWhiteSpace(reward.itemId))
                { error = "드랍 아이템이 지정되지 않았습니다."; return false; }
                if (reward.uidOffset < 1 || reward.uidOffset > 99 || !ids.Add(reward.uidOffset))
                { error = "드랍 UID는1~99에서 중복 없이 지정해야 합니다."; return false; }
            }
            error = ""; return true;
        }
    }
}
