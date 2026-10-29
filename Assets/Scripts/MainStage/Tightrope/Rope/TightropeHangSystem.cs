using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// M2(2026-10-27) 외줄 추락 매달림·동료 끌어올리기. 근거: Harness/Project/Decisions/tightrope-course-rules.md #1-1.
    /// 추락 게이지가 넘치면(<see cref="PlayerBalance.GaugeFallCatcher"/>) 바로 죽지 않고 줄을 잡고 버틴다(기본 7초).
    /// 스스로는 못 올라오고, 근처 동료가 F를 누르면 2초 동안 끌어올린다(그동안 시간 정지, 동료가 다치거나 죽으면 끊김).
    /// 시간이 다 지나면 낙사. 목마 낙하는 <see cref="PiggybackSystem"/>이 아래부터 차례로 <see cref="ServerTryHang"/>을 부른다.
    /// 판정은 호스트가 하고, 몸 위치는 각자 화면이 내 캐릭터만 옮긴다(위치 공유는 기존 방식). 씬에 놓지 않고 <see cref="TightropeRun"/>이 붙인다.
    /// 줄 밖 착지·바닥선 추락처럼 게이지가 아닌 추락은 지금처럼 바로 낙사한다.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed partial class TightropeHangSystem : MonoBehaviour
    {
        public static TightropeHangSystem Current { get; private set; }

        /// <summary>호스트: 누군가 줄에 매달렸다(접속 번호). 관객 추락 야유는 이때 한 번(뒤 낙사에 중복 없음).</summary>
        public static event Action<ulong> ServerHangStarted;

        readonly TightropeHangBook book = new();
        TightropeCourse course;
        TightropeRun run;
        double snapshotAt;

        static TightropeSettings.HangSettings Rules => GameSettings.Tightrope.Hang;
        int Round => run != null ? run.Round : 0;
        bool Active => run != null && course != null && course.IsGameplayActive
            && (run.State == TightropeRunState.Waiting || run.State == TightropeRunState.Running);

        public IEnumerable<TightropeHangBook.Hang> Hangs => book.All;
        public bool IsHanging(NetworkPlayer player) => player != null && book.IsHanging(player.OwnerClientId)
            || player != null && player.IsLocal && localHang != null;
        public bool IsRescuing(NetworkPlayer player) => player != null && book.IsRescuing(player.OwnerClientId);

        /// <summary>표시용 남은 버티는 시간(초). 끌어올리는 중이면 멈춘 값.</summary>
        public float RemainingSeconds(TightropeHangBook.Hang hang) => hang == null ? 0f
            : (float)Math.Max(0d, hang.IsPulling || IsHost ? hang.Remaining : hang.Remaining - (Now - snapshotAt));

        /// <summary>외줄 추락 매달림 전용 추락 게이지. 7초 동안 0→1, 구조 중에는 버티기 시간과 함께 정지한다.</summary>
        public float HangDangerRatio(NetworkPlayer player)
        {
            if (player == null || !book.TryGet(player.OwnerClientId, out var hang)) return 0f;
            return Mathf.Clamp01(1f - RemainingSeconds(hang) / Rules.HoldSeconds);
        }

        /// <summary>표시용 끌어올리기 진행(0~1).</summary>
        public float PullProgress(TightropeHangBook.Hang hang)
        {
            if (hang == null || !hang.IsPulling) return 0f;
            double left = IsHost ? hang.PullRemaining : hang.PullRemaining - (Now - snapshotAt);
            return Mathf.Clamp01(1f - (float)(Math.Max(0d, left) / Rules.PullSeconds));
        }

        /// <summary>매달린 사람이 줄을 잡은 지점(줄 윗면).</summary>
        public Vector3 GripPoint(TightropeHangBook.Hang hang) => course != null && hang != null ? course.GetLanePosition(hang.Lane, hang.Distance) : Vector3.zero;

        static double Now => Time.unscaledTimeAsDouble;

        void Awake()
        {
            course = GetComponent<TightropeCourse>();
            run = GetComponent<TightropeRun>();
        }

        void OnEnable()
        {
            Current = this;
            PlayerBalance.GaugeFallCatcher = CatchLocalGaugeFall;
        }

        void OnDisable()
        {
            if (PlayerBalance.GaugeFallCatcher == (Func<PlayerBalance, bool>)CatchLocalGaugeFall) PlayerBalance.GaugeFallCatcher = null;
            ReleaseRescuerBalance();
            ReleaseLocal(false);
            Disconnect();
            book.Clear();
            if (Current == this) Current = null;
        }

        void Update()
        {
            Connect();
            if (!Active || lastRound != Round)
            {
                lastRound = Round;
                if (book.Count > 0) { book.Clear(); if (IsHost) Publish(); }
                if (localHang != null) ReleaseLocal(false);
                if (!Active) { ReleaseRescuerBalance(); return; }
            }
            if (IsHost) ServerTick(Time.deltaTime);
            UpdateLocal();
        }
        int lastRound;

        // ── 호스트 ────────────────────────────────────────────

        /// <summary>
        /// 호스트: 이 사람을 줄에 매달리게 한다. 가까운 빈 줄이 없으면 낙사시키고 false.
        /// <paramref name="preferredLane"/>가 음수면 위치에서 가장 가까운 줄을 쓴다.
        /// </summary>
        public bool ServerTryHang(NetworkPlayer player, float distance, int preferredLane = -1)
        {
            if (!IsHost || player == null || !player.IsSpawned || player.State != PlayerState.Normal) return false;
            if (book.IsHanging(player.OwnerClientId)) return true;
            if (!Active) { player.ServerKill(); return false; }
            if (preferredLane < 0 && !course.TryGetRopePoint(player.transform.position, out preferredLane, out _))
                preferredLane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
            int lane = book.PickLane(preferredLane, distance, course.LaneCount, Rules.LaneReach, Rules.LaneOccupyDistance, course.IsLaneUsable);
            if (lane < 0)
            {
                player.ServerKill(); Publish(); return false;
            }
            // 받는 사람이 끊어진 상태(끌어올리는 중)였다면 정리한다.
            foreach (var other in book.All) if (other.Rescuer == player.OwnerClientId) book.CancelPull(other.Id);
            book.Begin(player.OwnerClientId, lane, distance, Rules.HoldSeconds);
            NotifyHang(player.OwnerClientId);
            Publish();
            return true;
        }

        static void NotifyHang(ulong id)
        {
            if (ServerHangStarted == null) return;
            foreach (Action<ulong> observer in ServerHangStarted.GetInvocationList())
                try { observer(id); }
                catch (Exception error) { Debug.LogException(error); }
        }

        void ServerRescue(ulong rescuerId, ulong target)
        {
            var rescuer = Find(rescuerId);
            if (rescuer == null || rescuer.State != PlayerState.Normal || !book.TryGet(target, out var hang)) return;
            if (!WithinRescueReach(rescuer, hang, Rules.RescueDistance)) return;
            if (book.StartPull(target, rescuerId, (int)rescuer.LostParts, Rules.PullSeconds))
            { UpdateRescuerBalance(); Publish(); }
        }

        void ServerCancel(ulong rescuerId)
        {
            bool changed = false;
            foreach (var hang in book.All) if (hang.Rescuer == rescuerId) changed |= book.CancelPull(hang.Id);
            if (changed) { UpdateRescuerBalance(); Publish(); }
        }

        void ServerTick(float deltaTime)
        {
            bool changed = false;
            List<ulong> gone = null;
            foreach (var hang in book.All)
            {
                var player = Find(hang.Id);
                if (player == null || player.State != PlayerState.Normal) { (gone ??= new List<ulong>()).Add(hang.Id); continue; }
                if (!hang.IsPulling) continue;
                var rescuer = Find(hang.Rescuer);
                // 도와주는 동료가 떠나거나, 죽거나, 다치거나, 멀어지거나, 자신도 떨어지면 끊기고 남은 시간이 다시 흐른다.
                if (rescuer == null || rescuer.State != PlayerState.Normal || (int)rescuer.LostParts != hang.RescuerParts
                    || book.IsHanging(rescuer.OwnerClientId)
                    || !WithinRescueReach(rescuer, hang, Rules.RescueDistance * 1.5f))
                    changed |= book.CancelPull(hang.Id);
            }
            if (gone != null) { foreach (ulong id in gone) book.Remove(id); changed = true; }
            foreach (var (id, outcome, hang) in book.Tick(deltaTime))
            {
                changed = true;
                if (outcome == TightropeHangBook.Outcome.Dropped) Find(id)?.ServerKill();
                else SendRescued(hang);
            }
            if (changed) { UpdateRescuerBalance(); Publish(); }
        }

        // ── 내 캐릭터 ─────────────────────────────────────────

        sealed class LocalHang
        {
            public NetworkPlayer Player;
            public PlayerMover Mover;
            public PlayerBalance Balance;
            public Vector3 FallPosition;
            public double RequestedAt;
            public bool Confirmed;
        }
        LocalHang localHang;
        PlayerBalance rescuerBalance;
        bool placing;

        // 균형은 각 캐릭터의 소유자가 계산한다. 호스트가 승인한 구조 상태로 로컬 캐릭터만 보호한다.
        void UpdateRescuerBalance()
        {
            var local = NetworkPlayer.Local;
            var next = Active && local != null && local.State == PlayerState.Normal && book.IsRescuing(local.OwnerClientId)
                ? local.GetComponent<PlayerBalance>() : null;
            if (rescuerBalance != next) { ReleaseRescuerBalance(); rescuerBalance = next; }
            if (rescuerBalance != null) rescuerBalance.IsRescueSupported = true;
        }

        void ReleaseRescuerBalance()
        {
            if (rescuerBalance != null) rescuerBalance.IsRescueSupported = false;
            rescuerBalance = null;
        }

        /// <summary>내 캐릭터의 추락 게이지가 넘쳤다. 외줄 진행 중이면 매달리기를 요청하고 추락을 막는다.</summary>
        bool CatchLocalGaugeFall(PlayerBalance balance)
        {
            var local = NetworkPlayer.Local;
            if (!Active || local == null || balance == null || balance.gameObject != local.gameObject || local.State != PlayerState.Normal) return false;
            if (localHang != null) return true;
            if (!local.TryGetComponent(out PlayerMover mover) || mover.Body == null) return false;
            if (!course.TryGetRopePoint(local.transform.position, out int lane, out float distance)) return false;
            BindLocal(local, mover, balance);
            Send(Request.Hang, 0, lane, distance);
            return true;
        }

        void BindLocal(NetworkPlayer local, PlayerMover mover, PlayerBalance balance)
        {
            localHang = new LocalHang
            {
                Player = local, Mover = mover, Balance = balance, FallPosition = local.transform.position, RequestedAt = Now
            };
            mover.Teleported += OnLocalTeleported;
            mover.ClearPendingInput(); mover.Simulated = false; mover.Body.enabled = false;
            mover.ResetExternalMotion(true);
            if (balance != null) { balance.IsSuspended = true; balance.SetAirborne(true); }
        }

        void UpdateLocal()
        {
            UpdateRescuerBalance();
            var local = NetworkPlayer.Local;
            // 목마 낙하처럼 호스트가 먼저 매달리게 한 경우에도 내 몸을 붙잡는다.
            if (localHang == null && local != null && local.State == PlayerState.Normal && book.IsHanging(local.OwnerClientId)
                && local.TryGetComponent(out PlayerMover mover) && mover.Body != null)
                BindLocal(local, mover, local.GetComponent<PlayerBalance>());
            if (localHang == null) return;
            if (local != localHang.Player || local == null || local.State != PlayerState.Normal) { ReleaseLocal(false); return; }
            if (book.TryGet(local.OwnerClientId, out var hang))
            {
                localHang.Confirmed = true;
                Place(GripPoint(hang) - Vector3.up * Rules.HangDrop);
            }
            else if (localHang.Confirmed || Now - localHang.RequestedAt > 2d)
            {
                // 호스트 장부에서 빠졌는데 구조 신호가 없었다 = 거절·낙사. 상태 변경이 오기 전까지 떨어지는 대로 둔다.
                ReleaseLocal(false);
                if (local.TryGetComponent(out PlayerBalance balance)) balance.ForceFall();
                return;
            }
            else Place(localHang.FallPosition);
            // 목마 위층이던 사람은 목마 해제가 몸을 다시 켤 수 있으므로 매 프레임 고정을 유지한다.
            localHang.Mover.Simulated = false;
            if (localHang.Mover.Body != null) localHang.Mover.Body.enabled = false;
            localHang.Mover.ClearPendingInput();
            if (localHang.Balance != null) { localHang.Balance.IsSuspended = true; localHang.Balance.SetAirborne(true); }
        }

        void Place(Vector3 feet)
        {
            if (localHang == null || course == null) return;
            placing = true;
            try { localHang.Player.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(course.Forward)); }
            finally { placing = false; }
        }

        void OnLocalTeleported()
        {
            if (!placing) ReleaseLocal(false);
        }

        /// <summary>몸을 놓는다. <paramref name="onRope"/>면 줄 위 그 지점으로 올리고 균형을 처음부터 시작한다.</summary>
        void ReleaseLocal(bool onRope, Vector3 rope = default)
        {
            var held = localHang;
            localHang = null;
            if (held == null) return;
            if (held.Balance != null) held.Balance.IsSuspended = false;
            if (held.Mover == null) return;
            var mover = held.Mover;
            mover.Teleported -= OnLocalTeleported;
            // 매달림은 목마가 끊긴 뒤라 줄 위 단독 상태로 돌아간다(목마 위층의 추종 값은 되살리지 않는다).
            if (mover.Body != null) mover.Body.enabled = true;
            mover.ClearPendingInput();
            mover.Simulated = true;
            if (!onRope) { mover.ResetExternalMotion(true); return; }
            held.Balance?.ResetBalance();
            mover.Relocate(rope, Quaternion.LookRotation(course.Forward));
        }

        /// <summary>호스트가 끌어올리기 완료를 알렸다.</summary>
        void ApplyRescued(ulong id, int lane, float distance)
        {
            book.Remove(id);
            UpdateRescuerBalance();
            var local = NetworkPlayer.Local;
            if (local == null || local.OwnerClientId != id || course == null) return;
            if (localHang == null && local.TryGetComponent(out PlayerMover mover) && mover.Body != null)
                BindLocal(local, mover, local.GetComponent<PlayerBalance>());
            ReleaseLocal(true, course.GetLanePosition(lane, distance));
        }

        // ── 입력 ─────────────────────────────────────────────

        /// <summary>
        /// 외줄 조작 규칙이 가장 먼저 묻는다. 매달린 사람은 모든 조작을 막는다. 끌어올리는 사람은 F 길게로만 취소하고 그 밖의 조작을 막는다.
        /// 근처에 매달린 동료가 있으면 F 짧게로 끌어올리기를 시작한다. 처리했으면 true.
        /// </summary>
        public bool TryHandleInput(PlayerControlContext context, in PlayerInputFrame input)
        {
            var local = NetworkPlayer.Local;
            if (!Active || local == null || context.Mover == null || context.Mover.gameObject != local.gameObject) return false;
            if (localHang != null || book.IsHanging(local.OwnerClientId)) { context.Mover.ClearPendingInput(); return true; }
            if (book.IsRescuing(local.OwnerClientId))
            {
                if (input.InteractLongPressed) Send(Request.Cancel);
                context.Mover.ClearPendingInput();
                return true;
            }
            if (!input.InteractTapped || input.ActionPressed) return false;
            var target = NearestRescueTarget(local);
            if (target == null) return false;
            context.Mover.ClearPendingInput();
            Send(Request.Rescue, target.Id);
            return true;
        }

        /// <summary>내 캐릭터가 끌어올릴 수 있는 가장 가까운 매달린 동료. 없으면 null.</summary>
        public TightropeHangBook.Hang NearestRescueTarget(NetworkPlayer local)
        {
            if (local == null || local.State != PlayerState.Normal || book.IsHanging(local.OwnerClientId) || localHang != null) return null;
            TightropeHangBook.Hang best = null;
            float nearest = Rules.RescueDistance;
            foreach (var hang in book.All)
            {
                if (hang.IsPulling || hang.Id == local.OwnerClientId || !WithinRescueReach(local, hang, Rules.RescueDistance)) continue;
                float distance = Horizontal(local.transform.position, GripPoint(hang));
                if (distance <= nearest) { nearest = distance; best = hang; }
            }
            return best;
        }

        bool WithinRescueReach(NetworkPlayer rescuer, TightropeHangBook.Hang hang, float reach)
        {
            // 그네·리프트 탑승은 외줄 추락 구조와 별개다. 탑승 중에는 구조를 시작하거나 계속할 수 없다.
            if (TightropeSwingPole.Current != null && TightropeSwingPole.Current.IsCrossing(rescuer)
                || TightropeTrapeze.Current != null && TightropeTrapeze.Current.IsCrossing(rescuer)
                || TightropeRelayGate.Current != null && TightropeRelayGate.Current.IsHeld(rescuer)) return false;
            Vector3 grip = GripPoint(hang);
            // 아래로 떨어지는 사람이나 매달린 높이에서는 동료를 끌어올릴 수 없다.
            if (rescuer.TryGetComponent(out PlayerMover mover) && mover.IsAirborne) return false;
            return Mathf.Abs(rescuer.transform.position.y - grip.y) <= Mathf.Max(course.Shape.RopeHeightTolerance, Rules.RescueHeightTolerance)
                && Horizontal(rescuer.transform.position, grip) <= reach;
        }

        static float Horizontal(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

        static NetworkPlayer Find(ulong id)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == id) return player;
            return null;
        }
    }
}
