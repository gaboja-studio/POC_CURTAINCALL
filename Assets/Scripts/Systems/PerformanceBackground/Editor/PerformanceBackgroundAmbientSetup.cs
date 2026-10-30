using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.PerformanceBackground.Editor
{
    public static class PerformanceBackgroundAmbientSetup
    {
        const string AssetRoot = "Assets/_ThirdParty/curtaincall/PerformanceBackground/Dynamic";
        sealed class Piece
        {
            public List<int> indices;
            public Vector3[] points;
            public Vector3 center, normal;
            public float radius, thickness, roundness;
        }
        sealed class Source
        {
            public MeshFilter filter;
            public Mesh mesh;
            public int[] triangles;
            public List<Piece> pieces;
        }
        sealed class Saw
        {
            public Vector3 center, normal;
            public float radius;
            public Transform frame, pivot;
        }

        static GameObject Venue => GameObject.Find("PerformanceBackground/CircularVenue");
        static bool IsSawSource(string name) => name.StartsWith("HeroSawGantry_") || name.StartsWith("ThreateningMachinery_") || name.StartsWith("SawSurfaceDetails_");

        static List<Source> Sources()
        {
            var result = new List<Source>();
            foreach (var filter in Venue.GetComponentsInChildren<MeshFilter>())
            {
                if (!IsSawSource(filter.name) || filter.sharedMesh.vertexCount == 0) continue;
                var mesh = filter.sharedMesh;
                if (mesh.subMeshCount != 1) throw new InvalidOperationException("Expected single material source.");
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                var pieces = new List<Piece>();
                foreach (var indices in PerformanceBackgroundMotionSetup.ConnectedPieces(vertices, triangles))
                {
                    var points = indices.Select(i => triangles[i]).Distinct().Select(i => filter.transform.TransformPoint(vertices[i])).ToArray();
                    pieces.Add(Describe(indices, points));
                }
                result.Add(new Source { filter = filter, mesh = mesh, triangles = triangles, pieces = pieces });
            }
            return result;
        }

        static List<Saw> FindSaws(List<Source> sources)
        {
            var result = new List<Saw>();
            // Start with full disks. Small spokes/teeth around them are assigned in a second pass.
            foreach (var source in sources.OrderByDescending(s => s.filter.name.StartsWith("HeroSawGantry_")))
            foreach (var piece in source.pieces.OrderByDescending(p => p.radius))
            {
                if (piece.indices.Count < 180 || piece.radius < 5f || piece.radius > 40f || piece.thickness > 4.5f || piece.roundness < .68f) continue;
                if (result.Any(s => Vector3.Distance(s.center, piece.center) < Math.Max(3f, piece.radius * .15f))) continue;
                result.Add(new Saw { center = piece.center, normal = piece.normal, radius = piece.radius, frame = source.filter.transform.parent });
            }
            return result;
        }

        public static string InspectSaws()
        {
            var saws = FindSaws(Sources());
            return string.Join("\n", saws.Select((s,i) => $"Saw {i+1}: {s.center}, normal {s.normal}, radius {s.radius:F2}, parent {s.frame.name}"));
        }

        public static string Build()
        {
            if (Venue == null) throw new InvalidOperationException("Open performance_background.");
            var motion = Venue.GetComponentInChildren<PerformanceBackgroundMotion>();
            if (motion == null || motion.machinery.Length != 0) throw new InvalidOperationException("Base motion missing or ambience already installed.");
            PerformanceBackgroundMotionEditor.StopPreview();
            motion.RestoreRestPose();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Animate whole industrial circus");
            var sources = Sources();
            var saws = FindSaws(sources);
            if (saws.Count < 3) throw new InvalidOperationException("Could not identify the large saw disks.");
            var ambientRoot = NewObject("29_LivingAtmosphere", motion.transform.parent).transform;
            for (int i = 0; i < saws.Count; i++)
            {
                var saw = saws[i];
                saw.pivot = NewObject("SpinningSaw_" + (i+1).ToString("00"), saw.frame).transform;
                saw.pivot.SetPositionAndRotation(saw.center, Quaternion.LookRotation(saw.normal));
            }
            int sourceTriangles = 0, outputTriangles = 0, sourceNumber = 0;
            foreach (var source in sources)
            {
                sourceTriangles += source.triangles.Length / 3;
                var buckets = Enumerable.Range(0, saws.Count + 1).Select(_ => new List<int>()).ToArray();
                foreach (var piece in source.pieces)
                {
                    int winner = -1;
                    float nearest = float.MaxValue;
                    for (int i = 0; i < saws.Count; i++)
                    {
                        var saw = saws[i];
                        float distance = (piece.center - saw.center).sqrMagnitude;
                        if (distance >= nearest) continue;
                        bool inside = piece.points.All(p =>
                        {
                            var delta = p - saw.center;
                            float axial = Vector3.Dot(delta, saw.normal);
                            return Mathf.Abs(axial) <= 5.5f && (delta - axial*saw.normal).sqrMagnitude <= Mathf.Pow(saw.radius*1.16f,2);
                        });
                        if (inside) { nearest = distance; winner = i; }
                    }
                    foreach (int index in piece.indices) buckets[winner + 1].Add(source.triangles[index]);
                }
                outputTriangles += buckets.Sum(b => b.Count / 3);
                if (buckets.Skip(1).All(b => b.Count == 0)) continue;
                string prefix = "AmbientSaw_" + (sourceNumber++).ToString("00") + "_" + source.filter.name;
                Undo.RecordObject(source.filter, "Retain fixed machinery");
                source.filter.sharedMesh = PerformanceBackgroundMotionSetup.SaveSubset(source.mesh, buckets[0], prefix + "_Fixed");
                for (int i = 0; i < saws.Count; i++)
                    if (buckets[i+1].Count != 0) MeshObject(source.filter, source.mesh, buckets[i+1], prefix + "_Disk" + i, saws[i].pivot);
            }
            if (sourceTriangles != outputTriangles) throw new InvalidOperationException("Saw geometry conservation failed.");
            motion.machinery = saws.Where(s => s.pivot.childCount != 0).Select((s,i) =>
            {
                var track = Track(s.pivot, Vector3.zero, Vector3.zero, 60f, 0f);
                track.spinDegreesPerSecond = new Vector3(0f,0f,(i%2==0?1:-1)*(48f+i%5*9f));
                return track;
            }).ToArray();
            var atmosphere = new List<PerformanceBackgroundMotion.Motion>();
            var surfaces = new List<PerformanceBackgroundMotion.SurfaceMotion>();
            int fogs = AddFog(atmosphere, surfaces);
            int hanging = AddHanging(atmosphere, ambientRoot);
            int cables = AddCableSectors(atmosphere, ambientRoot);
            var lights = Venue.GetComponentsInChildren<Light>().Where(l => l.enabled && l.type != LightType.Directional).ToArray();
            motion.practicalLights = lights.Select((l,i) => new PerformanceBackgroundMotion.LightPulse
            {
                target=l, restIntensity=l.intensity, amount=l.type==LightType.Spot?.1f:.18f, periodSeconds=7f+i%9, phase=(i*.137f)%1f
            }).ToArray();
            int glow = 0;
            foreach (var renderer in Venue.GetComponentsInChildren<MeshRenderer>())
            {
                var material = renderer.sharedMaterial;
                if (material == null || material.shader.name != "Universal Render Pipeline/Unlit" || !material.HasProperty("_BaseColor")) continue;
                var color = material.GetColor("_BaseColor");
                if (Mathf.Max(color.r,color.g,color.b) < 1.2f) continue;
                surfaces.Add(new PerformanceBackgroundMotion.SurfaceMotion { target=renderer, amount=.2f, phase=glow*.113f%1f,periodSeconds=7f+glow%9 });
                glow++;
            }
            motion.atmosphere = atmosphere.ToArray();
            motion.surfaces = surfaces.ToArray();
            motion.particles = AddParticles(ambientRoot, saws);
            // Existing tracks predate restScale: capture their authored neutral poses once.
            foreach (var track in motion.cranes.Concat(motion.showLights)) track.CaptureRestPose();
            EditorUtility.SetDirty(motion);
            EditorSceneManager.MarkSceneDirty(Venue.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(Venue.scene);
            Selection.activeGameObject = motion.gameObject;
            return $"Saws={motion.machinery.Length}; fogs={fogs}; hanging={hanging}; cable sectors={cables}; pulsing lights={lights.Length}; glowing surfaces={glow}; particles={motion.particles.Length}; saw triangles preserved={sourceTriangles}";
        }

        static int AddFog(List<PerformanceBackgroundMotion.Motion> tracks, List<PerformanceBackgroundMotion.SurfaceMotion> surfaces)
        {
            var shader = Shader.Find("CurtainCall/FlowingVenueHaze");
            if (shader == null) throw new InvalidOperationException("Import FlowingVenueHaze first.");
            var materials = new Dictionary<Material,Material>();
            int count = 0;
            foreach (var renderer in Venue.GetComponentsInChildren<MeshRenderer>())
            {
                var original = renderer.sharedMaterial;
                if (original == null || original.shader.name != "CurtainCall/ScenicHaze" || (!renderer.name.Contains("Smog") && !renderer.name.Contains("Veil"))) continue;
                if (!materials.TryGetValue(original, out var material))
                {
                    material = new Material(shader) { name = original.name + "_Flowing" };
                    var color = original.GetColor("_BaseColor");
                    color.a *= 1.2f;
                    material.SetColor("_BaseColor", color);
                    AssetDatabase.CreateAsset(material, AssetRoot + "/" + material.name + ".mat");
                    materials.Add(original, material);
                }
                Undo.RecordObject(renderer, "Use flowing fog");
                renderer.sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,0);
                var track = Track(renderer.transform, new Vector3(0f,2f,0f), new Vector3(count%2==0?22:-18,4f+count%3*2f,12f), 78f+count*7f, count*.081f%1f);
                // Source fog vertices are baked far from origin: translation and shader turbulence avoid orbiting the venue.
                track.rotationDegrees = Vector3.zero;
                tracks.Add(track);
                surfaces.Add(new PerformanceBackgroundMotion.SurfaceMotion { target=renderer, flowingFog=true });
                count++;
            }
            return count;
        }

        static int AddHanging(List<PerformanceBackgroundMotion.Motion> tracks, Transform parent)
        {
            int count = 0;
            var banners = Venue.GetComponentsInChildren<Transform>().Where(t => t.name == "TornCircusBanner").ToArray();
            foreach (var banner in banners)
            {
                var renderers = banner.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                var pivot = NewObject("BannerWind_"+count, parent).transform;
                pivot.position = new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
                Undo.SetTransformParent(banner,pivot,"Anchor banner at top");
                foreach (var r in renderers) GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);
                tracks.Add(Track(pivot,new Vector3(1.1f,0f,2.1f),Vector3.zero,7f+count%5,count*.087f%1f));
                count++;
            }
            var cloth = Venue.transform.Find("25_Undercity/CircusRigging_Cloth");
            if (cloth != null) count += SplitSectors(cloth.GetComponent<MeshFilter>(), tracks, parent, "HangingVelvet", 12, new Vector3(1.4f,0f,2.4f), Vector3.zero,9f);
            return count;
        }

        static int AddCableSectors(List<PerformanceBackgroundMotion.Motion> tracks, Transform parent)
        {
            int count=0;
            foreach (string name in new[]{"UtilityCablesAndLights_Black","UtilityCablesAndLights_Amber"})
            {
                var t=Venue.transform.Find("25_Undercity/"+name);
                if(t!=null) count+=SplitSectors(t.GetComponent<MeshFilter>(),tracks,parent,name,8,new Vector3(.12f,0f,.18f),new Vector3(.2f,.65f,.2f),11f);
            }
            var festoon=Venue.transform.Find("04_CircusDrapery/OverheadFestoonRuns");
            if(festoon!=null) foreach(var filter in festoon.GetComponentsInChildren<MeshFilter>().ToArray())
                count+=SplitSectors(filter,tracks,parent,"Overhead_"+filter.name,6,new Vector3(.08f,0f,.12f),new Vector3(0f,.3f,.15f),13f);
            return count;
        }

        static int SplitSectors(MeshFilter source,List<PerformanceBackgroundMotion.Motion> tracks,Transform parent,string label,int count,Vector3 rotation,Vector3 travel,float period)
        {
            if(source==null || source.sharedMesh.vertexCount==0)return 0;
            var mesh=source.sharedMesh;
            var vertices=mesh.vertices;
            var triangles=mesh.triangles;
            var buckets=Enumerable.Range(0,count).Select(_=>new List<int>()).ToArray();
            foreach(var indices in PerformanceBackgroundMotionSetup.ConnectedPieces(vertices,triangles))
            {
                Vector3 center=Vector3.zero;
                foreach(int i in indices)center+=source.transform.TransformPoint(vertices[triangles[i]]);
                center/=indices.Count;
                float angle=Mathf.Atan2(center.x,center.z-80f)+Mathf.PI;
                int sector=Mathf.Clamp((int)(angle/(2f*Mathf.PI)*count),0,count-1);
                foreach(int i in indices)buckets[sector].Add(triangles[i]);
            }
            int actual=0;
            for(int i=0;i<count;i++)
            {
                if(buckets[i].Count==0)continue;
                var positions=buckets[i].Select(index=>source.transform.TransformPoint(vertices[index])).ToArray();
                var bounds=new Bounds(positions[0],Vector3.zero);
                foreach(var p in positions)bounds.Encapsulate(p);
                var pivot=NewObject(label+"_Wind"+i,parent).transform;
                pivot.position=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
                MeshObject(source,mesh,buckets[i],"Ambient_"+label+"_"+i,pivot);
                tracks.Add(Track(pivot,rotation,travel,period+i%4,(i*.113f)%1f));
                actual++;
            }
            Undo.RecordObject(source,"Separate hanging sectors");
            source.sharedMesh=PerformanceBackgroundMotionSetup.SaveSubset(mesh,new List<int>(),"Ambient_"+label+"_Empty");
            return actual;
        }

        static ParticleSystem[] AddParticles(Transform parent,List<Saw> saws)
        {
            var shader=Shader.Find("CurtainCall/FlowingVenueHaze");
            var material=new Material(shader){name="SoftVenueMotes"};
            material.SetFloat("_Particle",1);
            material.SetColor("_BaseColor",Color.white);
            AssetDatabase.CreateAsset(material,AssetRoot+"/SoftVenueMotes.mat");
            var systems=new List<ParticleSystem>();
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/3;
                var position=new Vector3(Mathf.Sin(angle)*290f,-85f-(i%4)*105f,80f+Mathf.Cos(angle)*290f);
                bool ember=i%2==0;
                systems.Add(Particles(ember?"FurnaceEmbers_"+i:"DriftingDust_"+i,parent,position,material,ember?new Color(3.6f,1.05f,.18f,.65f):new Color(.7f,.85f,1.2f,.32f),ember?1.2f:1.8f,24f,ember?3.5f:.5f,new Vector3(170,50,140),.8f,1.6f));
            }
            for(int i=0;i<Math.Min(6,saws.Count);i++)
            {
                var saw=saws[i];
                var contact=Vector3.ProjectOnPlane(Vector3.down,saw.normal).normalized;
                var system=Particles("GrindingSparks_"+(i+1),saw.frame,saw.center+contact*saw.radius,material,new Color(5f,2f,.3f,.85f),5f,1.2f,10f,new Vector3(1.2f,1f,1f),.35f,.8f);
                system.transform.rotation=Quaternion.LookRotation(contact+saw.normal*.2f);
                var main=system.main; main.gravityModifier=.35f;
                systems.Add(system);
            }
            return systems.ToArray();
        }

        static ParticleSystem Particles(string name,Transform parent,Vector3 position,Material material,Color color,float rate,float lifetime,float speed,Vector3 size,float minSize,float maxSize)
        {
            var go=NewObject(name,parent);
            go.transform.position=position;
            go.transform.rotation=Quaternion.Euler(-90f,0f,0f);
            var system=go.AddComponent<ParticleSystem>();
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;
            main.loop=true; main.duration=30f; main.startLifetime=new ParticleSystem.MinMaxCurve(lifetime*.65f,lifetime);
            main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.5f,speed);main.startSize=new ParticleSystem.MinMaxCurve(minSize,maxSize);
            main.startColor=color;main.maxParticles=90;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake=true;main.prewarm=true;
            var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=size;
            var emission=system.emission;emission.rateOverTime=rate;
            var noise=system.noise;noise.enabled=true;noise.strength=1.5f;noise.frequency=.15f;noise.scrollSpeed=.17f;
            var over=system.colorOverLifetime;over.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(.6f,.75f),new GradientAlphaKey(0,1)});
            over.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            system.useAutoRandomSeed=false;system.randomSeed=(uint)(systemsSeed++*37+17);
            return system;
        }
        static int systemsSeed;

        static GameObject NewObject(string name,Transform parent)
        {
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Animate venue");go.transform.SetParent(parent,false);return go;
        }
        static PerformanceBackgroundMotion.Motion Track(Transform target,Vector3 degrees,Vector3 travel,float period,float phase)
        {
            var track=new PerformanceBackgroundMotion.Motion{target=target,rotationDegrees=degrees,travel=travel,periodSeconds=period,phase=phase};track.CaptureRestPose();return track;
        }
        static void MeshObject(MeshFilter source,Mesh mesh,List<int> indices,string name,Transform parent)
        {
            var go=NewObject(name,source.transform.parent);
            go.transform.SetLocalPositionAndRotation(source.transform.localPosition,source.transform.localRotation);go.transform.localScale=source.transform.localScale;
            go.AddComponent<MeshFilter>().sharedMesh=PerformanceBackgroundMotionSetup.SaveSubset(mesh,indices,name);
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
            go.transform.SetParent(parent,true);
        }

        static Piece Describe(List<int> indices,Vector3[] points)
        {
            Vector3 center=Vector3.zero;foreach(var p in points)center+=p;center/=points.Length;
            var a=new double[3,3];var vectors=new double[3,3];for(int i=0;i<3;i++)vectors[i,i]=1;
            foreach(var p in points){var d=p-center;for(int i=0;i<3;i++)for(int j=0;j<3;j++)a[i,j]+=d[i]*d[j];}
            for(int iteration=0;iteration<20;iteration++)
            {
                int p=0,q=1;double largest=Math.Abs(a[0,1]);
                for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)if(Math.Abs(a[i,j])>largest){p=i;q=j;largest=Math.Abs(a[i,j]);}
                if(largest<1e-7)break;
                double angle=.5*Math.Atan2(2*a[p,q],a[q,q]-a[p,p]),c=Math.Cos(angle),s=Math.Sin(angle);
                double app=a[p,p],aqq=a[q,q],apq=a[p,q];
                for(int k=0;k<3;k++)if(k!=p&&k!=q){double akp=a[k,p],akq=a[k,q];a[k,p]=a[p,k]=c*akp-s*akq;a[k,q]=a[q,k]=s*akp+c*akq;}
                a[p,p]=c*c*app-2*s*c*apq+s*s*aqq;a[q,q]=s*s*app+2*s*c*apq+c*c*aqq;a[p,q]=a[q,p]=0;
                for(int k=0;k<3;k++){double vkp=vectors[k,p],vkq=vectors[k,q];vectors[k,p]=c*vkp-s*vkq;vectors[k,q]=s*vkp+c*vkq;}
            }
            var eigen=Enumerable.Range(0,3).OrderBy(i=>a[i,i]).ToArray();int axis=eigen[0];
            var normal=new Vector3((float)vectors[0,axis],(float)vectors[1,axis],(float)vectors[2,axis]).normalized;
            float radius=0,thickness=0;
            foreach(var p in points){var delta=p-center;float axial=Vector3.Dot(delta,normal);radius=Mathf.Max(radius,(delta-axial*normal).magnitude);thickness=Mathf.Max(thickness,Mathf.Abs(axial)*2f);}
            return new Piece{indices=indices,points=points,center=center,normal=normal,radius=radius,thickness=thickness,roundness=(float)(a[eigen[1],eigen[1]]/Math.Max(1e-8,a[eigen[2],eigen[2]]))};
        }
    }
}
