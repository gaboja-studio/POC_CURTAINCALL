using System;
using System.Linq;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.SpaceLink;
using CurtainCall.StartLobby;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class EndingIntegrationProbe
{
    public static object FinishScenery()
    {
        if (Application.isPlaying) throw new Exception("Edit mode only");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Levels/escape_route.unity");
        var rig = scene.GetRootGameObjects().Single(r => r.name == "EscapeCampaignFrame").transform.Find("Ending cinematic scenery");
        var moon = rig.Find("Moonlight").GetComponent<Light>();
        moon.intensity = 1.3f; moon.transform.localRotation = Quaternion.Euler(35,-90,0);
        var roof = rig.Find("Distant circus roof");
        roof.localPosition = new Vector3(320,roof.localPosition.y,78);
        if (rig.Find("Distant circus wall") == null)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wall.name = "Distant circus wall";
            wall.transform.SetParent(rig,false); wall.transform.localPosition = new Vector3(320,4,78);
            wall.transform.localScale = new Vector3(44,4,44);
            wall.GetComponent<MeshRenderer>().sharedMaterial = roof.GetComponent<MeshRenderer>().sharedMaterials[0];
            UnityEngine.Object.DestroyImmediate(wall.GetComponent<Collider>());
        }
        rig.Find("Circus warm glow").localPosition = new Vector3(344,10,78);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Saved moon fill and grounded distant circus";
    }
    public static object PrepareStart()
    {
        SessionState.SetBool("EndingProbe.DevBefore", DevMode.Enabled);
        DevMode.SetEditorEnabled(true);
        EditorSceneManager.OpenScene("Assets/Scenes/InGame/Start.unity");
        return "Start scene for real host/lobby/escape transition";
    }
    static async System.Threading.Tasks.Task Until(Func<bool> ready)
    {
        double limit = Time.realtimeSinceStartupAsDouble + 50;
        while (!ready())
        {
            if (Time.realtimeSinceStartupAsDouble > limit) throw new Exception("Timed out: " + JsonUtility.ToJson(Inspect()));
            await System.Threading.Tasks.Task.Delay(100);
        }
    }
    public static async System.Threading.Tasks.Task<object> HostAndEscape()
    {
        var session = NetworkSessionManager.EnsureExists();
        var flow = StartLobbyFlow.EnsureExists();
        await System.Threading.Tasks.Task.Delay(500);
        using (session.RequestMinimumStartPlayers(1))
        {
            if (!await session.HostAsync(new LanSessionConnector(session.NetworkManager, 7794))) throw new Exception("Host failed");
            if (!flow.OpenLobbyAsHost()) throw new Exception("Open lobby rejected");
            await Until(() => SceneManager.GetActiveScene().name == "Lobby" && NetworkPlayer.Local != null
                && !CurtainCall.UI.SceneIntegration.SceneCurtainTransition.IsActive);
            if (!session.StartGame()) throw new Exception("Start game rejected");
            SetupRun(); await System.Threading.Tasks.Task.Delay(100);
            if (!CampaignRun.Instance.DebugStartAt(new RunStartPoint(1,RunStartPhase.Escape,0,"Tightrope"))) throw new Exception("Escape start rejected");
            await Until(() => SceneManager.GetActiveScene().name == "escape_route" && !EscapeDevelopmentSession.AwaitingEntrance
                && !SpaceLinkDirector.Instance.IsTransitioning && !CurtainCall.UI.SceneIntegration.SceneCurtainTransition.IsActive);
            return Inspect();
        }
    }
    public static object RepeatSnapshot()
    {
        var results = EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        string json = JsonUtility.ToJson(typeof(EscapeEndingResults).GetField("snapshot", flags).GetValue(results));
        typeof(EscapeEndingResults).GetMethod("Receive", flags).Invoke(results, new object[] {json});
        results.Show(true);
        return new { statisticsStillSelected = View.StatisticsSelected };
    }
    public static object BindResultCanvas()
    {
        var result = UnityEngine.Object.FindFirstObjectByType<EscapeEndingResults>();
        var data = new SerializedObject(result);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/EscapeResultsKit/EscapeResultsCanvas.prefab");
        if (prefab.GetComponentInChildren<CurtainCall.UI.EscapeResultsKit.EscapeResultsView>(true) == null) throw new Exception("Missing view");
        data.FindProperty("prefab").objectReferenceValue = prefab;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(result.gameObject.scene); EditorSceneManager.SaveScene(result.gameObject.scene);
        return "Canvas root and child view verified and saved";
    }
    public static object Prepare()
    {
        SessionState.SetBool("EndingProbe.DevBefore", DevMode.Enabled);
        DevMode.SetEditorEnabled(true);
        EditorSceneManager.OpenScene("Assets/Scenes/Levels/escape_route.unity");
        return "Actual escape scene ready";
    }
    public static object SetupRun()
    {
        CampaignRun.EnsureExists(); SpaceLinkDirector.EnsureExists(); PerformanceRewards.EnsureExists();
        return Inspect();
    }
    public static async System.Threading.Tasks.Task<object> BeginFlow()
    {
        double limit = Time.realtimeSinceStartupAsDouble + 25;
        while (NetworkPlayer.Local == null || EscapeDevelopmentSession.AwaitingEntrance)
        { if (Time.realtimeSinceStartupAsDouble > limit) throw new Exception("Player not ready"); await System.Threading.Tasks.Task.Delay(100); }
        SetupRun(); await System.Threading.Tasks.Task.Delay(500);
        if (!CampaignRun.Instance.DebugStartAt(new RunStartPoint(1,RunStartPhase.Escape,0,"Tightrope"))) throw new Exception("Escape start failed");
        await System.Threading.Tasks.Task.Delay(500);
        NetworkPlayer.Local.ServerSetState(PlayerState.Arrived);
        if (!CampaignRun.Instance.EndEscape(true)) throw new Exception("Escape end failed");
        await System.Threading.Tasks.Task.Delay(7500);
        return Inspect();
    }
    public static object StartEscape()
    {
        bool started = CampaignRun.Instance.DebugStartAt(new RunStartPoint(1,RunStartPhase.Escape,0,"Tightrope"));
        return new { started, state = Inspect() };
    }
    public static object Arrive()
    {
        NetworkPlayer.Local.ServerSetState(PlayerState.Arrived);
        return new { ended = CampaignRun.Instance.EndEscape(true), state = Inspect() };
    }
    public static async System.Threading.Tasks.Task<object> CaptureEnding()
    {
        DevMode.SetEditorEnabled(false);
        Arrive();
        await System.Threading.Tasks.Task.Delay(8000);
        Capture("Tasks/Active/Task-20261112-120-escape-ending-concept/Previews/actual-ending-players.png");
        var players = Inspect();
        Statistics();
        await System.Threading.Tasks.Task.Delay(500);
        Capture("Tasks/Active/Task-20261112-120-escape-ending-concept/Previews/actual-ending-statistics.png");
        return new { players, repeatedSnapshot = RepeatSnapshot() };
    }
    static void Click(GameObject target) => ExecuteEvents.Execute(target,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    static CurtainCall.UI.EscapeResultsKit.EscapeResultsView View => EscapeEndingPresentation.Current.GetComponent<EscapeEndingResults>().View;
    public static object Statistics() { Click(View.StatisticsTab.gameObject); return View.StatisticsSelected; }
    public static object Players() { Click(View.PlayersTab.gameObject); return !View.StatisticsSelected; }
    public static object Capture(string path)
    {
        var camera = Camera.main; var canvas = View.GetComponentInParent<Canvas>();
        var mode = canvas.renderMode; var world = canvas.worldCamera; var plane = canvas.planeDistance;
        var target = camera.targetTexture; var active = RenderTexture.active; var aspect = camera.aspect;
        var rt = new RenderTexture(1920,1080,24); var image = new Texture2D(1920,1080,TextureFormat.RGBA32,false);
        try
        {
            camera.targetTexture = rt; camera.aspect = 1920f/1080;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
            System.IO.File.WriteAllBytes(path,image.EncodeToPNG()); return path;
        }
        finally
        {
            camera.targetTexture = target; camera.aspect = aspect; RenderTexture.active = active;
            canvas.renderMode = mode; canvas.worldCamera = world; canvas.planeDistance = plane;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
        }
    }
    public static object Return() { Click(View.ReturnButton.gameObject); return Inspect(); }
    public static async System.Threading.Tasks.Task<object> ReturnAndInput()
    {
        Return();
        await Until(() => SceneManager.GetActiveScene().name == "Lobby" && !SpaceLinkDirector.Instance.IsTransitioning
            && !CurtainCall.UI.SceneIntegration.SceneCurtainTransition.IsActive);
        await System.Threading.Tasks.Task.Delay(200);
        var returned = Inspect();
        if (NetworkPlayer.Local.State != PlayerState.Normal || InputGate.IsBlocked) throw new Exception("Lobby control not restored");
        var origin = NetworkPlayer.Local.transform.position;
        PressMove();
        await System.Threading.Tasks.Task.Delay(500);
        ReleaseMove();
        float distance = Vector3.Distance(origin, NetworkPlayer.Local.transform.position);
        if (distance < .25f) throw new Exception("Keyboard movement failed");
        var moved = Inspect();
        var lever = await UseLobbyLever();
        if (CampaignRun.Instance.State.Phase != CampaignPhase.None || NetworkSessionManager.Instance.GameState != GameSessionState.Waiting)
            throw new Exception("Lever interaction failed");
        return new { returned, distance, moved, lever };
    }
    public static object PressMove()
    {
        var keyboard = InputSystem.devices.OfType<Keyboard>().FirstOrDefault(k => k.name == "EndingProbeKeyboard")
            ?? InputSystem.AddDevice<Keyboard>("EndingProbeKeyboard");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
        return Inspect();
    }
    public static object ReleaseMove()
    {
        var keyboard = InputSystem.devices.OfType<Keyboard>().FirstOrDefault(k => k.name == "EndingProbeKeyboard");
        if (keyboard != null) InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        return Inspect();
    }
    public static async System.Threading.Tasks.Task<object> UseLobbyLever()
    {
        var lever = UnityEngine.Object.FindFirstObjectByType<LobbyCurtainLever>();
        NetworkPlayer.Local.GetComponent<PlayerMover>().Relocate(lever.transform.position + new Vector3(0,.05f,-1.2f), Quaternion.identity);
        await System.Threading.Tasks.Task.Delay(300);
        var before = Inspect();
        var keyboard = InputSystem.devices.OfType<Keyboard>().First(k => k.name == "EndingProbeKeyboard");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));
        await System.Threading.Tasks.Task.Delay(100);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        await System.Threading.Tasks.Task.Delay(500);
        return new { before, after = Inspect() };
    }
    public static object Inspect()
    {
        var local = NetworkPlayer.Local;
        var mover = local != null ? local.GetComponent<PlayerMover>() : null;
        var presentation = EscapeEndingPresentation.Current;
        var results = presentation != null ? presentation.GetComponent<EscapeEndingResults>() : null;
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        return new { playing = Application.isPlaying, scene = SceneManager.GetActiveScene().name,
            phase = CampaignRun.Instance?.State.Phase.ToString(), session = NetworkSessionManager.Instance?.GameState.ToString(),
            playerState = local != null ? local.State.ToString() : null, position = local != null ? local.transform.position.ToString("F3") : null,
            control = mover != null && mover.ControlEnabled, simulated = mover != null && mover.Simulated,
            moving = mover != null && mover.IsMoving, sprint = mover != null && mover.IsSprinting,
            gate = InputGate.Describe(), blocked = InputGate.IsBlocked, result = results != null && results.View != null && results.View.Visible,
            cinematic = presentation != null && presentation.GetComponent<EscapeEndingCinematic>().Playing,
            liveCamera = brain != null ? brain.ActiveVirtualCamera?.Name : null,
            transitioning = SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.IsTransitioning,
            interaction = UnityEngine.Object.FindFirstObjectByType<LobbyRoom>()?.FocusedInteraction?.name };
    }
    public static object Restore()
    {
        foreach (var keyboard in InputSystem.devices.OfType<Keyboard>().Where(k => k.name == "EndingProbeKeyboard").ToArray()) InputSystem.RemoveDevice(keyboard);
        DevMode.SetEditorEnabled(SessionState.GetBool("EndingProbe.DevBefore",false));
        return "Probe input and original Dev Mode restored";
    }
}
