var director = CurtainCall.WaitingRoom.Pursuit.PursuitDirector.Current;
var actor = director.Actors.First(a => a.Room.name == "B1_ChangingRoom");
var player = CurtainCall.Network.PlayerSync.NetworkPlayer.Local;
var mover = player.GetComponent<CurtainCall.Player.PlayerMover>();
var controller = player.GetComponent<UnityEngine.CharacterController>();
UnityEngine.AI.NavMesh.SamplePosition(actor.transform.position + actor.transform.forward * 2.5f, out var ground, 2, UnityEngine.AI.NavMesh.AllAreas);
controller.enabled = false; player.transform.position = ground.position; controller.enabled = true;
mover.SetControlEnabled(false);
var preset = UnityEngine.ScriptableObject.CreateInstance<CurtainCall.Cameras.CameraViewPreset>();
preset.Kind = CurtainCall.Cameras.CameraViewKind.ThirdPerson; preset.Distance = 3.5f; preset.Height = 2.5f; preset.LookHeight = .15f; preset.LookAhead = 0; preset.HideLocalModel = false;
CurtainCall.Cameras.CameraViewDirector.Current.RequestView(actor, preset, 999, UnityEngine.Vector3.forward);
CurtainCall.Campaign.CampaignRun.Instance.DebugSetRemaining(.01f);
double until = UnityEditor.EditorApplication.timeSinceStartup + 35;
UnityEditor.EditorApplication.CallbackFunction review = null;
review = () => {
    if (director == null || UnityEditor.EditorApplication.timeSinceStartup > until) { UnityEditor.EditorApplication.update -= review; return; }
    var captured = director.Actors.FirstOrDefault(a => a.Victim == player.gameObject && a.CaptureElapsed > 1.7f);
    if (captured == null) return;
    UnityEditor.EditorApplication.update -= review;
    var pose = player.GetComponent<CurtainCall.WaitingRoom.Pursuit.PursuitVictimPose>();
    System.IO.File.WriteAllText("Tasks/Active/Task-20261102-094-waiting-room-pursuit-system/behaviour-live-capture.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
        elapsed = captured.CaptureElapsed, facingDot = UnityEngine.Vector3.Dot(captured.transform.forward, pose.HeadDirection),
        offset = UnityEngine.Vector3.Distance(captured.transform.position, pose.AttackerPosition),
        blood = player.GetComponent<CurtainCall.WaitingRoom.Pursuit.PursuitCaptureBlood>().EmittedBursts,
        graph = captured.GraphReady, frame = UnityEngine.Time.frameCount
    }, Newtonsoft.Json.Formatting.Indented));
    UnityEditor.EditorApplication.isPaused = true;
};
UnityEditor.EditorApplication.update += review;
return "Capture visual review armed";
