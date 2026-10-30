using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void BrutalSwing(Transform p)
        {
            // A deep transfer chamber. The 25m stunt gap remains entirely empty at route height.
            BFloor(p, "트램폴린 진입 대", 77.5f, 90, 17.5f, 34, 0, 3);
            BFloor(p, "8m 아래 도착 정비대", 115, 127.5f, 15, 45, -8, 3);
            BWallX(p, 6, 76, 129, -100, 48, 3);
            BWallZ(p, 129, 7.5f, 45, -100, 48, 3);
            BWallZ(p, 76, 7.5f, 22, -100, 48, 3);
            BWallZ(p, 76, 35, 45, -100, 48, 3);
            BWallZ(p, 76, 22, 35, -100, 0, 3); BWallZ(p, 76, 22, 35, 5, 48, 3);
            BWallX(p, 46.5f, 77.5f, 115, -100, 48, 3);
            BWallX(p, 46.5f, 115, 127.5f, -100, -8, 3);
            BWallX(p, 46.5f, 115, 127.5f, 6, 48, 3);
            BMass(p, "공동의 육중한 덮개", new Vector3(102.5f, 50, 26), new Vector3(54, 4, 41));
            BFloor(p, "100m 깊이의 하부", 77.5f, 127.5f, 7.5f, 45, -100, 3);
            foreach (float x in new[] { 94f, 112f })
            {
                BMass(p, "남측 거대 전단벽", new Vector3(x, -25, 13), new Vector3(5, 146, 11));
                BMass(p, "북측 거대 전단벽", new Vector3(x + 4, -25, 40), new Vector3(5, 146, 10));
            }
            BMass(p, "횡단 상부 거대 전이 보", new Vector3(102.5f, 23, 26), new Vector3(50, 7, 9));
            BMass(p, "후경 상부 전이 보", new Vector3(103, 38, 39), new Vector3(50, 6, 7));
            BMass(p, "트램폴린 설치 자리", new Vector3(86.25f, .25f, 25), new Vector3(4, .5f, 4), bSteel);
            for (int i = 0; i < 3; i++)
            {
                float x = 95 + i * 9, y = 5 - i * 2;
                foreach (float z in new[] { 24f, 26f }) VerticalCable(p, "그네 설치 지지선", new Vector3(x, 19.5f, z), new Vector3(x, y, z), .06f, bSteel);
                BMass(p, "그네 설치 가이드", new Vector3(x, y, 25), new Vector3(.14f, .14f, 2), bSteel, false);
            }
            foreach (float y in new[] { -24f, -54f, -82f })
            {
                BMass(p, "하부 콘크리트 점검층", new Vector3(102.5f, y - .8f, 18), new Vector3(44, 1.6f, 3), bRecess, false);
                BMass(p, "점검층 두꺼운 흉벽", new Vector3(102.5f, y + .55f, 19.3f), new Vector3(44, 1.1f, .4f), bConcrete, false);
                BMass(p, "작은 점검문", new Vector3(88, y + 1.1f, 16.4f), new Vector3(1, 2.2f, .1f), bSteel, false);
                BLight(p, new Vector3(88, y + 3, 17), new Vector3(88, y, 20), 170, 18);
            }
            foreach (float x in new[] { 84f, 121f })
                BMass(p, "횡단 발판 하부 교각", new Vector3(x, -55, 26), new Vector3(5, 88, 6));
            BLight(p, new Vector3(82, 9, 18), new Vector3(86, 0, 25), 550, 24);
            BLight(p, new Vector3(121, 3, 34), new Vector3(120, -8, 28), 600, 26);
            BLight(p, new Vector3(81, 35, 29), new Vector3(98, 20, 39), 2200, 60);
            BLight(p, new Vector3(124, 32, 20), new Vector3(94, 10, 13), 1700, 57);
            BLight(p, new Vector3(122, -25, 33), new Vector3(100, -65, 18), 1200, 65);
            BMass(p, "진입측 짧은 흉벽", new Vector3(83, .6f, 17.5f), new Vector3(11, 1.2f, .5f));
            BMass(p, "도착측 흉벽", new Vector3(121, -7.4f, 15), new Vector3(12, 1.2f, .5f));
        }

        static void BrutalRegroup(Transform p)
        {
            BFloor(p, "하부 재합류", 115, 127.5f, 45, 50, -8, 2);
            for (int i = 0; i < 40; i++)
                BFloor(p, "시설 상행 계단 " + i, 119, 127, 50 + i * .35f, 50 + (i + 1) * .35f, -8 + (i + 1) * .2f, .4f);
            BFloor(p, "상부 재합류 플랫폼", 115, 140, 64, 87.5f, 0, 3);
            BWallZ(p, 114, 45, 87.5f, -45, 34); BWallZ(p, 141, 45, 67.5f, -45, 34);
            BWallX(p, 88.5f, 115, 140, -45, 34);
            BFloor(p, "45m 하부 설비 바닥", 115, 140, 45, 87.5f, -45);
            BMass(p, "재합류 상부 덮개", new Vector3(127.5f, 35, 66.25f), new Vector3(29, 4, 46.5f));
            BMass(p, "계단 측면 거대 벽체", new Vector3(134, -5, 55), new Vector3(9, 58, 16));
            foreach (float x in new[] { 119.3f, 126.7f }) VerticalStrut(p, "계단 콘크리트 하부 보", new Vector3(x, -9, 50), new Vector3(x, -1, 64), .8f, bConcrete);
            VerticalCable(p, "계단 내측 손잡이", new Vector3(119, -6.9f, 50), new Vector3(119, 1.1f, 64), .07f, bSteel);
            for (int i = 0; i <= 10; i++) VerticalCable(p, "계단 난간 지지", new Vector3(119, -8 + i * .8f, 50 + i * 1.4f), new Vector3(119, -6.9f + i * .8f, 50 + i * 1.4f), .06f, bSteel);
            BMass(p, "상부 길을 가리는 매달린 덩어리", new Vector3(124, 15, 76), new Vector3(18, 20, 9));
            BLight(p, new Vector3(127, 4, 61), new Vector3(123, -5, 55), 350, 25);
            BLight(p, new Vector3(116, 4.4f, 70), new Vector3(125, 0, 75), 220, 25);
            BLight(p, new Vector3(137, 5, 83), new Vector3(144, 0, 78), 250, 20);
            BLight(p, new Vector3(116, -19, 58), new Vector3(117, -40, 57), 200, 27);
            ContextSign(p, "반출 구역 방향", "DISPATCH  >", new Vector3(127, 2.4f, 87.4f), 0, 3);
        }

        static void BrutalRun(Transform p)
        {
            BFloor(p, "긴 반출 트렌치", 140, 195, 71, 84, 0, 2.5f);
            BFloor(p, "마지막 남하 굽이", 175, 195, 37.5f, 71, 0, 2.5f);
            BWallX(p, 89, 140, 197, -32, 36, 3);
            BWallX(p, 66, 140, 175, -32, 36, 3);
            BWallZ(p, 196.5f, 37.5f, 87.5f, -32, 36, 3);
            BWallZ(p, 173.5f, 37.5f, 67.5f, -32, 36, 3);
            BFloor(p, "반출 트렌치 바닥", 140, 195, 67.5f, 87.5f, -32);
            BMass(p, "반출동 두꺼운 덮개", new Vector3(168, 37, 77.5f), new Vector3(59, 4, 24));
            BMass(p, "굽이 상부 덮개", new Vector3(185, 37, 52), new Vector3(24, 4, 32));
            foreach (float x in new[] { 149f, 169f, 189f })
            {
                BMass(p, "반출동 북측 하중벽", new Vector3(x, 13, 85), new Vector3(5, 46, 6));
                BMass(p, "진행 상부 거대 보", new Vector3(x, 14, 77.5f), new Vector3(5, 10, 21));
                BLight(p, new Vector3(x + 1, 7, 83), new Vector3(x + 5, 0, 77.5f), 330, 26);
            }
            BMass(p, "끝에서 시야를 꺾는 벽", new Vector3(193, 9, 83), new Vector3(4, 18, 9));
            BPortalZ(p, 51, 180, 190, 4.5f, 24, 6);
            BLight(p, new Vector3(191, 6, 60), new Vector3(185, 0, 50), 250, 24);
            BLight(p, new Vector3(180, 4, 43), new Vector3(185, 0, 36), 160, 18);
            foreach (float x in new[] { 151f, 176f })
            {
                BMass(p, "하부 반출 측면 선반", new Vector3(x, -18, 86), new Vector3(15, 1.5f, 2), bRecess, false);
                BLight(p, new Vector3(x, -14, 86), new Vector3(x + 2, -20, 84), 100, 13);
            }
        }

        static void BrutalExit(Transform p)
        {
            BFloor(p, "최종 반출실 바닥", 175, 210, 0, 37.5f, 0, 3);
            BWallX(p, -1.5f, 173.5f, 211.5f, -24, 34, 3);
            BWallZ(p, 173.5f, 0, 37.5f, -24, 34, 3);
            BWallX(p, 39, 195, 210, -24, 34, 3);
            BMass(p, "반출실 두꺼운 천장", new Vector3(192.5f, 35, 18.75f), new Vector3(38, 4, 40.5f));
            foreach (float x in new[] { 198f, 207f })
            {
                BMass(p, "외벽 방벽 남측", new Vector3(x, 16, 5.5f), new Vector3(6, 36, 14));
                BMass(p, "외벽 방벽 북측", new Vector3(x, 16, 30), new Vector3(6, 36, 15));
                BMass(p, "외벽 방벽 인방", new Vector3(x, 20, 17.5f), new Vector3(6, 27, 10));
                BLight(p, new Vector3(x - 3.1f, 5, 21.5f), new Vector3(x - 7, 0, 17.5f), 170, 18);
            }
            BWallZ(p, 211, 0, 12.5f, -24, 34); BWallZ(p, 211, 22.5f, 37.5f, -24, 34);
            BWallZ(p, 211, 12.5f, 22.5f, 6.5f, 34);
            BMass(p, "출구 잠금 장치 설치 자리", new Vector3(196, .15f, 27), new Vector3(5, .3f, 5), bSteel);
            BMass(p, "출구 측 콘크리트 작업대", new Vector3(181, 3, 8), new Vector3(7, 6, 12));
            BLight(p, new Vector3(177, 11, 28), new Vector3(186, 0, 27), 450, 30);
            BLight(p, new Vector3(191, 20, 4), new Vector3(180, 8, 12), 600, 30);
            ContextSign(p, "외벽 반출구", "OUT  /  08", new Vector3(194.9f, 3, 24), 90, 2.5f);
        }
    }
}
