using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Saws;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CurtainCall.Settings.EditorTools
{
    /// <summary>씬 제작 도구·직렬화 데이터·공통 에셋 연결을 빌드와 check-work에서 함께 검사한다.</summary>
    public sealed class CommonSettingsValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public static readonly (Type type, string fields)[] Retired =
        {
            (typeof(HumanCannonPole), "riderCapacity riderSpacing hangLength grabDistance requireClearGrabPath holdSeconds swingAcceleration maxSwingAngle maxSwingSpeed swingDamping continuousSwingInput releaseUpwardSpeed horizontalReleaseMultiplier maxReleaseUpwardSpeed airControlSpeed releaseHorizontalSpeedLimit"),
            (typeof(HumanCannonPoleSystem), "allowPlatformJumpGrab independentInteraction useGrabFacing"),
            (typeof(HumanCannonSystem), "injuries"),
            (typeof(HumanCannonTrampoline), "requireCarrierForBounce grabDistance upwardSpeed groundBounceAirControlSpeed"),
            (typeof(HumanCannonStation), "boardingDistance airControlSpeed animateBarrel"),
            (typeof(HumanCannonHazard), "fireSeversLimbs"),
            (typeof(HumanCannonFireWall), "killOnContact"),
            (typeof(TightropeCourse), "bidirectionalSceneTraversal"),
            (typeof(HumanCannonSettings), "jumpHeight poleJumpHorizontalSpeedLimit"),
            (typeof(TightropeSettings.CourseSettings), "relayHoldSeconds ropeWalkWidth ropeHeightTolerance fallDepth laneChangeZones"),
            (typeof(TightropeSettings.RopeMovementSettings), "jumpInputBuffer trapezeEntryHeightTolerance trapezeEntryGrabDistance"),
            (typeof(TightropeSettings.BodyPenaltySettings), "oneLegMoveSpeed minimumInjuredMoveSpeed reachLossPerArm"),
            (typeof(TightropeSettings.HangSettings), "laneReach rescueDistance"),
            (typeof(SawSettings), "hitCooldown"),
            (typeof(CurtainCall.WaitingRoom.ExpertRoomView), "reach"),
            (typeof(CurtainCall.WaitingRoom.BreakableWall), "reach"),
            (typeof(CurtainCall.WaitingRoom.ClueInspectable), "reach"),
            (typeof(CurtainCall.WaitingRoom.DebugEscapeKeyProp), "reach"),
            (typeof(CurtainCall.WaitingRoom.ExplorationGate), "reach"),
            (typeof(CurtainCall.WaitingRoom.PerformancePropPickup), "reach"),
            (typeof(CurtainCall.WaitingRoom.ToolTarget), "reach"),
            (typeof(CurtainCall.WaitingRoom.NightBed), "reach"),
            (typeof(CurtainCall.WaitingRoom.ScheduleBoardView), "reach"),
            (typeof(CurtainCall.WaitingRoom.DebugMoneyProp), "reach"),
            (typeof(CurtainCall.WaitingRoom.FloorPropsView), "reach"),
            (typeof(CurtainCall.WaitingRoom.HandoverShelfView), "reach"),
            (typeof(CurtainCall.WaitingRoom.ShopDeliverySlot), "reach"),
            (typeof(CurtainCall.WaitingRoom.ShopPhone), "reach"),
            (typeof(CurtainCall.EscapeRoute.EscapeCooperativeLocks), "controlDistance"),
            (typeof(CurtainCall.EscapeRoute.EscapeCooperativeRopes), "buttonDistance"),
            (typeof(CurtainCall.EscapeRoute.EscapeExitSaw), "grabDistance"),
            (typeof(CurtainCall.StartLobby.LobbyInteractable), "reach"),
        };

        public static string[] Validate(GameSettings settings = null, bool checkSources = true, bool checkSerializedAssets = true)
        {
            var errors = new List<string>();
            if (settings == null) settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings/GameSettings/GameSettings.asset");
            if (settings == null) errors.Add("공통 GameSettings.asset 누락");
            else
            {
                var data = new SerializedObject(settings);
                foreach (string field in new[] { "baseSettings", "tightrope", "humanCannon", "poles", "trampoline", "waitingRoom", "exploration", "escapeRoute" })
                {
                    var reference = data.FindProperty(field)?.objectReferenceValue;
                    if (reference == null || !AssetDatabase.Contains(reference)) errors.Add("GameSettings 공통 연결 누락: " + field);
                }
                if (data.FindProperty("poles")?.objectReferenceValue is PoleSettings poles)
                    if (poles.Visual(false) == null || poles.Visual(true) == null) errors.Add("봉 공통 외형 연결 누락");
            }
            foreach (var entry in Retired)
                foreach (var field in entry.fields.Split(' '))
                    if (entry.type.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null)
                        errors.Add("폐기 설정 재도입: " + entry.type.Name + "." + field);
            // 씬 연결 어댑터에는 공통 동작 수치를 저장할 자리를 만들지 않는다.
            foreach (var type in new[] { typeof(TightropeSwingPole), typeof(TightropeTrapeze), typeof(TightropeRelayGate) })
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    if ((field.IsPublic || field.IsDefined(typeof(SerializeField), true))
                        && (field.FieldType == typeof(float) || field.FieldType == typeof(double) || field.FieldType == typeof(int)))
                        errors.Add("씬 연결 어댑터의 개별 수치: " + type.Name + "." + field.Name);
            if (checkSources) CheckSources(errors);
            if (checkSerializedAssets) CheckSerialized(errors);
            return errors.ToArray();
        }

        static void CheckSources(List<string> errors)
        {
            // forward/upward launch speeds remain map trajectory data for cannons.
            var names = Retired.Where(e => e.type == typeof(HumanCannonPole) || e.type == typeof(HumanCannonStation) || e.type == typeof(HumanCannonPoleSystem) || e.type == typeof(TightropeSettings.CourseSettings) || e.type == typeof(TightropeSettings.HangSettings))
                .SelectMany(e => e.fields.Split(' ')).Concat(new[] { "requireCarrierForBounce", "groundBounceAirControlSpeed", "fireSeversLimbs", "bidirectionalSceneTraversal" }).Distinct();
            var oldWrite = new Regex("(?:FindProperty|FindPropertyRelative|SetPoleFloat)\\([^\\n;]*\"(?:" + string.Join("|", names) + ")\"");
            foreach (string folder in new[] { "Assets/Scripts/EscapeRoute/Editor", "Assets/Scripts/MainStage/HumanCannon/Editor", "Assets/Scripts/WaitingRoom/Rehearsal/Editor" })
                foreach (string path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                    if (!path.Contains("Tests") && oldWrite.IsMatch(File.ReadAllText(path))) errors.Add("제작 도구가 폐기 설정을 참조함: " + path);
            foreach (string path in Directory.GetFiles("Assets/Scripts/WaitingRoom", "*.cs", SearchOption.AllDirectories))
            {
                if (!path.Replace('\\', '/').Contains("/Editor/") || path.Contains("Tests")) continue;
                string source = File.ReadAllText(path);
                // UV는 근접 상호작용과 별개의 원거리 장비다. 이 기존 제작 코드만 허용한다.
                if (Path.GetFileName(path) == "StructureAFeatureUpdate.cs")
                    source = source.Replace("{ var so = new SerializedObject(mark); so.FindProperty(\"reach\").floatValue = 10f; so.ApplyModifiedProperties(); }", "");
                if (Regex.IsMatch(source, "(?:FindProperty|FindPropertyRelative|Set|SetFloat)\\([^\\n;]*\"reach\""))
                    errors.Add("제작 도구가 개별 근접 거리를 지정함: " + path);
            }
        }

        public static string[] AssetsWithRetiredData()
        {
            var errors = new List<string>();
            CheckSerialized(errors, true);
            return errors.Distinct().ToArray();
        }

        static void CheckSerialized(List<string> errors, bool pathsOnly = false)
        {
            var scriptFields = new Dictionary<string, HashSet<string>>();
            foreach (var entry in Retired)
            {
                Type owner = entry.type.IsNested ? entry.type.DeclaringType : entry.type;
                string[] scripts = AssetDatabase.FindAssets(owner.Name + " t:MonoScript", new[] { "Assets/Scripts" });
                foreach (string guid in scripts)
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                    if (script == null || script.GetClass() != owner) continue;
                    if (!scriptFields.TryGetValue(guid, out var fields)) scriptFields[guid] = fields = new HashSet<string>();
                    foreach (var field in entry.fields.Split(' ')) fields.Add(field);
                }
            }
            // Scene course shapes contain the same retired placement fields.
            string courseGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/MainStage/Tightrope/Rope/TightropeCourse.cs");
            if (!scriptFields.TryGetValue(courseGuid, out var sceneFields)) scriptFields[courseGuid] = sceneFields = new HashSet<string>();
            foreach (var field in Retired.First(e => e.type == typeof(TightropeSettings.CourseSettings)).fields.Split(' ')) sceneFields.Add(field);
            // SawSettings is embedded in TightropeSettings rather than a standalone asset.
            string ropeGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Systems/Settings/TightropeSettings.cs");
            if (scriptFields.TryGetValue(ropeGuid, out var ropeFields)) ropeFields.Add("hitCooldown");
            foreach (string guid in AssetDatabase.FindAssets("t:Scene t:Prefab t:ScriptableObject", new[] { "Assets/Scenes", "Assets/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!File.Exists(path)) continue;
                string source = File.ReadAllText(path);
                if (!source.StartsWith("%YAML")) continue;
                foreach (string block in Regex.Split(source, "(?m)^--- !u!"))
                {
                    var match = Regex.Match(block, @"m_Script: \{[^\n]*guid: ([a-f0-9]+)");
                    if (!match.Success || !scriptFields.TryGetValue(match.Groups[1].Value, out var fields)) continue;
                    foreach (string field in fields)
                        if (Regex.IsMatch(block, @"(?m)^\s+" + Regex.Escape(field) + ":"))
                            errors.Add(pathsOnly ? path : "폐기된 저장값: " + path + " / " + field);
                }
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = Validate();
            if (errors.Length > 0) throw new BuildFailedException(string.Join("\n", errors));
        }

        [MenuItem("Tools/CurtainCall/공통 설정/연결과 폐기 설정 검사")]
        public static void CheckMenu()
        {
            var errors = Validate();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("공통 설정 연결·제작 도구·씬 저장값 검사 통과");
        }
    }
}
