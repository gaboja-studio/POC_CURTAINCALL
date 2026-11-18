using System;
using System.Linq;
using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>Scene-authored transforms, not gameplay tuning. Stable references keep network device indices identical.</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class PerformanceLayout : MonoBehaviour
    {
        [Serializable]
        public struct Pose
        {
            public bool active;
            public Vector3 position, euler, scale;
            public static Pose Read(Transform t) => new Pose { active=t.gameObject.activeSelf,
                position=t.localPosition,euler=t.localEulerAngles,scale=t.localScale };
            public void Apply(Transform t)
            {
                t.SetLocalPositionAndRotation(position,Quaternion.Euler(euler)); t.localScale=scale;
                if(t.gameObject.activeSelf!=active) t.gameObject.SetActive(active);
            }
        }
        [Serializable]
        public sealed class Entry
        {
            public Transform target;
            public Pose original;
            public Pose[] levels;
        }
        public string trickId="HumanCannon";
        public Entry[] entries=Array.Empty<Entry>();
        int applied;
        public Entry Find(Transform target) => entries.FirstOrDefault(e=>e.target==target);
        public Entry Track(Transform target)
        {
            var entry=Find(target); if(entry!=null) return entry;
            var pose=Pose.Read(target);
            entry=new Entry {target=target,original=pose,levels=new[]{pose,pose,pose}};
            Array.Resize(ref entries,entries.Length+1); entries[^1]=entry; return entry;
        }
        public void Apply(int level)
        {
            foreach(var entry in entries)
                if(entry.target && entry.levels!=null && entry.levels.Length>=level && level>=1)
                    entry.levels[level-1].Apply(entry.target);
        }
        public void Restore() { foreach(var entry in entries) if(entry.target) entry.original.Apply(entry.target); }
        void Awake() => Sync();
        void Update() => Sync();
        void Sync()
        {
            var state=CampaignRun.Instance?.State;
            int level=state.HasValue && state.Value.TrickId==trickId && state.Value.Difficulty>0 ? state.Value.Difficulty : 1;
            if(applied==level) return;
            Apply(level); applied=level;
        }
    }
}
