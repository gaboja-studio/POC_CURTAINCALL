#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
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
        [Serializable] public class Command { public int id; public string action; public Vector3 target; public float move, side, seconds=1, yaw, pitch=12; public bool jump, tap, hold, sprint; public string file; }
        [Serializable] public class Actor { public ulong id; public Vector3 feet; public string state; public bool airborne,seated,flying; public int pole,grabbable,boardable; public string carry; public bool local; public long below=-1,above=-1; }
        [Serializable] public class Launcher { public string name; public ulong carrier; public int cuts,total,shots; public bool open; public Vector3 position,handle; public string guide; }
        [Serializable] public class Snapshot { public int process,slot,command,restarts; public string connection,game,scheme; public bool ready,blocked,continuous; public float time; public Actor[] players; public Launcher[] saws; public bool ropesOpen; public int hiddenRope; public bool[] locks,armed; public string[] fire; public double fireAt,clock; public Vector3 view; public string error; }
        static readonly PropertyInfo inputProperty=typeof(PlayerInputReader).GetProperty("Current");
        string folder; int slot,lastId; Command active; double until,nextRead,nextState; string error; bool continuous;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-escapeReview");
#if UNITY_EDITOR
            if(EscapeMppmReviewConfig.TryGet(out string editorFolder,out int editorSlot))
            {
                var editorReview=new GameObject("Explicit four-editor MPPM review").AddComponent<EscapeMultiplayerReview>();
                editorReview.folder=editorFolder;editorReview.slot=editorSlot;editorReview.continuous=true;
                Directory.CreateDirectory(editorFolder);return;
            }
#endif
            if(index<0||index+1>=args.Length)return;
            var go=new GameObject("Explicit four-client review");var review=go.AddComponent<EscapeMultiplayerReview>();
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
                        if(continuous&&command.action=="setup")throw new InvalidOperationException("Checkpoint positioning is disabled in continuous runs");
                        lastId=command.id;active=command;until=Time.realtimeSinceStartupAsDouble+Mathf.Clamp(command.seconds,0,120);
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
                    input.Move=active.move;input.Posture=active.side;input.SprintHeld=active.sprint;
                    input.ActionPressed=active.jump;input.InteractTapped=active.tap;input.InteractLongPressed=active.hold;input.InteractHoldProgress=active.hold?1:0;
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
                }
                inputProperty.SetValue(reader,input);
            }
            if(Time.realtimeSinceStartupAsDouble>=nextState){nextState=Time.realtimeSinceStartupAsDouble+.35;WriteState();}
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
            if(c.action=="capture")Capture();
            if(c.action=="quit")Application.Quit();
        }
        void Capture()
        {
            // Hidden background players do not reach a presented end-of-frame. Render their actual camera explicitly.
            var camera=Camera.main; if(camera==null)throw new InvalidOperationException("No gameplay camera");
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
        void WriteState()
        {
            try
            {
                var session=NetworkSessionManager.Instance;var poles=HumanCannonPoleSystem.Current;var locks=EscapeCooperativeLocks.Current;
                var local=NetworkPlayer.Local;var dev=FindFirstObjectByType<EscapeDevelopmentSession>();
                var state=new Snapshot{process=System.Diagnostics.Process.GetCurrentProcess().Id,slot=slot,command=lastId,time=Time.realtimeSinceStartup,
                    connection=session?.ConnectionState.ToString(),game=session?.GameState.ToString(),ready=dev!=null&&dev.Ready,blocked=InputGate.IsBlocked,continuous=continuous,restarts=dev?.RecoveryCount??0,
                    scheme=local!=null?local.GetComponent<PlayerController>()?.Scheme?.name:null,view=CameraViewDirector.Current!=null?CameraViewDirector.Current.ViewForward:Vector3.zero,error=error,
                    players=NetworkPlayer.All.Where(p=>p!=null).Select(p=>new Actor{id=p.OwnerClientId,feet=p.transform.position,state=p.State.ToString(),airborne=p.IsAirborne,pole=poles!=null?poles.HeldBy(p.OwnerClientId):-1,grabbable=poles!=null?poles.FindGrabbable(p):-1,boardable=HumanCannonSystem.Current!=null?HumanCannonSystem.Current.FindBoardableStation(p):-1,seated=HumanCannonSystem.Current!=null&&HumanCannonSystem.Current.IsSeated(p.OwnerClientId),flying=HumanCannonSystem.Current!=null&&HumanCannonSystem.Current.IsFlying(p.OwnerClientId),carry=HumanCannonCarry.Held(p.OwnerClientId)?.CarryName,local=p.IsLocal,below=PiggybackSystem.Current?.GetBelow(p) is NetworkPlayer below?(long)below.OwnerClientId:-1,above=PiggybackSystem.Current?.GetAbove(p) is NetworkPlayer above?(long)above.OwnerClientId:-1}).ToArray(),
                    saws=EscapeExitSaw.Devices.Where(s=>s!=null).Select(s=>new Launcher{name=s.name,carrier=s.Carrier,cuts=s.CutCableCount,total=s.RequiredCableCount,shots=s.ActiveProjectileCount,open=s.DoorOpen,position=s.CarryRoot.position,handle=s.Handle.position,guide=s.Guidance}).ToArray(),
                    ropesOpen=EscapeCooperativeRopes.Current!=null&&EscapeCooperativeRopes.Current.ReturnOpen,hiddenRope=EscapeCooperativeRopes.Current!=null?EscapeCooperativeRopes.Current.HiddenRopeIndex:-1,
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
