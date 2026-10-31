using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Dev
{
    /// <summary>
    /// Dev 모드 전용 테스트 치트 상태와 순간이동 지점 목록(Task-089). 화면은 <see cref="DevCheatPanel"/>.
    /// 순간이동 지점은 각 콘텐츠가 <see cref="RegisterTeleportSource"/>로 붙인다(공용 코드가 콘텐츠 타입을 알 필요 없음).
    /// 모든 값은 이 화면(이 PC)에만 적용되며, 판정이 호스트에 있는 치트는 호스트 화면에서 켜야 다른 플레이어에게도 적용된다.
    /// </summary>
    public static class DevCheats
    {
        public readonly struct TeleportPoint
        {
            public readonly string Label;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public TeleportPoint(string label, Vector3 position, Quaternion rotation)
            { Label = label; Position = position; Rotation = rotation; }
        }

        static readonly List<Action<NetworkPlayer, List<TeleportPoint>>> sources = new();
        static bool suppressFalls;

        /// <summary>
        /// 추락 결과 무시: 균형 붕괴·줄 밖 착지·대포 추락 착지·목마 추락·매달림 낙하로 죽지 않는다(톱·화염 등 직접 피해는 그대로).
        /// 탈출로 개발 씬의 같은 옵션(<c>EscapeDevelopmentSession.FallConsequencesSuppressed</c>) 경로를 그대로 쓴다. Dev 모드가 꺼지면 무시한다.
        /// </summary>
        public static bool SuppressFalls
        {
            get => suppressFalls && DevMode.Enabled;
            set => suppressFalls = value;
        }

        /// <summary>콘텐츠의 순간이동 지점 공급자를 붙인다. 같은 함수는 한 번만 붙는다.</summary>
        public static void RegisterTeleportSource(Action<NetworkPlayer, List<TeleportPoint>> source)
        {
            if (source != null && !sources.Contains(source)) sources.Add(source);
        }

        /// <summary>지금 씬에서 이 플레이어가 갈 수 있는 지점을 모은다. 공급자 오류는 다른 공급자를 막지 않는다.</summary>
        public static void CollectTeleportPoints(NetworkPlayer local, List<TeleportPoint> results)
        {
            results.Clear();
            if (local == null) return;
            if (UnityEngine.Object.FindAnyObjectByType<PlayerSpawnPoints>() != null && local.TryGetComponent(out PlayerMover mover))
            {
                PlayerSpawnPoints.GetPose(Mathf.Max(0, local.Slot), mover.LaneSpacing, out var position, out var rotation);
                results.Add(new TeleportPoint("씬 출발점", position, rotation));
            }
            foreach (var source in sources)
            {
                try { source(local, results); }
                catch (Exception error) { Debug.LogWarning($"[DevCheats] 순간이동 지점 수집 실패: {error.Message}"); }
            }
        }

        /// <summary>내 캐릭터를 옮긴다(재시작 위치는 그대로). 살아 있는 내 캐릭터만. 시선도 지점 방향으로 맞춘다.</summary>
        public static bool Teleport(NetworkPlayer local, Vector3 position, Quaternion rotation, out string reason)
        {
            reason = "";
            if (!DevMode.Enabled) { reason = "Dev 모드를 켜 주세요."; return false; }
            if (local == null || !local.IsSpawned || !local.TryGetComponent(out PlayerMover mover)) { reason = "내 캐릭터가 없습니다."; return false; }
            if (local.State != PlayerState.Normal) { reason = "살아 있을 때만 이동할 수 있습니다. 먼저 되살리세요."; return false; }
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            var facing = forward.sqrMagnitude > .0001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : local.transform.rotation;
            mover.Relocate(position + Vector3.up * .05f, facing);
            if (local.TryGetComponent(out PlayerBalance balance) && balance.IsActive && !balance.HasFallen) balance.ResetBalance();
            if (CurtainCall.Cameras.CameraViewDirector.Current != null) CurtainCall.Cameras.CameraViewDirector.Current.SetLookRotation(facing);
            return true;
        }

        // 도메인 리로드 없이 Play에 들어가도 치트가 꺼진 상태로 시작한다. 공급자는 정적 등록이라 유지한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => suppressFalls = false;
    }
}
