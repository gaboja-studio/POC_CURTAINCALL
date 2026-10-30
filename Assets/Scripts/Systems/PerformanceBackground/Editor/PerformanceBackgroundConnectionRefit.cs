using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.PerformanceBackground.Editor
{
    /// <summary>Authoring pass: fit existing geometry first, then build the missing joints/load paths.</summary>
    public static class PerformanceBackgroundConnectionRefit
    {
        const string Folder = "Assets/_ThirdParty/curtaincall/PerformanceBackground/Connections";
        static readonly Vector3 Center = new(0, 0, 80);
        static Transform venue, root;
        static PerformanceBackgroundMotion motion;
        static Material iron, steel, brass, cable, warm, cool;
        static readonly Dictionary<MeshFilter, Source> sources = new();
        static readonly List<Batch> batches = new();
        static readonly List<string> report = new();
        static readonly List<Floor> floors = new();
        static readonly List<Bridge> bridges = new();
        static readonly List<Bounds> posts = new();
        static Surface[] ceiling;
        static Surface[] seating;
        static int meshNumber, boxes, rods, bridgesAdjusted;

        sealed class Part
        {
            public int[] ids;
            public Bounds bounds;
            public Vector3 axis;
            public float length, width;
        }
        sealed class Source
        {
            public MeshFilter filter;
            public Mesh mesh;
            public Vector3[] vertices, normals;
            public int[] triangles;
            public Matrix4x4 toWorld, toLocal;
            public List<Part> parts;
            public bool changed;
            public readonly HashSet<Part> mapped = new();
            public readonly HashSet<int> removed = new();
        }
        sealed class Floor { public float y; public List<Vector2> hull; public Bounds bounds; public string source; public bool hidden,buried; public Source mesh; public Part[] members; }
        sealed class Bridge
        {
            public Source source;
            public Part part;
            public Vector3 oldA, oldB, a, b;
        }
        sealed class Surface
        {
            public Vector3[] vertices;
            public int[] triangles;
            public Bounds bounds;
        }
        sealed class Batch
        {
            public Transform parent;
            public Material material;
            public readonly List<Vector3> vertices = new(), normals = new();
            public readonly List<int> triangles = new();
        }

        public static string Build()
        {
            venue = GameObject.Find("PerformanceBackground/CircularVenue")?.transform;
            if (venue == null || venue.Find("30_ConnectedStructure") != null)
                throw new InvalidOperationException("Open the background; connection refit must only be applied once.");
            motion = venue.GetComponentInChildren<PerformanceBackgroundMotion>();
            if (motion == null) throw new InvalidOperationException("Background motion is missing.");
            PerformanceBackgroundMotionEditor.StopPreview();
            motion.RestoreRestPose();
            sources.Clear(); batches.Clear(); report.Clear(); floors.Clear(); bridges.Clear(); posts.Clear();
            meshNumber = boxes = rods = bridgesAdjusted = 0;
            Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Reconnect industrial circus background");
            Undo.RecordObject(motion,"Preserve motion settings while moving its attachments");
            root = NewObject("30_ConnectedStructure", venue).transform;
            var city = venue.Find("25_Undercity");
            iron = city.Find("Tier0_Iron").GetComponent<MeshRenderer>().sharedMaterial;
            steel = city.Find("Tier0_Steel").GetComponent<MeshRenderer>().sharedMaterial;
            brass = city.Find("Tier0_Bronze").GetComponent<MeshRenderer>().sharedMaterial;
            cable = city.Find("UtilityCablesAndLights_Black").GetComponent<MeshRenderer>().sharedMaterial;
            warm = city.Find("CityPractical_0") != null
                ? city.Find("HeroSawGantry_Amber").GetComponent<MeshRenderer>().sharedMaterial : brass;
            cool = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "ConnectedCoolLens" };
            cool.SetColor("_BaseColor", new Color(.35f, .75f, 2.8f, 1));
            AssetDatabase.CreateAsset(cool, Folder + "/ConnectedCoolLens.mat");
            Phase("roof"); FitRoof();
            Phase("deck survey"); ReadDecks();
            Phase("declutter overlaps"); DeclutterFloors();
            Phase("foundations"); FitFoundations();
            Phase("catwalks"); FitBridges();
            Phase("grandstands"); SupportSeating(); FitGallerySeats();
            Phase("cranes"); FitCranes();
            Phase("mechanical bearings"); SupportSaws();
            Phase("lighting mounts"); MountLighting();
            Phase("hanging attachments"); MountHanging();
            Phase("mesh save"); Flush();
            foreach (var track in motion.cranes.Concat(motion.showLights).Concat(motion.machinery).Concat(motion.atmosphere)) track.CaptureRestPose();
            EditorUtility.SetDirty(motion);
            EditorSceneManager.MarkSceneDirty(venue.gameObject.scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(venue.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            report.Add($"Generated joint geometry: {boxes} boxes, {rods} rods, {batches.Count} material/parent batches.");
            Phase("complete");
            return string.Join("\n", report);
        }

        static void Phase(string phase)
        {
            System.IO.File.WriteAllText("Temp/connection-refit-status.txt", phase + "\n" + string.Join("\n", report));
        }

        static Source Read(MeshFilter filter)
        {
            if (sources.TryGetValue(filter, out var cached)) return cached;
            var mesh = filter.sharedMesh;
            var source = new Source { filter=filter, mesh=mesh, vertices=mesh.vertices, normals=mesh.normals, triangles=mesh.triangles,
                toWorld=filter.transform.localToWorldMatrix, toLocal=filter.transform.worldToLocalMatrix, parts=new List<Part>() };
            foreach (var piece in PerformanceBackgroundMotionSetup.ConnectedPieces(source.vertices,source.triangles))
            {
                var ids = piece.Select(i=>source.triangles[i]).Distinct().ToArray();
                var points = ids.Select(i=>source.toWorld.MultiplyPoint3x4(source.vertices[i])).ToArray();
                var bounds = new Bounds(points[0],Vector3.zero);
                foreach(var p in points) bounds.Encapsulate(p);
                float xx=0,xz=0,zz=0;
                foreach(var p in points) {var d=p-bounds.center;xx+=d.x*d.x;xz+=d.x*d.z;zz+=d.z*d.z;}
                float angle=.5f*Mathf.Atan2(2*xz,xx-zz);
                var axis=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var side=Vector3.Cross(Vector3.up,axis);
                float minU=float.MaxValue,maxU=float.MinValue,minV=float.MaxValue,maxV=float.MinValue;
                foreach(var p in points){var d=p-bounds.center;float u=Vector3.Dot(d,axis),v=Vector3.Dot(d,side);minU=Mathf.Min(minU,u);maxU=Mathf.Max(maxU,u);minV=Mathf.Min(minV,v);maxV=Mathf.Max(maxV,v);}
                source.parts.Add(new Part {ids=ids,bounds=bounds,axis=axis,length=maxU-minU,width=maxV-minV});
            }
            sources.Add(filter,source);
            return source;
        }

        static void FitRoof()
        {
            float wallTop=venue.Find("09_RoundContinuousWall").GetComponentInChildren<Renderer>().bounds.max.y;
            var roof=venue.Find("11_IndustrialDomeShell").GetComponentsInChildren<MeshFilter>();
            float bottom=roof.Min(f=>f.GetComponent<Renderer>().bounds.min.y);
            float delta=wallTop-bottom;
            foreach(var f in roof){Undo.RecordObject(f.transform,"Seat dome on wall");f.transform.position+=Vector3.up*delta;}
            ceiling=roof.Select(SurfaceOf).ToArray();
            seating=venue.Find("26_CircusAudience").GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Ring")&&f.name.EndsWith("_Wood")).Select(SurfaceOf).ToArray();
            report.Add($"Dome lowered {Mathf.Abs(delta):F2}; dome rim/wall contact y={wallTop:F2}.");
        }

        static Surface SurfaceOf(MeshFilter f) => new Surface { vertices=f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray(),triangles=f.sharedMesh.triangles,bounds=f.GetComponent<Renderer>().bounds };

        static float UpHit(IEnumerable<Surface> surfaces,Vector3 point)
        {
            float best=float.PositiveInfinity;
            foreach(var surface in surfaces)
            {
                if(point.x<surface.bounds.min.x-.01f||point.x>surface.bounds.max.x+.01f||point.z<surface.bounds.min.z-.01f||point.z>surface.bounds.max.z+.01f)continue;
                var v=surface.vertices;var t=surface.triangles;
                for(int i=0;i<t.Length;i+=3)
                {
                    var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];
                    float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                    if(Mathf.Abs(d)<1e-7f)continue;
                    float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/d;
                    float w=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/d;
                    if(u<-.0001f||w<-.0001f||u+w>1.0001f)continue;
                    float y=u*a.y+w*b.y+(1-u-w)*c.y;
                    if(y>point.y+.01f)best=Mathf.Min(best,y);
                }
            }
            return best;
        }

        static void FitFoundations()
        {
            var city=venue.Find("25_Undercity");
            var foundation=venue.Find("27_DeepAbyss");
            foreach(var name in new[]{"LongFoundation_Iron","LongFoundation_Steel"})
            {
                var t=foundation.Find(name);Undo.RecordObject(t.gameObject,"Replace repeated platform foundations with shared supports");t.gameObject.SetActive(false);
            }
            int removed=0;
            foreach(var f in venue.GetComponentsInChildren<MeshFilter>().ToArray())
            {
                if(!(f.name.EndsWith("_Iron")||f.name.EndsWith("_Steel"))||f.sharedMesh.vertexCount==0)continue;
                string prefix=f.name.Substring(0,f.name.LastIndexOf('_'));
                var islands=floors.Where(p=>p.source==prefix&&!p.hidden).ToArray();
                if(islands.Length==0)continue;
                var source=Read(f);
                foreach(var part in source.parts)
                {
                    var floor=islands.OrderBy(p=>(Closest(p.hull,XZ(part.bounds.center))-XZ(part.bounds.center)).sqrMagnitude+.05f*Mathf.Abs(p.y-part.bounds.max.y)).First();
                    if(part.bounds.max.y<floor.y-2.5f || (part.bounds.size.y>12&&part.bounds.center.y<floor.y-7))
                    {Remove(source,part);removed++;}
                }
            }
            // Keep a small, regular set of perimeter piers; horizontal shared ties remain continuous.
            int perimeterRemoved=0;
            foreach(var f in foundation.GetComponentsInChildren<MeshFilter>())
            {
                if(!f.name.StartsWith("PerimeterShaft_"))continue;
                var source=Read(f);
                foreach(var part in source.parts)
                {
                    float angle=Mathf.Atan2(part.bounds.center.x,part.bounds.center.z-80)*Mathf.Rad2Deg;
                    bool bay=Mathf.Abs(Mathf.DeltaAngle(angle,Mathf.Round(angle/30)*30))<3;
                    if(!bay&&(part.bounds.size.y>4||f.name.EndsWith("_Blue")||f.name.EndsWith("_Ember")))
                    {Remove(source,part);perimeterRemoved++;}
                }
            }
            // Only the three hero crane towers retain independent full-height feet.
            var main=Read(city.Find("HeroSawGantry_Iron").GetComponent<MeshFilter>());
            var cranePosts=main.parts.Where(p=>p.bounds.size.y>80&&p.bounds.size.x<5&&p.bounds.size.z<5).ToArray();
            foreach(var part in cranePosts)
            {
                var p=part.bounds.center;float top=part.bounds.min.y-.5f;
                Beam(new Vector3(p.x,-1078.5f,p.z),new Vector3(p.x,top,p.z),2.6f,2.6f,iron);
                Box(new Vector3(p.x,-1079.25f,p.z),new Vector3(4,1.5f,4),Quaternion.identity,steel);
                Box(new Vector3(p.x,top+.5f,p.z),new Vector3(3.6f,1,3.6f),Quaternion.identity,steel);
                posts.Add(part.bounds);
            }
            var rings=new Dictionary<string,float>();
            NewObject("WallGirderSaddles",root);
            foreach(var level in floors.Where(p=>!p.hidden&&(p.source.StartsWith("Tier")||p.source.StartsWith("DistantDistrict"))).GroupBy(p=>p.source))
            {
                float y=level.Average(p=>p.y)-4;rings.Add(level.Key,y);
                for(int i=0;i<64;i++)
                {
                    var a=Polar(577,y,i*360f/64);var b=Polar(577,y,(i+1)*360f/64);Beam(a,b,1.5f,2,iron);
                    var midpoint=(a+b)*.5f;var wall=Center+Radial(midpoint)*581.7f;wall.y=y;
                    Beam(midpoint,wall,.8f,1.2f,steel);Box(wall,new Vector3(3,3,.8f),Quaternion.LookRotation(Radial(wall)),steel);
                }
            }
            int cantilevers=0;
            foreach(var floor in floors.Where(p=>!p.hidden))
            {
                var center=floor.hull.Aggregate(Vector2.zero,(s,p)=>s+p)/floor.hull.Count;
                var c=new Vector3(center.x,floor.bounds.min.y-.8f,center.y);var radial=Radial(c);var side=Vector3.Cross(Vector3.up,radial);
                float width=floor.hull.Max(p=>Vector2.Dot(p-center,XZ(side)))-floor.hull.Min(p=>Vector2.Dot(p-center,XZ(side)));
                var pads=new List<Vector3>();
                foreach(float sign in new[]{-1f,1f})
                {
                    var p=c+side*(Mathf.Min(12,width*.24f)*sign)-radial*3;
                    if(!Inside(floor.hull,XZ(p))){var edge=Closest(floor.hull,XZ(p));var inset=Vector2.Lerp(center,edge,.8f);p.x=inset.x;p.z=inset.y;}
                    var wall=Center+Radial(p)*581.7f;wall.y=p.y;
                    Beam(wall,p,1.1f,1.4f,iron);Beam(wall+Vector3.down*12,p,.8f,.8f,steel);
                    Box(wall+Vector3.down*4,new Vector3(5,14,.8f),Quaternion.LookRotation(Radial(p)),iron);
                    Box(p+Vector3.up*.45f,new Vector3(3,.7f,3),Quaternion.identity,steel);pads.Add(p);cantilevers++;
                    if(rings.TryGetValue(floor.source,out float y))Beam(wall,new Vector3(wall.x,y,wall.z),1.2f,1.2f,iron);
                }
                Beam(pads[0],pads[1],1,1.3f,steel);
            }
            report.Add($"Removed {removed} repeated under-platform column/brace pieces and {perimeterRemoved} dense perimeter members; old long foundations hidden. Only {cranePosts.Length} shared hero-tower feet remain.");
            report.Add($"Platforms use {cantilevers} short wall cantilevers and {rings.Count} shared wall girders, not individual ground columns.");
        }

        static void Remove(Source source,Part part){foreach(int id in part.ids)source.removed.Add(id);source.changed=true;}

        /// <summary>Recover the original layered industrial silhouette without a forest of independent platform legs.</summary>
        public static string RestoreIndustrialDensity()
        {
            venue=GameObject.Find("PerformanceBackground/CircularVenue")?.transform;
            if(venue==null)throw new InvalidOperationException("Open the performance background first.");
            var connected=venue.Find("30_ConnectedStructure");
            if(connected==null||connected.Find("SharedIndustrialFrames")!=null)throw new InvalidOperationException("Connection refit required; density pass must only run once.");
            motion=venue.GetComponentInChildren<PerformanceBackgroundMotion>();
            PerformanceBackgroundMotionEditor.StopPreview();motion.RestoreRestPose();
            sources.Clear();batches.Clear();floors.Clear();bridges.Clear();report.Clear();posts.Clear();
            boxes=rods=0;meshNumber=2000;
            Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Recover layered circus industrial atmosphere");
            root=NewObject("SharedIndustrialFrames",connected).transform;
            var city=venue.Find("25_Undercity");
            iron=city.Find("Tier0_Iron").GetComponent<MeshRenderer>().sharedMaterial;
            steel=city.Find("Tier0_Steel").GetComponent<MeshRenderer>().sharedMaterial;
            ReadDecks();int restored=0;
            // These belong to the enclosing shaft, not to individual platforms. Their layered
            // silhouettes and distant blue/amber accents establish the original depth scale.
            foreach(var f in venue.Find("27_DeepAbyss").GetComponentsInChildren<MeshFilter>())
            {
                if(!f.name.StartsWith("PerimeterShaft_"))continue;
                var original=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_ThirdParty/curtaincall/PerformanceBackground/DeepAbyss/"+f.name+".asset");
                if(original==null)throw new InvalidOperationException("Missing original shaft mesh: "+f.name);
                Undo.RecordObject(f,"Recover original perimeter lattice");f.sharedMesh=original;restored++;
            }
            // Broad multi-storey lattice frames read as a single enormous building skeleton.
            // They share the wall load path and do not repeat the outline of every platform.
            const float bottom=-1078.5f,top=-52f;
            for(int bay=0;bay<12;bay++)
            {
                float angle=15+bay*30;var center=Polar(535,0,angle);var radial=Radial(center);var tangent=Vector3.Cross(Vector3.up,radial);
                var corners=new[]{center-tangent*19-radial*13,center+tangent*19-radial*13,center-tangent*19+radial*13,center+tangent*19+radial*13};
                foreach(var p in corners)
                {
                    Beam(new Vector3(p.x,bottom,p.z),new Vector3(p.x,top,p.z),2.4f,2.4f,iron);
                    Box(new Vector3(p.x,-1079.25f,p.z),new Vector3(5.5f,1.5f,5.5f),Quaternion.identity,steel);
                }
                int cells=24;
                for(int j=0;j<cells;j++)
                {
                    float y=Mathf.Lerp(bottom,top,(float)j/cells),next=Mathf.Lerp(bottom,top,(float)(j+1)/cells);
                    foreach(var edge in new[]{new[]{0,1},new[]{1,3},new[]{3,2},new[]{2,0}})
                    {
                        var a=corners[edge[0]];var b=corners[edge[1]];a.y=y;b.y=y;
                        Beam(a,b,1.3f,1.3f,iron);
                        var c=a;var d=b;c.y=next;d.y=next;
                        Beam(a,d,.85f,.85f,steel);Beam(b,c,.85f,.85f,steel);
                        if(j==cells-1)Beam(c,d,1.3f,1.3f,iron);
                    }
                    if(j%2==0)
                    {
                        var p=Polar(548,y,angle);var wall=Polar(581.7f,y,angle);
                        Beam(p,wall,2,2,iron);Beam(p,wall+Vector3.up*15,1,1,steel);
                        Box(wall,new Vector3(7,7,1.4f),Quaternion.LookRotation(radial),steel);
                    }
                }
            }
            // Deep continuous belt trusses connect the shared frames behind the platforms.
            // Their diagonals restore visual overlap and avoid an empty, flat wall backdrop.
            var levelBySource=floors.Where(p=>!p.hidden&&(p.source.StartsWith("Tier")||p.source.StartsWith("DistantDistrict")))
                .GroupBy(p=>p.source).ToDictionary(g=>g.Key,g=>g.Average(p=>p.y)-7);
            var levels=levelBySource.Values.OrderByDescending(y=>y).ToArray();
            foreach(float y in levels)
            {
                for(int i=0;i<72;i++)Truss(Polar(531,y,i*5),Polar(531,y,(i+1)*5),24,1.6f,iron);
                for(int bay=0;bay<12;bay++)
                {
                    float angle=15+bay*30;var center=Polar(535,y,angle);var radial=Radial(center);var tangent=Vector3.Cross(Vector3.up,radial);
                    foreach(float depth in new[]{-13f,13f})Beam(center+radial*depth-tangent*19,center+radial*depth+tangent*19,2,2,iron);
                    Beam(center-radial*13,center+radial*13,2,2,iron);
                }
            }
            int transfers=0;
            foreach(var floor in floors.Where(p=>!p.hidden))
            {
                var center=floor.hull.Aggregate(Vector2.zero,(s,p)=>s+p)/floor.hull.Count;
                var p=new Vector3(center.x,floor.bounds.min.y-1,center.y);var radial=Radial(p);var tangent=Vector3.Cross(Vector3.up,radial);
                float angle=Mathf.Atan2(p.x,p.z-80)*Mathf.Rad2Deg;
                // A pair of diagonal transfer arms joins the existing underside to the rear belt.
                foreach(float side in new[]{-1f,1f})
                {
                    var end=p+tangent*side*4;
                    if(!Inside(floor.hull,XZ(end)))continue;
                    float beltTop=levelBySource.TryGetValue(floor.source,out float ownLevel)?ownLevel:levels.OrderBy(y=>Mathf.Abs(y-(p.y-7))).First();
                    var back=Polar(531,beltTop-24,angle+side*2);
                    Beam(back,end,1.25f,1.25f,steel);
                    Box(end+Vector3.up*.35f,new Vector3(3,.7f,3),Quaternion.identity,iron);transfers++;
                }
            }
            Flush();EditorUtility.SetDirty(motion);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(venue.gameObject.scene);EditorSceneManager.SaveScene(venue.gameObject.scene);
            Phase("density complete");
            return $"Restored {restored} original outer-shaft meshes; 12 shared full-depth frames, {levels.Length} deep belt trusses, {transfers} deck transfer arms. {boxes} members in {batches.Count} batches. Individual platform foundations remain removed.";
        }
        static Vector3 Polar(float radius,float y,float degrees){float a=degrees*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a)*radius,y,80+Mathf.Cos(a)*radius);}

        static void DeclutterFloors()
        {
            int buried=PruneBuriedFloorSkins();
            int modules=0,pieces=0;
            foreach(var floor in floors.OrderByDescending(p=>Priority(p.source)))
            {
                if(floor.hidden)continue;
                if(Priority(floor.source)<3)continue;
                foreach(var other in floors)
                {
                    if(other==floor||other.hidden||Priority(other.source)>=Priority(floor.source)||Mathf.Abs(other.y-floor.y)>10)continue;
                    if(Overlap(floor.hull,other.hull)<Mathf.Min(Area(floor.hull),Area(other.hull))*.25f)continue;
                    floor.hidden=true;modules++;break;
                }
            }
            foreach(var group in floors.Where(p=>p.hidden&&!p.buried).GroupBy(p=>p.source))
            {
                var peers=floors.Where(p=>p.source==group.Key).ToArray();
                foreach(var filter in venue.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith(group.Key+"_")).ToArray())
                {
                    if(filter.sharedMesh.vertexCount==0)continue;var source=Read(filter);
                    foreach(var part in source.parts)
                    {
                        var owner=peers.Where(p=>part.bounds.center.y>p.y-14&&part.bounds.center.y<p.y+36)
                            .OrderBy(p=>(Closest(p.hull,XZ(part.bounds.center))-XZ(part.bounds.center)).sqrMagnitude+Mathf.Abs(p.y-part.bounds.center.y)*.25f).FirstOrDefault();
                        if(owner!=null&&owner.hidden){Remove(source,part);pieces++;}
                    }
                }
            }
            report.Add($"Decluttered {modules} secondary platform modules with >25% footprint overlap and their {pieces} associated room/rail pieces; {buried} nearly coplanar buried floor skins removed.");
        }
        static int PruneBuriedFloorSkins()
        {
            int count=0;
            foreach(var small in floors)
            {
                if(small.hidden)continue;
                float area=Area(small.hull);
                bool buried=floors.Any(big=>big!=small&&!big.hidden&&big.source==small.source&&Area(big.hull)>area*4&&Mathf.Abs(big.y-small.y)<.6f&&Overlap(small.hull,big.hull)>area*.98f);
                if(!buried)continue;
                foreach(var part in small.members)Remove(small.mesh,part);
                small.hidden=small.buried=true;count++;
            }
            return count;
        }
        public static string RefineBuriedFloorSkins()
        {
            venue=GameObject.Find("PerformanceBackground/CircularVenue").transform;root=venue.Find("30_ConnectedStructure");motion=venue.GetComponentInChildren<PerformanceBackgroundMotion>();
            if(root==null)throw new InvalidOperationException("Apply the connection layout first.");
            PerformanceBackgroundMotionEditor.StopPreview();motion.RestoreRestPose();sources.Clear();floors.Clear();bridges.Clear();batches.Clear();report.Clear();meshNumber=1000;
            ReadDecks();int count=PruneBuriedFloorSkins();int saddles=MountExistingWallGirders();Flush();EditorSceneManager.MarkSceneDirty(venue.gameObject.scene);EditorSceneManager.SaveScene(venue.gameObject.scene);
            System.IO.File.AppendAllText("Temp/connection-refit-status.txt",$"\nFinal skin audit: {count} fully buried near-coplanar floor patches removed.");
            return $"Removed {count} fully buried near-coplanar floor patches; {saddles} wall-girder saddle anchors fitted. Room and railing geometry retained.";
        }
        static int MountExistingWallGirders()
        {
            if(root.Find("WallGirderSaddles")!=null)return 0;
            steel=venue.Find("25_Undercity/Tier0_Steel").GetComponent<MeshRenderer>().sharedMaterial;
            int count=0;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Connected_SootedIron").ToArray())
            foreach(var part in Read(filter).parts)
            {
                if(Mathf.Abs(Horizontal(part.bounds.center,Center)-576.305f)>.5f||Mathf.Abs(part.bounds.size.y-2)>.02f||part.length<50||part.length>60)continue;
                var p=part.bounds.center;var wall=Center+Radial(p)*581.7f;wall.y=p.y;
                Beam(p,wall,.8f,1.2f,steel);Box(wall,new Vector3(3,3,.8f),Quaternion.LookRotation(Radial(wall)),steel);count++;
            }
            NewObject("WallGirderSaddles",root);return count;
        }
        static int Priority(string source)=>source.StartsWith("Inhabited")?4:source.StartsWith("DeepCore")?3:source.StartsWith("Distant")?2:source.StartsWith("Audience")?1:0;
        static float Overlap(List<Vector2> a,List<Vector2> b)
        {
            var polygon=new List<Vector2>(a);
            for(int i=0;i<b.Count&&polygon.Count>0;i++)
            {
                var start=b[i];var edge=b[(i+1)%b.Count]-start;var next=new List<Vector2>();
                for(int j=0;j<polygon.Count;j++)
                {
                    var p=polygon[j];var q=polygon[(j+1)%polygon.Count];float dp=Cross(edge,p-start),dq=Cross(edge,q-start);
                    if(dp>=0)next.Add(p);if((dp>=0)!=(dq>=0))next.Add(p+(q-p)*(dp/(dp-dq)));
                }
                polygon=next;
            }
            return polygon.Count<3?0:Area(polygon);
        }

        static void ReadDecks()
        {
            foreach(var f in venue.GetComponentsInChildren<MeshFilter>())
            {
                if(!f.name.EndsWith("_Deck")||f.sharedMesh.vertexCount==0)continue;
                var s=Read(f);
                var floorParts=new List<Part>();
                foreach(var p in s.parts)
                {
                    if(p.bounds.size.y>1.1f||p.length<5||p.width<1.5f)continue;
                    var radial=p.bounds.center-Center;radial.y=0;
                    bool wallBridge=p.width>=4.7f&&p.width<=8f&&p.length>18&&radial.magnitude>350&&Mathf.Abs(Vector3.Dot(p.axis,radial.normalized))>.8f;
                    if(wallBridge)
                    {
                        var axis=Vector3.Dot(p.axis,radial)>0?p.axis:-p.axis;
                        var a=p.bounds.center-axis*p.length*.5f;var b=p.bounds.center+axis*p.length*.5f;
                        if((XZ(b)-XZ(Center)).magnitude>575) { bridges.Add(new Bridge{source=s,part=p,oldA=a,oldB=b});continue; }
                    }
                    floorParts.Add(p);
                }
                foreach(var level in floorParts.GroupBy(p=>Mathf.RoundToInt(p.bounds.center.y*10)))
                {
                    var parts=level.ToArray();var assigned=new bool[parts.Length];
                    for(int i=0;i<parts.Length;i++)
                    {
                        if(assigned[i])continue;
                        var cluster=new List<Part>{parts[i]};assigned[i]=true;
                        for(int j=0;j<cluster.Count;j++)for(int k=0;k<parts.Length;k++)
                        {
                            if(assigned[k])continue;
                            var expanded=cluster[j].bounds;expanded.Expand(new Vector3(.45f,.1f,.45f));
                            if(expanded.Intersects(parts[k].bounds)){assigned[k]=true;cluster.Add(parts[k]);}
                        }
                        var points=cluster.SelectMany(p=>p.ids.Select(id=>s.toWorld.MultiplyPoint3x4(s.vertices[id]))).ToArray();
                        var hull=Hull(points.Select(XZ));if(hull.Count<3)continue;
                        var bounds=cluster[0].bounds;foreach(var p in cluster)bounds.Encapsulate(p.bounds);
                        if(Area(hull)>60)floors.Add(new Floor{y=bounds.max.y,hull=hull,bounds=bounds,source=f.name.Substring(0,f.name.Length-5),mesh=s,members=cluster.ToArray()});
                    }
                }
            }
            report.Add($"Surveyed {floors.Count} connected floor islands and {bridges.Count} radial wall bridges.");
        }

        static void FitBridges()
        {
            float worst=0;
            foreach(var bridge in bridges)
            {
                if(bridge.part.ids.Any(bridge.source.removed.Contains))continue;
                var dir=(bridge.oldB-bridge.oldA).normalized;
                var start=XZ(bridge.oldA);
                var nearest=floors.Where(f=>!f.hidden&&Mathf.Abs(f.y-(bridge.oldA.y+.4f))<1.25f)
                    .Select(f=>new{floor=f,point=Closest(f.hull,start),distance=Inside(f.hull,start)?0:(Closest(f.hull,start)-start).sqrMagnitude})
                    .OrderBy(f=>f.distance).ThenBy(f=>Mathf.Abs(f.floor.y-(bridge.oldA.y+.4f))).ThenByDescending(f=>Area(f.floor.hull)).FirstOrDefault();
                if(nearest==null||(nearest.point-start).magnitude>30f)continue;
                var point=nearest.point;
                var crossing=LineBoundary(nearest.floor.hull,start,XZ(dir));
                if(crossing.HasValue)point=crossing.Value;
                var a=new Vector3(point.x,nearest.floor.y-.4f,point.y);
                var endRadial=bridge.oldB-Center;endRadial.y=0;
                var b=Center+endRadial.normalized*581.8f;b.y=bridge.oldB.y;
                worst=Mathf.Max(worst,Vector3.Distance(a,bridge.oldA));
                bridge.a=a;bridge.b=b;
                MapPart(bridge.source,bridge.part,bridge);
                string prefix=bridge.source.filter.name.Substring(0,bridge.source.filter.name.Length-5);
                var envelope=bridge.part.bounds;envelope.Expand(new Vector3(4,24,4));
                foreach(var name in new[]{prefix+"_Iron",prefix+"_Bronze",prefix+"_Black",prefix+"_Amber"})
                {
                    var sibling=bridge.source.filter.transform.parent.Find(name);
                    if(sibling==null||sibling.GetComponent<MeshFilter>()==null)continue;
                    var source=Read(sibling.GetComponent<MeshFilter>());
                    foreach(var p in source.parts)
                    {
                        if(p.bounds.size.y>16||!envelope.Contains(p.bounds.min)||!envelope.Contains(p.bounds.max))continue;
                        var center=p.bounds.center-bridge.part.bounds.center;
                        if(Mathf.Abs(Vector3.Dot(center,Vector3.Cross(Vector3.up,dir)))>bridge.part.width*.5f+1.2f)continue;
                        MapPart(source,p,bridge);
                    }
                }
                var rotation=Quaternion.LookRotation((b-a).normalized,Vector3.up);
                Box(a+Vector3.up*.35f,new Vector3(bridge.part.width+.4f,.25f,1.4f),rotation,steel);
                var radial=(b-Center);radial.y=0;radial.Normalize();
                Box(b+radial*.15f+Vector3.down*3,new Vector3(9,11,1),Quaternion.LookRotation(radial),iron);
                var under=b-radial*8+Vector3.down*.7f;
                Beam(under,b+Vector3.down*9,1.1f,1.1f,steel);
                Beam(under,b+Vector3.down*.7f,1.1f,1.1f,steel);
                bridgesAdjusted++;
            }
            report.Add($"Realigned {bridgesAdjusted} complete bridge/rail assemblies to floor boundaries and wall sockets; largest end correction {worst:F2}.");
        }

        static void MapPart(Source source,Part part,Bridge bridge)
        {
            if(!source.mapped.Add(part))return;
            var oldDir=(bridge.oldB-bridge.oldA).normalized;var newDir=(bridge.b-bridge.a).normalized;
            var rotation=Quaternion.FromToRotation(oldDir,newDir);
            float length=Vector3.Distance(bridge.oldA,bridge.oldB);
            foreach(int i in part.ids)
            {
                var world=source.toWorld.MultiplyPoint3x4(source.vertices[i]);
                float along=Vector3.Dot(world-bridge.oldA,oldDir);
                var offset=world-(bridge.oldA+oldDir*along);
                source.vertices[i]=source.toLocal.MultiplyPoint3x4(Vector3.LerpUnclamped(bridge.a,bridge.b,along/length)+rotation*offset);
                if(source.normals.Length==source.vertices.Length)source.normals[i]=source.toLocal.MultiplyVector(rotation*source.toWorld.MultiplyVector(source.normals[i])).normalized;
            }
            source.changed=true;
        }

        static void SupportSeating()
        {
            int contacts=0;
            for(int i=0;i<24;i++)
            {
                float angle=(7.5f+i*15f)*Mathf.Deg2Rad;var radial=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                var points=new List<Vector3>();
                foreach(float r in new[]{343f,377f,405f,439f,467f,501f})
                {
                    var p=Center+radial*r;p.y=-5;
                    float y=UpHit(seating,p);if(float.IsInfinity(y))continue;
                    p.y=y-1.2f;points.Add(p);
                    Box(p+Vector3.up*.95f,new Vector3(4,.5f,4),Quaternion.LookRotation(radial),steel);contacts++;
                }
                var wall=Center+radial*581.8f;wall.y=points[points.Count-1].y;points.Add(wall);
                for(int j=1;j<points.Count;j++)Truss(points[j-1],points[j],j%2==0?9:7,1.15f,iron);
                var rear=points[points.Count-2];
                Box((rear+wall)*.5f+Vector3.up*1.8f,new Vector3(5,.65f,Vector3.Distance(rear,wall)),Quaternion.LookRotation(wall-rear),steel);
                Box(wall-radial*.15f+Vector3.down*4,new Vector3(12,18,1.4f),Quaternion.LookRotation(radial),iron);
            }
            report.Add($"Grandstands: 24 connected radial underframes, {contacts} actual timber underside contacts, rear service landings and wall anchors.");
        }

        static void FitGallerySeats()
        {
            var gallery=Read(venue.Find("25_Undercity/AudienceGalleries_Deck").GetComponent<MeshFilter>());
            var pads=gallery.parts.Where(p=>p.bounds.size.y<1&&p.width>30&&p.length>30&&p.length<45).ToArray();
            int adjusted=0;
            for(int i=0;i<9;i++)
            {
                var audience=venue.Find("26_CircusAudience");var wood=audience.Find("Gallery"+i+"_Wood");if(wood==null)continue;
                var bounds=wood.GetComponent<Renderer>().bounds;
                var pad=pads.OrderBy(p=>Horizontal(p.bounds.center,bounds.center)).FirstOrDefault();if(pad==null)continue;
                var members=new[]{"Wood","Gold","Red","Dark"}.Select(s=>audience.Find("Gallery"+i+"_"+s)).Where(t=>t!=null).ToArray();
                var anchor=NewObject("GallerySeatFit_"+i,root).transform;anchor.position=pad.bounds.center;
                float radius=0;foreach(var t in members)foreach(var v in t.GetComponent<MeshFilter>().sharedMesh.vertices)radius=Mathf.Max(radius,Horizontal(t.TransformPoint(v),pad.bounds.center));
                float scale=Mathf.Min(1f,(pad.width*.5f-.7f)/radius);
                foreach(var t in members)Undo.SetTransformParent(t,anchor,"Seat gallery on its real deck");
                anchor.localScale=new Vector3(scale,1,scale);
                float lift=pad.bounds.max.y-wood.GetComponent<Renderer>().bounds.min.y+.02f;
                anchor.position+=Vector3.up*lift;adjusted++;
            }
            report.Add($"Refit {adjusted} gallery seating sets to their circular deck footprints and top surfaces.");
        }

        static void FitCranes()
        {
            int fixedParts=0;
            foreach(var crane in motion.cranes)
            {
                var pivot=crane.target;float cap=pivot.position.y-5.5f;
                foreach(var filter in pivot.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("HeroSawGantry_")).ToArray())
                {
                    var source=Read(filter);var keep=new List<int>();var detach=new List<int>();
                    foreach(var p in source.parts)
                    {
                        float radial=Horizontal(p.bounds.center,pivot.position);
                        bool fixedMember=(radial>26&&p.bounds.center.y<pivot.position.y-6)||radial+p.length*.5f>95;
                        var destination=fixedMember?detach:keep;
                        var set=new HashSet<int>(p.ids);
                        for(int t=0;t<source.triangles.Length;t+=3)if(set.Contains(source.triangles[t])){destination.Add(source.triangles[t]);destination.Add(source.triangles[t+1]);destination.Add(source.triangles[t+2]);}
                        if(fixedMember)fixedParts++;
                    }
                    if(detach.Count==0)continue;
                    var moving=PerformanceBackgroundMotionSetup.SaveSubset(source.mesh,keep,"Refit_"+pivot.name+"_"+filter.name+"_Moving");
                    var stationary=PerformanceBackgroundMotionSetup.SaveSubset(source.mesh,detach,"Refit_"+pivot.name+"_"+filter.name+"_Stationary");
                    Undo.RecordObject(filter,"Keep only free crane members rotating");filter.sharedMesh=moving;
                    var go=NewObject("Fixed_"+pivot.name+"_"+filter.name,filter.transform.parent);
                    go.transform.SetLocalPositionAndRotation(filter.transform.localPosition,filter.transform.localRotation);go.transform.localScale=filter.transform.localScale;
                    go.AddComponent<MeshFilter>().sharedMesh=stationary;go.AddComponent<MeshRenderer>().sharedMaterials=filter.GetComponent<MeshRenderer>().sharedMaterials;
                    go.transform.SetParent(root,true);
                }
                var frame=venue.Find("28_DynamicShow/CraneSupport_"+(Array.IndexOf(motion.cranes,crane)+1).ToString("00"));
                if(frame!=null){var bearing=frame.Find("SlewBearing");Undo.RecordObject(bearing,"Close slew bearing gap");bearing.localPosition=new Vector3(0,-2.2f,0);bearing.localScale=new Vector3(6,1.6f,6);}
                foreach(var filter in venue.Find("25_Undercity").GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="HeroSawGantry_Iron"))
                {
                    var source=Read(filter);
                    foreach(var part in source.parts.Where(p=>p.bounds.size.y>80&&Horizontal(p.bounds.center,pivot.position)<70))
                    {
                        if(part.bounds.max.y<=cap)continue;
                        float scale=(cap-part.bounds.min.y)/part.bounds.size.y;
                        foreach(int id in part.ids){var p=source.toWorld.MultiplyPoint3x4(source.vertices[id]);p.y=part.bounds.min.y+(p.y-part.bounds.min.y)*scale;source.vertices[id]=source.toLocal.MultiplyPoint3x4(p);}
                        source.changed=true;
                    }
                }
            }
            report.Add($"Moved {fixedParts} leg/anchor members out of rotating crane assemblies; adjusted fixed post heads and bearing interfaces.");
        }

        static void SupportSaws()
        {
            foreach(var track in motion.machinery)
            {
                var target=track.target;var normal=target.forward;var parent=target.parent;
                var reference=parent.name.StartsWith("Crane_")?parent.position:Center+Radial(target.position)*582f;
                if(Vector3.Dot(normal,reference-target.position)<0)normal=-normal;
                var bearing=target.position+normal*7f;
                Vector3 anchor;
                if(parent.name.StartsWith("Crane_"))anchor=parent.position+normal*10f+Vector3.down*2;
                else
                {
                    anchor=posts.Select(b=>new Vector3(b.center.x,Mathf.Clamp(bearing.y,b.min.y+2,b.max.y-2),b.center.z))
                        .Where(p=>Vector3.Dot(p-target.position,normal)>5.5f).OrderBy(p=>(p-bearing).sqrMagnitude).FirstOrDefault();
                    if(anchor==Vector3.zero||Vector3.Distance(anchor,bearing)>100){anchor=Center+Radial(target.position)*581.5f;anchor.y=bearing.y;}
                    parent=root;
                }
                Rod(target.position-normal*.5f,bearing,1.05f,brass,parent);
                Box(bearing,new Vector3(4.5f,4.5f,3),Quaternion.LookRotation(normal),steel,parent);
                Beam(anchor,bearing,1.4f,2f,iron,parent);
                var lower=anchor+Vector3.down*10;
                float backDistance=Vector3.Dot(lower-target.position,normal);
                if(backDistance<6)lower+=normal*(6-backDistance);
                Beam(lower,bearing,1f,1f,steel,parent);
                Box(anchor,new Vector3(4.5f,5,3),Quaternion.LookRotation(normal),iron,parent);
            }
            report.Add($"Added non-spinning axles, bearing blocks and rear brackets for {motion.machinery.Length} saw disks.");
        }

        static void MountLighting()
        {
            int moved=0,roofHeads=0;
            foreach(var track in motion.showLights)
            {
                var target=track.target;var light=target.GetComponent<Light>();
                if(light!=null)
                {
                    if(!light.name.StartsWith("ColdRoof"))continue;
                    var below=target.position;below.y=200;
                    float roofY=UpHit(ceiling,below);
                    if(!float.IsInfinity(roofY))
                    {
                        Undo.RecordObject(target,"Keep roof light below the ceiling skin");
                        var position=target.position;position.y=Mathf.Min(position.y,roofY-8);target.position=position;
                        var top=new Vector3(target.position.x,roofY-.3f,target.position.z);
                        Rod(top,target.position+Vector3.up*6,.65f,cable);
                        Box(top,new Vector3(8,.6f,8),Quaternion.identity,iron);
                        Beam(target.position+new Vector3(-5,6,0),target.position+new Vector3(5,6,0),1.1f,1.1f,steel);
                        Rod(target.position+Vector3.up*6,target.position+Vector3.up*1.5f,.8f,iron);
                    }
                    LampHead(target,target.forward,cool);roofHeads++;
                }
                else if(target.name.StartsWith("DustBeam"))
                {
                    var filter=target.GetComponentInChildren<MeshFilter>();
                    var oldDirection=(filter.GetComponent<Renderer>().bounds.center-target.position).normalized;
                    bool isCool=target.name.Contains("Cool");float radius=isCool?420f:356f;
                    var p=Center+Radial(target.position)*radius;p.y=-5;
                    float underside=UpHit(seating,p);
                    if(float.IsInfinity(underside))throw new InvalidOperationException("No seating underside for beam fixture "+target.name);
                    p.y=underside-5;
                    Undo.RecordObject(target,"Relocate beam to an actual suspended fixture");
                    target.position=p;
                    var aim=new Vector3(0,-160,80);
                    target.rotation=Quaternion.FromToRotation(oldDirection,(aim-p).normalized)*target.rotation;
                    var direction=(filter.GetComponent<Renderer>().bounds.center-p).normalized;
                    Rod(new Vector3(p.x,underside-.25f,p.z),p+Vector3.up*1.5f,.65f,iron);
                    Box(new Vector3(p.x,underside-.25f,p.z),new Vector3(6,.5f,6),Quaternion.identity,steel);
                    LampHead(target,direction,isCool?cool:warm);moved++;
                }
            }
            report.Add($"Mounted {roofHeads} roof spot heads and relocated {moved} floating beam sources onto seating undersides with real hangers/yokes.");
        }

        static void LampHead(Transform target,Vector3 direction,Material lens)
        {
            var p=target.position;var q=Quaternion.LookRotation(direction);
            Box(p-direction*2,new Vector3(4.8f,4.8f,5),q,iron,target);
            Box(p+direction*.65f,new Vector3(4.2f,4.2f,.35f),q,lens,target);
            var side=q*Vector3.right;
            Rod(p-side*4,p+side*4,.7f,brass);
            Beam(p-side*4,p-side*4+Vector3.up*4,.8f,.8f,steel);
            Beam(p+side*4,p+side*4+Vector3.up*4,.8f,.8f,steel);
            Beam(p-side*4+Vector3.up*4,p+side*4+Vector3.up*4,.8f,.8f,steel);
            Rod(p+Vector3.up*4,p,.7f,iron);
        }

        static void MountHanging()
        {
            int count=0;
            foreach(var track in motion.atmosphere)
            {
                var pivot=track.target;if(!pivot.name.StartsWith("BannerWind")&&!pivot.name.StartsWith("HangingVelvet"))continue;
                var filters=pivot.GetComponentsInChildren<MeshFilter>();
                var points=filters.SelectMany(f=>f.sharedMesh.vertices.Select(f.transform.TransformPoint)).ToArray();
                float top=points.Max(p=>p.y);
                var upper=points.Where(p=>p.y>top-.15f).ToArray();
                Vector3 center=Vector3.zero;foreach(var p in upper)center+=p;center/=upper.Length;
                Vector3 axis=Vector3.right;float span=0;
                for(int i=0;i<upper.Length;i++)for(int j=i+1;j<upper.Length;j++){var d=upper[j]-upper[i];d.y=0;if(d.sqrMagnitude>span){span=d.sqrMagnitude;axis=d.normalized;}}
                var children=new List<(Transform t,Vector3 p,Quaternion r)>();foreach(Transform child in pivot)children.Add((child,child.position,child.rotation));
                Undo.RecordObject(pivot,"Place banner hinge on its upper attachment edge");pivot.position=center;
                foreach(var child in children){Undo.RecordObject(child.t,"Preserve banner geometry");child.t.SetPositionAndRotation(child.p,child.r);}
                track.rotationDegrees=Vector3.zero;track.travel=Vector3.zero;track.hingeAxis=pivot.InverseTransformDirection(axis);track.hingeDegrees=pivot.name.StartsWith("BannerWind")?2.6f:3.2f;
                float half=Mathf.Sqrt(span)*.5f;
                foreach(float sign in new[]{-1f,1f})
                {
                    var pin=center+axis*(half*sign);
                    var wall=Center+Radial(pin)*581.7f;wall.y=pin.y+2.5f;
                    var end=pin+Vector3.up*2.5f;
                    Beam(wall,end,1f,1.3f,iron);
                    Beam(wall+Vector3.down*12,end,.75f,.75f,steel);
                    Box(wall+Vector3.down*3,new Vector3(5,10,.7f),Quaternion.LookRotation(Radial(pin)),iron);
                    Rod(end,pin,.45f,brass);
                    Rod(pin-axis*.65f,pin+axis*.65f,.6f,brass);
                }
                count++;
            }
            report.Add($"Rehung {count} banners/cloth panels on fixed upper-edge hinges and wall-mounted brackets; top edges remain fixed through their motion.");
        }

        static void Flush()
        {
            foreach(var s in sources.Values.Where(s=>s.changed))
            {
                var mesh=UnityEngine.Object.Instantiate(s.mesh);mesh.name=s.filter.name+"_Connected";
                if(s.removed.Count==0){mesh.vertices=s.vertices;if(s.normals.Length==s.vertices.Length)mesh.normals=s.normals;}
                else
                {
                    var old=new List<int>();for(int i=0;i<s.triangles.Length;i+=3)if(!s.removed.Contains(s.triangles[i])){old.Add(s.triangles[i]);old.Add(s.triangles[i+1]);old.Add(s.triangles[i+2]);}
                    var used=old.Distinct().ToArray();var lookup=new Dictionary<int,int>();for(int i=0;i<used.Length;i++)lookup[used[i]]=i;
                    var uv=s.mesh.uv;var colors=s.mesh.colors;
                    mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=used.Select(i=>s.vertices[i]).ToArray();
                    if(s.normals.Length==s.vertices.Length)mesh.normals=used.Select(i=>s.normals[i]).ToArray();
                    if(uv.Length==s.vertices.Length)mesh.uv=used.Select(i=>uv[i]).ToArray();if(colors.Length==s.vertices.Length)mesh.colors=used.Select(i=>colors[i]).ToArray();mesh.triangles=old.Select(i=>lookup[i]).ToArray();
                }
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,Folder+"/Refit_"+(meshNumber++).ToString("000")+"_"+s.filter.name+".asset");
                Undo.RecordObject(s.filter,"Use aligned connection geometry");s.filter.sharedMesh=mesh;
            }
            var newSurfaces=new List<PerformanceBackgroundMotion.SurfaceMotion>();
            foreach(var b in batches)
            {
                if(b.vertices.Count==0)continue;
                var mesh=new Mesh{name="ConnectedStructure",indexFormat=IndexFormat.UInt32};var local=b.parent.worldToLocalMatrix;
                mesh.vertices=b.vertices.Select(local.MultiplyPoint3x4).ToArray();mesh.normals=b.normals.Select(n=>local.MultiplyVector(n).normalized).ToArray();mesh.triangles=b.triangles.ToArray();mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,Folder+"/Joint_"+(meshNumber++).ToString("000")+".asset");
                var go=NewObject("Connected_"+b.material.name,b.parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=b.material;
                if(b.material==warm||b.material==cool)newSurfaces.Add(new PerformanceBackgroundMotion.SurfaceMotion{target=renderer,amount=.12f,periodSeconds=12f,phase=newSurfaces.Count*.13f});
            }
            motion.surfaces=motion.surfaces.Concat(newSurfaces).ToArray();
        }

        static Batch Buffer(Material material,Transform parent)
        {
            parent=parent==null?root:parent;
            var buffer=batches.FirstOrDefault(b=>b.material==material&&b.parent==parent);
            if(buffer!=null)return buffer;
            buffer=new Batch{material=material,parent=parent};batches.Add(buffer);return buffer;
        }

        static void Box(Vector3 center,Vector3 size,Quaternion rotation,Material material,Transform parent=null)
        {
            var b=Buffer(material,parent);var h=size*.5f;
            var v=new[]{new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z)};
            for(int i=0;i<v.Length;i++)v[i]=center+rotation*v[i];
            Quad(b,v[4],v[5],v[6],v[7]);Quad(b,v[1],v[0],v[3],v[2]);Quad(b,v[7],v[6],v[2],v[3]);Quad(b,v[0],v[1],v[5],v[4]);Quad(b,v[5],v[1],v[2],v[6]);Quad(b,v[0],v[4],v[7],v[3]);boxes++;
        }

        static void Beam(Vector3 a,Vector3 b,float width,float depth,Material material,Transform parent=null)
        {
            var direction=b-a;if(direction.sqrMagnitude<.0001f)return;
            var up=Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.up))>.98f?Vector3.forward:Vector3.up;
            Box((a+b)*.5f,new Vector3(width,depth,direction.magnitude),Quaternion.LookRotation(direction,up),material,parent);
        }

        static void Truss(Vector3 a,Vector3 b,float depth,float width,Material material)
        {
            Beam(a,b,width,width*1.3f,material);Beam(a-Vector3.up*depth,b-Vector3.up*depth,width,width,material);
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/16));
            for(int i=0;i<steps;i++){var p=Vector3.Lerp(a,b,(float)i/steps);var q=Vector3.Lerp(a,b,(float)(i+1)/steps);Beam(p,q-Vector3.up*depth,width*.6f,width*.6f,steel);Beam(p,p-Vector3.up*depth,width*.6f,width*.6f,steel);}
        }

        static void Rod(Vector3 a,Vector3 b,float radius,Material material,Transform parent=null)
        {
            var direction=b-a;if(direction.sqrMagnitude<.0001f)return;
            var axis=direction.normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.98f?Vector3.right:Vector3.up).normalized*radius;var v=Vector3.Cross(axis,u).normalized*radius;
            var batch=Buffer(material,parent);
            for(int i=0;i<8;i++)
            {
                float t=i*Mathf.PI/4,s=(i+1)*Mathf.PI/4;var p=u*Mathf.Cos(t)+v*Mathf.Sin(t);var q=u*Mathf.Cos(s)+v*Mathf.Sin(s);
                Quad(batch,a+p,a+q,b+q,b+p);Tri(batch,a,a+q,a+p);Tri(batch,b,b+p,b+q);
            }
            rods++;
        }

        static void Quad(Batch b,Vector3 a,Vector3 c,Vector3 d,Vector3 e) {Tri(b,a,c,d);Tri(b,a,d,e);}
        static void Tri(Batch b,Vector3 a,Vector3 c,Vector3 d)
        {
            int index=b.vertices.Count;var n=Vector3.Cross(c-a,d-a).normalized;b.vertices.Add(a);b.vertices.Add(c);b.vertices.Add(d);b.normals.Add(n);b.normals.Add(n);b.normals.Add(n);b.triangles.Add(index);b.triangles.Add(index+1);b.triangles.Add(index+2);
        }

        static GameObject NewObject(string name,Transform parent){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Reconnect scenery");go.transform.SetParent(parent,false);return go;}
        static Vector2 XZ(Vector3 p)=>new(p.x,p.z);
        static Vector3 Radial(Vector3 p){p-=Center;p.y=0;return p.normalized;}
        static float Horizontal(Vector3 a,Vector3 b)=>Vector2.Distance(XZ(a),XZ(b));
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static float Area(List<Vector2> polygon){float area=0;for(int i=0;i<polygon.Count;i++)area+=Cross(polygon[i],polygon[(i+1)%polygon.Count]);return Mathf.Abs(area)*.5f;}
        static bool Inside(List<Vector2> polygon,Vector2 point){for(int i=0;i<polygon.Count;i++)if(Cross(polygon[(i+1)%polygon.Count]-polygon[i],point-polygon[i])<-.01f)return false;return true;}
        static List<Vector2> Hull(IEnumerable<Vector2> input)
        {
            var points=input.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();var hull=new List<Vector2>();
            foreach(var p in points){while(hull.Count>=2&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=.0001f)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            int lower=hull.Count;for(int i=points.Length-2;i>=0;i--){var p=points[i];while(hull.Count>lower&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=.0001f)hull.RemoveAt(hull.Count-1);hull.Add(p);}if(hull.Count>1)hull.RemoveAt(hull.Count-1);return hull;
        }
        static Vector2 Closest(List<Vector2> polygon,Vector2 point)
        {
            var best=polygon[0];float distance=float.MaxValue;
            for(int i=0;i<polygon.Count;i++){var a=polygon[i];var edge=polygon[(i+1)%polygon.Count]-a;var p=a+edge*Mathf.Clamp01(Vector2.Dot(point-a,edge)/Mathf.Max(.00001f,edge.sqrMagnitude));float d=(p-point).sqrMagnitude;if(d<distance){distance=d;best=p;}}
            return best;
        }
        static Vector2? LineBoundary(List<Vector2> polygon,Vector2 origin,Vector2 direction)
        {
            float closest=float.MaxValue;Vector2? best=null;
            for(int i=0;i<polygon.Count;i++){var a=polygon[i];var edge=polygon[(i+1)%polygon.Count]-a;float d=Cross(direction,edge);if(Mathf.Abs(d)<.00001f)continue;float t=Cross(a-origin,edge)/d,u=Cross(a-origin,direction)/d;if(u<0||u>1||Mathf.Abs(t)>30)continue;if(Mathf.Abs(t)<closest){closest=Mathf.Abs(t);best=origin+direction*t;}}
            return best;
        }
    }
}
