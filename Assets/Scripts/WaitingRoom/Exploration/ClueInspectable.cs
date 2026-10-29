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
    /// 팀 공유 발견 상태는 4단계(런 내 진행 유지·다인원)에서 붙인다. 지금은 내 화면의 <see cref="Discovered"/>만 남긴다.
    /// </summary>
    public sealed class ClueInspectable : MonoBehaviour, IShopTarget, IShopPresentation
    {
        [Tooltip("보여 줄 단서 몸체(없으면 이 오브젝트).")]
        [SerializeField] Transform body;

        [Tooltip("단서 이름(F 안내·오버뷰 제목).")]
        [SerializeField] string title = "단서";

        [Tooltip("오버뷰 아래에 보일 설명. 비우면 제목만.")]
        [SerializeField, TextArea] string description = "";

        [Tooltip("단서에 닿는 수평 거리(m).")]
        [SerializeField, Min(.5f)] float reach = 1.6f;

        [Tooltip("눈앞에 띄울 때 단서의 가장 긴 변 크기(m).")]
        [SerializeField, Min(.05f)] float viewSize = .32f;

        [Tooltip("카메라에서 단서까지 거리(m). 휠로 이 범위 안에서 당기고 민다.")]
        [SerializeField] Vector2 viewDistance = new(.35f, .9f);

        [Tooltip("마우스 끌기 회전 속도(도/픽셀).")]
        [SerializeField, Min(.01f)] float rotateSpeed = .35f;

        public static event Action<ClueInspectable> Inspected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Inspected = null; }

        /// <summary>지금 내 화면에서 보고 있는 단서(없으면 null).</summary>
        public static ClueInspectable Current { get; private set; }

        /// <summary>내 화면에서 한 번이라도 열어 본 적이 있는가.</summary>
        public bool Discovered { get; private set; }
        public bool IsOpen => Current == this;
        public string Title => title;

        public Transform VisualRoot => Body;
        public Vector3 PresentationPoint => Body.position + Vector3.up * .25f;
        public bool IsAvailable => Current == null;
        public string ActionLabel => $"{title} 살펴보기";

        Transform Body => body != null ? body : transform;

        UIInputBlocker blocker;
        Transform view, pivot;
        float distance;
        bool inReach;
        int openedFrame;
        GUIStyle titleStyle, textStyle;

        void Awake()
        {
            blocker = gameObject.AddComponent<UIInputBlocker>();
            blocker.BlockWhileActive = false;
        }

        void OnEnable() => ShopReach.Add(this);
        void OnDisable() { ShopReach.Remove(this); SetOpen(false); }

        public float Reach(Vector3 feet)
        {
            if (Current != null) return -1f;
            var p = Body.position; float d = new Vector2(feet.x - p.x, feet.z - p.z).magnitude;
            return d <= reach ? d : -1f;
        }

        public void Interact() { if (Current == null) SetOpen(true); }

        void Update()
        {
            ShopReach.Pump();
            inReach = ShopReach.Focused == (IShopTarget)this;
            if (!IsOpen) return;

            var reader = LocalReader();
            bool pushedAway = reader == null || new Vector2(reader.transform.position.x - Body.position.x, reader.transform.position.z - Body.position.z).magnitude > reach + .6f;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            bool close = pushedAway || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (Time.frameCount > openedFrame + 1 && keyboard != null && keyboard.fKey.wasPressedThisFrame)
                || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            if (close) { SetOpen(false); return; }

            if (mouse != null)
            {
                if (mouse.leftButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue() * rotateSpeed;
                    var cam = view.parent;
                    pivot.Rotate(cam.up, -delta.x, Space.World);
                    pivot.Rotate(cam.right, delta.y, Space.World);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f) distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * .06f, viewDistance.x, viewDistance.y);
            }
            view.localPosition = Vector3.Lerp(view.localPosition, new Vector3(0, 0, distance), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
        }

        /// <summary>내 화면에서만 오버뷰를 연다/닫는다.</summary>
        public void SetOpen(bool value)
        {
            if (value == IsOpen) return;
            if (value)
            {
                var cam = Camera.main;
                if (cam == null || Current != null) return;
                Current = this; Discovered = true; openedFrame = Time.frameCount;
                distance = Mathf.Lerp(viewDistance.x, viewDistance.y, .4f);
                BuildView(cam.transform);
                blocker.SetOpen(true);
                Inspected?.Invoke(this);
            }
            else
            {
                if (Current == this) Current = null;
                if (view != null) Destroy(view.gameObject);
                view = pivot = null;
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
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var part = new GameObject(r.name).transform; part.SetParent(copy.transform, false);
                part.localPosition = source.InverseTransformPoint(r.transform.position);
                part.localRotation = Quaternion.Inverse(source.rotation) * r.transform.rotation;
                var ls = r.transform.lossyScale; var ss = source.lossyScale;
                part.localScale = new Vector3(ls.x / Mathf.Max(1e-5f, ss.x), ls.y / Mathf.Max(1e-5f, ss.y), ls.z / Mathf.Max(1e-5f, ss.z));
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var pr = part.gameObject.AddComponent<MeshRenderer>(); pr.sharedMaterials = r.sharedMaterials;
                pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;
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
            lamp.type = LightType.Point; lamp.range = 2f; lamp.intensity = 2.5f; lamp.color = new Color(1f, .95f, .85f); lamp.shadows = LightShadows.None;
        }

        static Material shade;

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
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one * .1f);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
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
                GUI.Label(new Rect(0, Screen.height * .08f, Screen.width, 36), title, titleStyle);
                if (!string.IsNullOrEmpty(description)) GUI.Label(new Rect(Screen.width * .2f, Screen.height * .78f, Screen.width * .6f, 80), description, textStyle);
                GUI.Label(new Rect(0, Screen.height - 40, Screen.width, 28), "왼쪽 끌기: 돌려 보기   휠: 확대/축소   ESC·F·오른쪽 클릭: 내려놓기", textStyle);
                return;
            }
            if (inReach && !ShopReach.HasExternalPresentation && !InputGate.IsBlocked)
                GUI.Box(new Rect((Screen.width - 260) / 2f, Screen.height * .62f, 260, 28), "[F] " + ActionLabel);
        }
    }
}
