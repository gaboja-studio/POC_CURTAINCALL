using System;
using UnityEngine;

namespace CurtainCall.CharacterArt
{
    [CreateAssetMenu(menuName="CurtainCall/Player Appearance Catalog")]
    public sealed class PlayerAppearanceCatalog : ScriptableObject
    {
        [Serializable] public struct ModelOption
        {
            public string name;
            [Tooltip("기본 캐릭터 프리팹. HumanoidModel과 기존 캐릭터 구성 요소가 필요합니다.")]
            public GameObject prefab;
            [Tooltip("선택 사항: 의상 순서에 맞춘 별도 프리팹. 비어 있는 항목은 기본 프리팹을 사용합니다.")]
            public GameObject[] outfitPrefabs;
        }
        [Serializable] public struct SlotDefault { [Tooltip("Models 배열의 번호 (0부터)")] public int model; [Tooltip("Outfits 배열의 번호 (0부터)")] public int outfit; }
        public ModelOption[] models = Array.Empty<ModelOption>();
        public string[] outfits = { "빨강", "파랑", "노랑", "초록" };
        public Color[] outfitColors = { new(.65f,.13f,.16f), new(.12f,.35f,.65f), new(.90f,.60f,.17f), new(.23f,.48f,.25f) };
        [Tooltip("의상별 무늬: 0 마름모, 1 줄무늬, 2 점, 3 격자. 항목이 없으면 기존 의상 번호를 사용합니다.")]
        public int[] outfitPatterns = Array.Empty<int>();
        public int PatternFor(int outfit) => outfitPatterns != null && outfit >= 0 && outfit < outfitPatterns.Length ? Mathf.Clamp(outfitPatterns[outfit], 0, 3) : outfit;
        [Tooltip("플레이어 1~4 순서의 기본 모델과 의상")]
        public SlotDefault[] defaults = { new(){outfit=0}, new(){outfit=1}, new(){outfit=2}, new(){outfit=3} };
        static PlayerAppearanceCatalog cached;
        public static PlayerAppearanceCatalog Current => cached != null ? cached : cached = Resources.Load<PlayerAppearanceCatalog>("Settings/PlayerAppearanceCatalog");
        public SlotDefault DefaultFor(int slot) => defaults != null && slot >= 0 && slot < defaults.Length ? defaults[slot] : new SlotDefault { outfit = Mathf.Max(0, slot) };
        public bool Valid(int model, int outfit) => models != null && model >= 0 && model < models.Length && models[model].prefab != null && outfits != null && outfit >= 0 && outfit < outfits.Length && outfit < 4;
        public GameObject PrefabFor(int model, int outfit)
        {
            if (!Valid(model, outfit)) return null;
            var option = models[model];
            return option.outfitPrefabs != null && outfit < option.outfitPrefabs.Length && option.outfitPrefabs[outfit] != null
                ? option.outfitPrefabs[outfit] : option.prefab;
        }
        public static int FindAvailableOutfit(int preferred, int count, System.Collections.Generic.IEnumerable<int> occupied)
        {
            count = Mathf.Clamp(count, 0, 4);
            if (count == 0) return -1;
            var taken = new System.Collections.Generic.HashSet<int>(occupied);
            for (int i = 0; i < count; i++)
            {
                int candidate = (Mathf.Clamp(preferred, 0, count - 1) + i) % count;
                if (!taken.Contains(candidate)) return candidate;
            }
            return -1;
        }
    }
}
