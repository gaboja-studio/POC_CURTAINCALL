using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 숙소 TV 화면(2026-11-14 사용자). 테이프가 들어가 있는 동안(openedBy 대상이 열려 있는 동안) 화면에 직접 영상을 띄운다.
    /// 상태는 탐색 장부로 공유되므로 모든 화면에서 같이 켜지고 꺼진다. 영상(clip)이 없으면 지직거리는 노이즈를 보여 준다.
    /// </summary>
    public sealed class TapeScreen : MonoBehaviour
    {
        [Tooltip("이 대상이 열려 있는 동안 재생한다(TV의 테이프 투입구).")]
        [SerializeField] string openedBy = "dorm:tv";
        [Tooltip("영상을 띄울 화면 면(Quad).")]
        [SerializeField] Renderer screen;
        [Tooltip("재생할 영상. 비우면 노이즈.")]
        [SerializeField] VideoClip clip;
        [Tooltip("화면 빛(켜진 동안만).")]
        [SerializeField] Light glow;
        [SerializeField, Min(0f)] float glowIntensity = 1.4f;

        Material offMaterial, playMaterial;
        Texture2D noise;
        VideoPlayer player;
        RenderTexture feed;
        bool playing;
        float nextNoise;

        void Awake()
        {
            if (screen != null) offMaterial = screen.sharedMaterial;
            Apply(false);
        }

        // ---- 화면 들여다보기(2026-11-15 사용자) ----
        // 켜진 TV에 F를 짧게 누르면 기계실 모니터처럼 화면을 가까이서 본다(내 화면에서만, 아래에 설명 판).
        // 다시 F를 짧게 누르거나 ESC면 닫힌다. F를 길게 누르면 테이프를 꺼낸다. 누가 테이프를 꺼내면 보던 사람의 화면도 닫힌다.
        [Header("들여다보기")]
        [SerializeField] string viewTitle = "낡은 TV";
        [SerializeField, TextArea] string viewText = "화면이 지직거린다.\nF를 꾹 눌러서 비디오 테이프 꺼내기";
        [Tooltip("테이프를 꺼내려면 F를 누르고 있어야 하는 시간(초).")]
        [SerializeField, Min(.2f)] float removeHoldSeconds = .8f;
        ExplorationGate viewGate; Camera lookCamera; ExplorationPanel panel; UIInputBlocker blocker;
        bool waitRelease; float heldFor;
        public bool Viewing => viewGate != null;

        public void Open(ExplorationGate gate)
        {
            var main = Camera.main;
            if (Viewing || gate == null || screen == null || main == null || !playing) return;
            viewGate = gate; waitRelease = true; heldFor = 0f;
            var b = screen.bounds; Vector3 outward = -screen.transform.forward;
            float distance = b.size.y * .5f / Mathf.Tan(main.fieldOfView * .5f * Mathf.Deg2Rad) / .6f;
            lookCamera = new GameObject("TvLookCamera").AddComponent<Camera>();
            lookCamera.CopyFrom(main); lookCamera.targetTexture = null; lookCamera.depth = main.depth + 10; lookCamera.useOcclusionCulling = false;
            lookCamera.nearClipPlane = Mathf.Max(.03f, distance - .25f);
            // 아래쪽 설명 판에 가리지 않게 화면을 조금 위쪽에 둔다.
            lookCamera.transform.SetPositionAndRotation(b.center + outward * distance + Vector3.down * (b.size.y * .28f), Quaternion.LookRotation(-outward, Vector3.up));
            if (blocker == null) { blocker = gameObject.AddComponent<UIInputBlocker>(); blocker.BlockWhileActive = false; }
            blocker.SetOpen(true);
            if (panel == null) panel = gameObject.AddComponent<ExplorationPanel>();
            panel.Open(WaitingRoomText.Get("target." + openedBy + ".name", viewTitle), WaitingRoomText.Get("target." + openedBy + ".view-text", viewText).Replace("\\n", "\n"), false, null, Close, true);
        }

        public void Close()
        {
            if (!Viewing) return;
            viewGate = null;
            if (lookCamera != null) { Destroy(lookCamera.gameObject); lookCamera = null; }
            if (panel != null) panel.Hide();
            if (blocker != null) blocker.SetOpen(false);
        }

        void TickView()
        {
            // 누가 테이프를 꺼냈으면(화면이 꺼지면) 강제로 닫는다.
            if (!playing) { Close(); return; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            var local = CurtainCall.Network.PlayerSync.NetworkPlayer.Local;
            var reader = local != null ? local.GetComponent<PlayerInputReader>() : OfflineLocalPlayer.Reader;
            if (reader == null) return;
            bool pressed = reader.IsPressed("Interact");
            if (waitRelease) { if (!pressed) waitRelease = false; return; } // 화면을 연 그 입력은 넘긴다
            if (pressed)
            {
                heldFor += Time.unscaledDeltaTime;
                if (heldFor >= removeHoldSeconds) { var gate = viewGate; Close(); gate.HoldInteract(); }
            }
            else
            {
                // 짧게 눌렀다 뗐으면 닫는다.
                if (heldFor > 0f) Close();
                heldFor = 0f;
            }
        }

        void Update()
        {
            bool on = ExplorationGate.IsAnyOpen(openedBy);
            if (on != playing) Apply(on);
            if (Viewing) TickView();
            if (!playing || clip != null || playMaterial == null || Time.unscaledTime < nextNoise) return;
            // 노이즈: 무늬를 매번 다른 자리로 옮겨 지직거리게 한다.
            nextNoise = Time.unscaledTime + .05f;
            playMaterial.mainTextureOffset = new Vector2(Random.value, Random.value);
            if (glow != null) glow.intensity = glowIntensity * Random.Range(.75f, 1.1f);
        }

        void Apply(bool on)
        {
            playing = on;
            if (glow != null) glow.enabled = on;
            if (screen == null) return;
            if (!on)
            {
                if (player != null) player.Stop();
                if (offMaterial != null) screen.sharedMaterial = offMaterial;
                return;
            }
            if (playMaterial == null)
            {
                playMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "TapeScreenPlay", hideFlags = HideFlags.DontSave };
                playMaterial.color = Color.white;
            }
            if (clip != null)
            {
                if (feed == null) { feed = new RenderTexture(640, 480, 0) { name = "TapeScreenFeed" }; feed.Create(); }
                if (player == null)
                {
                    player = gameObject.AddComponent<VideoPlayer>();
                    player.playOnAwake = false; player.isLooping = true; player.renderMode = VideoRenderMode.RenderTexture;
                    player.audioOutputMode = VideoAudioOutputMode.None;
                }
                player.clip = clip; player.targetTexture = feed; player.Play();
                playMaterial.mainTexture = feed; playMaterial.mainTextureScale = Vector2.one; playMaterial.mainTextureOffset = Vector2.zero;
            }
            else
            {
                if (noise == null)
                {
                    const int n = 128;
                    noise = new Texture2D(n, n, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
                    var pixels = new Color32[n * n];
                    for (int i = 0; i < pixels.Length; i++) { byte g = (byte)Random.Range(20, 235); pixels[i] = new Color32(g, g, g, 255); }
                    noise.SetPixels32(pixels); noise.Apply();
                }
                playMaterial.mainTexture = noise; playMaterial.mainTextureScale = new Vector2(1.4f, 1.05f);
            }
            screen.sharedMaterial = playMaterial;
        }

        void OnDisable() => Close();

        void OnDestroy()
        {
            if (playMaterial != null) Destroy(playMaterial);
            if (noise != null) Destroy(noise);
            if (feed != null) { feed.Release(); Destroy(feed); }
        }
    }
}
