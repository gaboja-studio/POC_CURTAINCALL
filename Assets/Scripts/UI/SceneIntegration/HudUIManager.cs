using System.Collections.Generic;
using CurtainCall.UI.BackstageHudV1;
using CurtainCall.UI.InteractionV1;
using CurtainCall.UI.NotificationsV1;
using CurtainCall.UI.SpectatorV1;
using CurtainCall.WaitingRoom;
using TMPro;
using CurtainCall.Player;
using UnityEngine.InputSystem;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    public sealed class HudUIManager : MonoBehaviour
    {
        const string Path = "Prefabs/UIs/Roles/HudUIManager";
        static HudUIManager instance;
        public HudRoleCard[] prefabs;
        public HudRoleCard courseStatus;
        public HudPresentationSettings presentation;
        readonly List<HudRoleCard> cards = new();
        public static HudUIManager Current
        {
            get
            {
                if (instance != null) return instance;
                var prefab = Resources.Load<HudUIManager>(Path);
                if (prefab == null) throw new System.InvalidOperationException("Missing HUD manager: "+Path);
                instance = Instantiate(prefab); DontDestroyOnLoad(instance.gameObject);
                ScheduleScreenHold.Track(instance.GetComponent<Canvas>());
                return instance;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => instance = null;

        HudRoleCard Add(HudRole role, string content, Transform owner, string title, string condition, float? seconds,HudRoleCard skin=null,HudEventKind kind=HudEventKind.Notice,string badgeKey=null,Camera camera=null,bool upperRightEvent=false)
        {
            // Remove older cards (including cards still playing their exit) before the new card enters.
            int kept=0, capacity=presentation.For(role).Capacity;
            foreach(var old in cards)
                if(old && old.gameObject.activeSelf && old.role==role && old.UpperRightEvent==upperRightEvent && ++kept>=capacity)old.Close(true);
            var card = Instantiate(skin ? skin : prefabs[(int)role],transform,false);
            if(card.TryGetComponent<HudEventAppearance>(out var style)) style.Select(kind);
            if (upperRightEvent)
            {
                card.UpperRightEvent=true;
                card.Rect.anchorMin=card.Rect.anchorMax=card.Rect.pivot=Vector2.one;
                card.Rect.anchoredPosition=new Vector2(-32,-24);
            }
            card.BadgeKey=badgeKey;card.WorldCamera=camera;
            cards.Insert(0,card); card.Open(owner,content,title,condition,seconds);
            Arrange();
            return card;
        }
        public HudRoleCard ShowObjective(string content, Transform owner, string title, string condition = "") =>
            Add(HudRole.Objective,content,owner,title,condition,null);
        public HudRoleCard ShowGuide(string content, Transform owner, string title = "", float? seconds = null,string key=null)
        {
            foreach (var card in cards)
                if (card && card.role==HudRole.Guide && card.isActiveAndEnabled && !card.Closing &&
                    card.Owner && card.Owner.gameObject.activeInHierarchy && card.MatchesGuide(title,content,key))
                    return card;
            return Add(HudRole.Guide,content,owner,title,"",seconds,badgeKey:key);
        }
        public HudRoleCard ShowSystemEvent(string content, Transform owner, string title, float? seconds = null,HudEventKind kind=HudEventKind.Notice) =>
            Add(HudRole.SystemEvent,content,owner,title,"",seconds,null,kind);
        internal HudRoleCard ShowDeathWarning(string content, Transform owner, string title, float seconds) =>
            Add(HudRole.SystemEvent,content,owner,title,"",seconds,kind:HudEventKind.Warning,upperRightEvent:true);
        public HudRoleCard ShowZoneName(string content, Transform owner)
            => Add(HudRole.ZoneName,content,owner,"","",null);
        public HudRoleCard ShowFeedback(string content, Transform owner, string title = "", float? seconds = null) =>
            Add(HudRole.Feedback,content,owner,title,"",seconds);
        public HudRoleCard ShowOutcome(string content, Transform owner, string title, float? seconds = null) =>
            Add(HudRole.Outcome,content,owner,title,"",seconds);
        // A blocking result owns its canvas and button until the host confirms, including during a scene curtain.
        public HudRoleCard ShowModalOutcome(string content, Transform owner, string title, Transform modalRoot)
        {
            var card=Instantiate(prefabs[(int)HudRole.Outcome],transform,false);
            card.Open(owner,content,title,"",0);
            card.transform.SetParent(modalRoot,false);
            card.motion.group.blocksRaycasts=true;
            return card;
        }
        public HudRoleCard ShowVote(string content, Transform owner, string title, string hint,params HudVoteChoice[] choices)
        {
            var card=Add(HudRole.Vote,content,owner,title,hint,null);
            if (!voteInput) RefreshVoteInput();
            card.SetShortcutLabels(voteLabels);
            card.SetChoices(choices);
            return card;
        }
        public HudRoleCard ShowStatus(string content, Transform owner, string title = "",bool bottomCenter=false)
        {
            return Add(HudRole.Status,content,owner,title,"",null,bottomCenter ? courseStatus : null);
        }
        public HudRoleCard ShowInteraction(string content, Transform target, string key, Camera camera)
        {
            if (!target || !camera) return null;
            return Add(HudRole.Interaction,content,target,key,"",null,camera:camera);
        }
        // Specialized presenters keep their game data, input, world anchor and artwork.
        public static bool ShowFeedback(string content,ToastView view,ToastState state,float seconds,ToastIcon icon=ToastIcon.Default)
            => view.Show(content,state,seconds,icon);
        public static void ShowOutcome(ResultsPresentation content,ResultsPanelView view,bool takeFocus)
            => view.Open(content,takeFocus);
        public static bool ShowInteraction(string content, InteractionPromptView view, string key, InteractionAvailability availability,
            bool hold, float progress, string helper) => view.Show(key,content,availability,hold,progress,helper);
        public static bool ShowScoreFeedback(string content,FeedbackView view,int slot,int magnitude,bool positive)
            => positive ? view.TryShowCheer(slot,magnitude,content) : view.TryShowBoo(slot,magnitude,content);
        public static bool ShowSpectator(string content,SpectatorView view,SpectatorState state,string title,string prev,string next,bool canSwitch)
            => view.Present(state,title,content,prev,next,canSwitch);
        public static void ShowInventory(string content, InventorySlotsView view, IReadOnlyList<InventorySlotData> slots, TMP_Text name)
        { view.SetSlots(slots); name.text = content; }

        public static void DismissOwned(Transform owner,HudRole role)
        {
            if (!instance) return;
            foreach (var c in instance.cards)
                if (c && c.Owner==owner && c.role==role && !c.Closing) c.Close(true);
        }

        InputActionAsset voteInput;
        InputAction firstVote, secondVote;
        string[] voteLabels = System.Array.Empty<string>();
        void RefreshVoteInput()
        {
            if (voteInput) { voteInput.Disable(); Destroy(voteInput); }
            voteInput = InputBindingPreferences.CreateDraft();
            firstVote = voteInput.FindAction("Common/VotePrimary", true);
            secondVote = voteInput.FindAction("Common/VoteSecondary", true);
            voteLabels = new[] { InputBindingPreferences.KeyLabel(firstVote.bindings[0].effectivePath), InputBindingPreferences.KeyLabel(secondVote.bindings[0].effectivePath) };
            firstVote.Enable(); secondVote.Enable();
            foreach (var card in cards)
                if (card && card.role == HudRole.Vote) card.SetShortcutLabels(voteLabels);
        }
        void BindingsChanged() { if (voteInput) RefreshVoteInput(); }
        void OnEnable() => InputBindingPreferences.Changed += BindingsChanged;
        void OnDisable()
        {
            InputBindingPreferences.Changed -= BindingsChanged;
            if (voteInput) { voteInput.Disable(); Destroy(voteInput); voteInput=null; }
            firstVote=secondVote=null;
        }
        bool votesCovered;
        CanvasGroup modalMask;
        void Awake() { modalMask=gameObject.AddComponent<CanvasGroup>(); if(!presentation)presentation=Resources.Load<HudPresentationSettings>(HudPresentationSettings.ResourcePath); }
        void LateUpdate()
        {
            bool covered = SceneCurtainTransition.IsActive || (CommonMenuUI.Current && CommonMenuUI.Current.IsOpen)
                || (PerformanceEndingPresentation.Current && PerformanceEndingPresentation.Current.ResultVisible);
            modalMask.alpha=covered ? 0f : 1f;
            modalMask.blocksRaycasts=modalMask.interactable=!covered;
            cards.RemoveAll(c => c == null || !c.gameObject.activeSelf);
            foreach (var card in cards) card.Tick(covered ? 0 : Time.unscaledDeltaTime);
            Arrange();
            // A choice may synchronously dismiss/replace cards; dispatch after enumeration, once to the newest vote.
            if (!covered && !votesCovered && !ScheduleScreenHold.IsHeld)
            {
                int index = firstVote?.WasPressedThisFrame() == true ? 0 : secondVote?.WasPressedThisFrame() == true ? 1 : -1;
                var focus = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
                if (index >= 0 && !(focus && focus.TryGetComponent<TMP_InputField>(out var field) && field.isFocused)) cards.Find(c => c && c.role == HudRole.Vote && c.isActiveAndEnabled && !c.Closing)?.PressChoice(index);
            }
            votesCovered = covered;
        }
        static readonly int UpperRightEventLane = System.Enum.GetValues(typeof(HudRole)).Length;
        readonly float[] displacement = new float[UpperRightEventLane+1];
        readonly Dictionary<Transform,float> worldOffsets=new();
        void Arrange()
        {
            worldOffsets.Clear();
            System.Array.Clear(displacement,0,displacement.Length);
            // Lay out independent roles, alerts, objectives and votes in that order.
            for (int phase=0;phase<4;phase++)
            foreach(var c in cards)
            {
                if(!c || !c.gameObject.activeSelf) continue;
                int order=c.role switch { HudRole.SystemEvent=>1, HudRole.Objective=>2, HudRole.Vote=>3, _=>0 };
                if(order!=phase) continue;
                var r=c.Rect;
                var mode=presentation.For(c.role).mode;
                float step=mode==HudRepeatMode.Replace ? 0 : (mode==HudRepeatMode.StackUp?1:-1)*(r.rect.height+c.stackGap);
                if(c.WorldCamera && c.Owner)
                {
                    var cam=c.WorldCamera;
                    var screen=cam.WorldToScreenPoint(c.Owner.position);
                    c.motion.surface.gameObject.SetActive(screen.z>0 && cam.pixelRect.Contains(new Vector2(screen.x,screen.y)));
                    if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,screen,null,out var point))
                    {
                        var parent=((RectTransform)transform).rect;
                        var anchor=new Vector2(Mathf.Lerp(r.anchorMin.x,r.anchorMax.x,r.pivot.x),Mathf.Lerp(r.anchorMin.y,r.anchorMax.y,r.pivot.y));
                        var pos=point-parent.min-Vector2.Scale(anchor,parent.size)+c.BasePosition;
                        if(step!=0)
                        {
                            worldOffsets.TryGetValue(c.Owner,out float previous);pos.y+=previous;
                            worldOffsets[c.Owner]=previous+step;
                        }
                        if(r.anchoredPosition!=pos) r.anchoredPosition=pos;
                    }
                    continue;
                }
                float offset=0;
                if(c.avoidPreviousLane && phase>0)
                {
                    var predecessor=c.role switch {
                        HudRole.SystemEvent=>c.UpperRightEvent ? HudRole.Feedback : HudRole.ZoneName,
                        HudRole.Objective=>HudRole.Feedback, _=>HudRole.Objective
                    };
                    var basePivot=(Vector2)r.localPosition + c.BasePosition-r.anchoredPosition;
                    foreach(var other in cards)
                    {
                        if(!other || !other.gameObject.activeSelf ||
                            (other.role!=predecessor && !(phase>=2 && other.UpperRightEvent))) continue;
                        var prev=other.Rect;
                        if(basePivot.x+r.rect.xMin >= prev.localPosition.x+prev.rect.xMax ||
                           basePivot.x+r.rect.xMax <= prev.localPosition.x+prev.rect.xMin) continue;
                        offset=Mathf.Min(offset,prev.localPosition.y+prev.rect.yMin-c.stackGap-basePivot.y-r.rect.yMax);
                    }
                }
                int lane=c.UpperRightEvent ? UpperRightEventLane : (int)c.role;
                if(step!=0)offset+=displacement[lane];
                var placed=c.BasePosition+Vector2.up*offset;
                if(r.anchoredPosition!=placed) r.anchoredPosition=placed;
                displacement[lane]+=step;
            }
        }
    }
}
