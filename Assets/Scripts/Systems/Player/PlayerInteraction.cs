using System;
using CurtainCall.Settings;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 동작 부품: 상호작용·해제 요청 창구. 조작 규칙이 요청하면 신호를 보낸다.
    /// 무엇과 상호작용할지(외줄에서는 목마 연결·해제)는 이 신호를 받는 기능(목마 006)이 정한다.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        /// <summary>공통 행동 자격. 네트워크 단원은 보통 상태일 때만 상호작용한다.</summary>
        public static bool CanInteract(Component actor) => actor != null
            && (!actor.TryGetComponent<NetworkPlayer>(out var player) || player.IsSpawned && player.State == PlayerState.Normal);

        /// <summary>호스트가 클라이언트 요청을 검사한다. 단원이 없는 단독 제작 씬은 기존 오프라인 동작을 유지한다.</summary>
        public static bool CanInteract(ulong actor)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == actor) return player.State == PlayerState.Normal;
            return NetworkPlayer.All.Count == 0 && (CurtainCall.Network.Session.NetworkSessionManager.Instance == null
                || CurtainCall.Network.Session.NetworkSessionManager.Instance.ConnectionState == CurtainCall.Network.Session.SessionConnectionState.Offline);
        }
        /// <summary>몸 상태를 읽어 호스트·클라이언트가 같은 근접 상호작용 거리를 사용한다.</summary>
        public static float RestrictReach(float distance, Component actor)
        {
            if (distance < 0f || actor == null) return -1f;
            float scale = actor.TryGetComponent<PlayerCondition>(out var condition)
                ? GameSettings.Base.BodyDamage.ReachMultiplier(condition.LostArmCount) : 1f;
            return distance <= GameSettings.Base.Interaction.Distance * scale ? distance : -1f;
        }
        /// <summary>발 원점 대신 현재 몸통 중심을 사용한다. 비활성 충돌체도 같은 위치를 반환한다.</summary>
        public static Vector3 Center(Component actor)
        {
            if (actor.TryGetComponent(out PlayerMover mover) && mover.Body != null)
                return actor.transform.TransformPoint(mover.Body.center);
            return actor.transform.position + Vector3.up * (GameSettings.Base.Character.CapsuleHeight * .5f);
        }

        /// <summary>대상 중심의 구형 상호작용 범위. 입력·호스트·안내에서 함께 사용한다.</summary>
        public static bool InRange(Component actor, Vector3 target, float radius, Transform targetRoot = null, bool ignoreRope = false) => actor != null
            && float.IsFinite(radius) && radius > 0f && (Center(actor) - target).sqrMagnitude <= radius * radius
            && ClearPathFrom(Center(actor), target, targetRoot, actor, ignoreRope);

        static readonly RaycastHit[] reachHits = new RaycastHit[64];
        static readonly Collider[] reachOverlaps = new Collider[32];

        public static bool ClearPath(Component actor, Vector3 target, Transform targetRoot = null) => actor != null
            && ClearPathFrom(Center(actor), target, targetRoot, actor);

        /// <summary>몸 중심부터 대상까지 같은 경로를 검사한다. 층·벽은 차단하고 대상 자체와 사람은 제외한다.</summary>
        public static bool ClearPathFrom(Vector3 origin, Vector3 target, Transform targetRoot, Component actor = null, bool ignoreRope = false)
        {
            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            if (!float.IsFinite(distance)) return false;
            int overlaps = Physics.OverlapSphereNonAlloc(origin, .01f, reachOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlaps == reachOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++)
                if (Blocks(reachOverlaps[i], targetRoot, actor, ignoreRope)) return false;
            if (distance < .01f) return true;
            int count = Physics.RaycastNonAlloc(origin, delta / distance, reachHits, Mathf.Max(0, distance - .01f),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == reachHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (Blocks(reachHits[i].collider, targetRoot, actor, ignoreRope)) return false;
            return true;
        }

        static bool Blocks(Collider hit, Transform targetRoot, Component actor, bool ignoreRope) => hit != null
            && (targetRoot == null || !hit.transform.IsChildOf(targetRoot))
            && (actor == null || !hit.transform.IsChildOf(actor.transform))
            && (!ignoreRope || !CurtainCall.Tightrope.TightropeCourse.IsRopeSurface(hit))
            && !CurtainCall.Damage.DismemberedLimb.Contains(hit)
            && hit.GetComponentInParent<PlayerMover>() == null;

        /// <summary>상호작용 요청(외줄: 목마 연결 시도, F 짧게).</summary>
        public event Action InteractRequested;

        /// <summary>해제 요청(외줄: 목마 해제, F 길게).</summary>
        public event Action ReleaseRequested;

        /// <summary>마지막 요청 이름과 시각. 디버그 표시용.</summary>
        /// <summary>
        /// 상호작용 가능 거리 배율(×, 0 이상). 기본 1. 예: 팔을 잃으면 외줄 신체 손상 불이익이 줄인다.
        /// 거리 판정을 하는 기능(목마 006 올라타기 등)이 자기 기본 거리에 곱해 쓴다.
        /// </summary>
        public float ReachMultiplier
        {
            get => reachMultiplier;
            set => reachMultiplier = Mathf.Max(0f, value);
        }

        float reachMultiplier = 1f;

        public string LastRequest { get; private set; } = "-";
        public float LastRequestTime { get; private set; } = -10f;

        /// <summary>상호작용을 요청한다.</summary>
        public void RequestInteract()
        {
            Record("상호작용");
            InteractRequested?.Invoke();
        }

        /// <summary>해제를 요청한다.</summary>
        public void RequestRelease()
        {
            Record("해제");
            ReleaseRequested?.Invoke();
        }

        void Record(string request)
        {
            LastRequest = request;
            LastRequestTime = Time.time;
        }
    }
}
