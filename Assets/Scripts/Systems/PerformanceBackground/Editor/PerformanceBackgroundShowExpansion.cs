using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.PerformanceBackground.Editor
{
    /// <summary>One-time authoring pass for additional moving rigs, saw contact sparks and vertical show lighting.</summary>
    public static class PerformanceBackgroundShowExpansion
    {
        const string Folder="Assets/_ThirdParty/curtaincall/PerformanceBackground/ShowExpansion";
        sealed class Geometry
        {
            public Transform parent; public Material material;
            public readonly List<Vector3> vertices=new(),normals=new();
            public readonly List<int> triangles=new();
        }
        static readonly List<Geometry> geometry=new();
        static Material iron,steel,brass,warmLens,coolLens;
        static readonly Vector3 Center=new(0,0,80);

        public static string Build()
        {
            var venue=GameObject.Find("PerformanceBackground/CircularVenue")?.transform;
            if(venue==null||venue.Find("31_ShowExpansion")!=null)throw new InvalidOperationException("Open the background; expansion must only run once.");
            var motion=venue.GetComponentInChildren<PerformanceBackgroundMotion>();
            PerformanceBackgroundMotionEditor.StopPreview();motion.RestoreRestPose();geometry.Clear();
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_ThirdParty/curtaincall/PerformanceBackground","ShowExpansion");
            Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Add crane frames, saw sparks and vertical spotlights");
            Undo.RecordObject(motion,"Expand decorative show motion");
            var root=New("31_ShowExpansion",venue);
            var city=venue.Find("25_Undercity");
            iron=city.Find("Tier0_Iron").GetComponent<Renderer>().sharedMaterial;
            steel=city.Find("Tier0_Steel").GetComponent<Renderer>().sharedMaterial;
            brass=city.Find("Tier0_Bronze").GetComponent<Renderer>().sharedMaterial;
            warmLens=Lens("WarmRigLens",new Color(4,1.5f,.3f,1));coolLens=Lens("CoolRigLens",new Color(.3f,1.1f,3.5f,1));
            var cranes=motion.cranes.ToList();var lights=motion.showLights.ToList();var pulses=motion.practicalLights.ToList();
            var surfaces=motion.surfaces.ToList();
            float[] heights={-125,-220,-340,-465,-650,-845};
            for(int i=0;i<6;i++)
            {
                float angle=15+i*60;var radial=Quaternion.Euler(0,angle,0)*Vector3.forward;
                var rig=New("SharedTowerCrane_"+(i+1).ToString("00"),root);
                rig.SetPositionAndRotation(Center+radial*535+Vector3.up*heights[i],Quaternion.LookRotation(-radial));
                // Crosshead ends meet the four existing shared-tower posts, not an invented ground pillar.
                foreach(float z in new[]{-13f,13f})Beam(rig,new Vector3(-19,0,z),new Vector3(19,0,z),2,iron);
                foreach(float x in new[]{-19f,19f})Beam(rig,new Vector3(x,0,-13),new Vector3(x,0,13),2,iron);
                Beam(rig,new Vector3(0,0,-13),new Vector3(0,0,13),2,iron);
                Box(rig,new Vector3(0,7,0),new Vector3(5,14,5),Quaternion.identity,iron);
                foreach(float side in new[]{-1f,1f})Beam(rig,new Vector3(side*16,0,0),new Vector3(0,13,0),1.2f,steel);
                Box(rig,new Vector3(0,14,0),new Vector3(9,2,9),Quaternion.identity,brass);
                var arm=New("SlewingLuffingFrame",rig);arm.localPosition=new Vector3(0,15,0);
                float length=48+i%3*5;
                foreach(float x in new[]{-4f,4f})foreach(float y in new[]{-3f,3f})Beam(arm,new Vector3(x,y,0),new Vector3(x,y,length),.9f,iron);
                int cells=6;
                for(int j=0;j<=cells;j++)
                {
                    float z=length*j/cells;
                    Beam(arm,new Vector3(-4,-3,z),new Vector3(4,-3,z),.65f,steel);
                    Beam(arm,new Vector3(-4,3,z),new Vector3(4,3,z),.65f,steel);
                    foreach(float x in new[]{-4f,4f})
                    {
                        Beam(arm,new Vector3(x,-3,z),new Vector3(x,3,z),.65f,steel);
                        if(j<cells)Beam(arm,new Vector3(x,-3,z),new Vector3(x,3,z+length/cells),.65f,steel);
                    }
                }
                Box(arm,new Vector3(0,0,-10),new Vector3(12,9,13),Quaternion.identity,iron);
                Box(arm,new Vector3(0,0,0),new Vector3(7,3,7),Quaternion.identity,brass);
                Beam(arm,new Vector3(0,3,0),new Vector3(0,14,6),1,steel);
                Beam(arm,new Vector3(0,14,6),new Vector3(0,3,length),.35f,steel);
                var track=Track(arm,new Vector3(4.5f+i%2*1.5f,10+i%3*2,0),76+i*11,i*.137f);
                cranes.Add(track);
                // Two mounted fixtures per rig: one sweeps upward and the other downward.
                for(int direction=0;direction<2;direction++)
                {
                    bool up=direction==0;bool warm=(i+direction)%2==0;
                    var head=New(up?"UpwardSpot":"DownwardSpot",arm);
                    head.localPosition=new Vector3(up?-2.5f:2.5f,-3.5f,length-3);
                    var aim=(Vector3.forward*.55f+(up?Vector3.up:Vector3.down)).normalized;
                    head.localRotation=Quaternion.LookRotation(aim);
                    Box(arm,head.localPosition+Vector3.up*1.3f,new Vector3(1.2f,2.6f,1.2f),Quaternion.identity,steel);
                    Box(head,new Vector3(0,0,-1.2f),new Vector3(3,3,3),Quaternion.identity,iron);
                    Box(head,new Vector3(0,0,.35f),new Vector3(2.4f,2.4f,.2f),Quaternion.identity,warm?warmLens:coolLens);
                    var light=head.gameObject.AddComponent<Light>();light.type=LightType.Spot;
                    light.color=warm?new Color(1,.44f,.12f):new Color(.26f,.52f,1);
                    light.intensity=up?12000:17000;light.range=155;light.spotAngle=34;light.innerSpotAngle=21;
                    light.shadows=LightShadows.None;light.renderMode=LightRenderMode.Auto;
                    lights.Add(Track(head,new Vector3(7,11,0),52+i*7+direction*5,(i*.173f+direction*.4f)%1));
                    pulses.Add(new PerformanceBackgroundMotion.LightPulse{target=light,restIntensity=light.intensity,amount=.08f,periodSeconds=13+i,phase=i*.15f});
                    var beam=New("VisibleShowBeam",head);var filter=beam.gameObject.AddComponent<MeshFilter>();
                    filter.sharedMesh=BeamMesh();var renderer=beam.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=BeamMaterial(warm);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    surfaces.Add(new PerformanceBackgroundMotion.SurfaceMotion{target=renderer,amount=.08f,periodSeconds=13+i,phase=i*.15f});
                }
            }
            var particles=motion.particles.Where(p=>p!=null&&!p.name.StartsWith("GrindingSparks_")).ToList();
            foreach(var old in motion.particles.Where(p=>p!=null&&p.name.StartsWith("GrindingSparks_")))
            {Undo.RecordObject(old.gameObject,"Replace sparse old sparks");old.gameObject.SetActive(false);}
            var sparkMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_ThirdParty/curtaincall/PerformanceBackground/Dynamic/SoftVenueMotes.mat");
            for(int i=0;i<motion.machinery.Length;i++)
            {
                var saw=motion.machinery[i].target;
                float radius=0;
                foreach(var filter in saw.GetComponentsInChildren<MeshFilter>())foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var local=saw.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    radius=Mathf.Max(radius,new Vector2(local.x,local.y).magnitude);
                }
                var normal=saw.forward;var contact=Vector3.ProjectOnPlane(Vector3.down,normal).normalized;
                if(contact.sqrMagnitude<.1f)contact=saw.up;
                var tangent=Vector3.Cross(normal,contact)*Mathf.Sign(motion.machinery[i].spinDegreesPerSecond.z);
                var anchor=New("SawContactSparks_"+(i+1).ToString("00"),saw.parent);
                anchor.SetPositionAndRotation(saw.position+contact*radius,Quaternion.LookRotation((tangent+contact*.35f+normal*.18f).normalized));
                var system=anchor.gameObject.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=system.main;main.loop=true;main.duration=6;main.playOnAwake=true;main.prewarm=false;
                main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.85f);main.startSpeed=new ParticleSystem.MinMaxCurve(17,36);
                main.startSize=new ParticleSystem.MinMaxCurve(.18f,.5f);main.startColor=new Color(6,2.3f,.35f,.95f);
                main.gravityModifier=.8f;main.maxParticles=120;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=system.emission;emission.rateOverTime=14;
                var burst=new ParticleSystem.Burst(.3f+i*.09f,(short)18,(short)32,0,2.1f+i%3*.35f){probability=.8f};
                emission.SetBursts(new[]{burst});
                var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=17;shape.radius=.35f;
                var colors=system.colorOverLifetime;colors.enabled=true;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.2f,.01f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.9f,.55f),new GradientAlphaKey(0,1)});colors.color=gradient;
                var size=system.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.1f));
                var renderer=anchor.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sparkMaterial;renderer.renderMode=ParticleSystemRenderMode.Stretch;
                renderer.velocityScale=.055f;renderer.lengthScale=2.4f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                system.useAutoRandomSeed=false;system.randomSeed=(uint)(871+i*37);particles.Add(system);
                // A small fixed contact shoe gives the grinding effect a visible physical source.
                var shoe=New("SawContactShoe_"+(i+1).ToString("00"),saw.parent);
                shoe.SetPositionAndRotation(anchor.position+contact*1.2f,Quaternion.LookRotation(normal));
                Box(shoe,Vector3.zero,new Vector3(3,1.8f,2.5f),Quaternion.identity,steel);
            }
            foreach(var batch in geometry)SaveGeometry(batch);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                if(renderer.sharedMaterial==warmLens||renderer.sharedMaterial==coolLens)
                    surfaces.Add(new PerformanceBackgroundMotion.SurfaceMotion{target=renderer,amount=.1f,periodSeconds=12,phase=surfaces.Count*.13f%1});
            motion.cranes=cranes.ToArray();motion.showLights=lights.ToArray();motion.practicalLights=pulses.ToArray();
            motion.surfaces=surfaces.ToArray();motion.particles=particles.ToArray();
            EditorUtility.SetDirty(motion);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(venue.gameObject.scene);
            EditorSceneManager.SaveScene(venue.gameObject.scene);
            return $"Added 6 independently slewing/luffing frames, 12 upward/downward spotlights and beams, 15 fixed-contact world-space spark systems. Totals: cranes={motion.cranes.Length}, lights={motion.showLights.Length}, particles={motion.particles.Length}. Saved neutral scene.";
        }

        static Transform New(string name,Transform parent){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Expand circus show");go.transform.SetParent(parent,false);return go.transform;}
        static PerformanceBackgroundMotion.Motion Track(Transform target,Vector3 degrees,float period,float phase){var t=new PerformanceBackgroundMotion.Motion{target=target,rotationDegrees=degrees,periodSeconds=period,phase=phase};t.CaptureRestPose();return t;}
        static Material Lens(string name,Color color){var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name};mat.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat");return mat;}
        static Material BeamMaterial(bool warm)
        {
            string path=Folder+(warm?"/WarmShowBeam.mat":"/CoolShowBeam.mat");var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat!=null)return mat;mat=new Material(Shader.Find("CurtainCall/ScenicHaze"));mat.SetColor("_BaseColor",warm?new Color(1.7f,.65f,.14f,.065f):new Color(.2f,.55f,1.6f,.055f));AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static Mesh BeamMesh()
        {
            string path=Folder+"/ShowBeam.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            const int sides=24,rings=9;
            for(int j=0;j<rings;j++)for(int i=0;i<=sides;i++)
            {float t=(float)j/(rings-1),a=i*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(a)*t*34,Mathf.Sin(a)*t*34,1+t*125));colors.Add(new Color(1,1,1,(1-t)*(1-t)));}
            for(int j=0;j<rings-1;j++)for(int i=0;i<sides;i++){int a=j*(sides+1)+i,b=a+sides+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            mesh=new Mesh{name="SoftShowCone"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void Beam(Transform parent,Vector3 a,Vector3 b,float width,Material mat)
        {var delta=b-a;var up=Mathf.Abs(delta.normalized.y)>.98f?Vector3.forward:Vector3.up;Box(parent,(a+b)*.5f,new Vector3(width,width,delta.magnitude),Quaternion.LookRotation(delta,up),mat);}
        static void Box(Transform parent,Vector3 center,Vector3 size,Quaternion rotation,Material mat)
        {
            var batch=geometry.FirstOrDefault(g=>g.parent==parent&&g.material==mat);
            if(batch==null){batch=new Geometry{parent=parent,material=mat};geometry.Add(batch);}
            var h=size*.5f;var v=new[]{new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z)};
            for(int i=0;i<8;i++)v[i]=center+rotation*v[i];
            foreach(var face in new[]{new[]{4,5,6,7},new[]{1,0,3,2},new[]{7,6,2,3},new[]{0,1,5,4},new[]{5,1,2,6},new[]{0,4,7,3}})
                foreach(int corner in new[]{0,1,2,0,2,3}){batch.triangles.Add(batch.vertices.Count);batch.vertices.Add(v[face[corner]]);batch.normals.Add(Vector3.Cross(v[face[1]]-v[face[0]],v[face[2]]-v[face[0]]).normalized);}
        }
        static void SaveGeometry(Geometry batch)
        {
            var mesh=new Mesh{name=batch.parent.name+"_"+batch.material.name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(batch.vertices);mesh.SetNormals(batch.normals);mesh.SetTriangles(batch.triangles,0);mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Folder+"/Rig_"+geometry.IndexOf(batch).ToString("000")+".asset");
            var go=New("Frame_"+batch.material.name,batch.parent);go.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.material;
        }
    }
}
