using System;
using System.Collections.Generic;

namespace CurtainCall.WaitingRoom
{
    /// <summary>대기실 안내 한 항목. 본문의 {interact}·{uv}는 현재 키 이름으로 바뀐다.</summary>
    [Serializable]
    public sealed class TutorialEntry
    {
        public string id, title, body;
        /// <summary>표시 시간(초). 0이면 패널 설정 파일의 기본 표시 시간.</summary>
        public float seconds;
        /// <summary>조사 화면처럼 입력이 막힌 동안에도 띄운다.</summary>
        public bool whileBlocked;

        public TutorialEntry() { }
        public TutorialEntry(string id, string title, string body, float seconds = 0f, bool whileBlocked = false)
        { this.id = id; this.title = title; this.body = body; this.seconds = seconds; this.whileBlocked = whileBlocked; }
    }

    /// <summary>
    /// 대기실 튜토리얼 진행(2026-10-31 사용자 확정 문구, Task-086 guide-copy-draft.md). 개인별·비강제.
    /// 조건이 맞은 항목을 순서대로 줄 세우고, 한 번 띄운 항목은 다시 띄우지 않는다. 저장 문자열은 본 항목 ID 목록이다.
    /// </summary>
    public sealed class WaitingRoomTutorialBook
    {
        public const string InteractToken = "{interact}", UvToken = "{uv}";

        public static IReadOnlyList<TutorialEntry> Defaults { get; } = new[]
        {
            // ① 가이드 쉬는 시간
            new TutorialEntry("G01", "쉬는 시간", "다음 공연까지 대기실을 둘러볼 수 있습니다. 남은 시간은 오른쪽 아래 시계에 보입니다."),
            new TutorialEntry("G02", "깊이 들어가면 시간이 흐릅니다", "로비·숙소 같은 생활 구역에서는 시계가 멈춥니다. 배관·기계실·지하처럼 깊은 곳에 들어가면 시계가 흐릅니다."),
            new TutorialEntry("G03", "살펴보기", "외곽선이 빛나는 물건을 바라보고 [{interact}]를 누르면 조사하거나 집을 수 있습니다."),
            new TutorialEntry("G04", "자세히 보기", "왼쪽 끌기로 돌려 보고 휠로 확대합니다. [{interact}]·ESC로 내려놓습니다. 화면을 보는 동안에도 시간은 흐릅니다.", 0f, true),
            new TutorialEntry("G05", "들고 다니기", "기물은 기본 2칸까지 들 수 있고, 배낭이 있으면 더 들 수 있습니다. [{interact}]를 길게 누르면 마지막에 든 기물을 내려놓습니다."),
            new TutorialEntry("G06a", "머리핀", "잠긴 상자·캐비닛 앞에서 [{interact}]로 따 봅니다. 한 번에 안 열릴 수 있고, 열렸을 때만 닳습니다."),
            new TutorialEntry("G06b", "말뚝 뽑개", "나무 상자나 약해 보이는 벽을 [{interact}]로 내리칩니다. 여러 번 쳐야 부서지는 것도 있습니다."),
            new TutorialEntry("G06c", "UV 등", "[{uv}]을 누르고 있는 동안 켜집니다. 맨눈에 안 보이는 낙서를 비춰 보세요."),
            new TutorialEntry("G07", "단서", "흩어진 단서는 서로 이어집니다. 숫자·그림·열쇠를 모아 잠긴 문과 숨은 길을 열어 보세요."),
            new TutorialEntry("G08", "숨은 공연 장비", "대기실 어딘가에 아직 쓰지 않은 공연 장비가 있습니다. 찾아서 챙기면 새 공연이 열립니다."),
            new TutorialEntry("G09", "상점", "쉬는 시간에는 신체 복구 서비스만 이용할 수 있습니다. 기물 구매는 밤 정비에 열립니다."),
            new TutorialEntry("G10", "신체 복구", "잃은 팔다리는 상점의 신체 복구 서비스로 되찾을 수 있습니다. 예약한 뒤 승강기로 가세요. 비용은 팀 토큰입니다."),
            new TutorialEntry("G11", "무대로 복귀", "종이 울리면 무대 입구 통로로 돌아가세요. 시간이 끝났을 때 통로 밖에 있으면 팔 하나를 잃고 무대로 끌려갑니다. 모두 모이면 바로 공연이 시작됩니다."),
            new TutorialEntry("G11A", "소집 경보", "종이 울렸습니다. 시간이 끝나기 전에 무대 입구 통로로 돌아가세요."),
            new TutorialEntry("G12", "열쇠 구멍", "2개의 열쇠 구멍이 보입니다."),
            // ② 밤 정비
            new TutorialEntry("N01", "밤 정비", "밤에는 시계가 흐르지 않습니다. 대신 전기가 끊겨 일부 구역은 철창으로 닫힙니다."),
            new TutorialEntry("N02", "기물 사기", "상점 수화기로 기물을 살 수 있습니다. 산 물건은 아래 배송구로 떨어집니다. 토큰은 팀이 함께 씁니다."),
            new TutorialEntry("N03", "내일 공연 정하기", "로비 스케줄표에서 내일 공연 순서와 난이도를 정합니다. 호스트가 확정해야 다음 날로 넘어갑니다."),
            new TutorialEntry("N04", "물려줄 선반", "선반 4칸에 올린 기물은 런에 실패해 처음부터 다시 시작해도 남습니다."),
            new TutorialEntry("N05", "잠자리", "준비가 끝나면 숙소의 자기 침대에서 [{interact}]를 누르세요. 모두 누우면 다음 날이 됩니다."),
            // 상황 안내
            new TutorialEntry("S01", "다시 깨어남", "공연에서 쓰러졌지만 팔 하나만 남은 채 돌아왔습니다. 신체 복구로 잃은 팔다리를 되찾을 수 있습니다."),
            new TutorialEntry("S02", "쓰러진 동료", "공연에서 쓰러진 동료는 다음 쉬는 시간에 팔 하나만 남은 채 돌아옵니다."),
            new TutorialEntry("S03", "손이 가득 참", "더 들 수 없습니다. [{interact}]를 길게 눌러 내려놓거나 소품실에 보관하세요."),
            new TutorialEntry("S04", "다 쓴 도구", "도구가 닳아 없어졌습니다. 밤 정비에 상점에서 새로 살 수 있습니다."),
        };

        readonly Dictionary<string, TutorialEntry> entries = new();
        readonly List<string> order = new();
        readonly HashSet<string> done = new();
        readonly List<string> queue = new();

        /// <param name="overrides">문구를 바꾼 항목(같은 ID만 대체). 비면 기본 문구.</param>
        public WaitingRoomTutorialBook(IEnumerable<TutorialEntry> overrides = null)
        {
            foreach (var entry in Defaults) { entries[entry.id] = entry; order.Add(entry.id); }
            if (overrides == null) return;
            foreach (var entry in overrides)
                if (entry != null && !string.IsNullOrEmpty(entry.id) && entries.ContainsKey(entry.id)) entries[entry.id] = entry;
        }

        public int QueuedCount => queue.Count;
        public bool IsDone(string id) => done.Contains(id);
        public bool IsQueued(string id) => queue.Contains(id);
        public TutorialEntry Get(string id) => entries.TryGetValue(id, out var entry) ? entry : null;

        /// <summary>띄울 차례에 올린다. 이미 봤거나 줄에 있거나 없는 ID면 false.</summary>
        public bool Enqueue(string id)
        {
            if (!entries.ContainsKey(id) || done.Contains(id) || queue.Contains(id)) return false;
            queue.Add(id);
            return true;
        }

        /// <summary>다음에 띄울 항목. 입력이 막힌 동안에는 <see cref="TutorialEntry.whileBlocked"/> 항목만 앞질러 꺼낸다.</summary>
        public bool TryNext(bool inputBlocked, out TutorialEntry entry) => TryNext(inputBlocked, null, out entry);

        /// <summary>다음에 띄울 항목 중 <paramref name="allow"/>를 통과한 첫 항목(연계 튜토리얼 중에는 연계 항목만 꺼낸다).</summary>
        public bool TryNext(bool inputBlocked, Predicate<string> allow, out TutorialEntry entry)
        {
            foreach (var id in queue)
            {
                var candidate = entries[id];
                if (inputBlocked && !candidate.whileBlocked) continue;
                if (allow != null && !allow(id)) continue;
                entry = candidate;
                return true;
            }
            entry = null;
            return false;
        }

        /// <summary>띄웠다(또는 더 띄울 필요가 없다). 줄에서 빼고 본 것으로 기록한다.</summary>
        public void MarkDone(string id)
        {
            queue.Remove(id);
            if (entries.ContainsKey(id)) done.Add(id);
        }

        public void ResetAll() { done.Clear(); queue.Clear(); }
        public void ClearQueue() => queue.Clear();

        /// <summary>본 항목을 원래 순서대로(다시 보기 목록).</summary>
        public IEnumerable<TutorialEntry> Seen()
        {
            foreach (var id in order) if (done.Contains(id)) yield return entries[id];
        }

        public string Serialize()
        {
            var ids = new List<string>();
            foreach (var id in order) if (done.Contains(id)) ids.Add(id);
            return string.Join(",", ids);
        }

        /// <summary>저장 문자열을 읽는다. 모르는 ID는 버린다.</summary>
        public void Load(string saved)
        {
            done.Clear();
            if (string.IsNullOrEmpty(saved)) return;
            foreach (var id in saved.Split(','))
                if (entries.ContainsKey(id.Trim())) done.Add(id.Trim());
        }

        /// <summary>
        /// 사람이 고치는 문구 파일(`ID | 제목 | 본문 [| 초]`, 한 줄에 하나, #으로 시작하면 설명)을 읽는다.
        /// 4번째 칸이 숫자면 그 안내만의 표시 시간(1~60초), 없으면 패널 기본. 입력 막힘 예외는 기본값을 따른다.
        /// 형식이 틀린 줄과 모르는 ID는 건너뛴다.
        /// </summary>
        public static List<TutorialEntry> ParseTexts(string text)
        {
            var result = new List<TutorialEntry>();
            if (string.IsNullOrEmpty(text)) return result;
            var defaults = new Dictionary<string, TutorialEntry>();
            foreach (var entry in Defaults) defaults[entry.id] = entry;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(new[] { '|' }, 3);
                if (parts.Length < 3) continue;
                string id = parts[0].Trim(), title = parts[1].Trim(), body = parts[2].Trim();
                if (!defaults.TryGetValue(id, out var basis)) continue;
                float seconds = basis.seconds;
                // 마지막 칸이 숫자면 표시 시간. 숫자가 아니면 본문의 일부로 둔다.
                int bar = body.LastIndexOf('|');
                if (bar >= 0 && WaitingRoomTutorialPanelSettings.TryNumber(body.Substring(bar + 1), out float custom))
                {
                    seconds = custom < 1f ? 1f : custom > 60f ? 60f : custom;
                    body = body.Substring(0, bar).Trim();
                }
                if (title.Length == 0 || body.Length == 0) continue;
                result.Add(new TutorialEntry(id, title, body, seconds, basis.whileBlocked));
            }
            return result;
        }

        public static string Format(string text, string interactKey, string uvKey) =>
            (text ?? "").Replace(InteractToken, interactKey).Replace(UvToken, uvKey);
    }
}
