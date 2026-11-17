#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    /// <summary>Offline editing of licensed human recordings. No audio synthesis or DSP allocation during play.</summary>
    public static class AudienceRecordingBaker
    {
        const int Rate = 24000;
        public static AudioClip[] Build(AudioClip source, string folder, string name, float[] starts, bool boo)
        {
            var original = new float[source.samples * source.channels];
            if (!source.GetData(original, 0)) throw new InvalidOperationException("Cannot decode " + source.name);
            var clips = new AudioClip[starts.Length];
            for (int take = 0; take < starts.Length; take++)
            {
                float duration = (boo ? 1.85f : 1.7f) + (take % 3) * .12f;
                // Resample the human recording offline: heavier voices, no layered synthetic sub-bass.
                float speed = boo ? .794f : .841f; // -4 / -3 semitones from the original source.
                int dryCount = (int)(duration * Rate), count = dryCount + (int)(Rate * .95f);
                var dry = new float[count];
                float low = 0, highPass = 0, previous = 0;
                float lowAlpha = 1 - Mathf.Exp(-2 * Mathf.PI * (boo ? 2400 : 3000) / Rate);
                float highAlpha = Mathf.Exp(-2 * Mathf.PI * 65 / Rate);
                for (int i = 0; i < dryCount; i++)
                {
                    float frame = (starts[take] + i * speed / Rate) * source.frequency;
                    int a = (int)frame;
                    if (a + 1 >= source.samples) break;
                    float sample = 0;
                    for (int c = 0; c < source.channels; c++)
                        sample += Mathf.Lerp(original[a * source.channels + c], original[(a + 1) * source.channels + c], frame - a) / source.channels;
                    highPass = highAlpha * (highPass + sample - previous); previous = sample;
                    low += lowAlpha * (highPass - low);
                    float fade = Mathf.Min(Mathf.Clamp01(i / (Rate * .07f)), Mathf.Clamp01((dryCount - 1 - i) / (Rate * .38f)));
                    dry[i] = low * fade;
                }
                var wet = Reverb(dry);
                double energy = 0; float peak = 0;
                for (int i = 0; i < count; i++)
                {
                    dry[i] += wet[i] * (boo ? .17f : .07f); // applause already contains the recorded hall.
                    dry[i] *= Mathf.Clamp01((count - 1 - i) / (Rate * .12f));
                    if (i < dryCount) energy += dry[i] * dry[i];
                    peak = Mathf.Max(peak, Mathf.Abs(dry[i]));
                }
                float gain = Mathf.Min(.85f / Mathf.Max(.001f, peak), .115f / Mathf.Max(.001f, (float)Math.Sqrt(energy / dryCount)));
                string path = folder + "/" + name + "-" + (take + 1) + ".wav";
                Write(path, dry, gain);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path); var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate; settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings; importer.loadInBackground = true; importer.SaveAndReimport();
                clips[take] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            return clips;
        }
        static float[] Reverb(float[] input)
        {
            // Eight damped, incommensurate delay paths followed by diffusion. Baked once, never run on a reaction.
            var sum = new float[input.Length];
            foreach (int length in new[] { 733, 809, 887, 947, 1031, 1123, 1217, 1301 })
            {
                var delay = new float[length]; float damped = 0;
                float feedback = Mathf.Pow(.001f, length / (Rate * 1.15f));
                for (int i = 0; i < input.Length; i++)
                {
                    int index = i % length; float read = delay[index];
                    damped += .34f * (read - damped);
                    delay[index] = (i >= 480 ? input[i - 480] : 0) + damped * feedback;
                    sum[i] += read * .125f;
                }
            }
            foreach (int length in new[] { 113, 173, 277, 421 })
            {
                var delay = new float[length];
                for (int i = 0; i < sum.Length; i++)
                {
                    int index = i % length; float read = delay[index];
                    float value = sum[i]; sum[i] = read - .5f * value; delay[index] = value + .5f * sum[i];
                }
            }
            return sum;
        }
        static void Write(string path, float[] samples, float gain)
        {
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            foreach (float sample in samples) writer.Write((short)(Mathf.Clamp(sample * gain, -1, 1) * 32767));
        }
    }
}
#endif
