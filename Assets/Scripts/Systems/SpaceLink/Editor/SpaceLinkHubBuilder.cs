using System;
using System.Collections.Generic;
using CurtainCall.Network.Session;
using CurtainCall.SpaceLink;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static CurtainCall.DummySpaces.EditorTools.DummyUiKit;
using Object = UnityEngine.Object;

namespace CurtainCall.SpaceLink.EditorTools
{
    /// <summary>
    /// SpaceLink 시작 씬에 Canvas 화면(접속·흐름 탭), EventSystem, 3D 더미 로비를 저장하고 IMGUI 접속 패널을 뗀다.
    /// 장소 전환용 SpaceLink Hub·Main Camera·Directional Light는 유지한다.
    /// </summary>
    public static class SpaceLinkHubBuilder
    {
        const string ScenePath = "Assets/Scenes/Tests/Systems/SpaceLink/SpaceLink.unity";
        static readonly List<SpaceLinkHubCanvas.ActionButton> buttons = new();

        [MenuItem("Tools/CurtainCall/Build SpaceLink Hub")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다. 먼저 확인하세요.");
            string original = SceneManager.GetActiveScene().path;
            LoadFont();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            DestroyRoots(scene, "Hub Canvas", "Hub EventSystem", "Hub Environment");
            var hub = Object.FindAnyObjectByType<SpaceLinkHub>();
            if (hub == null) throw new InvalidOperationException("SpaceLink Hub가 없습니다.");
            var oldPanel = hub.GetComponent<NetworkSessionTestUI>();
            if (oldPanel != null) Object.DestroyImmediate(oldPanel);

            buttons.Clear();
            var canvas = Canvas("Hub Canvas", typeof(SpaceLinkHubCanvas));
            var view = canvas.GetComponent<SpaceLinkHubCanvas>();
            var rt = (RectTransform)canvas.transform;
            var top = Box("HUD Top", rt, 0, 0, 1600, 76, NavyGlass);
            top.anchorMax = new Vector2(1, 1);
            top.sizeDelta = new Vector2(0, 76);
            view.place = Text("Place", top, "시작 로비", 24, 8, 980, 36, 26, Color.white);
            Text("Hint", top, "왼쪽 패널 [접속]에서 방을 만들거나, [흐름]에서 혼자 오프라인 런을 시작하세요", 24, 46, 980, 24, 15, new Color(0.74f, 0.81f, 0.83f));
            view.status = Text("Status", top, "런 시작 전", 1000, 10, 360, 30, 19, Color.white, TextAlignmentOptions.TopRight);
            view.funds = Text("Funds", top, "공동 자금 0", 1370, 10, 210, 30, 20, Gold, TextAlignmentOptions.TopRight);
            Corner((RectTransform)view.status.transform, new Vector2(1, 1));
            Corner((RectTransform)view.funds.transform, new Vector2(1, 1));

            view.panel = SidePanel(rt, new[] { "접속", "흐름" }, out view.panelToggle, out view.panelToggleLabel, out view.tabButtons, out view.tabPages);
            var connect = (RectTransform)view.tabPages[0].transform;
            view.connection = Text("Connection", connect, "접속 상태", 24, 8, 512, 70, 16, Ink);
            Action(connect, "modeSession", "방 코드(세션)", 24, 84, 252, Line);
            Action(connect, "modeLan", "LAN(직접 IP)", 284, 84, 252, Line);
            var sessionGroup = Rect("Session Group", connect, 0, 140, 560, 140);
            Action(sessionGroup, "host", "방 만들기", 24, 0, 512, Teal);
            Text("CodeLabel", sessionGroup, "방 코드", 24, 54, 512, 22, 14, Muted);
            view.code = Input(sessionGroup, "Code", "", 24, 80, 380, 44);
            Action(sessionGroup, "join", "참가", 412, 80, 124, Teal);
            view.sessionGroup = sessionGroup.gameObject;
            var lanGroup = Rect("LAN Group", connect, 0, 140, 560, 140);
            Action(lanGroup, "lanHost", "방 만들기 (LAN Host)", 24, 0, 512, Teal);
            Text("AddressLabel", lanGroup, "주소 (IP:포트)", 24, 54, 512, 22, 14, Muted);
            view.address = Input(lanGroup, "Address", "127.0.0.1:7777", 24, 80, 380, 44);
            Action(lanGroup, "lanJoin", "참가 (LAN)", 412, 80, 124, Teal);
            view.lanGroup = lanGroup.gameObject;
            lanGroup.gameObject.SetActive(false);
            Text("ConnectedHeading", connect, "접속한 뒤", 24, 300, 512, 24, 15, Muted);
            Action(connect, "copy", "방 코드 복사", 24, 330, 512, Line);
            Action(connect, "startGame", "게임 시작 (방장)", 24, 382, 512, Teal);
            Action(connect, "endGame", "게임 종료 (호스트 판정 테스트)", 24, 434, 512, Line);
            Action(connect, "leave", "방 닫기 / 나가기", 24, 486, 512, Danger);

            var flow = (RectTransform)view.tabPages[1].transform;
            view.flow = Text("Flow", flow, "런 상태", 24, 8, 512, 90, 16, Ink);
            Action(flow, "offlineStart", "오프라인 런 시작 (접속 안 함)", 24, 110, 512, Teal);
            Action(flow, "intro", "도입 끝 → 공연", 24, 162, 512, Teal);
            Action(flow, "offlineEnd", "오프라인 런 끝", 24, 214, 512, Line);
            Text("SpaceHint", flow, "공간 안 신호(대기실 출구, 탈출 개시, 밤 준비)는 각 공간 화면의 왼쪽 패널 [조작]에 있습니다.", 24, 280, 512, 70, 15, Muted);
            view.actions = buttons.ToArray();
            var events = EventSystem("Hub EventSystem");

            Lobby();
            var serialized = new SerializedObject(hub);
            var ui = serialized.FindProperty("hubUi");
            ui.arraySize = 2;
            ui.GetArrayElementAtIndex(0).objectReferenceValue = canvas.gameObject;
            ui.GetArrayElementAtIndex(1).objectReferenceValue = events;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ReportMissing("SpaceLink");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(original) && original != ScenePath) EditorSceneManager.OpenScene(original);
            Debug.Log("[SpaceLink] 시작 씬 Canvas·EventSystem·더미 로비 저장, IMGUI 접속 패널 제거");
        }

        static void Action(Transform parent, string id, string label, float x, float y, float width, Color color) =>
            buttons.Add(new SpaceLinkHubCanvas.ActionButton { id = id, button = Button(parent, "Button-" + id, label, x, y, width, 44, color) });

        static void Lobby()
        {
            var env = new GameObject("Hub Environment").transform;
            var eye = new Vector3(0, 7.5f, -7.5f);
            FrameCamera(Camera.main, eye, new Vector3(0, 1, 6));
            Floor(env, -7.5f, 7.5f, 0, 10);
            Wall(env, true, 10, -7.5f, 7.5f, doorAt: 3);
            Prop(Props + "SM_Prop_Curtain_01", env, new Vector3(-4.2f, 0, 9.4f), 0, 1.6f, new Vector3(2.3f, 1.9f, 0.7f));
            Prop(Props + "SM_Prop_Curtain_01", env, new Vector3(4.2f, 0, 9.4f), 0, 1.6f, new Vector3(2.3f, 1.9f, 0.7f));
            Prop(Props + "SM_Prop_Floodlights_01", env, new Vector3(-6, 0, 6), 25, 0.6f, new Vector3(1.4f, 3.9f, 1.5f));
            Prop(Props + "SM_Prop_Floodlights_01", env, new Vector3(6, 0, 6), -25, 0.6f, new Vector3(1.4f, 3.9f, 1.5f));
            Prop(Props + "SM_Prop_Plank_Bench_01", env, new Vector3(0, 0, 3.5f), 90, 1, new Vector3(0.8f, 0.5f, 3.5f));
            AddLight(env, "Lobby Spot", LightType.Spot, new Vector3(0, 7, 4), new Vector3(70, 0, 0), Gold, 35, 16, 70);
            Sign(env, "CURTAIN CALL", new Vector3(1.25f, 4.6f, 9.7f), eye, 5f, Gold);
            Sign(env, "시작 로비 · 더미", new Vector3(1.25f, 3.5f, 9.7f), eye, 2.6f, Mint);
        }
    }
}
