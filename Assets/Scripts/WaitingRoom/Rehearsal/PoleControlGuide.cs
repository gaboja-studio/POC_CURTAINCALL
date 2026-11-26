using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 그네 조작 안내(2026-11-26 M3 피드백: 그네 해제 방법을 모름, 매달린 사람이 반동을 줘야 하는데 아래 사람이 주는 줄 앎).
    /// 대기실(리허설·연습실)에서 내가 처음 그네를 잡으면 왼쪽 가이드로 한 번 알려 준다. 그네를 직접 잡은 사람과
    /// 동료에게 매달린 사람의 안내를 나누고, 아래 사람이 반동을 줄 수 있는지는 공통 봉 설정(TopRiderOnlySwing)을 따른다.
    /// 런(세대)이 바뀌면 다시 한 번 보여 준다. 문구: 대기실 문구 파일 `guide.pole-*`.
    /// </summary>
    public sealed class PoleControlGuide : MonoBehaviour
    {
        public enum Role { None, Holder, Hanger }

        static PoleControlGuide instance;
        readonly HashSet<Role> shown = new();
        int generation = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(PoleControlGuide));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PoleControlGuide>();
        }

        /// <summary>내가 지금 그네에서 맡은 자리. 잡고 있지 않으면 None.</summary>
        public static Role RoleOf(HumanCannonPoleSystem poles, ulong actor)
        {
            if (poles == null || poles.HeldBy(actor) < 0) return Role.None;
            return poles.IsLower(actor) ? Role.Hanger : Role.Holder;
        }

        /// <summary>자리별 안내(제목, 내용). 아래 사람의 반동 가능 여부는 공통 봉 설정을 따른다.</summary>
        public static (string title, string body) Text(Role role)
        {
            if (role == Role.Holder)
                return (WaitingRoomText.Get("guide.pole-holder.title", "그네 조작"),
                    WaitingRoomText.Get("guide.pole-holder.body", "그네를 잡은 사람이 W/S로 직접 반동을 줍니다 · Space로 흔들리는 방향으로 도약 · F 길게 눌러 그네 놓기"));
            if (role == Role.Hanger)
                return GameSettings.Poles.TopRiderOnlySwing
                    ? (WaitingRoomText.Get("guide.pole-hanger.title", "동료에게 매달리기"),
                        WaitingRoomText.Get("guide.pole-hanger.body", "반동은 그네를 잡은 위 동료가 W/S로 줍니다 · 아래에서는 반동을 줄 수 없어요 · Space로 도약 · F 길게 눌러 놓기"))
                    : (WaitingRoomText.Get("guide.pole-hanger.title", "동료에게 매달리기"),
                        WaitingRoomText.Get("guide.pole-hanger-shared.body", "W/S로 함께 반동을 줄 수 있어요 · Space로 도약 · F 길게 눌러 놓기"));
            return (null, null);
        }

        void Update()
        {
            var local = NetworkPlayer.Local;
            var poles = HumanCannonPoleSystem.Current;
            if (local == null || !local.IsSpawned || poles == null || !WaitingRoomScenes.IsWaitingRoom(SceneManager.GetActiveScene())) return;
            int current = CurtainCall.Campaign.CampaignRun.Instance != null ? CurtainCall.Campaign.CampaignRun.Instance.State.Generation : 0;
            if (current != generation) { generation = current; shown.Clear(); }
            var role = RoleOf(poles, local.OwnerClientId);
            if (role == Role.None || shown.Contains(role)) return;
            var (title, body) = Text(role);
            if (RehearsalGuide.Show(title, body, 10f)) shown.Add(role);
        }
    }
}
