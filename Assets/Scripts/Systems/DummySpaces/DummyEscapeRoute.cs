using CurtainCall.Campaign;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.DummySpaces
{
    /// <summary>하역장·리타이어의 수동 입력과 공통 탈출 결론을 분리한다. 참가자 집계는 하지 않는다.</summary>
    [RequireComponent(typeof(DummySpaceDiagnostics))]
    public sealed class DummyEscapeRoute : MonoBehaviour
    {
        DummySpaceDiagnostics diagnostics;
        readonly string[] rows = new string[4];
        public DummySpaceDiagnostics Diagnostics => diagnostics;
        public int InputRows => GameSettings.EscapeRoute.DummyInputRows;
        public string RowState(int row) => row >= 0 && row < InputRows ? rows[row] ?? "미입력" : "범위 밖";
        void Awake() => diagnostics = GetComponent<DummySpaceDiagnostics>();

        public bool StartEscape() => diagnostics.Invoke("입구 전원 집결을 가정한 수동 신호 → StartEscape", diagnostics.Campaign.StartEscape);

        public bool SetRow(int row, bool arrived)
        {
            if (row < 0 || row >= InputRows || diagnostics.Campaign.State.Phase != CampaignPhase.Escape) return false;
            string value = arrived ? "하역장 도착 입력" : "리타이어 입력";
            if (rows[row] == value) return false;
            rows[row] = value;
            diagnostics.Note("더미 입력", $"행 {row + 1}", $"{value}; 실제 참가자 상태/자동 종료 아님");
            return true;
        }

        public bool Finish(bool anyoneArrived)
        {
            bool applied = diagnostics.Invoke($"수동 결론(도착 있음={anyoneArrived}) → EndEscape", () => diagnostics.Campaign.EndEscape(anyoneArrived));
            if (applied) System.Array.Clear(rows, 0, rows.Length);
            return applied;
        }
    }
}
