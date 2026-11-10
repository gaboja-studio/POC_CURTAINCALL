using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CurtainCall.WaitingRoom.Editor
{
    // 전체 층 보기: 한 화면에 가장 높은 층(지상)을 왼쪽에 크게, 그 옆 오른쪽 열에 지하 층들을 위→아래로 쌓아 그린다.
    // 칸마다 그 층의 방 범위에 맞춰 따로 화면을 잡아 층 전체가 보이게 한다. 휠·드래그는 마우스가 있는 칸에만 적용된다.
    // 보기·선택 전용이며 배치/드래그 이동은 표시 층에서 층을 골라서 한다.
    public sealed partial class WaitingRoomObjectWindow
    {
        bool allFloors;
        const string AllFloorsChoice = "전체 층 (한 화면)";
        const float MainFloorShare = .62f;
        readonly Dictionary<float, (Vector3 center, float size)> floorViews = new();
        float panFloor = float.NaN;

        float SliceTopFor(float floor) => Mathf.Min(floor + cutHeight, floorHeights.Where(y => y > floor + .1f).DefaultIfEmpty(float.PositiveInfinity).Min() - .1f);
        float FloorOf(Transform t) => FloorForHeight(t.position.y, floorHeights);

        VisualElement CreateAllFloorsControls() => new Label("전체 층: 왼쪽 지상 층, 오른쪽 지하 층 · 칸마다 휠·드래그") { style = { whiteSpace = WhiteSpace.Normal, color = Color.gray } };

        // 가장 높은 층은 왼쪽 큰 칸, 나머지 층은 오른쪽 열에 높은 층부터 위→아래.
        List<(float floor, Rect rect)> FloorTiles(Rect area)
        {
            var floors = floorHeights.OrderByDescending(y => y).ToArray();
            var tiles = new List<(float, Rect)>();
            if (floors.Length <= 1) { tiles.Add((floors.FirstOrDefault(), Inset(area))); return tiles; }
            float mainWidth = area.width * MainFloorShare;
            tiles.Add((floors[0], Inset(new Rect(area.x, area.y, mainWidth, area.height))));
            float h = area.height / (floors.Length - 1);
            for (int i = 1; i < floors.Length; i++)
                tiles.Add((floors[i], Inset(new Rect(area.x + mainWidth, area.y + (i - 1) * h, area.width - mainWidth, h))));
            return tiles;
            static Rect Inset(Rect r) => new(r.x + 2, r.y + 2, Mathf.Max(1, r.width - 4), Mathf.Max(1, r.height - 4));
        }

        void FrameAllFloors() { floorViews.Clear(); mapInitialized = true; RepaintMap(); }

        (Vector3 center, float size) FloorView(float floor, Rect tile)
        {
            if (floorViews.TryGetValue(floor, out var view)) return view;
            var rooms = MapRooms().Where(r => r.Area != null
                && Mathf.Abs(Mathf.Round(CurtainCall.WaitingRoom.Pursuit.Editor.PursuitAuthoring.FloorReference(r)) - floor) < .1f).ToArray();
            Bounds b;
            if (rooms.Length > 0) { b = rooms[0].Area.bounds; foreach (var room in rooms) b.Encapsulate(room.Area.bounds); }
            else {
                var renderers = MapRenderers().Where(r => r.bounds.max.y >= floor - .5f && r.bounds.min.y <= SliceTopFor(floor)).ToArray();
                if (renderers.Length == 0) return (mapCenter, mapSize);
                b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
            }
            float aspect = tile.height > 1 ? tile.width / tile.height : 1;
            view = (b.center, Mathf.Max(2, Mathf.Max(b.size.z, b.size.x / Mathf.Max(.1f, aspect)) * .55f));
            floorViews[floor] = view;
            return view;
        }

        static Vector2 TilePixel(Vector3 world, Rect tile, Vector3 center, float size)
        {
            float units = tile.height / (2 * size);
            return tile.center + new Vector2(world.x - center.x, center.z - world.z) * units;
        }

        void RenderAllFloors(Rect rect, List<(float floor, Rect rect)> tiles)
        {
            PrepareSpotPreviews();
            EnsureMapTarget(rect);
            var masked = MaskMapRenderers();
            try {
                // 칸 사이 여백까지 배경색으로 한 번 지운다.
                mapCamera.rect = new Rect(0, 0, 1, 1);
                mapCamera.transform.SetPositionAndRotation(new Vector3(0, -100000, 0), Quaternion.Euler(90, 0, 0));
                mapCamera.nearClipPlane = .01f; mapCamera.farClipPlane = .02f; mapCamera.Render();
                foreach (var (floor, tile) in tiles) {
                    var view = FloorView(floor, tile);
                    mapCamera.rect = new Rect(tile.x / rect.width, 1 - tile.yMax / rect.height, tile.width / rect.width, tile.height / rect.height);
                    mapCamera.transform.SetPositionAndRotation(new Vector3(view.center.x, floor + 1000, view.center.z), Quaternion.Euler(90, 0, 0));
                    mapCamera.nearClipPlane = Mathf.Max(.01f, 1000 - (SliceTopFor(floor) - floor));
                    mapCamera.farClipPlane = 1000.5f;
                    mapCamera.orthographicSize = view.size; mapCamera.aspect = tile.width / tile.height;
                    mapCamera.Render();
                }
            }
            finally {
                mapCamera.rect = new Rect(0, 0, 1, 1);
                RestoreMapRenderers(masked);
            }
        }

        void DrawAllFloors(Rect rect)
        {
            var tiles = FloorTiles(rect);
            if (Event.current.type == EventType.Repaint) RenderAllFloors(rect, tiles);
            if (mapTexture != null) GUI.DrawTexture(rect, mapTexture, ScaleMode.StretchToFill, false);
            Handles.BeginGUI();
            foreach (var (floor, tile) in tiles) {
                var view = FloorView(floor, tile);
                Handles.color = new Color(1, 1, 1, .35f);
                Handles.DrawSolidRectangleWithOutline(tile, Color.clear, Handles.color);
                if (!hideInteractions)
                    foreach (var target in objectRows.Where(t => t != null && t != selected && Mathf.Abs(FloorOf(t) - floor) < .1f)) {
                        if (!showConditionalContents && ConditionalContent(target)) continue;
                        var p = TilePixel(PlacementPoint(target), tile, view.center, view.size);
                        if (!tile.Contains(p)) continue;
                        Handles.color = target.gameObject.activeSelf ? Color.cyan : Color.gray;
                        Handles.DrawSolidDisc(p, Vector3.forward, 3);
                    }
                if (selected != null && Mathf.Abs(FloorOf(selected) - floor) < .1f) {
                    var p = TilePixel(PlacementPoint(selected), tile, view.center, view.size);
                    if (tile.Contains(p)) {
                        Handles.color = Color.yellow; Handles.DrawWireDisc(p, Vector3.forward, 10);
                        GUI.Label(new Rect(p.x + 12, p.y, 320, 22), InteractionLabel(selected), EditorStyles.whiteLabel);
                    }
                }
                var title = floor >= floorHeights.Max() - .1f ? $"{floor:0.##}m 층 (지상)" : $"{floor:0.##}m 층 (지하)";
                GUI.Label(new Rect(tile.x + 6, tile.y + 4, tile.width - 12, 22), title, EditorStyles.whiteBoldLabel);
            }
            Handles.EndGUI();
            GUI.Label(new Rect(8, rect.height - 24, rect.width - 16, 22), "N ↑ · 클릭 선택 · 더블클릭 그 층으로 이동 · 칸마다 드래그·휠 · 맵 전체 보기로 다시 맞춤", EditorStyles.whiteLabel);
            AllFloorsInput(rect, tiles);
        }

        Transform PickAllFloors(Vector2 mouse, float floor, Rect tile)
        {
            if (hideInteractions) return null;
            var view = FloorView(floor, tile);
            var marker = objectRows.Where(t => t != null && Mathf.Abs(FloorOf(t) - floor) < .1f && (showConditionalContents || !ConditionalContent(t) || t == selected))
                .OrderBy(t => (TilePixel(PlacementPoint(t), tile, view.center, view.size) - mouse).sqrMagnitude).FirstOrDefault();
            return marker != null && Vector2.Distance(TilePixel(PlacementPoint(marker), tile, view.center, view.size), mouse) <= 9 ? marker : null;
        }

        void AllFloorsInput(Rect rect, List<(float floor, Rect rect)> tiles)
        {
            var e = Event.current;
            var hover = tiles.FirstOrDefault(t => t.rect.Contains(e.mousePosition));
            bool overTile = hover.rect.width > 0;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { placing = false; ClearCreation(); existingPaletteSource = null; EndMapDrag(); panFloor = float.NaN; e.Use(); return; }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F) { FrameMap(true); e.Use(); return; }
            if (e.type == EventType.DragUpdated && DragAndDrop.GetGenericData(CardDragKey) != null) {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                status.text = "전체 층 보기에서는 배치할 수 없습니다. 표시 층에서 층을 고르세요."; e.Use(); return;
            }
            if (e.type == EventType.MouseUp) {
                if (mapPanning && panButton == 0 && panDistance < 3 && overTile) {
                    var hit = PickAllFloors(e.mousePosition, hover.floor, hover.rect);
                    if (hit != null) Selection.activeGameObject = hit.gameObject;
                }
                EndMapDrag(); panFloor = float.NaN; e.Use(); return;
            }
            if (!rect.Contains(e.mousePosition) && !mapPanning) return;
            if (e.type == EventType.ScrollWheel && overTile) {
                var view = FloorView(hover.floor, hover.rect);
                var before = MapWorld(e.mousePosition, hover.rect, view.center, view.size, 0);
                view.size = Mathf.Clamp(view.size * Mathf.Exp(e.delta.y * .08f), .5f, 2000);
                view.center += before - MapWorld(e.mousePosition, hover.rect, view.center, view.size, 0);
                floorViews[hover.floor] = view;
                e.Use(); RepaintMap();
            } else if (e.type == EventType.MouseDown) {
                map.Focus();
                if (e.button == 0 && e.clickCount >= 2 && overTile) {
                    var target = PickAllFloors(e.mousePosition, hover.floor, hover.rect);
                    var view = FloorView(hover.floor, hover.rect);
                    var point = MapWorld(e.mousePosition, hover.rect, view.center, view.size, hover.floor);
                    ShowFloor(hover.floor);
                    if (target != null) FocusObject(target);
                    else { mapCenter = point; RepaintMap(); }
                    e.Use(); return;
                }
                if (e.alt && e.button == 0) { status.text = "전체 층 보기에서는 배치·이동할 수 없습니다. 표시 층에서 층을 고르세요."; e.Use(); return; }
                if ((e.button == 0 || e.button == 2) && overTile) { mapPanning = true; panDistance = 0; panButton = e.button; panFloor = hover.floor; }
                e.Use();
            } else if (e.type == EventType.MouseDrag && mapPanning && !float.IsNaN(panFloor)) {
                panDistance += e.delta.magnitude;
                var tile = tiles.FirstOrDefault(t => Mathf.Abs(t.floor - panFloor) < .1f).rect;
                var view = FloorView(panFloor, tile);
                view.center += new Vector3(-e.delta.x, 0, e.delta.y) * (2 * view.size / Mathf.Max(1, tile.height));
                floorViews[panFloor] = view;
                e.Use(); RepaintMap();
            }
        }

        // 한 층 보기로 돌아간다. 그 칸에서 보던 확대·중심을 이어서 쓴다.
        void ShowFloor(float floor)
        {
            if (floorViews.TryGetValue(floor, out var view)) { mapCenter = view.center; mapSize = view.size; }
            allFloors = false; planeHeight = floor;
            EndMapDrag(); placing = false; ClearCreation(); existingPaletteSource = null;
            RefreshFloors(); RefreshList(); RefreshDetails(); RepaintMap();
        }
    }
}
