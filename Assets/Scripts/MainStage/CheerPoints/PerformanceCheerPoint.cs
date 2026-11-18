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
        [Tooltip("1=난이도1, 2=난이도2, 4=난이도3. 배치 창에서 편집합니다. 7은 모든 난이도.")]
        public int difficultyMask = 7;
        public static readonly List<PerformanceCheerPoint> All = new();
        GameObject visual;
        GameObject source;
        CheerPointPresentation presentation;
        bool collected;
        float collectedAt;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);
        public bool Collected => collected;
        public bool AppliesToDifficulty(int level) => level >= 1 && level <= 3 && (difficultyMask & (1 << (level-1))) != 0;

        public void Present(bool active, bool taken, CheerPointStyle style)
        {
            if (taken && !collected) collectedAt = Time.unscaledTime;
            collected = taken;
            // Inactive performances and the host's OFF setting need no visual instances.
            if (!active)
            {
                if (visual) visual.SetActive(false);
                return;
            }
            var prefab = GameSettings.PerformanceEquipment.Visual(style);
            if (source != prefab)
            {
                if (visual) Destroy(visual);
                source = prefab; visual = prefab ? Instantiate(prefab,transform) : null;
                presentation = visual ? visual.AddComponent<CheerPointPresentation>() : null;
            }
            if (!visual) return;
            presentation.Sample(taken ? Time.unscaledTime-collectedAt : 0,taken,Camera.main);
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
