using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Damage;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Survival
{
    /// <summary>
    /// 테스트 씬(Tests/Systems/Survival/InjurySurvival) 전용 임시 화면. 플레이어별 몸 상태·기물 칸 수를 보여 주고,
    /// 호스트에서 절단·추락·잡힘·외팔·부활·복구를 버튼으로 일으킨다. 다른 Task가 보낼 신호(시간대 시작·장소)도 흉내 낸다.
    /// 접속 화면(NetworkSessionTestUI)과 함께 두고, 실제 런 알림으로 확인하려면 CampaignFlowTestUI도 둔다. 공간 연결(CC-111) 뒤 지운다.
    /// </summary>
    public sealed class InjurySurvivalTestUI : MonoBehaviour
    {
        const int MaxLogLines = 8;

        // 접속 화면(10, 10)·런 진행 테스트 화면(360, 10) 오른쪽
        [SerializeField] Rect _panelRect = new Rect(770, 10, 520, 700);

        readonly List<string> _log = new();
        Vector2 _scroll;
        bool _backpack;

        void OnEnable()
        {
            NetworkPlayer.Revived += HandleRevived;
            NetworkPlayer.LocalSpawned += TurnOffBalance;
            if (NetworkPlayer.Local != null) TurnOffBalance(NetworkPlayer.Local);
        }

        void OnDisable()
        {
            NetworkPlayer.Revived -= HandleRevived;
            NetworkPlayer.LocalSpawned -= TurnOffBalance;
        }

        // 공용 기본 조작이 아직 외줄 규칙이라 균형이 켜진 채 서 있으면 흔들려 넘어진다. 균형은 외줄 전용이므로 이 테스트 씬에서는 끈다(공용 기본 이동 규칙은 037)
        static void TurnOffBalance(NetworkPlayer player)
        {
            if (player.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
        }

        void HandleRevived(NetworkPlayer player) => AddLog($"P{player.Slot} 되살아남 · {Describe(player.LostParts)}");

        void AddLog(string line)
        {
            _log.Add($"[{Time.time:0.0}s] {line}");
            if (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        void OnGUI()
        {
            GUILayout.BeginArea(_panelRect, GUI.skin.box);
            GUILayout.Label("<b>부상·생존 테스트</b>", new GUIStyle(GUI.skin.label) { richText = true });

            var local = NetworkPlayer.Local;
            if (local == null)
            {
                GUILayout.Label("접속 후 사용합니다(왼쪽 접속 화면에서 호스트 시작).");
                GUILayout.EndArea();
                return;
            }

            bool host = local.IsServer;
            GUILayout.Label(host ? "호스트: 버튼으로 판정을 일으킵니다." : "클라이언트: 보기만 합니다(판정은 호스트).");

            DrawPlace(host);
            DrawStep(host);
            DrawLocalPenalty(local);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
            foreach (var player in NetworkPlayer.All) DrawPlayer(player, host);
            GUILayout.EndScrollView();

            GUILayout.Label("기록");
            foreach (var line in _log) GUILayout.Label(line);
            GUILayout.EndArea();
        }

        void DrawPlace(bool host)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"장소: {PlaceName(SurvivalRules.ServerPlace)}", GUILayout.Width(150));
            GUI.enabled = host;
            if (GUILayout.Button("공연")) SetPlace(SurvivalPlace.Performance);
            if (GUILayout.Button("탐색")) SetPlace(SurvivalPlace.Exploration);
            if (GUILayout.Button("탈출로")) SetPlace(SurvivalPlace.EscapeRoute);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void SetPlace(SurvivalPlace place)
        {
            SurvivalRules.ServerPlace = place;
            AddLog($"장소 → {PlaceName(place)}");
        }

        // 런 진행(028)이 보낼 시간대 시작 알림을 흉내 낸다. 실제 런이 돌고 있으면 그 알림으로도 같은 일이 일어난다
        void DrawStep(bool host)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = host;
            if (GUILayout.Button("쉬는 시간 시작")) SimulateStep(CampaignPhase.RestTime, 1);
            if (GUILayout.Button("밤 정비 시작")) SimulateStep(CampaignPhase.Night, 1);
            if (GUILayout.Button("세대 교체")) SimulateStep(CampaignPhase.Intro, 2);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void SimulateStep(CampaignPhase phase, int generation)
        {
            var step = new CampaignSnapshot { Generation = generation, Day = 1, Phase = phase };
            InjuryRevival.ServerApplyStep(step);
            AddLog($"시간대 흉내: {CampaignRun.Describe(step)}");
        }

        void DrawLocalPenalty(NetworkPlayer local)
        {
            if (!local.TryGetComponent(out BodyDamageEffects effects)) return;
            GUILayout.BeginHorizontal();
            string policy = effects.Policy == null ? "없음" : effects.Policy.name;
            GUILayout.Label($"내 패널티: {policy}", GUILayout.Width(250));
            if (GUILayout.Button("탐색 중")) effects.SetPolicy(ExplorationBodyPenalty.Shared);
            if (GUILayout.Button("기본")) effects.SetPolicy(BodyDamageEffects.DefaultPolicyFactory?.Invoke());
            if (GUILayout.Button("없음")) effects.SetPolicy(null);
            GUILayout.EndHorizontal();

            var p = effects.Current;
            local.TryGetComponent(out PlayerMover mover);
            float jump = mover != null ? mover.JumpHeightMultiplier : 1f;
            GUILayout.Label($"{(effects.IsCrawling ? "기어가기 · " : "")}이동 ×{p.MoveSpeed:0.##} · 손 닿는 거리 ×{p.Reach:0.##} · 점프 높이 ×{jump:0.##}"
                            + $" ({GameSettings.Base.Character.JumpHeight * jump:0.##}m)");
            _backpack = GUILayout.Toggle(_backpack, "배낭 있음(기물 칸 계산용)");
        }

        void DrawPlayer(NetworkPlayer player, bool host)
        {
            player.TryGetComponent(out PlayerCondition condition);
            int lost = PlayerCondition.CountLimbs(player.LostParts);
            string me = player.IsLocal ? " (나)" : "";
            GUILayout.Label($"P{player.Slot}{me} · {StateName(player.State)} · 잃은 팔다리 {lost} ({BodyName(lost)}) · {Describe(player.LostParts)}"
                            + $" · 기물 {SurvivalRules.ItemSlots(condition, _backpack)}칸");
            if (!host) return;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("절단 1")) Log(player, "절단 1", Describe(player.ServerCut(1)));
            if (GUILayout.Button("절단 2")) Log(player, "절단 2", Describe(player.ServerCut(2)));
            if (GUILayout.Button("추락")) Log(player, "추락", player.ServerKill() ? StateName(player.State) : "거절");
            if (GUILayout.Button("잡힘")) Log(player, "잡힘", player.ServerCatch().ToString());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("외팔 설정")) { player.ServerSetOneArm(); Log(player, "외팔 설정", Describe(player.LostParts)); }
            if (GUILayout.Button("부활")) Log(player, "부활", player.ServerRevive() ? "외팔 상태" : "사망자가 아님");
            if (GUILayout.Button("복구 1")) Log(player, "신체 복구", Describe(player.ServerRestorePart()));
            if (GUILayout.Button("온전한 몸")) { player.ServerRestoreAll(); Log(player, "온전한 몸", ""); }
            GUILayout.EndHorizontal();
        }

        void Log(NetworkPlayer player, string action, string result) => AddLog($"P{player.Slot} {action}: {result}");

        static string PlaceName(SurvivalPlace place) => place switch
        {
            SurvivalPlace.Performance => "공연",
            SurvivalPlace.Exploration => "탐색(백스테이지)",
            SurvivalPlace.EscapeRoute => "탈출로",
            _ => place.ToString(),
        };

        static string StateName(PlayerState state) => state switch
        {
            PlayerState.Normal => "생존",
            PlayerState.Fallen => "사망",
            PlayerState.Arrived => "도착",
            PlayerState.Retired => "리타이어",
            _ => state.ToString(),
        };

        static string BodyName(int lost) => lost switch
        {
            0 => "온전한 몸",
            PlayerCondition.OneArmLostCount => "외팔",
            4 => "사망",
            _ => "부상",
        };

        static string Describe(BodyPart parts)
        {
            if (parts == BodyPart.None) return "없음";
            var names = new List<string>();
            foreach (var part in PlayerCondition.CutOrder)
                if ((parts & part) != 0) names.Add(PartName(part));
            return string.Join("·", names);
        }

        static string PartName(BodyPart part) => part switch
        {
            BodyPart.LeftLeg => "왼다리",
            BodyPart.RightLeg => "오른다리",
            BodyPart.LeftArm => "왼팔",
            BodyPart.RightArm => "오른팔",
            _ => part.ToString(),
        };
    }
}
