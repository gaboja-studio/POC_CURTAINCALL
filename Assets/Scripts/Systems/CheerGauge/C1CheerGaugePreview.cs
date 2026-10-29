using CurtainCall.DummySpaces;
using CurtainCall.Reward;
using UnityEngine;

namespace CurtainCall.CheerGauge
{
    /// <summary>전용 확인 씬의 정적 샘플과 실제 오프라인 보상 입력을 분리한다.</summary>
    public sealed class C1CheerGaugePreview : MonoBehaviour
    {
        [SerializeField] C1CheerGaugeView view;
        [SerializeField] C1CheerGaugeBinding binding;
        [SerializeField] Font controlFont;
        DummyPerformance performance;
        bool sampleMode = true;
        string result = "정적 표시 샘플 · 실제 게임 점수가 아닙니다.";
        GUIStyle style;

        public void Configure(C1CheerGaugeView gauge, C1CheerGaugeBinding liveBinding, Font font)
        {
            view = gauge;
            binding = liveBinding;
            controlFont = font;
        }
        void Start() => ShowSample(100);
        public void ShowSample(int cap)
        {
            sampleMode = true;
            binding.enabled = false;
            view.SetScores(cap == 100 ? new long[] { 60, 45, 30, 20 } : new long[] { cap, cap, cap, cap }, cap * 4L, false);
            result = "정적 표시 샘플 · 실제 게임 점수가 아닙니다.";
        }
        public bool BeginLive()
        {
            if (performance == null)
                performance = new GameObject("OfflinePerformanceDriver").AddComponent<DummyPerformance>();
            // 기존 더미 드라이버의 Start 이후 다음 클릭부터 실제 런을 시작한다.
            bool began = performance.StartOfflineRun();
            bool opened = performance.BeginPerformance();
            sampleMode = false;
            binding.enabled = true;
            result = began || opened ? "실제 오프라인 보상 API 연결" : "드라이버 준비 후 한 번 더 눌러 주세요. 진행 중인 런은 초기화하지 않습니다.";
            return opened;
        }
        public bool ChangeScore(int index, bool positive)
        {
            var rewards = PerformanceRewards.Instance;
            if (sampleMode || rewards?.State == null || index < 0 || index >= rewards.State.Segments.Count) return false;
            return rewards.AddCheer(rewards.State.Segments[index].ParticipantId, positive);
        }
        public bool KillFirst()
        {
            var state = PerformanceRewards.Instance?.State;
            return !sampleMode && performance != null && state?.Segments.Count > 0 && performance.Kill(state.Segments[0].ParticipantId);
        }
        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.button) { fontSize = 16, font = controlFont };
            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, font = controlFont, wordWrap = true };
            GUILayout.BeginArea(new Rect(24, Screen.height - 220, Mathf.Min(650, Screen.width - 48), 204), GUI.skin.box);
            GUILayout.Label("C1 호응 게이지 확인 · 테스트 전용", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("미리보기 155", style)) ShowSample(100);
            if (GUILayout.Button("150 상한 · 합계600", style)) ShowSample(150);
            if (GUILayout.Button("200 상한 · 합계800", style)) ShowSample(200);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("빈 링", style)) { binding.enabled = false; sampleMode = true; view.SetScores(new long[4], 400, false); }
            if (GUILayout.Button("추락 강조 샘플", style))
            {
                ShowSample(100);
                view.FlashFall(1);
                view.SetScores(new long[] { 60, 0, 30, 20 }, 400);
            }
            if (GUILayout.Button("실제 오프라인 시작", style)) BeginLive();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = !sampleMode;
            if (GUILayout.Button("단원1 환호", style)) ChangeScore(0, true);
            if (GUILayout.Button("단원2 환호", style)) ChangeScore(1, true);
            if (GUILayout.Button("단원1 야유", style)) ChangeScore(0, false);
            if (GUILayout.Button("단원1 사망", style)) KillFirst();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label(result, labelStyle);
            GUILayout.EndArea();
        }
    }
}
