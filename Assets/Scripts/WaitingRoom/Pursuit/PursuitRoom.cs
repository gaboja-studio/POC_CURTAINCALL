using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>방별 범위·조명·배치 지점. 순찰/스폰 Transform은 Scene 뷰에서 이동합니다. 조명 값은 <see cref="PursuitLighting"/>이 적용합니다.</summary>
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

        /// <summary>포획 암전이 유지되는 시각(Time.time). 포획 연출 중 매 갱신마다 연장된다.</summary>
        public float CaptureFlickerUntil { get; private set; } = float.NegativeInfinity;
        public bool CaptureFlickering => Time.time < CaptureFlickerUntil;

        public bool Contains(Vector3 p) => Area != null && Area.bounds.Contains(p + Vector3.up * .5f);
        public bool ContainsView(Vector3 p) => Area != null && Area.bounds.Contains(p);
        public void Flicker(float until) => CaptureFlickerUntil = Mathf.Max(CaptureFlickerUntil, until);
        public void ClearFlicker() => CaptureFlickerUntil = float.NegativeInfinity;
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
