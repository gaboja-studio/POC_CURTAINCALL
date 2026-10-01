using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Player
{
    /// <summary>
    /// 테스트용 디버그 표시. 플레이어가 지금 내리는 명령을 화면 왼쪽 위에 글자로 보여 준다.
    /// 기능 코드는 이 클래스를 참조하지 않는다. 씬에서 빼도 조작에는 영향이 없다.
    /// </summary>
    public class PlayerCommandDebugHud : MonoBehaviour
    {
        [Tooltip("표시할 플레이어. 비워 두면 씬에서 처음 찾은 플레이어를 쓴다.")]
        [SerializeField] PlayerInputReader target;

        [Tooltip("표시를 켜고 끄는 키.")]
        [SerializeField] Key toggleKey = Key.P;

        [SerializeField] bool visible = true;

        [SerializeField, Range(1f, 3f)] float scale = 1.5f;

        PlayerMover mover;
        GUIStyle labelStyle;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
                visible = !visible;

            if (target == null)
                target = FindAnyObjectByType<PlayerInputReader>();
            if (target != null && (mover == null || mover.gameObject != target.gameObject))
                mover = target.GetComponent<PlayerMover>();
        }

        void OnGUI()
        {
            if (!visible) return;

            labelStyle ??= new GUIStyle(GUI.skin.label) { richText = true };

            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(10f, 10f, 360f, 400f), GUI.skin.box);

            GUILayout.Label($"<b>Player Command</b>  ({toggleKey} 표시 끄기)", labelStyle);
            if (target == null)
            {
                GUILayout.Label("플레이어 없음 (PlayerInputReader)", labelStyle);
            }
            else
            {
                PlayerCommand cmd = target.Current;
                GUILayout.Label($"Move     {Signed(cmd.Move)}  {MoveText(cmd.Move)}", labelStyle);
                GUILayout.Label($"Posture  {Signed(cmd.Posture)}  {Bar(cmd.Posture)}", labelStyle);

                if (mover != null)
                {
                    Vector3 f = mover.CourseForward;
                    Vector3 p = target.transform.position;
                    GUILayout.Space(6f);
                    GUILayout.Label($"Course   ({f.x:0.00}, {f.z:0.00})", labelStyle);
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

        static string MoveText(float v) => v > 0f ? "전진" : v < 0f ? "후진" : "정지";
    }
}
