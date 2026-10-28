using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        sealed class SupportSurface
        {
            public MeshFilter mesh;
            public Bounds bounds;
            public string path;
        }
        static readonly List<object> SupportEvidence=new();
        static SupportSurface[] supportSurfaces;
        static readonly System.Reflection.MethodInfo RayMesh=typeof(HandleUtility).GetMethod("IntersectRayMesh",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);

        public static string RepairVisibleSeamsAndSupports()
        {
            var (scene,target)=Begin();var original=Snapshot(source);int undo=StartUndo("Seal seams and attach hanging decorations");
            try
            {
                ApplyVisualSupportRepairs(target);Physics.SyncTransforms();AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
                return "Visible seam sealed against actual vault edges; hanging cloth and fixtures anchored; evidence="+SupportEvidence.Count;
            }
            catch{Undo.RevertAllDownToGroup(undo);throw;}
        }

        static void ApplyVisualSupportRepairs(Transform target)
        {
            Remove(target,"SupportFinishing");var g=Group(target,"SupportFinishing",Origin);SupportEvidence.Clear();
            string Path(Transform t)=>AnimationUtility.CalculateTransformPath(t,target);
            RehangDormClothes(target);
            supportSurfaces=target.GetComponentsInChildren<MeshFilter>().Where(m=>m.sharedMesh!=null&&m.GetComponent<Renderer>()!=null&&m.GetComponent<Renderer>().enabled)
                .Where(m=>{var p=Path(m.transform).ToLowerInvariant();return p.Contains("/shell/")||p.Contains("/uppershell/")||p.Contains("/lowershell/")||p.Contains("/vault")||p.Contains("/w_06_")||(p.Contains("/mezzanine/")&&m.name!="Rope")||new[]{"Beam","Column","Roof","Ceiling","Girder","BannerRod","DrapeRod","ScreenRod","ScreenPost","RigTruss","RailTop","Stands"}.Any(n=>m.name.Contains(n));})
                .Select(m=>new SupportSurface{mesh=m,bounds=m.GetComponent<Renderer>().bounds,path=Path(m.transform)}).ToArray();
            SealActualVaultJoint(target,g);
            foreach(var cloth in target.GetComponentsInChildren<Cloth>())
            {
                var skin=cloth.GetComponent<SkinnedMeshRenderer>();if(skin==null||skin.sharedMesh==null)continue;
                var vertices=skin.sharedMesh.vertices;var coefficients=cloth.coefficients;
                var pins=Enumerable.Range(0,Mathf.Min(vertices.Length,coefficients.Length)).Where(i=>coefficients[i].maxDistance<.001f).Select(i=>skin.transform.TransformPoint(vertices[i])).ToArray();
                if(pins.Length<2)continue;
                var ends=FarthestPair(pins);SupportBar(g,ends.Item1,ends.Item2,"ClothHeader");
                AttachToStructure(g,ends.Item1,Path(cloth.transform));AttachToStructure(g,ends.Item2,Path(cloth.transform));
            }
            foreach(var t in target.GetComponentsInChildren<Transform>().Where(t=>new[]{"Banner","FadedDrape","FadedCurtain","RailPennant","SM_MVV_HangingCloth05_c1_LOD0"}.Contains(t.name)).ToArray())
            {
                var points=VisibleVertices(t);if(points.Length==0)continue;float top=points.Max(p=>p.y);
                var ends=FarthestPair(points.Where(p=>p.y>top-.035f).ToArray());
                SupportBar(g,ends.Item1,ends.Item2,"FabricTopBinding");AttachToStructure(g,ends.Item1,Path(t));AttachToStructure(g,ends.Item2,Path(t));
            }
            SupportCanopy(target,g);
            RepairFlagStrings(target,g);
            foreach(var t in target.GetComponentsInChildren<Transform>().Where(t=>t.name=="BannerRod"||t.name=="DrapeRod").ToArray())
            {
                var b=t.GetComponent<Renderer>().bounds;var along=b.size.x>b.size.z?Vector3.right:Vector3.forward;
                float half=Mathf.Max(b.size.x,b.size.z)*.45f;
                AttachToStructure(g,b.center+along*half,Path(t),t);AttachToStructure(g,b.center-along*half,Path(t),t);
            }
            // Complete the high ends of existing suspension cables; deliberately leave torn lower ends alone.
            foreach(var t in target.GetComponentsInChildren<Transform>().Where(t=>t.name=="Rope"&&!t.IsChildOf(g)).ToArray())
            {
                var a=t.TransformPoint(Vector3.up);var b=t.TransformPoint(Vector3.down);
                if(Mathf.Abs((a-b).normalized.y)<.4f)continue;var high=a.y>b.y?a:b;
                if(high.y>4)AttachToStructure(g,high,Path(t),t,4.5f);
            }
            SupportBenchBoards(target,g);
            SeatStoredProps(target);
            // The moved clothesline keeps its source ceiling drops; finish them at the dorm's actual ceiling.
            var line=target.Find("S02_Dormitory/Dormitory/Living/PersonalClothesline");
            if(line!=null)foreach(var fixing in line.Cast<Transform>().Where(t=>t.name.StartsWith("CeilingFixing")))
            {
                var b=fixing.GetComponent<Renderer>().bounds;AttachToStructure(g,new Vector3(b.center.x,b.max.y,b.center.z),Path(fixing),line,6);
            }
            System.IO.Directory.CreateDirectory(".harness/local/task075-supports");
            System.IO.File.WriteAllText(".harness/local/task075-supports/support-repairs.json",Newtonsoft.Json.JsonConvert.SerializeObject(SupportEvidence,Newtonsoft.Json.Formatting.Indented));
        }

        static Vector3[] VisibleVertices(Transform t)
        {
            var mf=t.GetComponentsInChildren<MeshFilter>().Where(m=>m.sharedMesh!=null).OrderByDescending(m=>m.sharedMesh.vertexCount).FirstOrDefault();
            if(mf==null)return Array.Empty<Vector3>();var mesh=mf.sharedMesh;var vs=mesh.vertices;
            return mesh.triangles.Distinct().Select(i=>mf.transform.TransformPoint(vs[i])).ToArray();
        }
        static (Vector3,Vector3) FarthestPair(Vector3[] points)
        {
            var a=points[0];var b=a;float best=0;
            foreach(var p in points)foreach(var q in points)if((p-q).sqrMagnitude>best){best=(p-q).sqrMagnitude;a=p;b=q;}
            return (a,b);
        }
        static bool SurfaceHit(Vector3 point,Vector3 direction,float limit,Transform excluded,out Vector3 hit,out string path)
        {
            hit=point;path="";float nearest=limit;var ray=new Ray(point+direction*.008f,direction);
            foreach(var item in supportSurfaces)
            {
                if(excluded!=null&&(item.mesh.transform==excluded||item.mesh.transform.IsChildOf(excluded)))continue;
                if(!item.bounds.IntersectRay(ray,out float entry)||entry>nearest)continue;
                var args=new object[]{ray,item.mesh.sharedMesh,item.mesh.transform.localToWorldMatrix,null};
                if((bool)RayMesh.Invoke(null,args)){var h=(RaycastHit)args[3];if(h.distance>.002f&&h.distance<nearest){nearest=h.distance;hit=h.point;path=item.path;}}
            }
            return path.Length>0;
        }
        static void AttachToStructure(Transform g,Vector3 from,string owner,Transform excluded=null,float limit=24)
        {
            Vector3 best=from;string path="";float distance=limit;
            foreach(var direction in new[]{Vector3.up,Vector3.left,Vector3.right,Vector3.forward,Vector3.back})
                if(SurfaceHit(from,direction,distance,excluded,out var at,out var candidate)&&Vector3.Distance(from,at)<distance){distance=Vector3.Distance(from,at);best=at;path=candidate;}
            if(path.Length==0){SupportEvidence.Add(new{kind="unresolved",owner,at=from.ToString()});return;}
            if(distance>.018f)SupportLine(g,from,best,"SuspensionTie",.012f,Mat("V4_Black"));
            Prim(g,PrimitiveType.Sphere,"AnchorEye",best,Vector3.one*.065f,Mat("V4_RustyMetal"));
            SupportEvidence.Add(new{kind="anchor",owner,from=from.ToString(),to=best.ToString(),distance,surface=path});
        }
        static void SupportLine(Transform g,Vector3 a,Vector3 b,string name,float radius,Material material)
        {
            if(Vector3.Distance(a,b)<.002f)return;
            var t=Prim(g,PrimitiveType.Cylinder,name,(a+b)*.5f,new Vector3(radius*2,Vector3.Distance(a,b)*.5f,radius*2),material);
            t.rotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
        }
        static void SupportBar(Transform g,Vector3 a,Vector3 b,string name)=>SupportLine(g,a,b,name,.022f,Mat("V4_RustyMetal"));

        static void SupportCanopy(Transform target,Transform g)
        {
            var canopy=target.Find("S09_PracticeRoom/Arena/CanopyAndRig/TornCanopy");if(canopy==null)return;
            var points=VisibleVertices(canopy);var center=W(PracRing.x,PracRing.y);
            for(int i=0;i<20;i++)
            {
                float angle=i*18f;var candidates=points.Where(p=>Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(p.z-center.z,p.x-center.x)*Mathf.Rad2Deg,angle))<.2f).ToArray();
                if(candidates.Length==0)continue;var at=candidates.OrderByDescending(p=>new Vector2(p.x-center.x,p.z-center.z).sqrMagnitude).First();
                AttachToStructure(g,at,"PracticeCanopyEdge");
            }
            var crown=target.Find("S09_PracticeRoom/Arena/CanopyAndRig/CanopyCrown").GetComponent<Renderer>().bounds;
            AttachToStructure(g,new Vector3(crown.center.x,crown.max.y,crown.center.z),"PracticeCanopyCrown");
        }
        static Vector3 HighestVertex(Transform t)
        {
            var points=VisibleVertices(t);return points.OrderByDescending(p=>p.y).First();
        }
        static void RepairFlagStrings(Transform target,Transform g)
        {
            var surroundings=target.Find("S09_PracticeRoom/Arena/Surroundings");
            if(surroundings!=null)
            foreach(var row in surroundings.Cast<Transform>().Where(t=>t.name=="Pennant").GroupBy(t=>Mathf.RoundToInt(t.position.z*10)))
            {
                var tops=row.Select(HighestVertex).OrderBy(p=>p.x).ToArray();
                foreach(var old in surroundings.Cast<Transform>().Where(t=>t.name=="Rope"&&Mathf.Abs(t.position.z-tops[0].z)<.1f&&t.GetComponent<Renderer>().bounds.size.x>20).ToArray())MuteFacility(old);
                for(int i=0;i<tops.Length-1;i++)SupportLine(g,tops[i],tops[i+1],"PennantCatenary",.012f,Mat("V4_Black"));
                AttachToStructure(g,tops[0],"PracticePennants");AttachToStructure(g,tops[tops.Length-1],"PracticePennants");
                SupportEvidence.Add(new{kind="flag-row",flags=tops.Length});
            }
            var dressing=target.Find("FacilityConnections/StageProcessionalRoute/GreyboxComposition/Tunnel/WallDressing");if(dressing==null)return;
            var cables=dressing.Cast<Transform>().Where(t=>t.name=="Cable").ToArray();
            foreach(var flag in dressing.Cast<Transform>().Where(t=>t.name=="Flag"))
            {
                var top=HighestVertex(flag);Vector3 nearest=top;float best=float.PositiveInfinity;
                foreach(var cable in cables)
                {
                    var a=cable.TransformPoint(Vector3.up);var b=cable.TransformPoint(Vector3.down);var ab=b-a;
                    var q=a+ab*Mathf.Clamp01(Vector3.Dot(top-a,ab)/ab.sqrMagnitude);if(Vector3.Distance(top,q)<best){best=Vector3.Distance(top,q);nearest=q;}
                }
                if(best<1.2f){SupportLine(g,top,nearest,"FlagBinding",.012f,Mat("V4_Black"));SupportEvidence.Add(new{kind="flag-binding",gap=best});}
                else AttachToStructure(g,top,"StageFlag");
            }
        }

        static Vector3[] EndProfile(MeshFilter mf,float x)
        {
            return mf.sharedMesh.vertices.Select(mf.transform.TransformPoint).Where(p=>Mathf.Abs(p.x-x)<.01f).GroupBy(p=>Mathf.RoundToInt(p.z*10000)).Select(v=>v.OrderByDescending(p=>p.y).First()).OrderBy(p=>p.z).ToArray();
        }
        static float ProfileHeight(Vector3[] points,float z)
        {
            for(int i=0;i<points.Length-1;i++)if(z>=points[i].z-.001f&&z<=points[i+1].z+.001f)return Mathf.Lerp(points[i].y,points[i+1].y,(z-points[i].z)/(points[i+1].z-points[i].z));
            return 0;
        }
        static void SealActualVaultJoint(Transform target,Transform g)
        {
            var stage=target.Find("FacilityConnections/StageProcessionalRoute");if(stage==null)return;
            var inner=stage.Find("OriginalVaultApproach/Vault").GetComponent<MeshFilter>();var outer=stage.Find("GreyboxComposition/Tunnel/Vault").GetComponent<MeshFilter>();
            var i=EndProfile(inner,ShowStart);var o=EndProfile(outer,ShowStart);var cuts=i.Concat(o).Select(p=>p.z).Distinct().OrderBy(z=>z).ToArray();var mesh=new MeshBuilder("LA_SealedSourceTransition",1);
            for(int k=0;k<cuts.Length-1;k++)
            {
                float a=cuts[k],b=cuts[k+1],mid=(a+b)*.5f;bool opening=mid>i[0].z&&mid<i[i.Length-1].z;
                float la=opening?ProfileHeight(i,a)-.025f:0,lb=opening?ProfileHeight(i,b)-.025f:0,ha=ProfileHeight(o,a)+.025f,hb=ProfileHeight(o,b)+.025f;
                foreach(var x in new[]{ShowStart-.02f,ShowStart+.02f})mesh.Quad(0,new Vector3(x,la,a),new Vector3(x,ha,a),new Vector3(x,hb,b),new Vector3(x,lb,b),x<ShowStart?Vector3.left:Vector3.right,3);
                mesh.Quad(0,new Vector3(ShowStart-.02f,la,a),new Vector3(ShowStart+.02f,la,a),new Vector3(ShowStart+.02f,lb,b),new Vector3(ShowStart-.02f,lb,b),Vector3.down,3);
            }
            var cover=MeshObject(g,"SealedActualVaultTransition",Vector3.zero,SaveMesh(mesh.Build()),outer.GetComponent<Renderer>().sharedMaterial);cover.gameObject.AddComponent<MeshCollider>().sharedMesh=cover.GetComponent<MeshFilter>().sharedMesh;
            MuteFacility(stage.Find("ClosedSourceSectionTransition"));SupportEvidence.Add(new{kind="sealed-vault-joint",innerVertices=i.Length,outerVertices=o.Length});
        }
        // Beds and their hanging clothes moved with the deeper dorm, but the source clothesline stayed at the old depth.
        // Move the line under the clothes again (source offset: line 0.02m in front of the clothes).
        static void RehangDormClothes(Transform target)
        {
            var line=target.Find("S02_Dormitory/Dormitory/Living/PersonalClothesline");var clothes=target.Find("S02_Dormitory/Dormitory/Beds/PersonalClothes_0");
            if(line==null||clothes==null)return;
            var rope=line.Cast<Transform>().Where(t=>t.name.StartsWith("Rope_")).Select(t=>t.GetComponent<Renderer>().bounds.center.z).Average();
            float dz=clothes.position.z-.02f-rope;if(Mathf.Abs(dz)<.01f)return;
            MoveFacility(line,line.position+Vector3.forward*dz);SupportEvidence.Add(new{kind="rehung-clothesline",dz});
        }

        static void SeatStoredProps(Transform target)
        {
            var rack=target.Find("S11_StageGear/Zone11/CurtainRack");
            if(rack!=null)foreach(var t in rack.Cast<Transform>().Where(t=>t.name=="RolledCurtain"))
            {
                var b=t.GetComponent<Renderer>().bounds;float shelf=b.center.y<2?1.15f:2.65f;float gap=b.min.y-shelf;
                if(gap>.012f&&gap<.12f){MoveFacility(t,t.position-Vector3.up*(gap+.002f));SupportEvidence.Add(new{kind="seated-roll",gap});}
            }
            var dressing=target.Find("FacilityConnections/StageProcessionalRoute/GreyboxComposition/Tunnel/WallDressing");if(dressing==null)return;
            foreach(var t in dressing.Cast<Transform>().Where(t=>t.name=="트렁크"))
            {
                var b=Footprint(t);if(b.min.y<.3f)continue;
                var hits=Physics.RaycastAll(new Vector3(b.center.x,b.min.y+.02f,b.center.z),Vector3.down,1,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider.transform.IsChildOf(target)&&!h.collider.transform.IsChildOf(t)).OrderBy(h=>h.distance).ToArray();
                if(hits.Length>0&&hits[0].distance>.07f&&hits[0].distance<.4f){float drop=hits[0].distance-.022f;MoveFacility(t,t.position-Vector3.up*drop);SupportEvidence.Add(new{kind="seated-trunk",drop});}
            }
        }
        static void SupportBenchBoards(Transform target,Transform g)
        {
            var surroundings=target.Find("S09_PracticeRoom/Arena/Surroundings");if(surroundings==null)return;
            foreach(var board in surroundings.Cast<Transform>().Where(t=>t.name=="BenchBoard"))
            foreach(var side in new[]{-.4f,.4f})
            {
                var top=board.TransformPoint(new Vector3(side,-.5f,0));
                if(!SurfaceHit(top,Vector3.down,.5f,board,out var bottom,out var path))continue;
                float height=top.y-bottom.y;if(height<.025f)continue;
                var leg=Box(g,"BenchBearer",(top+bottom)*.5f,new Vector3(.13f,height+.008f,.25f),Mat("V4_Wood_Old"));leg.rotation=board.rotation;
                SupportEvidence.Add(new{kind="bench-bearer",ownerId=board.GetInstanceID(),from=bottom.ToString(),to=top.ToString(),height,surface=path});
            }
        }
    }
}
