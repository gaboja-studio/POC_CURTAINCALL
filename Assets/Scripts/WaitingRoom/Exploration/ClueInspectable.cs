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
        Renderer pageSurfaceCopy; MaterialPropertyBlock pageBlock;
        int PageCount => Mathf.Max(Pages.Length, pageImages != null ? pageImages.Length : 0);
        void ShowPageImage()
        {
            if (pageSurfaceCopy == null || pageImages == null || pageImages.Length == 0) return;
            var image = pageImages[Mathf.Clamp(page, 0, pageImages.Length - 1)]; if (image == null) return;
            pageBlock ??= new MaterialPropertyBlock(); pageSurfaceCopy.GetPropertyBlock(pageBlock);
            pageBlock.SetTexture("_BaseMap", image); pageBlock.SetTexture("_MainTex", image); pageSurfaceCopy.SetPropertyBlock(pageBlock);
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
        public Vector3 PresentationPoint => Body.position + Vector3.up * .25f;
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
            string text = Description;
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
            return (string.IsNullOrEmpty(text) ? "" : text + "\n") + WaitingRoomText.Get("clue.inspect-help", "왼쪽 끌기: 회전   휠: 확대/축소   ESC·F·오른쪽 클릭: 내려놓기");
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
                BuildView(cam.transform);
                blocker.SetOpen(true);
                Inspected?.Invoke(this);
                if (panelView == null) panelView = gameObject.AddComponent<ExplorationPanel>();
                page = 0; pageCache = null;
                ShowPageImage();
                // 글귀가 있는 노트만 아래 판을 키운다. 그림 쪽만 있는 노트는 그림이 가려지지 않게 작은 판을 쓴다.
                panelView.Open(Title, Message(), false, null, () => SetOpen(false, "PANEL_BUTTON"), true, null, Pages.Length > 0);
                openedAt = Time.realtimeSinceStartupAsDouble;
                InspectionObserved?.Invoke(this, "OPENED", "OVERVIEW", null);
            }
            else
            {
                if (Current == this) Current = null;
                openedBy = null; openedByNetworkPlayer = false;
                InspectionObserved?.Invoke(this, "CLOSED", reason, InspectionMs);
                if (panelView != null) panelView.Hide();
                if (view != null) Destroy(view.gameObject);
                view = pivot = secretCopy = null; pageSurfaceCopy = null; secretParts.Clear(); secretHeld = 0f;
                if (blocker != null) blocker.SetOpen(false);
            }
        }

        // 카메라 앞에 단서 사본(렌더러만), 장면을 어둡게 가리는 막, 단서를 비추는 작은 조명.
        void BuildView(Transform cam)
        {
            view = new GameObject("ClueOverview").transform;
            view.SetParent(cam, false);
            view.localPosition = new Vector3(0, 0, viewDistance.y);
            pivot = new GameObject("Pivot").transform; pivot.SetParent(view, false);

            var source = Body; var bounds = Measure(source);
            // 메시·재질만 옮긴 사본(충돌·스크립트 없음).
            var copy = new GameObject("ClueCopy"); copy.transform.SetParent(pivot, false);
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
            }
            if (secretMark != null)
            {
                // 새김 면의 중심·바깥 방향을 사본에 옮겨 둔다(시선 판정용).
                secretCopy = new GameObject("SecretFace").transform; secretCopy.SetParent(copy.transform, false);
                secretCopy.localPosition = source.InverseTransformPoint(secretMark.position);
                secretCopy.localRotation = Quaternion.Inverse(source.rotation) * secretMark.rotation;
            }
            copy.transform.localRotation = Quaternion.Inverse(cam.rotation) * source.rotation;
            float scale = viewSize / Mathf.Max(.001f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            copy.transform.localScale = source.lossyScale * scale;
            // 사본의 중심을 회전축에 맞춘다.
            copy.transform.localPosition = -(copy.transform.localRotation * Vector3.Scale(source.InverseTransformPoint(bounds.center), copy.transform.localScale));

            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(backdrop.GetComponent<Collider>());
            backdrop.name = "Backdrop"; backdrop.transform.SetParent(view, false);
            backdrop.transform.localPosition = new Vector3(0, 0, .35f); backdrop.transform.localRotation = Quaternion.identity; backdrop.transform.localScale = new Vector3(4, 4, 1);
            var br = backdrop.GetComponent<Renderer>(); br.sharedMaterial = Shade(); br.shadowCastingMode = ShadowCastingMode.Off;

            var lamp = new GameObject("InspectLight").AddComponent<Light>();
            lamp.transform.SetParent(view, false); lamp.transform.localPosition = new Vector3(.25f, .3f, -.35f);
            lamp.type = LightType.Point; lamp.range = 2f; lamp.intensity = inspectLight; lamp.color = new Color(1f, .95f, .85f); lamp.shadows = LightShadows.None;
        }

        static Material shade;
        static readonly System.Collections.Generic.Dictionary<Material, Material> secretMaterials = new();

        // 새김 글자 사본 재질: 원래 색 그대로, 찾았을 때만 발광색을 올린다.
        static Material SecretMaterial(Material source)
        {
            if (source == null) return null;
            if (secretMaterials.TryGetValue(source, out var m) && m != null) return m;
            m = new Material(source) { name = "ClueSecret_" + source.name, hideFlags = HideFlags.DontSave };
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return secretMaterials[source] = m;
        }

        // 장면을 어둡게 가리는 반투명 막 재질(한 번만 만든다).
        static Material Shade()
        {
            if (shade != null) return shade;
            shade = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "ClueOverviewShade" };
            shade.SetFloat("_Surface", 1); shade.SetFloat("_Blend", 0);
            shade.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); shade.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            shade.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); shade.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            shade.SetFloat("_ZWrite", 0); shade.SetOverrideTag("RenderType", "Transparent");
            shade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); shade.renderQueue = (int)RenderQueue.Transparent;
            shade.SetColor("_BaseColor", new Color(0, 0, 0, .72f));
            return shade;
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
            return FindFirstObjectByType<PlayerInputReader>();
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
