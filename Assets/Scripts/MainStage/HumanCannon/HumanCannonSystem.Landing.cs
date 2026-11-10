using System;
using System.Collections.Generic;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSystem
    {
        const string LandingMessage = "CurtainCall.HumanCannon.Landing";
        const int LandingSize = 21; // actor + station + launch + segment + safe
        const string SpringReboundMessage = "CurtainCall.HumanCannon.SpringRebound";
        const int SpringReboundSize = 36; // actor + station + launch + segment + contact + speed
        struct PendingLanding { public int Station; public uint Revision, Segment; public Vector3 Position; }
        readonly Dictionary<ulong, PendingLanding> pendingLandingReports = new();
        readonly List<ulong> pendingLandingActors = new();

        void RetryLandingReports()
        {
            pendingLandingActors.Clear(); pendingLandingActors.AddRange(pendingLandingReports.Keys);
            foreach (var actor in pendingLandingActors)
            {
                var report = pendingLandingReports[actor];
                var player = FindPlayer(actor);
                if (player == null || player.State != PlayerState.Normal || !flying.Matches(actor, report.Station, report.Revision, report.Segment))
                { pendingLandingReports.Remove(actor); continue; }
                if ((player.transform.position - report.Position).sqrMagnitude > 25f) continue;
                pendingLandingReports.Remove(actor);
                HandleLanding(player, report.Station, report.Revision, report.Segment, report.Position);
            }
        }
        /// <summary>M2: 비행 중 일반 바닥·코스 밖으로 떨어지면 잃는 팔다리 수. 잃은 팔다리가 4개가 되면 사망.</summary>
        public const int FallCuts = 2;
        public int LocalSafeLandingCount { get; private set; }

        /// <summary>내 캐릭터가 마지막으로 탄(발사한) 대포 번호. 아직 없으면 -1. 코스 밖으로 떨어지면 이 대포 앞으로 돌아온다.</summary>
        public int LocalLastStation { get; private set; } = -1;

        /// <summary>호스트: 추락으로 다쳤다(접속 번호). 관객 야유 등 관찰용이며 같은 추락은 한 번만 알린다.</summary>
        public static event Action<ulong> ServerFell;

        /// <summary>
        /// 호스트: 추락 부상(M2 부상 규칙). 즉사가 아니라 공통 순서로 <see cref="FallCuts"/>곳을 잃고, 4곳이 되면 공용 규칙이 사망으로 확정한다.
        /// 불꽃 같은 즉사 장애물은 이것을 쓰지 않는다.
        /// </summary>
        public static void NotifyCommonFall(NetworkPlayer player)
        {
            if (player == null || !player.IsServer) return;
            if (ServerFell != null)
                foreach (Action<ulong> observer in ServerFell.GetInvocationList())
                    try { observer(player.OwnerClientId); }
                    catch (Exception error) { Debug.LogException(error); }
        }

        /// <summary>
        /// 호스트: 코스 아래 낭떠러지 추락. 외줄타기 낙사와 같이 공용 사망으로 끝낸다(2026-11-03 담당자 결정).
        /// Dev "추락 무시"·개발 씬 옵션이면 외줄타기와 같이 아무것도 하지 않는다.
        /// </summary>
        public static void ServerFallDeath(NetworkPlayer player)
        {
            if (player == null || !player.IsServer || player.State != PlayerState.Normal
                || (Current != null && !Current.IsPracticeReady(player.OwnerClientId))
                || EscapeDevelopmentSession.FallConsequencesSuppressed(player)) return;
            if (ServerFell != null)
                foreach (Action<ulong> observer in ServerFell.GetInvocationList())
                    try { observer(player.OwnerClientId); }
                    catch (Exception error) { Debug.LogException(error); }
            player.ServerKill(NetworkPlayer.DeathOrigin.MapExit);
        }

        /// <summary>호스트: 스프링 신발이 추락 부상 대신 반동시켰다(접속 번호). 관찰·테스트용.</summary>
        public static event Action<ulong> ServerSpringRebounded;

        /// <summary>
        /// 호스트: 스프링 신발(기물 시스템 31784961, Task-092). 추락 부상이 나는 바닥에 떨어지면 부상 대신 기본 트램펄린과 같은 반동으로 다시 뛴다.
        /// 2026-11-01 사용자: 부상 대신 반동, 쿨타임 중·다 쓴 신발이면 기존 부상. 바닥 없는 낙하·안전 착지면에는 발동하지 않는다(부르는 쪽이 거른다).
        /// 실제 반동이 난 때만 1 소모한다. 같은 접촉은 비행 구간(segment)이 바뀌므로 다시 소모하지 않는다.
        /// </summary>
        bool TrySpringRebound(NetworkPlayer player, int index, uint revision, uint segment, Vector3 contact)
        {
            var carry = PropCarry.Instance;
            ulong actor = player.OwnerClientId;
            if (carry == null || !carry.HasAuthority || player.State != PlayerState.Normal || !carry.EquipmentReady(actor, ItemUses.SpringShoes)) return false;
            float speed = SpringReboundSpeed;
            if (!ApplyRebound(actor, index, revision, segment, contact, speed)) return false;
            carry.UseEquipment(actor, ItemUses.SpringShoes);
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
            {
                if (client == NetworkManager.ServerClientId) continue;
                using var writer = new FastBufferWriter(SpringReboundSize, Allocator.Temp);
                writer.WriteValueSafe(actor);
                writer.WriteValueSafe(index);
                writer.WriteValueSafe(revision);
                writer.WriteValueSafe(segment);
                writer.WriteValueSafe(contact);
                writer.WriteValueSafe(speed);
                messaging.SendNamedMessage(SpringReboundMessage, client, writer, NetworkDelivery.ReliableSequenced);
            }
            NotifyCommonSpring(player);
            return true;
        }

        public static void NotifyCommonSpring(NetworkPlayer player)
        {
            if (player == null || !player.IsServer || ServerSpringRebounded == null) return;
            foreach (Action<ulong> observer in ServerSpringRebounded.GetInvocationList())
                try { observer(player.OwnerClientId); }
                catch (Exception error) { Debug.LogException(error); }
        }

        /// <summary>스프링 신발 반동 속도: 기본 트램펄린 반동 × 기물 효과 배율(초안 1).</summary>
        static float SpringReboundSpeed
        {
            get
            {
                float trampoline = CurtainCall.Settings.GameSettings.Trampoline.UpwardSpeed;
                float scale = ItemCatalog.Get(ItemUses.SpringShoes)?.effect ?? 1f;
                return trampoline * (scale > 0f ? scale : 1f);
            }
        }

        void HandleSpringReboundMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != SpringReboundSize) return;
            reader.ReadValueSafe(out ulong actor);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out uint segment);
            reader.ReadValueSafe(out Vector3 contact);
            reader.ReadValueSafe(out float speed);
            ApplyRebound(actor, index, revision, segment, contact, speed);
        }

        /// <summary>호스트가 현재 비행의 착지를 판정한 뒤 한 번 알린다. Position은 검증된 소유자의 착지 좌표다.</summary>
        public event Action<ulong, int, uint, uint, Vector3, bool> LandingConfirmed;

        void NotifyLanding(ulong actor, int station, uint revision, uint segment, Vector3 position, bool safe)
        {
            if (LandingConfirmed == null) return;
            foreach (Action<ulong, int, uint, uint, Vector3, bool> observer in LandingConfirmed.GetInvocationList())
                try { observer(actor, station, revision, segment, position, safe); }
                catch (Exception error) { Debug.LogException(error, this); }
        }

        void HandleLanding(NetworkPlayer player, int index, uint revision, uint segment, Vector3 position)
        {
            // 현재 좌석 소유권과 무관하게 해당 비행만 한 번 종료한다. R/이전 반동의 늦은 요청은 무시한다.
            if (!flying.Matches(player.OwnerClientId, index, revision, segment)) return;
            if (HumanCannonPoleSystem.Current != null && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0) return;
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) return;
            // A reliable landing request can arrive ahead of the owner's transform. Do not
            // convert that mismatch into an injury: wait for the same flight's position update.
            if ((player.transform.position - position).sqrMagnitude > 25f)
            {
                pendingLandingReports[player.OwnerClientId] = new PendingLanding { Station = index, Revision = revision, Segment = segment, Position = position };
                return;
            }
            pendingLandingReports.Remove(player.OwnerClientId);
            bool safe = IsSafeLandingSurface(player.GetComponent<PlayerMover>(), position);
            if (!safe && position.y < FlightFloorHeight)
            {
                ApplyLanding(player.OwnerClientId, index, revision, segment, false);
                ServerFallDeath(player);
                return;
            }
            float height = player.FallHeightAt(position);
            if (player.CompleteFall(height, position,
                () => safe && TrySpringRebound(player, index, revision, segment, position),
                () => ApplyLanding(player.OwnerClientId, index, revision, segment, safe))) return;
            NotifyLanding(player.OwnerClientId, index, revision, segment, position, safe);
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
            {
                if (client == NetworkManager.ServerClientId) continue;
                using var writer = new FastBufferWriter(LandingSize, Allocator.Temp);
                writer.WriteValueSafe(player.OwnerClientId);
                writer.WriteValueSafe(index);
                writer.WriteValueSafe(revision);
                writer.WriteValueSafe(segment);
                writer.WriteValueSafe((byte)(safe ? 1 : 0));
                messaging.SendNamedMessage(LandingMessage, client, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        internal static bool IsSafeLandingSurface(PlayerMover mover, Vector3 feet)
        {
            var course = TightropeCourse.Current;
            // 좁은 줄의 가장자리에 몸통이 지지되어도 발 중심은 박스 밖일 수 있다.
            // 좌표상 줄 근처라는 이유가 아니라 실제 외줄 조작 전환과 같은 접촉으로 승인한다.
            if (course != null && course.IsTraversalActive
                && course.TryGetSupportingRopeAt(mover, feet, out _)) return true;
            return WalkableSupport.Contains(mover, feet);
        }

        void HandleLandingMessage(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != LandingSize) return;
            reader.ReadValueSafe(out ulong actor);
            reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out uint segment);
            reader.ReadValueSafe(out byte safe);
            if (safe > 1) return;
            ApplyLanding(actor, index, revision, segment, safe == 1);
        }

        void ApplyLanding(ulong actor, int index, uint revision, uint segment, bool safe)
        {
            if (!flying.TryComplete(actor, index, revision, segment)) return;
            if (boundPlayer == null || actor != boundPlayer.OwnerClientId) return;
            CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Mechanism(boundPlayer,this,"LANDING",safe?"SAFE":"UNSAFE",boundPlayer.transform.position);
            LocalLastStation = index;
            if (safe) LocalSafeLandingCount++;
            if (pilot != null) pilot.CompleteLanding(index, revision, segment);
        }
    }
}
