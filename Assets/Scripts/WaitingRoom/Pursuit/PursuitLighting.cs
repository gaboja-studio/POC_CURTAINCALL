using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 추격 단계별 전등 연출(밝기·색·지직임/스파크·진입/방 깜빡임)과 포획 방 암전을 모든 클라이언트에서 같은 규칙으로 적용한다.
    /// 단계·경과 시간·방 깜빡임 이벤트는 <see cref="PursuitDirector"/> 스냅샷으로 동기화되고, 깜빡임 모양은 결정적 해시로 계산한다.
    /// 추격이 끝나면 원래 조명/환경광 값으로 되돌린다.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public sealed class PursuitLighting : MonoBehaviour
    {
        const string DefaultSparkMaterial = "Prefabs/Objects/ETCs/TightropeArtKit/SawSpark";
        const float EntryDarkIntensity = .03f;
        struct Entry { public Light Light; public int Room, Seed; public float Intensity; public Color Color; public int SparkSlot; public int[] Spill; }
        readonly List<Entry> entries = new();
        readonly Dictionary<int, float> captureStarted = new();
        readonly HashSet<int> darkRooms = new();
        GameObject screenObject;
        UnityEngine.UI.Image screen;
        public float ScreenDarkness { get; private set; }
        PursuitDirector director;
        ParticleSystem sparks;
        bool built, engaged;
        float brightness = 1, ambient = 1, tintAmount;
        Color tint = Color.white;
        float lastElapsed = -1, lastElapsedAt, entryStart = -1;
        int lastStage = -1, entryCount;
        float entryCycle = .42f;
        Color ambientSky, ambientEquator, ambientGround;
        float ambientIntensity, reflectionIntensity;
        // 런타임에는 Trilight 색을 바꿔도 프로브가 다시 계산되지 않으므로 실제 사용되는 프로브와 반사 강도를 직접 조절한다.
        UnityEngine.Rendering.SphericalHarmonicsL2 ambientProbe;

        public int LightCount => entries.Count;
        public bool Engaged => engaged;
        public float Brightness => brightness;
        public float AmbientMultiplier { get; private set; } = 1;
        public float TintAmount => tintAmount;
        public int SparkBursts { get; private set; }
        public float Clock { get; private set; } = -1;

        void OnEnable() { director = GetComponent<PursuitDirector>(); }
        void OnDisable() { Release(); }
        void OnDestroy()
        {
            if (sparks != null) Destroy(sparks.gameObject);
            if (screenObject != null) Destroy(screenObject);
        }

        public void Rebuild() { Release(); built = false; entries.Clear(); }
        void Build()
        {
            built = true; entries.Clear();
            if (director == null || director.Rooms == null) return;
            var rooms = director.Rooms;
            var seen = new HashSet<Light>();
            for (int r = 0; r < rooms.Length; r++) {
                if (rooms[r] == null || rooms[r].Lights == null) continue;
                foreach (var light in rooms[r].Lights) if (light != null && seen.Add(light)) Add(light, r);
            }
            if (GameSettings.WaitingRoom.Pursuit.Lighting.IncludeUnassignedLights) {
                foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                    if (light == null || seen.Contains(light) || light.gameObject.scene != director.gameObject.scene) continue;
                    if (light.type == LightType.Directional || light.GetComponentInParent<PursuitActor>() != null
                        || light.GetComponentInParent<Unity.Netcode.NetworkObject>() != null) continue;
                    seen.Add(light); Add(light, RoomOf(light.transform.position));
                }
            }
            // 위치 기준 정렬: 클라이언트마다 같은 조명이 같은 지직임 시드를 받는다.
            entries.Sort((a, b) => Key(a.Light.transform.position).CompareTo(Key(b.Light.transform.position)));
            for (int i = 0; i < entries.Count; i++) { var e = entries[i]; e.Seed = i; e.Spill = SpillRooms(e); entries[i] = e; }
        }
        /// <summary>다른 방에 속하지만 빛 범위가 이 방 범위에 닿는 방 목록(포획 암전 때 함께 끈다).</summary>
        int[] SpillRooms(Entry e)
        {
            var light = e.Light;
            if (light.type != LightType.Point && light.type != LightType.Spot) return System.Array.Empty<int>();
            var list = new List<int>();
            float range = light.range * light.range;
            for (int r = 0; r < director.Rooms.Length; r++) {
                var room = director.Rooms[r];
                if (r == e.Room || room == null || room.Area == null) continue;
                if (room.Area.bounds.SqrDistance(light.transform.position) < range) list.Add(r);
            }
            return list.ToArray();
        }
        bool SpillsIntoDark(Entry e)
        {
            if (darkRooms.Count == 0 || e.Spill == null) return false;
            foreach (var r in e.Spill) if (darkRooms.Contains(r)) return true;
            return false;
        }
        void SetScreenDarkness(float alpha)
        {
            ScreenDarkness = alpha;
            if (alpha <= 0 && screenObject == null) return;
            if (screenObject == null) {
                screenObject = new GameObject("PursuitBlackout", typeof(RectTransform), typeof(Canvas));
                screenObject.transform.SetParent(transform, false);
                var canvas = screenObject.GetComponent<Canvas>();
                // HUD(0 이상)보다 아래, 게임 화면 위.
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -6;
                var go = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(screenObject.transform, false);
                screen = go.GetComponent<UnityEngine.UI.Image>(); screen.raycastTarget = false;
                var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            screen.color = new Color(0, 0, 0, alpha);
            screen.enabled = alpha > 0;
        }
        static long Key(Vector3 p) => ((long)Mathf.RoundToInt(p.x * 10) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y * 10) * 19349663L) ^ ((long)Mathf.RoundToInt(p.z * 10) * 83492791L);
        void Add(Light light, int room) => entries.Add(new Entry { Light = light, Room = room, SparkSlot = -1 });
        int RoomOf(Vector3 p)
        {
            int best = -1; float size = float.PositiveInfinity;
            for (int i = 0; i < director.Rooms.Length; i++) {
                var room = director.Rooms[i];
                if (room == null || room.Area == null || !room.Area.bounds.Contains(p)) continue;
                float s = room.Area.bounds.size.sqrMagnitude;
                if (s < size) { size = s; best = i; }
            }
            return best;
        }

        /// <summary>개발 미리보기: 현재 단계의 진입 깜빡임을 로컬에서 다시 재생한다.</summary>
        public void PreviewEntryFlicker(int count, float cycle)
        {
            entryCount = count; entryCycle = cycle; entryStart = LocalClock();
        }

        float LocalClock()
        {
            // 클라이언트는 경과 시간을 10Hz로 받으므로 다음 스냅샷까지 로컬로 보간한다.
            float elapsed = director != null ? director.Elapsed : -1;
            if (elapsed < 0) { lastElapsed = -1; return -1; }
            if (!Mathf.Approximately(elapsed, lastElapsed)) { lastElapsed = elapsed; lastElapsedAt = Time.time; }
            return elapsed + Mathf.Min(Time.time - lastElapsedAt, .5f);
        }

        void LateUpdate()
        {
            if (director == null && (director = GetComponent<PursuitDirector>()) == null) return;
            if (!built) Build();
            var settings = GameSettings.WaitingRoom.Pursuit.Lighting;
            float clock = Clock = LocalClock();
            bool active = clock >= 0 && settings.Enabled;
            int stageIndex = active ? director.CurrentStageIndex : -1;
            var rule = active ? settings.StageFor(stageIndex) : null;
            TrackStage(stageIndex, rule, clock);

            float dt = Time.deltaTime, k = 1 - Mathf.Exp(-dt * 3f / Mathf.Max(.05f, settings.TransitionSeconds));
            brightness = Mathf.Lerp(brightness, rule?.Brightness ?? 1, k);
            ambient = Mathf.Lerp(ambient, rule?.AmbientBrightness ?? 1, k);
            tintAmount = Mathf.Lerp(tintAmount, rule?.TintAmount ?? 0, k);
            if (rule != null) tint = rule.Tint;
            bool settled = Mathf.Abs(brightness - 1) < .002f && Mathf.Abs(ambient - 1) < .002f && tintAmount < .002f;

            bool capturing = false;
            var rooms = director.Rooms ?? System.Array.Empty<PursuitRoom>();
            for (int r = 0; r < rooms.Length; r++) {
                bool on = rooms[r] != null && rooms[r].CaptureFlickering;
                if (on && !captureStarted.ContainsKey(r)) captureStarted[r] = Time.time;
                else if (!on) captureStarted.Remove(r);
                capturing |= on;
            }
            if (!active && settled && !capturing) { Release(); return; }
            Engage();

            Vector3 view = ViewPosition();
            int viewRoom = -1;
            for (int r = 0; r < rooms.Length; r++) if (rooms[r] != null && rooms[r].ContainsView(view)) { viewRoom = r; break; }
            int flickerRoom = active ? director.RoomFlickerIndex : -1;
            float flickerSince = clock - director.RoomFlickerAt;
            bool flickerOn = flickerRoom >= 0 && rule != null && flickerSince >= 0 && flickerSince < rule.RoomFlickerSeconds;
            bool flickerDark = flickerOn && PursuitLightSettings.IsDark(flickerSince, 9, .5f, director.LightSeed + flickerRoom);
            bool entryDark = entryStart >= 0 && PursuitLightSettings.EntryDark(clock - entryStart, entryCount, entryCycle);
            int seed = director.LightSeed;

            // 포획 방별 현재 꺼짐 여부. 켜진 순간은 원래 밝기(+단계 색), 꺼진 순간은 암전.
            darkRooms.Clear();
            foreach (var pair in captureStarted)
                if (PursuitLightSettings.IsDark(Time.time - pair.Value, settings.CaptureFlickerRate, settings.CaptureDarkRatio, seed + pair.Key)) darkRooms.Add(pair.Key);

            float ambientMultiplier = ambient;
            bool viewInRoom = viewRoom >= 0, viewDark = false;
            if (viewInRoom && captureStarted.ContainsKey(viewRoom)) {
                viewDark = darkRooms.Contains(viewRoom);
                ambientMultiplier = viewDark ? settings.CaptureOffAmbient : settings.CaptureOnAmbient;
            } else if (viewInRoom && flickerDark && viewRoom == flickerRoom) ambientMultiplier *= rule.RoomFlickerOffAmbient;
            if (entryDark) ambientMultiplier *= rule != null ? rule.RoomFlickerOffAmbient : .35f;
            ApplyAmbient(AmbientMultiplier = ambientMultiplier);
            SetScreenDarkness(viewDark ? settings.CaptureScreenDarkness : 0);

            Color target = tint;
            for (int i = 0; i < entries.Count; i++) {
                var e = entries[i];
                if (e.Light == null) continue;
                float level = brightness;
                if (e.Room >= 0 && captureStarted.ContainsKey(e.Room)) {
                    level = darkRooms.Contains(e.Room) ? settings.CaptureOffIntensity : settings.CaptureOnBrightness;
                } else if (settings.CaptureDarkenSpill && SpillsIntoDark(e)) {
                    level = settings.CaptureOffIntensity;
                } else if (flickerOn && e.Room == flickerRoom) {
                    if (flickerDark) level *= rule.RoomFlickerOffIntensity;
                } else if (entryDark) level *= EntryDarkIntensity;
                else if (rule != null && PursuitLightSettings.Crackle(clock, e.Seed * 31 + seed, rule.CracklePerMinute, rule.CrackleSeconds, out int slot, out float local)) {
                    if (PursuitLightSettings.Hash(e.Seed, Mathf.FloorToInt(local * 30), slot) < .6f) level *= rule.CrackleDip;
                    if (slot != e.SparkSlot) { e.SparkSlot = slot; if (rule.Sparks > 0) EmitSparks(e.Light.transform.position, rule.Sparks, view, settings); }
                }
                e.Light.intensity = e.Intensity * level;
                e.Light.color = Color.Lerp(e.Color, target, tintAmount);
                entries[i] = e;
            }
        }
        void TrackStage(int stageIndex, PursuitLightStage rule, float clock)
        {
            if (stageIndex == lastStage) return;
            // 비활성→후반 단계 직행은 늦은 입장/복구로 보고 진입 깜빡임을 생략한다.
            bool lateJoin = lastStage < 0 && stageIndex > 0;
            lastStage = stageIndex;
            if (stageIndex < 0) { entryStart = -1; return; }
            if (rule != null && rule.EntryFlickers > 0 && !lateJoin) { entryCount = rule.EntryFlickers; entryCycle = rule.EntryFlickerCycle; entryStart = clock; }
        }
        Vector3 ViewPosition()
        {
            var camera = Camera.main;
            if (camera != null) return camera.transform.position;
            var local = NetworkPlayer.Local;
            return local != null ? local.transform.position + Vector3.up * 1.6f : Vector3.positiveInfinity;
        }
        void Engage()
        {
            if (engaged) return;
            engaged = true;
            for (int i = 0; i < entries.Count; i++) {
                var e = entries[i];
                if (e.Light == null) continue;
                e.Intensity = e.Light.intensity; e.Color = e.Light.color; e.SparkSlot = -1;
                entries[i] = e;
            }
            ambientSky = RenderSettings.ambientSkyColor; ambientEquator = RenderSettings.ambientEquatorColor;
            ambientGround = RenderSettings.ambientGroundColor; ambientIntensity = RenderSettings.ambientIntensity;
            ambientProbe = RenderSettings.ambientProbe; reflectionIntensity = RenderSettings.reflectionIntensity;
        }
        void ApplyAmbient(float m)
        {
            RenderSettings.ambientSkyColor = ambientSky * m; RenderSettings.ambientEquatorColor = ambientEquator * m;
            RenderSettings.ambientGroundColor = ambientGround * m; RenderSettings.ambientIntensity = ambientIntensity * m;
            RenderSettings.ambientProbe = ambientProbe * m; RenderSettings.reflectionIntensity = reflectionIntensity * m;
        }
        void Release()
        {
            if (screen != null) { screen.enabled = false; ScreenDarkness = 0; }
            if (!engaged) return;
            engaged = false; AmbientMultiplier = 1;
            foreach (var e in entries) if (e.Light != null) { e.Light.intensity = e.Intensity; e.Light.color = e.Color; }
            RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround; RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.ambientProbe = ambientProbe; RenderSettings.reflectionIntensity = reflectionIntensity;
            brightness = ambient = 1; tintAmount = 0; entryStart = -1; lastStage = -1;
            captureStarted.Clear(); darkRooms.Clear();
        }

        void EmitSparks(Vector3 position, int count, Vector3 view, PursuitLightSettings settings)
        {
            if ((position - view).sqrMagnitude > 35 * 35) return;
            if (sparks == null) sparks = CreateSparks(settings);
            if (sparks == null) return;
            var main = sparks.main; main.startColor = settings.SparkColor;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++) {
                var dir = Random.insideUnitSphere; dir.y = -Mathf.Abs(dir.y) * .7f - .15f;
                p.position = position + Random.insideUnitSphere * .08f;
                p.velocity = dir.normalized * settings.SparkSpeed * Random.Range(.45f, 1f);
                p.startSize = settings.SparkSize * Random.Range(.6f, 1.3f);
                p.startLifetime = Random.Range(.25f, .65f);
                sparks.Emit(p, 1);
            }
            SparkBursts++;
        }
        ParticleSystem CreateSparks(PursuitLightSettings settings)
        {
            var material = settings.SparkMaterial != null ? settings.SparkMaterial : Resources.Load<Material>(DefaultSparkMaterial);
            if (material == null) return null;
            var go = new GameObject("PursuitLightSparks", typeof(ParticleSystem));
            go.transform.SetParent(transform, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 1.1f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .1f));
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, .45f, .15f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .035f; renderer.lengthScale = 1.2f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            return ps;
        }
    }
}
