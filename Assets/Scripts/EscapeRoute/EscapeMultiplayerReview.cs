#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CurtainCall.Cameras;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.EscapeRoute
{
    // Explicit development-build flag only. Drives each real local client's normal input pipeline.
    // Checkpoint positioning is recorded as setup and never counted as traversal.
    [DefaultExecutionOrder(-90)]
    public sealed class EscapeMultiplayerReview : MonoBehaviour
    {
        [Serializable] public class Command { public int id; public string action; public Vector3 target; public float move, side, seconds=1, yaw, pitch=12; public bool jump, tap, hold, sprint, crouch, use, balance; public string file; public ulong actor; public int damage; }
        [Serializable] public class Actor
        {
            public ulong id;
            public Vector3 feet;
            public string state, death, carry;
            public bool airborne, seated, flying, crouched, sprinting, hanging, simulating, local, protectedWait;
            public int pole, grabbable, boardable, injury;
            public float poleAngle, poleSpeed;
            public Vector3 poleRelease;
            public PilotAudit pilot;
            public long below = -1, above = -1;
        }
        [Serializable] public class Launcher { public string name; public ulong carrier; public int cuts,total,shots; public bool open,combat; public Vector3 position,handle,forward,muzzle; public float cooldown; public CableTarget[] remaining; public string guide; }
        [Serializable] public class CameraAudit { public Vector3 position,forward,rayOrigin,rayDirection; public float distance,height,lookHeight,lookAhead,pitchLimit; public string kind; }
        [Serializable] public class BossAudit { public Vector3 position,forward; public string phase; public int mask; public bool combat,defeated; public BossPartAudit[] parts; }
        [Serializable] public class BossPartAudit { public int index; public bool valid,cut,exposed; public Vector3 centre,halfExtents; public Quaternion rotation; public float radius; }
        [Serializable] public class CableTarget { public string id,name; public Vector3 point; public bool valid; }
        [Serializable] public class PilotAudit { public int held; public bool unloading,hold; public float holdProgress; public string awaitingLanding,groundPending,latchReady,releasedFrames; }
        [Serializable] public class ShotRecord { public string device,key,phase,result; public ulong actor; public int localCommand; public double time,clock; public Vector3 position,direction; }
        [Serializable] public class Trampoline { public Vector3 surface, handle; public int bounces; public long carrier = -1; public bool canGrab, bounceReady; }
        [Serializable] public class PolePosition { public int index; public string name; public Vector3 grip; }
        [Serializable] public class Pursuit
        {
            public long target = -1, attackActor = -1;
            public int gate = -1;
            public string action;
            public float entranceTime, runWeight;
            public Vector3 attackOrigin, attackAim;
            public double attackStarted;
            public bool entranceComplete;
        }
        [Serializable] public class Snapshot { public int process,slot,command,restarts,observedShots,shotLogFailures; public string connection,game,scheme,campaign,end; public bool ready,blocked,continuous,ending,exitOpen,encounterStarted; public float time,balance,routeProgress,pressure; public Vector3 routeForward,pursuer; public Pursuit pursuit; public Actor[] players; public Launcher[] saws; public PolePosition[] poles; public Trampoline trampoline; public bool ropesOpen; public bool ropesPresent, warningLit; public bool[] locks,armed; public string[] fire; public double fireAt,clock,hazardAge,encounterEpoch; public Vector3 view; public CameraAudit camera; public BossAudit boss; public string error,inputBlockage; }
        static readonly PropertyInfo inputProperty=typeof(PlayerInputReader).GetProperty("Current");
        static readonly FieldInfo sawCables=typeof(EscapeExitSaw).GetField("cables",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo sawMuzzle=typeof(EscapeExitSaw).GetField("muzzle",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo cablePath=typeof(EscapeCuttableCable).GetField("path",BindingFlags.Instance|BindingFlags.NonPublic);
        string folder; int slot,lastId; Command active; double until,nextRead,nextState; string error; bool continuous;
        HumanCannonPoleLoop[] conveyorLoops;
        bool conveyorDisembarking;
        readonly Queue<ShotRecord> pendingShots=new();
        int observedShots,shotLogFailures;
        void OnEnable() => EscapeExitSaw.ShotObserved += RecordShot;
        void OnDisable() => EscapeExitSaw.ShotObserved -= RecordShot;
        void RecordShot(EscapeExitSaw saw,ulong actor,string key,string phase,Vector3 position,Vector3 direction,string result)
        {
            if(folder==null)return;
            if(phase=="FIRED")observedShots++;
            pendingShots.Enqueue(new ShotRecord {
                device=saw.name,actor=actor,key=key,phase=phase,result=result,position=position,direction=direction,
                localCommand=NetworkPlayer.Local&&NetworkPlayer.Local.OwnerClientId==actor?lastId:-1,
                time=Time.realtimeSinceStartupAsDouble,clock=EscapePursuitFire.Current?.Clock??0 });
        }
        void FlushShots()
        {
            try { while(pendingShots.Count>0) {
                File.AppendAllText(Path.Combine(folder,"shots-"+slot+".jsonl"),JsonUtility.ToJson(pendingShots.Peek())+Environment.NewLine);
                pendingShots.Dequeue();
            } }
            catch(Exception e) { shotLogFailures++;error="Shot log: "+e.GetType().Name; }
        }
        static Launcher ObserveLauncher(EscapeExitSaw saw)
        {
            var muzzle=sawMuzzle.GetValue(saw) as Transform;
            var origin=muzzle?muzzle.position:saw.CarryRoot.position;
            var cables=sawCables.GetValue(saw) as EscapeCuttableCable[] ?? Array.Empty<EscapeCuttableCable>();
            return new Launcher { name=saw.name,carrier=saw.Carrier,cuts=saw.CutCableCount,total=saw.RequiredCableCount,
                shots=saw.ActiveProjectileCount,open=saw.DoorOpen,combat=saw.FinaleBoss&&saw.FinaleBoss.CombatStarted,position=saw.CarryRoot.position,handle=saw.Handle.position,
                forward=saw.CarryRoot.forward,muzzle=origin,cooldown=saw.SecondsUntilShot,guide=saw.Guidance,
                remaining=cables.Where(c=>c&&c.RequiredForExit&&!c.IsCut).Select(c=>new CableTarget {
                    id=CurtainCall.Telemetry.Pipeline.DetailLogIds.ObjectId(c,"IN_"),name=c.name,
                    valid=c.SampleCount>=2,point=CablePointAtHeight(c,origin.y) }).ToArray() };
        }
        static Vector3 CablePointAtHeight(EscapeCuttableCable cable,float height)
        {
            var path=cablePath.GetValue(cable) as Vector3[];
            if(path==null||path.Length==0)return cable.transform.position;
            Vector3 best=cable.transform.TransformPoint(path[0]);
            for(int i=1;i<path.Length;i++)
            {
                Vector3 a=cable.transform.TransformPoint(path[i-1]),b=cable.transform.TransformPoint(path[i]);
                float t=Mathf.Abs(b.y-a.y)>.00001f?Mathf.Clamp01((height-a.y)/(b.y-a.y)):0;
                Vector3 point=Vector3.Lerp(a,b,t);
                if(Mathf.Abs(point.y-height)<Mathf.Abs(best.y-height))best=point;
            }
            return best;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-escapeReview");
#if UNITY_EDITOR
            if(EscapeMppmReviewConfig.TryGet(out string editorFolder,out int editorSlot))
            {
                var editorReview=new GameObject("Explicit multiplayer editor review").AddComponent<EscapeMultiplayerReview>();
                editorReview.folder=editorFolder;editorReview.slot=editorSlot;editorReview.continuous=true;
                Directory.CreateDirectory(editorFolder);return;
            }
#endif
            if(index<0||index+1>=args.Length)return;
            var go=new GameObject("Explicit multiplayer client review");var review=go.AddComponent<EscapeMultiplayerReview>();
            review.folder=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(review.folder);
            review.continuous=Array.IndexOf(args,"-escapeContinuous")>=0;
            index=Array.IndexOf(args,"-escapeSlot");if(index>=0&&index+1<args.Length)int.TryParse(args[index+1],out review.slot);
        }
        void Update()
        {
            if(folder==null)return;
            var player=NetworkPlayer.Local;
            if(Time.realtimeSinceStartupAsDouble>=nextRead)
            {
                nextRead=Time.realtimeSinceStartupAsDouble+.1;
                string path=Path.Combine(folder,"command-"+slot+".json");
                if(File.Exists(path))try
                {
                    var command=JsonUtility.FromJson<Command>(File.ReadAllText(path));
                    if(command!=null&&command.id>lastId&&(player!=null||command.action=="quit"))
                    {
                        if (continuous && (command.action == "setup" || command.action == "damage"))
                            throw new InvalidOperationException("Scenario setup is disabled in continuous runs");
                        lastId=command.id;active=command;conveyorDisembarking=false;
                        until=Time.realtimeSinceStartupAsDouble+Mathf.Clamp(command.seconds,0,120);
                        ApplyCommand(player,command);
                        File.AppendAllText(Path.Combine(folder,"actions-"+slot+".jsonl"),JsonUtility.ToJson(command)+Environment.NewLine);
                    }
                }catch(IOException) { }
                catch(Exception e){error=e.GetType().Name+": "+e.Message;}
            }
            if(player!=null&&player.TryGetComponent(out PlayerInputReader reader))
            {
                var input=new PlayerInputFrame();
                if(active!=null&&Time.realtimeSinceStartupAsDouble<until)
                {
                    input.Move=active.move;input.Posture=active.side;input.SprintHeld=active.sprint;input.CrouchHeld=active.crouch;
                    input.ActionPressed=active.jump;input.InteractTapped=active.tap;input.InteractLongPressed=active.hold;input.InteractHoldProgress=active.hold?1:0;
                    input.UseDevicePressed=active.use;
                    if(active.use)
                    {
                        var saw=EscapeExitSaw.Devices.FirstOrDefault(s=>s&&s.Carrier==player.OwnerClientId);
                        if(saw)RecordShot(saw,player.OwnerClientId,"","USE_INPUT",saw.CarryRoot.position,saw.CarryRoot.forward,"");
                    }
                    active.use=false;
                    active.jump=active.tap=active.hold=false;
                    if(active.action=="walk")
                    {
                        var delta=active.target-player.transform.position;delta.y=0;
                        if(delta.magnitude<.18f){input.Move=input.Posture=0;until=0;}
                        else
                        {
                            var mover=player.GetComponent<PlayerMover>();
                            var forward=CameraViewDirector.Current!=null&&CameraViewDirector.Current.UsesMouseLook?CameraViewDirector.Current.ViewForward:mover.CourseForward;
                            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
                            input.Move=Vector3.Dot(delta.normalized,forward);input.Posture=Vector3.Dot(delta.normalized,Vector3.Cross(Vector3.up,forward));
                        }
                    }
                    if (active.balance && player.TryGetComponent(out PlayerBalance balance) && balance.IsActive)
                        input.Posture = -Mathf.Clamp(balance.Value * .1f, -1f, 1f);
                    if (active.action == "conveyor") GuideConveyorInput(player, ref input);
                }
                inputProperty.SetValue(reader,input);
            }
            if(Time.realtimeSinceStartupAsDouble>=nextState){nextState=Time.realtimeSinceStartupAsDouble+.35;WriteState();}
        }
        void GuideConveyorInput(NetworkPlayer player, ref PlayerInputFrame input)
        {
            var cannon = HumanCannonSystem.Current;
            var poles = HumanCannonPoleSystem.Current;
            if (!cannon || !poles || player.State != PlayerState.Normal) return;
            int held = poles.HeldBy(player.OwnerClientId);
            if (held >= 0)
            {
                var loop = poles.Pole(held).GetComponentInParent<HumanCannonPoleLoop>();
                if (loop && loop.AtUnloadingBay(loop.Elapsed))
                {
                    conveyorDisembarking = true;
                    input.InteractLongPressed = true;
                    input.InteractHoldProgress = 1;
                }
                return;
            }
            if (cannon.IsFlying(player.OwnerClientId))
            {
                // After letting go, the same grip remains in reach until we fall to the receiver.
                // Keep the normal drop input from being immediately undone by the review pilot.
                input.InteractTapped = !conveyorDisembarking && poles.FindGrabbable(player) >= 0;
                return;
            }
            var station = cannon.SeatedStation(player.OwnerClientId);
            if (!station)
            {
                input.InteractTapped = cannon.FindBoardableStation(player) == 2;
                return;
            }
            conveyorLoops ??= FindObjectsByType<HumanCannonPoleLoop>(FindObjectsSortMode.None);
            var gravity = CurtainCall.Settings.GameSettings.Base.Character.Gravity;
            foreach (var loop in conveyorLoops)
            {
                if (!loop.IsSynchronized || !loop.IsConfigured) continue;
                var pole = loop.GetComponentInChildren<HumanCannonPole>();
                if (!pole) continue;
                for (float t = .7f; t < 3.2f; t += .025f)
                {
                    var grip = loop.PointAt(pole.Grip.position, loop.Elapsed + t);
                    var hands = station.Muzzle.position + station.LaunchVelocity * t + Vector3.up * (1.2f - .5f * gravity * t * t);
                    if ((hands - grip).sqrMagnitude >= .65f * .65f) continue;
                    input.InteractTapped = true;
                    return;
                }
            }
        }
        void ApplyCommand(NetworkPlayer player,Command c)
        {
            if(c.action=="look")CameraViewDirector.Current?.SetLookRotation(Quaternion.Euler(c.pitch,c.yaw,0));
            if(c.action=="setup")
            {
                var mover=player.GetComponent<PlayerMover>();mover.Relocate(c.target,Quaternion.Euler(0,c.yaw,0));
                player.transform.rotation=Quaternion.Euler(0,c.yaw,0);CameraViewDirector.Current?.SetLookRotation(Quaternion.Euler(c.pitch,c.yaw,0));
            }
            if(c.action=="restart")FindFirstObjectByType<EscapeDevelopmentSession>()?.Restart();
            if (c.action == "damage") SetupDamage(player, c);
            if(c.action=="capture")Capture();
            if(c.action=="quit")Application.Quit();
        }

        static void SetupDamage(NetworkPlayer local, Command command)
        {
            if (!local.IsServer || command.damage < 1 || command.damage > 15)
                throw new InvalidOperationException("Scenario damage requires the host and a valid body mask");
            var actor = NetworkPlayer.All.FirstOrDefault(p => p && p.OwnerClientId == command.actor);
            if (!actor || actor.State != PlayerState.Normal)
                throw new InvalidOperationException("Scenario must start with a living player");
            var goal = (BodyPart)command.damage;
            int total = PlayerCondition.CountLimbs(goal), damaged = PlayerCondition.CountLimbs(actor.LostParts);
            if (goal != PlayerCondition.PartsForLostCount(total) || actor.LostParts != PlayerCondition.PartsForLostCount(damaged)
                || damaged > total)
                throw new InvalidOperationException("Scenario damage must follow the common cut order");
            // Use the normal host damage path; no assignment of state, motor or device occupancy.
            if (total > damaged && actor.ServerCut(total - damaged) != (goal & ~PlayerCondition.PartsForLostCount(damaged)))
                throw new InvalidOperationException("Host rejected scenario damage");
        }

        static Actor Observe(NetworkPlayer player, HumanCannonPoleSystem poles, EscapeUnderstageChase chase)
        {
            ulong id = player.OwnerClientId;
            var cannon = HumanCannonSystem.Current;
            var mover = player.GetComponent<PlayerMover>();
            var stack = PiggybackSystem.Current;
            int heldPole = poles ? poles.HeldBy(id) : -1;
            return new Actor
            {
                pilot=ObservePilot(player,poles,heldPole),
                id = id, feet = player.transform.position, state = player.State.ToString(),
                injury = (int)player.LostParts, protectedWait = chase && player.IsServer && chase.ReviewConveyorWait(id),
                death = player.State is PlayerState.Fallen or PlayerState.Retired ? player.LastDeathOrigin.ToString() : null,
                airborne = player.IsAirborne, crouched = mover && mover.IsCrouched, sprinting = mover && mover.IsSprinting,
                simulating = mover && mover.Simulated, hanging = TightropeHangSystem.Current?.IsHanging(player) == true,
                pole = heldPole, grabbable = poles ? poles.FindGrabbable(player) : -1,
                poleAngle = heldPole >= 0 ? poles.AngleAt(heldPole) : 0,
                poleSpeed = heldPole >= 0 ? poles.SpeedAt(heldPole) : 0,
                poleRelease = heldPole >= 0 ? poles.ReleaseVelocityAt(heldPole) : Vector3.zero,
                boardable = cannon ? cannon.FindBoardableStation(player) : -1, seated = cannon && cannon.IsSeated(id),
                flying = cannon && cannon.IsFlying(id), carry = HumanCannonCarry.Held(id)?.CarryName, local = player.IsLocal,
                below = stack?.GetBelow(player) is NetworkPlayer below ? (long)below.OwnerClientId : -1,
                above = stack?.GetAbove(player) is NetworkPlayer above ? (long)above.OwnerClientId : -1
            };
        }

        static object AuditMember(object target,string name)
        {
            if(target==null)return null;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            return target.GetType().GetField(name,flags)?.GetValue(target)
                ?? target.GetType().GetProperty(name,flags)?.GetValue(target);
        }
        static PilotAudit ObservePilot(NetworkPlayer player,HumanCannonPoleSystem poles,int held)
        {
            var pilot=player.GetComponent<HumanCannonPilot>();
            if(!pilot)return null;
            var latch=AuditMember(pilot,"latch");var request=AuditMember(pilot,"groundRequest");
            var reader=player.GetComponent<PlayerInputReader>();
            var loop=held>=0&&poles?poles.Pole(held).GetComponentInParent<HumanCannonPoleLoop>():null;
            return new PilotAudit { held=pilot.HeldPole,unloading=loop&&loop.AtUnloadingBay(loop.Elapsed),
                hold=reader&&reader.Current.InteractLongPressed,holdProgress=reader?reader.Current.InteractHoldProgress:-1,
                awaitingLanding=AuditMember(pilot,"awaitingLanding")?.ToString(),groundPending=AuditMember(request,"Pending")?.ToString(),
                latchReady=AuditMember(latch,"ready")?.ToString(),releasedFrames=AuditMember(latch,"releasedFrames")?.ToString() };
        }
        void Capture()
        {
            // Hidden background players do not reach a presented end-of-frame. Render their actual camera explicitly.
            var camera=Camera.main;
            // Failure presentation replaces the gameplay camera with an untagged output camera.
            if(camera==null)camera=FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(candidate=>candidate.isActiveAndEnabled&&candidate.targetTexture==null
                    &&(!candidate.TryGetComponent<UniversalAdditionalCameraData>(out var data)||data.renderType==CameraRenderType.Base))
                .OrderByDescending(candidate=>candidate.depth).FirstOrDefault();
            if(camera==null)throw new InvalidOperationException("No gameplay camera");
            var texture=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active; Texture2D pixels=null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});
                RenderTexture.active=texture; pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder,"client-"+slot+"-"+lastId+".png"),pixels.EncodeToPNG());
            }
            finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(texture);if(pixels!=null)Destroy(pixels); }
        }
        static CameraAudit ObserveCamera()
        {
            var camera=Camera.main; if(!camera)return null;
            var view=CameraViewDirector.Current?.CurrentView;
            var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            return new CameraAudit { position=camera.transform.position,forward=camera.transform.forward,
                rayOrigin=ray.origin,rayDirection=ray.direction,kind=view?view.Kind.ToString():null,
                distance=view?view.Distance:0,height=view?view.Height:0,lookHeight=view?view.LookHeight:0,
                lookAhead=view?view.LookAhead:0,pitchLimit=view?view.PitchLimit:0 };
        }
        static BossAudit ObserveBoss()
        {
            var boss = EscapePursuerBoss.Current;
            if (!boss) return null;
            int mask = 0;
            for (int i = 0; i < boss.PartCount && i < 8; i++) if (boss.IsCut(i)) mask |= 1 << i;
            return new BossAudit { position = boss.transform.position, forward = boss.transform.forward,
                phase = boss.State.ToString(), mask = mask, combat = boss.CombatStarted, defeated = boss.Defeated,
                parts = Enumerable.Range(0, boss.PartCount).Select(i => {
                    var box = boss.Hitbox(i);
                    var half = box ? Vector3.Scale(box.size * .5f, box.transform.lossyScale) : Vector3.zero;
                    half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
                    return new BossPartAudit { index = i, valid = box && box.enabled && box.gameObject.activeInHierarchy,
                        cut = boss.IsCut(i), exposed = EscapePursuerBoss.Exposed((byte)mask, i),
                        centre = box ? box.transform.TransformPoint(box.center) : Vector3.zero,
                        halfExtents = half, rotation = box ? box.transform.rotation : Quaternion.identity, radius = half.magnitude };
                }).ToArray() };
        }

        void WriteState()
        {
            FlushShots();
            try
            {
                var session=NetworkSessionManager.Instance;var poles=HumanCannonPoleSystem.Current;var locks=EscapeCooperativeLocks.Current;
                var local=NetworkPlayer.Local;var dev=FindFirstObjectByType<EscapeDevelopmentSession>();
                var campaign=CurtainCall.Campaign.CampaignRun.Instance;
                var chase=EscapeUnderstageChase.Current;
                var trampoline = EscapeCooperativeRopes.Current?.Trampoline;
                var state=new Snapshot{process=System.Diagnostics.Process.GetCurrentProcess().Id,slot=slot,command=lastId,time=Time.realtimeSinceStartup,
                    observedShots=observedShots,shotLogFailures=shotLogFailures,
                    connection=session?.ConnectionState.ToString(),game=session?.GameState.ToString(),ready=dev!=null&&dev.Ready,blocked=InputGate.IsBlocked,continuous=continuous,restarts=dev?.RecoveryCount??0,
                    encounterStarted=EscapeRouteTimer.Current&&EscapeRouteTimer.Current.HasStarted,
                    encounterEpoch=EscapeRouteTimer.Current?EscapeRouteTimer.Current.StartedAt:-1,
                    inputBlockage=InputGate.Describe(),
                    campaign=campaign?.State.Phase.ToString(),end=campaign?.State.LastEnd.ToString(),ending=EscapeEndingPresentation.Current&&EscapeEndingPresentation.Current.IsPresenting,exitOpen=EscapeCampaignContent.Current&&EscapeCampaignContent.Current.ExitOpen,
                    balance=local!=null?local.GetComponent<PlayerBalance>().Value:0,
                    routeForward=local!=null?local.GetComponent<PlayerMover>().CourseForward:Vector3.zero,
                    pursuer=chase?chase.transform.position:Vector3.zero,routeProgress=chase?chase.ReviewRouteProgress:-1,
                    pressure=chase?chase.ReviewPressure:0,boss=ObserveBoss(),
                    scheme=local!=null?local.GetComponent<PlayerController>()?.Scheme?.name:null,view=CameraViewDirector.Current!=null?CameraViewDirector.Current.ViewForward:Vector3.zero,camera=ObserveCamera(),error=error,
                    pursuit=chase && session != null && session.IsHost ? new Pursuit { target=chase.ReviewTarget, gate=chase.ReviewGate,
                        action=chase.ReviewAction, attackActor=chase.ReviewAttackActor, entranceTime=chase.ReviewEntranceTime,
                        attackOrigin=chase.ReviewAttackOrigin, attackAim=chase.ReviewAttackAim, attackStarted=chase.ReviewAttackStarted,
                        entranceComplete=chase.ReviewEntranceComplete, runWeight=chase.ReviewRunWeight } : null,
                    players=NetworkPlayer.All.Where(p=>p!=null).Select(p=>Observe(p,poles,chase)).ToArray(),
                    poles=poles ? Enumerable.Range(0, poles.PoleCount).Where(i => poles.Pole(i) && poles.Pole(i).Grip)
                        .Select(i => new PolePosition { index = i, name = poles.Pole(i).name, grip = poles.Pole(i).Grip.position }).ToArray() : Array.Empty<PolePosition>(),
                    hazardAge=HumanCannonHazardCourse.Current?.Elapsed??0,
                    saws=EscapeExitSaw.Devices.Where(s=>s!=null).Select(ObserveLauncher).ToArray(),
                    trampoline = trampoline ? new Trampoline { surface = trampoline.Surface, handle = trampoline.GrabPoint,
                        carrier = unchecked((long)trampoline.Carrier), bounces = trampoline.LocalBounceCount,
                        canGrab = local && trampoline.CanGrab(local), bounceReady = trampoline.BounceReady } : null,
                    ropesOpen=EscapeCooperativeRopes.Current!=null&&EscapeCooperativeRopes.Current.ReturnOpen,ropesPresent=EscapeCooperativeRopes.Current!=null&&EscapeCooperativeRopes.Current.RopesPresent,warningLit=EscapeCooperativeRopes.Current!=null&&EscapeCooperativeRopes.Current.WarningLit,
                    locks=locks!=null?Enumerable.Range(0,locks.Count).Select(locks.IsOpen).ToArray():Array.Empty<bool>(),
                    armed=locks!=null?Enumerable.Range(0,locks.Count).Select(locks.IsArmed).ToArray():Array.Empty<bool>(),
                    fireAt=EscapePursuitFire.Current?.ArmedAt??-1,clock=EscapePursuitFire.Current?.Clock??0,
                    fire=EscapePursuitFire.Current is EscapePursuitFire fire?Enumerable.Range(0,fire.BayCount).Select(i=>fire.PhaseAt(i,fire.Clock).ToString()).ToArray():Array.Empty<string>()};
                string json=JsonUtility.ToJson(state);
                if(continuous)File.AppendAllText(Path.Combine(folder,"timeline-"+slot+".jsonl"),json+Environment.NewLine);
                string path=Path.Combine(folder,"state-"+slot+".json");File.WriteAllText(path+".tmp",json);File.Copy(path+".tmp",path,true);
            }
            // A reader may briefly lock the previous snapshot on Windows; retry the next sample.
            catch(IOException) { }
            catch(Exception e){error=e.GetType().Name+": "+e.Message;}
        }
    }
}
#endif
