using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Voice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Rehearse one action in the playable Understage hall, with the local player's normal controls.</summary>
    [RequireComponent(typeof(CameraCursorExclusion))]
    public sealed class UnderstagePursuerShowcase : MonoBehaviour
    {
        enum Motion { Breach, Walk, Turn, Duck, Run, Jump, Arm, Capture }
        static readonly string[] Labels = { "벽 부수기", "걷기", "돌아서 추격", "숙여 통과", "달리기", "점프·착지", "팔 공격·회피", "포획·투척" };
        static readonly string[] Places = { "복도 끝 콘크리트 벽", "복도 끝", "복도 끝", "복도 중앙 문틀", "복도 끝", "복도 끝", "훈련 더미 · 고정 예고선", "훈련 더미 · 절단과 투척" };
        const float PanelWidth = 720f;

        [SerializeField] EscapeUnderstageBreach breach;
        [SerializeField] EscapeUnderstageChase chase;
        [SerializeField] Transform hallStart, passage;
        [SerializeField] Font labelFont;

        CameraCursorExclusion cursorRegion;
        VoiceChatDevOverlay hiddenVoicePanel;
        GUIStyle buttonStyle, headingStyle;
        Motion motion;
        bool reviewing, openWhenReady, wasChasing;
        double epoch;
        Pose savedActor;
        Vector3 jumpStart, jumpEnd;
        bool previewCart;
        Transform previewDummy;
        readonly GameObject[] previewLimbs = new GameObject[4];
        float elapsed;
        Transform Actor => chase.transform;
        Transform Track => motion == Motion.Duck ? passage : hallStart;

        void Awake() => cursorRegion = GetComponent<CameraCursorExclusion>();
        public void RequestOpen() => openWhenReady = true;

        bool CanReview()
        {
            var local = NetworkPlayer.Local;
            var timer = EscapeRouteTimer.Current;
            return Debug.isDebugBuild && local && local.IsServer && local.State == PlayerState.Normal && NetworkPlayer.All.Count == 1 &&
                timer && timer.gameObject.scene == gameObject.scene && timer.HasStarted && EscapeCampaignContent.IsTraversalActive;
        }

        void Open()
        {
            if (reviewing || !CanReview()) return;
            epoch = EscapeRouteTimer.Current.StartedAt;
            wasChasing = breach.EntranceComplete;
            savedActor = chase.ResumePose;
            reviewing = true;
            chase.InShowcase = true;
            var panel = FindFirstObjectByType<VoiceChatDevOverlay>();
            if (panel && !panel.PresentationSuppressed)
            {
                hiddenVoicePanel = panel;
                hiddenVoicePanel.PresentationSuppressed = true;
            }
            chase.enabled = false;
            breach.enabled = false;
            Select(Motion.Breach);
        }

        void Close()
        {
            if (!reviewing) return;
            reviewing = false;
            if (previewDummy) previewDummy.gameObject.SetActive(false);
            chase.InShowcase = false;
            if (hiddenVoicePanel) hiddenVoicePanel.PresentationSuppressed = false;
            hiddenVoicePanel = null;
            chase.enabled = false;
            chase.RestPose();
            chase.ResetAttackPreview();
            breach.Sample(0f);
            var timer = EscapeRouteTimer.Current;
            if (wasChasing && timer && timer.HasStarted && timer.StartedAt == epoch)
            {
                breach.Sample(breach.Duration, false);
                Actor.SetPositionAndRotation(savedActor.position, savedActor.rotation);
            }
            breach.enabled = true;
            chase.enabled = true;
        }

        void OnDisable()
        {
            openWhenReady = false;
            if (cursorRegion) cursorRegion.SetScreenRect(Rect.zero);
            if (chase && breach) Close();
        }

        void Select(Motion next)
        {
            chase.enabled = false;
            chase.RestPose();
            chase.ResetAttackPreview();
            if (previewDummy) previewDummy.gameObject.SetActive(false);
            breach.Sample(0f);
            motion = next;
            elapsed = 0f;
            if (next == Motion.Breach)
            {
                elapsed = Mathf.Max(0f, breach.ImpactTime - .65f);
                breach.Sample(elapsed);
                return;
            }
            breach.Sample(breach.Duration, false);
            Actor.SetPositionAndRotation(Track.TransformPoint(Vector3.forward * (next == Motion.Duck ? -4.1f : 0f)),
                Track.rotation);
            if (next == Motion.Jump)
            {
                var link = chase.CartLink;
                var player = NetworkPlayer.Local;
                previewCart = link && player && Vector3.Distance(player.transform.position, link.transform.position) <= 12f;
                jumpStart = previewCart ? link.transform.TransformPoint(link.startPoint) : hallStart.position;
                jumpEnd = previewCart ? link.transform.TransformPoint(link.endPoint) : hallStart.TransformPoint(Vector3.forward * 3.6f);
                Actor.rotation = Quaternion.LookRotation(jumpEnd - jumpStart, hallStart.up);
                SampleJump(0f);
                return;
            }
            chase.SampleGait(0f, 0f);
            if (next == Motion.Arm || next == Motion.Capture)
            {
                EnsureDummy();
                previewDummy.gameObject.SetActive(true);
                foreach (var limb in previewLimbs) limb.SetActive(true);
                return;
            }
            if (next == Motion.Turn)
            {
                var towardPlayer = Vector3.ProjectOnPlane(NetworkPlayer.Local.transform.position - Actor.position, Track.up);
                if (towardPlayer.sqrMagnitude > .01f)
                    Actor.rotation = Quaternion.AngleAxis(90f, Track.up) * Quaternion.LookRotation(towardPlayer, Track.up);
                chase.enabled = true;
            }
        }

        void Update()
        {
            if (!CanReview() || (reviewing && EscapeRouteTimer.Current.StartedAt != epoch))
            {
                Close();
                cursorRegion.SetScreenRect(Rect.zero);
                return;
            }
            bool input = !InputGate.IsBlocked;
            var keys = Keyboard.current;
            if (openWhenReady || (input && keys != null && keys.f6Key.wasPressedThisFrame))
            {
                if (reviewing && !openWhenReady) Close(); else Open();
                openWhenReady = false;
            }
            cursorRegion.SetScreenRect(input ? Panel() : Rect.zero);
            if (!reviewing) return;
            if (input && keys != null)
            {
                for (int i = 0; i < Labels.Length; i++)
                    if (keys[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { Select((Motion)i); return; }
                if (keys.rKey.wasPressedThisFrame) { Select(motion); return; }
            }
            Advance(Time.deltaTime);
        }

        void Advance(float dt)
        {
            float previous = elapsed;
            elapsed += dt;
            if (motion == Motion.Arm || motion == Motion.Capture)
            {
                chase.SampleGait(0, 0);
                bool caught = motion == Motion.Capture;
                bool severed;
                bool finished = chase.SampleAttackPreview(elapsed, Track.TransformPoint(Vector3.forward * 5f),
                    Track.right, previewDummy, caught, out severed);
                foreach (var limb in previewLimbs) limb.SetActive(!severed);
                if (finished) Select(motion);
                return;
            }
            if (motion == Motion.Jump)
            {
                SampleJump(elapsed);
                if (previous < chase.ContactTime && elapsed >= chase.ContactTime) chase.PlayStep(.8f);
                if (elapsed >= chase.JumpDuration + .65f) Select(motion);
                return;
            }
            if (motion == Motion.Breach)
            {
                if (elapsed >= breach.Duration + .65f) Select(motion);
                else breach.Sample(elapsed);
                return;
            }
            if (motion == Motion.Turn)
            {
                if (elapsed >= 4f) Select(motion);
                return;
            }
            float z = Track.InverseTransformPoint(Actor.position).z;
            float end = motion switch { Motion.Duck => 4.1f, Motion.Run => 8f, _ => 5.4f };
            if (z >= end - .001f) { Select(motion); return; }
            float duck = motion == Motion.Duck
                ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.6f, 3.6f, Mathf.Abs(z))) : 0f;
            float run = motion == Motion.Run ? Mathf.Clamp01(Mathf.Min(elapsed / .55f, (end - z) / 2f)) : 0f;
            float distance = Mathf.Min(end - z, chase.Speed(duck, run) * dt);
            Actor.Translate(Vector3.forward * distance);
            chase.AdvanceGait(distance, duck, run, false, dt);
        }

        void SampleJump(float time)
        {
            chase.SampleJump(time, jumpStart, jumpEnd);
        }

        void EnsureDummy()
        {
            if (previewDummy) return;
            previewDummy = new GameObject("Pursuer preview dummy").transform;
            previewDummy.SetParent(transform, false);
            void Part(string name, Vector3 position, Vector3 scale, int limb = -1)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name; go.transform.SetParent(previewDummy, false);
                go.transform.localPosition = position; go.transform.localScale = scale;
                go.GetComponent<Collider>().enabled = false;
                if (limb >= 0) previewLimbs[limb] = go;
            }
            Part("Torso", new Vector3(0, 1.05f, 0), new Vector3(.42f, .55f, .26f));
            Part("Head", new Vector3(0, 1.48f, 0), Vector3.one * .3f);
            Part("Left arm", new Vector3(-.32f, 1.05f, 0), new Vector3(.18f, .55f, .18f), 0);
            Part("Right arm", new Vector3(.32f, 1.05f, 0), new Vector3(.18f, .55f, .18f), 1);
            Part("Left leg", new Vector3(-.13f, .4f, 0), new Vector3(.2f, .8f, .2f), 2);
            Part("Right leg", new Vector3(.13f, .4f, 0), new Vector3(.2f, .8f, .2f), 3);
        }

        Rect Panel()
        {
            float scale = Mathf.Min(1.2f, Screen.width / (reviewing ? PanelWidth + 20f : 600f), Screen.height / 480f);
            float width = (reviewing ? PanelWidth : 162f) * scale, height = (reviewing ? 114f : 32f) * scale;
            return new Rect((Screen.width - width) / 2f, Screen.height - height - 10f, width, height);
        }

        void OnGUI()
        {
            if (!CanReview() || InputGate.IsBlocked) return;
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button) { font = labelFont, fontSize = 15 };
                headingStyle = new GUIStyle(GUI.skin.label) { font = labelFont, fontSize = 15, alignment = TextAnchor.MiddleLeft };
                headingStyle.normal.textColor = Color.white;
            }
            Rect panel = Panel();
            if (!reviewing)
            {
                if (GUI.Button(panel, "F6   추격자 모드", buttonStyle)) Open();
                return;
            }
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(panel.position, Quaternion.identity, Vector3.one * (panel.width / PanelWidth));
            GUI.Box(new Rect(0, 0, PanelWidth, 114), GUIContent.none);
            string place = motion == Motion.Jump && previewCart ? "진입 보행면 정비 카트" : Places[(int)motion];
            GUI.Label(new Rect(10, 2, 435, 28), $"추격자 모드  /  {place}     ·     R 다시 보기", headingStyle);
            if (GUI.Button(new Rect(PanelWidth - 120f, 3, 112, 25), "F6   일반 추격", buttonStyle)) Close();
            var background = GUI.backgroundColor;
            float column = (PanelWidth - 16f) / 4;
            for (int i = 0; i < Labels.Length; i++)
            {
                GUI.backgroundColor = i == (int)motion ? new Color(.45f, .85f, .82f) : background;
                if (GUI.Button(new Rect(8 + i % 4 * column, 33 + i / 4 * 38, column - 4f, 34), $"{i + 1}   {Labels[i]}", buttonStyle) && reviewing) Select((Motion)i);
            }
            GUI.backgroundColor = background;
            GUI.matrix = oldMatrix;
        }
    }
}
