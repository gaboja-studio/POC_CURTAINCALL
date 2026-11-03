using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    /// <summary>추격 단계별 전등 연출. 단계 번호는 <see cref="PursuitSettings.Stages"/>와 같으며 값이 부족하면 마지막 조명 단계를 사용한다.</summary>
    [Serializable]
    public sealed class PursuitLightSettings
    {
        [Tooltip("끄면 단계별 밝기·색·지직임·방 깜빡임을 적용하지 않습니다. 포획 암전은 항상 적용합니다.")]
        public bool Enabled = true;
        [Tooltip("방에 지정되지 않은 씬의 점/스포트 조명도 위치로 방을 찾아 함께 조절합니다.")]
        public bool IncludeUnassignedLights = true;
        [Min(.05f)] public float TransitionSeconds = 1.5f;

        [Header("포획 암전 (해당 방)")]
        [Range(0, 1)] public float CaptureOffIntensity = 0;
        [Tooltip("방 안에서 보는 화면의 환경광 배율(꺼진 순간). 0이면 완전 암전.")]
        [Range(0, 1)] public float CaptureOffAmbient = .02f;
        [Min(1)] public float CaptureFlickerRate = 6;
        [Range(.05f, .95f)] public float CaptureDarkRatio = .5f;
        [Tooltip("켜진 순간의 밝기. 원래 조명 세기 대비 배율(단계 밝기 무시). 색은 현재 단계 색을 사용합니다.")]
        [Range(0, 2)] public float CaptureOnBrightness = 1;
        [Tooltip("켜진 순간 방 안 화면의 환경광 배율(원래 대비).")]
        [Range(0, 1.5f)] public float CaptureOnAmbient = 1;
        [Tooltip("꺼진 순간 이 방까지 빛이 닿는 주변(옆방·복도) 조명도 함께 끕니다.")]
        public bool CaptureDarkenSpill = true;
        [Tooltip("꺼진 순간 방 안 화면을 검게 덮는 정도. 반사·발광 재질까지 가려 거의 안 보이게 합니다. 0이면 끔.")]
        [Range(0, 1)] public float CaptureScreenDarkness = .9f;

        [Header("스파크")]
        [Tooltip("비우면 Resources의 SawSpark 재질을 사용합니다.")]
        public Material SparkMaterial;
        public Color SparkColor = new(1f, .78f, .42f, 1);
        [Min(.01f)] public float SparkSize = .045f;
        [Min(.1f)] public float SparkSpeed = 2.4f;

        public PursuitLightStage[] Stages = {
            new() { Brightness = .5f, AmbientBrightness = .6f },
            new() { Brightness = .5f, AmbientBrightness = .6f, CracklePerMinute = 5, Sparks = 10 },
            new() { Brightness = .5f, AmbientBrightness = .6f, CracklePerMinute = 5, Sparks = 10, EntryFlickers = 3, RoomFlickerInterval = 14 },
            new() { Brightness = .5f, AmbientBrightness = .6f, CracklePerMinute = 5, Sparks = 10, RoomFlickerInterval = 14,
                Tint = new Color(1f, .42f, .34f, 1), TintAmount = .35f }
        };

        public PursuitLightStage StageFor(int index)
        {
            if (index < 0 || Stages == null || Stages.Length == 0) return null;
            return Stages[Mathf.Min(index, Stages.Length - 1)];
        }

        /// <summary>결정적 0..1 해시. 모든 클라이언트가 같은 시계·인덱스로 같은 깜빡임을 계산한다.</summary>
        public static float Hash(int a, int b, int c = 0)
        {
            unchecked {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u + (uint)c * 2246822519u + 3266489917u;
                h = (h ^ (h >> 15)) * 2246822519u;
                h = (h ^ (h >> 13)) * 3266489917u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
        /// <summary>깜빡임 구간에서 꺼진 순간인지. 첫 조각은 항상 꺼져 시작을 분명하게 보여준다.</summary>
        public static bool IsDark(float elapsed, float rate, float darkRatio, int seed)
        {
            if (elapsed < 0) return false;
            int slot = Mathf.FloorToInt(elapsed * Mathf.Max(1, rate));
            return slot == 0 || Hash(seed, slot, 17) < darkRatio;
        }
        /// <summary>진입 깜빡임: N번 꺼졌다 켜진다. 한 번 = cycle초(앞 절반 꺼짐).</summary>
        public static bool EntryDark(float sinceEntry, int count, float cycle)
        {
            if (count <= 0 || sinceEntry < 0 || cycle <= 0 || sinceEntry >= count * cycle) return false;
            return sinceEntry % cycle < cycle * .5f;
        }
        /// <summary>조명별 지직임 시작 시각(이 슬롯 안의 오프셋). 분당 횟수로 슬롯을 나눈다.</summary>
        public static bool Crackle(float clock, int light, float perMinute, float seconds, out int slot, out float local)
        {
            slot = -1; local = 0;
            if (perMinute <= 0 || clock < 0) return false;
            float period = 60f / perMinute;
            slot = Mathf.FloorToInt(clock / period);
            float start = slot * period + Hash(light, slot, 5) * Mathf.Max(0, period - seconds);
            local = clock - start;
            return local >= 0 && local < seconds;
        }
    }

    [Serializable]
    public sealed class PursuitLightStage
    {
        [Tooltip("원래 조명 세기 대비 배율")]
        [Range(0, 1.5f)] public float Brightness = 1;
        [Tooltip("환경광(Ambient) 배율. 전역 밝기에 영향을 줍니다.")]
        [Range(0, 1.5f)] public float AmbientBrightness = 1;
        public Color Tint = Color.white;
        [Tooltip("0 = 원래 색, 1 = Tint 색")]
        [Range(0, 1)] public float TintAmount;

        [Header("지직임 / 스파크")]
        [Tooltip("조명 1개당 분당 지직임 횟수. 0이면 끔.")]
        [Min(0)] public float CracklePerMinute;
        [Min(.05f)] public float CrackleSeconds = .32f;
        [Tooltip("지직일 때 최저 밝기 배율")]
        [Range(0, 1)] public float CrackleDip = .08f;
        [Tooltip("지직일 때 튀는 스파크 수. 0이면 끔.")]
        [Min(0)] public int Sparks;

        [Header("단계 시작 깜빡임 (전체 조명)")]
        [Range(0, 6)] public int EntryFlickers;
        [Min(.1f)] public float EntryFlickerCycle = .42f;

        [Header("방 깜빡임 (플레이어 방·근처 방 중 랜덤)")]
        [Tooltip("평균 간격(초). 0이면 끔.")]
        [Min(0)] public float RoomFlickerInterval;
        [Min(0)] public float RoomFlickerJitter = 5;
        [Min(.2f)] public float RoomFlickerSeconds = 1.8f;
        [Tooltip("플레이어가 있는 방에서 이 거리(m) 안의 방을 근처 방으로 봅니다.")]
        [Min(0)] public float NeighborDistance = 4;
        [Range(0, 1)] public float RoomFlickerOffIntensity = .03f;
        [Range(0, 1)] public float RoomFlickerOffAmbient = .35f;

        public PursuitLightStage Clone() => (PursuitLightStage)MemberwiseClone();
    }
}
