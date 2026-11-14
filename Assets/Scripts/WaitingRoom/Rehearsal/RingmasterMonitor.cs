using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 리허설을 마치고 무대 입구 통로에 다가가면 켜지는 모니터(Task-097). 치직거리는 잡음 화면 뒤에 싱긋 웃는 가면을 쓴 단장 얼굴이 나온다.
    /// 얼굴 그림과 잡음 소리는 아트·사운드가 없어 코드로 만든 임시 자원이다(교체 예정).
    /// </summary>
    public sealed class RingmasterMonitor : MonoBehaviour
    {
        const float StaticSeconds = 1.3f, NoiseStep = .05f;
        enum Mode { Off, Static, Face, Slide, Watch, Image }

        static Texture2D faceTexture;
        static AudioClip staticClip;

        Material screen;
        Texture2D noise;
        AudioSource audioSource;
        Mode mode;
        float modeStarted, nextNoise;
        bool externallyTimed;

        public bool IsOn => mode != Mode.Off;
        /// <summary>치직 잡음·웃는 얼굴 연출이 재생 중인가(이 동안에는 안내 화면·감시 화면으로 덮지 않는다).</summary>
        public bool IsPlayingFace => mode == Mode.Static || mode == Mode.Face;

        TMPro.TextMeshPro slideTitle, slideBody;
        Texture2D watchTexture;
        Color[] watchBase;
        Vector2 pupil, shownPupil = new(99f, 99f);
        string shownSlide;

        /// <summary>뒷면(본체 뒤) 두께(m). 벽걸이는 이 만큼 벽에서 띄워 뒷면이 벽에 닿게 둔다.</summary>
        public const float BackDepth = .15f;

        /// <summary>
        /// 모니터를 만든다. <paramref name="facing"/>은 화면이 바라보는 방향. <paramref name="hangLength"/>가 0보다 크면
        /// 화면 위쪽 끝에서 그 길이만큼 위로 매다는 막대를 단다(부르는 쪽이 실제 천장까지의 길이를 재서 넘긴다).
        /// </summary>
        public static RingmasterMonitor Create(Transform parent, string name, Vector3 center, Vector3 facing, Vector2 size, Material bezel, float hangLength)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(center, Quaternion.LookRotation(facing.normalized));
            var monitor = root.AddComponent<RingmasterMonitor>();
            monitor.Build(size, bezel, hangLength);
            return monitor;
        }

        void Build(Vector2 size, Material bezel, float hang)
        {
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Bezel"; Destroy(frame.GetComponent<Collider>());
            frame.transform.SetParent(transform, false);
            frame.transform.localPosition = new Vector3(0f, 0f, -.08f);
            frame.transform.localScale = new Vector3(size.x + .18f, size.y + .18f, .14f);
            if (bezel != null) frame.GetComponent<Renderer>().sharedMaterial = bezel;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Screen"; Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            // 쿼드는 -Z를 바라보므로 뒤집어 앞(+Z)으로 향하게 한다.
            quad.transform.localPosition = new Vector3(0f, 0f, .001f);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            screen = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, color = Color.black };
            quad.GetComponent<Renderer>().sharedMaterial = screen;
            if (hang > 0f)
            {
                // 모니터 암(2026-11-14 사용자): 천장 판 → 내려오는 기둥 → 본체 뒤 받침. 기둥은 본체 뒤쪽에 있어 화면을 가리지 않는다.
                void Part(string name, Vector3 at, Vector3 scale)
                {
                    var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    part.name = name; Destroy(part.GetComponent<Collider>());
                    part.transform.SetParent(transform, false); part.transform.localPosition = at; part.transform.localScale = scale;
                    if (bezel != null) part.GetComponent<Renderer>().sharedMaterial = bezel;
                }
                float top = size.y * .5f + .09f, thick = Mathf.Clamp(size.x * .03f, .08f, .2f), back = -.15f - thick * .5f;
                Part("ArmColumn", new Vector3(0f, (top + hang) * .5f, back), new Vector3(thick, top + hang, thick));
                Part("ArmMount", new Vector3(0f, 0f, back + thick * .25f), new Vector3(size.x * .3f, size.y * .3f, thick * .5f));
                Part("ArmCeilingPlate", new Vector3(0f, top + hang - .02f, back), new Vector3(thick * 4f, .04f, thick * 4f));
            }
            // 안내 화면 글자(공연 안내가 넘어가는 화면, 2026-11-14 사용자). 화면 면과 같은 방향으로 살짝 앞에 둔다.
            var overlay = new GameObject("Overlay").transform; overlay.SetParent(transform, false);
            overlay.localPosition = new Vector3(0f, 0f, .004f); overlay.localRotation = Quaternion.Euler(0f, 180f, 0f);
            // 한글 전체가 들어 있는 공통 글꼴(화면 UI의 대체 글꼴과 같은 것). 없으면 패널 글꼴.
            var font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF") ?? Resources.Load<TMPro.TMP_FontAsset>("Fonts/BasicPanels/Pretendard-Medium SDF");
            TMPro.TextMeshPro Text(string name, float y, float height, float maxSize, Color color)
            {
                var go = new GameObject(name); go.transform.SetParent(overlay, false); go.transform.localPosition = new Vector3(0f, y, 0f);
                var text = go.AddComponent<TMPro.TextMeshPro>(); if (font != null) text.font = font;
                text.rectTransform.sizeDelta = new Vector2(size.x * .88f, height); text.alignment = TMPro.TextAlignmentOptions.Center;
                text.enableAutoSizing = true; text.fontSizeMin = .5f; text.fontSizeMax = maxSize; text.color = color; text.text = ""; go.SetActive(false);
                return text;
            }
            slideTitle = Text("SlideTitle", size.y * .24f, size.y * .3f, size.y * 2.2f, new Color(1f, .86f, .5f));
            slideBody = Text("SlideBody", -size.y * .16f, size.y * .46f, size.y * 1.3f, new Color(.95f, .93f, .88f));
            noise = new Texture2D(96, 54, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = .85f; audioSource.volume = .7f;
            audioSource.minDistance = 4f; audioSource.maxDistance = 40f;
        }

        /// <summary>치직 소리와 함께 켜진다. 잡음 뒤 단장 얼굴.</summary>
        public void TurnOn()
        {
            if (IsPlayingFace) return;
            HideSlide();
            externallyTimed = false;
            mode = Mode.Static; modeStarted = Time.time; nextNoise = 0f;
            screen.color = Color.white;
            audioSource.clip = StaticClip();
            audioSource.Play();
        }

        /// <summary>호스트가 공유한 시작 시각 기준. 얼굴은 기획의 3초 후 꺼지고 재진입해도 다시 재생하지 않는다.</summary>
        public void PlayAt(float elapsed)
        {
            if (!externallyTimed) { if (elapsed < StaticSeconds) TurnOn(); externallyTimed = true; }
            RenderAt(elapsed);
        }

        void RenderAt(float elapsed)
        {
            if (elapsed >= StaticSeconds + 3f) { if (IsPlayingFace) TurnOff(); return; }
            if (elapsed < StaticSeconds)
            {
                mode = Mode.Static;
                screen.color = Color.white;
                if (Time.unscaledTime >= nextNoise) { FillNoise(); nextNoise = Time.unscaledTime + NoiseStep; }
                return;
            }
            mode = Mode.Face; screen.mainTexture = FaceTexture();
            float flicker = .88f + .12f * Mathf.PerlinNoise(Time.time * 6f, 0f);
            screen.color = new Color(flicker, flicker, flicker);
        }

        public void ResetPlayback() { TurnOff(); externallyTimed = false; }

        public void TurnOff()
        {
            mode = Mode.Off;
            HideSlide();
            if (screen != null) { screen.mainTexture = null; screen.mainTextureOffset = Vector2.zero; screen.color = Color.black; }
            if (audioSource != null) audioSource.Stop();
        }

        void Update()
        {
            if (!externallyTimed && IsPlayingFace) RenderAt(Time.time - modeStarted);
        }

        Texture shownImage;
        float burstUntil, nextBurst;
        /// <summary>
        /// 그림 한 장을 띄운다(2026-11-14 사용자: 단장 그림). 꺼져 있다가 켜질 때와 그림이 바뀔 때, 그리고 주기적으로 치직거리는 잡음이 잠깐 낀다.
        /// 켜질 때는 치직 소리가 길게, 그 밖에는 짧게 난다. 매 프레임 불러도 된다.
        /// </summary>
        public void ShowImage(Texture image)
        {
            if (screen == null || image == null) return;
            float now = Time.unscaledTime;
            if (mode != Mode.Image)
            {
                bool poweringOn = mode == Mode.Off;
                HideSlide(); mode = Mode.Image; shownImage = image; externallyTimed = false;
                Burst(poweringOn ? StaticSeconds : .45f, true);
            }
            else if (shownImage != image) { shownImage = image; Burst(.45f, true); }
            else if (now >= nextBurst) Burst(Random.Range(.18f, .5f), Random.value < .6f);
            screen.mainTextureOffset = Vector2.zero;
            if (now < burstUntil)
            {
                screen.color = Color.white;
                if (now >= nextNoise) { FillNoise(); nextNoise = now + NoiseStep; }
                return;
            }
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
            // 평소에도 화면이 아주 약하게 떨리고, 가끔 한 줄이 밀린다.
            float flicker = .9f + .1f * Mathf.PerlinNoise(Time.time * 7f, 1.3f);
            screen.mainTexture = shownImage; screen.color = new Color(flicker, flicker, flicker);
            if (Mathf.PerlinNoise(Time.time * 2.1f, 9.1f) > .86f) screen.mainTextureOffset = new Vector2(Random.Range(-.012f, .012f), 0f);
        }

        void Burst(float seconds, bool withSound)
        {
            burstUntil = Time.unscaledTime + seconds; nextBurst = burstUntil + Random.Range(5f, 13f); nextNoise = 0f;
            if (withSound && audioSource != null) { audioSource.clip = StaticClip(); audioSource.time = 0f; audioSource.Play(); }
        }

        void HideSlide()
        {
            shownSlide = null;
            if (slideTitle != null) slideTitle.gameObject.SetActive(false);
            if (slideBody != null) slideBody.gameObject.SetActive(false);
        }

        /// <summary>공연 안내 화면 한 장을 띄운다(평소 화면). 웃는 얼굴 연출 중에는 무시한다.</summary>
        public void ShowSlide(string title, string body, Color background)
        {
            if (IsPlayingFace || screen == null) return;
            mode = Mode.Slide;
            float hum = .94f + .06f * Mathf.PerlinNoise(Time.time * 4f, 3f);
            screen.mainTexture = null; screen.mainTextureOffset = Vector2.zero; screen.color = background * hum;
            string key = title + "\n" + body;
            if (shownSlide == key) return;
            shownSlide = key;
            slideTitle.text = title; slideBody.text = body;
            slideTitle.gameObject.SetActive(true); slideBody.gameObject.SetActive(true);
        }

        /// <summary>
        /// 감시 화면: 눈을 뜬 단장 얼굴이 <paramref name="worldTarget"/>(사람 머리)을 눈동자로 따라본다. 얼굴도 그쪽으로 살짝 기운다.
        /// 사람 위치는 모든 화면에 공유되므로 같은 모습으로 보인다.
        /// </summary>
        public void Watch(Vector3 worldTarget)
        {
            if (IsPlayingFace || screen == null) return;
            if (mode != Mode.Watch) { HideSlide(); mode = Mode.Watch; shownPupil = new Vector2(99f, 99f); }
            var local = transform.InverseTransformPoint(worldTarget);
            float depth = Mathf.Max(1.5f, local.z);
            // 화면 그림 기준 오른쪽(+u)은 모니터의 -x 쪽이다.
            var want = new Vector2(Mathf.Clamp(-local.x / depth, -1f, 1f), Mathf.Clamp(local.y / depth, -1f, 1f));
            pupil = Vector2.MoveTowards(pupil, want, Time.deltaTime * 4f);
            var tex = WatchTexture();
            if ((pupil - shownPupil).sqrMagnitude > .0004f) { PaintEyes(tex, pupil); shownPupil = pupil; }
            screen.mainTexture = tex; screen.mainTextureOffset = new Vector2(-pupil.x * .025f, -pupil.y * .012f);
            float flicker = .9f + .1f * Mathf.PerlinNoise(Time.time * 6f, 0f);
            screen.color = new Color(flicker, flicker, flicker);
        }

        Texture2D WatchTexture()
        {
            if (watchTexture != null) return watchTexture;
            watchBase = Paint(true);
            watchTexture = new Texture2D(FaceW, FaceH, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            watchTexture.SetPixels(watchBase); watchTexture.Apply(false);
            return watchTexture;
        }

        // 눈구멍(검정) 안에서 붉은 눈동자가 움직인다. 눈 둘레만 다시 칠한다.
        void PaintEyes(Texture2D tex, Vector2 look)
        {
            const int rx = 12, ry = 8;
            var block = new Color[(rx * 2 + 1) * (ry * 2 + 1)];
            foreach (int cx in new[] { 110, 146 })
            {
                float px = look.x * 6f, py = look.y * 3.5f;
                for (int y = -ry; y <= ry; y++)
                    for (int x = -rx; x <= rx; x++)
                    {
                        var c = watchBase[(78 + y) * FaceW + cx + x];
                        float e = x * x / (float)(rx * rx) + y * y / (float)(ry * ry);
                        if (e <= 1f)
                        {
                            c = new Color(.03f, .02f, .03f);
                            float d = (x - px) * (x - px) + (y - py) * (y - py);
                            if (d < 20f) c = d < 5f ? new Color(1f, .85f, .5f) : new Color(.95f, .15f, .1f);
                        }
                        block[(y + ry) * (rx * 2 + 1) + x + rx] = c;
                    }
                tex.SetPixels(cx - rx, 78 - ry, rx * 2 + 1, ry * 2 + 1, block);
            }
            tex.Apply(false);
        }

        void FillNoise()
        {
            var pixels = noise.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)Random.Range(0, 256);
                if ((i / noise.width) % 3 == 0) v = (byte)(v * .6f); // 주사선
                pixels[i] = new Color32(v, v, v, 255);
            }
            noise.SetPixels32(pixels); noise.Apply(false);
            screen.mainTexture = noise;
        }

        void OnDestroy()
        {
            if (screen != null) Destroy(screen);
            if (noise != null) Destroy(noise);
            if (watchTexture != null) Destroy(watchTexture);
        }

        // ── 임시 자원(코드로 그림) ──

        static Texture2D FaceTexture()
        {
            if (faceTexture != null) return faceTexture;
            var tex = new Texture2D(FaceW, FaceH, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(Paint(false)); tex.Apply(false);
            return faceTexture = tex;
        }

        const int FaceW = 256, FaceH = 160;

        /// <summary>단장 얼굴 그림. <paramref name="watching"/>이면 웃는 눈 대신 눈구멍을 비워 두고(눈동자는 따로 칠한다) 입을 다문 옅은 웃음으로 그린다.</summary>
        static Color[] Paint(bool watching)
        {
            const int w = FaceW, h = FaceH;
            var px = new Color[w * h];
            var ink = new Color(.05f, .03f, .04f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float vx = (x - w * .5f) / (w * .5f), vy = (y - h * .5f) / (h * .5f);
                    float vignette = Mathf.Clamp01(1f - (vx * vx + vy * vy) * .55f);
                    var c = new Color(.32f, .06f, .09f) * vignette; // 붉은 무대 배경
                    // 실크해트(검정 + 붉은 띠).
                    if (Mathf.Abs(x - 128) < 30 && y > 112 && y < 156) c = y < 120 ? new Color(.7f, .1f, .12f) : ink;
                    if (Mathf.Abs(x - 128) < 50 && y >= 106 && y <= 112) c = ink;
                    // 도자기 가면(타원).
                    float ex = (x - 128) / 44f, ey = (y - 62) / 50f;
                    if (ex * ex + ey * ey <= 1f) c = Color.Lerp(new Color(.97f, .95f, .9f), new Color(.82f, .8f, .78f), Mathf.Clamp01(ex * ex + ey * ey - .3f));
                    // 볼 홍조.
                    foreach (int cx in new[] { 100, 156 })
                        if ((x - cx) * (x - cx) + (y - 52) * (y - 52) < 64) c = Color.Lerp(c, new Color(.95f, .45f, .5f), .6f);
                    // 웃는 눈(∩ 모양 곡선). 감시 화면은 눈썹만 그리고 눈은 따로 칠한다.
                    foreach (int cx in new[] { 110, 146 })
                    {
                        float dx = x - cx;
                        if (!watching && Mathf.Abs(dx) <= 11f && Mathf.Abs(y - (78f - dx * dx / 16f)) < 2.2f) c = ink;
                        if (watching && Mathf.Abs(dx) <= 13f && Mathf.Abs(y - (92f - (x - 128) * Mathf.Sign(cx - 128) * .22f)) < 1.6f) c = ink;
                    }
                    // 크게 웃는 입(U 모양 곡선 + 붉은 안쪽). 감시 화면은 다문 입꼬리만 올라간 선.
                    float mx = x - 128;
                    if (watching) { if (Mathf.Abs(mx) <= 24f && Mathf.Abs(y - (38f + mx * mx / 60f)) < 1.8f) c = ink; }
                    else if (Mathf.Abs(mx) <= 26f)
                    {
                        float top = 46f + mx * mx / 80f, bottom = 30f + mx * mx / 34f;
                        if (y > bottom && y < top) c = new Color(.6f, .05f, .08f);
                        if (Mathf.Abs(y - top) < 1.6f || Mathf.Abs(y - bottom) < 1.8f) c = ink;
                    }
                    // 주사선.
                    if (y % 3 == 0) c *= .85f;
                    px[y * w + x] = c;
                }
            return px;
        }

        static AudioClip StaticClip()
        {
            if (staticClip != null) return staticClip;
            const int rate = 22050;
            int length = Mathf.CeilToInt(rate * (StaticSeconds + .4f));
            var data = new float[length];
            var random = new System.Random(7);
            float crackle = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Clamp01(t * 20f) * Mathf.Clamp01((StaticSeconds + .4f - t) * 3f);
                if (random.NextDouble() < .004) crackle = 1f; // 치직 튀는 소리
                crackle *= .995f;
                float hiss = (float)(random.NextDouble() * 2 - 1);
                data[i] = hiss * (.25f + .55f * crackle) * envelope;
            }
            staticClip = AudioClip.Create("RingmasterMonitorStatic", length, 1, rate, false);
            staticClip.SetData(data, 0);
            return staticClip;
        }
    }
}
