using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Survival;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ⑥ 무대 입구 통로 집결 판정(테스트 대체 영역, 백스테이지 연결 CC-111 때 실제 ⑥으로 교체).
    /// 판정 화면(호스트·오프라인 런)이 쉬는 시간에 살아 있는 전원이 이 상자 안에 있으면 <see cref="CampaignRun.NotifyAllAtStageEntrance"/>를 한 번 부른다.
    /// 그 뒤 공연 전환은 런 진행·장소 전환이 모두에게 함께 한다. 일부만 들어오면 아무 일도 없다.
    /// 위치로만 판정하므로 원격 캐릭터도 동기화된 위치로 센다. 접속하지 않은 맵 확인 씬에서는 씬에 놓인 플레이어를 센다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StageEntranceGather : MonoBehaviour
    {
        [Tooltip("화면 왼쪽 위에 집결 인원을 표시한다(공동 테스트 확인용).")]
        [SerializeField] bool showStatus = true;

        [Header("판정 범위 (이 오브젝트 기준 로컬, m)")]
        [Tooltip("판정 상자 중심. 이 오브젝트 위치에서의 거리.")]
        [SerializeField] Vector3 areaCenter = Vector3.zero;
        [Tooltip("판정 상자 크기(x = 통로 길이 방향, y = 높이, z = 통로 폭 방향).")]
        [SerializeField] Vector3 areaSize = new(4f, 3f, 7f);
        [Tooltip("바닥 테두리 묶음(자식 N·S·W·E 띠). 지정하면 범위를 바꿀 때 함께 맞춘다.")]
        [SerializeField] Transform border;

        readonly List<PlayerMover> offlinePlayers = new();
        BoxCollider area;
        bool notified;

        /// <summary>영역 안에 있는 살아 있는 인원.</summary>
        public int InsideCount { get; private set; }

        /// <summary>살아 있는 전체 인원(사망·리타이어 제외).</summary>
        public int AliveCount { get; private set; }

        /// <summary>판정 화면에서 전원 집결을 런 진행에 알렸다.</summary>
        public event Action Gathered;

        void Awake()
        {
            area = GetComponent<BoxCollider>();
            ApplyArea();
        }

        // 쉬는 시간 만료 잡힘 판정(RestTimeoutCatch)에 무대 입구 통로 안 여부를 알려 준다.
        void OnEnable() => RestTimeoutCatch.IsAtStageEntrance = IsInside;
        void OnDisable()
        {
            if (RestTimeoutCatch.IsAtStageEntrance == (Func<NetworkPlayer, bool>)IsInside) RestTimeoutCatch.IsAtStageEntrance = null;
        }

        /// <summary>이 플레이어가 무대 입구 통로 판정 상자 안에 있는지.</summary>
        public bool IsInside(NetworkPlayer player) => player != null && area != null && Contains(player.transform.position);

        void OnValidate() => ApplyArea();

        /// <summary>인스펙터의 판정 범위를 상자 충돌체와 바닥 테두리에 반영한다.</summary>
        public void ApplyArea()
        {
            var box = area != null ? area : GetComponent<BoxCollider>();
            if (box == null) return;
            areaSize = Vector3.Max(areaSize, new Vector3(0.5f, 0.5f, 0.5f));
            box.center = areaCenter;
            box.size = areaSize;
            if (border == null) return;

            var lossy = transform.lossyScale;
            var c = transform.TransformPoint(areaCenter);
            float hx = areaSize.x * lossy.x / 2f, hz = areaSize.z * lossy.z / 2f;
            var right = transform.right;
            var forward = transform.forward;
            void Strip(string name, Vector3 at, float lengthX, float lengthZ)
            {
                var t = border.Find(name);
                if (t == null) return;
                var floor = new Vector3(at.x, border.position.y + 0.01f, at.z);
                t.SetPositionAndRotation(floor, transform.rotation);
                t.localScale = new Vector3(lengthX, t.localScale.y, lengthZ);
            }
            Strip("N", c + forward * (hz - 0.05f), hx * 2f, 0.1f);
            Strip("S", c - forward * (hz - 0.05f), hx * 2f, 0.1f);
            Strip("W", c - right * (hx - 0.05f), 0.1f, hz * 2f);
            Strip("E", c + right * (hx - 0.05f), 0.1f, hz * 2f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(areaCenter, areaSize);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
            Gizmos.DrawWireCube(areaCenter, areaSize);
        }

        void Start()
        {
            if (NetworkPlayer.All.Count == 0)
                offlinePlayers.AddRange(FindObjectsByType<PlayerMover>(FindObjectsSortMode.None));
        }

        void Update()
        {
            Count();

            var run = CampaignRun.Instance;
            if (run == null || !run.HasAuthority || run.State.Phase != CampaignPhase.RestTime)
            {
                notified = false;
                return;
            }
            // 쉬는 시간마다 한 번만 알린다(마지막 아침처럼 거절돼도 매 프레임 다시 부르지 않는다).
            if (notified || AliveCount == 0 || InsideCount < AliveCount) return;

            notified = true;
            if (!run.NotifyAllAtStageEntrance()) return;
            Debug.Log($"[WaitingRoom] ⑥ 전원 집결 {InsideCount}/{AliveCount} → 공연 전환");
            Gathered?.Invoke();
        }

        void Count()
        {
            int inside = 0, alive = 0;
            if (NetworkPlayer.All.Count > 0)
            {
                foreach (var player in NetworkPlayer.All)
                {
                    if (player.State == PlayerState.Fallen || player.State == PlayerState.Retired) continue;
                    alive++;
                    if (Contains(player.transform.position)) inside++;
                }
            }
            else
            {
                foreach (var mover in offlinePlayers)
                {
                    if (mover == null || !mover.isActiveAndEnabled) continue;
                    alive++;
                    if (Contains(mover.transform.position)) inside++;
                }
            }
            InsideCount = inside;
            AliveCount = alive;
        }

        // 발 위치는 바닥에 닿아 있으므로 조금 올려서 본다.
        bool Contains(Vector3 feet) => area.bounds.Contains(feet + Vector3.up * 0.5f);

        void OnGUI()
        {
            if (!showStatus) return;
            GUI.Label(new Rect(10, 10, 320, 24), $"⑥ 무대 입구 집결 {InsideCount}/{AliveCount}");
        }
    }
}
