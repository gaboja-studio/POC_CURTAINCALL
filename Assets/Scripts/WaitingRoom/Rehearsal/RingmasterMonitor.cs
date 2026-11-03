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
        enum Mode { Off, Static, Face }

        static Texture2D faceTexture;
        static AudioClip staticClip;

        Material screen;
        Texture2D noise;
        AudioSource audioSource;
        Mode mode;
        float modeStarted, nextNoise;
        bool externallyTimed;

        public bool IsOn => mode != Mode.Off;

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
                foreach (float x in new[] { -size.x * .35f, size.x * .35f })
                {
                    // 화면 위 끝(본체 위 끝)에서 천장까지 이어지는 막대. 원기둥은 높이 2라 절반을 크기로 쓴다.
                    var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rod.name = "HangingRod"; Destroy(rod.GetComponent<Collider>());
                    rod.transform.SetParent(transform, false);
                    float top = size.y * .5f + .09f;
                    rod.transform.localPosition = new Vector3(x, top + hang * .5f, -.08f);
                    rod.transform.localScale = new Vector3(.04f, hang * .5f, .04f);
                    if (bezel != null) rod.GetComponent<Renderer>().sharedMaterial = bezel;
                }
            noise = new Texture2D(96, 54, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = .85f; audioSource.volume = .7f;
            audioSource.minDistance = 4f; audioSource.maxDistance = 40f;
        }

        /// <summary>치직 소리와 함께 켜진다. 잡음 뒤 단장 얼굴.</summary>
        public void TurnOn()
        {
            if (mode != Mode.Off) return;
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
            if (elapsed >= StaticSeconds + 3f) { TurnOff(); return; }
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
            if (screen != null) { screen.mainTexture = null; screen.color = Color.black; }
            if (audioSource != null) audioSource.Stop();
        }

        void Update()
        {
            if (!externallyTimed && mode != Mode.Off) RenderAt(Time.time - modeStarted);
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
        }

        // ── 임시 자원(코드로 그림) ──

        static Texture2D FaceTexture()
        {
            if (faceTexture != null) return faceTexture;
            const int w = 256, h = 160;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
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
                    // 웃는 눈(∩ 모양 곡선).
                    foreach (int cx in new[] { 110, 146 })
                    {
                        float dx = x - cx;
                        if (Mathf.Abs(dx) <= 11f && Mathf.Abs(y - (78f - dx * dx / 16f)) < 2.2f) c = ink;
                    }
                    // 크게 웃는 입(U 모양 곡선 + 붉은 안쪽).
                    float mx = x - 128;
                    if (Mathf.Abs(mx) <= 26f)
                    {
                        float top = 46f + mx * mx / 80f, bottom = 30f + mx * mx / 34f;
                        if (y > bottom && y < top) c = new Color(.6f, .05f, .08f);
                        if (Mathf.Abs(y - top) < 1.6f || Mathf.Abs(y - bottom) < 1.8f) c = ink;
                    }
                    // 주사선.
                    if (y % 3 == 0) c *= .85f;
                    px[y * w + x] = c;
                }
            tex.SetPixels(px); tex.Apply(false);
            return faceTexture = tex;
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
