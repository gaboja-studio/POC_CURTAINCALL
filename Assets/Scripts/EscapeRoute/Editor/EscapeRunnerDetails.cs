using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static void RunnerDischargeBay(Transform p, float x, bool south, string id)
        {
            var bay = VerticalChild(p, "SideDischarge_" + id);
            float z = south ? 61 : 95, endZ = south ? 73 : 83;
            float sign = south ? 1 : -1;
            BMass(bay, "측면 파쇄 반출기 기초", new Vector3(x, 10, z), new Vector3(11, 28, 10), bRecess);
            BMass(bay, "파쇄기 수직 공급 코어", new Vector3(x - 2, 22, z - sign * 2), new Vector3(6, 18, 7));
            // Dark mouth, rails and a chute make the threat's source legible at running speed.
            BMass(bay, "반출기 어두운 토출구", new Vector3(x, 5.4f, z + sign * 5.1f), new Vector3(6.8f, 4.5f, .15f), bSteel, false);
            var start = new Vector3(x, 5.4f, z + sign * 5.2f);
            var end = new Vector3(x, 1.1f, endZ);
            var rotation = Quaternion.LookRotation(end - start);
            BMass(bay, "경사진 반출 슈트", (start + end) / 2, new Vector3(6, .4f, Vector3.Distance(start, end)), wornSteel).transform.localRotation = rotation;
            foreach (float dx in new[] { -3f, 3f })
                BMass(bay, "슈트 측면 접힌 철판", (start + end) / 2 + new Vector3(dx, .5f, 0), new Vector3(.25f, 1.4f, Vector3.Distance(start, end)), wornPipe).transform.localRotation = rotation;
            RevealAsset(bay, "반출 구동 장치", Industrial + "SM_Machinery_03.prefab", new Vector3(x - 6, 0, z + sign * 3), new Vector3(3, 3.4f, 3), wornSteel);
            RevealAsset(bay, "토출부에 걸린 반출 드럼", Factory + "SM_Barrel_1.prefab", end + new Vector3(1.4f, .4f, -sign * 1.5f), new Vector3(1.3f, 1.6f, 1.3f), wornPipe, Quaternion.Euler(22, 13, 67));
            BMass(bay, "배출 슈트에 남은 자재", start + new Vector3(-1.2f, .7f, sign), new Vector3(2, 1.1f, 1.3f)).transform.localRotation = Quaternion.Euler(7, 19, -12);
            ServicePipe(bay, new Vector3(x - 4, 1, z + sign * 5.4f), new Vector3(x - 4, 20, z + sign * 5.4f), .45f);
            WallWear(bay, new Vector3(x, 13, z + sign * 5.03f), new Vector2(8, 19), south ? 180 : 0);
            FloorWear(bay, new Vector3(x + 2, .022f, south ? 73 : 83), new Vector2(7, 4), x);
            RunnerThreshold(bay, x - 5, south ? 71 : 80.5f, south ? 75.5f : 85);
            RunnerThreshold(bay, x + 5, south ? 71 : 80.5f, south ? 75.5f : 85);
            RunnerAnchor(bay, "SideLaunch_" + id, end + Vector3.up, south ? Vector3.forward : Vector3.back);
            ContextSign(bay, "배출 설비 번호 " + id, "DISCHARGE " + id, new Vector3(x, 8.5f, z + sign * 5.2f), south ? 180 : 0, 5);
            BLight(bay, new Vector3(x - 3.5f, 7.5f, z + sign * 6), new Vector3(x, 1, endZ), 360, 20);
        }

        static void RunnerDropLoad(Transform p, Vector3 at, string id)
        {
            var bay = VerticalChild(p, "OverheadDrop_" + id);
            BMass(bay, "고장난 호이스트 콘크리트 지지", new Vector3(at.x, 23, 78), new Vector3(6, 5, 33));
            RevealAsset(bay, "상부 호이스트 레일", Industrial + "SM_Crane_Beam.prefab", new Vector3(at.x, 19, 78), new Vector3(2, 1.5f, 29), wornSteel);
            // Static suspended load indicates the future falling envelope; no timing or damage is simulated.
            BMass(bay, "낙하 예정 중량물 설치 형상", at + Vector3.up * 8, new Vector3(4.5f, 3, 4), bRecess);
            foreach (float dx in new[] { -2f, 2f })
            {
                VerticalCable(bay, "호이스트 인양 케이블", new Vector3(at.x + dx, 20, at.z), new Vector3(at.x + dx, 9.5f, at.z), .08f, wornSteel);
                BMass(bay, "중량물 묶음 밴드", at + new Vector3(dx, 8, 0), new Vector3(.18f, 3.15f, 4.15f), wornSteel, false);
            }
            RunnerThreshold(bay, at.x - 3, at.z - 2.3f, at.z + 2.3f);
            RunnerThreshold(bay, at.x + 3, at.z - 2.3f, at.z + 2.3f);
            RunnerAnchor(bay, "FallOrigin_" + id, at + Vector3.up * 8, Vector3.down);
            RunnerAnchor(bay, "FallImpact_" + id, at, Vector3.right);
            FloorWear(bay, at + Vector3.up * .02f, new Vector2(7, 6), 17);
        }

        static void RunnerThreshold(Transform p, float x, float z1, float z2)
        {
            for (float z = z1 + .25f; z < z2; z += .8f)
                RevealBlock(p, "마모된 위험 경계 도색", new Vector3(x, .017f, z), new Vector3(.32f, .012f, .5f), maintenancePaint, false, false)
                    .transform.localRotation = Quaternion.Euler(0, 24, 0);
        }

        static void RunnerImpact(Transform p, Vector3 at, bool reverse)
        {
            var group = VerticalChild(p, "충돌 흔적과 가장자리 잔해"); group.localPosition = at;
            float sign = reverse ? -1 : 1;
            BMass(group, "이탈한 무거운 콘크리트 조각", new Vector3(0, .65f, 0), new Vector3(3.2f, 1.3f, 2.2f))
                .transform.localRotation = Quaternion.Euler(0, sign * 17, 4);
            for (int i = 0; i < 8; i++)
            {
                float dx = -4 + i * 1.15f;
                var piece = RevealBlock(group, "잘게 깨진 바닥 가장자리", new Vector3(dx, .1f + (i % 3) * .08f, (i % 2) * sign * .8f),
                    new Vector3(.55f + (i % 3) * .2f, .2f + (i % 2) * .15f, .6f), i % 3 == 0 ? patchConcrete : bConcrete, false, true);
                piece.transform.localRotation = Quaternion.Euler(7 * i, 23 * i, 11);
            }
            for (int i = 0; i < 4; i++)
                VerticalCable(group, "절단면에 드러난 철근", new Vector3(-1.5f + i * .7f, .8f, 0), new Vector3(-1 + i * .7f, 1.15f, sign * 1.5f), .035f, wornSteel);
            FloorWear(group, new Vector3(.5f, .026f, 0), new Vector2(10, 4), 13);
        }

        static void RunnerSurfaceLife(Transform p)
        {
            // Uneven service strips and actual working equipment, rather than regularly spaced display props.
            foreach (var span in new[] { new Vector2(143, 165), new Vector2(202, 237), new Vector2(306, 347) })
            {
                CableTray(p, new Vector3(span.x, 3.7f, 88), new Vector3(span.y, 3.7f, 88));
                ServicePipe(p, new Vector3(span.x, 1.6f, 88.8f), new Vector3(span.y, 1.6f, 88.8f), .22f);
            }
            foreach (float x in new[] { 165f, 224f, 310f, 365f })
            {
                BFloor(p, "비켜난 설비 정비대", x, x + 7, 85, 92, 0, 1.8f);
                MaintenanceBay(p, new Vector3(x + 2, 0, 89), x % 2 == 0 ? 15 : -19);
                RevealAsset(p, "정비대 뒤 배전반", Industrial + "SM_CS1_WorkCabinet_01a_c1_Nanite.prefab", new Vector3(x + 4, 0, 90), new Vector3(1.2f, 1.8f, .65f), wornSteel);
            }
            foreach (float x in new[] { 153f, 171f, 189f, 211f, 231f, 267f, 304f, 324f, 345f, 377f })
            {
                float z = x % 3 < 1 ? 73 : 83;
                FloorWear(p, new Vector3(x, .024f, z), new Vector2(12, 3.7f), x * .7f);
                RepairJoint(p, new Vector3(x + 4, .012f, 78), 13.8f, true);
            }
            foreach (float x in new[] { 180f, 236f, 272f, 329f, 360f })
            {
                WallWear(p, new Vector3(x, 14, 57.03f), new Vector2(11, 28), 180);
                WallWear(p, new Vector3(x + 7, 4, 98.97f), new Vector2(9, 26), 0);
                BMass(p, "기초 옆 재타설 패치", new Vector3(x + 2, -8, 98.9f), new Vector3(7, 12, .15f), patchConcrete, false);
            }
            // A broken service branch hanging off the side gives a directed, non-symmetrical silhouette.
            ServicePipe(p, new Vector3(226, 12, 67), new Vector3(250, 12, 67), .8f);
            ServicePipe(p, new Vector3(250, 12, 67), new Vector3(257, 5, 70), .8f);
            for (int cable = 0; cable < 3; cable++)
            {
                var previous = new Vector3(310, 7, 86.5f + cable * .12f);
                for (int segment = 1; segment <= 10; segment++)
                {
                    float t = segment / 10f;
                    var next = new Vector3(310 + 17 * t, 7 - Mathf.Sin(t * Mathf.PI) * (2.2f + cable * .3f), 86.5f + cable * .12f);
                    VerticalCable(p, "늘어진 임시 전력선", previous, next, .04f, wornSteel); previous = next;
                }
            }
            // Small stretches of parapet imply former maintenance safety without fencing the entire escape route.
            foreach (float x in new[] { 145f, 207f, 260f, 310f, 362f })
                BMass(p, "남겨진 짧은 콘크리트 흉벽", new Vector3(x, .55f, 71), new Vector3(6, 1.1f, .4f));
            foreach (float x in new[] { 173f, 206f, 239f, 262f, 325f, 351f })
            {
                // Exposed reinforcement follows the broken outer lip; the running strip remains flat.
                for (int i = 0; i < 5; i++)
                    VerticalCable(p, "깨진 슬래브 끝 철근", new Vector3(x + i * .35f, -.35f, 72.6f), new Vector3(x + i * .35f + .15f, -.8f, 70.2f), .035f, wornSteel);
                FloorWear(p, new Vector3(x, .029f, 74), new Vector2(7, 5), 27);
                for (int i = 0; i < 4; i++)
                    RevealBlock(p, "자재가 끌린 바닥 흠집", new Vector3(x + i * .35f, .009f, 74.6f + i * .12f), new Vector3(4.7f - i * .45f, .006f, .025f), bRecess, false, false)
                        .transform.localRotation = Quaternion.Euler(0, 13, 0);
            }
            var plant = VerticalChild(p, "측면 원동기와 집진 설비");
            BFloor(plant, "원동기 정비 발판", 185, 199, 85, 98, -2, 2);
            RevealAsset(plant, "대형 원동기 본체", Industrial + "SM_Machinery_01.prefab", new Vector3(191, -2, 91), new Vector3(10, 9, 7), wornPipe);
            ServicePipe(plant, new Vector3(188, 5, 93), new Vector3(188, 27, 93), 1.6f);
            ServicePipe(plant, new Vector3(188, 27, 93), new Vector3(213, 27, 93), 1.6f);
            BLight(plant, new Vector3(181, 8, 86), new Vector3(192, 3, 91), 650, 25);
            BFloor(plant, "하부 집진기 점검 대", 240, 259, 59, 69, -7, 2);
            RevealAsset(plant, "하부 집진 설비", Industrial + "SM_Machinery_06.prefab", new Vector3(248, -7, 64), new Vector3(12, 14, 8), wornSteel);
            ServicePipe(plant, new Vector3(249, 6, 64), new Vector3(249, 33, 64), 2);
            BLight(plant, new Vector3(237, 7, 70), new Vector3(248, 0, 64), 800, 28);
        }
    }
}
