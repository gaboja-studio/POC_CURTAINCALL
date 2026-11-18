using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.PerformanceBackground;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.Presentation
{
    [DefaultExecutionOrder(1200)]
    public sealed class PerformanceFinaleLighting : MonoBehaviour
    {
        readonly Dictionary<Light,float> venueLights=new(), directionalLights=new();
        readonly Dictionary<Light,Color> lightColors=new();
        readonly Dictionary<PerformanceBackgroundMotion.LightPulse,float> pulses=new();
        readonly Dictionary<Renderer,MaterialPropertyBlock> originalBlocks=new();
        readonly Dictionary<Renderer,Color> decorativeBeams=new(), audienceBulbs=new();
        readonly Dictionary<Renderer,Material[]> originalMaterials=new();
        readonly Dictionary<Material,Material> emissionCopies=new();
        // Rendering Layer 7 is reserved for the performance-only fill. Keep GameObject/physics layers intact.
        public const uint PerformanceFillLayer=1u<<7;
        readonly Dictionary<Renderer,uint> originalRenderingLayers=new();
        readonly List<Renderer> fillRecipients=new();
        readonly List<GameObject> performanceRoots=new();
        readonly List<Light> indirectLights=new();
        float nextRecipientRefresh;
        readonly Dictionary<NetworkPlayer,PerformanceSpotlightFixture> assignments=new();
        readonly HashSet<PerformanceSpotlightFixture> occupied=new();
        readonly List<NetworkPlayer> performers=new(), removed=new();
        PerformanceSpotlightFixture[] fixtures=System.Array.Empty<PerformanceSpotlightFixture>();
        MaterialPropertyBlock block;
        TightropeCourse ropeCourse;
        HumanCannonDestination cannonFinish;
        Scene performance;
        Transform venue;
        Camera environmentCamera;
        Color cameraBackground, ambientSky, ambientEquator, ambientGround, fogColor;
        AmbientMode ambientMode;
        SphericalHarmonicsL2 ambientProbe;
        FogMode fogMode;
        bool fog, environmentCaptured;
        float fogDensity, cue;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"), Tint=Shader.PropertyToID("_Tint");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() { SceneManager.sceneLoaded-=AddToVenue; SceneManager.sceneLoaded+=AddToVenue; }
        static void AddToVenue(Scene scene,LoadSceneMode mode)
        {
            if(scene.path!=PerformanceBackgroundLayout.ScenePath)return;
            var go=new GameObject("Performance Finale Lighting");
            SceneManager.MoveGameObjectToScene(go,scene); go.AddComponent<PerformanceFinaleLighting>();
        }
        void Awake()=>block=new MaterialPropertyBlock();
        void LateUpdate()
        {
            var active=SceneManager.GetActiveScene();
            var loaded=PerformanceBackgroundLoader.BackgroundRoot;
            var settings=GameSettings.PerformanceLighting;
            bool needed=settings.enabled && loaded!=null && loaded.gameObject.activeInHierarchy && (active.name=="Tightrope" || active.name=="HumanCannon");
            if(!needed) { if(venue!=null)Restore(); return; }
            if(performance!=active || venue!=loaded)
            {
                Restore(); performance=active; venue=loaded; ropeCourse=null; cannonFinish=null;
                foreach(var root in active.GetRootGameObjects())
                {
                    if(ropeCourse==null)ropeCourse=root.GetComponentInChildren<TightropeCourse>();
                    if(cannonFinish==null)cannonFinish=root.GetComponentInChildren<HumanCannonDestination>();
                }
                Cache();
            }
            performers.Clear();
            foreach(var player in NetworkPlayer.All)
                if(player!=null && player.IsSpawned && player.State==PlayerState.Normal)performers.Add(player);
            performers.Sort((a,b)=>a.OwnerClientId.CompareTo(b.OwnerClientId));
            float progress=0;
            foreach(var player in performers)progress=Mathf.Max(progress,Progress(player.transform.position));
            float target=Mathf.InverseLerp(settings.finaleStart,Mathf.Max(settings.finaleStart+.001f,settings.finaleFull),progress);
            cue=Mathf.MoveTowards(cue,target,Time.unscaledDeltaTime/Mathf.Max(.1f,settings.finaleFadeSeconds));
            ApplyEnvironment(settings);
            RefreshFillRecipients();
            foreach(var pair in venueLights)if(pair.Key!=null)pair.Key.intensity=pair.Value*Brightness(pair.Key,settings);
            foreach(var light in indirectLights)if(light!=null)
            {
                light.color=settings.indirectColor;light.range=settings.indirectRange;
                light.spotAngle=settings.indirectAngle;light.innerSpotAngle=settings.indirectAngle*.4f;
            }
            foreach(var pair in pulses)if(pair.Key.target!=null)
            {
                // Preserve authored pulse phase while changing its baseline.
                float wave=pair.Key.restIntensity>0?pair.Key.target.intensity/pair.Key.restIntensity:1;
                pair.Key.restIntensity=pair.Value*Brightness(pair.Key.target,settings);
                pair.Key.target.intensity=pair.Key.restIntensity*wave;
            }
            foreach(var pair in directionalLights)if(pair.Key!=null)pair.Key.intensity=pair.Value*settings.directionalBrightness;
            foreach(var pair in lightColors)if(pair.Key!=null && pair.Key.name.StartsWith("Audience Wash"))pair.Key.color=settings.audienceColor;
            foreach(var pair in decorativeBeams)SetColor(pair.Key,pair.Value,settings.decorativeBeamBrightness,true);
            foreach(var pair in audienceBulbs)SetColor(pair.Key,pair.Value,settings.audienceBulbBrightness,false);
            foreach(var pair in emissionCopies)
            {
                float amount=pair.Key.name.StartsWith("Audience")?settings.audienceEmission:settings.backgroundEmission*Mathf.Lerp(1,settings.finaleBackground,cue);
                if(pair.Key.HasProperty("_EmissionColor"))pair.Value.SetColor("_EmissionColor",pair.Key.GetColor("_EmissionColor")*amount);
                if(pair.Key.HasProperty(BaseColor) && !pair.Key.name.StartsWith("Audience"))
                {
                    var color=pair.Key.GetColor(BaseColor);float alpha=color.a;color*=settings.backgroundSurface;color.a=alpha;pair.Value.SetColor(BaseColor,color);
                }
            }
            UpdateFixtures(settings);
        }
        void Cache()
        {
            fixtures=venue.GetComponentsInChildren<PerformanceSpotlightFixture>(true);
            foreach(var light in venue.GetComponentsInChildren<Light>(true))
            {
                if(light.GetComponentInParent<PerformanceSpotlightFixture>()!=null)continue;
                venueLights[light]=light.intensity; lightColors[light]=light.color;
                if(light.name.StartsWith("Indirect Fill"))indirectLights.Add(light);
            }
            foreach(var root in performance.GetRootGameObjects())foreach(var light in root.GetComponentsInChildren<Light>(true))
                if(light.type==LightType.Directional)directionalLights[light]=light.intensity;
            foreach(var motion in venue.GetComponentsInChildren<PerformanceBackgroundMotion>(true))foreach(var pulse in motion.practicalLights)
                if(pulse?.target!=null && !pulses.ContainsKey(pulse)){pulses[pulse]=pulse.restIntensity;venueLights.Remove(pulse.target);}
            foreach(var renderer in venue.GetComponentsInChildren<Renderer>(true))
            {
                originalRenderingLayers[renderer]=renderer.renderingLayerMask;
                renderer.renderingLayerMask&=~PerformanceFillLayer;
                var materials=renderer.sharedMaterials;
                bool changed=false;
                for(int i=0;i<materials.Length;i++)
                {
                    var source=materials[i];
                    if(source==null || source.shader.name!="Universal Render Pipeline/Lit")continue;
                    string name=source.name.ToLowerInvariant();
                    if(name.Contains("lamp") || name.Contains("neon") || name.Contains("glow") || name.Contains("hot") || name.Contains("ember"))continue;
                    if(!changed)originalMaterials[renderer]=renderer.sharedMaterials;
                    if(!emissionCopies.TryGetValue(source,out var copy))
                    {
                        copy=new Material(source){name=source.name+" (performance dimming)"};
                        emissionCopies.Add(source,copy);
                    }
                    materials[i]=copy;changed=true;
                }
                if(changed)renderer.sharedMaterials=materials;
                var material=renderer.sharedMaterial;
                if(material==null || !material.HasProperty(BaseColor))continue;
                bool beam=material.name.Contains("Beam") && renderer.GetComponentInParent<PerformanceSpotlightFixture>()==null;
                bool bulb=renderer.name.StartsWith("Audience Bulb");
                if(!beam && !bulb)continue;
                var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original);originalBlocks[renderer]=original;
                if(beam)decorativeBeams[renderer]=material.GetColor(BaseColor);
                if(bulb)audienceBulbs[renderer]=material.GetColor(BaseColor);
            }
            ambientMode=RenderSettings.ambientMode;ambientProbe=RenderSettings.ambientProbe;ambientSky=RenderSettings.ambientSkyColor;ambientEquator=RenderSettings.ambientEquatorColor;ambientGround=RenderSettings.ambientGroundColor;
            fog=RenderSettings.fog;fogMode=RenderSettings.fogMode;fogDensity=RenderSettings.fogDensity;fogColor=RenderSettings.fogColor;environmentCaptured=true;
        }
        void RefreshFillRecipients()
        {
            if(Time.unscaledTime<nextRecipientRefresh)return;
            nextRecipientRefresh=Time.unscaledTime+.5f;
            performanceRoots.Clear();performance.GetRootGameObjects(performanceRoots);
            foreach(var root in performanceRoots)
            {
                fillRecipients.Clear();root.GetComponentsInChildren(true,fillRecipients);
                foreach(var renderer in fillRecipients)AddFillRecipient(renderer);
            }
            foreach(var player in performers)
            {
                fillRecipients.Clear();player.GetComponentsInChildren(true,fillRecipients);
                foreach(var renderer in fillRecipients)AddFillRecipient(renderer);
            }
        }
        void AddFillRecipient(Renderer renderer)
        {
            if(renderer==null || renderer.transform.IsChildOf(venue))return;
            if(!originalRenderingLayers.ContainsKey(renderer))originalRenderingLayers[renderer]=renderer.renderingLayerMask;
            renderer.renderingLayerMask|=PerformanceFillLayer;
        }
        float Brightness(Light light,PerformanceLightingSettings s)
        {
            if(light.name.StartsWith("Indirect Fill"))return s.indirectFill?s.indirectBrightness*Mathf.Lerp(1,s.indirectFinaleBrightness,cue):0;
            float finale=Mathf.Lerp(1,s.finaleBackground,cue);
            if(light.name.StartsWith("Audience Wash"))return s.audienceBrightness*Mathf.Lerp(1,.78f,cue);
            if(light.name.StartsWith("Circus Accent"))return s.accentBrightness*finale;
            if(light.name.StartsWith("Route Key"))return s.courseBrightness*Mathf.Lerp(1,.75f,cue);
            var local=venue.InverseTransformPoint(light.transform.position)*PerformanceBackgroundLayout.Scale;
            float distance=new Vector2(local.x,local.y).magnitude;
            float falloff=Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.dimStartDistance,Mathf.Max(s.dimStartDistance+1,s.dimEndDistance),distance));
            return s.backgroundBrightness*Mathf.Lerp(1,s.distantBrightness,falloff)*finale;
        }
        void ApplyEnvironment(PerformanceLightingSettings s)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=s.ambientSky;RenderSettings.ambientEquatorColor=s.ambientEquator;RenderSettings.ambientGroundColor=s.ambientGround;
            var probe=new SphericalHarmonicsL2();probe.AddAmbientLight((s.ambientSky+s.ambientEquator+s.ambientGround)/3f);RenderSettings.ambientProbe=probe;
            RenderSettings.fog=s.fogDensity>0;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=s.fogDensity;RenderSettings.fogColor=s.fogColor;
            if(Camera.main!=environmentCamera)
            {
                if(environmentCamera!=null)environmentCamera.backgroundColor=cameraBackground;
                environmentCamera=Camera.main;if(environmentCamera!=null)cameraBackground=environmentCamera.backgroundColor;
            }
            if(environmentCamera!=null)environmentCamera.backgroundColor=s.fogColor;
        }
        void SetColor(Renderer renderer,Color color,float amount,bool alpha)
        {
            if(renderer==null)return;
            renderer.GetPropertyBlock(block);
            if(alpha)color.a*=amount;else {float a=color.a;color*=amount;color.a=a;}
            block.SetColor(BaseColor,color);renderer.SetPropertyBlock(block);
        }
        void UpdateFixtures(PerformanceLightingSettings s)
        {
            removed.Clear();foreach(var pair in assignments)if(!s.followSpots || !performers.Contains(pair.Key))removed.Add(pair.Key);
            foreach(var player in removed)assignments.Remove(player);
            occupied.Clear();foreach(var pair in assignments)if(pair.Value!=null)occupied.Add(pair.Value);
            if(s.followSpots)foreach(var player in performers)
            {
                assignments.TryGetValue(player,out var current);var best=current;
                float bestDistance=current!=null?Vector3.Distance(current.transform.position,player.transform.position)-s.fixtureHysteresis:float.MaxValue;
                foreach(var fixture in fixtures)
                {
                    if(fixture==null || fixture.head==null || fixture.spot==null || (occupied.Contains(fixture) && fixture!=current))continue;
                    float distance=Vector3.Distance(fixture.transform.position,player.transform.position);
                    if(distance<bestDistance){bestDistance=distance;best=fixture;}
                }
                if(best!=null){if(current!=null)occupied.Remove(current);occupied.Add(best);assignments[player]=best;}
            }
            for(int index=0;index<fixtures.Length;index++)
            {
                var fixture=fixtures[index];if(fixture==null || fixture.head==null || fixture.spot==null)continue;
                NetworkPlayer target=null;foreach(var pair in assignments)if(pair.Value==fixture){target=pair.Key;break;}
                fixture.fade=Mathf.MoveTowards(fixture.fade,target!=null?1:0,Time.unscaledDeltaTime/Mathf.Max(.05f,s.handoffSeconds));
                var spot=fixture.spot;
                if(target!=null)
                {
                    var focus=target.transform.position+Vector3.up*s.focusHeight;var direction=focus-fixture.head.position;
                    if(direction.sqrMagnitude>.01f)
                    {
                        var rotation=Quaternion.LookRotation(direction);
                        fixture.head.rotation=fixture.fade<.05f?rotation:Quaternion.Slerp(fixture.head.rotation,rotation,1-Mathf.Exp(-s.trackingSpeed*Time.unscaledDeltaTime));
                    }
                    float distance=Mathf.Max(1,Vector3.Distance(spot.transform.position,focus));
                    spot.spotAngle=Mathf.Clamp(2*Mathf.Atan(Mathf.Max(.3f,s.spotRadius)/distance)*Mathf.Rad2Deg,1,90);
                    spot.innerSpotAngle=spot.spotAngle*Mathf.Clamp(s.innerConeRatio,.1f,.98f);spot.range=distance*1.7f+Mathf.Max(4,s.spotRadius*2);
                    fixture.UpdateAperture(distance,Mathf.Max(.3f,s.spotRadius),Mathf.Clamp(s.innerConeRatio,.1f,.98f));
                    float performanceBrightness=performance.name=="Tightrope"?s.tightropeSpotBrightness:s.humanCannonSpotBrightness;
                    spot.intensity=s.performerIlluminance*Mathf.Max(0,performanceBrightness)*distance*distance*fixture.fade*Mathf.Lerp(1,s.finaleSpotBoost,cue);
                    if(fixture.beam!=null)
                    {
                        float scale=Mathf.Max(.0001f,fixture.beam.transform.parent.lossyScale.x);
                        // The cone starts at the lens and continues slightly beyond the performer.
                        // Keep its radius at the focus plane identical to the real spot's visible core.
                        const float extension=1.12f;
                        fixture.beam.transform.localPosition=Vector3.zero;
                        fixture.beam.transform.localRotation=Quaternion.identity;
                        fixture.beam.transform.localScale=new Vector3(s.spotRadius*s.beamWidth*extension/scale,s.spotRadius*s.beamWidth*extension/scale,distance*extension/scale);
                    }
                }
                else spot.intensity*=Mathf.Clamp01(1-Time.unscaledDeltaTime/Mathf.Max(.05f,s.handoffSeconds));
                spot.color=Color.Lerp(s.spotColor,s.secondarySpotColor,index%2==0?0:s.colorTint);spot.enabled=fixture.fade>.001f;
                spot.shadows=s.spotShadows?LightShadows.Soft:LightShadows.None;spot.shadowStrength=s.shadowStrength;
                if(fixture.beam!=null)
                {
                    fixture.beam.enabled=fixture.fade>.001f && s.beamOpacity>0;
                    fixture.beam.GetPropertyBlock(block);var color=spot.color;color.a=s.beamOpacity*fixture.fade;block.SetColor(Tint,color);fixture.beam.SetPropertyBlock(block);
                }
                if(fixture.lens!=null){fixture.lens.GetPropertyBlock(block);block.SetColor(BaseColor,spot.color*Mathf.Lerp(.035f,2,fixture.fade));fixture.lens.SetPropertyBlock(block);}
            }
        }
        float Progress(Vector3 position)
        {
            if(ropeCourse!=null){if(ropeCourse.FinishDistance<=0 || position.y<ropeCourse.RopeHeightAt(position)-2)return 0;return ropeCourse.GetDistance(position)/ropeCourse.FinishDistance;}
            var cannon=HumanCannonSystem.Current;
            if(cannonFinish?.Zone==null || cannon==null || cannon.StationCount==0 || cannon.Station(0)==null)return 0;
            var start=cannon.Station(0).Muzzle.position;var finish=cannonFinish.Zone.transform.position;if(position.y<finish.y-8)return 0;
            var axis=Vector3.ProjectOnPlane(finish-start,Vector3.up);return axis.sqrMagnitude>.001f?Vector3.Dot(position-start,axis)/axis.sqrMagnitude:0;
        }
        void OnDisable()=>Restore();
        void Restore()
        {
            foreach(var pair in venueLights)if(pair.Key!=null)pair.Key.intensity=pair.Value;
            foreach(var pair in directionalLights)if(pair.Key!=null)pair.Key.intensity=pair.Value;
            foreach(var pair in lightColors)if(pair.Key!=null)pair.Key.color=pair.Value;
            foreach(var pair in pulses)if(pair.Key.target!=null){pair.Key.restIntensity=pair.Value;pair.Key.target.intensity=pair.Value;}
            foreach(var pair in originalBlocks)if(pair.Key!=null)pair.Key.SetPropertyBlock(pair.Value);
            foreach(var pair in originalRenderingLayers)if(pair.Key!=null)
                pair.Key.renderingLayerMask=(pair.Key.renderingLayerMask&~PerformanceFillLayer)|(pair.Value&PerformanceFillLayer);
            originalRenderingLayers.Clear();indirectLights.Clear();nextRecipientRefresh=0;
            foreach(var pair in originalMaterials)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
            foreach(var copy in emissionCopies.Values)if(copy!=null)Destroy(copy);
            originalMaterials.Clear();emissionCopies.Clear();
            foreach(var fixture in fixtures)if(fixture!=null){fixture.fade=0;fixture.ReleaseAperture();if(fixture.spot!=null)fixture.spot.enabled=false;if(fixture.beam!=null)fixture.beam.enabled=false;if(fixture.lens!=null)fixture.lens.SetPropertyBlock(null);}
            if(environmentCaptured && SceneManager.GetActiveScene()==performance && venue!=null && venue.gameObject.activeInHierarchy)
            {
                RenderSettings.ambientMode=ambientMode;RenderSettings.ambientProbe=ambientProbe;RenderSettings.ambientSkyColor=ambientSky;RenderSettings.ambientEquatorColor=ambientEquator;RenderSettings.ambientGroundColor=ambientGround;
                RenderSettings.fog=fog;RenderSettings.fogMode=fogMode;RenderSettings.fogDensity=fogDensity;RenderSettings.fogColor=fogColor;if(environmentCamera!=null)environmentCamera.backgroundColor=cameraBackground;
            }
            environmentCamera=null;environmentCaptured=false;venue=null;cue=0;
            venueLights.Clear();directionalLights.Clear();lightColors.Clear();pulses.Clear();originalBlocks.Clear();decorativeBeams.Clear();audienceBulbs.Clear();assignments.Clear();fixtures=System.Array.Empty<PerformanceSpotlightFixture>();
        }
    }
}
