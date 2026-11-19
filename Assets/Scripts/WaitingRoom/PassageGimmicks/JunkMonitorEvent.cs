using System.Collections;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 폐기물 보관소 거대 모니터 연출(2026-11-19 사용자). 6분할 화면 대신 단장 모니터처럼 하나의 화면을 쓴다.
    /// 누군가 보관소 안에서 모니터 쪽을 처음 바라보면: 보관소 조명이 3번 깜빡 → 화면이 켜져 그림 5초 → 전원이 나가며 꺼진다.
    /// 시선은 각자 화면에서만 알 수 있어 본 사람이 판정 화면에 기록을 요청하고(<see cref="ExplorationState.RequestSeen"/>),
    /// 기록이 켜지는 순간 모든 화면이 함께 재생한다. 기록은 세대 교체(런 종료) 때 비워지므로 그 전까지 다시 나오지 않는다.
    /// 들어왔을 때 이미 기록돼 있으면 재생하지 않는다.
    /// </summary>
    [DefaultExecutionOrder(100)] // 회로 조명(NightPower.LateUpdate)이 켜고 끈 뒤에 덮어쓴다
    public sealed class JunkMonitorEvent : MonoBehaviour
    {
        [SerializeField] string seenId = ExplorationState.SeenPrefix + "junk-monitor";
        [Tooltip("이 범위(월드 기준) 안에서 화면을 바라봐야 한다(폐기물 보관소).")]
        [SerializeField] Bounds room;
        [SerializeField] Vector3 screenCenter;
        [SerializeField] Vector3 screenFacing = Vector3.right;
        [SerializeField] Vector2 screenSize = new(5.2f, 2.925f);
        [SerializeField] Material bezel;
        [SerializeField] Texture image;
        [Tooltip("화면 가운데가 카메라 정면에서 이 각도 안이면 바라본 것으로 본다.")]
        [SerializeField, Range(5f, 60f)] float lookAngle = 28f;
        [SerializeField] Light[] lights = System.Array.Empty<Light>();
        [Tooltip("깜빡일 때 함께 어두워지는 형광등 면.")]
        [SerializeField] Renderer[] glows = System.Array.Empty<Renderer>();
        [SerializeField, Min(1f)] float imageSeconds = 5f;

        RingmasterMonitor monitor;
        bool seen, was, playing, lightsOff;
        bool[] before;
        MaterialPropertyBlock dark;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor"), BaseColor = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            monitor = RingmasterMonitor.Create(transform, "JunkMonitorScreen", screenCenter, screenFacing, screenSize, bezel, 0f);
            monitor.TurnOff();
        }

        void Update()
        {
            var state = ExplorationState.Instance;
            if (state == null || !state.StateReady) return; // 판정 화면의 상태를 받은 뒤부터 본다
            bool found = state.IsOpen(seenId);
            if (!found && !WaitingRoomRehearsal.IsFirstTutorialPending && LocalLooking(state)) state.RequestSeen(seenId);
            if (seen && found && !was && !playing) StartCoroutine(Play());
            seen = true; was = found;
        }

        bool LocalLooking(ExplorationState state)
        {
            var cam = Camera.main;
            if (cam == null || !(state.LocalFeet is Vector3 feet) || !room.Contains(feet + Vector3.up * .5f)) return false;
            var to = screenCenter - cam.transform.position;
            if (Vector3.Dot(to, screenFacing) > 0f) return false; // 화면 뒤쪽
            if (Vector3.Angle(cam.transform.forward, to) > lookAngle) return false;
            // 사이에 벽·기물이 막고 있으면 보지 못한 것이다(화면 바로 앞까지만 검사). 사람 몸(3인칭 카메라 앞 내 캐릭터 등)은 무시한다.
            int count = Physics.RaycastNonAlloc(cam.transform.position, to.normalized, hits, Mathf.Max(0f, to.magnitude - .4f), ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].collider.GetComponentInParent<CurtainCall.Network.PlayerSync.NetworkPlayer>() == null) return false;
            return true;
        }
        readonly RaycastHit[] hits = new RaycastHit[16];

        IEnumerator Play()
        {
            playing = true;
            // 조명 3번 깜빡임(꺼짐 0.12~0.2초, 켜짐 0.25~0.35초).
            float[] pattern = { .15f, .3f, .12f, .25f, .2f, .45f };
            for (int i = 0; i < pattern.Length; i++)
            {
                SetLightsOff(i % 2 == 0);
                for (float t = 0f; t < pattern[i]; t += Time.deltaTime) yield return null;
            }
            SetLightsOff(false);
            // 화면이 켜진다: 치직 잡음(약 1.3초) 뒤 그림 5초.
            if (image != null && monitor != null)
            {
                monitor.ShowImage(image);
                for (float t = 0f; t < 1.3f + imageSeconds; t += Time.deltaTime) { monitor.ShowImage(image); yield return null; }
            }
            // 전원이 나간다: 조명이 잠깐 꺼지고 화면도 꺼진다.
            if (monitor != null) monitor.TurnOff();
            SetLightsOff(true);
            for (float t = 0f; t < .6f; t += Time.deltaTime) yield return null;
            SetLightsOff(false);
            playing = false;
        }

        void SetLightsOff(bool off)
        {
            if (off == lightsOff) return;
            lightsOff = off;
            if (off)
            {
                before ??= new bool[lights.Length];
                for (int i = 0; i < lights.Length; i++) if (lights[i] != null) before[i] = lights[i].enabled;
            }
            else for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].enabled = before[i];
            if (dark == null) { dark = new MaterialPropertyBlock(); dark.SetColor(EmissionColor, Color.black); dark.SetColor(BaseColor, new Color(.08f, .08f, .08f)); }
            foreach (var glow in glows) if (glow != null) glow.SetPropertyBlock(off ? dark : null);
        }

        void LateUpdate()
        {
            if (lightsOff) foreach (var light in lights) if (light != null) light.enabled = false;
        }

        void OnDisable()
        {
            if (!playing && !lightsOff) return;
            StopAllCoroutines(); SetLightsOff(false); playing = false;
            if (monitor != null) monitor.TurnOff();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireCube(room.center, room.size);
            Gizmos.color = Color.cyan; Gizmos.DrawRay(screenCenter, screenFacing.normalized * 2f);
        }
    }
}
