using UnityEditor;
using UnityEngine;

namespace CurtainCall.Sound.Editor
{
    /// <summary>
    /// 편집 모드·플레이 모드에서 큐 설정(클립·음량·피치·구간·페이드·루프)을 그대로 들려 준다.
    /// 꺼진 큐도 들을 수 있다. 사용자 음량 설정은 거치지 않는다. 루프는 정지를 누를 때까지 구간을 반복한다.
    /// </summary>
    static class SoundPreviewPlayer
    {
        static AudioSource source;
        static double startedAt, endAt;
        static float baseVolume, fadeOut, loopStart, loopEnd;
        static bool loop;
        /// <summary>지금 미리듣기 중인 대상(시트 ID 또는 상황 키).</summary>
        public static string PlayingId { get; private set; }

        public static bool IsPlaying => source != null && PlayingId != null;

        public static void Play(SoundCatalog catalog, SoundCue cue, float volumeScale = 1f, string playingId = null)
        {
            Stop();
            var clip = cue?.PickClip();
            if (clip == null) return;
            if (source == null)
            {
                var host = EditorUtility.CreateGameObjectWithHideFlags("SoundPreview", HideFlags.HideAndDontSave, typeof(AudioSource));
                source = host.GetComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
            }
            float pitch = cue.PickPitch();
            loop = cue.loop;
            baseVolume = Mathf.Clamp01(cue.volume * volumeScale * (catalog != null ? catalog.CategoryVolume(cue.category) : 1f));
            fadeOut = !loop && cue.maxDuration > 0f ? cue.fadeOut : 0f;
            loopStart = cue.StartTime(clip);
            loopEnd = cue.EndTime(clip);
            source.clip = clip;
            source.pitch = pitch;
            source.volume = baseVolume;
            source.loop = loop;
            source.outputAudioMixerGroup = cue.output;
            source.time = loopStart;
            source.Play();
            startedAt = EditorApplication.timeSinceStartup;
            endAt = loop ? double.PositiveInfinity : startedAt + cue.PlayLength(clip, pitch);
            PlayingId = playingId ?? cue.id;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void Stop()
        {
            if (source != null) source.Stop();
            PlayingId = null;
            EditorApplication.update -= Tick;
        }

        static void Tick()
        {
            if (source == null) { Stop(); return; }
            double now = EditorApplication.timeSinceStartup;
            if (now >= endAt || now > startedAt + 0.2 && !source.isPlaying) { Stop(); return; }
            if (loop && source.clip != null && (loopStart > 0.001f || loopEnd < source.clip.length - 0.01f)
                && (source.time >= loopEnd || source.time < loopStart - 0.001f))
                source.time = loopStart;
            if (fadeOut > 0f && endAt - now < fadeOut)
                source.volume = baseVolume * Mathf.Clamp01((float)((endAt - now) / fadeOut));
        }

        /// <summary>미리듣기 진행 비율(0~1). 루프는 구간 안 위치. 재생 중이 아니면 -1.</summary>
        public static float Progress(string id)
        {
            if (!IsPlaying || PlayingId != id) return -1f;
            if (loop) return loopEnd > loopStart ? Mathf.InverseLerp(loopStart, loopEnd, source.time) : 0f;
            if (endAt <= startedAt) return -1f;
            return Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - startedAt) / (endAt - startedAt)));
        }
    }
}
