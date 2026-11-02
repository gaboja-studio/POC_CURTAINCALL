var session = CurtainCall.Network.Session.NetworkSessionManager.Instance;
if (session.GameState == CurtainCall.Network.Session.GameSessionState.Waiting) session.StartGame();
var campaign = CurtainCall.Campaign.CampaignRun.Instance;
if (!campaign.DebugStartAt(new CurtainCall.Campaign.RunStartPoint(2, CurtainCall.Campaign.RunStartPhase.Morning, 0, "Tightrope"))) throw new System.Exception("Cannot start rest");
var director = UnityEngine.Object.FindFirstObjectByType<CurtainCall.WaitingRoom.Pursuit.PursuitDirector>();
var actor = director.Actors.First(a => a.Room.name == "B1_Office");
var player = CurtainCall.Network.PlayerSync.NetworkPlayer.Local;
if (player == null || !player.IsSpawned) throw new System.Exception("Network player unavailable");
var mover = player.GetComponent<CurtainCall.Player.PlayerMover>();
var point = actor.transform.position + actor.transform.forward * 2;
UnityEngine.AI.NavMesh.SamplePosition(point, out var hit, 2, UnityEngine.AI.NavMesh.AllAreas);
var controller = player.GetComponent<UnityEngine.CharacterController>();
controller.enabled=false; player.transform.position=hit.position; controller.enabled=true; mover.Simulated=false;
var origins=director.Actors.ToDictionary(a=>a,a=>a.transform.position);
campaign.DebugSetRemaining(.05f);
double expires=UnityEditor.EditorApplication.timeSinceStartup+30;
bool captureRecorded=false;
UnityEditor.EditorApplication.CallbackFunction poll=null;
poll=()=>{
    var capturing=director!=null ? director.Actors.FirstOrDefault(a=>a.Victim==player.gameObject) : null;
    if(!captureRecorded && capturing!=null && capturing.CaptureElapsed>=1.5f) {
        System.IO.File.WriteAllText("Tasks/Active/Task-20261102-094-waiting-room-pursuit-system/host-capture-report.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new{phase=campaign.State.Phase.ToString(),timer=campaign.State.Timer.ToString(),state=player.State.ToString(),
                lost=player.GetComponent<CurtainCall.Player.PlayerCondition>().LostCount,control=mover.ControlEnabled,actor=capturing.Room.name,
                distanceTravelled=UnityEngine.Vector3.Distance(origins[capturing],capturing.transform.position)},Newtonsoft.Json.Formatting.Indented));
        captureRecorded=true;
    }
    if(captureRecorded && campaign.State.Phase==CurtainCall.Campaign.CampaignPhase.Performance && player.State==CurtainCall.Network.PlayerSync.PlayerState.Normal) {
        System.IO.File.WriteAllText("Tasks/Active/Task-20261102-094-waiting-room-pursuit-system/host-transition-report.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new{phase=campaign.State.Phase.ToString(),state=player.State.ToString(),lost=player.GetComponent<CurtainCall.Player.PlayerCondition>().LostCount,
                generation=campaign.State.Generation,spawned=player.IsSpawned},Newtonsoft.Json.Formatting.Indented));
        UnityEditor.EditorApplication.update-=poll; UnityEditor.EditorApplication.isPaused=true;
    } else if(UnityEditor.EditorApplication.timeSinceStartup>expires) {
        System.IO.File.WriteAllText("Tasks/Active/Task-20261102-094-waiting-room-pursuit-system/host-timeout.json","{\"result\":\"timeout\"}");
        UnityEditor.EditorApplication.update-=poll;
    }
};
UnityEditor.EditorApplication.update+=poll;
return new{player=player.OwnerClientId,slot=player.Slot,timer=campaign.State.Timer.ToString()};
