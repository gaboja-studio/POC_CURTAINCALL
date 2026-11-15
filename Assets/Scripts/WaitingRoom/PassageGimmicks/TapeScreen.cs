using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 숙소 TV 화면(2026-11-14 사용자). 테이프가 들어가 있는 동안(openedBy 대상이 열려 있는 동안) 화면에 직접 영상을 띄운다.
    /// 상태는 탐색 장부로 공유되므로 모든 화면에서 같이 켜지고 꺼진다. 영상(clip)이 없으면 지직거리는 노이즈를 보여 준다.
    /// 테이프가 들어 있는 동안 영상은 계속 반복되고, 뽑았다가 다시 넣으면 처음부터 재생한다(2026-11-15 사용자).
    /// 숙소에서 누군가 처음 TV에 다가가면 흑백 노이즈가 잠깐 나왔다가 전원이 끊긴다(<see cref="glitchId"/>, 게임을 켠 동안 한 번).
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
        bool playing, glitching;
        float nextNoise;

        void Awake()
        {
            if (screen != null) offMaterial = screen.sharedMaterial;
            Apply(false, false);
        }

        // ---- 첫 접근 오작동(2026-11-15 사용자) ----
        // 누군가 처음 TV 앞에 다가가면 모든 화면에서 흑백 노이즈가 glitchSeconds 동안 나오고 꺼진다.
        // 런이 끝나 탐색 기록이 비워져도 다시 나오지 않고, 게임을 껐다 켜면 다시 나온다.
        [Header("첫 접근 오작동")]
        [SerializeField] string glitchId = "dorm:tv-glitch";
        [Tooltip("TV 화면 앞 이 거리 안에 들어오면 오작동한다(m).")]
        [SerializeField, Min(.5f)] float glitchRange = 3f;
        [SerializeField, Min(.1f)] float glitchSeconds = 3f;
        static bool glitchSpent;
        bool glitchSeen, wasGlitched; float glitchUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => glitchSpent = false;

        bool Near(Vector3 feet)
        {
            // 화면 앞쪽에서만(벽 너머 뒤쪽은 제외).
            Vector3 to = feet + Vector3.up - screen.bounds.center;
            return to.sqrMagnitude <= glitchRange * glitchRange && Vector3.Dot(to, -screen.transform.forward) > 0f;
        }

        void TickGlitch()
        {
            var state = ExplorationState.Instance;
            if (state == null || !state.StateReady || screen == null) return; // 늦게 들어온 화면에서 다시 재생되지 않게 상태를 받은 뒤부터 본다
            bool glitched = state.IsOpen(glitchId);
            if (!glitched && !glitchSpent && state.HasAuthority)
            {
                bool near = false; int players = 0;
                foreach (var p in NetworkPlayer.All)
                {
                    if (p == null || !p.IsSpawned || p.State != PlayerState.Normal) continue;
                    players++; near |= Near(p.transform.position);
                }
                if (players == 0 && state.LocalFeet is Vector3 feet) near = Near(feet);
                if (near) state.MarkVisited(glitchId);
            }
            if (glitchSeen && glitched && !wasGlitched && !glitchSpent) glitchUntil = Time.unscaledTime + glitchSeconds;
            if (glitched) glitchSpent = true;
            glitchSeen = true; wasGlitched = glitched;
        }

        // ---- 화면 들여다보기(2026-11-15 사용자) ----
        // 켜진 TV에 F를 짧게 누르면 기계실 모니터처럼 화면을 가까이서 본다(내 화면에서만, 아래에 설명 판).
        // 다시 F를 짧게 누르거나 ESC·마우스 우클릭이면 닫힌다. F를 길게 누르면 테이프를 꺼낸다. 누가 테이프를 꺼내면 보던 사람의 화면도 닫힌다.
        // 설명 판은 단서 오버뷰와 같은 규칙: 설명 아래 한 줄 띄우고 조작법을 흐린 색으로 적는다(2026-11-15 사용자).
        [Header("들여다보기")]
        [SerializeField] string viewTitle = "낡은 TV";
        [SerializeField, TextArea] string viewText = "";
        [SerializeField] string viewHelp = "ESC·F·마우스 우클릭: 나가기   F 꾹 누르기: 비디오 테이프 꺼내기";
        [Tooltip("테이프를 꺼내려면 F를 누르고 있어야 하는 시간(초).")]
        [SerializeField, Min(.2f)] float removeHoldSeconds = .8f;
        ExplorationGate viewGate; Camera lookCamera; ExplorationPanel panel; UIInputBlocker blocker;
        bool waitRelease; float heldFor;
        public bool Viewing => viewGate != null;
        // 들여다보는 동안에는 캐릭터(나·동료)가 화면을 가리지 않게 내 화면에서만 숨긴다(2026-11-15 사용자).
        readonly System.Collections.Generic.List<Renderer> hidden = new();
        float nextHide;

        void HidePlayers()
        {
            nextHide = Time.unscaledTime + .5f; // 보는 중에 들어온 사람도 숨긴다
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null) continue;
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                    if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }
            }
        }

        void ShowPlayers()
        {
            foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
            hidden.Clear();
        }

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
            HidePlayers();
            if (panel == null) panel = gameObject.AddComponent<ExplorationPanel>();
            string text = WaitingRoomText.Get("target." + openedBy + ".view-text", viewText).Replace("\\n", "\n");
            panel.Open(WaitingRoomText.Get("target." + openedBy + ".name", viewTitle), (string.IsNullOrEmpty(text) ? "" : text + "\n\n")
                + "<color=#B8B29C>" + WaitingRoomText.Get("target." + openedBy + ".view-help", viewHelp) + "</color>", false, null, Close, true);
        }

        public void Close()
        {
            if (!Viewing) return;
            viewGate = null;
            ShowPlayers();
            if (lookCamera != null) { Destroy(lookCamera.gameObject); lookCamera = null; }
            if (panel != null) panel.Hide();
            if (blocker != null) blocker.SetOpen(false);
        }

        void TickView()
        {
            // 누가 테이프를 꺼냈으면(화면이 꺼지면) 강제로 닫는다.
            if (!playing) { Close(); return; }
            if (Time.unscaledTime >= nextHide) HidePlayers();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) { Close(); return; }
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
            // 테이프가 들어 있어도 숙소에 아무도 없으면 꺼 둔다. 누가 들어오면 처음부터 다시 재생한다.
            bool on = ExplorationGate.IsAnyOpen(openedBy) && Occupied();
            TickGlitch();
            bool glitch = !on && Time.unscaledTime < glitchUntil;
            if (on != playing || glitch != glitching) Apply(on, glitch);
            if (Viewing) TickView();
            if (!(glitching || (playing && clip == null)) || playMaterial == null || Time.unscaledTime < nextNoise) return;
            // 노이즈: 무늬를 매번 다른 자리로 옮겨 지직거리게 한다.
            nextNoise = Time.unscaledTime + .05f;
            playMaterial.mainTextureOffset = new Vector2(Random.value, Random.value);
            if (glow != null) glow.intensity = glowIntensity * Random.Range(.75f, 1.1f);
        }

        // ---- 빈 숙소에서는 끄기(2026-11-15 사용자: 최적화) ----
        [Header("빈 숙소에서 끄기")]
        [Tooltip("이 구역들에 한 명도 없으면 테이프가 들어 있어도 화면을 끄고 영상을 멈춘다. 비우면 늘 재생.")]
        [SerializeField] string[] roomZones = { "Area_S02_Dormitory", "F8_Dorm", "Area_Dormitory" };
        ExplorationTimerZones zones;

        bool Occupied()
        {
            if (roomZones.Length == 0) return true;
            if (zones == null) zones = FindFirstObjectByType<ExplorationTimerZones>();
            if (zones == null) return true; // 구역을 알 수 없으면 끄지 않는다
            int players = 0;
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                players++;
                if (System.Array.IndexOf(roomZones, zones.ZoneAt(p.transform.position)) >= 0) return true;
            }
            var state = ExplorationState.Instance;
            return players == 0 && (state == null || !(state.LocalFeet is Vector3 feet) || System.Array.IndexOf(roomZones, zones.ZoneAt(feet)) >= 0);
        }

        void Apply(bool on, bool glitch)
        {
            playing = on; glitching = glitch;
            if (glow != null) { glow.enabled = on || glitch; glow.intensity = glowIntensity; }
            if (screen == null) return;
            if (!on && !glitch)
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
            if (on && clip != null)
            {
                // 화면 비율에 맞춘 판에 영상을 꽉 채운다(넘치는 좌우는 잘림).
                if (feed == null) { var size = screen.bounds.size; float aspect = Mathf.Max(size.x, size.z) / Mathf.Max(.01f, size.y); feed = new RenderTexture(Mathf.RoundToInt(512 * aspect), 512, 0) { name = "TapeScreenFeed" }; feed.Create(); }
                // 앞서 보던 장면이 잠깐 비치지 않게 검게 비우고 처음부터 재생한다.
                var active = RenderTexture.active; RenderTexture.active = feed; GL.Clear(false, true, Color.black); RenderTexture.active = active;
                if (player == null)
                {
                    player = gameObject.AddComponent<VideoPlayer>();
                    player.playOnAwake = false; player.isLooping = true; player.renderMode = VideoRenderMode.RenderTexture;
                    player.audioOutputMode = VideoAudioOutputMode.None; player.aspectRatio = VideoAspectRatio.FitOutside;
                }
                player.Stop(); player.clip = clip; player.targetTexture = feed; player.Play();
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
