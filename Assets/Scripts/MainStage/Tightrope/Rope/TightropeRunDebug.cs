using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope.Prototype;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 테스트 씬 전용 디버그: 묘기 진행 상태 표시, 내 캐릭터 추락·도착 시키기, 코스 조회 결과 표시(좌우 착지 가능한 줄 표시기).
    /// 실제 씬에는 넣지 않는다.
    /// </summary>
    public sealed class TightropeRunDebug : MonoBehaviour
    {
        [Tooltip("내 캐릭터를 바로 추락시킨다.")]
        [SerializeField] Key fallKey = Key.K;

        [Tooltip("내 캐릭터를 도착 플랫폼으로 옮긴다(도착 판정 확인용).")]
        [SerializeField] Key finishKey = Key.L;

        [Tooltip("화면 표시를 켜고 끈다.")]
        [SerializeField] Key toggleKey = Key.O;

        [Tooltip("내 캐릭터가 착지할 때마다 균형 값·충격·흔들림 배율을 Console에 남긴다(반동 조사용).")]
        [SerializeField] bool logLandings = true;

        [SerializeField] bool visible;
        [SerializeField, Min(0.5f)] float scale = 1.5f;

        [Tooltip("좌우 착지 가능한 줄 위에 띄울 표시기 크기(m).")]
        [SerializeField, Min(0.05f)] float markerSize = 0.25f;

        static readonly Color CanLand = new(0.2f, 0.9f, 0.3f);
        static readonly Color ZoneClosed = new(1f, 0.75f, 0.2f);

        Transform leftMarker, rightMarker;
        GUIStyle style;

        PlayerMover loggedMover;
        PlayerBalance loggedBalance;
        float valueAtTakeoff;
        int landingCount;

        void Update()
        {
            var keyboard = Keyboard.current;
            var player = NetworkPlayer.Local;
            if (keyboard != null && !TightropeInputGate.IsBlocked)
            {
                if (keyboard[toggleKey].wasPressedThisFrame) visible = !visible;
                if (visible && player != null && keyboard[fallKey].wasPressedThisFrame) Fall(player);
                if (visible && player != null && keyboard[finishKey].wasPressedThisFrame) MoveToFinish(player);
            }
            UpdateMarkers(player);
            BindLandingLog(player);
        }

        void OnDisable() => BindLandingLog(null);

        void BindLandingLog(NetworkPlayer player)
        {
            var mover = visible && logLandings && !TightropeInputGate.IsBlocked && player != null ? player.GetComponent<PlayerMover>() : null;
            if (mover == loggedMover) return;
            if (loggedMover != null) loggedMover.AirborneChanged -= LogAirborne;
            loggedMover = mover;
            loggedBalance = mover != null ? mover.GetComponent<PlayerBalance>() : null;
            if (loggedMover != null) loggedMover.AirborneChanged += LogAirborne; // PlayerBalance보다 늦게 등록되므로 충격이 들어간 뒤에 불린다
        }

        /// <summary>뜰 때 값과 착지 직후 값·충격을 남긴다. 착지 알림 때는 CurrentJump가 아직 남아 있다.</summary>
        void LogAirborne(bool airborne)
        {
            if (loggedBalance == null || !loggedBalance.IsActive) return;
            if (airborne)
            {
                valueAtTakeoff = loggedBalance.Value;
                return;
            }
            landingCount++;
            // 공중에서는 균형이 멈추므로, 착지 직후 값 - 뜰 때 값 = 이번 착지 충격(직전과 같은 크기여도 구분된다)
            float delta = loggedBalance.Value - valueAtTakeoff;
            string shock = Mathf.Abs(delta) < 0.05f ? "없음" : delta.ToString("+0.0;-0.0;0");
            Debug.Log($"[TightropeDebug] 착지 #{landingCount} {loggedMover.CurrentJump}: 뜰 때 {valueAtTakeoff:+0.0;-0.0;0} → 착지 후 {loggedBalance.Value:+0.0;-0.0;0} " +
                      $"(충격 {shock}, 흔들림 배율 ×{loggedBalance.LaneBoostMultiplier:0.00} {loggedBalance.LaneBoostRemaining:0.0}s, 목마 ×{loggedBalance.StackMultiplier:0.0})");
        }

        static void Fall(NetworkPlayer player)
        {
            if (player.TryGetComponent(out PlayerBalance balance) && balance.enabled) balance.ForceFall();
            else player.RequestState(PlayerState.Fallen);
        }

        static void MoveToFinish(NetworkPlayer player)
        {
            var course = TightropeCourse.Current;
            if (course == null || !player.TryGetComponent(out PlayerMover mover)) return;

            // 지금 서 있는 줄 자리로 도착한다(그 줄이 도착까지 안 가면 도착까지 가는 줄 중 아무거나)
            int lane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
            if (course.Layout.GetLaneEnd(lane) < course.FinishDistance)
                for (int i = 0; i < course.LaneCount; i++)
                    if (course.Layout.GetLaneEnd(i) >= course.FinishDistance) lane = i;

            // 출발 위치를 잠깐 도착 지점으로 바꿔 순간이동하고(온라인 순간이동 포함), 원래 출발 위치로 되돌린다
            Pose start = course.GetStartPose(player.Slot);
            mover.SetStartPose(course.GetLanePosition(lane, course.FinishDistance + 0.5f), start.rotation, true);
            mover.SetStartPose(start.position, start.rotation, false);
        }

        /// <summary>내 캐릭터 옆에서 옆줄 이동하면 착지할 줄 위에 표시기를 띄운다. 초록 = 지금 이동 가능, 주황 = 줄은 있지만 옆줄 이동 구간 밖.</summary>
        void UpdateMarkers(NetworkPlayer player)
        {
            var course = TightropeCourse.Current;
            bool show = visible && course != null && player != null && player.State == PlayerState.Normal && course.GetDistance(player.transform.position) >= 0f;
            PlaceMarker(ref leftMarker, show, course, player, -1);
            PlaceMarker(ref rightMarker, show, course, player, +1);
        }

        void PlaceMarker(ref Transform marker, bool show, TightropeCourse course, NetworkPlayer player, int direction)
        {
            int lane = show ? course.FindLandingLane(player.transform.position, direction) : TightropeCourse.NoLane;
            if (lane == TightropeCourse.NoLane)
            {
                if (marker != null) marker.gameObject.SetActive(false);
                return;
            }

            if (marker == null) marker = CreateMarker(direction < 0 ? "LandingLeft" : "LandingRight");
            float distance = course.GetDistance(player.transform.position);
            marker.gameObject.SetActive(true);
            marker.position = course.GetLanePosition(lane, distance) + Vector3.up * markerSize;
            marker.localScale = Vector3.one * markerSize;
            marker.GetComponent<Renderer>().material.color = course.IsLaneChangeAllowed(distance) ? CanLand : ZoneClosed;
        }

        Transform CreateMarker(string markerName)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = markerName;
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);
            return marker.transform;
        }

        void OnGUI()
        {
            if (!visible) return;
            var run = TightropeRun.Current;
            var course = TightropeCourse.Current;
            if (run == null || course == null) return;

            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = false };
            style.fontSize = Mathf.RoundToInt(14 * scale);

            float t = run.TimeRemaining;
            string performance = run.IsPerformanceStarted ? $"묘기 시작 후 {run.PerformanceElapsed:0}s" : "묘기 시작 전(누군가 외줄에 오르면 시작)";
            string text = $"묘기: {StateLabel(run)} · 남은 시간 {(int)(t / 60f)}:{(int)(t % 60f):00}\n{performance}\n진행 중 {run.InProgressCount} · 도착 {run.ArrivedCount} · 사망 {run.DeadCount} / {run.ParticipantCount}";
            var player = NetworkPlayer.Local;
            if (player != null)
            {
                Vector3 position = player.transform.position;
                float distance = course.GetDistance(position);
                string lane = course.TryGetRopePoint(position, out int nearest, out _) ? nearest.ToString() : "-";
                string onRope = course.IsOnRope(position, 0f, out _) ? "줄 위" : "줄 밖";
                text += $"\n거리: {distance:0.0} / {course.FinishDistance:0.0}m · 가까운 줄: {lane} ({onRope})" +
                        $"\n옆줄 착지: 왼쪽 {LaneLabel(course.FindLandingLane(position, -1))} · 오른쪽 {LaneLabel(course.FindLandingLane(position, +1))}" +
                        $" · 옆줄 구간 {(course.IsLaneChangeAllowed(distance) ? "안" : "밖")}";
            }
            text += $"\n[{fallKey}] 추락  [{finishKey}] 도착으로  [{toggleKey}] 표시";

            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);
            GUI.Box(new Rect((Screen.width - size.x) * 0.5f, 10f, size.x, size.y), content, style);
        }

        static string StateLabel(TightropeRun run) => run.State switch
        {
            TightropeRunState.Waiting => "대기(게임 시작 전)",
            TightropeRunState.Running => "진행 중",
            TightropeRunState.Failed => $"실패 — {run.RestartRemaining:0.0}초 뒤 재시작",
            _ => $"클리어! {run.ArrivedCount}명 도착",
        };

        static string LaneLabel(int lane) => lane == TightropeCourse.NoLane ? "없음" : $"{lane}번";
    }
}
