using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace CurtainCall.Sound
{
    /// <summary>재생 음량을 묶는 분류. 사용자 설정의 효과음·배경음 음량과 아래 분류별 음량을 함께 곱한다.</summary>
    public enum SoundCategory { Ui, Sfx, Bgm }

    /// <summary>
    /// 사운드 시트의 고유 ID 한 줄의 소리 설정(클립·음량·구간·피치·루프). 어떤 게임 상황에서 이 소리를 낼지는
    /// <see cref="SoundSituation"/>이 정한다. 설정 창: Tools/CurtainCall/사운드 설정.
    /// </summary>
    [Serializable]
    public sealed class SoundCue
    {
        [Tooltip("사운드 시트의 고유 ID(예: UI-104). 코드가 이 값으로 재생한다.")]
        public string id = "";
        [Tooltip("시트의 '나오는 상황'.")]
        public string situation = "";
        [Tooltip("시트의 설명·적용 메모.")]
        [TextArea(1, 4)] public string description = "";
        [Tooltip("끄면 이 상황에서 소리를 내지 않는다.")]
        public bool enabled = true;
        [Tooltip("여러 개면 재생할 때마다 무작위로 하나를 고른다. 비우면 소리 없음.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        public SoundCategory category = SoundCategory.Ui;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("재생마다 이 범위에서 무작위 피치. 최소=최대면 고정.")]
        [Range(0.1f, 3f)] public float pitchMin = 1f;
        [Range(0.1f, 3f)] public float pitchMax = 1f;
        [Tooltip("클립의 이 위치(초)부터 재생한다. 앞쪽 무음·불필요한 부분을 건너뛴다.")]
        [Min(0f)] public float startOffset;
        [Tooltip("재생 길이(초). 0이면 클립 끝까지.")]
        [Min(0f)] public float maxDuration;
        [Tooltip("시트의 '루프 여부'. 켜면 멈출 때까지 재생 구간을 반복한다(재생 코드가 Stop 해야 함).")]
        public bool loop;
        [Tooltip("재생 길이를 자를 때 끝부분 페이드 아웃(초).")]
        [Min(0f)] public float fadeOut = 0.05f;
        [Tooltip("요청 후 이 시간(초) 뒤에 재생한다.")]
        [Min(0f)] public float delay;
        [Tooltip("같은 소리를 다시 낼 수 있는 최소 간격(초). 0이면 제한 없음.")]
        [Min(0f)] public float cooldown;
        [Tooltip("비우면 기본 출력. 믹서를 쓰게 되면 그룹을 지정한다.")]
        public AudioMixerGroup output;

        public bool HasClip => FirstClip != null;

        /// <summary>표시·구간 기준으로 쓰는 첫 클립.</summary>
        public AudioClip FirstClip
        {
            get
            {
                if (clips == null) return null;
                foreach (var clip in clips) if (clip != null) return clip;
                return null;
            }
        }

        /// <summary>null을 건너뛰고 무작위 클립을 고른다.</summary>
        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) return null;
            int count = 0;
            foreach (var clip in clips) if (clip != null) count++;
            if (count == 0) return null;
            int pick = UnityEngine.Random.Range(0, count);
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (pick-- == 0) return clip;
            }
            return null;
        }

        public float PickPitch()
        {
            float min = Mathf.Min(pitchMin, pitchMax), max = Mathf.Max(pitchMin, pitchMax);
            return Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
        }

        /// <summary>시작 위치를 클립 안으로 제한한다.</summary>
        public float StartTime(AudioClip clip) =>
            clip == null ? 0f : Mathf.Clamp(startOffset, 0f, Mathf.Max(0f, clip.length - 0.01f));

        /// <summary>재생 구간의 끝 위치(초, 클립 기준).</summary>
        public float EndTime(AudioClip clip)
        {
            if (clip == null) return 0f;
            float start = StartTime(clip);
            return maxDuration > 0f ? Mathf.Min(clip.length, start + maxDuration) : clip.length;
        }

        /// <summary>피치를 반영한 실제 재생 시간(초).</summary>
        public float PlayLength(AudioClip clip, float pitch)
        {
            if (clip == null) return 0f;
            float remaining = Mathf.Max(0f, clip.length - StartTime(clip));
            if (maxDuration > 0f) remaining = Mathf.Min(remaining, maxDuration);
            return remaining / Mathf.Max(0.01f, Mathf.Abs(pitch));
        }
    }

    /// <summary>
    /// 게임 안에서 소리가 나는 한 상황(코드의 재생 지점). 코드는 <see cref="key"/>로 재생을 요청하고,
    /// 어떤 시트 ID(<see cref="cueId"/>)의 소리를 낼지·켜고 끄기·음량 배율은 설정 창 '적용 상황' 탭에서 바꾼다.
    /// </summary>
    [Serializable]
    public sealed class SoundSituation
    {
        [Tooltip("코드가 부르는 상황 키(예: waiting-room.recall-broadcast). 코드와 같아야 한다.")]
        public string key = "";
        [Tooltip("상황 이름.")]
        public string label = "";
        [Tooltip("언제 나는지·코드 위치 메모.")]
        [TextArea(1, 4)] public string description = "";
        [Tooltip("재생할 사운드의 시트 고유 ID. 비우면 소리 없음.")]
        public string cueId = "";
        [Tooltip("끄면 이 상황에서만 소리를 내지 않는다(같은 시트 ID를 쓰는 다른 상황은 그대로).")]
        public bool enabled = true;
        [Tooltip("이 상황에서만 곱하는 음량 배율.")]
        [Range(0f, 2f)] public float volumeScale = 1f;
    }

    /// <summary>프로젝트 전체 사운드 큐·적용 상황 목록. Resources/<see cref="ResourcePath"/>에 하나 둔다.</summary>
    [CreateAssetMenu(fileName = "SoundCatalog", menuName = "CurtainCall/Sound/Sound Catalog")]
    public sealed class SoundCatalog : ScriptableObject
    {
        public const string ResourcePath = "Settings/Sound/SoundCatalog";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".asset";

        [Header("분류별 음량 (사용자 설정 음량에 곱함)")]
        [Range(0f, 1f)] public float uiVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 1f;

        [Tooltip("같은 순간에 낼 수 있는 최대 소리 수. 넘치면 가장 오래된 소리를 끊는다.")]
        [Range(1, 32)] public int voices = 12;

        public List<SoundCue> cues = new();
        public List<SoundSituation> situations = new();

        static SoundCatalog cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => cached = null;

        public static SoundCatalog Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<SoundCatalog>(ResourcePath);
                return cached;
            }
        }

        public SoundCue Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var cue in cues)
                if (cue != null && string.Equals(cue.id, id, StringComparison.OrdinalIgnoreCase)) return cue;
            return null;
        }

        public SoundSituation FindSituation(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var situation in situations)
                if (situation != null && string.Equals(situation.key, key, StringComparison.OrdinalIgnoreCase)) return situation;
            return null;
        }

        /// <summary>상황 키면 그 상황의 시트 ID로, 아니면 시트 ID 그대로 사운드를 찾는다.</summary>
        public SoundCue Resolve(string keyOrId, out SoundSituation situation)
        {
            situation = FindSituation(keyOrId);
            return Find(situation != null ? situation.cueId : keyOrId);
        }

        public float CategoryVolume(SoundCategory category) => category switch
        {
            SoundCategory.Sfx => sfxVolume,
            SoundCategory.Bgm => bgmVolume,
            _ => uiVolume,
        };
    }
}
