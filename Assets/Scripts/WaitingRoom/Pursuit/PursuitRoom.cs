using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>방별 범위·조명·배치 지점. 순찰/스폰 Transform은 Scene 뷰에서 이동합니다.</summary>
    public sealed class PursuitRoom : MonoBehaviour
    {
        public BoxCollider Area;
        public Light[] Lights;
        public Transform[] SpawnPoints;
        public Transform[] PatrolPoints;
        [Min(0)] public int Count = 1;
        [Tooltip("끄면 이 방의 순찰 경로/포획 범위 안에서만 추격합니다.")]
        public bool RoamAcrossRooms = true;
        [Tooltip("전용 배치 창에서 편집한 마네킹을 사용. 기존 Count/SpawnPoints는 보존됩니다.")]
        public bool UsePlacements;
        public PursuitPlacement[] Placements = System.Array.Empty<PursuitPlacement>();
        [System.NonSerialized] float[] intensities;
        [System.NonSerialized] Light[] flickerLights;
        float flickerUntil;

        public bool Contains(Vector3 p) => Area != null && Area.bounds.Contains(p + Vector3.up * .5f);
        public void Flicker(float until)
        {
            if (Lights == null) return;
            if (intensities == null) {
                flickerLights = (Light[])Lights.Clone();
                intensities = new float[flickerLights.Length];
                for (int i = 0; i < flickerLights.Length; i++) if (flickerLights[i] != null) intensities[i] = flickerLights[i].intensity;
            }
            flickerUntil = Mathf.Max(flickerUntil, until);
        }
        void LateUpdate()
        {
            if (intensities == null || flickerLights == null) return;
            bool flashing = Time.time < flickerUntil;
            for (int i = 0; i < flickerLights.Length; i++)
                if (flickerLights[i] != null) flickerLights[i].intensity = intensities[i] * (flashing && Mathf.Sin(Time.time * 37f) > -.2f ? .02f : 1f);
            if (!flashing) { intensities = null; flickerLights = null; }
        }
        void OnDisable()
        {
            if (intensities == null || flickerLights == null) return;
            for (int i = 0; i < flickerLights.Length; i++) if (flickerLights[i] != null) flickerLights[i].intensity = intensities[i];
            intensities = null; flickerLights = null;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            if (SpawnPoints != null) foreach (var p in SpawnPoints) if (p != null) Gizmos.DrawWireSphere(p.position, .4f);
            Gizmos.color = Color.cyan;
            if (PatrolPoints == null) return;
            for (int i = 0; i < PatrolPoints.Length; i++) {
                var a = PatrolPoints[i]; var b = PatrolPoints[(i + 1) % PatrolPoints.Length];
                if (a != null && b != null) Gizmos.DrawLine(a.position, b.position);
            }
        }
    }
}
