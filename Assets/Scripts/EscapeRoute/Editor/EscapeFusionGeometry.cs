using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void BuildFusionTransferSpace(Transform p, GameObject trampolineVisual)
        {
            var lower = VerticalChild(p, "01_LowerServicePocket");
            BFloor(lower, "반동 준비 작업대", 93, 101, 24, 32, -4.6f, 1.2f);
            BFloor(lower, "계단 하부 연결", 98.05f, 101, 21.4f, 24, -4.6f, 1.2f);
            // A separate service approach lets teammates reach the receiving area before the rope user.
            for (int i = 0; i < 23; i++)
                BFloor(lower, "하부 접근 계단 " + i, 90 + i * .35f, 90 + (i + 1) * .35f,
                    21.4f, 24, -(i + 1) * .2f, .35f);
            foreach (float x in new[] { 94f, 96f })
                BMass(lower, "기존 전단벽에 물린 캔틸레버 보", new Vector3(x, -6.55f, 25), new Vector3(.7f, 1.5f, 14), bRecess, false);
            VerticalStrut(lower, "계단 하부 경사 받침", new Vector3(90, -1.0f, 22.7f), new Vector3(98.05f, -5.6f, 22.7f), .65f, bConcrete);
            RevealRail(lower, new Vector3(93, -4.6f, 32), new Vector3(101, -4.6f, 32), bSteel);
            RevealRail(lower, new Vector3(93, -4.6f, 25), new Vector3(93, -4.6f, 31), bSteel);
            VerticalCable(lower, "하부 계단 손잡이", new Vector3(90, 1, 21.4f), new Vector3(98.05f, -3.6f, 21.4f), .06f, bSteel);

            var rope = VerticalChild(p, "02_RopeAndBrokenServiceEnd");
            FusionRope(rope, "05 외줄", new Vector3(89, .05f, 28), new Vector3(96.2f, .05f, 28));
            BFloor(rope, "끊긴 정비대 끝", 95.5f, 96.9f, 27, 29, 0, .25f);
            foreach (float z in new[] { 26.9f, 29.1f })
                BMass(rope, "정비대 받침", new Vector3(96, -2.4f, z), new Vector3(.18f, 4.3f, .18f), bSteel, false);
            FusionSocket(rope, new Vector3(88.5f, 0, 28));
            FusionSocket(rope, new Vector3(96.1f, 0, 28.7f));
            // A torn continuation hangs down at the side; the forward drop remains open and visible.
            VerticalCable(rope, "끊어진 후속 와이어", new Vector3(96.7f, -.05f, 29), new Vector3(97, -1.8f, 29.2f), .055f, bSteel);
            FusionFloorMarks(rope, new Vector3(87.2f, .02f, 28), Vector3.right);

            var rebound = VerticalChild(p, "03_ReboundVisual_LogicPending");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(trampolineVisual, rebound);
            Undo.RegisterCreatedObjectUndo(visual, "Existing trampoline visual");
            visual.name = "Trampoline_VisualOnly";
            visual.transform.position = new Vector3(98, -4.6f, 28);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            // Keep the shared prop at its authored size. This is not a bounce collider or runtime system.
            BLight(rebound, new Vector3(94.3f, -1, 25.2f), new Vector3(98, -4.1f, 28), 200, 12);
            VerticalStrut(rebound, "작업등 지주", new Vector3(94.3f, -4.6f, 25.2f), new Vector3(94.3f, -.9f, 25.2f), .08f, bSteel);

            var swing = VerticalChild(p, "04_DescendingSwing_LogicPending");
            var ochre = AssetDatabase.LoadAssetAtPath<Material>(Materials + "working-faded-ochre.mat");
            for (int i = 0; i < 3; i++)
            {
                float x = 101 + i * 6, y = 1 - i;
                foreach (float z in new[] { 27f, 29f })
                {
                    VerticalCable(swing, "그네 " + (i + 1) + " 현수선", new Vector3(x, 19.5f, z), new Vector3(x, y, z), .065f, bSteel);
                    BMass(swing, "그네 천장 고정판", new Vector3(x, 19.43f, z), new Vector3(.6f, .14f, .6f), bSteel, false);
                }
                VerticalCable(swing, "그네 " + (i + 1) + " 손잡이", new Vector3(x, y, 27), new Vector3(x, y, 29), .12f, bSteel);
                VerticalCable(swing, "그네 " + (i + 1) + " 손잡이 도색", new Vector3(x, y, 27.5f), new Vector3(x, y, 28.5f), .125f, ochre);
            }
            FusionFloorMarks(p, new Vector3(118, -7.98f, 28), new Vector3(1, 0, 2));
            // Landing and the turn towards the existing ascent stay broad; do not add another obstacle here.
            BLight(p, new Vector3(118, -3, 32), new Vector3(119, -8, 28), 180, 14);
            VerticalStrut(p, "착지 작업등 지주", new Vector3(118, -8, 32), new Vector3(118, -2.9f, 32), .09f, bSteel);
        }

        static void FusionRope(Transform p, string name, Vector3 start, Vector3 end)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector3 At(float t) => Vector3.Lerp(start, end, t) - Vector3.up * (.08f * Mathf.Sin(t * Mathf.PI));
                VerticalCable(p, name + " 시각선 " + i, At(i / 12f), At((i + 1) / 12f), .075f, bSteel);
            }
        }

        static void FusionSocket(Transform p, Vector3 feet)
        {
            BMass(p, "외줄 고정 베이스", feet + Vector3.up * .045f, new Vector3(.35f, .09f, .35f), bSteel, false);
        }

        static void FusionFloorMarks(Transform p, Vector3 feet, Vector3 forward)
        {
            var paint = AssetDatabase.LoadAssetAtPath<Material>(Materials + "working-faded-ochre.mat");
            var direction = forward.normalized;
            for (int i = 0; i < 3; i++)
            {
                var mark = RevealBlock(p, "닳은 진행 도색", feet + direction * (.27f * i), new Vector3(.06f, .008f, .6f), paint, false, false);
                mark.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 90, 0);
            }
        }
    }
}
