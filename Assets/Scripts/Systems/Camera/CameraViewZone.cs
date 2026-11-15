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

        void Awake()
        {
            volume = GetComponent<BoxCollider>();
            // 구역 트리거가 주변 벽·소품 수백 개와 매 물리 tick 겹침 쌍을 만들어 이벤트 처리에 시간을 썼다(2026-11-14 대기실 최적화).
            // 판정은 FixedUpdate가 매 tick 직접 하므로 물리 엔진의 트리거 쌍은 만들지 않는다(침투 계산은 그대로 된다).
            if (Application.isPlaying && TryGetComponent(out Rigidbody body)) body.detectCollisions = false;
        }

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        void FixedUpdate() => RefreshRequest();
        // 트리거 이벤트는 반응만 앞당긴다(FixedUpdate가 매 tick 다시 본다). 내 캐릭터가 아닌 물체의 이벤트는 무시한다.
        // 대기실은 거의 모든 콜라이더가 Default 레이어라, 진입 프레임에 벽·소품 이벤트 2584회가 매번 OverlapBox를 돌려
        // 약 2초를 썼다(Task-20261105-108). 판정 기준(내 NetworkPlayer 아래 콜라이더)은 ContainsLocal과 같다.
        void OnTriggerEnter(Collider other) { if (IsLocalCollider(other)) RefreshRequest(); }
        void OnTriggerExit(Collider other) { if (IsLocalCollider(other)) RefreshRequest(); }

        static bool IsLocalCollider(Collider other)
        {
            var local = NetworkPlayer.Local;
            // 내 캐릭터가 없으면 기존처럼 정리(Release)만 하도록 넘긴다.
            return local == null || (other != null && other.transform.IsChildOf(local.transform));
        }
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
            // 빠른 판정(2026-11-14 최적화): 예전에는 매 tick 구역 안 모든 콜라이더를 훑어 내 캐릭터를 찾았다(큰 구역은 2천 개 이상).
            // 내 몸통 중심이 구역 안이면 겹침이 확실하고, 구역에서 3m 넘게 떨어졌으면 겹칠 수 없다. 경계 근처에서만 아래 정밀 검사를 한다.
            if (local.TryGetComponent(out CharacterController body) && body.enabled)
            {
                Vector3 at = Quaternion.Inverse(transform.rotation) * (body.bounds.center - center);
                Vector3 inner = Vector3.Scale(volume.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                if (Mathf.Abs(at.x) <= inner.x && Mathf.Abs(at.y) <= inner.y && Mathf.Abs(at.z) <= inner.z) return true;
                const float Far = 3f;
                if (Mathf.Abs(at.x) > half.x + Far || Mathf.Abs(at.y) > half.y + Far || Mathf.Abs(at.z) > half.z + Far) return false;
            }
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
