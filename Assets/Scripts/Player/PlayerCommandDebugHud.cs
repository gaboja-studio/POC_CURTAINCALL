using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Player
{
    /// <summary>
    /// 테스트용 디버그 표시. 지금 입력(공통 역할 값)·조작 규칙·동작 상태를 화면 왼쪽 위에 글자로 보여 준다.
    /// 기능 코드는 이 클래스를 참조하지 않는다. 씬에서 빼도 조작에는 영향이 없다.
    /// 테스트 씬 한정으로 추락 신호 → 조작 잠금, 재시작 키 → 출발 위치·균형 초기화를 대신 연결한다(실제 게임 연결은 외줄 기능).
    /// </summary>
    public class PlayerCommandDebugHud : MonoBehaviour
    {
        [Tooltip("표시할 플레이어. 비워 두면 씬에서 처음 찾은 플레이어를 쓴다.")]
        [SerializeField] PlayerInputReader target;

        [Tooltip("표시를 켜고 끄는 키.")]
        [SerializeField] Key toggleKey = Key.P;

        [Tooltip("균형 시스템을 켜고 끄는 디버그 키(줄에 오르내리는 상황 흉내).")]
        [SerializeField] Key balanceToggleKey = Key.B;

        [Tooltip("추락 후 다시 시작하는 디버그 키(균형 중앙, 추락 해제).")]
        [SerializeField] Key restartKey = Key.R;

        [SerializeField] bool visible = true;

        [SerializeField, Range(1f, 3f)] float scale = 1.5f;

        PlayerMover mover;
        PlayerBalance balance;
        PlayerController controller;
        PlayerInteraction interaction;
        GUIStyle labelStyle;
        float lastJumpCommandTime = -10f;
        float jumpPeak;
        float lateralFromStart;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
                visible = !visible;

            if (target == null)
                target = FindAnyObjectByType<PlayerInputReader>();
            if (target != null && (mover == null || mover.gameObject != target.gameObject))
            {
                Unsubscribe();
                mover = target.GetComponent<PlayerMover>();
                balance = target.GetComponent<PlayerBalance>();
                controller = target.GetComponent<PlayerController>();
                interaction = target.GetComponent<PlayerInteraction>();
                if (balance != null) balance.Fell += OnFell;
            }

            TrackJump();

            if (keyboard == null) return;

            if (balance != null && keyboard[balanceToggleKey].wasPressedThisFrame)
            {
                balance.SetBalanceActive(!balance.IsActive);
                if (mover != null) mover.SetControlEnabled(true);
            }

            if (keyboard[restartKey].wasPressedThisFrame)
            {
                if (balance != null) balance.ResetBalance();
                if (mover != null)
                {
                    mover.ResetToStart();
                    mover.SetControlEnabled(true);
                }
            }
        }

        void TrackJump()
        {
            if (target == null) return;
            if (target.Current.ActionPressed) lastJumpCommandTime = Time.time;
            if (mover == null) return;
            Vector3 fromStart = target.transform.position - startPositionForHud;
            lateralFromStart = Vector3.Dot(fromStart, mover.CourseRight);
            float y = target.transform.position.y;
            if (!mover.IsAirborne) jumpPeak = y;
            else jumpPeak = Mathf.Max(jumpPeak, y);
        }

        Vector3 startPositionForHud;

        void Start()
        {
            if (target == null) target = FindAnyObjectByType<PlayerInputReader>();
            if (target != null) startPositionForHud = target.transform.position;
        }

        /// <summary>길게 누르기 진행을 막대로 그린다. 예: 0.6 → [######....]</summary>
        static string HoldBar(float t)
        {
            int n = Mathf.RoundToInt(Mathf.Clamp01(t) * 10f);
            return "[" + new string('#', n) + new string('.', 10 - n) + "]";
        }

        static string DirText(int d) => d < 0 ? "◀ 왼쪽" : d > 0 ? "오른쪽 ▶" : "-";

        void OnDestroy() => Unsubscribe();

        void OnFell()
        {
            if (mover != null) mover.SetControlEnabled(false);
        }

        void Unsubscribe()
        {
            if (balance != null) balance.Fell -= OnFell;
        }

        void OnGUI()
        {
            if (!visible) return;

            labelStyle ??= new GUIStyle(GUI.skin.label) { richText = true };

            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(10f, 10f, 440f, 540f), GUI.skin.box);

            GUILayout.Label($"<b>Player Input</b>  ({toggleKey} 표시 끄기)", labelStyle);
            if (target == null)
            {
                GUILayout.Label("플레이어 없음 (PlayerInputReader)", labelStyle);
            }
            else
            {
                PlayerInputFrame cmd = target.Current;
                string scheme = controller == null ? "<b>PlayerController 없음</b>" : controller.Scheme != null ? controller.Scheme.name : "없음";
                GUILayout.Label($"Scheme   {scheme}", labelStyle);
                GUILayout.Label($"Move     {Signed(cmd.Move)}  {MoveText(cmd.Move)}", labelStyle);
                GUILayout.Label($"Posture  {Signed(cmd.Posture)}  {Bar(cmd.Posture)}", labelStyle);
                GUILayout.Label($"Action   {(Time.time - lastJumpCommandTime < 0.3f ? "<b>눌림</b>" : "-")}", labelStyle);
                int held = controller != null ? controller.HeldDirection : 0;
                GUILayout.Label($"Aux      {cmd.AuxDirection:+0;-0;0}   방향 지정 {DirText(held)}", labelStyle);
                string request = interaction == null ? "<b>PlayerInteraction 없음</b>"
                    : Time.time - interaction.LastRequestTime < 1f ? $"<b>{interaction.LastRequest} 요청</b>" : "-";
                GUILayout.Label($"Interact {HoldBar(cmd.InteractHoldProgress)}  {request}", labelStyle);

                if (balance != null)
                {
                    GUILayout.Space(6f);
                    if (balance.IsActive)
                    {
                        GUILayout.Label($"Balance  {balance.Value:+0.0;-0.0;0.0}  ({balance.Zone})", labelStyle);
                        GUILayout.Label($"Sway     {balance.CurrentSway:+0.0;-0.0;0.0}/s", labelStyle);
                        GUILayout.Label($"Air      {(balance.IsAirborne ? "<b>공중 (균형 정지)</b>" : "땅")}   Shock {balance.LastShock:+0.0;-0.0;0.0}", labelStyle);
                        if (balance.LaneBoostRemaining > 0f)
                            GUILayout.Label($"LaneBoost ×{balance.LaneBoostMultiplier:0.00}  {balance.LaneBoostRemaining:0.0}s", labelStyle);
                        GUILayout.Label($"Stack    {balance.StackSize}인  ×{balance.StackMultiplier:0.0}   Upper {balance.UpperPush:+0.0;-0.0;0.0}/s", labelStyle);
                        GUILayout.Label($"Red      {balance.RedTime:0.0} / {balance.FallTime:0.0}s" + (balance.HasFallen ? "  <b>추락</b>" : ""), labelStyle);
                    }
                    else
                    {
                        GUILayout.Label("Balance  꺼짐", labelStyle);
                    }
                    GUILayout.Label($"({balanceToggleKey} 균형 켜기/끄기, {restartKey} 재시작)", labelStyle);
                }

                if (mover != null)
                {
                    Vector3 f = mover.CourseForward;
                    Vector3 p = target.transform.position;
                    GUILayout.Space(6f);
                    GUILayout.Label($"Course   ({f.x:0.00}, {f.z:0.00})", labelStyle);
                    GUILayout.Label($"Control  {(mover.ControlEnabled ? "가능" : "<b>잠김</b>")}   {(mover.IsAirborne ? "공중" : "땅")}  최고 Y {jumpPeak:0.00}", labelStyle);
                    string jump = mover.CurrentJump == JumpKind.Lane ? $"<b>옆줄 {DirText(mover.LaneJumpDirection)} (고정)</b>" : mover.CurrentJump.ToString();
                    GUILayout.Label($"Jump     {jump}   옆 이동 {lateralFromStart:+0.00;-0.00;0.00}m", labelStyle);
                    GUILayout.Label($"Position ({p.x:0.0}, {p.y:0.0}, {p.z:0.0})", labelStyle);
                }
            }

            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }

        static string Signed(float v) => v.ToString("+0.00;-0.00; 0.00");

        /// <summary>-1~+1 값을 가운데 기준 막대로 그린다. 예: -1 → [#####|.....]</summary>
        static string Bar(float v)
        {
            const int half = 5;
            int n = Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * half);
            string left = new string('.', half - Mathf.Max(0, -n)) + new string('#', Mathf.Max(0, -n));
            string right = new string('#', Mathf.Max(0, n)) + new string('.', half - Mathf.Max(0, n));
            return $"[{left}|{right}]";
        }

        static string MoveText(float v) => v > 0f ? "앞" : v < 0f ? "뒤" : "-";
    }
}
