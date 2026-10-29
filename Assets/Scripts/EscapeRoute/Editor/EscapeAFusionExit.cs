using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void BuildAFusionCoopAndAscent(Transform root, Transform bg, HumanCannonPole source, List<BoxCollider> ropes)
        {
            if (root == null || bg == null || source == null || ropes == null)
                throw new ArgumentException("The existing A route, original swing and rope collection are required.");
            CompletionMaterials();
            var coop = bg.Find("32_A_CooperativeRopes");
            var north = bg.Find(DepthGroup + "/06_NorthServiceLoop");
            if (coop == null || north == null)
                throw new InvalidOperationException("A cooperative ropes and the north service loop must already exist.");

            var fifth = VerticalChild(root, "05_CooperativeReboundAndExit");
            BuildAFusionCooperativeExit(fifth, bg, coop, source, ropes);
            var sixth = VerticalChild(root, "06_FlamesSwingCannonAndUpperWire");
            BuildAFusionNorthExit(sixth, bg, north, source, ropes);
        }

        static void BuildAFusionCooperativeExit(Transform p, Transform bg, Transform coop, HumanCannonPole source, List<BoxCollider> ropes)
        {
            PreserveFusionGuide(coop.Find("상부 진입 정비대"));
            PreserveFusionGuide(coop.Find("하강 그네 넓은 착지턱"));
            foreach (var part in coop.Cast<Transform>().ToArray())
                if (part.name.StartsWith("정비대", StringComparison.Ordinal)
                    && (Mathf.Abs(part.position.x - 80) < .1f
                        || part.position.x >= 117 && part.position.z >= 25 && part.position.z <= 31))
                    PreserveFusionGuide(part);

            // Preserve all four timed ropes, the carried trampoline, button and gated return stairs.
            // Feedback: close descending swings could be skipped. These two swings have separate jobs:
            // trampoline-to-button recovery, then a diagonal exit beyond the first swing's flight range.
            foreach (var pole in bg.GetComponentsInChildren<HumanCannonPole>(true).Where(pole =>
                pole.Grip != null && pole.Grip.position.x >= 105 && pole.Grip.position.x <= 117
                && Mathf.Abs(pole.Grip.position.z - 28) < 1).ToArray())
                PreserveAFusionPole(bg, pole);

            var oldTransfer = bg.Find(BrutalGroup + "/05_TransferChasm");
            if (oldTransfer == null) throw new InvalidOperationException("A05 concrete shell is missing.");
            PreserveFusionGuide(oldTransfer.Find("8m 아래 도착 정비대"));
            PreserveFusionGuide(oldTransfer.Find("도착측 흉벽"));

            AFusionLandingFloor(p, "영구 진입 외줄 도착 정비대", 82, 88, 20, 33, 0);
            AFusionLandingRope(p, "A05_PermanentEntryWire", new Vector3(85, 0, 15), new Vector3(85, 0, 20), ropes);
            // The button deck reaches X105/Z31 and the retained return corridor X106/Z30..43.5.
            // Only fill their outer edge so the X104..106.5/Z29..35 handoff has no doubled faces.
            AFusionLandingFloor(p, "버튼 출구대 측면 이음", 105, 106.5f, 29, 30, 0);
            AFusionLandingFloor(p, "버튼에서 출구 그네로", 106, 106.5f, 30, 35, 0);

            AFusionPole(p, source, "A05_ReboundToButton", new Vector3(98.5f, 1.8f, 25), Vector3.right, 19.5f);
            AFusionPole(p, source, "A05_ButtonExitSwing", new Vector3(108, 1.4f, 35), new Vector3(11, 0, 10).normalized, 19.5f);

            // The old full-height pier occupies X113.5..118.5/Z35..45, directly in the exit arc.
            // Keep its mass above/below the flight and its south jamb, with capsule/camera clearance.
            var pier = oldTransfer.Cast<Transform>().SingleOrDefault(t =>
                t.name == "북측 거대 전단벽" && Mathf.Abs(t.position.x - 116) < .1f);
            if (pier == null) throw new InvalidOperationException("A05 exit pier was not found at X116.");
            PreserveFusionGuide(pier);
            BMass(p, "출구 전단벽 하부", new Vector3(116, -54.5f, 40), new Vector3(5, 87, 10));
            BMass(p, "출구 전단벽 상부", new Vector3(116, 29, 40), new Vector3(5, 38, 10));
            BMass(p, "출구 전단벽 남측 잼", new Vector3(116, -.5f, 36), new Vector3(5, 21, 2));

            // Meets the retained A05-to-A06 slab at Z45 without laying a second face over it.
            AFusionLandingFloor(p, "하강 출구 착지턱", 117.5f, 122.5f, 42, 45, -8);
            var connection = bg.Find(DepthGroup + "/06_NorthServiceLoop/A05 하부 연결");
            if (connection != null) Ensure<HumanCannonLandingZone>(connection.gameObject);
            FusionFloorMarks(p, new Vector3(105.8f, .02f, 33), new Vector3(2.2f, 0, 2));
            FusionFloorMarks(p, new Vector3(119.5f, -7.98f, 44), Vector3.forward);
            BLight(p, new Vector3(106, 7, 32), new Vector3(108, 1, 35), 320, 20);
            BLight(p, new Vector3(123, -1, 44), new Vector3(119, -8, 44), 350, 19);
        }

        static void BuildAFusionNorthExit(Transform p, Transform bg, Transform north, HumanCannonPole source, List<BoxCollider> ropes)
        {
            foreach (var part in north.Cast<Transform>().ToArray())
                if (part.name == "북측 하부 정비 갤러리" || part.name == "코어 끝 횡단 참"
                    || part.name == "반출동을 향한 상부 회랑" || part.name.StartsWith("북측 상행 ", StringComparison.Ordinal)
                    || part.name == "계단 하부 철근 콘크리트 보" || part.name == "연속 계단 손잡이"
                    || part.name == "거친 갤러리 물막이" || part.name == "1.1m 점검 난간"
                    || part.name == "갤러리 하부 벽체 받침" && part.position.z >= 89
                    || part.name == "닳은 진행 도색" && part.position.z >= 89)
                    PreserveFusionGuide(part);

            // Later finish passes put the former continuous gallery rails in a sibling group.
            var finish = bg.Find(DepthGroup + "/07_ServiceFinish");
            if (finish != null)
                foreach (var part in finish.Cast<Transform>().ToArray())
                    if (part.position.x >= 119 && part.position.z >= 49
                        && (part.name == "연속 갤러리 손잡이" || part.name == "연속 갤러리 중간대" || part.name == "갤러리 난간 기둥"))
                        PreserveFusionGuide(part);

            AFusionLandingFloor(p, "화염 사이 하부 갤러리", 120, 125, 49, 89, -8);
            SpaceEdgeRail(p, new Vector3(120, -8, 50), new Vector3(120, -8, 88));
            SpaceEdgeRail(p, new Vector3(125, -8, 50), new Vector3(125, -8, 88));
            MoveAFusionFlamePairs(bg);

            AFusionPole(p, source, "A06_FlameExitSwing", new Vector3(122.5f, -6.5f, 90.5f), Vector3.forward, 12);
            AFusionLandingFloor(p, "코어 끝 짧은 대포대", 120, 125, 100, 108, -8);
            var target = new Vector3(135, 0, 105);
            var cannon = CompletionCannon(p, "A06_CoreEndAscentCannon", new Vector3(122.5f, -8, 104), target, 2);
            FeedbackAim(cannon, target, 2);
            AFusionLandingRope(p, "A06_UpperReturnWire", new Vector3(135, 0, 106), new Vector3(135, 0, 90), ropes);
            AFusionPole(p, source, "A06_WireExitSwing", new Vector3(135, 1.5f, 89), new Vector3(3, 0, -11).normalized, 18);
            AFusionLandingFloor(p, "반출동 착지 측면 이음", 136, 137.5f, 76, 80, 0);
            var exit = finish != null ? finish.Find("A07 접합 발판") : north.Find("A07 연결 발판");
            if (exit != null) Ensure<HumanCannonLandingZone>(exit.gameObject);

            // Existing core: X126..131, Z50..102. The diagonal launch stays north of Z104;
            // upper wire X135 also stays clear of the core and the east wall beginning at X139.5.
            var core = north.Find("길을 가리는 길고 두꺼운 하중 코어");
            var coreRenderer = core != null ? core.GetComponent<Renderer>() : null;
            if (coreRenderer == null || Mathf.Min(cannon.Muzzle.position.z, target.z) <= coreRenderer.bounds.max.z + 1.25f)
                throw new InvalidOperationException("A06 cannon needs at least 1.25m clearance north of the actual core bounds.");
            FusionFloorMarks(p, new Vector3(122.5f, -7.98f, 87.5f), Vector3.forward);
            FusionFloorMarks(p, new Vector3(122.5f, -7.98f, 102), Vector3.forward);
            BLight(p, new Vector3(117, -1, 92), new Vector3(122.5f, -6, 91), 320, 21);
            BLight(p, new Vector3(136, 7, 109), new Vector3(135, 0, 104), 380, 23);
            BLight(p, new Vector3(138, 7, 86), new Vector3(135, 1, 89), 340, 21);
        }

        static void MoveAFusionFlamePairs(Transform bg)
        {
            var vents = bg.Find(FeedbackGroup + "/SixOpposedFlamePairs");
            if (vents == null) throw new InvalidOperationException("The six existing opposed flame pairs are required.");
            float[] positions = { 58, 62, 70, 74, 82, 86 };
            for (int pair = 0; pair < positions.Length; pair++)
            {
                foreach (string side in new[] { "Left", "Right" })
                {
                    var group = vents.Find("Pair_" + (pair + 1) + "_" + side);
                    var cue = group != null ? group.GetComponentInChildren<HumanCannonHazardCue>(true) : null;
                    if (cue == null) throw new InvalidOperationException("A06 existing flame pair is incomplete.");
                    Vector3 delta = Vector3.forward * (positions[pair] - cue.origin.z);
                    Undo.RecordObject(group, "Move complete original flame device");
                    Undo.RecordObject(cue, "Move original world-space flame cue");
                    group.position += delta;
                    cue.origin += delta;
                    cue.destination += delta;
                    EditorUtility.SetDirty(cue);
                }
            }
            foreach (var part in vents.Cast<Transform>().Where(t => !t.name.StartsWith("Pair_", StringComparison.Ordinal)).ToArray())
            {
                // Housings, recesses, feed pipes and light assemblies are siblings of the device pairs.
                int pair = Mathf.RoundToInt((part.position.z - 60) / 9);
                if (pair < 0 || pair >= positions.Length || Mathf.Abs(part.position.z - (60 + pair * 9)) > .5f)
                    throw new InvalidOperationException("Unrecognised A06 flame mounting position: " + part.name);
                Undo.RecordObject(part, "Move flame mounting with its device");
                part.position += Vector3.forward * (positions[pair] - (60 + pair * 9));
            }
        }

        static void PreserveAFusionPole(Transform bg, HumanCannonPole pole)
        {
            PreserveFusionGuide(pole.transform);
            foreach (var part in bg.GetComponentsInChildren<Transform>(true))
                if (part.name == pole.name + " 천장 매립 하중보" || part.name == pole.name + "_LowerSuspensionBeam"
                    || part.name == pole.name + "_FixedDropRod")
                    PreserveFusionGuide(part);
        }

        static void AFusionPole(Transform p, HumanCannonPole source, string name, Vector3 grip, Vector3 forward, float ceiling)
        {
            var pole = OriginalPole(p, source, name, grip, forward, ceiling);
            TuneFlowPole(p, pole, grip, 5, 38, 7.2f);
        }

        static void AFusionLandingFloor(Transform p, string name, float x1, float x2, float z1, float z2, float y)
        {
            BFloor(p, name, x1, x2, z1, z2, y, 1.2f);
            Ensure<HumanCannonLandingZone>(p.Find(name).gameObject);
        }

        static void AFusionLandingRope(Transform p, string name, Vector3 from, Vector3 to, List<BoxCollider> ropes)
        {
            var rope = CompletionRope(p, name, from, to);
            Ensure<HumanCannonLandingZone>(rope.gameObject);
            ropes.Add(rope);
        }
    }
}
