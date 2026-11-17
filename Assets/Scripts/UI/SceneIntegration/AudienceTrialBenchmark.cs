#if UNITY_EDITOR || CURTAINCALL_AUDIENCE_BENCHMARK
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Network.Session;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Results;
using CurtainCall.SpaceLink;
using CurtainCall.Settings;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CurtainCall.UI.SceneIntegration {
 // Opt-in local QA build only. No bootstrap or class in ordinary player builds.
 public sealed class AudienceTrialBenchmark : MonoBehaviour {
  const double PassSeconds=4;
  static readonly FieldInfo Toggle=typeof(AudienceFeedbackSettings).GetField("crowdTrial",BindingFlags.Instance|BindingFlags.NonPublic);
  string folder,role,stage; int players,pass=-1; bool running,host; double start,launchSeconds,nextEvent; long rxBytes,rxMessages;
  AudienceSectorTrial trial; ProfilerRecorder main,render,draws,gc; readonly FrameTiming[] timing=new FrameTiming[1];
  readonly List<object> results=new(); readonly double[][] samples=new double[6][]; bool validPass; int count; long rxStart,msgStart; int beforeReactions,beforeAudio;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
   if(Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(),"-audience-benchmark")<0)return;
   var go=new GameObject("AudienceTrialBenchmark"); DontDestroyOnLoad(go); go.AddComponent<AudienceTrialBenchmark>();
  }
  static string Arg(string key,string fallback){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
  async void Start() {
   folder=Arg("-audience-output",Path.Combine(Application.persistentDataPath,"AudienceBenchmark")); Directory.CreateDirectory(folder);
   role=Arg("-audience-role","host"); host=role=="host"; players=int.Parse(Arg("-audience-players","1"));
   try { await Setup(); } catch(Exception e){ File.WriteAllText(Path.Combine(folder,role+"-error.txt"),e.ToString()); Application.Quit(2); }
  }
  void Trace(string message) => File.AppendAllText(Path.Combine(folder,role+"-setup.txt"),Time.realtimeSinceStartupAsDouble.ToString("F2")+" "+message+Environment.NewLine);
  string StateDescription(){var entry=FindFirstObjectByType<CampaignPerformanceEntry>();return "scenes="+string.Join(",",Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).name))+"; ui="+(CommonPerformanceUI.Current!=null)+"; rewards="+(PerformanceRewards.Instance?.State.IsOpen==true)+"; rope="+(CurtainCall.Tightrope.TightropeCourse.Current!=null)+"; local="+(NetworkPlayer.Local!=null)+"; director="+(SpaceLinkDirector.Instance!=null)+"; entry="+(entry!=null)+"; placed="+(entry!=null&&entry.LocalPlaced)+"; uiLoading="+CommonUiSceneLoader.IsLoading+"; curtain="+SceneCurtainTransition.IsActive;}
  async Task Until(Func<bool> condition,int seconds=90){Trace("wait "+stage);double deadline=Time.realtimeSinceStartupAsDouble+Math.Min(seconds,int.Parse(Arg("-audience-setup-timeout","90")));double nextTrace=Time.realtimeSinceStartupAsDouble+5;while(!condition()){if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Benchmark setup: "+role+" / "+stage+" / "+StateDescription());if(Time.realtimeSinceStartupAsDouble>=nextTrace){Trace(stage+" / "+StateDescription());nextTrace+=5;}await Task.Delay(100);}Trace("ready "+stage);}
  async Task Setup(){
   Application.runInBackground=true; await Task.Delay(1000); QualitySettings.vSyncCount=0; Application.targetFrameRate=-1; AudioListener.volume=0; Screen.SetResolution(1280,720,FullScreenMode.Windowed);
   var session=NetworkSessionManager.EnsureExists(); stage="connect";
   // This QA shortcut skips Lobby's SpaceLinkHub. Match its per-peer bootstrap before a host launches.
   // The opt-in negative control reproduces the old shortcut for a paired regression run.
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-audience-legacy-entry")<0){CampaignRun.EnsureExists();SpaceLinkDirector.EnsureExists();await Task.Delay(100);}
   if(host){if(!await session.HostLanAsync(17945))throw new Exception("Host failed");File.WriteAllText(Path.Combine(folder,"host-ready"),"ready");await Until(()=>session.PlayerCount==players);}
   else {await Until(()=>File.Exists(Path.Combine(folder,"host-ready")));if(!await session.JoinLanAsync("127.0.0.1:17945"))throw new Exception("Join failed");}
   // Server connection count can precede a client's completed initial scene/player sync.
   stage="local player sync"; await Until(()=>NetworkPlayer.Local!=null && NetworkPlayer.Local.IsSpawned && NetworkPlayer.Local.Slot!=NetworkPlayer.NoSlot);
   File.WriteAllText(Path.Combine(folder,role+"-joined"),"ready");
   stage="all peers synced"; await Until(()=>Directory.GetFiles(folder,"*-joined").Length==players); await Task.Delay(500);
   NetworkManager.Singleton.NetworkConfig.NetworkTransport.OnTransportEvent+=Receive;
   NetworkManager.Singleton.SceneManager.OnSceneEvent+=value=>{if(!running)Trace("NGO "+value.SceneEventType+" "+value.SceneName+" client="+value.ClientId);};
   SceneManager.sceneLoaded+=(scene,mode)=>{if(!running)Trace("loaded "+scene.name+" "+mode);};
   double launched=Time.realtimeSinceStartupAsDouble;
   stage="dev launch";
   if(host){var launch=DevStartLauncher.EnsureExists();if(!launch.TryLaunch(new RunStartPoint(2,RunStartPhase.Performance,0,"Tightrope")))throw new Exception(launch.Status);await Until(()=>!launch.IsBusy);if(!launch.LastLaunchSucceeded)throw new Exception(launch.Status);}
   stage="performance UI/rewards/course/local player";
   await Until(()=>CommonPerformanceUI.Current!=null && PerformanceRewards.Instance!=null && PerformanceRewards.Instance.State.IsOpen && CurtainCall.Tightrope.TightropeCourse.Current!=null && NetworkPlayer.Local!=null);
   launchSeconds=Time.realtimeSinceStartupAsDouble-launched;
   await Task.Delay(1000); var mover=NetworkPlayer.Local.GetComponent<PlayerMover>();
   mover.Relocate(CurtainCall.Tightrope.TightropeCourse.Current.GetLanePosition(0,8+NetworkPlayer.Local.Slot*2)+Vector3.up*.05f,Quaternion.identity);
   await Task.Delay(300); mover.enabled=false;
   trial=UnityEngine.Object.FindFirstObjectByType<AudienceSectorTrial>(); Toggle.SetValue(GameSettings.Base.AudienceFeedback,true);
   stage="sector renderer"; await Until(()=>trial.SectorRenderer!=null); await Task.Delay(500); Toggle.SetValue(GameSettings.Base.AudienceFeedback,false);
   await Task.Delay(8000); QualitySettings.vSyncCount=0; Application.targetFrameRate=-1; Screen.SetResolution(1280,720,FullScreenMode.Windowed);
   for(int i=0;i<samples.Length;i++)samples[i]=new double[12000];
   main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1); render=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Render Thread",1);
   draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
   File.WriteAllText(Path.Combine(folder,role+"-ready"),"ready");
   stage="all peers warm";
   if(host){await Until(()=>Directory.GetFiles(folder,"*-ready").Length>=players);File.WriteAllText(Path.Combine(folder,"start-ticks"),DateTime.UtcNow.AddSeconds(3).Ticks.ToString());}
   await Until(()=>File.Exists(Path.Combine(folder,"start-ticks")));
   long ticks=long.Parse(File.ReadAllText(Path.Combine(folder,"start-ticks")));
   start=Time.realtimeSinceStartupAsDouble+(ticks-DateTime.UtcNow.Ticks)/(double)TimeSpan.TicksPerSecond;
   nextEvent=start+1.4; running=true;
  }
  void Receive(NetworkEvent evt,ulong id,ArraySegment<byte> payload,float at){if(evt==NetworkEvent.Data){rxBytes+=payload.Count;rxMessages++;}}
  void Update(){if(!running)return;try{Tick();}catch(Exception e){running=false;File.WriteAllText(Path.Combine(folder,role+"-error.txt"),e.ToString());Application.Quit(3);}}
  void Tick(){double elapsed=Time.realtimeSinceStartupAsDouble-start;if(elapsed<0)return;
   int current=(int)(elapsed/PassSeconds);if(current!=pass){if(pass>=0)FinishPass();if(current>=4){Finish();return;}pass=current;count=0;validPass=true;rxStart=rxBytes;msgStart=rxMessages;beforeReactions=trial.ReactionCount;beforeAudio=CommonPerformanceUI.Current.Feedback.ReactionAudio.PlayCount;Toggle.SetValue(GameSettings.Base.AudienceFeedback,pass%2==1);}
   if(host && Time.realtimeSinceStartupAsDouble>=nextEvent){nextEvent+=2.2;var state=PerformanceRewards.Instance.State;for(int i=0;i<state.Segments.Count;i++){var p=state.Segments[i];if(p.Connected&&!p.Dead)PerformanceRewards.Instance.AddAudienceReaction(p.ParticipantId,(i+(int)(elapsed/2.2))%2==0?6:-5);}}
   var alive=PerformanceRewards.Instance.State; if(!alive.IsOpen)validPass=false; for(int i=0;i<alive.Segments.Count;i++)if(alive.Segments[i].Connected&&alive.Segments[i].Dead)validPass=false;
   FrameTimingManager.CaptureFrameTimings(); uint got=FrameTimingManager.GetLatestTimings(1,timing);
   if(elapsed%PassSeconds<.5 || count>=samples[0].Length)return;
   samples[0][count]=main.Valid?main.LastValue/1e6:-1; samples[1][count]=render.Valid?render.LastValue/1e6:-1;
   samples[2][count]=got>0&&timing[0].gpuFrameTime>0?timing[0].gpuFrameTime:-1;
   samples[3][count]=draws.Valid?draws.LastValue:-1; samples[4][count]=gc.Valid?gc.LastValue:-1; samples[5][count]=Time.unscaledDeltaTime*1000; count++;
  }
  object Stats(double[] data){var a=data.Take(count).Where(v=>v>=0).OrderBy(v=>v).ToArray();return a.Length==0?null:new{samples=a.Length,median=a[a.Length/2],p95=a[Math.Min(a.Length-1,(int)(a.Length*.95))]};}
  void FinishPass(){results.Add(new{pass,on=pass%2==1,valid=validPass,frames=count,mainMs=Stats(samples[0]),renderMs=Stats(samples[1]),gpuMs=Stats(samples[2]),drawCalls=Stats(samples[3]),wholeFrameGcBytes=Stats(samples[4]),frameMs=Stats(samples[5]),receivedBytes=rxBytes-rxStart,receivedMessages=rxMessages-msgStart,crowdReactions=trial.ReactionCount-beforeReactions,personalReactions=CommonPerformanceUI.Current.Feedback.ReactionAudio.PlayCount-beforeAudio});}
  object MeasureObserve(){
   Toggle.SetValue(GameSettings.Base.AudienceFeedback,true);trial.Clear();
   var id=new PerformanceIdentity("33333333333333333333333333333333",1,1,0,"Tightrope");var rules=new RewardRules(10,5,100,0,0);
   RewardSnapshot Snapshot(int n)=>new(id.SessionId,1,n+1,0,id,true,rules,Enumerable.Range(0,4).Select(i=>new RewardSegment("bench"+i,(ulong)(i+1),i,0,0,false,true,50,n,10,10)).ToArray(),null,0,0,Array.Empty<FundEntry>());
   var states=Enumerable.Range(0,150).Select(Snapshot).ToArray();for(int i=0;i<20;i++)trial.Observe(states[i]);
   long before=GC.GetAllocatedBytesForCurrentThread();long ticks=System.Diagnostics.Stopwatch.GetTimestamp();for(int i=20;i<150;i++)trial.Observe(states[i]);
   double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-ticks)*1000.0/System.Diagnostics.Stopwatch.Frequency;
   long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
   return new{calls=130,allocatedBytes=bytes,totalMs=ms,includes="Observe only; snapshots prepared before measurement; four participants"};
  }
  void Finish(){running=false;
   try{var mesh=trial.SectorRenderer.GetComponent<MeshFilter>().sharedMesh;
    var report=new{role,players,passSeconds=PassSeconds,warmupSeconds=8,actualPlayers=NetworkSessionManager.Instance.PlayerCount,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,ramMiB=SystemInfo.systemMemorySize,width=Screen.width,height=Screen.height,development=Debug.isDebugBuild,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],vSync=QualitySettings.vSyncCount,targetFps=Application.targetFrameRate,launchSeconds,creationMs=trial.CreationMilliseconds,lastReactionLatencyMs=trial.LastReactionLatencySeconds*1000,rendererCount=1,triangles=mesh.triangles.Length/3,meshBytes=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh),networkObjects=trial.SectorRenderer.GetComponentsInChildren<NetworkObject>().Length,observe=MeasureObserve(),passes=results,note="Local same-machine LAN; all players render. Frame GC includes entire game; GPU null means unavailable. A/B does not prove internet or separate-PC performance."};
    File.WriteAllText(Path.Combine(folder,role+"-report.json"),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
   }catch(Exception e){File.WriteAllText(Path.Combine(folder,role+"-error.txt"),e.ToString());}
   main.Dispose();render.Dispose();draws.Dispose();gc.Dispose(); _ = QuitAfterReports();
  }
  async Task QuitAfterReports(){await Task.Delay(1500);Application.Quit();}
 }
}
#endif
