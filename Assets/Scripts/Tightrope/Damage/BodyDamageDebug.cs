using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 테스트 씬 전용 디버그: 내 캐릭터의 팔·다리를 자르고 상태를 화면에 표시한다. 톱날(012) 연결 전 시험용. 실제 씬에는 넣지 않는다.
    /// 온라인이면 호스트에 절단을 요청하고(판정·공유는 호스트), 접속 전이면 이 화면의 첫 플레이어만 바꾼다.
    /// 키는 입력 액션이라 인스펙터에서 바꿀 수 있고, 실행 중에는 키맵 설정 기능이 <see cref="Actions"/>를 다시 지정(리바인딩)할 수 있다.
    /// 기본 키는 맥북에서도 fn 없이 누를 수 있고 게임 조작·다른 디버그 키와 겹치지 않는 숫자 1~5.
    /// </summary>
    public sealed class BodyDamageDebug : MonoBehaviour
    {
        [Tooltip("왼팔 자르기. 기본 1.")]
        [SerializeField] InputAction cutLeftArm = new("왼팔 자르기", InputActionType.Button, "<Keyboard>/1");

        [Tooltip("오른팔 자르기. 기본 2.")]
        [SerializeField] InputAction cutRightArm = new("오른팔 자르기", InputActionType.Button, "<Keyboard>/2");

        [Tooltip("왼다리 자르기. 기본 3.")]
        [SerializeField] InputAction cutLeftLeg = new("왼다리 자르기", InputActionType.Button, "<Keyboard>/3");

        [Tooltip("오른다리 자르기. 기본 4.")]
        [SerializeField] InputAction cutRightLeg = new("오른다리 자르기", InputActionType.Button, "<Keyboard>/4");

        [Tooltip("가로 톱날 순서로 자르기(다리 → 남은 다리 → 팔, 좌우 랜덤). 기본 5.")]
        [SerializeField] InputAction cutSawOrder = new("톱날 순서로 자르기", InputActionType.Button, "<Keyboard>/5");

        [SerializeField] bool visible = true;
        [SerializeField, Min(0.5f)] float scale = 1.5f;

        GUIStyle style;

        /// <summary>이 디버그의 키 액션(왼팔·오른팔·왼다리·오른다리·톱날 순서). 키맵 설정 기능이 다시 지정할 때 쓴다.</summary>
        public InputAction[] Actions => new[] { cutLeftArm, cutRightArm, cutLeftLeg, cutRightLeg, cutSawOrder };

        void OnEnable()
        {
            foreach (var action in Actions) action.Enable();
        }

        void OnDisable()
        {
            foreach (var action in Actions) action.Disable();
        }

        void Update()
        {
            if (cutLeftArm.WasPressedThisFrame()) Cut(BodyPart.LeftArm);
            if (cutRightArm.WasPressedThisFrame()) Cut(BodyPart.RightArm);
            if (cutLeftLeg.WasPressedThisFrame()) Cut(BodyPart.LeftLeg);
            if (cutRightLeg.WasPressedThisFrame()) Cut(BodyPart.RightLeg);
            if (cutSawOrder.WasPressedThisFrame()) Cut(BodyPart.None);
        }

        /// <summary>None이면 가로 톱날 순서.</summary>
        static void Cut(BodyPart part)
        {
            var local = NetworkPlayer.Local;
            if (local != null)
            {
                local.RequestCutPart(part);
                return;
            }

            var condition = FindAnyObjectByType<PlayerCondition>();
            if (condition == null) return;
            BodyPart cut = part == BodyPart.None
                ? PlayerCondition.NextLegThenArmCut(condition.LostParts, Random.value < 0.5f)
                : PlayerCondition.ResolveCut(condition.LostParts, part);
            if (cut != BodyPart.None) condition.LosePart(cut);
        }

        void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };

            var local = NetworkPlayer.Local;
            var condition = local != null ? local.GetComponent<PlayerCondition>() : FindAnyObjectByType<PlayerCondition>();
            string state = local != null ? local.State.ToString() : "오프라인";
            string lost = condition != null ? condition.LostParts.ToString() : "-";
            var effects = condition != null ? condition.GetComponent<BodyDamageEffects>() : null;
            string penalty = "-";
            if (effects != null)
            {
                var p = effects.Current;
                penalty = $"{(effects.IsCrawling ? "기어가기 · " : "")}이동 ×{p.MoveSpeed:0.##} · 흔들림 ×{p.Sway:0.##} · 착지 ×{p.LandingShock:0.##}/옆줄 ×{p.LaneLandingShock:0.##} · 거리 ×{p.Reach:0.##}";
            }

            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(10f, 200f, 460f, 160f));
            GUILayout.Label($"[신체 손상] {Key(cutLeftArm)}/{Key(cutRightArm)} 왼팔/오른팔, {Key(cutLeftLeg)}/{Key(cutRightLeg)} 왼다리/오른다리, {Key(cutSawOrder)} 톱날 순서", style);
            GUILayout.Label($"잃은 부위: {lost} · 상태: {state}", style);
            GUILayout.Label($"불이익(공통+외줄): {penalty}", style);
            GUILayout.EndArea();
            GUI.matrix = matrix;
        }

        /// <summary>지금 지정된 키 이름(다시 지정하면 바뀐 키가 보인다).</summary>
        static string Key(InputAction action) => action.GetBindingDisplayString();
    }
}
