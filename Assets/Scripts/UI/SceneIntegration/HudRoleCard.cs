using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CurtainCall.WaitingRoom;

namespace CurtainCall.UI.SceneIntegration
{
    public readonly struct HudVoteChoice
    {
        public readonly string Label;
        public readonly bool Interactable;
        public readonly Action OnClick;
        public HudVoteChoice(string label, bool interactable, Action onClick)
        { Label=label; Interactable=interactable; OnClick=onClick; }
    }

    public sealed class HudRoleCard : MonoBehaviour
    {
        [HideInInspector] public int layoutVersion;
        public HudRole role;
        internal string BadgeKey { get; set; }
        public HudRoleMotion motion;
        public TMP_Text title, body, condition;
        public RectTransform buttons;
        public Button[] choices;
        [Header("Size · disable both to keep the prefab rectangle")]
        public bool fitWidth = true, fitHeight = true;
        [Min(1)] public float minWidth = 75, maxWidth = 420;
        public VerticalLayoutGroup rows;
        [Header("Placement · the root RectTransform sets the screen position")]
        [Min(0)] public float minHeight;
        [Min(0)] public float stackGap = 8;
        [Tooltip("Objectives clear feedback above them; votes clear objectives above them.")]
        public bool avoidPreviousLane;
        internal Vector2 BasePosition;
        internal bool UpperRightEvent;
        static readonly Regex tokens = new Regex(@"\{(title|content|condition)\}");
        string[] formats;
        bool[] enabledRows;
        string lastHeading, lastText, lastDetail;
        string[] choiceFormats;

        public RectTransform Rect => (RectTransform)transform;
        public bool Closing { get; private set; }
        public string Title => title.text;
        public string Content => body.text;
        internal bool MatchesGuide(string heading,string text,string key) =>
            lastHeading==(heading??"") && lastText==(text??"") && (BadgeKey??"").Trim()==(key??"").Trim();
        internal Transform Owner;
        internal Camera WorldCamera;
        public void Extend(float seconds) { if (timed) remaining = Mathf.Max(remaining,seconds); }
        float remaining;
        bool timed;
        UiRuntimeFontFallback fonts;

        internal void Open(Transform owner, string text, string heading, string detail, float? seconds)
        {
            Owner = owner; BasePosition=Rect.anchoredPosition;
            fonts = GetComponentInParent<UiRuntimeFontFallback>();
            SetText(text, heading, detail);
            float duration = motion.allowDurationOverride ? seconds ?? motion.displaySeconds : motion.displaySeconds;
            remaining = duration; timed = duration > 0;
            motion.group.blocksRaycasts = role == HudRole.Vote;
            motion.Play(true);
        }
        public void SetText(string text, string heading = "", string detail = "")
        {
            text ??= ""; heading ??= ""; detail ??= "";
            if (formats==null)
            {
                formats=new[]{title.text,body.text,condition.text};
                enabledRows=new[]{title.gameObject.activeSelf,body.gameObject.activeSelf,condition.gameObject.activeSelf};
            }
            if (lastHeading==heading && lastText==text && lastDetail==detail) return;
            lastHeading=heading; lastText=text; lastDetail=detail;
            void Bind(TMP_Text label,int i)
            {
                string format=formats[i] ?? "";
                bool hasValue=false;
                string rendered=tokens.Replace(format, match =>
                {
                    string value=match.Groups[1].Value switch { "title"=>heading, "content"=>text, _=>detail };
                    hasValue |= value.Length!=0;
                    return value;
                });
                HudText.Configure(label, statusRow: i == 2);
                label.text=rendered;
                label.gameObject.SetActive(enabledRows[i] && rendered.Length!=0 && (hasValue || !tokens.IsMatch(format)));
            }
            Bind(title,0); Bind(body,1); Bind(condition,2);
            if (fonts) fonts.WarmUpLocalText(transform,title.text + body.text + condition.text);
            if(TryGetComponent<HudControlBadge>(out var badge))
            {
                if(fonts)fonts.WarmUpLocalText(badge.label.transform,role==HudRole.Interaction ? title.text : BadgeKey ?? badge.label.text);
                badge.Bind(role==HudRole.Interaction ? title.text : BadgeKey);
            }
            Fit();
        }
        string[] shortcutLabels = Array.Empty<string>();
        HudVoteChoice[] currentChoices;
        internal void SetShortcutLabels(string[] labels)
        {
            shortcutLabels = labels;
            if (currentChoices != null) SetChoices(currentChoices);
        }
        internal void PressChoice(int index)
        {
            if (Closing || !isActiveAndEnabled || index >= choices.Length) return;
            var button = choices[index];
            if (button && button.isActiveAndEnabled && button.IsInteractable()) button.onClick.Invoke();
        }
        public void SetChoices(HudVoteChoice[] options)
        {
            currentChoices = options;
            bool changed = false;
            for(int i=0;i<choices.Length;i++)
            {
                var button=choices[i];
                bool used=i<options.Length;
                changed |= button.gameObject.activeSelf != used;
                button.gameObject.SetActive(used);
                if(!used) { callbacks[i]=null; continue; }
                var option=options[i];
                button.interactable=option.Interactable;
                var label=button.GetComponentInChildren<TMP_Text>(true);
                HudText.Configure(label);
                var caption=choiceFormats[i].Replace("{choice}",option.Label ?? "");
                if (i < shortcutLabels.Length) caption = "[" + shortcutLabels[i] + "] " + caption;
                if(label.text!=caption)
                {
                    if(fonts) fonts.WarmUpLocalText(button.transform,caption);
                    label.text=caption; changed=true;
                }
                callbacks[i]=option.OnClick;
            }
            if (changed) Fit();
        }
        Action[] callbacks;
        void Awake()
        {
            if(role!=HudRole.Vote) return;
            callbacks=new Action[choices.Length]; choiceFormats=new string[choices.Length];
            for(int i=0;i<choices.Length;i++)
            {
                choiceFormats[i]=choices[i].GetComponentInChildren<TMP_Text>(true).text;
                int index=i;
                choices[i].onClick.AddListener(()=>callbacks[index]?.Invoke());
            }
        }
        public void Fit()
        {
            if(TryGetComponent<HudEventAppearance>(out var appearance)){appearance.Fit(this);return;}
            if (!rows || !rows.enabled || (!fitWidth && !fitHeight)) return;
            var layout=(RectTransform)rows.transform;
            if(fitWidth)
            {
                float limit = Mathf.Max(minWidth, maxWidth);
                float available = Mathf.Max(1, limit - rows.padding.horizontal);
                float widest=0;
                foreach(RectTransform child in layout)
                {
                    if(!child.gameObject.activeSelf || child.TryGetComponent<LayoutElement>(out var el) && el.ignoreLayout) continue;
                    if(child.TryGetComponent<HorizontalLayoutGroup>(out var group))group.CalculateLayoutInputHorizontal();
                    float w=child.TryGetComponent<TMP_Text>(out var t) ?
                        HudText.Measure(t, available).x : LayoutUtility.GetPreferredWidth(child);
                    widest=Mathf.Max(widest,w);
                }
                Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Clamp(Mathf.Ceil(widest)+rows.padding.horizontal,minWidth,limit));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(layout);
            if(fitHeight) Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(minHeight,Mathf.Ceil(LayoutUtility.GetPreferredHeight(layout))));
            LayoutRebuilder.ForceRebuildLayoutImmediate(layout);
        }
        internal void Tick(float dt)
        {
            if (Owner == null || !Owner.gameObject.activeInHierarchy) { Close(true); return; }
            if (!Closing && timed && !ScheduleScreenHold.IsHeld && (remaining -= dt) <= 0) Close();
        }
        public void Close(bool immediate = false)
        {
            if (immediate) { Closing = true; gameObject.SetActive(false); Destroy(gameObject); return; }
            if (Closing) return;
            Closing = true; motion.group.blocksRaycasts = false;
            motion.Play(false, () => Destroy(gameObject));
        }
    }
}
