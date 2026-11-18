using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Sound;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapeRouteSoundSetup
    {
        const string Library = "Assets/_ThirdParty/Gamemaster Audio - Pro Sound Collection";
        static readonly (string id, string[] names)[] Sources =
        {
            (SoundCueIds.EscapeWallCollapse, new[] { "rock_avalanche_landslide_debris_01", "rock_avalanche_landslide_debris_02", "rock_avalanche_landslide_debris_03" }),
            (SoundCueIds.EscapeFootstep, new[] { "metal_robot_impact_med_step_01", "metal_robot_large_impact_step_01" }),
            (SoundCueIds.EscapeRoar, new[] { "voice_monster_roar_growl_groan_distant_01" }),
            (SoundCueIds.EscapeLanding, new[] { "cinematic_deep_low_whoosh_impact_04" }),
            (SoundCueIds.EscapeArmWarning, new[] { "troll_monster_battle_grunt_02", "troll_monster_battle_grunt_06" }),
            (SoundCueIds.EscapeGrab, new[] { "bow_crossbow_arrow_draw_stretch1_01", "bow_crossbow_arrow_draw_stretch1_10" }),
            (SoundCueIds.EscapeThrow, new[] { "cinematic_deep_low_whoosh_impact_01", "cinematic_deep_bass_pass_whoosh_05" }),
            (SoundCueIds.EscapeRopeWarning, new[] { "elec_lightning_magic_spell_09" }),
            (SoundCueIds.EscapeRopeDisappear, new[] { "elec_lightning_magic_spell_07" }),
            (SoundCueIds.EscapeRopeRestore, new[] { "elec_lightning_magic_spell_04" }),
            (SoundCueIds.EscapeFireWarning, new[] { "alarm_siren_loop_01" }),
            (SoundCueIds.EscapeFireIgnition, new[] { "fireball_blast_projectile_spell_01" }),
            (SoundCueIds.EscapeFireBurning, new[] { "fireball_blast_projectile_spell_02" }),
            (SoundCueIds.EscapeBridgeDrive, new[] { "manhole_metal_slide_door_open_close_3" }),
            (SoundCueIds.EscapeBridgeReady, new[] { "manhole_metal_door_lid_impact_03" }),
            (SoundCueIds.EscapeSawLaunch, new[] { "bullet_impact_metal_heavy_01" }),
            (SoundCueIds.EscapeDoorOpen, new[] { "metal_low_creaking_ship_structure_01" }),
        };

        [MenuItem("Tools/CurtainCall/탈출로/시트 사운드 연결")]
        public static void ApplyMenu()
        {
            Apply();
            Debug.Log("탈출로 사운드 17개 항목·23개 클립을 연결했습니다.");
        }

        public static object Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 사운드를 연결하세요.");
            var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalog>(SoundCatalog.AssetPath);
            if (!catalog) throw new InvalidOperationException("공용 사운드 카탈로그가 없습니다.");
            var paths = Directory.GetFiles(Library, "*.wav", SearchOption.AllDirectories);
            var byName = paths.GroupBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var clips = new Dictionary<string, AudioClip>();
            foreach (var (_, names) in Sources) foreach (string name in names)
            {
                if (!byName.TryGetValue(name, out var matches)) throw new FileNotFoundException("시트 지정 클립이 없습니다.", name);
                if (matches.Length != 1) throw new InvalidOperationException("동일한 파일명을 확인하세요. " + name);
                string path = matches[0].Replace('\\', '/');
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!clip || clip.length <= 0) throw new InvalidOperationException("읽을 수 없는 사운드 " + path);
                clips.Add(name, clip);
            }

            Undo.RecordObject(catalog, "탈출로 시트 사운드 연결");
            catalog.cues.RemoveAll(cue => cue != null && cue.id == "ESC-028");
            foreach (var (id, names) in Sources)
            {
                var cue = catalog.Find(id);
                if (cue == null)
                {
                    var definition = SoundCueIds.Defaults.Single(row => row.id == id);
                    cue = new SoundCue { id = id, situation = definition.situation, description = definition.description,
                        category = SoundCategory.Sfx, volume = .7f };
                    cue.loop = id == SoundCueIds.EscapeFireBurning || id == SoundCueIds.EscapeBridgeDrive;
                    // 시트의 보류 상태를 유지한다. 사운드 설정에서 켜면 연결된 지점에서 재생된다.
                    cue.enabled = id != SoundCueIds.EscapeFireIgnition && id != SoundCueIds.EscapeFireBurning
                        && id != SoundCueIds.EscapeSawLaunch;
                    if (id == SoundCueIds.EscapeFireBurning) cue.volume = .3f;
                    if (id == SoundCueIds.EscapeFootstep)
                    {
                        cue.volume = .65f; cue.minDistance = 4; cue.maxDistance = 70;
                        cue.maxDuration = .9f; cue.fadeOut = .1f;
                    }
                    if (id == SoundCueIds.EscapeRoar)
                    {
                        cue.maxDistance = 85; cue.startOffset = .1f;
                        cue.maxDuration = 2.2f; cue.fadeOut = .18f;
                    }
                    // 원본은 6.8초 whoosh다. 착지 시점에 1.74초 부근의 충격이 오도록 맞춘다.
                    if (id == SoundCueIds.EscapeLanding)
                    {
                        cue.startOffset = 1.68f; cue.maxDuration = 1.8f; cue.fadeOut = .18f;
                    }
                    if (id == SoundCueIds.EscapeGrab) cue.startOffset = .4f;
                    if (id == SoundCueIds.EscapeThrow)
                    {
                        cue.startOffset = .3f; cue.maxDuration = 1.5f; cue.fadeOut = .18f;
                    }
                    if (id == SoundCueIds.EscapeRopeWarning) { cue.maxDuration = .25f; cue.fadeOut = .04f; }
                    if (id == SoundCueIds.EscapeBridgeDrive) cue.volume = .4f;
                    catalog.cues.Add(cue);
                }
                cue.clips = names.Select(name => clips[name]).ToArray();
                if (id == SoundCueIds.EscapeRopeWarning || id == SoundCueIds.EscapeRopeDisappear || id == SoundCueIds.EscapeRopeRestore)
                {
                    cue.minDistance = 1.5f;
                    cue.maxDistance = 14f;
                }
                foreach (var clip in cue.clips)
                {
                    var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                    importer.forceToMono = true;
                    var settings = importer.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }
            }
            catalog.voices = Mathf.Max(catalog.voices, 32);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return new { cues = Sources.Length, clips = clips.Count, libraryAudio = paths.Length, catalog = SoundCatalog.AssetPath };
        }

        public static object Audit()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalog>(SoundCatalog.AssetPath);
            var rows = Sources.Select(row => new
            {
                id = row.id,
                enabled = catalog.Find(row.id)?.enabled ?? false,
                expected = row.names.Length,
                clips = catalog.Find(row.id)?.clips?.Select(clip => new
                {
                    name = clip ? clip.name : "MISSING",
                    path = AssetDatabase.GetAssetPath(clip),
                    seconds = clip ? clip.length : 0,
                }).ToArray(),
            }).ToArray();
            return new { cues = rows.Length, rows };
        }
    }
}
