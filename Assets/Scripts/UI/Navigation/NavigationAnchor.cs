using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.UI.Wayfinding
{
    /// <summary>Wire identity for a visual endpoint. Dynamic objects use NetworkObject IDs; scene objects use indexed hierarchy paths.</summary>
    [Serializable]
    public sealed class NavigationAnchor
    {
        public bool outlined, networked;
        public ulong networkId;
        public string scene;
        public int[] hierarchy;
        public string[] names;
        [NonSerialized] Transform local;
        [NonSerialized] bool localOnly;
        internal static NavigationAnchor From(NavigationTarget target,bool shared)
        {
            var value=new NavigationAnchor{outlined=target.UsesObjectOutline};
            if(!value.outlined)return value;
            var transform=target.OutlineTarget;
            if(!shared){value.local=transform;value.localOnly=true;return value;}
            if(transform==null)return value;
            var network=transform.GetComponentInParent<NetworkObject>();
            var indices=new List<int>();
            var names=new List<string>();
            if(network!=null&&network.IsSpawned)
            {
                value.networked=true;value.networkId=network.NetworkObjectId;
                for(var t=transform;t!=network.transform;t=t.parent){indices.Add(t.GetSiblingIndex());names.Add(t.name);}
            }
            else
            {
                value.scene=transform.gameObject.scene.path;
                if(string.IsNullOrEmpty(value.scene))return value;
                for(var t=transform;t!=null;t=t.parent){indices.Add(t.GetSiblingIndex());names.Add(t.name);}
            }
            indices.Reverse();names.Reverse();value.hierarchy=indices.ToArray();value.names=names.ToArray();return value;
        }
        internal Transform Resolve(NetworkManager manager)
        {
            if(!outlined)return null;
            if(localOnly)return local!=null&&local.gameObject.activeInHierarchy?local:null;
            if(hierarchy==null||!Valid)return null;
            Transform target;int start;
            if(networked)
            {
                if(manager==null||manager.SpawnManager==null||!manager.SpawnManager.SpawnedObjects.TryGetValue(networkId,out var network))return null;
                target=network.transform;start=0;
            }
            else
            {
                if(string.IsNullOrEmpty(scene)||hierarchy.Length==0)return null;
                var loaded=SceneManager.GetSceneByPath(scene);if(!loaded.IsValid()||!loaded.isLoaded)return null;
                var roots=loaded.GetRootGameObjects();int root=hierarchy[0];
                if(root<0||root>=roots.Length)return null;target=roots[root].transform;if(target.name!=names[0])return null;start=1;
            }
            for(int i=start;i<hierarchy.Length;i++)
            {int index=hierarchy[i];if(index<0||index>=target.childCount)return null;target=target.GetChild(index);if(target.name!=names[i])return null;}
            return target.gameObject.activeInHierarchy?target:null;
        }
        internal bool Valid
        {
            get
            {
                if(scene!=null&&scene.Length>512)return false;
                if(hierarchy==null)return names==null;
                if(hierarchy.Length>64||names==null||names.Length!=hierarchy.Length)return false;
                for(int i=0;i<hierarchy.Length;i++)if(hierarchy[i]<0||names[i]==null||names[i].Length>256)return false;
                return true;
            }
        }
    }
}
