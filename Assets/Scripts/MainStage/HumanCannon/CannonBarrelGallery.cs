using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CurtainCall.HumanCannon
{
    public sealed class CannonBarrelGallery : MonoBehaviour
    {
        public CannonBarrelPresentation Cannon;
        public Camera ViewCamera;
        public bool Paused { get; private set; }
        public float Elapsed { get; private set; }
        public float Speed { get; set; } = 1;
        public int Mode { get; private set; } = 4;
        UnityEngine.UI.Text status, pause;
        UnityEngine.UI.Slider scrub;
        Font font;
        bool updatingSlider;
        static readonly string[] Names = { "Active", "Inactive", "Fire + lower", "Recover (4 nods)", "Full cycle" };
        public float Duration => Mode == 2 ? 1.2f : Mode == 3 ? 2.8f : Mode == 4 ? 1 + CurtainCall.Settings.GameSettings.HumanCannon.CannonCooldown : 1;

        void Start() { Cannon.Preview = true; BuildControls(); Sample(); }
        void Update()
        {
            if (ViewCamera)
            {
                float panel = Mathf.Min(.43f, 290f * Screen.height / (900f * Screen.width));
                ViewCamera.rect = new Rect(panel, 0, 1 - panel, 1);
            }
            if (!Paused) Elapsed = Mathf.Repeat(Elapsed + Time.unscaledDeltaTime * Speed, Duration);
            Sample();
            if (status == null) return;
            status.text = Names[Mode] + "\n" + Cannon.Phase + $"   {Elapsed:0.00} / {Duration:0.00} s";
            pause.text = Paused ? "Play" : "Pause";
            updatingSlider = true; scrub.value = Elapsed / Duration; updatingSlider = false;
        }
        public void Select(int mode) { Mode = Mathf.Clamp(mode, 0, 4); Elapsed = 0; Paused = false; Sample(); }
        public void Seek(float normalized) { Paused = true; Elapsed = Mathf.Clamp01(normalized) * Duration; Sample(); }
        public void SetPaused(bool value) => Paused = value;
        void Sample()
        {
            if (!Cannon) return;
            Cannon.Preview = true;
            var phase = (CannonBarrelPhase)Mathf.Min(Mode, 3); float t = Elapsed / Duration;
            if (Mode == 4)
            {
                if (Elapsed < 1) { phase = CannonBarrelPhase.Active; t = 0; }
                else if (Elapsed < 2.2f) { phase = CannonBarrelPhase.FireDisable; t = (Elapsed - 1) / 1.2f; }
                else if (Elapsed < Duration - 2.8f) { phase = CannonBarrelPhase.Inactive; t = 0; }
                else { phase = CannonBarrelPhase.Recover; t = (Elapsed - (Duration - 2.8f)) / 2.8f; }
            }
            Cannon.Sample(phase, t);
        }
        public void SetCamera(float yaw)
        {
            if (!ViewCamera) return;
            ViewCamera.transform.rotation = Quaternion.Euler(18, yaw, 0);
            ViewCamera.transform.position = new Vector3(0, 1.2f, .15f) - ViewCamera.transform.forward * 12;
        }
        RectTransform Row(Transform parent, string name, float height)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rt.SetParent(parent, false);
            rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height; return rt;
        }
        UnityEngine.UI.Text Label(Transform parent, string value, float height = 35)
        {
            var text = Row(parent, value, height).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font; text.fontSize = 17; text.color = Color.white; text.text = value;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        UnityEngine.UI.Text Button(Transform parent, string value, Action action)
        {
            var row = Row(parent, value, 38); row.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.18f, .23f, .3f);
            row.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => action());
            var label = Label(row, value); var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; return label;
        }
        void BuildControls()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = new GameObject("Barrel Controls", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scale.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1400, 900); scale.matchWidthOrHeight = 1;
            var panel = Row(canvas.transform, "Panel", 0); panel.anchorMin = Vector2.zero; panel.anchorMax = new Vector2(0, 1);
            panel.pivot = new Vector2(0, .5f); panel.sizeDelta = new Vector2(290, 0); panel.anchoredPosition = Vector2.zero;
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.05f, .065f, .09f, .98f);
            var layout = panel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = 7;
            layout.padding = new RectOffset(15, 15, 20, 15); layout.childControlHeight = true; layout.childForceExpandHeight = false;
            Label(panel, "CANNON BARREL LAB", 50); status = Label(panel, "", 65);
            for (int i = 0; i < Names.Length; i++) { int mode = i; Button(panel, Names[i], () => Select(mode)); }
            pause = Button(panel, "Pause", () => Paused = !Paused); Button(panel, "Restart", () => Select(Mode));
            var rail = Row(panel, "Timeline", 25); rail.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.2f, .24f, .3f);
            scrub = rail.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var handle = Row(rail, "Handle", 0); handle.sizeDelta = new Vector2(14, 0);
            var image = handle.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(1, .7f, .3f);
            scrub.handleRect = handle; scrub.targetGraphic = image;
            scrub.onValueChanged.AddListener(v => { if (!updatingSlider) Seek(v); });
            Button(panel, "Speed: 0.25x", () => Speed = .25f); Button(panel, "Speed: 1x", () => Speed = 1);
            Button(panel, "Front / muzzle", () => SetCamera(180)); Button(panel, "Side", () => SetCamera(90));
            Button(panel, "3/4 view", () => SetCamera(135)); Button(panel, "Rear", () => SetCamera(0));
            Label(panel, "Scrub pauses playback.\nOnly the barrel moves.", 60);
            SetCamera(135);
            var events = new GameObject("Gallery EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
