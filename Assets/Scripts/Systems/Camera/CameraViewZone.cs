using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>로컬 플레이어만 시점을 요청한다. 매 물리 tick 재조사해 Exit 누락·teleport도 정리한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class CameraViewZone : MonoBehaviour
    {
        [SerializeField] CameraViewPreset preset;
        [SerializeField] int priority = 10;
        [Tooltip("이미 진입한 뒤에는 이 거리만큼 구역 밖으로 완전히 나갈 때까지 좁은 시점을 유지한다(m). 진입 범위는 그대로다.")]
        [SerializeField, Min(0f)] float exitPadding;
        public float ExitPadding => exitPadding;
        BoxCollider volume;
        Collider[] overlaps = new Collider[16];
        CameraViewDirector registered;

        void Awake() => volume = GetComponent<BoxCollider>();

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        void FixedUpdate() => RefreshRequest();
        void OnTriggerEnter(Collider other) => RefreshRequest();
        void OnTriggerExit(Collider other) => RefreshRequest();
        void OnDisable() => Release();

        void RefreshRequest()
        {
            var director = CameraViewDirector.Current;
            var local = NetworkPlayer.Local;
            if (registered != director) Release();
            if (!isActiveAndEnabled || volume == null || !volume.enabled || !volume.isTrigger || preset == null ||
                director == null || local == null || !local.isActiveAndEnabled || !ContainsLocal(local))
            {
                Release();
                return;
            }
            registered = director;
            registered.RequestView(this, preset, priority);
        }

        bool ContainsLocal(NetworkPlayer local)
        {
            Vector3 scale = transform.lossyScale;
            Vector3 half = Vector3.Scale(volume.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            bool retaining = registered != null && exitPadding > 0f;
            if (retaining) half += new Vector3(exitPadding, 0f, exitPadding);
            Vector3 center = transform.TransformPoint(volume.center);
            int count = Physics.OverlapBoxNonAlloc(center, half, overlaps, transform.rotation, ~0, QueryTriggerInteraction.Collide);
            if (count == overlaps.Length)
            {
                // 꽉 찬 버퍼에는 로컬 콜라이더가 누락될 수 있으므로 전체 결과로 다시 확인한다.
                var all = Physics.OverlapBox(center, half, transform.rotation, ~0, QueryTriggerInteraction.Collide);
                overlaps = new Collider[Mathf.Max(16, all.Length * 2)];
                all.CopyTo(overlaps, 0);
                count = all.Length;
            }
            for (int i = 0; i < count; i++)
            {
                var candidate = overlaps[i];
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy ||
                    candidate.GetComponentInParent<NetworkPlayer>() != local) continue;
                // OverlapBox는 확장된 회전 상자와 실제 콜라이더의 겹침을 검사한다.
                // 유지 구간에서는 원래 작은 volume으로 다시 검사해 조기 해제하지 않는다.
                if (retaining || Physics.ComputePenetration(volume, transform.position, transform.rotation,
                        candidate, candidate.transform.position, candidate.transform.rotation, out _, out _)) return true;
            }
            return false;
        }

        void Release()
        {
            if (registered != null) registered.ReleaseView(this);
            registered = null;
        }
    }
}
