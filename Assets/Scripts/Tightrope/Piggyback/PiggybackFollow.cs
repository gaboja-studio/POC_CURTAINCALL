using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 목마 따라 움직이기·맨 위 점프(006). 연결된 위 사람을 바로 아래 사람의 머리 위(발 = 아래 사람 발 + 몸 높이)에 붙이고 같은 방향을 보게 한다.
    /// 모든 화면이 자기 화면의 아래 사람 위치로 맞추므로 위 사람이 늦게 따라오지 않는다(위 사람 소유자가 맞춘 위치는 007 위치 공유로도 나간다).
    /// 내 캐릭터가 위에 있는 동안은 이동 계산을 끈다(W/S 무시, A/D 균형은 그대로). F 길게로 내려오면 다시 켜고 목마 맨 아래 사람 바로 앞(코스 진행 쪽)에 내려놓는다(2026-10-04 PM).
    /// 재시작·추락(내 균형 추락, 아래 사람 추락으로 인한 연쇄 추락)으로 풀린 경우는 내려놓지 않는다(재시작 위치·추락 처리를 따른다).
    /// 맨 위 점프(Harness/Project/Decisions/tightrope-rules.md 7, 2026-10-04):
    /// SB = 제자리 점프(목마 유지, 아래 사람 머리 기준으로 뛰었다가 다시 머리 위에 착지). W/S + SB = 앞·뒤 분리 점프(뛰는 순간 해제 요청, 이동 부품이 착지까지 처리).
    /// <see cref="PiggybackSystem"/>와 같은 오브젝트에 둔다. 위치 공유가 쓴 값을 덮어써야 하므로 늦게 실행한다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(PiggybackSystem))]
    public sealed class PiggybackFollow : MonoBehaviour
    {
        const float RestartGrace = 1f;     // 재시작 직후 이 시간(초) 안에 풀린 연결은 내려놓지 않는다
        const float LaunchTimeout = 1.5f;  // 분리 점프 후 이 시간(초)이 지나도 해제되지 않으면(거부 등) 다시 머리 위로
        const float RemoteHopMin = 0.05f;  // 남의 위 사람: 머리보다 이만큼(m) 이상 높게 받으면 제자리 점프 중으로 보고 그 높이로 보인다

        PiggybackSystem system;
        PlayerMover localMover;    // 이동 계산을 꺼 둔 내 캐릭터. 위에 있지 않으면 null
        NetworkPlayer localBottom; // 내가 타고 있던 목마의 맨 아래 사람(내려놓을 기준)
        float restartedAt = -10f;

        bool hopping;      // 내 캐릭터 제자리 점프 중(목마 유지)
        float hopHeight;   // 머리 위 높이(m)
        float hopSpeed;    // 위로 속도(m/s)
        float launchedAt = -1f; // 분리 점프를 시작한 시각. 해제될 때까지 머리 위에 붙이지 않는다
        PlayerMover controlRestore; // 내려놓은 뒤 한 프레임 조작을 껐다가 켤 이동 부품(위에서 누른 SB가 내려놓는 순간 점프로 나가지 않게)
        PlayerBalance carriedBalance; // 위층의 전체 점프·제자리 점프 공중 상태를 함께 관리한다

        void Update() => UpdateCarriedAirborne(NetworkPlayer.Local);

        /// <summary>맨 아래의 공유된 공중 상태와 내 제자리 점프를 합친다. 둘 다 끝나야 착지 충격을 한 번 받는다.</summary>
        void UpdateCarriedAirborne(NetworkPlayer local)
        {
            var below = local != null ? system.GetBelow(local) : null;
            bool carried = below != null && launchedAt < 0f && local.State == PlayerState.Normal
                           && local.TryGetComponent(out PlayerBalance balance) && !balance.HasFallen;
            if (!carried)
            {
                ClearCarriedAirborne();
                return;
            }
            carriedBalance = local.GetComponent<PlayerBalance>();
            carriedBalance.SetAirborne(hopping || Bottom(below).IsAirborne);
        }

        void ClearCarriedAirborne()
        {
            if (carriedBalance == null) return;
            // 분리 점프 중에는 이동 부품의 공중 상태를 유지한다(여기서 착지로 처리하지 않는다).
            var local = NetworkPlayer.Local;
            bool airborne = local != null && local.State == PlayerState.Normal && !carriedBalance.HasFallen
                            && carriedBalance.TryGetComponent(out PlayerMover mover) && mover.IsAirborne;
            carriedBalance.SetAirborne(airborne);
            carriedBalance = null;
        }

        void Awake() => system = GetComponent<PiggybackSystem>();

        void OnEnable()
        {
            NetworkPlayer.Restarted += HandleRestarted;
            NetworkPlayer.LocalDespawned += Release;
        }

        void OnDisable()
        {
            NetworkPlayer.Restarted -= HandleRestarted;
            NetworkPlayer.LocalDespawned -= Release;
            Release();
        }

        void LateUpdate()
        {
            var local = NetworkPlayer.Local;
            if (controlRestore != null)
            {
                // 이동 부품이 한 프레임 돌며 쌓인 입력을 비웠다
                if (local != null && local.State == PlayerState.Normal) controlRestore.SetControlEnabled(true);
                controlRestore = null;
            }
            UpdateHop(local);

            // 아래층부터 차례로 맞춰야 3층 이상도 이번 프레임 위치를 따른다
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || system.GetBelow(player) != null) continue;
                for (var below = player; below != null;)
                {
                    var above = system.GetAbove(below);
                    if (above == null) break;
                    if (above != local) PlaceOnTop(above, below, RemoteHop(above, below));
                    else if (launchedAt < 0f) PlaceOnTop(above, below, hopping ? hopHeight : 0f);
                    below = above;
                }
            }

            UpdateLocal(local);
            UpdateCarriedAirborne(local);
        }

        static Vector3 HeadOf(NetworkPlayer below)
        {
            float height = below.TryGetComponent(out PlayerMover mover) ? mover.BodyHeight : GameSettings.Base.Character.CapsuleHeight;
            Vector3 up = below.TryGetComponent(out PlayerModelSlot model) && model.Slot != null ? model.Slot.up : Vector3.up;
            return below.transform.position + up * height;
        }

        /// <summary>
        /// 아래 사람의 머리 위(겉모습이 기울어 있으면 기운 머리 끝)에 발을 둔다(제자리 점프 중이면 그만큼 위). 방향은 아래 사람 몸통 방향과 같고,
        /// 위 사람 몸의 기울기는 자기 균형으로 따로 보인다. 모든 화면의 기울기는 007 균형 공유로 같다.
        /// </summary>
        static void PlaceOnTop(NetworkPlayer above, NetworkPlayer below, float hop)
        {
            above.transform.SetPositionAndRotation(HeadOf(below) + Vector3.up * hop, below.transform.rotation);
        }

        /// <summary>남의 위 사람: 위치 공유로 받은 높이가 머리보다 높으면 제자리 점프 중인 높이로 본다.</summary>
        static float RemoteHop(NetworkPlayer above, NetworkPlayer below)
        {
            float hop = above.transform.position.y - HeadOf(below).y;
            return hop >= RemoteHopMin ? hop : 0f;
        }

        /// <summary>내 캐릭터가 맨 위에 있을 때 SB 입력을 처리하고 제자리 점프 높이를 갱신한다.</summary>
        void UpdateHop(NetworkPlayer local)
        {
            if (hopping)
            {
                hopSpeed -= GameSettings.Base.Character.Gravity * Time.deltaTime;
                hopHeight += hopSpeed * Time.deltaTime;
                if (hopHeight <= 0f && hopSpeed < 0f) EndHop(local);
                return;
            }

            if (local == null || localMover == null || launchedAt >= 0f) return;
            if (system.GetBelow(local) == null || system.GetAbove(local) != null) return; // 맨 위만(중간층은 점프 불가)
            if (local.State != PlayerState.Normal || !local.TryGetComponent(out PlayerInputReader reader) || !reader.enabled) return;

            var input = reader.Current;
            if (!input.ActionPressed) return;
            if (input.AuxDirection != 0)
            {
                LaunchLane(input.AuxDirection);
                return;
            }

            var rules = GameSettings.Tightrope.Piggyback;
            if (input.Move > 0.5f) Launch(local, rules.ForwardJumpSpeed);
            else if (input.Move < -0.5f) Launch(local, -rules.BackwardJumpSpeed);
            else StartHop(local);
        }

        void StartHop(NetworkPlayer local)
        {
            hopping = true;
            hopHeight = 0f;
            hopSpeed = localMover.JumpSpeed;
            if (local.TryGetComponent(out PlayerBalance balance)) balance.SetAirborne(true); // 공중에서는 균형 정지
        }

        /// <summary>제자리 점프를 끝낸다.</summary>
        void EndHop(NetworkPlayer local)
        {
            if (!hopping) return;
            hopping = false;
            hopHeight = 0f;
            hopSpeed = 0f;
            UpdateCarriedAirborne(local); // 전체 점프 중이면 아직 공중이므로 착지 충격을 주지 않는다
        }

        /// <summary>앞(+)·뒤(−) 분리 점프: 지금 자리에서 이동 부품의 점프로 날리고, 호스트에 해제를 요청한다.</summary>
        void Launch(NetworkPlayer local, float alongSpeed)
        {
            var mover = localMover;
            var body = mover.Body;
            body.enabled = false; // 머리 위에 맞춰 둔 위치에서 바로 출발하도록 몸통 위치를 맞춘다
            body.enabled = true;
            mover.Simulated = true;
            mover.LaunchJump(alongSpeed);
            launchedAt = Time.time;
            system.RequestLocalRelease();
        }

        void LaunchLane(int direction)
        {
            var mover = localMover;
            if (!mover.LaunchLaneJump(direction)) return; // 거부되면 연결·추종 모두 유지한다
            var body = mover.Body;
            body.enabled = false;
            body.enabled = true;
            mover.Simulated = true;
            launchedAt = Time.time;
            system.RequestLocalRelease();
        }

        /// <summary>내 캐릭터가 위에 있으면 이동 계산을 끄고, 내려오면 켜고 내려놓는다.</summary>
        void UpdateLocal(NetworkPlayer local)
        {
            var below = local != null ? system.GetBelow(local) : null;

            if (launchedAt >= 0f)
            {
                if (below == null)
                {
                    // 분리 점프로 해제됨: 이동 부품이 착지까지 처리하므로 내려놓지 않는다
                    launchedAt = -1f;
                    localMover = null;
                    localBottom = null;
                    return;
                }
                if (Time.time - launchedAt < LaunchTimeout || (localMover != null && localMover.IsAirborne)) return;
                launchedAt = -1f; // 해제가 거부됐다: 다시 머리 위로
                if (localMover != null) localMover.Simulated = false;
            }

            if (below != null)
            {
                localBottom = Bottom(below);
                if (localMover == null && local.TryGetComponent(out PlayerMover mover))
                {
                    localMover = mover;
                    localMover.Simulated = false;
                }
                return;
            }

            EndHop(local);
            if (localMover == null) return;
            // 연쇄 추락·내 균형 추락으로 풀렸으면 내려놓지 않는다(그 자리에서 래그돌로 떨어진다)
            bool fell = local == null || local.State != PlayerState.Normal || system.WasDropped(local)
                        || (local.TryGetComponent(out PlayerBalance balance) && balance.HasFallen);
            bool putDown = !fell && Time.time - restartedAt > RestartGrace;
            var bottom = localBottom;
            var released = localMover;
            Release();
            if (putDown && bottom != null)
            {
                PutDownInFront(released, bottom);
                released.SetControlEnabled(false);
                controlRestore = released;
            }
        }

        NetworkPlayer Bottom(NetworkPlayer player)
        {
            for (var below = system.GetBelow(player); below != null; below = system.GetBelow(player))
                player = below;
            return player;
        }

        /// <summary>
        /// 목마 맨 아래 사람 바로 앞(코스 진행 쪽, 몸 간격)에 같은 높이로 내려놓는다(2026-10-04 PM).
        /// 임시 규칙(기획 미정): 그 자리에 다른 사람이 있어도 그대로 놓는다.
        /// </summary>
        static void PutDownInFront(PlayerMover mover, NetworkPlayer bottom)
        {
            if (!bottom.TryGetComponent(out PlayerMover bottomMover)) return;
            Vector3 forward = bottomMover.CourseForward;
            if (forward == Vector3.zero) forward = bottom.transform.forward;
            float gap = bottomMover.BodyRadius + mover.BodyRadius + GameSettings.Base.Character.PlayerGap;
            Vector3 position = bottom.transform.position + forward * gap;

            var body = mover.Body;
            body.enabled = false; // CharacterController는 꺼야 위치를 바로 옮길 수 있다
            mover.transform.SetPositionAndRotation(position, bottom.transform.rotation);
            body.enabled = true;
        }

        void HandleRestarted(NetworkPlayer player)
        {
            if (player == null || !player.IsLocal) return;
            restartedAt = Time.time;
            Release();
        }

        /// <summary>내 캐릭터의 이동 계산을 되돌린다(내려놓지 않음).</summary>
        void Release()
        {
            EndHop(NetworkPlayer.Local);
            ClearCarriedAirborne();
            if (localMover != null) localMover.Simulated = true;
            localMover = null;
            localBottom = null;
            launchedAt = -1f;
        }
    }
}
