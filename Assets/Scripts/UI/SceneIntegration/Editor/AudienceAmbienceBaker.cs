#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    /// <summary>Prepare a seamless, quiet human walla bed once in the editor.</summary>
    public static class AudienceAmbienceBaker
    {
        const int Rate = 24000;
        public static AudioClip Build(AudioClip source, string path)
        {
            var original = new float[source.samples * source.channels];
            if (source.length < 40 || !source.GetData(original, 0)) throw new InvalidOperationException("32s murmur source segment is required.");
            var segment = new float[Rate * 32]; float low = 0, high = 0, previous = 0;
            float lowAlpha = 1 - Mathf.Exp(-2 * Mathf.PI * 2200 / Rate);
            float highAlpha = Mathf.Exp(-2 * Mathf.PI * 100 / Rate);
            for (int i = 0; i < segment.Length; i++)
            {
                float frame = (8f + i / (float)Rate) * source.frequency; int a = (int)frame; float sample = 0;
                for (int c = 0; c < source.channels; c++)
                    sample += Mathf.Lerp(original[a * source.channels + c], original[(a + 1) * source.channels + c], frame - a) / source.channels;
                high = highAlpha * (high + sample - previous); previous = sample;
                low += lowAlpha * (high - low); segment[i] = low;
            }
            // Play 2..30s, then crossfade 30..32s into 0..2s; the wrap advances naturally from 2s again.
            int blend = Rate * 2; var output = new float[segment.Length - blend];
            int straight = segment.Length - 2 * blend;
            Array.Copy(segment, blend, output, 0, straight);
            for (int i = 0; i < blend; i++) output[straight + i] = Mathf.Lerp(segment[segment.Length - blend + i], segment[i], i / (float)(blend - 1));
            double energy = 0; float peak = 0;
            foreach (float sample in output) { energy += sample * sample; peak = Mathf.Max(peak, Mathf.Abs(sample)); }
            float gain = Mathf.Min(.65f / Mathf.Max(.001f, peak), .09f / Mathf.Max(.001f, (float)Math.Sqrt(energy / output.Length)));
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + output.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(output.Length * 2);
                foreach (float sample in output) writer.Write((short)(Mathf.Clamp(sample * gain, -1, 1) * 32767));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path); var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate; settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings; importer.loadInBackground = true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
#endif
