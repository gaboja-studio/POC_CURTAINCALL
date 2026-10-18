using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static CurtainCall.StartLobby.EditorTools.StartLobbyKit;
using Object = UnityEngine.Object;

namespace CurtainCall.StartLobby.EditorTools
{
    /// <summary>
    /// 시작 씬(Start)과 로비 씬(Lobby)을 통째로 다시 짓고 저장한다(Task-052). 빌드 씬 목록 맨 앞(Start 0번, Lobby 1번)에 넣고,
    /// 장소 전환 세팅의 시작 씬을 Lobby로 맞춘다. 씬을 손으로 고치면 다음 빌드 때 사라지므로 바꿀 것은 이 코드에서 고친다.
    /// </summary>
    public static class StartLobbySceneBuilder
    {
        const string StartPath = Folder + "/Start.unity";
        const string LobbyPath = Folder + "/Lobby.unity";
        const string SettingsPath = "Assets/Resources/Settings/GameSettings/BaseGameSettings.asset";
        const string Lobby = "Lobby";

        [MenuItem("Tools/CurtainCall/Build Start·Lobby Scenes")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.isDirty) throw new InvalidOperationException("미저장 씬이 있습니다. 먼저 저장하거나 정리하세요.");
            }
            string original = SceneManager.GetActiveScene().path;
            Prepare();

            Build(StartPath, BuildStart);
            Build(LobbyPath, BuildLobby);
            RegisterScenes();
            SetHubScene();
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(original) && original != StartPath && original != LobbyPath) EditorSceneManager.OpenScene(original);
            Debug.Log("[StartLobby] 시작·로비 씬 저장, 빌드 목록(Start 0 · Lobby 1)·시작 씬 세팅(Lobby) 확인");
        }

        static void Build(string path, Action build)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // 다시 열 때 URP(조명·카메라 추가 데이터)·TMP가 채우는 값까지 저장해, 열기만 해도 바뀐 씬이 되지 않게 한다.
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ── 시작 씬 ─────────────────────────────────────────

        static void BuildStart()
        {
            Ambient(new Color(0.09f, 0.06f, 0.07f));
            MainCamera(new Vector3(0, 1.9f, -7f), new Vector3(1.4f, 2.1f, 6f), 42f);
            var moon = AddLight(null, "Directional Light", LightType.Directional, Vector3.zero, Vector3.zero, new Color(0.55f, 0.6f, 0.8f), 0.15f);
            moon.transform.rotation = Quaternion.Euler(45, -30, 0);
            Stage(Group("Start Stage", null, new Vector3(2f, 0, 0)));

            var canvas = Canvas("Start Canvas", out _);
            foreach (float width in new[] { 560f, 700f, 840f, 980f })
            {
                // 겹친 띠 4장으로 메뉴 뒤를 왼쪽부터 부드럽게 어둡게 한다.
                var shade = Rect("Shade", canvas, new Vector2(0, 0.5f), Vector2.zero, new Vector2(width, 0), new Vector2(0, 0.5f));
                shade.anchorMin = new Vector2(0, 0);
                shade.anchorMax = new Vector2(0, 1);
                Fill(shade, new Color(0, 0, 0, 0.16f));
            }
            var title = Text("Title", canvas, "CURTAIN CALL", new Vector2(0, 1), new Vector2(92, -96), new Vector2(980, 120), 96, Gold, TextAlignmentOptions.Left, new Vector2(0, 1));
            title.characterSpacing = 6;
            title.fontStyle = FontStyles.Bold;
            Text("Subtitle", canvas, "커튼 콜  ·  막이 내리기 전에 살아남아라", new Vector2(0, 1), new Vector2(98, -214), new Vector2(900, 40), 26, Cream, TextAlignmentOptions.Left, new Vector2(0, 1));
            Rule(canvas, new Vector2(0, 1), new Vector2(98, -270), 420);

            var view = canvas.gameObject.AddComponent<StartMenu>();
            var main = Rect("Main", canvas, new Vector2(0, 0.5f), new Vector2(80, -40), new Vector2(520, 360), new Vector2(0, 0.5f));
            view.mainPage = main.gameObject;
            string[] labels = { "방 만들기", "방 코드로 참가", "LAN으로 하기", "설정", "종료" };
            var buttons = labels.Select((label, i) => MenuButton("Button " + label, main, label, new Vector2(0, 1), new Vector2(0, -i * 66), 460, 30, new Vector2(0, 1))).ToArray();
            view.host = buttons[0];
            view.join = buttons[1];
            view.lan = buttons[2];
            view.settings = buttons[3];
            view.quit = buttons[4];

            var join = Page(canvas, "Join", "방 코드로 참가", 300);
            view.joinPage = join.gameObject;
            view.code = Input("Code", join, "", "방 코드 (예: ABC123)", new Vector2(0, 1), new Vector2(32, -100), new Vector2(496, 58), new Vector2(0, 1));
            view.code.characterLimit = 12;
            view.joinConfirm = SolidButton("Confirm", join, "참가", new Vector2(0, 1), new Vector2(32, -184), new Vector2(240, 56), Crimson, 24, new Vector2(0, 1));
            view.joinBack = SolidButton("Back", join, "뒤로", new Vector2(0, 1), new Vector2(288, -184), new Vector2(240, 56), VelvetDark, 24, new Vector2(0, 1));

            var lan = Page(canvas, "LAN", "LAN으로 하기", 380);
            view.lanPage = lan.gameObject;
            view.lanHost = SolidButton("Host", lan, "LAN 방 만들기", new Vector2(0, 1), new Vector2(32, -96), new Vector2(496, 56), Crimson, 24, new Vector2(0, 1));
            Text("AddressLabel", lan, "주소 (IP:포트)  ·  같은 PC는 127.0.0.1:7777", new Vector2(0, 1), new Vector2(32, -174), new Vector2(496, 30), 18, Dim, TextAlignmentOptions.Left, new Vector2(0, 1));
            view.address = Input("Address", lan, "127.0.0.1:7777", "IP:포트", new Vector2(0, 1), new Vector2(32, -208), new Vector2(330, 58), new Vector2(0, 1));
            view.lanJoin = SolidButton("Join", lan, "참가", new Vector2(0, 1), new Vector2(378, -209), new Vector2(150, 56), Crimson, 24, new Vector2(0, 1));
            view.lanBack = SolidButton("Back", lan, "뒤로", new Vector2(0, 1), new Vector2(32, -296), new Vector2(496, 52), VelvetDark, 22, new Vector2(0, 1));

            view.settingsPage = StartLobbyKit.Settings(canvas);
            view.status = Text("Status", canvas, "", new Vector2(0, 0), new Vector2(98, 72), new Vector2(1000, 40), 22, Gold, TextAlignmentOptions.Left, new Vector2(0, 0));
            Text("Footer", canvas, "POC  ·  2~4인 협동", new Vector2(1, 0), new Vector2(-40, 30), new Vector2(400, 30), 16, Dim, TextAlignmentOptions.Right, new Vector2(1, 0));

            DummySpaces.EditorTools.DummyUiKit.EventSystem("Start EventSystem");
        }

        /// <summary>메뉴 옆에 뜨는 어두운 패널(방 코드·LAN).</summary>
        static RectTransform Page(Transform canvas, string name, string heading, float height)
        {
            var panel = Rect(name, canvas, new Vector2(0, 0.5f), new Vector2(92, -40), new Vector2(560, height), new Vector2(0, 0.5f));
            Fill(panel, Glass, true);
            Rule(panel, new Vector2(0.5f, 1f), new Vector2(0, -2), 560);
            Text("Heading", panel, heading, new Vector2(0, 1), new Vector2(32, -28), new Vector2(496, 46), 30, Gold, TextAlignmentOptions.Left, new Vector2(0, 1));
            panel.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>시작 씬 배경: 막이 내려간 빈 무대, 가운데 의자 하나에 핀 조명.</summary>
        static void Stage(Transform root)
        {
            var wood = Mat("stage-wood", new Color(0.36f, 0.19f, 0.09f), 0.45f);
            var velvet = Mat("velvet", Velvet, 0.18f);
            var shade = Mat("velvet-shade", VelvetDark, 0.12f);
            var gold = Mat("gold", Gold, 0.6f, 0.8f);

            Shape("Stage Floor", root, PrimitiveType.Cube, new Vector3(0, 0.3f, 4f), new Vector3(15f, 0.6f, 7.5f), wood);
            Shape("Stage Lip", root, PrimitiveType.Cube, new Vector3(0, 0.62f, 0.27f), new Vector3(15f, 0.06f, 0.1f), gold);
            Curtain("Back Curtain", root, new Vector3(0, 0.6f, 7.4f), 15f, 7f, velvet, shade);
            Curtain("Drape Left", root, new Vector3(-6.4f, 0.6f, 4.6f), 2.6f, 7f, velvet, shade);
            Curtain("Drape Right", root, new Vector3(6.4f, 0.6f, 4.6f), 2.6f, 7f, velvet, shade);
            Shape("Valance", root, PrimitiveType.Cube, new Vector3(0, 7.3f, 1.2f), new Vector3(16f, 1.6f, 0.4f), velvet);
            Shape("Valance Fringe", root, PrimitiveType.Cube, new Vector3(0, 6.48f, 1f), new Vector3(16f, 0.12f, 0.12f), gold);

            Prop(Props + "SM_Prop_Chair_Folding_01", root, new Vector3(0.4f, 0.6f, 3.6f), 200f, 1.2f, fallback: new Vector3(0.5f, 0.9f, 0.5f));
            var bulb = Mat("bulb", new Color(1f, 0.9f, 0.7f), 0.5f, 0f, new Color(3f, 2.3f, 1.3f));
            for (float x = -6.8f; x <= 6.8f; x += 0.85f)
                Shape("Footlight", root, PrimitiveType.Sphere, new Vector3(x, 0.66f, 0.42f), Vector3.one * 0.11f, bulb);
            Prop(Props + "SM_Prop_FlagLine_01", root, new Vector3(0, 6.1f, 2.2f), 0f, 2.2f);

            AddLight(root, "Pin Spot", LightType.Spot, new Vector3(0.4f, 9.5f, 1.2f), new Vector3(0.4f, 0.6f, 3.6f), new Color(1f, 0.9f, 0.72f), 90f, 22f, 26f, true);
            AddLight(root, "Rim Left", LightType.Spot, new Vector3(-8f, 6f, 0f), new Vector3(-1f, 2f, 7f), new Color(1f, 0.25f, 0.3f), 40f, 20f, 50f);
            AddLight(root, "Rim Right", LightType.Spot, new Vector3(8f, 6f, 0f), new Vector3(1f, 2f, 7f), new Color(1f, 0.55f, 0.25f), 32f, 20f, 50f);
            AddLight(root, "Footlights", LightType.Point, new Vector3(0, 0.9f, 0.2f), new Vector3(0, 0.9f, 0.2f), new Color(1f, 0.75f, 0.45f), 3f, 9f);
        }

        // ── 로비 씬 ─────────────────────────────────────────

        static void BuildLobby()
        {
            Ambient(new Color(0.16f, 0.11f, 0.11f));
            var camera = MainCamera(new Vector3(0, 6.4f, -5.4f), new Vector3(0, 0.4f, 3.4f), 40f);
            camera.gameObject.AddComponent<LobbyCamera>();
            var moon = AddLight(null, "Directional Light", LightType.Directional, Vector3.zero, Vector3.zero, new Color(0.65f, 0.6f, 0.75f), 0.35f, shadows: true);
            moon.transform.rotation = Quaternion.Euler(55, -25, 0);

            var env = Group("Lobby Environment", null);
            Room(env, camera.transform.position, out var board, out var lever, out var curtainLeft, out var curtainRight);

            var canvas = Canvas("Lobby Canvas", out _);
            var room = new GameObject("Lobby Room").AddComponent<LobbyRoom>();
            Hud(canvas, room);
            room.interactables = new LobbyInteractable[] { board, lever };
            room.curtainLeft = curtainLeft;
            room.curtainRight = curtainRight;
            room.curtainTravel = 2.0f;

            var events = DummySpaces.EditorTools.DummyUiKit.EventSystem("Lobby EventSystem");
            var hub = new GameObject("SpaceLink Hub").AddComponent<SpaceLinkHub>();
            var serialized = new SerializedObject(hub);
            var ui = serialized.FindProperty("hubUi");
            var hidden = new[] { canvas.gameObject, events, env.gameObject };
            ui.arraySize = hidden.Length;
            for (int i = 0; i < hidden.Length; i++) ui.GetArrayElementAtIndex(i).objectReferenceValue = hidden[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>단원 천막: 뒤쪽 가운데 무대 입구(커튼), 왼쪽 출연 명단, 오른쪽 커튼 레버. 바닥 10×8m, 보이지 않는 벽으로 막는다.</summary>
        static void Room(Transform env, Vector3 eye, out LobbyReadyBoard board, out LobbyCurtainLever lever, out Transform curtainLeft, out Transform curtainRight)
        {
            var wood = Mat("floor-wood", new Color(0.33f, 0.21f, 0.13f), 0.3f);
            var tent = Mat("tent", VelvetDark, 0.1f);
            var velvet = Mat("velvet", Velvet, 0.18f);
            var shade = Mat("velvet-shade", VelvetDark, 0.12f);
            var cream = Mat("tent-cream", new Color(0.86f, 0.78f, 0.62f), 0.1f);
            var gold = Mat("gold", Gold, 0.6f, 0.8f);
            var glow = Mat("stage-glow", new Color(1f, 0.85f, 0.6f), 0.2f, 0f, new Color(2.2f, 1.6f, 0.9f));
            var bulb = Mat("bulb", new Color(1f, 0.9f, 0.7f), 0.5f, 0f, new Color(3f, 2.3f, 1.3f));

            // 바닥·벽
            Shape("Floor", env, PrimitiveType.Cube, new Vector3(0, -0.1f, 3f), new Vector3(10.4f, 0.2f, 8.4f), wood, solid: true);
            Prop(Props + "SM_Prop_Rug_02", env, new Vector3(0, 0.01f, 2.6f), 0f, 1.7f);
            Shape("Back Wall Left", env, PrimitiveType.Cube, new Vector3(-3.6f, 2f, 7.25f), new Vector3(3.2f, 4f, 0.3f), tent, solid: true);
            Shape("Back Wall Right", env, PrimitiveType.Cube, new Vector3(3.6f, 2f, 7.25f), new Vector3(3.2f, 4f, 0.3f), tent, solid: true);
            Shape("Back Wall Top", env, PrimitiveType.Cube, new Vector3(0, 3.5f, 7.25f), new Vector3(4f, 1f, 0.3f), tent, solid: true);
            Shape("Arch Left", env, PrimitiveType.Cube, new Vector3(-2.05f, 1.5f, 7.05f), new Vector3(0.16f, 3f, 0.16f), gold);
            Shape("Arch Right", env, PrimitiveType.Cube, new Vector3(2.05f, 1.5f, 7.05f), new Vector3(0.16f, 3f, 0.16f), gold);
            Shape("Arch Top", env, PrimitiveType.Cube, new Vector3(0, 3.02f, 7.05f), new Vector3(4.26f, 0.16f, 0.16f), gold);
            Shape("Stage Glow", env, PrimitiveType.Cube, new Vector3(0, 1.5f, 7.6f), new Vector3(4f, 3f, 0.1f), glow);
            AddLight(env, "Stage Light", LightType.Point, new Vector3(0, 1.6f, 7.45f), new Vector3(0, 1.6f, 7.45f), new Color(1f, 0.8f, 0.55f), 6f, 3f);
            curtainLeft = Curtain("Curtain Left", env, new Vector3(-1f, 0, 6.92f), 2.1f, 3f, velvet, shade);
            curtainRight = Curtain("Curtain Right", env, new Vector3(1f, 0, 6.92f), 2.1f, 3f, velvet, shade);
            Label3D("Sign Stage", env, "무대 입구", new Vector3(0, 3.5f, 7.06f), eye, 5f, Gold);

            Stripes(env, -5.15f, tent, cream);
            Stripes(env, 5.15f, tent, cream);
            Rope(env, gold, velvet);

            // 보이지 않는 벽(맵 밖으로 못 나감)
            Blocker("Bound Left", env, new Vector3(-5.4f, 1.5f, 3f), new Vector3(0.6f, 4f, 9f));
            Blocker("Bound Right", env, new Vector3(5.4f, 1.5f, 3f), new Vector3(0.6f, 4f, 9f));
            Blocker("Bound Back", env, new Vector3(0, 1.5f, 7.7f), new Vector3(11f, 4f, 0.6f));
            Blocker("Bound Front", env, new Vector3(0, 1.5f, -1.5f), new Vector3(11f, 4f, 0.6f));

            // 소품
            Prop(GenericProps + "SM_Gen_Prop_Barrel_Wood_01", env, new Vector3(-4.4f, 0, 6.4f), 20f, 1f, true, new Vector3(0.7f, 1f, 0.7f));
            Prop(GenericProps + "SM_Gen_Prop_Barrel_Wood_02", env, new Vector3(-4.45f, 0, 5.5f), 70f, 1f, true, new Vector3(0.7f, 1f, 0.7f));
            Prop(GenericProps + "SM_Gen_Prop_Crate_01", env, new Vector3(4.3f, 0, -0.1f), 10f, 1f, true, new Vector3(0.9f, 0.9f, 0.9f));
            Prop(GenericProps + "SM_Gen_Prop_Crate_02", env, new Vector3(4.35f, 0, 0.9f), -15f, 0.9f, true, new Vector3(0.9f, 0.9f, 0.9f));
            Prop(Props + "SM_Prop_Plank_Bench_01", env, new Vector3(-4.3f, 0, 1.6f), 90f, 1f, true, new Vector3(0.8f, 0.5f, 3.5f));
            Prop(Props + "SM_Prop_Lamp_01", env, new Vector3(4.4f, 0, 3.4f), -90f, 1f, false, new Vector3(0.4f, 1.6f, 0.4f));
            Prop(GenericProps + "SM_Gen_Prop_Barrel_Wood_03", env, new Vector3(4.45f, 0, 5.4f), -40f, 1f, true, new Vector3(0.7f, 1f, 0.7f));
            foreach (var x in new[] { -3.6f, -1.2f, 1.2f, 3.6f })
            {
                Shape("Bulb", env, PrimitiveType.Sphere, new Vector3(x, 3.45f, 2.6f), Vector3.one * 0.16f, bulb);
                Shape("Cord", env, PrimitiveType.Cylinder, new Vector3(x, 4.3f, 2.6f), new Vector3(0.015f, 0.8f, 0.015f), Mat("wire", new Color(0.08f, 0.06f, 0.05f)));
                AddLight(env, "Bulb Light", LightType.Point, new Vector3(x, 3.3f, 2.6f), new Vector3(x, 3.3f, 2.6f), new Color(1f, 0.78f, 0.5f), 1.8f, 6f);
            }
            AddLight(env, "Key Spot", LightType.Spot, new Vector3(0, 7f, 0.5f), new Vector3(0, 0, 3f), new Color(1f, 0.92f, 0.8f), 28f, 16f, 75f, true);

            board = ReadyBoard(env, new Vector3(-3.3f, 0, 4.6f), eye, cream, gold);
            lever = Lever(env, new Vector3(3.2f, 0, 6.0f), eye, gold, velvet, board);

            // 출발 위치(자리 번호 0~3): 무대 입구를 바라본다.
            var spawns = Group("Spawn Points", env);
            var points = new Transform[4];
            for (int i = 0; i < points.Length; i++) points[i] = Group("Spawn " + i, spawns, new Vector3(-1.8f + i * 1.2f, 0.05f, 1.4f));
            var spawnPoints = spawns.gameObject.AddComponent<PlayerSpawnPoints>();
            var serialized = new SerializedObject(spawnPoints);
            var list = serialized.FindProperty("points");
            list.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>옆 천막 벽: 빨강·크림 세로 줄무늬.</summary>
        static void Stripes(Transform env, float x, Material dark, Material light)
        {
            var root = Group(x < 0 ? "Tent Left" : "Tent Right", env);
            int i = 0;
            for (float z = -1f; z < 7f - 0.01f; z += 0.8f, i++)
                Shape("Stripe", root, PrimitiveType.Cube, new Vector3(x, 1.3f, z + 0.4f), new Vector3(0.2f, 2.6f, 0.8f), i % 2 == 0 ? dark : light, solid: true);
        }

        /// <summary>앞쪽 낮은 줄 울타리(맵 끝 표시).</summary>
        static void Rope(Transform env, Material post, Material rope)
        {
            var root = Group("Front Rope", env);
            for (int i = 0; i <= 4; i++)
                Shape("Post", root, PrimitiveType.Cylinder, new Vector3(-4.6f + i * 2.3f, 0.45f, -0.95f), new Vector3(0.1f, 0.45f, 0.1f), post);
            Shape("Rope", root, PrimitiveType.Cylinder, new Vector3(0, 0.78f, -0.95f), new Vector3(0.05f, 4.6f, 0.05f), rope, euler: new Vector3(0, 0, 90));
        }

        static LobbyReadyBoard ReadyBoard(Transform env, Vector3 position, Vector3 eye, Material paper, Material gold)
        {
            var frame = Mat("board-frame", new Color(0.2f, 0.12f, 0.08f), 0.3f);
            var root = Group("Ready Board", env, position, -22f); // 앞면(-z)이 카메라 쪽
            Shape("Leg Left", root, PrimitiveType.Cylinder, new Vector3(-0.8f, 0.6f, 0.05f), new Vector3(0.08f, 0.6f, 0.08f), frame);
            Shape("Leg Right", root, PrimitiveType.Cylinder, new Vector3(0.8f, 0.6f, 0.05f), new Vector3(0.08f, 0.6f, 0.08f), frame);
            Shape("Frame", root, PrimitiveType.Cube, new Vector3(0, 1.75f, 0.04f), new Vector3(2.2f, 1.7f, 0.08f), frame, solid: true);
            Shape("Paper", root, PrimitiveType.Cube, new Vector3(0, 1.75f, -0.01f), new Vector3(2.02f, 1.52f, 0.03f), paper);
            Shape("Pin", root, PrimitiveType.Sphere, new Vector3(0, 2.45f, -0.04f), Vector3.one * 0.08f, gold);

            Vector3 front = root.TransformPoint(new Vector3(0, 1.75f, -6f));
            var ink = new Color(0.16f, 0.08f, 0.06f);
            Label3D("Heading", root, "출연 명단", new Vector3(0, 2.3f, -0.04f), front, 2.6f, new Color(0.55f, 0.04f, 0.1f));
            var view = root.gameObject.AddComponent<LobbyReadyBoard>();
            view.rows = new TextMeshPro[4];
            for (int i = 0; i < 4; i++)
                view.rows[i] = Label3D("Row " + i, root, $"{i + 1}.  빈 자리", new Vector3(0.02f, 2.05f - i * 0.24f, -0.04f), front, 1.6f, ink, TextAlignmentOptions.Left, 1.8f, 0.25f);
            view.summary = Label3D("Summary", root, "서명 0 / 0", new Vector3(0.45f, 1.08f, -0.04f), front, 1.3f, new Color(0.4f, 0.3f, 0.25f), TextAlignmentOptions.Right, 0.9f, 0.2f);
            AddLight(env, "Board Spot", LightType.Spot, position + new Vector3(2.2f, 4.2f, -2f), position + new Vector3(0, 1.6f, 0), new Color(1f, 0.92f, 0.78f), 22f, 9f, 38f);
            return view;
        }

        static LobbyCurtainLever Lever(Transform env, Vector3 position, Vector3 eye, Material gold, Material knob, LobbyReadyBoard board)
        {
            var metal = Mat("lever-metal", new Color(0.16f, 0.15f, 0.16f), 0.55f, 0.7f);
            var root = Group("Curtain Lever", env, position, 15f);
            Shape("Base", root, PrimitiveType.Cube, new Vector3(0, 0.5f, 0), new Vector3(0.55f, 1f, 0.4f), metal, solid: true);
            Shape("Plate", root, PrimitiveType.Cube, new Vector3(0, 1.02f, 0), new Vector3(0.62f, 0.05f, 0.46f), gold);
            var handle = Group("Handle", root, new Vector3(0, 1.05f, 0));
            handle.localRotation = Quaternion.Euler(-25f, 0, 0);
            Shape("Arm", handle, PrimitiveType.Cylinder, new Vector3(0, 0.36f, 0), new Vector3(0.07f, 0.36f, 0.07f), gold);
            Shape("Knob", handle, PrimitiveType.Sphere, new Vector3(0, 0.76f, 0), Vector3.one * 0.18f, knob);
            Label3D("Sign", root, "커튼 레버", new Vector3(0, 2.1f, 0), eye, 2.4f, Gold);
            AddLight(env, "Lever Spot", LightType.Spot, position + new Vector3(-1.2f, 4.2f, -2.4f), position + new Vector3(0, 1f, 0), new Color(1f, 0.55f, 0.4f), 20f, 9f, 32f);

            var view = root.gameObject.AddComponent<LobbyCurtainLever>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("board").objectReferenceValue = board;
            serialized.FindProperty("handle").objectReferenceValue = handle;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static void Hud(RectTransform canvas, LobbyRoom room)
        {
            var info = Rect("Room Info", canvas, new Vector2(0, 1), new Vector2(32, -28), new Vector2(540, 100), new Vector2(0, 1));
            Fill(info, Glass);
            Rule(info, new Vector2(0.5f, 1), new Vector2(0, -2), 540);
            room.roomCode = Text("Room Code", info, "연결 중...", new Vector2(0, 1), new Vector2(24, -16), new Vector2(390, 40), 24, Cream, TextAlignmentOptions.Left, new Vector2(0, 1));
            room.copyCode = SolidButton("Copy", info, "복사", new Vector2(1, 1), new Vector2(-18, -16), new Vector2(96, 40), VelvetDark, 18, new Vector2(1, 1));
            room.players = Text("Players", info, "", new Vector2(0, 1), new Vector2(24, -60), new Vector2(490, 30), 20, Dim, TextAlignmentOptions.Left, new Vector2(0, 1));

            var back = Rect("Banner Back", canvas, new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(1600, 156), new Vector2(0.5f, 1));
            back.anchorMin = new Vector2(0, 1);
            back.anchorMax = new Vector2(1, 1);
            Fill(back, new Color(0.03f, 0.015f, 0.02f, 0.72f));
            back.gameObject.SetActive(false);
            room.bannerBack = back.gameObject;
            room.banner = Text("Banner", canvas, "", new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1100, 84), 58, Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1));
            room.banner.fontStyle = FontStyles.Bold;
            room.bannerSub = Text("Banner Sub", canvas, "", new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(1100, 40), 24, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 1));
            room.prompt = Text("Prompt", canvas, "", new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(1000, 50), 30, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 0));
            room.prompt.fontStyle = FontStyles.Bold;
            Text("Hint", canvas, "WASD 이동   ·   Space 점프   ·   F 상호작용   ·   ESC 메뉴", new Vector2(0, 0), new Vector2(32, 26), new Vector2(1000, 30), 18, Dim, TextAlignmentOptions.Left, new Vector2(0, 0));
            Text("Goal", canvas, "모두 출연 명단에 서명하면 방장이 커튼 레버를 당겨 공연을 시작합니다", new Vector2(1, 0), new Vector2(-32, 26), new Vector2(900, 30), 18, Dim, TextAlignmentOptions.Right, new Vector2(1, 0));

            var menu = Stretch("Menu", canvas);
            Fill(menu, new Color(0, 0, 0, 0.6f), true);
            var panel = Rect("Panel", menu, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 400));
            Fill(panel, Glass, true);
            Rule(panel, new Vector2(0.5f, 1), new Vector2(0, -2), 460);
            Text("Heading", panel, "메뉴", new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(400, 50), 36, Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1));
            room.resume = SolidButton("Resume", panel, "계속하기", new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(340, 58), Crimson, 24, new Vector2(0.5f, 1));
            room.openSettings = SolidButton("Settings", panel, "설정", new Vector2(0.5f, 1), new Vector2(0, -192), new Vector2(340, 58), VelvetDark, 24, new Vector2(0.5f, 1));
            room.leave = SolidButton("Leave", panel, "방 나가기", new Vector2(0.5f, 1), new Vector2(0, -266), new Vector2(340, 58), VelvetDark, 24, new Vector2(0.5f, 1));
            room.leaveLabel = room.leave.GetComponentInChildren<TMP_Text>();
            room.menu = menu.gameObject;
            room.settings = StartLobbyKit.Settings(canvas);
        }

        // ── 프로젝트 설정 ────────────────────────────────────

        /// <summary>빌드 목록 맨 앞에 Start(0)·Lobby(1)를 두고 나머지 순서는 그대로 둔다.</summary>
        static void RegisterScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new(StartPath, true),
                new(LobbyPath, true),
            };
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != StartPath && s.path != LobbyPath));
            if (EditorBuildSettings.scenes.Select(s => s.path).Take(2).SequenceEqual(new[] { StartPath, LobbyPath })
                && EditorBuildSettings.scenes.Take(2).All(s => s.enabled)) return;
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>장소 전환 세팅의 시작 씬(도입·엔딩·런 전에 머무는 곳)을 로비로.</summary>
        static void SetHubScene()
        {
            var settings = AssetDatabase.LoadAssetAtPath<BaseGameSettings>(SettingsPath);
            if (settings == null) throw new InvalidOperationException("공통 세팅이 없습니다: " + SettingsPath);
            var serialized = new SerializedObject(settings);
            var hub = serialized.FindProperty("spaceTransition.hubScene");
            if (hub.stringValue == Lobby) return;
            hub.stringValue = Lobby;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
