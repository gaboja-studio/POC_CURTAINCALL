using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        // Original procedural source: filtered noise + low pressure pulse. No external audio dependency.
        static AudioClip FinaleRoar()
        {
            const string folder = "Assets/Scenes/Levels/EscapeRouteAudio";
            const string path = folder + "/InfernoBurst.wav";
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Scenes/Levels", "EscapeRouteAudio");
            const int rate = 24000, count = rate * 3;
            using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                var random = new System.Random(73030); float low = 0, air = 0;
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / rate;
                    float noise = (float)(random.NextDouble() * 2 - 1);
                    low += (noise - low) * .027f; air += (noise - air) * .24f;
                    float envelope = Mathf.Clamp01(t / .045f) * Mathf.Pow(Mathf.Clamp01((3 - t) / 2.6f), 1.2f);
                    float pressure = Mathf.Sin(2 * Mathf.PI * (54 * t - 4 * t * t)) * .14f;
                    float sample = Mathf.Clamp((low * 2.2f + air * .48f + pressure) * envelope, -.86f, .86f);
                    writer.Write((short)(sample * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .75f;
            importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
