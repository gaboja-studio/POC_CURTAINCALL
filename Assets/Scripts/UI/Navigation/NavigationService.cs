using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Network.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.Settings;

namespace CurtainCall.UI.Wayfinding
{
    /// <summary>Owns route lifetimes and views; network transport lives in the other partial file.</summary>
    public sealed partial class NavigationService : MonoBehaviour
    {
        // Local guides refresh quickly so moving players/objects are tracked; shared guides keep network traffic low.
        const float LocalRefreshSeconds=.15f,NetworkRefreshSeconds=.4f,FailureGraceSeconds=1;
        const string Prefab="Prefabs/UIs/Navigation/V2/RehearsalNavigationPulse";
        static NavigationService instance;
        readonly Dictionary<string,NavigationHandle> handles=new();
        readonly Dictionary<string,Route> locals=new(),shared=new();
        readonly List<Vector3> calculated=new();
        NavigationPathPlanner planner;
        GameObject viewPrefab;
        float nextRefresh,nextNetwork;
        [Serializable] internal sealed class Request
        {
            public string id,scene;
            public bool remove,valid;
            public Vector3 start,goal;
            public NavigationAnchor startAnchor,goalAnchor;
            public Color color;
            public float speed=1,emphasis=1,interval,follow=-1,startGap;
        }
        [Serializable] internal sealed class Snapshot
        {
            public string key,scene;
            public Color color;
            public float speed,emphasis,interval,follow=-1,startGap;
            public double phase,anchor;
            public Vector3[] points=Array.Empty<Vector3>();
            public NavigationAnchor startAnchor,goalAnchor;
        }
        sealed class Route
        {
            public Request request;
            public ulong owner;
            public float received,lastValid=-999;
            public Snapshot state=new();
            public RehearsalNavigationView view;
            public NavigationPulseSequence pulse;
            public bool showing;
            public SilhouetteOutline startOutline,goalOutline;
            public Transform liveStart; // local player start, never serialized
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()=>instance=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()=>EnsureExists();
        public static NavigationService EnsureExists()
        {
            if(instance!=null)return instance;
            var go=new GameObject("World navigation service");DontDestroyOnLoad(go);
            instance=go.AddComponent<NavigationService>();return instance;
        }
        void Awake()
        {
            if(instance!=null&&instance!=this){Destroy(gameObject);return;}
            instance=this;planner=gameObject.AddComponent<NavigationPathPlanner>();
            SceneManager.sceneUnloaded+=SceneUnloaded;
        }
        internal NavigationHandle Show(NavigationTarget goal,NavigationTarget start,bool sync,float? speed,float? emphasis,Color? color)
        {
            var scene=SceneManager.GetActiveScene();
            var handle=new NavigationHandle{Service=this,SceneHandle=scene.handle,ScenePath=scene.path};
            handles.Add(handle.Id,handle);Change(handle,goal,start,sync,speed,emphasis,color);return handle;
        }
        internal void Change(NavigationHandle h,NavigationTarget goal,NavigationTarget start,bool sync,float? speed,float? emphasis,Color? color)
        {
            if(h.Synchronized!=sync)
            {
                if(h.Synchronized)SendRequest(new Request{id=h.Id,remove=true});
                else if(locals.Remove(h.Id,out var old))DestroyRoute(old);
                h.SetVisible(false);
            }
            h.Start=start;h.Destination=goal;h.Synchronized=sync;h.Speed=speed.HasValue?SafeSpeed(speed.Value):null;h.Emphasis=emphasis.HasValue?SafeEmphasis(emphasis.Value):null;h.Color=color.HasValue?SafeColor(color.Value):null;
            nextRefresh=nextNetwork=0;
        }
        internal void SetInterval(NavigationHandle h,float? seconds)
        {float v=seconds.HasValue?SafeInterval(seconds.Value):0;h.Interval=v>0?v:(float?)null;nextRefresh=nextNetwork=0;}
        internal void SetFollow(NavigationHandle h,float? seconds)
        {float v=seconds.HasValue?SafeFollow(seconds.Value):-1;h.Follow=v>=0?v:(float?)null;nextRefresh=nextNetwork=0;}
        internal void Remove(NavigationHandle h)
        {
            if(h.Synchronized)SendRequest(new Request{id=h.Id,remove=true});
            if(locals.Remove(h.Id,out var route))DestroyRoute(route);
            if(shared.Remove(OwnKey(h.Id),out var remote))DestroyRoute(remote);
            handles.Remove(h.Id);
        }
        internal Vector3[] GetPath(NavigationHandle h)
        {
            var routes=h.Synchronized?shared:locals;string key=h.Synchronized?OwnKey(h.Id):h.Id;
            return routes.TryGetValue(key,out var r)?(Vector3[])r.state.points.Clone():Array.Empty<Vector3>();
        }
        public void InvalidatePaths()=>planner.Invalidate();

        void Update()
        {
            BindNetwork();
            if(Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+LocalRefreshSeconds;
                bool network=Time.unscaledTime>=nextNetwork;
                if(network)nextNetwork=Time.unscaledTime+NetworkRefreshSeconds;
                foreach(var h in handles.Values.ToArray())
                {
                    if(h.Synchronized&&!network)continue;
                    bool a=h.Start.TryGetPosition(out var from),b=h.Destination.TryGetPosition(out var to);
                    // The requester resolves the common settings, so a shared guide shows the same values on every screen.
                    var common=GameSettings.Base.Navigation;
                    var request=new Request{id=h.Id,scene=h.ScenePath,valid=a&&b,start=from,goal=to,color=SafeColor(h.Color??common.Color),
                        speed=SafeSpeed(h.Speed??common.AnimationSpeed),emphasis=SafeEmphasis(h.Emphasis??common.Brightness),
                        interval=SafeInterval(h.Interval??common.PulseIntervalSeconds),follow=SafeFollow(h.Follow??common.FollowSeconds),
                        startGap=h.Start.IsPlayer?common.PlayerStartGap:0,
                        startAnchor=NavigationAnchor.From(h.Start,h.Synchronized),goalAnchor=NavigationAnchor.From(h.Destination,h.Synchronized)};
                    if(h.Synchronized)SendRequest(request);
                    else
                    {
                        if(!locals.TryGetValue(h.Id,out var route))locals[h.Id]=route=NewRoute(h.Id,request,Time.unscaledTimeAsDouble);
                        UpdateRequest(route,request,Time.unscaledTimeAsDouble);route.liveStart=h.Start.PlayerTransform;
                        Calculate(route);RenderPath(route);
                    }
                }
                if(IsServer&&network)
                {
                    foreach(var pair in shared.ToArray())
                    {
                        if(Time.unscaledTime-pair.Value.received>3 || !PeerConnected(pair.Value.owner))
                        {DestroyRoute(pair.Value);shared.Remove(pair.Key);continue;}
                        Calculate(pair.Value);RenderPath(pair.Value);
                    }
                    Publish();
                }
            }
            foreach(var route in locals.Values)Animate(route,Time.unscaledTimeAsDouble);
            foreach(var route in shared.Values)Animate(route,ServerClock);
            foreach(var h in handles.Values.ToArray())
            {
                var routes=h.Synchronized?shared:locals;string key=h.Synchronized?OwnKey(h.Id):h.Id;
                h.SetVisible(routes.TryGetValue(key,out var r)&&r.showing&&r.state.emphasis>0&&r.state.color.a>0);
            }
        }

        static Route NewRoute(string key,Request request,double now)
        {
            var route=new Route();route.state.key=key;route.state.anchor=now;
            UpdateRequest(route,request,now);return route;
        }
        static void UpdateRequest(Route route,Request request,double now)
        {
            if(route.request!=null && (Vector3.Distance(route.request.goal,request.goal)>2 || route.request.scene!=request.scene))
            {route.state.points=Array.Empty<Vector3>();route.lastValid=-999;}
            route.state.phase+=Math.Max(0,now-route.state.anchor)*route.state.speed;
            route.state.anchor=now;route.state.color=SafeColor(request.color);route.state.speed=SafeSpeed(request.speed);
            route.state.emphasis=SafeEmphasis(request.emphasis);route.state.interval=SafeInterval(request.interval);route.state.follow=SafeFollow(request.follow);route.state.startGap=SafeGap(request.startGap);route.state.scene=request.scene;
            route.state.startAnchor=request.startAnchor;route.state.goalAnchor=request.goalAnchor;
            route.request=request;route.received=Time.unscaledTime;
        }
        void Calculate(Route route)
        {
            if(!route.request.valid){route.state.points=Array.Empty<Vector3>();route.lastValid=-999;return;}
            if(planner.TryCalculate(route.request.start,route.request.goal,calculated) && calculated.Count <= MaxPoints)
            {route.state.points=calculated.ToArray();route.lastValid=Time.unscaledTime;}
            else if(Time.unscaledTime-route.lastValid>FailureGraceSeconds)route.state.points=Array.Empty<Vector3>();
        }
        void RenderPath(Route route)
        {
            bool inScene=string.IsNullOrEmpty(route.state.scene)||SceneManager.GetSceneByPath(route.state.scene).isLoaded;
            route.showing=inScene&&route.state.points.Length>=2;
            if(!route.showing)
            {
                if(route.view!=null){route.view.Hide();route.pulse.SetEndpointPresentation(false,false,Vector3.zero);}
                ReleaseOutlines(route);return;
            }
            if(route.view==null)
            {
                if(viewPrefab==null)viewPrefab=Resources.Load<GameObject>(Prefab);
                if(viewPrefab==null){route.showing=false;Debug.LogError("[Navigation] Missing pulse prefab: "+Prefab);return;}
                var root=Instantiate(viewPrefab,transform);root.name="Navigation "+route.state.key;
                route.view=root.GetComponent<RehearsalNavigationView>();route.pulse=root.GetComponent<NavigationPulseSequence>();
                route.pulse.AutomaticPlayback=false;route.view.Hide();
            }
            route.view.SetPath(route.state.points,true);
            route.pulse.SetAppearance(route.state.color,route.state.emphasis);
            route.pulse.SetPulseIntervalOverride(route.state.interval);
            route.view.SetFollowOverride(route.state.follow);
            route.view.SetPlayerStartGap(route.state.startGap,LiveStart(route));
            route.pulse.SetEndpointPresentation(route.state.goalAnchor?.outlined==true,route.state.startAnchor?.outlined==true,route.state.points[0]);
            UpdateOutline(ref route.startOutline,route.state.startAnchor,route.state);
            UpdateOutline(ref route.goalOutline,route.state.goalAnchor,route.state);
        }
        /// <summary>Live player transform for the start fade: own local guides, own shared guides, or a resolvable player anchor.</summary>
        Transform LiveStart(Route route)
        {
            if(route.state.startGap<=0)return null;
            if(route.liveStart!=null)return route.liveStart;
            string key=route.state.key;int slash=key?.IndexOf('/')??-1;
            if(slash>=0&&OwnKey(key.Substring(slash+1))==key&&handles.TryGetValue(key.Substring(slash+1),out var own))return own.Start.PlayerTransform;
            return route.state.startAnchor?.Resolve(manager);
        }
        void UpdateOutline(ref SilhouetteOutline outline,NavigationAnchor anchor,Snapshot state)
        {
            var target=anchor?.Resolve(manager);
            if(outline!=null&&outline.Target!=target){outline.Dispose();outline=null;}
            if(target==null)return;
            var color=state.color;color.a*=state.emphasis;
            if(outline==null)outline=new SilhouetteOutline(target,color,3);
            outline.Color=color;
        }
        static void ReleaseOutlines(Route route)
        {
            route.startOutline?.Dispose();route.goalOutline?.Dispose();route.startOutline=null;route.goalOutline=null;
        }
        static void Animate(Route route,double now)
        {
            if(route.pulse!=null&&route.showing)
                route.pulse.EvaluateAt((float)(route.state.phase+Math.Max(0,now-route.state.anchor)*route.state.speed));
        }
        void DestroyRoute(Route route){ReleaseOutlines(route);if(route.view!=null)Destroy(route.view.gameObject);}
        void SceneUnloaded(Scene scene)
        {
            foreach(var h in handles.Values.Where(h=>h.SceneHandle==scene.handle).ToArray())h.Dispose();
            foreach(var pair in shared.Where(p=>p.Value.state.scene==scene.path).ToArray()){DestroyRoute(pair.Value);shared.Remove(pair.Key);}
        }
        void OnDestroy()
        {
            SceneManager.sceneUnloaded-=SceneUnloaded;UnbindNetwork();
            foreach(var route in locals.Values)ReleaseOutlines(route);
            foreach(var route in shared.Values)ReleaseOutlines(route);
            foreach(var h in handles.Values.ToArray()){h.Disposed=true;h.Service=null;h.SetVisible(false);}
            if(instance==this)instance=null;
        }
        internal static float SafeSpeed(float value)=>float.IsFinite(value)?Mathf.Clamp(value,0,10):1;
        /// <summary>0 keeps the prefab pulse interval.</summary>
        /// <summary>Negative keeps the prefab follow time; otherwise 0–2 seconds.</summary>
        internal static float SafeFollow(float value)=>float.IsFinite(value)&&value>=0?Mathf.Min(value,2):-1;
        internal static float SafeGap(float value)=>float.IsFinite(value)?Mathf.Clamp(value,0,5):0;
        internal static float SafeInterval(float value)=>float.IsFinite(value)&&value>0?Mathf.Clamp(value,.2f,10):0;
        internal static float SafeEmphasis(float value)=>float.IsFinite(value)?Mathf.Clamp01(value):1;
        internal static Color SafeColor(Color color)=>float.IsFinite(color.r)&&float.IsFinite(color.g)&&float.IsFinite(color.b)&&float.IsFinite(color.a)
            ?new Color(Mathf.Clamp01(color.r),Mathf.Clamp01(color.g),Mathf.Clamp01(color.b),Mathf.Clamp01(color.a)):WorldNavigation.DefaultColor;
    }
}
