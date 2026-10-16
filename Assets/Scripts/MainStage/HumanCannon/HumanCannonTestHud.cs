using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.HumanCannon
{
    /// <summary>인간 대포 단계별 테스트 씬 조작 안내. 제품 UI가 아니다.</summary>
    public sealed class HumanCannonTestHud : MonoBehaviour
    {
        // 게임 조작 에셋을 바꾸지 않는 테스트 전용 단축키다.
        void OnEnable() => InputGate.AddReleaseKeys(Key.R);

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || InputGate.IsBlocked || !keyboard.rKey.wasPressedThisFrame) return;
            var player = NetworkPlayer.Local;
            if (player == null || !player.IsSpawned || HumanCannonSystem.Current == null) return;
            player.RequestRestartAll();
            InputGate.RequireRelease();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Mathf.Max(10, Screen.width - 480), 10, 470, Mathf.Max(200, Screen.height - 20)), GUI.skin.box);
            GUILayout.Label("인간 대포 / 분리 출발·쉼터·불꽃 테스트");
            GUILayout.Label("대포 앞에서 F 짧게: 탑승 → 다시 F 짧게: 발사");
            GUILayout.Label("탑승 중 F 길게: 탑승 전 위치로 복귀");
            GUILayout.Label("비행·반동 중 WASD: 수평 조절 / 놓으면 보정 종료");
            GUILayout.Label("지상 Space 점프도 반동 / 반동 후 무입력은 수직 이동");
            GUILayout.Label("R: 플레이어·장치 초기화 (방 접속 유지)");
            GUILayout.Label("주황 쿠션: 안전 착지 / 비행 후 일반 바닥: 사망");
            GUILayout.Label("바닥: 포구 0m / 1m 눈금 · 5m 거리 숫자");
            GUILayout.Label("노란 손잡이 F: 정면에 잡기 / WASD: 운반·방향 추종");
            GUILayout.Label("운반 중 Space: 몸과 장치 점프 / F 길게: 놓기");
            var system = HumanCannonSystem.Current;
            var player = NetworkPlayer.Local;
            if (system != null && system.StationCount > 0)
            {
                string seats = "대포: ";
                for (int i = 0; i < system.StationCount; i++)
                    seats += $"{i + 1}:{(system.Occupant(i) == CannonSeatState.Nobody ? "빈자리" : "탑승")} ";
                GUILayout.Label(seats);
                var station = system.Station(0);
                if (player != null && station != null)
                {
                    var muzzle = station.Muzzle;
                    Vector3 forward = Vector3.ProjectOnPlane(muzzle.forward, Vector3.up).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    Vector3 offset = Vector3.ProjectOnPlane(player.transform.position - muzzle.position, Vector3.up);
                    float forwardDistance = Vector3.Dot(offset, forward);
                    float sideDistance = Vector3.Dot(offset, right);
                    GUILayout.Label($"발사점 기준 전방 {forwardDistance:F1} m / 좌우 {sideDistance:+0.0;-0.0;0.0} m");
                }
            }
            if (player != null) GUILayout.Label("내 상태: " + player.State
                + (system != null && system.IsFlying(player.OwnerClientId) ? " / 비행" : "")
                + (system != null ? $" / 안전 착지 {system.LocalSafeLandingCount}회" : ""));
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline != null)
            {
                string carrier = trampoline.Carrier == CannonSeatState.Nobody ? "무주인" :
                    player != null && trampoline.Carrier == player.OwnerClientId ? "내가 운반 중" : "다른 플레이어 운반 중";
                GUILayout.Label($"트램폴린: {carrier} / 내 반동 횟수: {trampoline.LocalBounceCount}");
            }
            var poles = HumanCannonPoleSystem.Current;
            var route = HumanCannonRouteSystem.Current;
            if (route != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("승강기: 트램폴린을 들고 혼자 탑승 → 1.5초 후 자동 출발");
                GUILayout.Label("초록: 출발 대기·이동 중 / 파랑: 출발 조건 미충족·도착 완료");
                GUILayout.Label("모든 리프트는 1인용: 2명 이상이면 양쪽 바닥 개방 / 비우면 복귀");
                GUILayout.Label("첫 출발 아래 안전 바닥 → 왼쪽 계단으로 다시 출발점");
                GUILayout.Label($"쉼터: 생존 비행자 {route.ArrivedFlyers}/{route.AliveFlyers} 합류 / 전원 도착 뒤 자동 상승");
                GUILayout.Label("대기 중 내리거나 장치를 놓으면 출발 취소 / 위에서 F 길게 놓고 F로 교대");
                GUILayout.Label("상승 후: 위 대포 3대·파란 발판으로 다음 구간 출발");
                for (int i = 0; i < route.TransportCount; i++)
                    GUILayout.Label($"이동장치 {i + 1}: {(route.TrapOpen(i) ? "과승 · 바닥 열림" : route.IsBoarding(i) ? $"자동 출발 {route.BoardingRemaining(i):F1}초" : route.TransportState(i).Phase.ToString())}");
                GUILayout.Label($"불꽃 1: {route.FireStatus(0)} / 불꽃 2: {route.FireStatus(1)}");
            }
            if (poles != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("비행 중 봉 아래 F: 잡기 / W·S: 반동");
                GUILayout.Label("봉에서 Space 또는 F 길게: 놓기 도약");
                GUILayout.Label("노랑·검정 봉 유지: 연결 톱 수납 / 놓기·시간 끝·사망: 예고 후 재동작");
                GUILayout.Label("협동: 위 봉 잡기 → 지상 담당 통과 → 너머에서 다시 받기");
                GUILayout.Label("담당 사망: 중앙 재반동 → 공중에서 외곽 손잡이 F로 인계");
                for (int i = 0; i < poles.PoleCount; i++)
                {
                    var state = poles.State(i);
                    var pole = poles.Pole(i);
                    if (pole == null) continue;
                    string label = pole.LinkedGate != null ? "연결 봉" : "일반 봉";
                    string status = state.IsOccupied ? $"유지 {Mathf.Max(0f, (float)(state.HoldUntil - poles.Clock)):F1}초"
                        : state.Phase == PoleSawPhase.Warning ? $"재동작 예고 {Mathf.Max(0f, (float)(state.RearmAt - poles.Clock)):F1}초" : "비어 있음";
                    GUILayout.Label($"{label}: {status}");
                    if (pole.LinkedGate != null) GUILayout.Label(pole.LinkedGate.CarrierPassed
                        ? "관문: 지상 통과 확인 / 비행자도 너머로 이동 가능"
                        : "관문: 지상 담당 통과 대기 / 비행자는 위·옆 우회 불가");
                }
            }
            GUILayout.EndArea();
        }
    }
}
