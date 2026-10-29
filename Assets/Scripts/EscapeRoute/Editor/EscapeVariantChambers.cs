using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        // Canonical chamber: travel in +X, leave across one side of its far landing.
        // Rotating whole chunks changes traversal axes without skewing human-scale props.
        static void BuildVariantChamber(string name, Vector3 origin, float yaw, bool cannon,
            bool exitFarSide, float landingY, float entryLane, bool staggered)
        {
            var p = VGroup(name, origin, yaw);
            float width = cannon ? 25 : 37.5f;
            VPlatform(p, 0, 12.5f, 0, width, 0);
            VPlatform(p, 37.5f, 50, 0, width, landingY);
            if (cannon)
            {
                float islandZ = staggered ? 17.5f : width / 2;
                VPlatform(p, 22, 30, islandZ - 4, islandZ + 4, landingY);
                VRail(p, new Vector3(22, landingY, islandZ - 4), new Vector3(30, landingY, islandZ - 4));
                VRail(p, new Vector3(22, landingY, islandZ + 4), new Vector3(30, landingY, islandZ + 4));
                VBlock(p, "대포 설치 자리", new Vector3(8, .3f, entryLane), new Vector3(3.5f, .6f, 3.5f), grey);
                VCable(p, new Vector3(29, landingY + 1, islandZ),
                    new Vector3(39, landingY + 1, staggered ? 8 : islandZ), .055f, "외줄 연결 시각 표시");
                VLabel(p, "대포 → 고립 발판 → 외줄 / 기능 없음", new Vector3(24, landingY + 3, islandZ));
            }
            else
            {
                VBlock(p, "트램폴린 설치 자리", new Vector3(8.5f, .25f, entryLane), new Vector3(4, .5f, 4), grey);
                float[] xs = { 19, 27, 35 };
                float[] zs = staggered ? new[] { 14f, 24f, 18f } : new[] { 12f, 20f, 27f };
                for (int i = 0; i < xs.Length; i++)
                {
                    float y = i == 1 ? 8 : 6.5f;
                    VCable(p, new Vector3(xs[i], 23, zs[i] - .8f), new Vector3(xs[i], y, zs[i] - .8f), .045f, "그네 지지선");
                    VCable(p, new Vector3(xs[i], 23, zs[i] + .8f), new Vector3(xs[i], y, zs[i] + .8f), .045f, "그네 지지선");
                    VBlock(p, "그네 설치 가이드", new Vector3(xs[i], y, zs[i]), new Vector3(.14f, .14f, 1.6f), vSteel, false);
                }
                VLabel(p, "트램폴린 → 엇갈린 그네 → 착지 / 기능 없음", new Vector3(23, 4, width / 2));
            }

            VWallX(p, 0, 0, exitFarSide ? 50 : 37.5f, 28);
            VWallX(p, width, 0, exitFarSide ? 37.5f : 50, 28);
            VWallZ(p, 50, 0, width, 28);
            VCeiling(p, 0, 50, 0, width, 28);
            VBlock(p, "후면 하부 외피", new Vector3(25, -31, width + .5f), new Vector3(51, 62, 1), vDepth);
            VBlock(p, "전면 하부 외피", new Vector3(25, -31, -.5f), new Vector3(51, 62, 1), vDepth);
            VBlock(p, "끝 하부 외피", new Vector3(50.5f, -31, width / 2), new Vector3(1, 62, width + 2), vDepth);
            VBlock(p, "입구 하부 외피", new Vector3(-.5f, -31, width / 2), new Vector3(1, 62, width + 2), vDepth);
            VBlock(p, "공동 암부 바닥", new Vector3(25, -62, width / 2), new Vector3(51, 1, width + 2), vDepth);

            // The narrow, low aperture establishes a familiar foreground before the full hall.
            float left = entryLane - 4.5f, right = entryLane + 4.5f;
            VBlock(p, "진입 교각 앞", new Vector3(0, 14, left / 2), new Vector3(2.4f, 28, left), vWall);
            VBlock(p, "진입 교각 뒤", new Vector3(0, 14, (right + width) / 2), new Vector3(2.4f, 28, width - right), vWall);
            VBlock(p, "5m 높이 진입 인방", new Vector3(0, 16.5f, entryLane), new Vector3(2.4f, 23, 9), vWall);
            VRail(p, new Vector3(0, 0, 0), new Vector3(12.5f, 0, 0));
            VRail(p, new Vector3(0, 0, width), new Vector3(12.5f, 0, width));

            foreach (float x in new[] { 16f, 33f })
            {
                VAsset(p, Industrial + "SM_Pipe_Big_01.prefab", new Vector3(x, -48, width - 1.8f),
                    new Vector3(2.5f, 73, 2.5f), vSteel, Quaternion.Euler(0, 0, 90));
                foreach (float y in new[] { -18f, 11f })
                {
                    VAsset(p, Industrial + "SM_Scaffolding_Flooring.prefab", new Vector3(x, y, width - 3.4f),
                        new Vector3(8, .25f, 1.6f), vSteel);
                    VRail(p, new Vector3(x - 4, y + .25f, width - 4.1f), new Vector3(x + 4, y + .25f, width - 4.1f));
                }
            }
            if (cannon)
            {
                VAsset(p, Industrial + "SM_Pipe_Big_Cap.prefab", new Vector3(49, 8, width / 2), new Vector3(1.2f, 14, 14), vSteel);
                VLight(p, "큰 점검구의 사광", new Vector3(30, 21, 4), new Vector3(48, 15, width / 2), 500, 32, 48);
            }
            else
            {
                VAsset(p, Industrial + "SM_Crane_Beam.prefab", new Vector3(27, 23, width / 2), new Vector3(1.5f, 1.4f, width - 3), vSteel);
                VAsset(p, Industrial + "SM_Crane_Beam.prefab", new Vector3(25, 26, width - 5), new Vector3(46, 1.4f, 1.4f), vSteel);
                VLight(p, "그네 지지 구조의 사광", new Vector3(10, 21, 4), new Vector3(29, 20, width - 3), 550, 38, 65);
            }
            VBay(p, 5, width - 1, 28, variantId + "-H");
            VLight(p, "입구 바닥", new Vector3(5, 6, entryLane), new Vector3(10, 0, entryLane), 150, 16);
            VLight(p, "도착 바닥", new Vector3(45, landingY + 10, width * .55f), new Vector3(43, landingY, width * .55f), 350, 22);
            VLight(p, "하부 점검층", new Vector3(20, -9, width - 7), new Vector3(33, -18, width - 3), 220, 26, 42);
            VPerson(p, new Vector3(45, landingY, width * .6f));
        }

        static void VGate(Transform p, Vector3 at, float span, float opening, bool acrossZ)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var offset = acrossZ ? new Vector3(0, opening / 2, side * span / 2) : new Vector3(side * span / 2, opening / 2, 0);
                VBlock(p, "진행 문 교각", at + offset, new Vector3(1.2f, opening, 1.2f), vWall);
            }
            VBlock(p, "진행 문 상부", at + Vector3.up * (opening + 1),
                acrossZ ? new Vector3(1.2f, 2, span + 1) : new Vector3(span + 1, 2, 1.2f), vSteel);
        }
    }
}
