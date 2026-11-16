using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapePursuitRemodel
    {
        const string IndustryAudioFolder = "Assets/Scenes/Tests/EscapeRoute/Pursuer/IndustrialAudio";
        const int IndustryAudioRate = 24000, IndustryLoopSeconds = 8;

        // Original procedural audio. No source recordings, third-party clips or shared
        // audio assets are changed. Fetch the clip before creating an AudioSource.
        internal static AudioSource AddIndustrialLoop(Transform parent, string name, Vector3 localPosition, bool boiling)
        {
            if (!parent || Application.isPlaying || parent.gameObject.scene.path != ScenePath)
                throw new InvalidOperationException("공장 공간음은 추격 실험 복사 씬의 편집 모드에서만 추가하세요.");
            var clip = IndustrialLoop(boiling);
            if (!clip) throw new InvalidOperationException("공장 공간음 생성 실패");
            var at = parent.Find(name);
            if (at && (!at.TryGetComponent(out AudioSource previous) || previous.clip != clip))
                throw new InvalidOperationException("같은 이름의 다른 오브젝트/음원을 보존합니다: " + name);
            if (!at)
            {
                at = new GameObject(name).transform;
                at.SetParent(parent, false);
            }
            at.localPosition = localPosition;
            var source = at.GetComponent<AudioSource>();
            if (!source) source = at.gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = boiling ? .18f : .14f;
            source.pitch = 1;
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 3; source.maxDistance = 30;
            source.loop = true; source.playOnAwake = true; source.dopplerLevel = 0;
            source.priority = 180;
            // This project has no mixer assets. LocalAudioController discovers scene
            // sources as SFX and applies the existing master/SFX preferences at runtime.
            EditorUtility.SetDirty(source);
            return source;
        }

        static AudioClip IndustrialLoop(bool boiling)
        {
            string path = IndustryAudioFolder + (boiling ? "/IndustrialBoilLoop.wav" : "/IndustrialDriveLoop.wav");
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing) return existing;
            if (!AssetDatabase.IsValidFolder(IndustryAudioFolder))
                AssetDatabase.CreateFolder("Assets/Scenes/Tests/EscapeRoute/Pursuer", "IndustrialAudio");
            // Preserve an already generated source even if its importer has not run yet.
            if (!File.Exists(path)) WriteIndustrialWave(path, IndustrialSamples(boiling));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = IndustryAudioRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static float[] IndustrialSamples(bool boiling)
        {
            int count = IndustryAudioRate * IndustryLoopSeconds;
            var samples = new float[count];
            var random = new System.Random(boiling ? 130814 : 130850);
            // Integer cycles per eight seconds give continuous values/slopes at the
            // seam, including the filtered noise bed. Avoid a repeated fade to silence.
            const int bands = 48;
            var frequency = new double[bands]; var phase = new double[bands];
            for (int b = 0; b < bands; b++)
            {
                frequency[b] = random.Next(boiling ? 1800 : 400, boiling ? 14000 : 6400) / (double)IndustryLoopSeconds;
                phase[b] = random.NextDouble() * Math.PI * 2;
            }
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)IndustryAudioRate, bed = 0;
                for (int b = 0; b < bands; b++) bed += Math.Sin(2 * Math.PI * frequency[b] * t + phase[b]);
                bed *= (boiling ? .052 : .065) / Math.Sqrt(bands);
                double tone = boiling ? .018 * Math.Sin(2 * Math.PI * 38 * t)
                    : .12 * Math.Sin(2 * Math.PI * 50 * t) + .035 * Math.Sin(2 * Math.PI * 100 * t)
                        + .025 * Math.Sin(2 * Math.PI * 30 * t);
                double pulse = .85 + .15 * Math.Sin(2 * Math.PI * (boiling ? .375 : 2) * t);
                samples[i] = (float)((tone + bed) * pulse);
            }
            int events = boiling ? 64 : 14;
            for (int e = 0; e < events; e++)
            {
                int start = random.Next(count);
                double duration = boiling ? .09 + random.NextDouble() * .22 : .035 + random.NextDouble() * .06;
                double from = boiling ? 220 + random.NextDouble() * 380 : 680 + random.NextDouble() * 640;
                double to = from * (boiling ? .35 + random.NextDouble() * .2 : .94);
                double amplitude = boiling ? .075 + random.NextDouble() * .085 : .018;
                int length = (int)(duration * IndustryAudioRate);
                for (int i = 0; i < length; i++)
                {
                    double t = i / (double)IndustryAudioRate, u = t / duration;
                    double envelope = Math.Sin(Math.PI * u);
                    envelope *= envelope * Math.Exp(-2 * u);
                    double angle = 2 * Math.PI * (from * t + (to - from) * t * t / (2 * duration));
                    // Circular placement carries a bubble/rattle tail across the seam.
                    samples[(start + i) % count] += (float)(Math.Sin(angle) * envelope * amplitude);
                }
            }
            float peak = 0;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            float gain = peak > .42f ? .42f / peak : 1;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
            return samples;
        }

        static void WriteIndustrialWave(string path, float[] samples)
        {
            using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(IndustryAudioRate); writer.Write(IndustryAudioRate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (float sample in samples) writer.Write((short)(Mathf.Clamp(sample, -1, 1) * short.MaxValue));
            }
        }
    }
}
