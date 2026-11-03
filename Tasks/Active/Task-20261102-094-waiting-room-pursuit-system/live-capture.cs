var director = UnityEngine.Object.FindFirstObjectByType<CurtainCall.WaitingRoom.Pursuit.PursuitDirector>();
var actor = director.Actors.First(a => a.Room.name == "B1_Office");
var point = actor.transform.position + actor.transform.forward * 3;
UnityEngine.AI.NavMesh.SamplePosition(point, out var hit, 2, UnityEngine.AI.NavMesh.AllAreas);
var prefab = UnityEngine.Resources.Load<UnityEngine.GameObject>("Prefabs/Characters/Players/Player");
var victim = UnityEngine.Object.Instantiate(prefab, hit.position, UnityEngine.Quaternion.identity);
victim.name = "PursuitOfflineVictim";
var mover = victim.GetComponent<CurtainCall.Player.PlayerMover>();
mover.Simulated = false;
var condition = victim.GetComponent<CurtainCall.Player.PlayerCondition>();
var campaign = CurtainCall.Campaign.CampaignRun.Instance;
var origin = actor.transform.position;
string reportPath = "Tasks/Active/Task-20261102-094-waiting-room-pursuit-system/live-capture-report.json";
double expires = UnityEditor.EditorApplication.timeSinceStartup + 20;
UnityEditor.EditorApplication.CallbackFunction poll = null;
poll = () => {
    var capturing = director != null ? director.Actors.FirstOrDefault(a => a.Victim == victim) : null;
    if (capturing != null && capturing.CaptureElapsed >= 1.5f) {
        System.IO.File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(new {result="capturing", elapsed=capturing.CaptureElapsed,
            moved=UnityEngine.Vector3.Distance(origin,capturing.transform.position), motion=capturing.State.ToString(), lost=condition.LostCount,
            control=mover.ControlEnabled, phase=campaign.State.Phase.ToString(), timer=campaign.State.Timer.ToString()},Newtonsoft.Json.Formatting.Indented));
        UnityEditor.EditorApplication.update -= poll;
        UnityEditor.EditorApplication.isPaused = true;
        var view = UnityEditor.SceneView.lastActiveSceneView;
        if(view != null) view.LookAtDirect(capturing.transform.position + UnityEngine.Vector3.up * .7f, UnityEngine.Quaternion.Euler(15,135,0), 3f);
    } else if (UnityEditor.EditorApplication.timeSinceStartup > expires) {
        System.IO.File.WriteAllText(reportPath,"{\"result\":\"capture timeout\"}");
        UnityEditor.EditorApplication.update -= poll;
    }
};
UnityEditor.EditorApplication.update += poll;
campaign.StepChanged += s => {
    if(s.Phase == CurtainCall.Campaign.CampaignPhase.Performance && condition != null)
        System.IO.File.WriteAllText("Tasks/Active/Task-20261102-094-waiting-room-pursuit-system/live-transition-report.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new{phase=s.Phase.ToString(),lost=condition.LostCount,generation=s.Generation},Newtonsoft.Json.Formatting.Indented));
};
return "Spawned offline victim 3m ahead; pause automatically during capture or report a 20s timeout.";
