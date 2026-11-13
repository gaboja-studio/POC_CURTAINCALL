using System.Collections;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.Wayfinding
{
    /// <summary>Dedicated query agent, separate from pursuit navigation. Shared by local and host-owned guides.</summary>
    public sealed class NavigationPathPlanner : MonoBehaviour
    {
        static readonly Bounds WaitingRoomBounds=new(new Vector3(-10,8,80),new Vector3(160,60,160));
        Bounds requested, built;
        bool requestedOnce, ready, building, agentCreated;
        int generation, agentType=-1;
        NavMeshData data, pendingData;
        NavMeshDataInstance instance;
        NavMeshQueryFilter filter;
        NavMeshPath path;
        void Awake(){path=new NavMeshPath();SceneManager.sceneLoaded+=Loaded;SceneManager.sceneUnloaded+=Unloaded;}
        void Loaded(Scene scene,LoadSceneMode mode)=>Invalidate();
        void Unloaded(Scene scene)=>Invalidate();
        public void Invalidate(){ready=false;generation++;}

        public bool TryCalculate(Vector3 start,Vector3 goal,List<Vector3> output)
        {
            if(!requestedOnce){requested=WaitingRoomBounds;requestedOnce=true;}
            var area=new Bounds(start,Vector3.one*12);area.Encapsulate(new Bounds(goal,Vector3.one*12));
            requested.Encapsulate(area);
            if(!ready || !built.Contains(area.min)||!built.Contains(area.max))
            { if(!building)StartCoroutine(Build()); return false; }
            if(!NavMesh.SamplePosition(start,out var a,2,filter)||!NavMesh.SamplePosition(goal,out var b,3,filter)
                ||!NavMesh.CalculatePath(a.position,b.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
            var corners=path.corners;
            if(corners.Length<2)return false;
            output.Clear();output.Add(a.position);
            for(int i=1;i<corners.Length;i++)output.Add(corners[i]);
            return true;
        }

        IEnumerator Build()
        {
            building=true;int version=generation;
            // Preserve the rehearsal grace for opening gates before collecting colliders.
            yield return new WaitForSecondsRealtime(3);
            var bounds=requested;
            var sources=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            sources.RemoveAll(s=>s.component is Collider c && (c.isTrigger||!c.enabled||!c.gameObject.activeInHierarchy||c.GetComponentInParent<NetworkPlayer>()!=null));
            if(!agentCreated)
            {
                var created=NavMesh.CreateSettings();agentType=created.agentTypeID;agentCreated=true;
                filter=new NavMeshQueryFilter{agentTypeID=agentType,areaMask=NavMesh.AllAreas};
            }
            var settings=NavMesh.GetSettingsByID(agentType);
            settings.agentRadius=.35f;settings.agentHeight=1.8f;settings.agentClimb=.4f;settings.agentSlope=45;
            settings.overrideVoxelSize=true;settings.voxelSize=.15f;settings.overrideTileSize=true;settings.tileSize=128;
            var next=new NavMeshData(agentType){hideFlags=HideFlags.HideAndDontSave}; pendingData=next;
            var build=NavMeshBuilder.UpdateNavMeshDataAsync(next,settings,sources,bounds);
            yield return build;
            if(instance.valid)NavMesh.RemoveNavMeshData(instance);
            if(data!=null)Destroy(data);
            data=next;pendingData=null;instance=NavMesh.AddNavMeshData(data);built=bounds;
            ready=version==generation;building=false;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded-=Loaded;SceneManager.sceneUnloaded-=Unloaded;
            if(instance.valid)NavMesh.RemoveNavMeshData(instance);
            if(data!=null)Destroy(data);
            if(pendingData!=null){NavMeshBuilder.Cancel(pendingData);Destroy(pendingData);}
            if(agentCreated)NavMesh.RemoveSettings(agentType);
        }
    }
}
