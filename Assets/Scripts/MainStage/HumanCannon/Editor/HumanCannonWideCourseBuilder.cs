using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>관문 앞 회전날을 옮기고, 두 본 코스를 넓혀 수평 톱 왕복 배치를 저장한다.</summary>
    public static class HumanCannonWideCourseBuilder
    {
        public const float FloorHalfWidth = 22f;
        public const float SawHalfWidth = 24f;

        [MenuItem("Tools/CurtainCall/인간 대포/지상 톱 4초 왕복 적용")]
        public static void ApplyGroundSawSpeedToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬에서 실행하세요.");
            var saws = All<HumanCannonHazard>(scene.GetRootGameObjects())
                .Where(h => h.Kind == CannonHazardKind.GroundSaw && h.GetComponent<HumanCannonPoleSaw>() == null).ToArray();
            if (saws.Length != 8 && saws.Length != 12)
                throw new InvalidOperationException("지상 톱 8개 또는 분산된 지상 톱 12개가 필요합니다.");
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("지상 톱 4초 왕복");
            try
            {
                foreach (var saw in saws)
                {
                    Undo.RecordObject(saw, "지상 톱 왕복 속도");
                    using var data = new SerializedObject(saw);
                    data.FindProperty("periodSeconds").floatValue = 4f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("지상 톱 속도 저장 실패");
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        [MenuItem("Tools/CurtainCall/인간 대포/관문 접근 및 넓은 수평 톱 적용")]
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬에서 실행하세요.");
            var roots = scene.GetRootGameObjects();
            var poles = All<HumanCannonPole>(roots);
            var saws = All<HumanCannonPoleSaw>(roots);
            if (poles.Length != 10 || saws.Length != 10 || saws.Any(s => s.Pole == null || s.Hazard == null))
                throw new InvalidOperationException("기존 봉10/왕복 톱10 연결이 필요합니다.");
            var linked = poles.Where(p => p.LinkedGate != null).OrderBy(p => p.Grip.position.z).ToArray();
            var hazards = All<HumanCannonHazard>(roots);
            if (linked.Length != 2 || (hazards.Length != 26 && hazards.Length != 30))
                throw new InvalidOperationException("협동 관문2/일반 함정26 또는 지상 톱 추가 후30 구성을 확인하세요.");
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("인간 대포 관문 접근과 수평 톱");
            foreach (var root in roots) Undo.RegisterFullObjectHierarchyUndo(root, "코스 폭과 톱 배치");
            try
            {
                // 리프트 구멍과 쉼터 승강기 착지 구역은 유지하고 본 코스의 바깥 가장자리만 늘린다.
                foreach (var floor in All<BoxCollider>(roots).Where(c => c.name.StartsWith("Floor_")
                    && (c.transform.parent.name == "FirstCourseFloor" || c.transform.parent.name == "SecondCourseFloor"
                        || c.transform.parent.name == "ShelterDeck")))
                {
                    float left = floor.bounds.min.x, right = floor.bounds.max.x;
                    if (Mathf.Abs(left + 15f) < .01f) left = -FloorHalfWidth;
                    if (Mathf.Abs(right - 15f) < .01f) right = FloorHalfWidth;
                    ResizeWorldX(floor, left, right);
                }
                foreach (var fence in All<BoxCollider>(roots).Where(c => c.transform.parent != null && c.transform.parent.name == "SideFences"))
                {
                    var position = fence.transform.position;
                    position.x = Mathf.Sign(position.x) * FloorHalfWidth;
                    fence.transform.position = position;
                }
                var lift = All<HumanCannonTransport>(roots).Single(t => t.name == "ShelterLift");
                lift.transform.position += Vector3.right * (-FloorHalfWidth - 10f - lift.StartPosition.x);
                var access = All<BoxCollider>(roots).Single(c => c.name == "ShelterLiftAccess");
                ResizeWorldX(access, -FloorHalfWidth - 13f, -FloorHalfWidth);
                for (int i = 0; i < linked.Length; i++)
                {
                    var gate = linked[i].LinkedGate;
                    // 선풍기형 날이 관문 평면에 겹쳐 있던 배치: 관문을 넘을 여유를 둔 뒤쪽으로 이동.
                    var rotor = hazards.Single(h => h.name == "RotatingBlades" + (i + 1));
                    var position = rotor.transform.position;
                    position.z = gate.transform.position.z + 5f;
                    rotor.transform.position = position;
                    WidenGate(gate);
                }
                foreach (var saw in saws)
                {
                    saw.ConfigureAcrossMap(saw.Pole, saw.Hazard, -SawHalfWidth, SawHalfWidth, 6f);
                    saw.Hazard.ConfigureHorizontalSweep(Vector3.right * SawHalfWidth, 6f);
                    EditorUtility.SetDirty(saw);
                }
                foreach (var hazard in hazards)
                {
                    if (hazard.Kind != CannonHazardKind.SpikeMace && hazard.GetComponent<HumanCannonPoleSaw>() == null)
                    {
                        float direction = hazard.Travel.x < 0 ? -1f : 1f;
                        hazard.ConfigureHorizontalSweep(Vector3.right * (direction * SawHalfWidth), hazard.PeriodSeconds);
                    }
                    hazard.Present(0, 1);
                    UpdateRail(hazard);
                    EditorUtility.SetDirty(hazard);
                }
                foreach (var fire in All<HumanCannonFireWall>(roots))
                {
                    using var data = new SerializedObject(fire);
                    data.FindProperty("halfWidth").floatValue = FloorHalfWidth + 3f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var wind in All<HumanCannonWindZone>(roots))
                {
                    var size = wind.Area.size; size.x = FloorHalfWidth * 2f - 2f;
                    wind.Area.size = size;
                    wind.PresentFlow(0);
                }
                foreach (var final in All<HumanCannonFinalSaws>(roots))
                {
                    using var data = new SerializedObject(final);
                    data.FindProperty("lateralPasses").boolValue = true;
                    data.FindProperty("horizontalHalfWidth").floatValue = SawHalfWidth;
                    // 왕복 거리가 3배가 되어도 기존 한 주기의 통과 시점을 유지한다.
                    data.FindProperty("horizontalSpeed").floatValue = 6f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    var views = data.FindProperty("horizontalViews");
                    for (int i = 0; i < views.arraySize; i++)
                        ((Transform)views.GetArrayElementAtIndex(i).objectReferenceValue)
                            .SetPositionAndRotation(final.HorizontalPosition(i, 0), final.transform.rotation);
                    foreach (string field in new[] { "verticalView", "warningView", "secondaryVerticalView", "secondaryWarningView" })
                        if (data.FindProperty(field).objectReferenceValue is Transform view)
                            view.SetPositionAndRotation(field.StartsWith("secondary") ? final.SecondaryVerticalPosition(0)
                                : final.VerticalPosition(0), final.transform.rotation);
                }
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("넓은 코스 저장 실패");
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static void ResizeWorldX(BoxCollider box, float left, float right)
        {
            Bounds bounds = box.bounds;
            var scale = box.transform.localScale;
            scale.x *= (right - left) / bounds.size.x;
            box.transform.localScale = scale;
            box.transform.position += Vector3.right * ((left + right) * .5f - bounds.center.x);
        }

        static void WidenGate(HumanCannonSawGate gate)
        {
            ResizeWorldX(gate.transform.Find("Barrier").GetComponent<BoxCollider>(), -FloorHalfWidth, FloorHalfWidth);
            var visual = gate.transform.Find("SawVisual");
            var template = visual.Find("GateSawDisc0");
            for (int i = 0; i < 19; i++)
            {
                var disc = visual.Find("GateSawDisc" + i);
                if (disc == null)
                {
                    disc = Object.Instantiate(template, visual, false);
                    disc.name = "GateSawDisc" + i;
                    Undo.RegisterCreatedObjectUndo(disc.gameObject, "넓어진 관문 톱");
                }
                disc.localPosition = new Vector3((i - 9) * 2.4f, 0, 0);
            }
        }

        static void UpdateRail(HumanCannonHazard hazard)
        {
            if (!hazard.TryGetComponent(out LineRenderer line)) return;
            line.positionCount = 2;
            var start = hazard.transform.position - hazard.Travel;
            var end = hazard.transform.position + hazard.Travel;
            line.SetPosition(0, line.useWorldSpace ? start : line.transform.InverseTransformPoint(start));
            line.SetPosition(1, line.useWorldSpace ? end : line.transform.InverseTransformPoint(end));
        }

        static T[] All<T>(GameObject[] roots) where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
