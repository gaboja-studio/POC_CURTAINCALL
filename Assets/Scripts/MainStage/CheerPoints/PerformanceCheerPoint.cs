using System.Collections.Generic;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>Stable authored crossing plane. Its animated visual never changes the scoring plane.</summary>
    public sealed class PerformanceCheerPoint : MonoBehaviour
    {
        public string pointId;
        public string trickId = "Tightrope";
        public bool equipmentRoute;
        public static readonly List<PerformanceCheerPoint> All = new();
        GameObject visual;
        GameObject source;
        bool collected;
        float collectedAt;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);
        public bool Collected => collected;

        public void Present(bool active, bool taken, CheerPointStyle style)
        {
            var prefab = GameSettings.PerformanceEquipment.Visual(style);
            if (source != prefab)
            {
                if (visual) Destroy(visual);
                source = prefab; visual = prefab ? Instantiate(prefab,transform) : null;
            }
            if (taken && !collected) collectedAt = Time.unscaledTime;
            collected = taken;
            if (!visual) return;
            float progress = taken ? Mathf.Clamp01((Time.unscaledTime-collectedAt)/.3f) : 0;
            visual.SetActive(active && progress < 1);
            visual.transform.localScale = Vector3.one * (1-progress);
        }

        public bool Crossed(Vector3 previous, Vector3 current)
            => CrossesOpening(transform.InverseTransformPoint(previous), transform.InverseTransformPoint(current), GameSettings.PerformanceEquipment.hoopRadius);

        public static bool CrossesOpening(Vector3 a, Vector3 b, float radius)
        {
            if (!float.IsFinite(a.sqrMagnitude) || !float.IsFinite(b.sqrMagnitude) || !float.IsFinite(radius) || radius <= 0) return false;
            if (Mathf.Abs(b.z-a.z) < .0001f || a.z*b.z > 0 || Mathf.Abs(a.z) < .0001f) return false;
            float t = -a.z/(b.z-a.z);
            if (t<0 || t>1) return false;
            Vector3 hit = Vector3.LerpUnclamped(a,b,t);
            return hit.x*hit.x + hit.y*hit.y < radius*radius;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = equipmentRoute ? new Color(1,.5f,.05f) : Color.yellow;
            var old=Gizmos.matrix; Gizmos.matrix=transform.localToWorldMatrix;
            float radius=GameSettings.PerformanceEquipment.hoopRadius;
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI/20, b=(i+1)*Mathf.PI/20;
                Gizmos.DrawLine(new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius,new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius);
            }
            Gizmos.DrawLine(Vector3.zero,Vector3.forward); Gizmos.matrix=old;
        }
    }
}
