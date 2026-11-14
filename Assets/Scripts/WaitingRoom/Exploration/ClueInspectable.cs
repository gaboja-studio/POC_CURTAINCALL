using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 조사 가능한 단서. F → 내 화면에서만 단서를 눈앞에 들어 회전(왼쪽 끌기)·확대(휠)해 앞뒤를 본다(Task-079 2단계).
    /// 결정(2026-10-29 사용자): 제자리 열람(소지·슬롯 없음, 닫으면 원위치), 보는 동안 이동·다른 입력 막고 시간은 흐름,
    /// 여러 명이 각자 동시에 본다(열람 자체는 동기화하지 않음). ESC·F·오른쪽 클릭으로 닫고, 밀려나면 자동으로 닫힌다.
    /// 원본은 그대로 두고 보이는 사본을 카메라 앞에 띄우므로 장면·충돌·다른 사람 화면은 바뀌지 않는다.
    /// 발견 상태는 기존 탐색 채널로 팀 공유하고, 오버뷰 조작은 각 화면에만 남긴다.
    /// </summary>
    public sealed class ClueInspectable : MonoBehaviour, IShopTarget, IShopPresentation, IExplorationTarget
    {
        [SerializeField] string targetId = "";
        [SerializeField] string availableAfter = "";
        [SerializeField] bool revealPending;

        [Tooltip("보여 줄 단서 몸체(없으면 이 오브젝트).")]
        [SerializeField] Transform body;

        [Tooltip("단서 이름(F 안내·오버뷰 제목).")]
        [SerializeField] string title = "단서";

        [Tooltip("오버뷰 아래에 보일 설명. 비우면 제목만.")]
        [SerializeField, TextArea] string description = "";

        [Tooltip("단서에 닿는 수평 거리(m).")]
        float CommonReach => CurtainCall.Settings.GameSettings.Base.Interaction.Distance;

        [Tooltip("눈앞에 띄울 때 단서의 가장 긴 변 크기(m).")]
        [SerializeField, Min(.05f)] float viewSize = .32f;

        [Tooltip("카메라에서 단서까지 거리(m). 휠로 이 범위 안에서 당기고 민다.")]
        [SerializeField] Vector2 viewDistance = new(.35f, .9f);

        [Tooltip("살펴볼 때 단서를 비추는 조명 세기. 밝은 종이(신문)는 낮춰야 글씨가 날아가지 않는다.")]
        [SerializeField, Min(0f)] float inspectLight = 2.5f;

        [Tooltip("살펴볼 때 정면으로 보여 줄 면(유니티 Quad와 같은 기준: 이 Transform의 -Z가 보는 사람 쪽, +Y가 위). 종이·지도·노트처럼 눕혀 두거나 벽에 붙인 단서는 이걸 지정해야 뒤집히지 않는다. 비우면 놓인 모습 그대로 살짝 위에서 본다.")]
        [SerializeField] Transform viewFace;
        [Tooltip("처음 보여 줄 때 더 돌릴 각도(도). viewFace가 있으면 그 정면 기준, 없으면 놓인 모습 기준.")]
        [SerializeField] Vector3 viewEuler;

        [Tooltip("마우스 끌기 회전 속도(도/픽셀).")]
        [SerializeField, Min(.01f)] float rotateSpeed = .35f;

        [Tooltip("오버뷰에서 돌려 봐야 보이는 새김 글자(위쪽 축이 새긴 면의 바깥 방향). 없으면 이 연출을 쓰지 않는다.")]
        [SerializeField] Transform secretMark;

        [Tooltip("새김 글자를 찾았을 때 설명에 더할 문구. 문구 파일의 target.<ID>.secret이 우선한다.")]
        [SerializeField, TextArea] string secretText = "";

        [Tooltip("노트처럼 넘겨 읽는 쪽 글귀(2026-11-12 사용자). 문구 파일의 target.<ID>.page.1, .page.2 …가 있으면 그쪽이 우선한다. 비우면 넘기는 화면 없이 기존처럼 보인다.")]
        [SerializeField, TextArea(3, 12)] string[] pages = Array.Empty<string>();
        [Tooltip("그림으로 된 쪽(2026-11-13 사용자: 낡은 노트). 넘길 때마다 pageSurface의 그림을 바꾼다. 글귀 쪽과 함께 있으면 긴 쪽 수를 따른다.")]
        [SerializeField] Texture2D[] pageImages = Array.Empty<Texture2D>();
        [Tooltip("쪽 그림을 보여 줄 면(몸체 안의 렌더러). 살펴보는 사본에서 이 면의 그림만 바뀐다.")]
        [SerializeField] Renderer pageSurface;
        Renderer pageSurfaceCopy, pageBackCopy; MaterialPropertyBlock pageBlock;
        int PageCount => Mathf.Max(Pages.Length, pageImages != null ? pageImages.Length : 0);
        void ShowPageImage()
        {
            if (pageSurfaceCopy == null || pageImages == null || pageImages.Length == 0) return;
            var image = pageImages[Mathf.Clamp(page, 0, pageImages.Length - 1)]; if (image == null) return;
            pageBlock ??= new MaterialPropertyBlock(); pageSurfaceCopy.GetPropertyBlock(pageBlock);
            pageBlock.SetTexture("_BaseMap", image); pageBlock.SetTexture("_MainTex", image); pageBlock.SetTexture("_EmissionMap", image); pageSurfaceCopy.SetPropertyBlock(pageBlock);
            if (pageBackCopy != null) pageBackCopy.SetPropertyBlock(pageBlock);
        }
        int page;
        string[] pageCache;
        /// <summary>넘겨 읽는 쪽 글귀. 문구 파일 → 인스펙터 순서.</summary>
        public string[] Pages
        {
            get
            {
                if (pageCache != null) return pageCache;
                var list = new System.Collections.Generic.List<string>();
                for (int n = 1; WaitingRoomText.Has("target." + targetId + ".page." + n); n++) list.Add(WaitingRoomText.Get("target." + targetId + ".page." + n, "").Replace("\\n", "\n"));
                return pageCache = list.Count > 0 ? list.ToArray() : pages ?? Array.Empty<string>();
            }
        }

        public static event Action<ClueInspectable> Inspected;

        internal static event Action<ClueInspectable, string, string, double?> InspectionObserved;
        internal static event Action<ClueInspectable, bool, string> AvailabilityObserved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Inspected = null; InspectionObserved = null; AvailabilityObserved = null; SecretsSeen.Clear(); }

        // 새김 글자는 찾은 플레이어 화면에만 남는다(2026-11-01 사용자: 공유하지 않음).
        static readonly System.Collections.Generic.HashSet<string> SecretsSeen = new();

        /// <summary>지금 내 화면에서 보고 있는 단서(없으면 null).</summary>
        public static ClueInspectable Current { get; private set; }
        ExplorationPanel panelView;

        /// <summary>팀이 이 런에서 발견했는가.</summary>
        public bool Discovered => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(targetId);
        public string UnlockTarget => availableAfter;
        public bool RevealPending => revealPending;
        public bool Revealed => !revealPending && (string.IsNullOrEmpty(availableAfter)
            || (ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(availableAfter)));
        public void ConfigureIdentity(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            targetId = id;
        }
        public void ConfigurePendingRoute(string id) { ConfigureIdentity(id); revealPending = true; }
        public void ConfigureRoute(string id, string unlockedBy)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            targetId = id; availableAfter = unlockedBy ?? ""; revealPending = false;
        }
        public string TargetId => targetId;
        public string ToolId => "";
        public string ToolName => "";
        public string Label => Title;
        public string RewardItemId => "";
        public Vector3 RewardPoint => Body.position;
        public int RewardUidOffset => 0;
        public bool Resolve(int attemptNumber, System.Random rng) => true;
        public string Describe(bool success, int attemptNumber) => WaitingRoomText.Format("clue.found", "{title} 발견", ("title", Title));
        public bool IsOpen => Current == this;
        public string Title => WaitingRoomText.Get("target." + targetId + ".name", title);
        public string Description => WaitingRoomText.Get("target." + targetId + ".desc", description);
        public bool HasSecret => secretMark != null;
        /// <summary>내 화면에서 새김 글자를 찾았는가.</summary>
        public bool SecretSeen => HasSecret && SecretsSeen.Contains(targetId);
        public string SecretText => WaitingRoomText.Get("target." + targetId + ".secret", secretText);

        public Transform VisualRoot => Body;
        [Tooltip("손이 닿는지 재는 점. 비우면 몸체 원점 위 0.25m. 몸체 원점이 받침대 속에 묻힌 단서(다리 없는 인형 옷)는 이걸 지정한다.")]
        [SerializeField] Transform reachPoint;
        public Vector3 PresentationPoint => reachPoint != null ? reachPoint.position : Body.position + Vector3.up * .25f;
        public bool IsAvailable => Revealed && Current == null;
        public string ActionLabel => WaitingRoomText.Format("prompt.inspect-clue", "{title} 살펴보기", ("title", Title));

        Transform Body => body != null ? body : transform;

        UIInputBlocker blocker;
        NetworkPlayer openedBy;
        bool openedByNetworkPlayer;
        Transform view, pivot;
        float distance;
        bool inReach;
        int openedFrame;
        double openedAt;
        bool lastRevealed;
        double InspectionMs => (Time.realtimeSinceStartupAsDouble - openedAt) * 1000d;
        GUIStyle titleStyle, textStyle;
        Transform secretCopy;
        readonly System.Collections.Generic.List<Renderer> secretParts = new();
        float secretHeld, secretFoundAt = -10f;
        MaterialPropertyBlock secretBlock;

        void Awake()
        {
            blocker = gameObject.AddComponent<UIInputBlocker>();
            blocker.BlockWhileActive = false;
        }

        void OnEnable()
        {
            useGUILayout = false; // OnGUI에서 GUILayout을 쓰지 않으므로 배치 단계 호출을 건너뛴다(2026-11-14 최적화)
            // 이전 배치도 씬/계층 경로로 식별. 정식 배치에는 명시적 ID 권장.
            if (string.IsNullOrEmpty(targetId))
            {
                string path = name + "#" + transform.GetSiblingIndex();
                for (var parent = transform.parent; parent != null; parent = parent.parent)
                    path = parent.name + "#" + parent.GetSiblingIndex() + "/" + path;
                targetId = "clue:" + gameObject.scene.path + ":" + path;
            }
            ShopReach.Add(this); ExplorationTargets.Add(this);
            lastRevealed = Revealed;
            AvailabilityObserved?.Invoke(this, lastRevealed, "ENABLED");
        }
        void OnDisable()
        {
            ShopReach.Remove(this); ExplorationTargets.Remove(this);
            SetOpen(false, "DISABLED");
            AvailabilityObserved?.Invoke(this, false, "DISABLED");
        }

        public float Reach(Vector3 feet, Component actor = null)
        {
            if (!Revealed || Current != null) return -1f;
            return ((IExplorationTarget)this).Reach(feet, actor);
        }

        // 호스트 자신의 오버뷰 상태가 다른 참가자의 단서 발견 판정을 막지 않는다.
        float IExplorationTarget.Reach(Vector3 feet, Component actor)
        {
            if (!Revealed) return -1f;
            return ExplorationReach.From(Body, PresentationPoint, feet, CommonReach, actor: actor);
        }

        public void Interact() { if (Current == null) SetOpen(true); }

        void Update()
        {
            bool revealed = Revealed;
            if (revealed != lastRevealed)
            {
                lastRevealed = revealed;
                AvailabilityObserved?.Invoke(this, revealed, "REVEAL_CONDITION");
            }
            ShopReach.Pump();
            inReach = ShopReach.Focused == (IShopTarget)this;
            if (!IsOpen) return;

            if (openedByNetworkPlayer && (openedBy == null || openedBy != NetworkPlayer.Local))
            { SetOpen(false, "OWNER_CHANGED"); return; }

            var reader = LocalReader();
            bool pushedAway = reader == null || new Vector2(reader.transform.position.x - Body.position.x, reader.transform.position.z - Body.position.z).magnitude > CommonReach + .6f;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            string close = !revealed ? "NO_LONGER_REVEALED" : reader == null ? "READER_UNAVAILABLE" :
                !PlayerInteraction.CanInteract(reader) ? "ACTOR_UNAVAILABLE" :
                pushedAway ? "OUT_OF_REACH" : keyboard != null && keyboard.escapeKey.wasPressedThisFrame ? "ESCAPE_KEY" :
                Time.frameCount > openedFrame + 1 && reader != null && reader.WasPressedThisFrame("Interact") ? "INTERACT_KEY" :
                mouse != null && mouse.rightButton.wasPressedThisFrame ? "RIGHT_CLICK" : null;
            if (close != null) { SetOpen(false, close); return; }

            if (mouse != null)
            {
                if (mouse.leftButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue() * rotateSpeed;
                    var cam = view.parent;
                    pivot.Rotate(cam.up, -delta.x, Space.World);
                    pivot.Rotate(cam.right, delta.y, Space.World);
                }
                // 노트: 좌우 화살표(또는 A·D)로 쪽을 넘긴다.
                if (keyboard != null && PageCount > 1)
                {
                    int turn = (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame ? 1 : 0) - (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame ? 1 : 0);
                    if (turn != 0) { page = Mathf.Clamp(page + turn, 0, PageCount - 1); ShowPageImage(); if (panelView != null) panelView.SetMessage(Message()); }
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f) distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * .06f, viewDistance.x, viewDistance.y);
            }
            view.localPosition = Vector3.Lerp(view.localPosition, new Vector3(0, 0, distance), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            WatchSecret();
        }

        // 새김 글자 면을 화면 가운데에서 정면으로 잠깐 마주 보면 찾은 것으로 본다.
        void WatchSecret()
        {
            if (secretCopy == null) return;
            if (!SecretSeen)
            {
                var cam = view.parent; var point = secretCopy.position;
                var vp = cam.GetComponent<Camera>() is { } c ? c.WorldToViewportPoint(point) : new Vector3(.5f, .5f, 1f);
                bool facing = Vector3.Dot(secretCopy.up, (cam.position - point).normalized) > .8f;
                bool centered = vp.z > 0 && vp.x > .2f && vp.x < .8f && vp.y > .2f && vp.y < .8f;
                secretHeld = facing && centered ? secretHeld + Time.unscaledDeltaTime : 0f;
                if (secretHeld >= .4f)
                {
                    SecretsSeen.Add(targetId);
                    InspectionObserved?.Invoke(this, "SECRET_FOUND", "SECRET_FACE_HELD", InspectionMs);
                    secretFoundAt = Time.unscaledTime;
                    if (panelView != null) panelView.SetMessage(Message());
                }
            }
            // 찾은 순간 밝게 번쩍인 뒤 은은하게 빛난다.
            if (SecretSeen && secretParts.Count > 0)
            {
                float t = Time.unscaledTime - secretFoundAt;
                float k = t < 1.2f ? Mathf.Lerp(3f, .6f, t / 1.2f) * (.75f + .25f * Mathf.Cos(t * 18f)) : .6f;
                secretBlock ??= new MaterialPropertyBlock();
                foreach (var r in secretParts) { if (r == null) continue; r.GetPropertyBlock(secretBlock); secretBlock.SetColor("_EmissionColor", SecretGlow * k); r.SetPropertyBlock(secretBlock); }
            }
        }

        static readonly Color SecretGlow = new(1f, .82f, .35f);

        string Message()
        {
            string text = Sentences(Description);
            var book = Pages;
            int total = PageCount;
            if (total > 0)
            {
                page = Mathf.Clamp(page, 0, total - 1);
                string body = page < book.Length ? book[page] + "\n" : "";
                text = (string.IsNullOrEmpty(text) ? "" : text + "\n") + body + "<color=#B8B29C>"
                    + WaitingRoomText.Format("clue.page-help", "← →: 쪽 넘기기 ({n}/{total})", ("n", page + 1), ("total", total)) + "</color>";
            }
            if (SecretSeen && !string.IsNullOrEmpty(SecretText)) text = (string.IsNullOrEmpty(text) ? "" : text + "\n") + "<color=#FFD45A>" + SecretText + "</color>";
            // 조작법은 설명 아래 한 줄 띄우고 적는다(2026-11-13 사용자).
            return (string.IsNullOrEmpty(text) ? "" : text + "\n\n") + "<color=#B8B29C>" + WaitingRoomText.Get("clue.inspect-help", "왼쪽 끌기: 회전   휠: 확대/축소   ESC·F·오른쪽 클릭: 내려놓기") + "</color>";
        }

        // 설명은 문장마다 줄을 바꿔 보여 준다(2026-11-13 사용자). 문구 파일에서 \n으로 직접 나눈 설명은 그대로 둔다.
        static string Sentences(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("\\n", "\n");
            return text.Contains("\n") ? text : System.Text.RegularExpressions.Regex.Replace(text, @"(?<=[^.…\s][.?!]) +", "\n");
        }

        /// <summary>내 화면에서만 오버뷰를 연다/닫는다.</summary>
        public void SetOpen(bool value) => SetOpen(value, "EXTERNAL");

        void SetOpen(bool value, string reason)
        {
            if (value == IsOpen) return;
            if (value)
            {
                var cam = Camera.main;
                if (cam == null || Current != null || !Revealed || !PlayerInteraction.CanInteract(LocalReader())) return;
                if (!Discovered) ExplorationState.Instance?.RequestUse(targetId);
                Current = this; openedFrame = Time.frameCount;
                openedBy = NetworkPlayer.Local; openedByNetworkPlayer = openedBy != null;
                distance = Mathf.Lerp(viewDistance.x, viewDistance.y, .4f);
                BuildView(cam);
                blocker.SetOpen(true);
                Inspected?.Invoke(this);
                if (panelView == null) panelView = gameObject.AddComponent<ExplorationPanel>();
                page = 0; pageCache = null;
                ShowPageImage();
                // 글귀가 있는 노트만 아래 판을 키운다. 그림 쪽만 있는 노트는 그림이 가려지지 않게 작은 판을 쓴다.
                panelView.Open(Title, Message(), false, null, () => SetOpen(false, "PANEL_BUTTON"), true, null, Pages.Length > 0, HasSecret ? 1 : 0);
                openedAt = Time.realtimeSinceStartupAsDouble;
                InspectionObserved?.Invoke(this, "OPENED", "OVERVIEW", null);
            }
            else
            {
                if (Current == this) Current = null;
                openedBy = null; openedByNetworkPlayer = false;
                InspectionObserved?.Invoke(this, "CLOSED", reason, InspectionMs);
                if (panelView != null) panelView.Hide();
                if (studio != null) Destroy(studio);
                if (studioFeed != null) { studioFeed.Release(); Destroy(studioFeed); }
                studio = null; studioFeed = null;
                view = pivot = secretCopy = null; pageSurfaceCopy = pageBackCopy = null; secretParts.Clear(); secretHeld = 0f;
                if (blocker != null) blocker.SetOpen(false);
            }
        }
        void OnDestroy() { if (studio != null) Destroy(studio); if (studioFeed != null) { studioFeed.Release(); Destroy(studioFeed); } }

        GameObject studio;
        RenderTexture studioFeed;

        // 단서 사본(렌더러만)을 장면 밖 빈 곳에 두고 전용 카메라로 찍어 화면 전체에 띄운다.
        // 예전에는 내 카메라 앞 장면 속에 띄워서 벽·책상에 가려지거나 어두운 막 밖으로 장면이 비쳤다(2026-11-13 사용자).
        void BuildView(Camera main)
        {
            studio = new GameObject("ClueStudio");
            studio.transform.position = new Vector3(0, -2000f, 0);
            var eye = studio.AddComponent<Camera>();
            eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(.02f, .02f, .025f, 1f);
            eye.fieldOfView = main.fieldOfView; eye.nearClipPlane = .03f; eye.farClipPlane = 10f; eye.useOcclusionCulling = false;
            eye.allowHDR = main.allowHDR; eye.depth = main.depth - 10;
            studioFeed = new RenderTexture(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 24) { name = "ClueStudioFeed" };
            eye.targetTexture = studioFeed;

            var canvasGo = new GameObject("ClueStudioCanvas", typeof(Canvas)); canvasGo.transform.SetParent(studio.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 350; // 설명 판(360) 바로 아래
            var shot = new GameObject("Feed", typeof(RectTransform), typeof(UnityEngine.UI.RawImage)); shot.transform.SetParent(canvasGo.transform, false);
            var sr = (RectTransform)shot.transform; sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
            var raw = shot.GetComponent<UnityEngine.UI.RawImage>(); raw.texture = studioFeed; raw.raycastTarget = false;

            view = new GameObject("ClueOverview").transform;
            view.SetParent(studio.transform, false);
            view.localPosition = new Vector3(0, 0, viewDistance.y);
            pivot = new GameObject("Pivot").transform; pivot.SetParent(view, false);

            var source = Body; var bounds = Measure(source);
            // 메시·재질만 옮긴 사본(충돌·스크립트 없음).
            var copy = new GameObject("ClueCopy"); copy.transform.SetParent(pivot, false);
            Transform textOnly = null; bool anySolid = false;
            foreach (var r in source.GetComponentsInChildren<MeshRenderer>())
            {
                // 꺼 둔 받침판(벽 낙서 자리 등)은 빼고 그려진 그림만 보인다.
                var mf = r.GetComponent<MeshFilter>(); if (!r.enabled || mf == null || mf.sharedMesh == null) continue;
                var part = new GameObject(r.name).transform; part.SetParent(copy.transform, false);
                part.localPosition = source.InverseTransformPoint(r.transform.position);
                part.localRotation = Quaternion.Inverse(source.rotation) * r.transform.rotation;
                var ls = r.transform.lossyScale; var ss = source.lossyScale;
                part.localScale = new Vector3(ls.x / Mathf.Max(1e-5f, ss.x), ls.y / Mathf.Max(1e-5f, ss.y), ls.z / Mathf.Max(1e-5f, ss.z));
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var pr = part.gameObject.AddComponent<MeshRenderer>(); pr.sharedMaterials = r.sharedMaterials;
                pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;
                if (secretMark != null && r.transform.IsChildOf(secretMark)) { pr.sharedMaterial = SecretMaterial(r.sharedMaterial); secretParts.Add(pr); }
                if (pageSurface != null && r == pageSurface) pageSurfaceCopy = pr;
                if (r.GetComponent<TMPro.TMP_Text>() != null) { if (textOnly == null) textOnly = part; } else anySolid = true;
                // 종이처럼 두께 없는 한 장짜리 면은 뒤에서 보면 그려지지 않아 사라졌다(2026-11-14 사용자). 뒤집은 면을 한 장 더 붙여 뒷면에서도 보이게 한다.
                if (r.GetComponent<TMPro.TMP_Text>() == null && BackFace(mf.sharedMesh) is Mesh back)
                {
                    var bp = new GameObject(r.name + "_Back"); bp.transform.SetParent(part, false);
                    bp.AddComponent<MeshFilter>().sharedMesh = back;
                    var br = bp.AddComponent<MeshRenderer>(); br.sharedMaterials = pr.sharedMaterials;
                    br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false;
                    if (pr == pageSurfaceCopy) pageBackCopy = br;
                }
            }
            if (secretMark != null)
            {
                // 새김 면의 중심·바깥 방향을 사본에 옮겨 둔다(시선 판정용).
                secretCopy = new GameObject("SecretFace").transform; secretCopy.SetParent(copy.transform, false);
                secretCopy.localPosition = source.InverseTransformPoint(secretMark.position);
                secretCopy.localRotation = Quaternion.Inverse(source.rotation) * secretMark.rotation;
            }
            copy.transform.localRotation = ViewRotation(source.rotation, viewFace != null ? viewFace.rotation : (Quaternion?)null, viewEuler, main.transform.eulerAngles.y);
            // 벽 낙서처럼 글자만 있는 단서(2026-11-15 사용자: 살펴보기 화면이 검게 나옴): 글자는 한쪽 면만 그려져서, 비스듬히·뒤에서 보면 아무것도 안 보였다.
            // 글자 앞면이 늘 보는 사람을 향하게 놓고, 꺼 둔 받침판(벽 조각)을 뒤에 깔아 글자가 읽히게 한다.
            if (textOnly != null && !anySolid)
            {
                copy.transform.localRotation = Quaternion.Inverse(textOnly.localRotation);
                // 받침: 글자 크기보다 조금 넓은 벽 조각을 글자 바로 뒤에 깐다(꺼 둔 받침판의 재질을 빌린다).
                var textMesh = textOnly.GetComponent<MeshFilter>().sharedMesh; var tb = textMesh.bounds;
                Material wallMaterial = null;
                foreach (var r in source.GetComponentsInChildren<MeshRenderer>())
                    if (!r.enabled && r.GetComponent<TMPro.TMP_Text>() == null && r.sharedMaterial != null) { wallMaterial = r.sharedMaterial; break; }
                var backing = GameObject.CreatePrimitive(PrimitiveType.Quad); backing.name = "TextBacking";
                var collider = backing.GetComponent<Collider>(); if (collider != null) Destroy(collider);
                backing.transform.SetParent(textOnly, false);
                backing.transform.localPosition = new Vector3(tb.center.x, tb.center.y, tb.center.z + .01f);
                backing.transform.localRotation = Quaternion.identity;
                backing.transform.localScale = new Vector3(tb.size.x * 1.3f + .06f, tb.size.y * 1.5f + .06f, 1f);
                var br2 = backing.GetComponent<MeshRenderer>(); if (wallMaterial != null) br2.sharedMaterial = wallMaterial;
                br2.shadowCastingMode = ShadowCastingMode.Off; br2.receiveShadows = false;
            }
            // 스스로 빛나게(2026-11-15 사용자: 신문·지도가 어둡게 나옴): 살펴보기 조명이 적용되지 않는 상황에서는 주변광만 받아 거의 안 보였다.
            // 사본 재질에 제 그림을 빛으로 얹어 조명과 상관없이 읽히게 하고, 조명은 입체감을 주는 정도로만 쓴다.
            foreach (var r in copy.GetComponentsInChildren<MeshRenderer>())
            {
                if (secretParts.Contains(r)) continue;
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++) { var g = SelfLit(mats[i]); if (g != mats[i]) { mats[i] = g; changed = true; } }
                if (changed) r.sharedMaterials = mats;
            }
            var lamp = new GameObject("InspectLight").AddComponent<Light>();
            lamp.transform.SetParent(view, false); lamp.transform.localPosition = new Vector3(.25f, .3f, -.35f);
            lamp.type = LightType.Point; lamp.range = 2f; lamp.intensity = inspectLight * LampShare; lamp.color = new Color(1f, .95f, .85f); lamp.shadows = LightShadows.None;
            // 장면 조명이 닿지 않는 곳이라 반대쪽에서 약하게 받쳐 준다.
            var fill = new GameObject("InspectFill").AddComponent<Light>();
            fill.transform.SetParent(view, false); fill.transform.localPosition = new Vector3(-.35f, -.1f, -.3f);
            fill.type = LightType.Point; fill.range = 2f; fill.intensity = inspectLight * .35f * LampShare; fill.color = new Color(.85f, .9f, 1f); fill.shadows = LightShadows.None;
        }

        /// <summary>
        /// 살펴보는 사본의 처음 방향(카메라 기준). 정면 면이 있으면 그 면의 -Z가 보는 사람을 향하고 +Y가 위가 된다.
        /// 없으면 놓인 모습 그대로(보는 사람의 좌우 방향만 맞춰) 살짝 위에서 본다.
        /// </summary>
        public static Quaternion ViewRotation(Quaternion body, Quaternion? face, Vector3 extraEuler, float viewerYaw)
        {
            if (face is Quaternion f) return Quaternion.Euler(extraEuler) * Quaternion.Inverse(Quaternion.Inverse(body) * f);
            return Quaternion.Euler(extraEuler) * Quaternion.Euler(-18f, 0, 0) * Quaternion.Inverse(Quaternion.Euler(0, viewerYaw, 0)) * body;
        }

        static readonly System.Collections.Generic.Dictionary<Mesh, Mesh> backFaces = new();

        // 두께 없는 면(Quad 등)의 뒤집은 사본: 삼각형 방향과 법선을 반대로 한다. 두께 있는 물체·읽을 수 없는 메시는 null.
        static Mesh BackFace(Mesh source)
        {
            if (source == null) return null;
            if (backFaces.TryGetValue(source, out var cached)) return cached;
            Mesh made = null;
            var size = source.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z)), thinnest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            if (source.isReadable && longest > 0f && thinnest <= longest * .01f)
            {
                made = Instantiate(source); made.name = source.name + "_Back"; made.hideFlags = HideFlags.DontSave;
                var normals = made.normals; for (int i = 0; i < normals.Length; i++) normals[i] = -normals[i];
                made.normals = normals;
                for (int sub = 0; sub < made.subMeshCount; sub++)
                {
                    var tris = made.GetTriangles(sub);
                    for (int i = 0; i + 2 < tris.Length; i += 3) (tris[i], tris[i + 1]) = (tris[i + 1], tris[i]);
                    made.SetTriangles(tris, sub);
                }
            }
            return backFaces[source] = made;
        }

        static readonly System.Collections.Generic.Dictionary<Material, Material> secretMaterials = new();

        // 새김 글자 사본 재질: 원래 색 그대로, 찾았을 때만 발광색을 올린다.
        const float SelfLight = .6f, LampShare = .4f;
        static readonly System.Collections.Generic.Dictionary<Material, Material> selfLit = new();
        // 제 그림(_BaseMap)을 빛으로 얹은 사본 재질. 이미 빛나는 재질이나 글자(TMP) 재질은 그대로 둔다.
        static Material SelfLit(Material source)
        {
            if (source == null || !source.HasProperty("_EmissionColor") || !source.HasProperty("_BaseMap") || source.IsKeywordEnabled("_EMISSION")) return source;
            if (selfLit.TryGetValue(source, out var m) && m != null) return m;
            m = new Material(source) { name = "ClueSelfLit_" + source.name, hideFlags = HideFlags.DontSave };
            m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", source.GetTexture("_BaseMap"));
            var tint = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
            m.SetColor("_EmissionColor", new Color(tint.r * SelfLight, tint.g * SelfLight, tint.b * SelfLight, 1f));
            return selfLit[source] = m;
        }

        static Material SecretMaterial(Material source)
        {
            if (source == null) return null;
            if (secretMaterials.TryGetValue(source, out var m) && m != null) return m;
            m = new Material(source) { name = "ClueSecret_" + source.name, hideFlags = HideFlags.DontSave };
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return secretMaterials[source] = m;
        }

        static Bounds Measure(Transform t)
        {
            Bounds? b = null;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
                if (r.enabled) { if (b is { } x) { x.Encapsulate(r.bounds); b = x; } else b = r.bounds; }
            return b ?? new Bounds(t.position, Vector3.one * .1f);
        }

        static Texture2D flashTexture;

        // 가장자리가 진하고 가운데가 빈 금빛 테두리.
        static void DrawFlash(float alpha)
        {
            if (flashTexture == null)
            {
                const int n = 64; flashTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs(x / (n - 1f) - .5f) * 2f, dy = Mathf.Abs(y / (n - 1f) - .5f) * 2f;
                    float edge = Mathf.Clamp01((Mathf.Max(dx, dy) - .55f) / .45f);
                    flashTexture.SetPixel(x, y, new Color(SecretGlow.r, SecretGlow.g, SecretGlow.b, edge * edge));
                }
                flashTexture.Apply();
            }
            var old = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(alpha) * .85f); GUI.depth = -100;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), flashTexture, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        static PlayerInputReader LocalReader()
        {
            if (NetworkPlayer.All.Count > 0) return NetworkPlayer.Local != null ? NetworkPlayer.Local.GetComponent<PlayerInputReader>() : null;
            return CurtainCall.Player.OfflineLocalPlayer.Reader;
        }

        void OnGUI()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            textStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperCenter, wordWrap = true, normal = { textColor = new Color(.9f, .88f, .82f) } };
            if (IsOpen)
            {
                // 새김 글자를 찾은 순간 화면 가장자리가 금빛으로 번쩍인다.
                float flash = 1f - (Time.unscaledTime - secretFoundAt) / .8f;
                if (flash > 0f) DrawFlash(flash);
                if (panelView != null) return;
                GUI.Label(new Rect(0, Screen.height * .08f, Screen.width, 36), Title, titleStyle);
                if (!string.IsNullOrEmpty(Description)) GUI.Label(new Rect(Screen.width * .2f, Screen.height * .78f, Screen.width * .6f, 80), Description, textStyle);
                GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 28), "왼쪽 끌기: 돌려 보기   휠: 확대/축소   ESC·F·오른쪽 클릭: 내려놓기", textStyle);
                return;
            }
            if (inReach && !ShopReach.HasExternalPresentation && !InputGate.IsBlocked)
                GUI.Box(new Rect((Screen.width - 260) / 2f, Screen.height * .62f, 260, 28), "[F] " + ActionLabel);
        }
    }
}
