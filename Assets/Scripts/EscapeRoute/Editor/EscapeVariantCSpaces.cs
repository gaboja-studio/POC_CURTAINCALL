using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        // Space first: these are ordinary structural shelves and recovery terraces.
        // Future stunts may replace individual links; no stunt order or runtime behaviour is prescribed.
        static void BuildVariantCSpaces(Transform group)
        {
            var first = VerticalChild(group, "03_OffsetMaintenanceShelves");
            BFloor(first, "진입 정비 선반", 32, 36, 73.7f, 75.3f, 0, .9f);
            SpaceTerrace(first, "01 상부 회복 정비대", 36, 43, 71, 78, 0, -90);
            CSpaceDescent(first, "하부 점검층 하강", 43, 74.5f, 0, 30, false);
            SpaceTerrace(first, "02 하부 회복 정비대", 53.5f, 60.5f, 72, 79, -6, -90);
            // Butt joints retain a single top surface. The north leg stops at the existing arrival slab.
            BFloor(first, "도착부 전 동향 선반", 60.5f, 62.8f, 76.5f, 78.1f, -6, .9f);
            BFloor(first, "도착부 전 북향 선반", 62.8f, 64.4f, 76.5f, 79, -6, .9f);

            SpaceEdgeRail(first, new Vector3(32, 0, 73.75f), new Vector3(36, 0, 73.75f));
            SpaceEdgeRail(first, new Vector3(36, 0, 71.1f), new Vector3(43, 0, 71.1f));
            SpaceEdgeRail(first, new Vector3(36, 0, 77.9f), new Vector3(43, 0, 77.9f));
            SpaceEdgeRail(first, new Vector3(53.5f, -6, 72.1f), new Vector3(60.5f, -6, 72.1f));
            SpaceEdgeRail(first, new Vector3(53.5f, -6, 78.9f), new Vector3(60.5f, -6, 78.9f));
            SpaceEdgeRail(first, new Vector3(60.5f, -6, 76.55f), new Vector3(64.3f, -6, 76.55f));
            SpaceLamp(first, new Vector3(36.5f, 0, 71.5f), new Vector3(40, 0, 74.5f), 230);
            SpaceLamp(first, new Vector3(59.9f, -6, 72.6f), new Vector3(56.5f, -6, 75.5f), 250);
            SpaceReserve(first, "UpperMaintenanceFloor", new Vector3(39.5f, 0, 74.5f));
            SpaceReserve(first, "LowerMaintenanceFloor", new Vector3(57, -6, 75.5f));
            FloorWear(first, new Vector3(40, .025f, 76), new Vector2(3.5f, 2), 8);
            FloorWear(first, new Vector3(57, -5.975f, 73.8f), new Vector2(3.5f, 2), -9);

            var second = VerticalChild(group, "05_DescendingMaintenancePlatforms");
            BFloor(second, "공동 진입 정비 선반", 90, 94, 24.2f, 25.8f, 0, .9f);
            SpaceTerrace(second, "01 횡단 전 정비대", 94, 100, 21.5f, 28.5f, 0, -100);
            CSpaceDescent(second, "첫 하강 정비 계단", 100, 25, 0, 20, false);
            SpaceTerrace(second, "02 중간 회복 정비대", 107, 113, 22, 29, -4, -100);
            // The last 0.2m descent lands on the preserved A platform at Y-8.
            // Do not add a coplanar final tread over that existing slab.
            CSpaceDescent(second, "도착부 하강 정비 계단", 113, 25, -4, 20, true);
            SpaceEdgeRail(second, new Vector3(90, 0, 24.25f), new Vector3(94, 0, 24.25f));
            SpaceEdgeRail(second, new Vector3(94, 0, 21.6f), new Vector3(100, 0, 21.6f));
            SpaceEdgeRail(second, new Vector3(94, 0, 28.4f), new Vector3(100, 0, 28.4f));
            SpaceEdgeRail(second, new Vector3(107, -4, 22.1f), new Vector3(113, -4, 22.1f));
            SpaceEdgeRail(second, new Vector3(107, -4, 28.9f), new Vector3(113, -4, 28.9f));
            SpaceLamp(second, new Vector3(94.5f, 0, 22), new Vector3(97, 0, 25), 260);
            SpaceLamp(second, new Vector3(112.4f, -4, 28.4f), new Vector3(110, -4, 25), 240);
            SpaceReserve(second, "UpperCrossingFloor", new Vector3(97, 0, 25));
            SpaceReserve(second, "LowerCrossingFloor", new Vector3(110, -4, 25));
            SpaceReserve(second, "ExistingArrivalFloor", new Vector3(121, -8, 28));
            FloorWear(second, new Vector3(97, .025f, 27), new Vector2(3, 2), 5);
            FloorWear(second, new Vector3(110, -3.975f, 23.8f), new Vector2(3, 2), -7);
        }

        static void CSpaceDescent(Transform parent, string name, float x, float z, float top, int transitions, bool landOnExisting)
        {
            var flight = VerticalChild(parent, name);
            int treads = landOnExisting ? transitions - 1 : transitions;
            for (int i = 0; i < treads; i++)
                BFloor(flight, "정비 계단 " + (i + 1), x + i * .35f, x + (i + 1) * .35f,
                    z - .9f, z + .9f, top - (i + 1) * .2f, .65f);
            var end = new Vector3(x + transitions * .35f, top - transitions * .2f, z);
            foreach (float side in new[] { -.72f, .72f })
                VerticalStrut(flight, "정비 계단 하부 거더", new Vector3(x, top - .85f, z + side),
                    end + new Vector3(0, -.85f, side), .25f, bRecess);
            SpaceEdgeRail(flight, new Vector3(x, top, z + .82f), end + Vector3.forward * .82f);
        }
    }
}
