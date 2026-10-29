using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void BuildDepthPumpHall(Transform p)
        {
            BWallZ(p, 56.5f, -68, 6, -86, 36, 3);
            BWallZ(p, 120, -68, 6, -86, 36, 4);
            BWallX(p, -68, 56.5f, 120, -86, 36, 4);
            BMass(p, "펌프동 압축 덮개", new Vector3(88, 38, -31), new Vector3(66, 4, 76));
            BFloor(p, "86m 아래 집수조", 58, 118, -66, 6, -86, 3);
            // A single weighty plant core hides the return station until the far side is reached.
            BMass(p, "수직 펌프 콘크리트 코어", new Vector3(88.5f, -28, -28), new Vector3(21, 116, 32));
            foreach (float z in new[] { -17f, -39f })
            {
                BMass(p, "코어 외측 전단벽", new Vector3(89, -25, z), new Vector3(27, 122, 3));
                BMass(p, "펌프동 상부 횡보", new Vector3(88, 23, z), new Vector3(64, 7, 5));
            }
            DepthDeck(p, "서측 긴 하부 갤러리", 65, 71, -61, 8.5f, -22);
            DepthDeck(p, "집수조 남측 횡단", 71, 113, -61, -55, -22);
            DepthDeck(p, "코어 뒤 동측 회랑", 107, 113, -55, -1.5f, -22);
            DepthDeck(p, "대포 정비대로 복귀", 89, 107, -6.5f, -1.5f, -22);
            foreach (float z in new[] { -7f, -29f, -51f })
            {
                BMass(p, "서측 갤러리 교각", new Vector3(68, -55, z), new Vector3(3, 63, 4), bRecess);
                BMass(p, "동측 갤러리 교각", new Vector3(110, -55, z), new Vector3(3, 63, 4), bRecess);
                BLight(p, new Vector3(62, -16, z - 3), new Vector3(68, -22, z - 4), 380, 23);
                BLight(p, new Vector3(116, -16, z + 3), new Vector3(110, -22, z + 4), 380, 23);
            }
            foreach (float x in new[] { 76f, 91f, 106f })
                BLight(p, new Vector3(x, -16, -64), new Vector3(x + 3, -22, -58), 330, 21);
            foreach (float y in new[] { 13f, -44f, -66f })
            {
                BMass(p, "깊이 속 반복 정비층", new Vector3(87, y - .5f, -64), new Vector3(52, 1, 2.5f), bRecess, false);
                BMass(p, "먼 점검문", new Vector3(102, y + 1.1f, -65.8f), new Vector3(1.1f, 2.2f, .12f), bSteel, false);
                BLight(p, new Vector3(102, y + 3, -64), new Vector3(100, y, -61), 200, 19);
            }
            foreach (float x in new[] { 80f, 97f })
            {
                VerticalCylinder(p, "거대 펌프 수직 압력관", new Vector3(x, -23, -47), 2.4f, 113, bSteel);
                foreach (float y in new[] { -72f, -48f, -24f, 0f, 24f })
                    VerticalCylinder(p, "압력관 플랜지", new Vector3(x, y, -47), 3.1f, .5f, bSteel);
            }
            BLight(p, new Vector3(61, 7, -54), new Vector3(88, 16, -46), 1800, 65);
            BLight(p, new Vector3(116, 9, -9), new Vector3(100, -16, -28), 1600, 65);
            BLight(p, new Vector3(86, -42, -60), new Vector3(88, -81, -49), 900, 55);
            DepthRail(p, new Vector3(65, -22, 5), new Vector3(65, -22, -61));
            DepthRail(p, new Vector3(71, -22, -12), new Vector3(71, -22, -51));
            DepthRail(p, new Vector3(65, -22, -61), new Vector3(113, -22, -61));
            DepthRail(p, new Vector3(74, -22, -55), new Vector3(103, -22, -55));
            DepthRail(p, new Vector3(113, -22, -61), new Vector3(113, -22, -1.5f));
            DepthRail(p, new Vector3(107, -22, -51), new Vector3(107, -22, -10));
            DepthRail(p, new Vector3(89, -22, -1.5f), new Vector3(113, -22, -1.5f));
            foreach (var mark in new[] { new Vector3(68, -21.98f, -4), new Vector3(68, -21.98f, -46) }) FusionFloorMarks(p, mark, Vector3.back);
            FusionFloorMarks(p, new Vector3(74, -21.98f, -58), Vector3.right);
            FusionFloorMarks(p, new Vector3(110, -21.98f, -49), Vector3.forward);
            FusionFloorMarks(p, new Vector3(101, -21.98f, -4), Vector3.left);
        }

        static void DepthRail(Transform p, Vector3 start, Vector3 end)
        {
            RevealRail(p, start, end, bSteel);
            // Half-height concrete toes explain the maintenance galleries without obscuring the abyss.
            Vector3 size = new Vector3(Mathf.Abs(end.x - start.x) + .2f, .35f, Mathf.Abs(end.z - start.z) + .2f);
            BMass(p, "거친 갤러리 물막이", (start + end) * .5f + Vector3.up * .175f, size, bConcrete);
        }

        static void BuildDepthRegroup(Transform p)
        {
            BWallZ(p, 114, 45, 120, -55, 34, 3);
            BWallZ(p, 141, 45, 68, -55, 34, 3);
            BWallZ(p, 141, 85, 120, -55, 34, 3);
            BWallZ(p, 141, 68, 85, -55, 0, 3);
            BWallZ(p, 141, 68, 85, 8, 34, 3);
            BWallX(p, 120, 114, 142.5f, -55, 34, 3);
            BMass(p, "북측 설비동 덮개", new Vector3(128, 35.5f, 83), new Vector3(30, 3, 77));
            BFloor(p, "북측 55m 하부 집수", 115.5f, 139.5f, 45, 118.5f, -55, 3);
            BMass(p, "길을 가리는 길고 두꺼운 하중 코어", new Vector3(128.5f, -11, 76), new Vector3(5, 86, 52));
            DepthDeck(p, "A05 하부 연결", 115, 127.5f, 45, 49, -8);
            DepthDeck(p, "북측 하부 정비 갤러리", 120, 125, 49, 115, -8);
            DepthDeck(p, "코어 끝 횡단 참", 125, 137.5f, 110, 115, -8);
            DepthStairs(p, "북측 상행", 135, 110, -8, 40, false);
            DepthDeck(p, "반출동을 향한 상부 회랑", 132.5f, 137.5f, 74, 96, 0);
            DepthDeck(p, "A07 연결 발판", 137.5f, 142.5f, 74, 82, 0);
            foreach (float z in new[] { 55f, 77f, 99f, 113f })
            {
                BLight(p, new Vector3(116, -2, z), new Vector3(122.5f, -8, z + 2), 330, 21);
                BMass(p, "갤러리 하부 벽체 받침", new Vector3(122.5f, -32, z), new Vector3(3, 46, 3), bRecess);
            }
            BLight(p, new Vector3(139, -1, 114), new Vector3(132, -8, 112), 360, 21);
            BLight(p, new Vector3(139, 6, 101), new Vector3(135, -3, 103), 350, 22);
            BLight(p, new Vector3(139, 5, 86), new Vector3(135, 0, 81), 300, 24);
            BLight(p, new Vector3(117, 23, 90), new Vector3(129, 15, 104), 1000, 50);
            BLight(p, new Vector3(137, -22, 110), new Vector3(124, -48, 99), 500, 38);
            DepthRail(p, new Vector3(120, -8, 50), new Vector3(120, -8, 115));
            DepthRail(p, new Vector3(125, -8, 52), new Vector3(125, -8, 106));
            DepthRail(p, new Vector3(120, -8, 115), new Vector3(137.5f, -8, 115));
            DepthRail(p, new Vector3(132.5f, 0, 75), new Vector3(132.5f, 0, 94));
            FusionFloorMarks(p, new Vector3(122.5f, -7.98f, 51), Vector3.forward);
            FusionFloorMarks(p, new Vector3(129, -7.98f, 112), Vector3.right);
            FusionFloorMarks(p, new Vector3(135, .02f, 90), Vector3.back);
            FusionFloorMarks(p, new Vector3(137, .02f, 78), Vector3.right);
        }
    }
}
