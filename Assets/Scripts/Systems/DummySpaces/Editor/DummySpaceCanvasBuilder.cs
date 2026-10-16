using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static CurtainCall.DummySpaces.EditorTools.DummyUiKit;
using Object = UnityEngine.Object;

namespace CurtainCall.DummySpaces.EditorTools
{
    /// <summary>
    /// 네 더미 씬에 3D 더미 공간, HUD, 왼쪽 조작 패널(탭: 조작·결과·기록)을 저장한다.
    /// 공간 루트(DummyX)와 Main Camera는 유지하고, 빌더가 만든 루트만 지우고 다시 만든다.
    /// </summary>
    public static class DummySpaceCanvasBuilder
    {
        const string Folder = "Assets/Scenes/Tests/Systems/DummySpaces";
        static readonly string[] Owned = { "Dummy Canvas", "Dummy Preview", "Dummy EventSystem", "Dummy Environment" };
        static readonly List<DummySpaceCanvas.ActionButton> buttons = new();

        [MenuItem("Tools/CurtainCall/Build Dummy Space Canvas")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다. 먼저 확인하세요.");
            string original = SceneManager.GetActiveScene().path;
            LoadFont();
            foreach (string name in new[] { "DummyPerformance", "DummyWaitingRoom", "DummyExploration", "DummyEscapeRoute" }) Build(name);
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            Debug.Log("[DummySpaces] 네 씬 3D 공간·HUD·조작 패널 저장 완료");
        }

        static void Build(string name)
        {
            var scene = EditorSceneManager.OpenScene($"{Folder}/{name}.unity");
            DestroyRoots(scene, Owned);
            buttons.Clear();
            bool performance = name == "DummyPerformance";
            bool waiting = name == "DummyWaitingRoom";
            bool exploration = name == "DummyExploration";

            var canvas = Canvas("Dummy Canvas", typeof(DummySpaceCanvas));
            var view = canvas.GetComponent<DummySpaceCanvas>();
            var rt = (RectTransform)canvas.transform;
            BuildHud(rt, view);
            BuildPanel(rt, view, performance, waiting, exploration);
            view.actions = buttons.ToArray();
            EventSystem("Dummy EventSystem");

            var camera = Camera.main;
            var env = new GameObject("Dummy Environment").transform;
            var spots = new Transform[2][];
            if (performance) spots = Stage(env, camera);
            else if (waiting) spots = WaitingRoom(env, camera);
            else if (exploration) spots = Backstage(env, camera);
            else spots = EscapeRoute(env, camera);
            view.preview = Tokens(spots[0], spots[1]);
            ReportMissing(name);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ── HUD ─────────────────────────────────────────────

        static void BuildHud(RectTransform rt, DummySpaceCanvas view)
        {
            var top = Box("HUD Top", rt, 0, 0, 1600, 76, NavyGlass);
            top.anchorMax = new Vector2(1, 1);
            top.sizeDelta = new Vector2(0, 76);
            view.place = Text("Place", top, "더미 공간", 24, 8, 980, 36, 26, Color.white);
            view.steps = Text("Steps", top, "도입 / 공연 / 쉬는 시간 / 밤 / 탈출 / 엔딩", 24, 46, 980, 24, 15, new Color(0.74f, 0.81f, 0.83f));
            view.status = Text("Status", top, "상태 연결 대기", 1000, 10, 360, 30, 19, Color.white, TextAlignmentOptions.TopRight);
            view.funds = Text("Funds", top, "공동 자금 0", 1370, 10, 210, 30, 20, Gold, TextAlignmentOptions.TopRight);
            Corner((RectTransform)view.status.transform, new Vector2(1, 1));
            Corner((RectTransform)view.funds.transform, new Vector2(1, 1));
            Text("Badge", top, "임시 검증용 더미", 1370, 46, 210, 22, 14, Mint, TextAlignmentOptions.TopRight);
            Corner((RectTransform)top.Find("Badge"), new Vector2(1, 1));

            var widget = Corner(Box("HUD Space", rt, 1000, 88, 580, 84, NavyGlass), new Vector2(1, 1));
            view.hud = Text("HudText", widget, "", 16, 10, 548, 68, 16, Color.white);

            var clockCard = Corner(Box("HUD Clock", rt, 1180, 700, 400, 180, NavyGlass), new Vector2(1, 0));
            view.live = Text("LiveText", clockCard, "흐름 연결 대기", 16, 12, 368, 88, 14, new Color(0.84f, 0.88f, 0.90f));
            view.clock = Text("Clock", clockCard, "--:--", 16, 100, 368, 54, 42, Mint, TextAlignmentOptions.TopRight);
            Box("ClockTrack", clockCard, 16, 162, 368, 5, new Color(1, 1, 1, 0.18f));
            view.clockFill = Box("ClockFill", clockCard, 16, 162, 368, 5, Mint).GetComponent<Image>();
            // 스프라이트 없는 단색 막대는 왼쪽 pivot 기준 크기만 바꾼다.
            view.clockFill.type = Image.Type.Simple;

            var toast = Box("HUD Toast", rt, 620, 820, 540, 52, NavyGlass);
            toast.anchorMin = toast.anchorMax = toast.pivot = new Vector2(0.5f, 0);
            toast.anchoredPosition = new Vector2(70, 28);
            view.toast = Text("ToastText", toast, "", 16, 0, 508, 52, 17, Color.white, TextAlignmentOptions.MidlineLeft);
            view.toastPanel = toast.gameObject;
        }

        // ── 왼쪽 패널 ───────────────────────────────────────

        static void BuildPanel(RectTransform rt, DummySpaceCanvas view, bool performance, bool waiting, bool exploration)
        {
            view.panel = SidePanel(rt, new[] { "조작", "결과", "기록" }, out view.panelToggle, out view.panelToggleLabel, out view.tabButtons, out view.tabPages);
            var control = (RectTransform)view.tabPages[0].transform;
            view.selection = Text("Selection", control, "선택 대상 연결 대기", 24, 8, 512, 110, 17, Ink);
            view.condition = Text("Condition", control, "현재 단계에서 가능한 버튼만 활성화됩니다.", 24, 120, 512, 80, 14, Muted);
            float top = 204;
            if (performance)
            {
                Action(control, "person", "다음 참가자 선택", 24, 204, 512, Teal);
                Action(control, "cut", "선택 대상 절단", 24, 254, 252, Line);
                Action(control, "kill", "선택 대상 사망", 284, 254, 252, Danger);
                Action(control, "cheer", "환호 +", 24, 304, 252, Teal);
                Action(control, "arrive", "선택 대상 도착", 284, 304, 252, Teal);
                top = 356;
            }
            var content = Scroll("Actions", control, 16, top, 528, 750 - top);
            Group(content, "흐름 준비");
            Flow(content, "advance", "수동 진행 · 다음 단계 준비", true);
            Flow(content, "ready", "밤 준비 / 해제");
            if (performance)
            {
                Group(content, "공연 시작");
                Flow(content, "start", "오프라인 런 시작");
                Flow(content, "intro", "도입 종료");
                Flow(content, "begin", "공연 시작 · 명부 고정", true);
                Flow(content, "timeout", "수동 제한 시간 종료", false, true);
            }
            else if (waiting)
            {
                Group(content, "출구 / 탈출");
                Flow(content, "leave", "대기실 출구 → 백스테이지", true);
                Flow(content, "escapeStart", "탈출 개시 → 탈출로 (방장)", true);
                Group(content, "공동 자금");
                Flow(content, "spend", "지출만 검증 · 물품 없음");
                Group(content, "조회 입력 → 규칙 확인");
                Flow(content, "lost", "조회 손실 수 변경  0 ~ 3");
                Flow(content, "backpack", "조회 배낭 유무 변경");
                Flow(content, "restorePreview", "복구 / 소지 칸 조회", true);
                ServerButtons(content, true);
            }
            else if (exploration)
            {
                Group(content, "구역 선택 / 전환 신호");
                Flow(content, "leave", "출구 → 백스테이지", true);
                Flow(content, "room", "대기실 구역 입력");
                Flow(content, "stage", "무대 입구 + 전원 집결 가정");
                Flow(content, "escapeStart", "탈출 개시 → 탈출로 (방장)", true);
                Flow(content, "expire", "남은 시간 0 요청", false, true);
                Group(content, "잡힘 연결");
                Flow(content, "catchPreview", "잡힘 규칙 조회", true);
                ServerButtons(content, false);
            }
            else
            {
                Group(content, "탈출 개시 / 행 선택");
                Flow(content, "escape", "탈출 개시 · 전원 집결 가정", true);
                Flow(content, "row", "다음 더미 행 선택");
                Flow(content, "rowArrive", "선택 행 도착 입력");
                Flow(content, "rowRetire", "선택 행 리타이어 입력");
                Group(content, "별도 수동 결론 · 자동 집계 아님");
                Flow(content, "success", "도착 있음 → 엔딩", true);
                Flow(content, "retired", "전원 리타이어 → 세대 교체", false, true);
                ServerButtons(content, false);
            }

            var result = (RectTransform)view.tabPages[1].transform;
            view.resultBadge = Text("ResultBadge", result, "실행 대기", 24, 8, 512, 28, 17, Muted);
            view.resultTitle = Text("ResultTitle", result, "조작 탭에서 버튼을 눌러 보세요", 24, 40, 512, 34, 22, Ink);
            view.resultBody = Text("ResultBody", result, "입력 → 공통 API → 변경값을 이곳에서 확인합니다.", 24, 80, 512, 120, 17, Ink);
            Text("ChangeHeading", result, "변경 전  →  변경 후", 24, 206, 512, 24, 15, Muted);
            var diff = Scroll("Changes", result, 16, 232, 528, 300);
            view.changes = FlowText(diff, "ChangesText", "아직 관찰한 변경값이 없습니다.", 15, Ink, 60);
            view.connection = Text("Connection", result, "입력 대기 · 실제 처리 / 더미 입력 / 조회를 구분합니다", 24, 544, 512, 60, 15, Teal);

            var log = (RectTransform)view.tabPages[2].transform;
            Text("HistoryHeading", log, "상황 기록 · 최신순 (원시 API는 개발자 모드 로그)", 24, 8, 512, 26, 15, Muted);
            var scroll = Scroll("History", log, 16, 40, 528, 700);
            view.history = FlowText(scroll, "HistoryText", "상황 기록 대기", 16, Ink, 120);
        }

        static void ServerButtons(RectTransform parent, bool restore)
        {
            Group(parent, "실제 서버 대상 필요");
            Flow(parent, "server", "다음 실제 플레이어 선택");
            Flow(parent, restore ? "restore" : "catch", restore ? "서버 신체 1부위 복구 · 무료 검증" : "서버 잡힘 적용 · 장소 일치 필요");
        }

        static void Action(Transform parent, string id, string label, float x, float y, float width, Color color) =>
            buttons.Add(new DummySpaceCanvas.ActionButton { id = id, button = Button(parent, "Button-" + id, label, x, y, width, 44, color) });

        static void Flow(RectTransform parent, string id, string label, bool primary = false, bool destructive = false) =>
            buttons.Add(new DummySpaceCanvas.ActionButton { id = id, button = FlowButton(parent, "Button-" + id, label, primary, destructive) });

        // ── 3D 더미 공간 ────────────────────────────────────
        // 각 함수는 [시작 지점들, 끝 지점들]을 돌려준다(토큰 진행 0 → 1).

        static Transform[][] Stage(Transform env, Camera camera)
        {
            var eye = new Vector3(0, 9.5f, -7f);
            FrameCamera(camera, eye, new Vector3(0, 1, 9));
            Sun(env);
            Floor(env, -10, 10, 0, 17.5f);
            Wall(env, true, 17.5f, -10, 10);
            Wall(env, false, -10, 0, 17.5f);
            Wall(env, false, 10, 0, 17.5f);
            var red = Lit("stage", new Color(0.42f, 0.10f, 0.10f));
            Primitive("Stage Platform", env, PrimitiveType.Cube, new Vector3(0, 0.5f, 12), new Vector3(14, 1, 9), red);
            Primitive("Stage Edge", env, PrimitiveType.Cube, new Vector3(0, 1.02f, 7.55f), new Vector3(14, 0.04f, 0.1f), Lit("gold", new Color(0.85f, 0.65f, 0.25f)));
            for (int i = 0; i < 4; i++) Prop(Props + "SM_Prop_Curtain_01", env, new Vector3(-5.6f + i * 3.7f, 1, 16.2f), 0, 1.6f, new Vector3(2.3f, 1.9f, 0.7f));
            var rope = Lit("rope", new Color(0.86f, 0.78f, 0.60f));
            var starts = new Transform[4];
            var ends = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float z = 9 + i * 1.5f;
                var line = Primitive($"Rope {i + 1}", env, PrimitiveType.Cylinder, new Vector3(0, 1.45f, z), new Vector3(0.06f, 5, 0.06f), rope);
                line.transform.localRotation = Quaternion.Euler(0, 0, 90);
                Primitive($"Post L{i + 1}", env, PrimitiveType.Cylinder, new Vector3(-5, 1.25f, z), new Vector3(0.12f, 0.25f, 0.12f), rope);
                Primitive($"Post R{i + 1}", env, PrimitiveType.Cylinder, new Vector3(5, 1.25f, z), new Vector3(0.12f, 0.25f, 0.12f), rope);
                starts[i] = Spot(env, $"Start {i + 1}", new Vector3(-5.8f, 1.9f, z));
                ends[i] = Spot(env, $"End {i + 1}", new Vector3(5.8f, 1.9f, z));
            }
            for (int row = 0; row < 3; row++)
                for (int x = -7; x <= 7; x++)
                    if (x != 0) Prop(Props + "SM_Prop_Seat_01", env, new Vector3(x * 1.05f, 0, 1.5f + row * 1.6f), 0, 1, new Vector3(0.7f, 1.3f, 0.7f));
            Prop(Props + "SM_Prop_Floodlights_01", env, new Vector3(-8.5f, 0, 7.5f), 30, 0.7f, new Vector3(1.4f, 3.9f, 1.5f));
            Prop(Props + "SM_Prop_Floodlights_01", env, new Vector3(8.5f, 0, 7.5f), -30, 0.7f, new Vector3(1.4f, 3.9f, 1.5f));
            AddLight(env, "Stage Spot Gold", LightType.Spot, new Vector3(-4, 8, 6), new Vector3(60, 20, 0), Gold, 40, 20, 60);
            AddLight(env, "Stage Spot Red", LightType.Spot, new Vector3(4, 8, 6), new Vector3(60, -20, 0), new Color(1f, 0.35f, 0.3f), 30, 20, 60);
            Sign(env, "공연장 · 무대", new Vector3(0, 4.5f, 16.8f), eye, 4.5f, Gold);
            Sign(env, "출발", new Vector3(-5.8f, 3.4f, 13.5f), eye, 3f);
            Sign(env, "도착", new Vector3(5.8f, 3.4f, 13.5f), eye, 3f);
            Sign(env, "객석", new Vector3(-8.8f, 2.2f, 3), eye, 3f, Mint);
            return new[] { starts, ends };
        }

        static Transform[][] WaitingRoom(Transform env, Camera camera)
        {
            var eye = new Vector3(0, 12, -10.5f);
            FrameCamera(camera, eye, new Vector3(0, 0.5f, 8));
            Sun(env);
            Floor(env, -10, 10, 0, 15);
            Wall(env, true, 15, -10, 10, doorAt: 3);
            Wall(env, false, -10, 0, 15);
            Wall(env, false, 10, 0, 15);
            Prop(Props + "SM_Prop_Blackboard_01", env, new Vector3(-6.5f, 0, 12.5f), 180, 1.5f, new Vector3(0.8f, 1.5f, 0.6f));
            Prop(Props + "SM_Prop_Table_01", env, new Vector3(-7.5f, 0, 6), 0, 1, new Vector3(1.2f, 0.7f, 1.2f));
            Prop(Props + "SM_Prop_CardboardBox_01", env, new Vector3(-8.6f, 0, 6.4f), 20, 1, new Vector3(0.5f, 0.5f, 0.7f));
            Prop(Props + "SM_Prop_CardboardBox_02", env, new Vector3(-8.5f, 0, 5.2f), -10, 1, new Vector3(0.5f, 0.5f, 0.7f));
            Prop(Props + "SM_Prop_Couch_01", env, new Vector3(0, 0, 9.5f), 180, 1, new Vector3(2.8f, 1.2f, 0.9f));
            Prop(Props + "SM_Prop_Plank_Bench_01", env, new Vector3(-3, 0, 6.5f), 90, 1, new Vector3(0.8f, 0.5f, 3.5f));
            Prop(Props + "SM_Prop_Plank_Bench_01", env, new Vector3(3, 0, 6.5f), 90, 1, new Vector3(0.8f, 0.5f, 3.5f));
            Prop(Props + "SM_Prop_Bed_Bunk_01", env, new Vector3(8.2f, 0, 3), 90, 1, new Vector3(2.5f, 1.9f, 1));
            Prop(Props + "SM_Prop_Bed_Bunk_01", env, new Vector3(8.2f, 0, 6), 90, 1, new Vector3(2.5f, 1.9f, 1));
            Prop(Props + "SM_Prop_Bed_Hospital_01", env, new Vector3(7.5f, 0, 12), 0, 1, new Vector3(1.4f, 1.4f, 2.5f));
            AddLight(env, "Lobby Warm", LightType.Point, new Vector3(0, 4, 7), Vector3.zero, new Color(1f, 0.82f, 0.6f), 6, 14);
            Sign(env, "일정판", new Vector3(-6.5f, 3.4f, 12.5f), eye, 3f, Gold);
            Sign(env, "상점 (전화 · 배송구)", new Vector3(-7.5f, 2.6f, 6), eye, 2.6f);
            Sign(env, "숙소", new Vector3(8.2f, 3.2f, 4.5f), eye, 2.6f);
            Sign(env, "전문가실", new Vector3(7.5f, 2.8f, 12), eye, 2.6f);
            Sign(env, "출구 → 백스테이지", new Vector3(-1.25f, 3.4f, 14.6f), eye, 3f, Mint);
            Sign(env, "로비", new Vector3(0, 0.15f, 4.2f), eye, 3f, new Color(1, 1, 1, 0.7f));
            var starts = new Transform[4];
            for (int i = 0; i < 4; i++) starts[i] = Spot(env, $"Lobby {i + 1}", new Vector3(-2.25f + i * 1.5f, 0.9f, 5.2f));
            return new[] { starts, starts };
        }

        static Transform[][] Backstage(Transform env, Camera camera)
        {
            var eye = new Vector3(0, 13, -14.5f);
            FrameCamera(camera, eye, new Vector3(0, 0.5f, 3.5f));
            Sun(env);
            Floor(env, -10, 10, 0, 7.5f);
            Wall(env, true, 7.5f, -10, 10);
            Wall(env, false, -10, 0, 7.5f, doorAt: 1);
            Prop(Props + "SM_Prop_Scaffold_01", env, new Vector3(-3, 0, 7.2f), 0, 1, new Vector3(2.5f, 3.1f, 1.5f));
            Prop(Props + "SM_Prop_Ladder_01", env, new Vector3(1.5f, 0, 7.2f), 0, 1, new Vector3(0.5f, 3, 0.1f));
            Prop(Props + "SM_Prop_Crate_01", env, new Vector3(-6, 0, 6.2f), 15, 1, new Vector3(0.8f, 0.8f, 0.8f));
            Prop(Props + "SM_Prop_Crate_Large_01", env, new Vector3(4.5f, 0, 6.3f), 90, 1, new Vector3(0.8f, 0.8f, 1.5f));
            Prop(Props + "SM_Prop_Pallet_01", env, new Vector3(-4.5f, 0, 1), 10, 1, new Vector3(1.5f, 0.2f, 1.4f));
            Prop(Props + "SM_Prop_CardboardBox_01", env, new Vector3(5.5f, 0, 1.2f), 30, 1, new Vector3(0.5f, 0.5f, 0.7f));
            Prop(Props + "SM_Prop_CardboardBox_02", env, new Vector3(6.2f, 0, 0.8f), -20, 1, new Vector3(0.5f, 0.5f, 0.7f));
            for (int i = 0; i < 3; i++) Prop(Props + "SM_Prop_Curtain_01", env, new Vector3(9.6f, 0, 1.4f + i * 2.3f), 90, 1.5f, new Vector3(2.3f, 1.9f, 0.7f));
            AddLight(env, "Backstage Cold", LightType.Point, new Vector3(0, 4, 3.5f), Vector3.zero, new Color(0.6f, 0.72f, 1f), 5, 16);
            AddLight(env, "Stage Entrance Red", LightType.Point, new Vector3(8.5f, 3, 3.5f), Vector3.zero, new Color(1f, 0.3f, 0.25f), 6, 7);
            Sign(env, "대기실 문", new Vector3(-8.6f, 3.6f, 3.5f), eye, 3f);
            Sign(env, "백스테이지", new Vector3(0, 2.4f, 7.2f), eye, 3.4f, Mint);
            Sign(env, "무대 입구", new Vector3(8.6f, 3.8f, 3.5f), eye, 3f, new Color(1f, 0.5f, 0.45f));
            var start = Spot(env, "Waiting Room Door", new Vector3(-8.5f, 0.9f, 3.5f));
            var end = Spot(env, "Stage Entrance", new Vector3(8.2f, 0.9f, 3.5f));
            return new[] { new[] { start, start, start, start }, new[] { end, end, end, end } };
        }

        static Transform[][] EscapeRoute(Transform env, Camera camera)
        {
            var eye = new Vector3(0, 13.5f, -14);
            FrameCamera(camera, eye, new Vector3(0, 0.5f, 4.5f));
            Sun(env);
            Floor(env, -10, 10, 0, 10);
            Wall(env, true, 10, -10, 10);
            Wall(env, false, 10, 0, 10, doorAt: 1);
            Prop(Props + "SM_Prop_Barrier_Concrete_01", env, new Vector3(-3, 0, 8.6f), 90, 1, new Vector3(0.5f, 0.9f, 2));
            Prop(Props + "SM_Prop_Barrier_Concrete_01", env, new Vector3(3.5f, 0, 0.8f), 90, 1, new Vector3(0.5f, 0.9f, 2));
            Prop(Props + "SM_Prop_Barricade_Pallet_01", env, new Vector3(-6.5f, 0, 9.4f), 0, 1, new Vector3(1.4f, 1.4f, 0.2f));
            Prop(Props + "SM_Prop_Barrel_01", env, new Vector3(5.5f, 0, 9), 0, 1, new Vector3(0.6f, 0.9f, 0.6f));
            Prop(Props + "SM_Prop_Barrel_01", env, new Vector3(6.2f, 0, 9.2f), 40, 1, new Vector3(0.6f, 0.9f, 0.6f));
            Prop(Props + "SM_Prop_Pallet_01", env, new Vector3(8.5f, 0, 8.6f), 0, 1, new Vector3(1.5f, 0.2f, 1.4f));
            Prop(Props + "SM_Prop_Crate_01", env, new Vector3(8.5f, 0.2f, 8.6f), 10, 1, new Vector3(0.8f, 0.8f, 0.8f));
            AddLight(env, "Route Dim", LightType.Point, new Vector3(-4, 4, 5), Vector3.zero, new Color(0.7f, 0.75f, 0.9f), 4, 14);
            AddLight(env, "Dock Light", LightType.Point, new Vector3(8.5f, 3.5f, 4), Vector3.zero, new Color(0.5f, 1f, 0.7f), 6, 8);
            Sign(env, "탈출로 입구", new Vector3(-8.8f, 3.4f, 5), eye, 3f);
            Sign(env, "옛 극장 → 하역장", new Vector3(0, 2.4f, 9.7f), eye, 3.2f, Mint);
            Sign(env, "하역장 (탈출 성공)", new Vector3(8.6f, 3.8f, 4), eye, 3f, new Color(0.6f, 1f, 0.75f));
            var starts = new Transform[4];
            var ends = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float z = 2 + i * 1.8f;
                starts[i] = Spot(env, $"Entrance {i + 1}", new Vector3(-8.5f, 0.9f, z));
                ends[i] = Spot(env, $"Dock {i + 1}", new Vector3(8.6f, 0.9f, z));
                Sign(env, $"행 {i + 1}", new Vector3(-9.6f, 0.15f, z), eye, 1.8f, new Color(1, 1, 1, 0.6f));
            }
            return new[] { starts, ends };
        }

        static void Sun(Transform env) => AddLight(env, "Sun", LightType.Directional, Vector3.zero, new Vector3(50, -30, 0), new Color(1f, 0.96f, 0.9f), 1.1f);

        static Transform Spot(Transform env, string name, Vector3 position)
        {
            var spots = env.Find("Spots") ?? new GameObject("Spots").transform;
            spots.SetParent(env, false);
            var go = new GameObject(name).transform;
            go.SetParent(spots, false);
            go.localPosition = position;
            return go;
        }

        static DummySpacePreview Tokens(Transform[] starts, Transform[] ends)
        {
            var root = new GameObject("Dummy Preview", typeof(DummySpacePreview));
            var view = root.GetComponent<DummySpacePreview>();
            view.idle = Unlit("idle", new Color(0.31f, 0.43f, 0.51f));
            view.active = Unlit("active", new Color(0.15f, 0.81f, 0.69f));
            view.complete = Unlit("complete", new Color(0.90f, 0.74f, 0.30f));
            view.danger = Unlit("danger", new Color(0.87f, 0.29f, 0.34f));
            view.starts = starts;
            view.ends = ends;
            view.tokens = new Transform[4];
            view.tokenRenderers = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                var token = Primitive("Dummy Token " + (i + 1), root.transform, PrimitiveType.Capsule, starts[i].position, new Vector3(0.55f, 0.9f, 0.55f), view.idle);
                view.tokens[i] = token.transform;
                view.tokenRenderers[i] = token.GetComponent<Renderer>();
            }
            return view;
        }
    }
}
