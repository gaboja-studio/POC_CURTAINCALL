using CurtainCall.Settings;
using CurtainCall.Settings.EditorTools;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>
    /// 봉 인스펙터(2026-11-26): 한글 라벨과 '목마 전용 높은 봉'의 바닥 잡기 판정 상자를 씬 뷰에서 조절한다.
    /// 상자는 물리 충돌체가 아니라 잡기 판정 전용이다. 사용법: Docs/Guides/high-pole-grab-zone-guide.md
    /// </summary>
    [CustomEditor(typeof(HumanCannonPole)), CanEditMultipleObjects]
    sealed class HumanCannonPoleEditor : Editor
    {
        static readonly Color ZoneColor = new(1f, .82f, .2f, 1f);
        static readonly Color SphereColor = new(.35f, .8f, 1f, .5f);
        static readonly Color ReachColor = new(.35f, 1f, .45f, 1f);
        static readonly Color BlockedColor = new(1f, .35f, .3f, 1f);
        readonly BoxBoundsHandle handle = new() { handleColor = ZoneColor, wireframeColor = ZoneColor };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            bool enter = true;
            while (property.NextVisible(enter))
            {
                enter = false;
                using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                    EditorGUILayout.PropertyField(property, KoreanRewardSettingsEditor.LabelFor(property, typeof(HumanCannonPole)), true);
            }
            serializedObject.ApplyModifiedProperties();
            if (targets.Length != 1) return;
            var pole = (HumanCannonPole)target;
            EditorGUILayout.Space();
            if (!pole.PiggybackOnly)
            {
                EditorGUILayout.HelpBox("일반 봉: 봉 중심 구 판정(GameSettings › 봉 설정 › 봉 직접 잡기 반경)을 쓴다. 씬 뷰 하늘색 원.\n"
                    + "목마로만 닿아야 하는 높은 봉이면 '목마 전용 높은 봉'을 켠다.", MessageType.None);
                return;
            }
            EditorGUILayout.HelpBox(Summary(pole), MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!pole.UsesCustomGrabZone && GUILayout.Button("기본 영역을 개별 상자로 복사"))
                {
                    serializedObject.Update();
                    serializedObject.FindProperty("customGrabZone").boolValue = true;
                    var zone = serializedObject.FindProperty("grabZone");
                    zone.FindPropertyRelative("center").vector3Value = GameSettings.Poles.PiggybackGrabZone.center;
                    zone.FindPropertyRelative("size").vector3Value = GameSettings.Poles.PiggybackGrabZone.size;
                    serializedObject.ApplyModifiedProperties();
                }
                if (GUILayout.Button("봉 설정(GameSettings) 선택")) Selection.activeObject = GameSettings.Poles;
            }
        }

        static string Summary(HumanCannonPole pole)
        {
            var zone = pole.GroundGrabZone;
            string source = pole.UsesCustomGrabZone ? "이 봉의 개별 상자" : "GameSettings 기본 영역";
            string rule = GameSettings.Poles.PiggybackOnlyRequiresStack
                ? "목마 맨 위 사람(또는 목마에서 막 도약한 사람)만 바닥에서 잡을 수 있음"
                : "목마 조건 꺼짐: 상자 안에 몸 중심이 닿으면 누구나 잡음";
            string text = $"바닥 잡기 판정: {source}\n중심 {zone.center} · 크기 {zone.SafeSize}\n{rule}\n공중(대포·봉 비행) 잡기와 매달린 동료 잡기는 기존 반경 그대로.";
            if (TryFloorBelow(pole, out float floor))
            {
                var bar = pole.BarPosition(0f);
                var yaw = pole.ZoneRotation;
                text += $"\n\n바로 아래 발판 기준(봉까지 {bar.y - floor:0.##}m):"
                    + Line("혼자 서기", PoleReachReference.SoloStand)
                    + Line("혼자 점프", PoleReachReference.SoloJump)
                    + Line("2단 점프(근사)", PoleReachReference.SoloDoubleJump)
                    + Line("목마 위에 서기", PoleReachReference.PiggybackStand)
                    + Line("목마 위 점프", PoleReachReference.PiggybackJump);
                string Line(string name, float height) => $"\n· {name}: {(PoleReachReference.Reaches(zone, bar, yaw, floor, height) ? "상자 안" : "상자 밖")}";
            }
            return text;
        }

        static bool TryFloorBelow(HumanCannonPole pole, out float floor)
        {
            floor = float.NegativeInfinity;
            var bar = pole.BarPosition(0f);
            foreach (var hit in Physics.RaycastAll(bar + Vector3.up * .05f, Vector3.down, 30f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(pole.transform) || hit.normal.y < .7f) continue;
                floor = Mathf.Max(floor, hit.point.y);
            }
            return float.IsFinite(floor);
        }

        void OnSceneGUI()
        {
            var pole = (HumanCannonPole)target;
            if (pole == null) return;
            var bar = pole.BarPosition(0f);
            if (!pole.PiggybackOnly)
            {
                using (new Handles.DrawingScope(SphereColor))
                    Handles.DrawWireDisc(bar, Vector3.up, GameSettings.Poles.GrabRadius);
                return;
            }
            var zone = pole.GroundGrabZone;
            var yaw = pole.ZoneRotation;
            using (new Handles.DrawingScope(ZoneColor, Matrix4x4.TRS(bar, yaw, Vector3.one)))
            {
                handle.center = zone.center;
                handle.size = zone.SafeSize;
                if (pole.UsesCustomGrabZone)
                {
                    EditorGUI.BeginChangeCheck();
                    handle.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(pole, "봉 판정 상자 조절");
                        serializedObject.Update();
                        var property = serializedObject.FindProperty("grabZone");
                        property.FindPropertyRelative("center").vector3Value = handle.center;
                        property.FindPropertyRelative("size").vector3Value = handle.size;
                        serializedObject.ApplyModifiedProperties();
                    }
                }
                else Handles.DrawWireCube(zone.center, zone.SafeSize);
            }
            Handles.Label(bar + Vector3.up * (zone.center.y + zone.SafeSize.y * .5f + .25f),
                pole.UsesCustomGrabZone ? "목마 전용 봉 · 개별 판정 상자" : "목마 전용 봉 · 기본 판정 상자(GameSettings)");
            if (!TryFloorBelow(pole, out float floor)) return;
            DrawReach(zone, bar, yaw, floor, PoleReachReference.SoloJump, "혼자 점프");
            DrawReach(zone, bar, yaw, floor, PoleReachReference.SoloDoubleJump, "2단 점프");
            DrawReach(zone, bar, yaw, floor, PoleReachReference.PiggybackStand, "목마 위");
            DrawReach(zone, bar, yaw, floor, PoleReachReference.PiggybackJump, "목마 점프");
        }

        static void DrawReach(PoleGrabZone zone, Vector3 bar, Quaternion yaw, float floor, float height, string label)
        {
            bool inside = PoleReachReference.Reaches(zone, bar, yaw, floor, height);
            Vector3 point = bar + yaw * new Vector3(zone.center.x, 0f, zone.center.z);
            point.y = floor + height;
            Vector3 side = yaw * Vector3.right * .6f;
            using (new Handles.DrawingScope(inside ? ReachColor : BlockedColor))
            {
                Handles.DrawLine(point - side, point + side, 2f);
                Handles.Label(point + side, $"{label} 몸 중심 · {(inside ? "닿음" : "안 닿음")}");
            }
        }
    }
}
