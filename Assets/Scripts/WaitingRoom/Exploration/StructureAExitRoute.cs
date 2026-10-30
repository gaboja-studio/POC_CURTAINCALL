using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 숨은 경로·패드·탈출로 문 연결. 엔딩은 만들지 않는다.
    /// 2026-10-30 사용자 결정: 기계실 숨겨진 문은 앞 배관함 속 키패드 정답으로 열린다(열쇠2 조건 제외).
    /// </summary>
    public sealed class StructureAExitRoute : MonoBehaviour
    {
        public const string Key2 = "structure-a:key-2";
        public const string MachineDoor = "structure-a:machine-hidden-door";
        public const string PasswordPad = "structure-a:escape-pad";
        [SerializeField] BreakableWall restroomBreach;
        [SerializeField] ExplorationGate key2, machineDoor, passwordPad, escapeDoor;
        [Tooltip("명시적으로 정한4자리 정답. 비우면 기본0000을 승인하지 않고 미정으로 둔다. ConfigurePassword API로 지정한 값은 존중.")]
        [SerializeField] string escapePassword = "";

        void Awake() { if (!TryApply(out var error)) Debug.LogError("[Exploration] " + error, this); }
        public bool TryApply(out string error)
        {
            var unique = new HashSet<MonoBehaviour>();
            foreach (var target in new MonoBehaviour[] { restroomBreach, key2, machineDoor, passwordPad, escapeDoor })
                if (target != null && !unique.Add(target))
                { error = "서로 다른 경로에 같은 대상이 지정되었습니다."; return false; }
            if ((key2 != null && !key2.IsOpenGate) || (machineDoor != null && !machineDoor.IsOpenGate)
                || (passwordPad != null && !passwordPad.NeedsPassword) || (escapeDoor != null && !escapeDoor.NeedsPartner))
            { error = "열쇠/일반 문, 비밀번호 패드, 협력 문 타입을 확인하세요."; return false; }
            if (!string.IsNullOrEmpty(escapePassword) && !ExplorationPuzzleRules.IsFourDigits(escapePassword))
            { error = "정답은 숫자4자리여야 합니다."; return false; }
            if (restroomBreach != null && (restroomBreach.RequiredHits != 6 || restroomBreach.ToolId != ItemUses.StakePuller))
            { error = "기존 화장실 벽의 천막 말뚝 뽑개 6타 규칙과 다른 참조입니다."; return false; }

            // 화장실 벽은 기존 ID/진행/균열/충돌/내구도 처리를 그대로 사용한다.
            key2?.ConfigureRoute(Key2); // 획득은 유지. 사용처는 후속 기획.
            if (machineDoor != null)
            {
                machineDoor.ConfigureRoute(MachineDoor, null, "");
                machineDoor.ConfigureOpenedBy(PasswordPad);
            }
            if (passwordPad != null)
            {
                passwordPad.ConfigureRoute(PasswordPad);
                if (!string.IsNullOrEmpty(escapePassword)) passwordPad.ConfigurePassword(escapePassword);
                else if (!passwordPad.PasswordExplicit) passwordPad.ConfigurePasswordPending();
            }
            escapeDoor?.ConfigureRoute(StructureAKeyRoute.EscapeDoor,
                new[] { StructureAKeyRoute.EscapeKey1, StructureAKeyRoute.EscapeKey2, PasswordPad });
            error = ""; return true;
        }
    }
}
