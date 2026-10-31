using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 로비 스케줄표 상호작용과 플래너 노트 창(임시 IMGUI — 낡은 플래너 아트는 후속). 기준: schedule-board.md.
    /// 스케줄표 앞에서 F → 내 화면만 스케줄표 클로즈업 카메라로 바뀌고 창이 열린다(이동 입력 막음). ESC·닫기·멀어지면 닫힌다.
    /// 창: 1~4일차 공연·난도, 밤이면 호스트가 칸을 고르고 확정, 참가자는 실시간으로 보기만. 준비 완료는 숙소 침대(<see cref="NightBed"/>)에서 한다.
    /// </summary>
    public sealed class ScheduleBoardView : MonoBehaviour, IShopTarget, IShopPresentation
    {
        [Tooltip("거리를 재는 스케줄표 판(없으면 이 오브젝트).")]
        [SerializeField] Transform board;

        [Tooltip("창이 열려 있는 동안 켜는 클로즈업 카메라. 4단계 씬 구성 메뉴가 만든다.")]
        [SerializeField] CinemachineCamera closeUp;

        [Tooltip("스케줄표에 닿는 수평 거리(m).")]
        [SerializeField, Min(.5f)] float reach = 2f;
        [SerializeField] bool showLegacyUi = true;

        [Tooltip("클로즈업 카메라 우선순위(플레이어 카메라보다 높게).")]
        [SerializeField] int closeUpPriority = 100;


        UIInputBlocker blocker;
        GUIStyle titleStyle;
        Vector2 scroll;
        bool open;
        bool inReach;

        /// <summary>창이 열려 있는가(내 화면).</summary>
        public bool IsOpen => open;
        public Transform VisualRoot => Target;
        public Vector3 PresentationPoint => Target.position;
        /// <summary>스케줄표 클로즈업 카메라 자리(튜토리얼 클로즈업도 같은 자리를 쓴다).</summary>
        public Transform CloseUpPose => closeUp != null ? closeUp.transform : null;
        public string ActionLabel => WaitingRoomText.Get("schedule.board-action", "스케줄표 보기");
        public bool IsAvailable => !open && ScheduleBoard.Instance != null;
        public float Reach(Vector3 feet) { float d = Flat(feet - Target.position); return d <= reach ? d : -1; }
        public void Interact() { if (IsAvailable) SetOpen(true); }
        void OnEnable() => ShopReach.Add(this);

        void Awake()
        {
            blocker = gameObject.AddComponent<UIInputBlocker>();
            blocker.BlockWhileActive = false;
            if (closeUp != null) closeUp.gameObject.SetActive(false);
        }

        void OnDisable() { ShopReach.Remove(this); SetOpen(false); }

        void Update()
        {
            ShopReach.Pump();
            var reader = LocalReader();
            inReach = reader != null && Flat(reader.transform.position - Target.position) <= reach;

            if (!open)
            {
                return;
            }

            var keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            // 창을 연 채로 밀려나거나 순간이동(장소 전환)하면 닫는다.
            if (escape || reader == null || Flat(reader.transform.position - Target.position) > reach + .5f || ScheduleBoard.Instance == null)
                SetOpen(false);
        }

        /// <summary>창을 연다/닫는다(내 화면만). 열면 클로즈업 카메라·입력 막기.</summary>
        public void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            if (blocker != null) blocker.SetOpen(value);
            if (closeUp != null)
            {
                closeUp.Priority = closeUpPriority;
                closeUp.gameObject.SetActive(value);
            }
            HideLocalModel(value);
        }

        // 클로즈업 카메라와 판 사이에 내 캐릭터가 서 있으면 판을 가리므로, 창이 열린 동안 내 화면에서만 내 캐릭터를 숨긴다.
        readonly System.Collections.Generic.List<Renderer> hidden = new();

        void HideLocalModel(bool hide)
        {
            foreach (var r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            if (!hide) return;
            var reader = LocalReader();
            if (reader == null) return;
            foreach (var r in reader.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                r.enabled = false;
                hidden.Add(r);
            }
        }

        Transform Target => board != null ? board : transform;

        static PlayerInputReader LocalReader()
        {
            if (NetworkPlayer.All.Count > 0)
                return NetworkPlayer.Local != null ? NetworkPlayer.Local.GetComponent<PlayerInputReader>() : null;
            return FindFirstObjectByType<PlayerInputReader>();
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        void OnGUI()
        {
            if (!showLegacyUi) return;
            var schedule = ScheduleBoard.Instance;
            if (schedule == null) return;
            if (!open)
            {
                if (!ShopReach.HasExternalPresentation && inReach && !InputGate.IsBlocked)
                    GUI.Box(new Rect((Screen.width - 220) / 2f, Screen.height * .62f, 220, 28), "[F] 스케줄표 보기");
                return;
            }
            DrawWindow(schedule);
        }

        void DrawWindow(ScheduleBoard schedule)
        {
            var book = schedule.Book;
            var run = CampaignRun.Instance;
            bool host = schedule.HasAuthority;
            int night = book.Night;
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };

            // 화면이 낮아도 아래 버튼(확정·준비·닫기)이 잘리지 않게 창을 화면에 맞추고 일정만 스크롤한다.
            float width = Mathf.Min(560f, Screen.width - 20f), height = Mathf.Min(600f, Screen.height - 20f);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height), GUI.skin.box);
            GUILayout.Label(run != null && run.State.Phase == CampaignPhase.DemoEnding ? "스케줄표 — 4일차 아침"
                : night > 0 ? $"스케줄표 — {night}일차 밤 정비" : "스케줄표 (보기만 — 작성은 밤 정비 때)", titleStyle);
            GUILayout.Label("하루: 아침 쉬는 시간 → 공연 1 → 쉬는 시간 → 공연 2 → 밤 정비");

            scroll = GUILayout.BeginScrollView(scroll);
            for (int day = 1; day <= ScheduleBook.Days; day++) DrawDay(schedule, book, day, host);
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            if (night > 0 && run != null && run.State.Phase == CampaignPhase.Night)
            {
                if (book.IsConfirmedTonight) GUILayout.Label("이번 밤 스케줄을 확정했습니다.");
                else if (host)
                {
                    if (GUILayout.Button("확정(저장 — 확정한 일차는 다시 고칠 수 없음)", GUILayout.Height(30))) schedule.Confirm();
                }
                else GUILayout.Label("호스트가 스케줄을 작성하고 확정합니다.");

                GUILayout.Label($"준비 완료 {run.State.ReadyCount}/{run.State.RequiredCount} — 숙소 침대에 누워 준비합니다.");
            }
            if (host && !string.IsNullOrEmpty(schedule.LastMessage)) GUILayout.Label(schedule.LastMessage);
            if (GUILayout.Button("닫기 (ESC)")) SetOpen(false);
            GUILayout.EndArea();
        }

        void DrawDay(ScheduleBoard schedule, ScheduleBook book, int day, bool host)
        {
            string state = day == 1 ? "고정"
                : book.IsLocked(day) ? "확정"
                : book.IsEditable(day) ? "작성 중"
                : "";
            if (day == ScheduleBook.UnplayedDay) state = string.IsNullOrEmpty(state) ? "실행 안 함" : state + " · 실행 안 함";

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{day}일차   {(string.IsNullOrEmpty(state) ? "" : $"[{state}]")}");
            GUILayout.BeginHorizontal();
            for (int slot = 0; slot < ScheduleBook.SlotsPerDay; slot++)
            {
                GUILayout.BeginVertical(GUILayout.MinWidth(200));
                string trick = book.Get(day, slot);
                int level = book.Difficulty(day, slot);
                GUILayout.Label($"공연 {slot + 1}: {(string.IsNullOrEmpty(trick) ? "—" : schedule.TrickName(trick))}{(level > 0 ? $"  ({level}단계 · {ScheduleBoard.TierRewardText(level)})" : "")}");
                if (host && book.IsEditable(day))
                {
                    GUILayout.BeginHorizontal();
                    foreach (var choice in schedule.SelectableTricks)
                    {
                        bool chosen = choice == trick;
                        if (GUILayout.Toggle(chosen, schedule.TrickName(choice), GUI.skin.button) != chosen)
                            schedule.Set(day, slot, chosen ? "" : choice);
                    }
                    GUILayout.EndHorizontal();
                    // 깬 단계의 다음 단계까지만 고를 수 있다(M2).
                    if (!string.IsNullOrEmpty(trick))
                    {
                        GUILayout.BeginHorizontal();
                        int max = schedule.MaxTier(trick);
                        for (int tier = 1; tier <= ScheduleBook.MaxDifficulty; tier++)
                        {
                            GUI.enabled = tier <= max;
                            if (GUILayout.Toggle(level == tier, $"{tier}단계", GUI.skin.button) && level != tier) schedule.Set(day, slot, trick, tier);
                        }
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
