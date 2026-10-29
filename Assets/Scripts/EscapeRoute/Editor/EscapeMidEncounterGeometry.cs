using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void BuildMidWaitingPocket(Transform p, Transform background)
        {
            var depth = background.Find(DepthGroup);
            foreach (var t in depth.GetComponentsInChildren<Transform>().Where(t =>
                Mathf.Abs(t.position.x - 65) < .05f &&
                (t.name == "거친 갤러리 물막이" || t.name == "연속 갤러리 손잡이" || t.name == "연속 갤러리 중간대"
                || t.name == "갤러리 난간 기둥" && t.position.z >= -27.1f && t.position.z <= -20.9f)))
                PreserveDepthObject(t.gameObject);
            // Keep the old posts outside the opening and replace only the continuous lengths.
            MidPocketRail(p, new Vector3(65, -22, 5), new Vector3(65, -22, -21), false);
            MidPocketRail(p, new Vector3(65, -22, -27), new Vector3(65, -22, -61), false);
            DepthDeck(p, "톱날 사이 대기 포켓", 61, 65, -28, -20, -22);
            BMass(p, "대기 포켓 하부 받침", new Vector3(63, -27, -24), new Vector3(3, 8, 5), bRecess);
            foreach (float z in new[] { -20.5f, -27.5f })
                BMass(p, "포켓 측면 방호벽", new Vector3(62.7f, -20.5f, z), new Vector3(3.4f, 3, .5f));
            MidPocketRail(p, new Vector3(61, -22, -27.5f), new Vector3(61, -22, -20.5f));
            BLight(p, new Vector3(62, -18, -24), new Vector3(63, -22, -24), 180, 10);
        }

        static void MidPocketRail(Transform p, Vector3 a, Vector3 b, bool posts = true)
        {
            var span = b - a;
            BMass(p, "포켓 접합 물막이", (a + b) * .5f + Vector3.up * .175f,
                new Vector3(Mathf.Abs(span.x) + .2f, .35f, Mathf.Abs(span.z) + .2f));
            foreach (float y in new[] { .6f, 1.1f })
                VerticalCable(p, "포켓 연속 난간", a + Vector3.up * y, b + Vector3.up * y, .06f, wornSteel);
            int count = posts ? Mathf.CeilToInt(span.magnitude / 3) : 1;
            for (int i = 0; i <= count; i++)
            {
                var at = Vector3.Lerp(a, b, i / (float)count);
                VerticalCable(p, "포켓 난간 기둥", at, at + Vector3.up * 1.1f, .06f, wornSteel);
            }
        }

        static void BuildMidLowDuct(Transform p)
        {
            // The original platform control scheme already supports crouch; no alternate input path.
            BMass(p, "낮게 내려앉은 설비 덕트", new Vector3(110, -19.88f, -32.5f), new Vector3(8, 2, 7), bRecess);
            foreach (float x in new[] { 106f, 114f })
                BMass(p, "덕트 벽체 지지", new Vector3(x, -19.5f, -32.5f), new Vector3(.8f, 5, 7.5f), wornSteel);
            foreach (float z in new[] { -36.02f, -28.98f })
            {
                BMass(p, "낮은 통과 높이 도색", new Vector3(110, -20.8f, z), new Vector3(6, .14f, .025f), maintenancePaint, false);
                for (int i = 0; i < 9; i++)
                    BMass(p, "설비 경고 줄무늬", new Vector3(107.6f + i * .6f, -20.5f, z), new Vector3(.25f, .4f, .03f), maintenancePaint, false);
            }
            BLight(p, new Vector3(115, -18, -39), new Vector3(110, -21, -36), 220, 16);
            MidSafe(p, "BeforeCrouch", new Vector3(110, -22, -39));
            MidSafe(p, "AfterCrouch", new Vector3(110, -22, -26));
        }

        [MenuItem("Tools/CurtainCall/탈출로/A 중반 톱날과 대기 포켓 보기")]
        public static void ViewMidSaws() => ViewAFusion(new Vector3(68, -20.5f, -9), new Vector3(66, -21, -24));
        [MenuItem("Tools/CurtainCall/탈출로/A 중반 낙하 압착기 보기")]
        public static void ViewMidPress() => ViewAFusion(new Vector3(75, -20.5f, -58), new Vector3(91, -15, -58));
        [MenuItem("Tools/CurtainCall/탈출로/A 북측 회피 화염 보기")]
        public static void ViewMidVents() => ViewAFusion(new Vector3(122.5f, -6.5f, 58), new Vector3(122.5f, -7, 79));
    }
}
