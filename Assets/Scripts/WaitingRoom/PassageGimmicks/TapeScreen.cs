using UnityEngine;
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

        void Update()
        {
            bool on = ExplorationGate.IsAnyOpen(openedBy);
            if (on != playing) Apply(on);
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

        void OnDestroy()
        {
            if (playMaterial != null) Destroy(playMaterial);
            if (noise != null) Destroy(noise);
            if (feed != null) { feed.Release(); Destroy(feed); }
        }
    }
}
