using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Sound
{
    /// <summary>재생 요청 때 사운드 설정 값 위에 덮어쓰는 값. 비운 칸은 설정 값을 쓴다.</summary>
    public struct SoundPlayOptions
    {
        /// <summary>루프 여부. 비우면 사운드 설정의 '루프'.</summary>
        public bool? Loop;
        /// <summary>음량 배율(설정 음량에 곱함). 비우면 1.</summary>
        public float? VolumeScale;
        /// <summary>피치 고정값. 비우면 설정의 피치 범위에서 무작위.</summary>
        public float? Pitch;
        /// <summary>지연(초). 비우면 설정의 지연.</summary>
        public float? Delay;
        /// <summary>재사용 간격을 무시한다.</summary>
        public bool IgnoreCooldown;

        public static SoundPlayOptions Looping => new() { Loop = true };
        public static SoundPlayOptions Volume(float scale) => new() { VolumeScale = scale };
    }

    /// <summary>재생 요청 결과.</summary>
    public enum SoundPlayResult { Played, NoCatalog, Unknown, SituationOff, CueOff, NoClip, Cooldown, NotPlaying }

    /// <summary>재생 기록 한 건(설정 창 '재생 기록' 탭·테스트 씬에서 본다).</summary>
    public readonly struct SoundPlayRecord
    {
        public readonly float Time;
        public readonly string Request, CueId, CallerFile, CallerMember;
        public readonly int CallerLine;
        public readonly SoundPlayResult Result;
        public readonly bool Loop;

        public SoundPlayRecord(float time, string request, string cueId, SoundPlayResult result, bool loop, string callerFile, int callerLine, string callerMember)
        {
            Time = time; Request = request; CueId = cueId; Result = result; Loop = loop;
            CallerFile = callerFile ?? ""; CallerLine = callerLine; CallerMember = callerMember ?? "";
        }

        /// <summary>예: ExplorationTimerView.Show:91</summary>
        public string Caller => string.IsNullOrEmpty(CallerFile) ? "" : $"{Path.GetFileNameWithoutExtension(CallerFile)}.{CallerMember}:{CallerLine}";
    }

    /// <summary>재생한 소리 하나. 루프 소리는 이 핸들로 멈춘다. 이미 끝났거나 다른 소리에 자리를 내줬으면 아무 일도 하지 않는다.</summary>
    public readonly struct SoundHandle
    {
        readonly int voice, serial;
        internal SoundHandle(int voice, int serial) { this.voice = voice; this.serial = serial; }

        public static SoundHandle None => default;
        public bool IsValid => serial != 0;
        public bool IsPlaying => SoundManager.HandlePlaying(voice, serial);
        /// <summary>멈춘다. fadeSeconds > 0이면 서서히 줄인 뒤 멈춘다.</summary>
        public void Stop(float fadeSeconds = 0f) => SoundManager.HandleStop(voice, serial, fadeSeconds);
        /// <summary>재생 중 음량 배율을 바꾼다(예: 추격자 거리에 따라).</summary>
        public void SetVolumeScale(float scale) => SoundManager.HandleVolume(voice, serial, scale);
        public static implicit operator bool(SoundHandle handle) => handle.serial != 0;
    }

    /// <summary>
    /// 사운드 재생기. 게임 코드는 상황 키나 시트 ID로 요청만 한다:
    /// <code>
    /// SoundManager.Play(SoundSituations.RecallBroadcast);             // 상황 키(권장) → 적용 상황 탭에서 고른 시트 ID
    /// SoundManager.Play("UI-302");                                     // 시트 ID 직접
    /// var hold = SoundManager.Play("UI-304", SoundPlayOptions.Looping); // 루프 → hold.Stop()
    /// SoundManager.Play("UI-404", new SoundPlayOptions { VolumeScale = .5f, Pitch = 1.2f });
    /// </code>
    /// 클립·음량·구간·피치·루프는 사운드 설정에서 바꾸며, 같은 키를 쓰는 모든 재생 지점에 함께 적용된다.
    /// Play는 화면 소리, PlayAt은 위치 소리다. 각 클라이언트에서만 재생하며 음량은 <see cref="LocalAudioController"/>를 거친다.
    /// </summary>
    public sealed class SoundManager : MonoBehaviour
    {
        sealed class Voice
        {
            public AudioSource Source;
            public string CueId;
            public float StartedAt, EndAt, BaseVolume, Scale = 1f, FadeOut, LoopStart, LoopEnd;
            public bool Busy, Loop;
            public Transform Follow;
            public bool Following;
            public int Serial;
        }

        const int HistoryLimit = 64;
        static SoundManager instance;
        static int nextSerial;
        static readonly HashSet<string> warned = new(StringComparer.OrdinalIgnoreCase);
        static readonly List<SoundPlayRecord> history = new();
        readonly List<Voice> voices = new();
        readonly Dictionary<string, float> lastPlayed = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>최근 재생 요청(오래된 것부터). 소리가 나지 않은 요청도 이유와 함께 남는다.</summary>
        public static IReadOnlyList<SoundPlayRecord> History => history;
        public static event Action<SoundPlayRecord> Recorded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            instance = null;
            warned.Clear();
            history.Clear();
            Recorded = null;
        }

        static SoundManager EnsureExists()
        {
            if (!Application.isPlaying) return null;
            if (instance != null) return instance;
            var host = new GameObject(nameof(SoundManager)) { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(host);
            return instance = host.AddComponent<SoundManager>();
        }

        /// <summary>상황 키 또는 시트 ID로 재생한다. 소리가 나지 않으면 빈 핸들(false).</summary>
        public static SoundHandle Play(string keyOrId,
            [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLine = 0, [CallerMemberName] string callerMember = "")
            => Play(keyOrId, default(SoundPlayOptions), callerFile, callerLine, callerMember);

        /// <summary>옵션(루프·음량 배율·피치·지연)을 덮어써 재생한다.</summary>
        public static SoundHandle Play(string keyOrId, SoundPlayOptions options,
            [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLine = 0, [CallerMemberName] string callerMember = "")
            => PlayInternal(keyOrId, options, null, null, callerFile, callerLine, callerMember);

        /// <summary>지정 위치에서 3D로 재생한다. 동기화된 상태를 표시하는 각 클라이언트에서 호출한다.</summary>
        public static SoundHandle PlayAt(string keyOrId, Vector3 position, SoundPlayOptions options = default,
            [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLine = 0, [CallerMemberName] string callerMember = "")
            => PlayInternal(keyOrId, options, position, null, callerFile, callerLine, callerMember);

        /// <summary>음원의 위치를 따라 재생한다. 대상이 비활성화되거나 사라지면 멈춘다.</summary>
        public static SoundHandle PlayAt(string keyOrId, Transform follow, SoundPlayOptions options = default,
            [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLine = 0, [CallerMemberName] string callerMember = "")
            => follow != null ? PlayInternal(keyOrId, options, follow.position, follow, callerFile, callerLine, callerMember) : SoundHandle.None;

        static SoundHandle PlayInternal(string keyOrId, SoundPlayOptions options, Vector3? position, Transform follow,
            string callerFile, int callerLine, string callerMember)
        {
            var caller = (callerFile, callerLine, callerMember);
            var catalog = SoundCatalog.Current;
            if (catalog == null)
            {
                WarnOnce("<catalog>", $"[Sound] Resources/{SoundCatalog.ResourcePath} 카탈로그가 없습니다.");
                return Record(keyOrId, "", SoundPlayResult.NoCatalog, false, caller);
            }
            var cue = catalog.Resolve(keyOrId, out var situation);
            string cueId = situation != null ? situation.cueId : keyOrId;
            bool loop = options.Loop ?? cue?.loop ?? false;
            if (situation != null && !situation.enabled) return Record(keyOrId, cueId, SoundPlayResult.SituationOff, loop, caller);
            if (cue == null)
            {
                if (situation == null || !string.IsNullOrEmpty(situation.cueId))
                    WarnOnce(keyOrId, $"[Sound] '{keyOrId}'에 맞는 상황·사운드가 없습니다. Tools/CurtainCall/사운드 설정에서 추가하세요. ({Path.GetFileName(caller.callerFile)}:{caller.callerLine})");
                return Record(keyOrId, cueId, SoundPlayResult.Unknown, loop, caller);
            }
            if (!cue.enabled) return Record(keyOrId, cueId, SoundPlayResult.CueOff, loop, caller);
            var manager = EnsureExists();
            if (manager == null) return Record(keyOrId, cueId, SoundPlayResult.NotPlaying, loop, caller);
            float scale = (situation != null ? situation.volumeScale : 1f) * (options.VolumeScale ?? 1f);
            var handle = manager.PlayCue(catalog, cue, options, loop, scale, position, follow, out var result);
            Record(keyOrId, cueId, result, loop, caller);
            return handle;
        }

        /// <summary>루프로 재생한다. 돌려받은 핸들로 멈춘다.</summary>
        public static SoundHandle PlayLoop(string keyOrId,
            [CallerFilePath] string callerFile = "", [CallerLineNumber] int callerLine = 0, [CallerMemberName] string callerMember = "")
            => Play(keyOrId, SoundPlayOptions.Looping, callerFile, callerLine, callerMember);

        /// <summary>상황 키 또는 시트 ID로 재생 중인 소리를 모두 멈춘다.</summary>
        public static void Stop(string keyOrId, float fadeSeconds = 0f)
        {
            if (instance == null) return;
            string cueId = CueIdOf(keyOrId);
            foreach (var voice in instance.voices)
                if (voice.Busy && string.Equals(voice.CueId, cueId, StringComparison.OrdinalIgnoreCase)) instance.StopVoice(voice, fadeSeconds);
        }

        public static void StopAll()
        {
            if (instance == null) return;
            foreach (var voice in instance.voices) if (voice.Busy) instance.Release(voice);
        }

        public static bool IsPlaying(string keyOrId)
        {
            if (instance == null) return false;
            string cueId = CueIdOf(keyOrId);
            foreach (var voice in instance.voices)
                if (voice.Busy && string.Equals(voice.CueId, cueId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static void ClearHistory() => history.Clear();

        static string CueIdOf(string keyOrId)
        {
            var situation = SoundCatalog.Current != null ? SoundCatalog.Current.FindSituation(keyOrId) : null;
            return situation != null ? situation.cueId : keyOrId;
        }

        static SoundHandle Record(string request, string cueId, SoundPlayResult result, bool loop, (string file, int line, string member) caller)
        {
            var record = new SoundPlayRecord(Time.unscaledTime, request, cueId, result, loop, caller.file, caller.line, caller.member);
            if (history.Count >= HistoryLimit) history.RemoveAt(0);
            history.Add(record);
            Recorded?.Invoke(record);
            return SoundHandle.None;
        }

        static void WarnOnce(string key, string message)
        {
            if (warned.Add(key ?? "")) Debug.LogWarning(message);
        }

        // ── 핸들 ─────────────────────────────────────────

        static Voice Lookup(int index, int serial) =>
            instance != null && index >= 0 && index < instance.voices.Count && instance.voices[index].Busy
            && instance.voices[index].Serial == serial ? instance.voices[index] : null;

        internal static bool HandlePlaying(int index, int serial) => Lookup(index, serial) != null;

        internal static void HandleStop(int index, int serial, float fade)
        {
            var voice = Lookup(index, serial);
            if (voice != null) instance.StopVoice(voice, fade);
        }

        internal static void HandleVolume(int index, int serial, float scale)
        {
            var voice = Lookup(index, serial);
            if (voice == null) return;
            voice.Scale = Mathf.Max(0f, scale);
            LocalAudioController.SetSourceVolume(voice.Source, Mathf.Clamp01(voice.BaseVolume * voice.Scale));
        }

        // ── 재생 ─────────────────────────────────────────

        SoundHandle PlayCue(SoundCatalog catalog, SoundCue cue, SoundPlayOptions options, bool loop, float scale,
            Vector3? position, Transform follow, out SoundPlayResult result)
        {
            var clip = cue.PickClip();
            if (clip == null) { result = SoundPlayResult.NoClip; return SoundHandle.None; }
            float now = Time.unscaledTime;
            if (!options.IgnoreCooldown && cue.cooldown > 0f && lastPlayed.TryGetValue(cue.id, out float last) && now - last < cue.cooldown)
            {
                result = SoundPlayResult.Cooldown;
                return SoundHandle.None;
            }
            lastPlayed[cue.id] = now;

            var voice = Acquire(catalog.voices);
            var source = voice.Source;
            float pitch = options.Pitch ?? cue.PickPitch();
            float delay = Mathf.Max(0f, options.Delay ?? cue.delay);
            source.Stop();
            voice.Follow = follow;
            voice.Following = follow != null;
            source.transform.position = position ?? transform.position;
            source.spatialBlend = position.HasValue ? 1f : 0f;
            source.ignoreListenerPause = !position.HasValue;
            source.minDistance = Mathf.Max(.01f, cue.minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, cue.maxDistance);
            source.clip = clip;
            source.pitch = pitch;
            source.loop = loop;
            source.outputAudioMixerGroup = cue.output;
            source.time = cue.StartTime(clip);
            LocalAudioController.Register(source, cue.category == SoundCategory.Bgm ? LocalAudioCategory.Bgm : LocalAudioCategory.Sfx);
            voice.BaseVolume = cue.volume * catalog.CategoryVolume(cue.category);
            voice.Scale = Mathf.Max(0f, scale);
            LocalAudioController.SetSourceVolume(source, Mathf.Clamp01(voice.BaseVolume * voice.Scale));
            if (delay > 0f) source.PlayDelayed(delay); else source.Play();

            voice.CueId = cue.id;
            voice.Loop = loop;
            voice.LoopStart = cue.StartTime(clip);
            voice.LoopEnd = cue.EndTime(clip);
            voice.StartedAt = now + delay;
            voice.EndAt = loop ? float.PositiveInfinity : voice.StartedAt + cue.PlayLength(clip, pitch);
            voice.FadeOut = !loop && cue.maxDuration > 0f ? cue.fadeOut : 0f;
            voice.Busy = true;
            voice.Serial = ++nextSerial;
            result = SoundPlayResult.Played;
            return new SoundHandle(voices.IndexOf(voice), voice.Serial);
        }

        Voice Acquire(int limit)
        {
            foreach (var voice in voices) if (!voice.Busy) return voice;
            if (voices.Count < Mathf.Max(1, limit)) return AddVoice();
            Voice oldest = null;
            foreach (var voice in voices)
                if (!voice.Loop && (oldest == null || voice.StartedAt < oldest.StartedAt)) oldest = voice;
            oldest ??= voices[0];
            Release(oldest);
            return oldest;
        }

        Voice AddVoice()
        {
            var host = new GameObject("Sound voice") { hideFlags = HideFlags.DontSave };
            host.transform.SetParent(transform, false);
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.ignoreListenerPause = true;
            var voice = new Voice { Source = source };
            voices.Add(voice);
            return voice;
        }

        void StopVoice(Voice voice, float fade)
        {
            if (fade <= 0f) { Release(voice); return; }
            float now = Time.unscaledTime;
            voice.Loop = false;
            if (voice.Source != null) voice.Source.loop = false;
            voice.EndAt = Mathf.Min(voice.EndAt, now + fade);
            voice.FadeOut = fade;
        }

        void Release(Voice voice)
        {
            if (voice.Source != null) voice.Source.Stop();
            voice.Busy = false;
            voice.Loop = false;
            voice.CueId = null;
            voice.Follow = null;
            voice.Following = false;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            foreach (var voice in voices)
            {
                if (!voice.Busy) continue;
                var source = voice.Source;
                if (source == null || voice.Following && (voice.Follow == null || !voice.Follow.gameObject.activeInHierarchy)
                    || now >= voice.EndAt || !voice.Loop && now > voice.StartedAt + 0.1f && !source.isPlaying)
                {
                    Release(voice);
                    continue;
                }
                if (voice.Following) source.transform.position = voice.Follow.position;
                // 루프는 설정한 재생 구간만 반복한다(구간이 클립 전체면 AudioSource 루프가 처리).
                if (voice.Loop && source.clip != null && (voice.LoopStart > 0.001f || voice.LoopEnd < source.clip.length - 0.01f)
                    && (source.time >= voice.LoopEnd || source.time < voice.LoopStart - 0.001f))
                    source.time = voice.LoopStart;
                if (voice.FadeOut > 0f && voice.EndAt - now < voice.FadeOut)
                    LocalAudioController.SetSourceVolume(source, Mathf.Clamp01(voice.BaseVolume * voice.Scale * Mathf.Clamp01((voice.EndAt - now) / voice.FadeOut)));
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
