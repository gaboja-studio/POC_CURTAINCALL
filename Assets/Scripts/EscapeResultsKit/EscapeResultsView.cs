using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace CurtainCall.UI.EscapeResultsKit
{
    [Serializable]
    public sealed class EscapePlayerData
    {
        public string Name;
        public Sprite Portrait;
        public Color SlotColor = Color.white;
        public bool Escaped;
        public Sprite[] Items = Array.Empty<Sprite>();
    }

    [Serializable]
    public sealed class EscapeStatisticData
    {
        public string Performance, Difficulty, ClearRate, AverageCheer;
    }

    [Serializable]
    public sealed class EscapePlayerRow
    {
        public GameObject Root;
        public UnityEngine.UI.Image Portrait, SlotColor, Outcome;
        public TMP_Text Name, OutcomeLabel;
        public UnityEngine.UI.Image[] Items;
        public TMP_Text[] EmptyItems;
    }

    [Serializable]
    public sealed class EscapeStatisticRow
    {
        public GameObject Root;
        public TMP_Text Performance, Difficulty, ClearRate, AverageCheer;
    }

    /// <summary>Display-only kit. The game owns snapshots, authority, input ownership and transitions.</summary>
    public sealed class EscapeResultsView : MonoBehaviour
    {
        public CanvasGroup Group;
        public GameObject PlayersContent, StatisticsContent;
        public UnityEngine.UI.Button PlayersTab, StatisticsTab, ReturnButton;
        public UnityEngine.UI.Image PlayersUnderline, StatisticsUnderline;
        public Sprite TabNormal, TabSelected, EscapedIcon, NotEscapedIcon;
        public TMP_Text Summary, ReturnLabel, HelpLabel, TeamBest, PersonalBest, EarnedTokens, SpentTokens;
        public UnityEngine.UI.Image PersonalBestPortrait;
        public GameObject PersonalBestPortraitFrame;
        public EscapePlayerRow[] PlayerRows;
        public EscapeStatisticRow[] StatisticRows;
        public UnityEvent ReturnRequested = new UnityEvent();
        public bool StatisticsSelected { get; private set; }
        public bool Visible => Group != null && Group.alpha > .5f;
        bool returnPending;
        bool returnAllowed;
        readonly Color positive = new Color32(115,199,161,255);
        readonly Color negative = new Color32(224,108,120,255);

        void Awake()
        {
            PlayersTab.onClick.AddListener(ShowPlayers);
            StatisticsTab.onClick.AddListener(ShowStatistics);
            ReturnButton.onClick.AddListener(RequestReturn);
        }

        void OnDestroy()
        {
            if (PlayersTab != null) PlayersTab.onClick.RemoveListener(ShowPlayers);
            if (StatisticsTab != null) StatisticsTab.onClick.RemoveListener(ShowStatistics);
            if (ReturnButton != null) ReturnButton.onClick.RemoveListener(RequestReturn);
        }

        public void ShowPlayers() => SelectTab(false);
        public void ShowStatistics() => SelectTab(true);
        public void SelectTab(bool statistics)
        {
            StatisticsSelected = statistics;
            PlayersContent.SetActive(!statistics);
            StatisticsContent.SetActive(statistics);
            PlayersUnderline.enabled = !statistics;
            StatisticsUnderline.enabled = statistics;
            SetTab(PlayersTab, !statistics);
            SetTab(StatisticsTab, statistics);
        }

        void SetTab(UnityEngine.UI.Button tab, bool selected)
        {
            var image = (UnityEngine.UI.Image)tab.targetGraphic;
            image.sprite = TabNormal;
            var states = tab.spriteState;
            states.selectedSprite = TabNormal;
            tab.spriteState = states;
            var label = tab.GetComponentInChildren<TMP_Text>();
            if(label != null)label.color=selected ? new Color32(240,238,220,255) : new Color32(182,193,191,255);
        }

        public void SetVisible(bool visible)
        {
            Group.alpha = visible ? 1 : 0;
            Group.interactable = visible;
            Group.blocksRaycasts = visible;
            if (!visible && EventSystem.current != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(transform))
                    EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void SetReturnAllowed(bool allowed, string help = null)
        {
            returnAllowed = allowed;
            returnPending = false;
            ReturnButton.interactable = allowed;
            ReturnLabel.text = allowed ? "로비로 돌아가기" : "방장의 결과 확인을 기다리는 중";
            HelpLabel.text = help ?? (allowed ? "버튼을 누르면 로비로 이동합니다" : "결과를 확인하는 동안 달리기는 계속됩니다");
        }

        public void RequestReturn()
        {
            if (!Visible || !returnAllowed || returnPending || !ReturnButton.interactable) return;
            returnPending = true;
            ReturnButton.interactable = false;
            ReturnRequested.Invoke();
        }

        public void SetPlayers(EscapePlayerData[] players)
        {
            if (players == null || players.Length < 1 || players.Length > PlayerRows.Length)
                throw new ArgumentException("Supply 1–4 players.", nameof(players));
            // Validate the complete snapshot before changing the view.
            foreach (var p in players)
                if (p == null || (p.Items != null && p.Items.Length > 2))
                    throw new ArgumentException("Each player needs a record and at most two item sprites.", nameof(players));
            int escaped = 0;
            for (int i=0;i<PlayerRows.Length;i++)
            {
                var row = PlayerRows[i]; row.Root.SetActive(i < players.Length);
                if (i >= players.Length) continue;
                var p = players[i]; if (p.Escaped) escaped++;
                row.Name.text = p.Name ?? "";
                row.Portrait.sprite = p.Portrait; row.Portrait.enabled = p.Portrait != null;
                row.SlotColor.color = p.SlotColor;
                row.Outcome.sprite = p.Escaped ? EscapedIcon : NotEscapedIcon;
                row.Outcome.color = p.Escaped ? positive : negative;
                row.OutcomeLabel.text = p.Escaped ? "탈출" : "미탈출";
                row.OutcomeLabel.color = row.Outcome.color;
                for(int j=0;j<2;j++)
                {
                    var item = p.Items != null && j<p.Items.Length ? p.Items[j] : null;
                    row.Items[j].sprite = item; row.Items[j].enabled = item != null;
                    row.EmptyItems[j].enabled = item == null;
                }
            }
            Summary.text = $"탈출 {escaped}명 · 미탈출 {players.Length-escaped}명";
        }

        public void SetStatistics(EscapeStatisticData[] rows, string teamBest, string personalBest,
            Sprite personalPortrait, string earnedTokens, string spentTokens)
        {
            if (rows == null || rows.Length > StatisticRows.Length)
                throw new ArgumentException("Supply up to six performance/difficulty rows.",nameof(rows));
            foreach(var row in rows) if(row==null) throw new ArgumentException("Null statistic row.",nameof(rows));
            for(int i=0;i<StatisticRows.Length;i++)
            {
                var target = StatisticRows[i]; target.Root.SetActive(i<rows.Length);
                if(i>=rows.Length) continue;
                target.Performance.text=rows[i].Performance ?? "";
                target.Difficulty.text=rows[i].Difficulty ?? "";
                target.ClearRate.text=rows[i].ClearRate ?? "—";
                target.AverageCheer.text=rows[i].AverageCheer ?? "—";
            }
            TeamBest.text=teamBest ?? "—"; PersonalBest.text=personalBest ?? "—";
            bool hasPortrait = personalPortrait != null;
            PersonalBestPortrait.sprite = personalPortrait;
            PersonalBestPortrait.enabled = hasPortrait;
            PersonalBestPortrait.transform.parent.gameObject.SetActive(hasPortrait);
            if (PersonalBestPortraitFrame != null) PersonalBestPortraitFrame.SetActive(hasPortrait);
            PersonalBest.alignment = hasPortrait ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Midline;
            PersonalBest.margin = hasPortrait ? new Vector4(72, 0, 0, 0) : Vector4.zero;
            EarnedTokens.text=earnedTokens ?? "—"; SpentTokens.text=spentTokens ?? "—";
        }
    }
}
