using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using DG.Tweening;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 목마 연결·해제(006 1단계). 내 캐릭터의 F 짧게(<see cref="PlayerInteraction.InteractRequested"/>)·F 길게(<see cref="PlayerInteraction.ReleaseRequested"/>)를
    /// 호스트에 요청하고, 호스트가 판정해 연결 상태(누가 누구 위에 있는지)를 모든 화면에 보낸다.
    /// 연결: 올라가는 사람은 단독(위아래 아무도 없음)이고, 가장 가까운 동료(목마면 맨 아래 사람 기준 거리)가 세팅 연결 거리 안이면 그 목마의 맨 위에 올라간다.
    /// 해제: 내 아래에 사람이 있고 내 위에 사람이 없을 때 본인만 내려온다.
    /// 연쇄 추락(Harness/Project/Decisions/tightrope-balance.md 2026-10-04): 목마에 속한 사람이 추락하면 호스트가 그 위 전원을 추락시키고(떨어짐 대상),
    /// 그 사람과 위쪽 연결을 모두 끊고, 바로 아래 사람에게 반동 충격 1회를 준다. 대상의 소유자 화면이 균형 추락(래그돌)·충격을 적용한다.
    /// 각 층의 균형에 전체 인원과 위층 균형값 합을 넣는다. 게이지는 각자 계산하며 남의 균형값은 기존 007 공유 값을 읽는다.
    /// 네트워크 오브젝트가 아니므로 NGO 이름 붙은 메시지로 주고받는다(<see cref="CourseShapeSync"/>와 같은 방식). 씬에 하나 둔다.
    /// </summary>
    [DefaultExecutionOrder(-100)] // PlayerBalance.Update 전에 목마 배율·위층 합계를 넣는다
    public sealed class PiggybackSystem : MonoBehaviour
    {
        const string RequestMessage = "CurtainCall.Tightrope.PiggybackRequest";
        const string LinksMessage = "CurtainCall.Tightrope.PiggybackLinks";
        const string ResultMessage = "CurtainCall.Tightrope.PiggybackResult";

        enum Request : byte { Interact, Release, Sync, LaneLanding }

        const string LaneLinkedResult = "옆줄 착지: 목마 탑승";

        [Tooltip("테스트 표시: 화면 왼쪽 아래에 목마 연결 상태와 마지막 결과를 쓴다.")]
        [SerializeField] bool showDebug = true;

        /// <summary>씬의 목마 기능. 없으면 null.</summary>
        public static PiggybackSystem Current { get; private set; }

        /// <summary>연결 상태가 바뀌었을 때(모든 화면, 호스트가 보낸 값 기준).</summary>
        public event Action LinksChanged;

        /// <summary>내 요청이 거부·처리됐을 때 결과 문구(내 화면만).</summary>
        public event Action<string> LocalResult;

        // 위 사람 clientId → 바로 아래 사람 clientId
        readonly Dictionary<ulong, ulong> belowOf = new();
        readonly Dictionary<ulong, ulong> aboveOf = new();

        NetworkSessionManager session;
        CustomMessagingManager messaging; // 핸들러를 등록한 곳(접속마다 새로 생긴다)
        PlayerInteraction interaction;
        string lastResult = "-";
        readonly HashSet<ulong> lastDropped = new(); // 마지막 연결 변경 때 연쇄로 떨어진 사람들
        Sequence handholdTween;
        float handholdAlpha;
        GUIStyle handholdStyle;

        /// <summary>손잡기 착지 표시(내 화면만). 새 착지·재시작·사망에는 이전 연출을 정리한다.</summary>
        public void ShowHandholdLanding()
        {
            ClearHandhold();
            handholdTween = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => handholdAlpha, value => handholdAlpha = value, 1f, 0.2f))
                .AppendInterval(1.2f)
                .Append(DOTween.To(() => handholdAlpha, value => handholdAlpha = value, 0f, 0.4f))
                .OnComplete(() => handholdTween = null);
        }

        void ClearHandhold()
        {
            handholdTween?.Kill();
            handholdTween = null;
            handholdAlpha = 0f;
        }

        /// <summary>옆줄 착지의 겹친 동료가 합체 가능한지. 최종 연결은 호스트가 다시 확인한다.</summary>
        public bool CanLaneLink(NetworkPlayer climber, NetworkPlayer target, bool predictTopRelease = false)
        {
            if (climber == null || target == null || climber == target) return false;
            if (IsInStack(climber) && (!predictTopRelease || GetAbove(climber) != null
                || BottomOf(climber.OwnerClientId) == BottomOf(target.OwnerClientId))) return false;
            if (climber.State != PlayerState.Normal || target.State != PlayerState.Normal) return false;
            for (var member = FindPlayer(BottomOf(target.OwnerClientId)); member != null; member = GetAbove(member))
                if (member.State != PlayerState.Normal || member.IsAirborne) return false;
            return true;
        }

        /// <summary>옆줄 착지 순간 지정한 동료의 목마에 합류 요청. F 연결의 근처 탐색과 구분한다.</summary>
        public void RequestLaneLanding(NetworkPlayer target)
        {
            var local = NetworkPlayer.Local;
            if (session == null || !CanLaneLink(local, target)) return;
            Vector3 landing = local.transform.position;
            if (session.IsHost)
            {
                Reply(LocalClientId, ServerTryLaneLink(LocalClientId, target.OwnerClientId, landing));
                return;
            }
            var manager = Messaging;
            if (manager == null) return;
            using var writer = new FastBufferWriter(21, Allocator.Temp);
            writer.WriteValueSafe((byte)Request.LaneLanding);
            writer.WriteValueSafe(target.OwnerClientId);
            writer.WriteValueSafe(landing);
            manager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        string ServerTryLaneLink(ulong climberId, ulong targetId, Vector3 landing)
        {
            var climber = FindPlayer(climberId);
            var target = FindPlayer(targetId);
            if (!CanLaneLink(climber, target)) return "거부: 옆줄 착지 대상이 합체할 수 없는 상태";
            if (!float.IsFinite(landing.x) || !float.IsFinite(landing.y) || !float.IsFinite(landing.z)) return "거부: 착지 위치가 올바르지 않음";
            if (!climber.TryGetComponent(out PlayerMover climberMover) || !target.TryGetComponent(out PlayerMover targetMover)) return "거부: 몸통을 찾지 못함";
            Vector3 offset = target.transform.position - landing;
            float height = Mathf.Abs(offset.y);
            offset.y = 0f;
            float reach = climberMover.BodyRadius + targetMover.BodyRadius + GameSettings.Base.Character.PlayerGap;
            var course = TightropeCourse.Current;
            if (course == null || !course.TryGetRopePoint(landing, out int lane, out _) || !course.TryGetRopePoint(target.transform.position, out int targetLane, out _) || lane != targetLane
                || height >= Mathf.Max(climberMover.BodyHeight, targetMover.BodyHeight) || offset.sqrMagnitude > reach * reach
                || Vector3.Distance(climber.transform.position, landing) > GameSettings.Tightrope.Piggyback.LinkDistance)
                return "거부: 옆줄 착지 대상이 겹친 위치에서 벗어남";
            ulong top = TopOf(targetId);
            belowOf[climberId] = top;
            aboveOf[top] = climberId;
            ServerBroadcast();
            return LaneLinkedResult;
        }

        void Awake() => Current = this;

        void OnEnable()
        {
            NetworkPlayer.LocalSpawned += Bind;
            NetworkPlayer.LocalDespawned += Unbind;
            NetworkPlayer.Restarted += HandleRestarted;
            if (NetworkPlayer.Local != null) Bind(NetworkPlayer.Local);
        }

        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= Bind;
            NetworkPlayer.LocalDespawned -= Unbind;
            NetworkPlayer.Restarted -= HandleRestarted;
            Unbind();
            ClearHandhold();
            ResetStackBalance();
            Unregister();
            if (session != null)
            {
                session.ConnectionStateChanged -= HandleConnectionState;
                session = null;
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            var local = NetworkPlayer.Local;
            if (local == null || local.State != PlayerState.Normal || (local.TryGetComponent(out PlayerBalance balance) && balance.HasFallen)) ClearHandhold();

            // 세션 매니저는 접속 UI가 나중에 만들 수 있으므로 생길 때 붙는다
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += HandleConnectionState;
                HandleConnectionState(session.ConnectionState);
            }

            if (session != null && session.IsHost)
            {
                ServerResolveFalls();
                ServerPruneMissing();
            }
            UpdateStackBalance();
        }

        /// <summary>계산 전 위층 값을 읽어 넣는다. 단독·사망한 사람에게는 이전 목마 영향을 남기지 않는다.</summary>
        void UpdateStackBalance()
        {
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.TryGetComponent(out PlayerBalance balance)) continue;
                int size = 1;
                float sum = 0f;
                if (player.State == PlayerState.Normal && !balance.HasFallen)
                {
                    size = GetStackSize(player);
                    for (var upper = GetAbove(player); upper != null; upper = GetAbove(upper))
                        if (upper.State == PlayerState.Normal && upper.TryGetComponent(out PlayerBalance upperBalance)
                            && upperBalance.IsActive && !upperBalance.HasFallen)
                            sum += upperBalance.Value;
                }
                balance.SetStackSize(size);
                balance.SetUpperBalanceSum(sum);
            }
        }

        void ResetStackBalance()
        {
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.TryGetComponent(out PlayerBalance balance)) continue;
                balance.SetStackSize(1);
                balance.SetUpperBalanceSum(0f);
            }
        }

        // ── 조회 (모든 화면) ─────────────────────────────

        /// <summary>바로 아래 사람. 없으면 null.</summary>
        public NetworkPlayer GetBelow(NetworkPlayer player) =>
            player != null && belowOf.TryGetValue(player.OwnerClientId, out ulong id) ? FindPlayer(id) : null;

        /// <summary>바로 위 사람. 없으면 null.</summary>
        public NetworkPlayer GetAbove(NetworkPlayer player) =>
            player != null && aboveOf.TryGetValue(player.OwnerClientId, out ulong id) ? FindPlayer(id) : null;

        /// <summary>목마에 속해 있는지(위나 아래에 누가 있음).</summary>
        public bool IsInStack(NetworkPlayer player) =>
            player != null && IsInStack(player.OwnerClientId);

        /// <summary>내가 속한 목마의 전체 인원. 단독이면 1.</summary>
        public int GetStackSize(NetworkPlayer player)
        {
            if (player == null) return 1;
            ulong bottom = BottomOf(player.OwnerClientId);
            int size = 1;
            for (ulong id = bottom; aboveOf.TryGetValue(id, out ulong up); id = up) size++;
            return size;
        }

        /// <summary>몇 번째 층인지(맨 아래 1). 단독이면 1.</summary>
        public int GetFloor(NetworkPlayer player)
        {
            if (player == null) return 1;
            int floor = 1;
            for (ulong id = player.OwnerClientId; belowOf.TryGetValue(id, out ulong down); id = down) floor++;
            return floor;
        }

        /// <summary>두 사람이 같은 목마에 속해 있는지(위아래 어느 쪽이든). 같은 사람이거나 둘 중 하나가 단독이면 false.</summary>
        public bool IsSameStack(NetworkPlayer a, NetworkPlayer b)
        {
            if (a == null || b == null || a == b) return false;
            ulong idA = a.OwnerClientId, idB = b.OwnerClientId;
            if (!IsInStack(idA) || !IsInStack(idB)) return false;
            return BottomOf(idA) == BottomOf(idB);
        }

        /// <summary>마지막 연결 변경이 이 사람을 연쇄 추락시킨 것인지(내려놓기 대신 추락 처리).</summary>
        public bool WasDropped(NetworkPlayer player) =>
            player != null && lastDropped.Contains(player.OwnerClientId);

        bool IsInStack(ulong id) => belowOf.ContainsKey(id) || aboveOf.ContainsKey(id);

        ulong BottomOf(ulong id)
        {
            while (belowOf.TryGetValue(id, out ulong down)) id = down;
            return id;
        }

        ulong TopOf(ulong id)
        {
            while (aboveOf.TryGetValue(id, out ulong up)) id = up;
            return id;
        }

        static NetworkPlayer FindPlayer(ulong clientId)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.OwnerClientId == clientId) return player;
            return null;
        }

        // ── 내 입력 ─────────────────────────────────────

        void Bind(NetworkPlayer local)
        {
            Unbind();
            interaction = local.GetComponent<PlayerInteraction>();
            if (interaction == null) return;
            interaction.InteractRequested += HandleInteract;
            interaction.ReleaseRequested += HandleRelease;
        }

        void Unbind()
        {
            if (interaction != null)
            {
                interaction.InteractRequested -= HandleInteract;
                interaction.ReleaseRequested -= HandleRelease;
            }
            interaction = null;
        }

        /// <summary>내 캐릭터의 목마 해제를 호스트에 요청한다(F 길게와 같은 판정). 예: 맨 위 앞·뒤 분리 점프.</summary>
        public void RequestLocalRelease() => SendRequest(Request.Release);

        void HandleInteract() => SendRequest(Request.Interact);

        void HandleRelease() => SendRequest(Request.Release);

        void SendRequest(Request request)
        {
            if (session == null) return;
            if (session.IsHost)
            {
                ServerHandle(LocalClientId, request);
                return;
            }
            var manager = Messaging;
            if (manager == null) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe((byte)request);
            manager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer);
        }

        // ── 호스트 판정 ─────────────────────────────────

        void HandleRequestMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte request);
            if ((Request)request == Request.LaneLanding)
            {
                reader.ReadValueSafe(out ulong target);
                reader.ReadValueSafe(out Vector3 landing);
                Reply(senderId, ServerTryLaneLink(senderId, target, landing));
                return;
            }
            ServerHandle(senderId, (Request)request);
        }

        void ServerHandle(ulong senderId, Request request)
        {
            switch (request)
            {
                case Request.Interact:
                    Reply(senderId, ServerTryLink(senderId));
                    break;
                case Request.Release:
                    Reply(senderId, ServerTryUnlink(senderId));
                    break;
                case Request.Sync:
                    SendLinks(senderId);
                    break;
            }
        }

        string ServerTryLink(ulong climberId)
        {
            var climber = FindPlayer(climberId);
            if (climber == null) return "거부: 내 캐릭터를 찾지 못함";
            if (climber.State != PlayerState.Normal) return "거부: 내 상태가 정상이 아님";
            if (climber.IsAirborne) return "거부: 공중에서는 올라갈 수 없음";
            if (IsInStack(climberId)) return "거부: 이미 목마에 속해 있음(단독만 올라감)";

            float reach = GameSettings.Tightrope.Piggyback.LinkDistance;
            Vector3 from = climber.transform.position;
            ulong target = 0;
            float best = float.MaxValue;
            foreach (var other in NetworkPlayer.All)
            {
                if (other == null || other == climber || other.State != PlayerState.Normal) continue;
                // 목마는 맨 아래 사람 기준 거리로만 본다(같은 목마를 한 번만)
                if (belowOf.ContainsKey(other.OwnerClientId)) continue;
                Vector3 offset = other.transform.position - from;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > reach || distance >= best) continue;
                best = distance;
                target = other.OwnerClientId;
            }

            if (best == float.MaxValue) return $"거부: {reach:0.##}m 안에 올라갈 동료가 없음";

            ulong top = TopOf(target);
            belowOf[climberId] = top;
            aboveOf[top] = climberId;
            ServerBroadcast();
            return $"연결: {top}번 위로 올라감";
        }

        string ServerTryUnlink(ulong riderId)
        {
            if (!belowOf.TryGetValue(riderId, out ulong below)) return "거부: 내 아래에 아무도 없음";
            if (aboveOf.ContainsKey(riderId)) return "거부: 내 위에 사람이 있어 내려올 수 없음";
            belowOf.Remove(riderId);
            aboveOf.Remove(below);
            ServerBroadcast();
            return "해제: 내려옴";
        }

        /// <summary>호스트: 나간 사람이 낀 연결을 끊는다(위에 있던 사람들은 각각 아래와 떨어진다).</summary>
        void ServerPruneMissing()
        {
            List<ulong> missing = null;
            foreach (var pair in belowOf)
            {
                if (FindPlayer(pair.Key) == null || FindPlayer(pair.Value) == null)
                    (missing ??= new List<ulong>()).Add(pair.Key);
            }
            if (missing == null) return;
            foreach (ulong upper in missing)
            {
                if (!belowOf.TryGetValue(upper, out ulong lower)) continue;
                belowOf.Remove(upper);
                aboveOf.Remove(lower);
            }
            ServerBroadcast();
        }

        /// <summary>
        /// 호스트: 목마에 속한 사람이 추락하면 그 위 전원을 추락시키고, 그 사람부터 위쪽 연결을 모두 끊고, 바로 아래 사람에게 반동을 준다.
        /// 맨 위가 떨어지면 그 사람만 빠지고, 중간층이면 그 위까지 떨어진다. 아래층은 목마를 유지한다.
        /// </summary>
        void ServerResolveFalls()
        {
            if (belowOf.Count == 0) return;
            List<ulong> dropped = null, shocked = null;
            bool changed = false;
            var members = new List<ulong>(belowOf.Keys);
            members.AddRange(aboveOf.Keys);
            foreach (ulong id in members)
            {
                if (!IsInStack(id)) continue; // 이번에 이미 끊긴 사람
                var player = FindPlayer(id);
                if (player == null || player.State != PlayerState.Fallen) continue;
                changed = true;

                for (ulong current = id; aboveOf.TryGetValue(current, out ulong up); current = up)
                {
                    var upper = FindPlayer(up);
                    if (upper != null && upper.State == PlayerState.Normal) upper.ServerSetState(PlayerState.Fallen);
                    (dropped ??= new List<ulong>()).Add(up);
                }

                if (belowOf.Remove(id, out ulong below))
                {
                    aboveOf.Remove(below);
                    var lower = FindPlayer(below);
                    if (lower != null && lower.State == PlayerState.Normal) (shocked ??= new List<ulong>()).Add(below);
                }
                for (ulong current = id; aboveOf.Remove(current, out ulong up); current = up)
                    belowOf.Remove(up);
            }

            if (!changed) return;
            ServerBroadcast(dropped, shocked);
        }

        void HandleRestarted(NetworkPlayer player)
        {
            ClearHandhold();
            // 모두 재시작이면 목마도 모두 푼다(호스트가 정해 보낸다)
            if (session == null || !session.IsHost || belowOf.Count == 0) return;
            belowOf.Clear();
            aboveOf.Clear();
            ServerBroadcast();
        }

        /// <summary>호스트: 연결 상태를 모두에게 보내고 이 화면에도 적용한다. 연쇄 추락·반동 대상이 있으면 함께 보낸다.</summary>
        void ServerBroadcast(List<ulong> dropped = null, List<ulong> shocked = null)
        {
            float shock = GameSettings.Tightrope.Piggyback.FallShock;
            var manager = Messaging;
            if (manager != null)
            {
                using var writer = WriteLinks(dropped, shocked, shock);
                manager.SendNamedMessageToAll(LinksMessage, writer, NetworkDelivery.ReliableSequenced);
            }
            ApplyEvents(dropped, shocked, shock);
        }

        void SendLinks(ulong clientId)
        {
            var manager = Messaging;
            if (manager == null) return;
            using var writer = WriteLinks();
            manager.SendNamedMessage(LinksMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void Reply(ulong clientId, string result)
        {
            var manager = Messaging;
            if (manager != null && clientId != LocalClientId)
            {
                using var writer = new FastBufferWriter(256, Allocator.Temp, 4096);
                writer.WriteValueSafe(result);
                manager.SendNamedMessage(ResultMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
                Debug.Log($"[Piggyback] {clientId}번 요청: {result}");
                return;
            }
            ShowResult(result);
        }

        FastBufferWriter WriteLinks(List<ulong> dropped = null, List<ulong> shocked = null, float shock = 0f)
        {
            int dropCount = dropped?.Count ?? 0;
            int shockCount = shocked?.Count ?? 0;
            var writer = new FastBufferWriter(16 + (belowOf.Count * 2 + dropCount + shockCount) * 8, Allocator.Temp);
            writer.WriteValueSafe(belowOf.Count);
            foreach (var pair in belowOf)
            {
                writer.WriteValueSafe(pair.Key);
                writer.WriteValueSafe(pair.Value);
            }
            writer.WriteValueSafe(dropCount);
            for (int i = 0; i < dropCount; i++) writer.WriteValueSafe(dropped[i]);
            writer.WriteValueSafe(shockCount);
            for (int i = 0; i < shockCount; i++) writer.WriteValueSafe(shocked[i]);
            writer.WriteValueSafe(shock);
            return writer;
        }

        // ── 클라이언트 수신 ─────────────────────────────

        void HandleLinksMessage(ulong senderId, FastBufferReader reader)
        {
            belowOf.Clear();
            aboveOf.Clear();
            reader.ReadValueSafe(out int count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong upper);
                reader.ReadValueSafe(out ulong lower);
                belowOf[upper] = lower;
                aboveOf[lower] = upper;
            }
            var dropped = ReadIds(reader);
            var shocked = ReadIds(reader);
            reader.ReadValueSafe(out float shock);
            ApplyEvents(dropped, shocked, shock);
        }

        static List<ulong> ReadIds(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int count);
            if (count == 0) return null;
            var ids = new List<ulong>(count);
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong id);
                ids.Add(id);
            }
            return ids;
        }

        /// <summary>연쇄 추락·반동을 내 캐릭터에 적용하고 연결 변경을 알린다. 추락은 균형 추락으로 처리해 래그돌·조작 잠금이 평소처럼 따라온다.</summary>
        void ApplyEvents(List<ulong> dropped, List<ulong> shocked, float shock)
        {
            lastDropped.Clear();
            if (dropped != null) lastDropped.UnionWith(dropped);

            var local = NetworkPlayer.Local;
            if (local != null && local.TryGetComponent(out PlayerBalance balance))
            {
                ulong id = local.OwnerClientId;
                if (lastDropped.Contains(id))
                {
                    ShowResult("연쇄 추락: 아래 사람이 떨어짐");
                    balance.ForceFall();
                }
                else if (shocked != null && shocked.Contains(id))
                {
                    ShowResult($"반동: 위 사람이 떨어짐(±{shock:0.#})");
                    balance.ApplyShock(shock);
                }
            }
            NotifyChanged();
        }

        void HandleResultMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string result);
            ShowResult(result);
        }

        void ShowResult(string result)
        {
            lastResult = result;
            if (result == LaneLinkedResult) ShowHandholdLanding();
            Debug.Log($"[Piggyback] {result}");
            LocalResult?.Invoke(result);
        }

        void NotifyChanged()
        {
            UpdateStackBalance();
            LinksChanged?.Invoke();
        }

        // ── 접속 ────────────────────────────────────────

        void HandleConnectionState(SessionConnectionState state)
        {
            Unregister();
            switch (state)
            {
                case SessionConnectionState.Host:
                    Register(RequestMessage, HandleRequestMessage);
                    break;
                case SessionConnectionState.Client:
                    Register(LinksMessage, HandleLinksMessage);
                    Register(ResultMessage, HandleResultMessage);
                    SendRequest(Request.Sync);
                    break;
                case SessionConnectionState.Offline:
                    if (belowOf.Count == 0) break;
                    belowOf.Clear();
                    aboveOf.Clear();
                    NotifyChanged();
                    break;
            }
        }

        ulong LocalClientId =>
            session != null && session.NetworkManager != null ? session.NetworkManager.LocalClientId : NetworkManager.ServerClientId;

        CustomMessagingManager Messaging =>
            session != null && session.NetworkManager != null ? session.NetworkManager.CustomMessagingManager : null;

        void Register(string message, CustomMessagingManager.HandleNamedMessageDelegate handler)
        {
            var manager = Messaging;
            if (manager == null) return;
            messaging = manager;
            messaging.RegisterNamedMessageHandler(message, handler);
        }

        void Unregister()
        {
            if (messaging == null) return;
            messaging.UnregisterNamedMessageHandler(RequestMessage);
            messaging.UnregisterNamedMessageHandler(LinksMessage);
            messaging.UnregisterNamedMessageHandler(ResultMessage);
            messaging = null;
        }

        // ── 테스트 표시 ─────────────────────────────────

        void OnGUI()
        {
            if (handholdAlpha > 0f)
            {
                handholdStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, handholdAlpha);
                GUI.Label(new Rect(0f, Screen.height * 0.65f, Screen.width, 40f), "손 잡아줌", handholdStyle);
                GUI.color = previous;
            }
            if (!showDebug || NetworkPlayer.Local == null) return;
            var local = NetworkPlayer.Local;
            var below = GetBelow(local);
            var above = GetAbove(local);
            string text =
                $"목마  나 {local.OwnerClientId}번 · {GetFloor(local)}층 / {GetStackSize(local)}명\n" +
                $"아래 {(below != null ? below.OwnerClientId + "번" : "-")} · 위 {(above != null ? above.OwnerClientId + "번" : "-")} · 연결 {belowOf.Count}개\n" +
                $"마지막: {lastResult}";
            GUI.Label(new Rect(10f, Screen.height - 70f, 480f, 60f), text);
        }
    }
}
