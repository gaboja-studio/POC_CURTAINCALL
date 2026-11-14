using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.WaitingRoom;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ScheduleMultiplayerProbe
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Scenario = "Assets/Settings/PlayMode/ScheduleVerificationLocal.asset";
    const string Output = "D:/github/POC_CURTAINCALL/Tasks/Active/Task-20261114-132-schedule-ui-redesign/Captures/";
    const ushort Port = 17786;
    static NetworkSessionManager Session => NetworkSessionManager.EnsureExists();
    static CampaignRun Run => CampaignRun.Instance;
    static ScheduleBoard Board => ScheduleBoard.Instance;
    static ScheduleBoardView BoardView => Object.FindFirstObjectByType<ScheduleBoardView>();
    static WaitingRoomScheduleUI UI => Object.FindFirstObjectByType<WaitingRoomScheduleUI>();
    static NetworkPlayer Local => NetworkPlayer.Local;
    static NetworkPlayer Remote => NetworkPlayer.All.Single(p => p.IsSpawned && p != Local);

    public static async Task<object> StartScenario()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Editor must be stopped");
        if (!AssetDatabase.CopyAsset("Assets/Settings/PlayMode/EscapeEntryFourEditors.asset", Scenario))
            throw new Exception("Temporary scenario already exists or could not be copied");
        SessionState.SetBool("ScheduleVerification.DevBefore", DevMode.Enabled);
        DevMode.SetEditorEnabled(true);
        var config = AssetDatabase.LoadMainAssetAtPath(Scenario);
        var serialized = new SerializedObject(config);
        serialized.FindProperty("m_EditorInstances").arraySize = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(config);
        config.GetType().GetMethod("OnConfigSelected", Flags).Invoke(config, null);
        var start = (Task)config.GetType().GetMethod("ExecuteStartAsync", Flags).Invoke(config, new object[]{CancellationToken.None});
        if (await Task.WhenAny(start, Task.Delay(1500)) == start) await start;
        return start.Status.ToString();
    }

    public static object Restore()
    {
        DevMode.SetEditorEnabled(SessionState.GetBool("ScheduleVerification.DevBefore", false));
        if (AssetDatabase.LoadMainAssetAtPath(Scenario) != null) AssetDatabase.DeleteAsset(Scenario);
        return "Restored DevMode; removed temporary scenario.";
    }

    static async Task Wait(Func<bool> f, string step, int seconds = 20, int intervalMs = 40)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (!f())
        {
            if (DateTime.UtcNow >= end) throw new Exception("Timed out: " + step);
            await Task.Delay(intervalMs);
        }
    }
    static void Ready()
    {
        if (!Application.isPlaying) throw new Exception("Expected Play Mode");
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        WaitingRoomTutorial.MemoryOnly = true;
        WaitingRoomTutorial.Enabled = false;
    }
    public static async Task<object> Host()
    {
        Ready();
        if (!await Session.HostLanAsync(Port)) throw new Exception("LAN host failed");
        return new { Session.IsHost, Port };
    }
    public static async Task<object> Join()
    {
        Ready();
        if (!await Session.JoinLanAsync("127.0.0.1", Port)) throw new Exception("LAN join failed");
        await Wait(() => Local != null && Local.IsSpawned, "client spawn");
        return new { id = Local.OwnerClientId, Local.Slot, Session.IsHost, Session.PlayerCount };
    }
    public static async Task<object> Night()
    {
        await Wait(() => Session.PlayerCount == 2 && NetworkPlayer.All.Count(p=>p.IsSpawned) == 2, "two players");
        var launcher = DevStartLauncher.EnsureExists();
        if (!launcher.TryLaunch(new RunStartPoint(1, RunStartPhase.Night, 0, ""))) throw new Exception(launcher.Status);
        await Wait(() => !launcher.IsBusy, "night launch", 70);
        if (!launcher.LastLaunchSucceeded) throw new Exception(launcher.Status);
        await Wait(() => Board != null && UI != null && BoardView != null, "schedule scene");
        // Arrange an unlocked second performance and stage 2 before measuring real UI clicks.
        var rig = Object.FindFirstObjectByType<WaitingRoomCannonRehearsal>();
        var shared = rig.GetType().GetField("shared", Flags).GetValue(rig);
        shared.GetType().GetField("mastered", Flags).SetValue(shared, true);
        Run.DiscoverTrick("HumanCannon");
        Run.RecordClear("HumanCannon", 1);
        Run.RecordClear("Tightrope", 1);
        if (Board.Set(2, 0, "Tightrope", 1).ToString() != "None") throw new Exception("Fixture did not set baseline");
        if (Run.State.Phase == CampaignPhase.RestTime) Run.DebugSetRemaining(600);
        return State();
    }
    public static object State()
    {
        var cards = UI?.View?.GetComponentsInChildren<ScheduleTicketCard>(true);
        var card = cards != null && cards.Length > 2 ? cards[2] : null;
        return new {
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, frame = Time.frameCount, phase = Run?.State.Phase.ToString(),
            host = Session.IsHost, count = Session.PlayerCount, id = Local?.OwnerClientId,
            net = NetworkPlayer.All.Where(p=>p.IsSpawned).Select(p=>new { id=p.OwnerClientId, owner=p.IsOwner, state=p.State.ToString(), lost=p.LostParts.ToString(), xyz=p.transform.position.ToString("F3") }).ToArray(),
            trick = Board?.Book.Get(2,0), tier=Board?.Book.Difficulty(2,0), authority=Board?.HasAuthority,
            shown = card?.performanceName.text, shownTier = card?.tierLabel.text,
            slot = card?.slotButton.IsInteractable(), arrow = card?.nextButton.IsInteractable(),
            open = BoardView?.IsOpen, canvas = UI?.View?.GetComponentInParent<Canvas>().renderMode.ToString()
        };
    }
    public static object Open()
    {
        var aim = BoardView.PresentationPoint;
        var f = Vector3.ProjectOnPlane(BoardView.CloseUpPose.position - aim, Vector3.up).normalized;
        var feet = aim + f * 1.65f;
        feet.y = FloorAt(feet);
        Local.GetComponent<PlayerMover>().SetStartPose(feet, Quaternion.LookRotation(-f));
        BoardView.Interact();
        if (!BoardView.IsOpen) throw new Exception("Board open refused");
        return new { feet=feet.ToString("F3"), board=aim.ToString("F3"), near=BoardView.CloseUpPose.position.ToString("F3") };
    }
    public static object Close() { BoardView.SetOpen(false); return State(); }
    public static object ArrangeRepeat()
    {
        if (Run.State.Phase == CampaignPhase.RestTime) Run.DebugSetRemaining(600);
        NetworkPlayer.ServerRestoreEveryone();
        if (Board.Set(2,0,"HumanCannon",1).ToString() != "None") throw new Exception("Repeat setup failed");
        return Open();
    }
    public static object Heal() { NetworkPlayer.ServerRestoreEveryone(); return State(); }
    static float FloorAt(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, BoardView.PresentationPoint.y + 1f, p.z), Vector3.down, 12, ~0, QueryTriggerInteraction.Ignore);
        var hit = hits.OrderBy(h=>h.distance).FirstOrDefault(h=>h.normal.y > .7f && h.collider.GetComponentInParent<NetworkPlayer>() == null);
        if (!hit.collider) throw new Exception("Missing floor near schedule");
        return hit.point.y + .03f;
    }
    public static async Task<object> Capture(string name)
    {
        Directory.CreateDirectory(Output);
        int f = Time.frameCount;
        string path = Path.Combine(Output,name+".png");
        var previous = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.UnixEpoch;
        ScreenCapture.CaptureScreenshot(path);
        await Wait(() => Time.frameCount > f && File.Exists(path) && File.GetLastWriteTimeUtc(path) > previous && new FileInfo(path).Length > 0, "Game View PNG");
        return new { path, frame=Time.frameCount, width=Screen.width, height=Screen.height };
    }
    static ScheduleTicketCard Card => UI.View.GetComponentsInChildren<ScheduleTicketCard>(true)[2];
    public static async Task<object> Click(string part, bool capture)
    {
        var card = Card;
        var button = part == "slot" ? card.slotButton : part == "up" ? card.nextButton : card.previousButton;
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var system = EventSystem.current;
        var hits = new List<RaycastResult>();
        system.RaycastAll(new PointerEventData(system){position=point},hits);
        if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button.gameObject)
            throw new Exception("Pointer hit mismatch: "+string.Join(", ",hits.Select(h=>h.gameObject.name)));
        var before = new { trick=Board.Book.Get(2,0), tier=Board.Book.Difficulty(2,0) };
        var mouse = InputSystem.AddDevice<Mouse>("ScheduleVerificationMouse");
        var samples = new List<object>();
        object geometry = null;
        try
        {
            foreach (ushort mask in new ushort[]{0,1,0})
            {
                int frame = Time.frameCount;
                if (capture)
                {
                    var ray = Camera.main.ScreenPointToRay(point);
                    if (mask == 1)
                    {
                        var start=Remote.transform.position;
                        await Wait(()=>
                        {
                            var bounds=RemoteBounds(RemoteRenderers());
                            var center=Camera.main.WorldToViewportPoint(bounds.center);
                            return Vector3.Distance(start,Remote.transform.position)>.10f && center.x>.43f && center.x<.69f && bounds.IntersectRay(ray);
                        }, "remote movement across arrow ray", 30);
                        frame=Time.frameCount;
                        geometry=Crossing();
                    }
                    samples.Add(new { mask, frame, overlap=RemoteBounds(RemoteRenderers()).IntersectRay(ray), remotePosition=Remote.transform.position.ToString("F3") });
                }
                InputSystem.QueueStateEvent(mouse,new MouseState { position=point, buttons=mask });
                if(capture && mask==1) WorldCamera("2p-remote-in-front");
                await Wait(()=>Time.frameCount >= frame+2, "mouse frame", 5, capture ? 1 : 40);
            }
            if (capture) await Capture("2p-host-applied");
            return new { hit=hits[0].gameObject.name, input=button.name, before, after=State(), feedback=card.feedback.text, samples, geometry };
        }
        finally { InputSystem.RemoveDevice(mouse); }
    }

    public static object Pose(float x, float z, float dx, float dz)
    {
        Vector3 p = new(x,BoardView.PresentationPoint.y,z);
        p.y = FloorAt(p);
        Quaternion q = Quaternion.LookRotation(new Vector3(dx,0,dz));
        Local.GetComponent<PlayerMover>().SetStartPose(p,q);
        var camera = CurtainCall.Cameras.CameraViewDirector.Current;
        if (camera != null && camera.UsesMouseLook) camera.SetLookRotation(q);
        return State();
    }

    public static object CameraTarget()
    {
        var c = Camera.main.transform;
        var t = BoardView.PresentationPoint;
        return new { cam=new[]{c.position.x,c.position.y,c.position.z}, board=new[]{t.x,t.y,t.z}, right=new[]{c.right.x,c.right.y,c.right.z} };
    }

    public static async Task<object> Walk(int ms)
    {
        var key = InputSystem.AddDevice<Keyboard>("ScheduleVerificationKeyboard");
        var prev = Local.transform.position;
        try
        {
            InputSystem.QueueStateEvent(key,new KeyboardState(Key.W));
            await Task.Delay(ms);
            InputSystem.QueueStateEvent(key,new KeyboardState());
            await Task.Delay(100);
            return new { from=prev.ToString("F3"), to=Local.transform.position.ToString("F3"), moved=Vector3.Distance(prev,Local.transform.position), state=State() };
        }
        finally { InputSystem.RemoveDevice(key); }
    }

    public static object Geometry()
    {
        return new {
            camera = CameraTarget(), body = Remote.GetComponentsInChildren<Renderer>(true).Select(r=>new {r.name,r.enabled,r.forceRenderingOff,active=r.gameObject.activeInHierarchy,bounds=r.bounds.ToString()}).ToArray(),
            camRay = Camera.main.ScreenPointToRay(RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)Card.nextButton.transform).TransformPoint(((RectTransform)Card.nextButton.transform).rect.center))).ToString(),
            state=State()
        };
    }

    public static async Task<object> MoveAndJump()
    {
        var key = InputSystem.AddDevice<Keyboard>("ScheduleVerificationKeyboard");
        var start = Local.transform.position;
        float peak = start.y;
        try
        {
            var end = DateTime.UtcNow.AddMilliseconds(450);
            InputSystem.QueueStateEvent(key,new KeyboardState(Key.W,Key.Space));
            while (DateTime.UtcNow < end) { await Task.Delay(20); peak=Math.Max(peak,Local.transform.position.y); }
            InputSystem.QueueStateEvent(key,new KeyboardState());
            await Task.Delay(100);
            return new { from=start.ToString("F3"), to=Local.transform.position.ToString("F3"), peak };
        }
        finally { InputSystem.RemoveDevice(key); }
    }

    public static object Crossing()
    {
        var cam = Camera.main;
        var renderers = RemoteRenderers();
        var bounds = RemoteBounds(renderers);
        var viewport = cam.WorldToViewportPoint(bounds.center);
        var board = cam.WorldToViewportPoint(BoardView.PresentationPoint);
        var rect = (RectTransform)Card.nextButton.transform;
        var target = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var ray = cam.ScreenPointToRay(target);
        return new {
            remote=Remote.OwnerClientId, bounds= bounds.ToString(), screen=new[]{viewport.x,viewport.y,viewport.z},
            boardDepth=board.z, visibleMeshCount=renderers.Length,
            intersectsArrowRay=bounds.IntersectRay(ray), state=State()
        };
    }
    static Renderer[] RemoteRenderers() => Remote.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && !r.forceRenderingOff).ToArray();
    static Bounds RemoteBounds(Renderer[] rs)
    {
        var bounds=rs.First().bounds;
        foreach(var r in rs.Skip(1)) bounds.Encapsulate(r.bounds);
        return bounds;
    }
    public static async Task<object> ClickDuringCrossing()
    {
        var click=await Click("up",true);
        if (Board.Book.Difficulty(2,0)!=2 || Board.Book.Get(2,0)!="HumanCannon") throw new Exception("Arrow failed to apply 2 / preserve show");
        return click;
    }
    public static async Task<object> Snapshot(string name, int tier)
    {
        await Wait(()=>Board != null && BoardView.IsOpen && Board.Book.Get(2,0)=="HumanCannon" &&
            Board.Book.Difficulty(2,0)==tier && Card.performanceName.text=="인간 대포" && Card.tierLabel.text==tier+"단계",
            "synced client ticket");
        if (Session.PlayerCount!=2 || Board.HasAuthority || Card.slotButton.IsInteractable() ||
            Card.previousButton.IsInteractable() || Card.nextButton.IsInteractable()) throw new Exception("Expected two-player read-only client");
        return new { state=State(), capture=await Capture(name) };
    }
    public static object WorldCamera(string name)
    {
        var camera = Camera.main;
        var rt = RenderTexture.GetTemporary(1280,720,24);
        var tex = new Texture2D(1280,720,TextureFormat.RGB24,false);
        var before = camera.targetTexture;
        var active = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0,0,1280,720),0,0);
            tex.Apply();
            File.WriteAllBytes(Output+name+".png",tex.EncodeToPNG());
            return Crossing();
        }
        finally { camera.targetTexture=before; RenderTexture.active=active; RenderTexture.ReleaseTemporary(rt); Object.Destroy(tex); }
    }
}
