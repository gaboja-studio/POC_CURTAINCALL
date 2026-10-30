using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>구조A 조사/힌트의 참조 연결. 문구·암호·좌표·미정 공개 조건은 생성하지 않는다.</summary>
    public sealed class StructureAClueRoute : MonoBehaviour
    {
        public const string Graffiti1 = "structure-a:graffiti-1";
        public const string Graffiti2 = "structure-a:graffiti-2";
        public const string Graffiti4 = "structure-a:graffiti-4";
        public const string OldNotebook = "structure-a:old-notebook";
        public const string PerformanceRecord = "structure-a:performance-record";
        public const string Newspaper = "structure-a:newspaper";
        public const string ConfiscatedNotebook = "structure-a:confiscated-notebook";
        public const string RestroomUv = "structure-a:uv-1";
        public const string PasswordPrefixUv = "structure-a:uv-password-prefix";
        public const string RestroomUvClue = "structure-a:uv-1-clue";
        public const string PasswordPrefixClue = "structure-a:password-prefix-clue";
        public const string Monitor = "structure-a:monitor";
        public const string HiddenGraffiti3 = "structure-a:hidden-graffiti-3";

        [SerializeField] ClueInspectable graffiti1, graffiti2, graffiti4, oldNotebook, performanceRecord, newspaper, confiscatedNotebook;
        [SerializeField] UvMark restroomUv, passwordPrefixUv;
        [SerializeField] ClueInspectable restroomUvClue, passwordPrefixClue;
        [SerializeField] ExplorationGate monitor;
        [SerializeField] ClueInspectable hiddenGraffiti3;
        [Tooltip("숨겨진 낙서3 공개 조건. 비우면 미정으로 숨겨 두며 UV/모니터 등을 임의 지정하지 않는다.")]
        [SerializeField] string hiddenGraffiti3Condition = "";

        ClueInspectable boundHidden;

        void Awake() { if (!TryApply(out var error)) Debug.LogError("[Exploration] " + error, this); }
        void Update() => RefreshHidden();
        public bool TryApply(out string error)
        {
            var unique = new HashSet<MonoBehaviour>();
            foreach (var target in new MonoBehaviour[] { graffiti1, graffiti2, graffiti4, oldNotebook, performanceRecord,
                newspaper, confiscatedNotebook, restroomUv, passwordPrefixUv, restroomUvClue, passwordPrefixClue, monitor, hiddenGraffiti3 })
                if (target != null && !unique.Add(target))
                { error = "서로 다른 단서에 같은 컴포넌트가 지정되었습니다."; return false; }
            if (monitor != null && !monitor.IsMonitor)
            { error = "모니터는 Monitor 대상으로 지정해야 합니다."; return false; }
            if (!ValidUvPair(restroomUv, restroomUvClue) || !ValidUvPair(passwordPrefixUv, passwordPrefixClue))
            { error = "UV 조사 내용의 발견 대상/계층 참조를 확인하세요."; return false; }
            if (hiddenGraffiti3 != null)
            {
                if (transform.IsChildOf(hiddenGraffiti3.transform))
                { error = "숨겨진 낙서가 연결 컴포넌트의 상위 오브젝트일 수 없습니다."; return false; }
                foreach (var target in unique)
                    if (target != hiddenGraffiti3 && target.transform.IsChildOf(hiddenGraffiti3.transform))
                    { error = "숨겨진 낙서의 하위에 다른 경로 대상을 둘 수 없습니다."; return false; }
            }
            // 기존 조사 조건/내용은 그대로 보존한다. 암호 앞뒤 단서를 자동 조합하지 않는다.
            graffiti1?.ConfigureIdentity(Graffiti1); graffiti2?.ConfigureIdentity(Graffiti2); graffiti4?.ConfigureIdentity(Graffiti4);
            oldNotebook?.ConfigureIdentity(OldNotebook); performanceRecord?.ConfigureIdentity(PerformanceRecord);
            newspaper?.ConfigureIdentity(Newspaper); confiscatedNotebook?.ConfigureIdentity(ConfiscatedNotebook);
            ConnectUv(restroomUv, restroomUvClue, RestroomUv, RestroomUvClue);
            ConnectUv(passwordPrefixUv, passwordPrefixClue, PasswordPrefixUv, PasswordPrefixClue);
            monitor?.ConfigureRoute(Monitor);
            if (hiddenGraffiti3 != null)
            {
                if (string.IsNullOrWhiteSpace(hiddenGraffiti3Condition))
                {
                    if (!hiddenGraffiti3.RevealPending && !string.IsNullOrWhiteSpace(hiddenGraffiti3.UnlockTarget))
                        hiddenGraffiti3.ConfigureIdentity(HiddenGraffiti3);
                    else hiddenGraffiti3.ConfigurePendingRoute(HiddenGraffiti3);
                }
                else hiddenGraffiti3.ConfigureRoute(HiddenGraffiti3, hiddenGraffiti3Condition);
            }
            boundHidden = hiddenGraffiti3;
            RefreshHidden();
            error = ""; return true;
        }
        void RefreshHidden()
        {
            if (boundHidden == null) return;
            bool visible = boundHidden.Revealed;
            if (boundHidden.gameObject.activeSelf != visible) boundHidden.gameObject.SetActive(visible);
        }
        static bool ValidUvPair(UvMark source, ClueInspectable clue) => clue == null
            || (source != null && !source.transform.IsChildOf(clue.transform));
        static void ConnectUv(UvMark source, ClueInspectable clue, string sourceId, string clueId)
        {
            if (source == null) return;
            source.ConfigureRoute(sourceId);
            if (clue == null) return;
            clue.ConfigureRoute(clueId, sourceId); source.RevealWhenFound(clue.gameObject);
        }
    }
}
