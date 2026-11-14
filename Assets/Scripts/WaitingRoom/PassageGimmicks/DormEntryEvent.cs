using System.Collections;
using CurtainCall.EscapeRoute;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.WaitingRoom.Pursuit;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 숙소 공통 이벤트(2026-11-14 사용자). 누군가 처음 숙소에 들어가려 하면 입구 문이 오작동하며 저절로 닫힌다.
    /// 들어가던 사람·이미 들어간 사람은 문 밖으로 옮겨지고, 카메라가 문과 세워 둔 녹슨 휴대 절단기를 비춘 뒤 절단기가 넘어지는 모습을 가까이 보여 준다.
    /// 화면 위 시스템 알림 뒤 내 시점으로 돌아온다. 녹슨 휴대 절단기로 입구 배선을 끊으면 문이 열리고, 절단기는 부서져 사라진다(1회용).
    /// 닫힘(<see cref="jammedId"/>)·배선 절단(<see cref="cableId"/>)은 탐색 장부로 공유되고, 각 화면이 같은 순간에 같은 연출을 재생한다.
    /// 연출 중에는 조작이 잠기고 추격자·탐색 제한 시간이 멈춘다.
    /// </summary>
    public sealed class DormEntryEvent : MonoBehaviour
    {
        [SerializeField] string jammedId = "dorm:entry-jammed", cableId = "dorm:entry-cable";
        [Header("문")]
        [Tooltip("문짝. 씬에 놓인 자리가 닫힌 자리다.")]
        [SerializeField] Transform leaf;
        [Tooltip("열렸을 때 문짝이 옮겨 가는 거리(문짝의 부모 기준).")]
        [SerializeField] Vector3 leafOpenOffset = new(0f, 4.3f, 0f);
        [SerializeField] Collider blocker;
        [Header("범위(월드 기준)")]
        [Tooltip("이 안에 들어서면 문이 닫힌다(입구 통로, 문 앞).")]
        [SerializeField] Bounds trigger;
        [Tooltip("문이 닫힐 때 이 안에 있는 사람은 문 밖으로 옮긴다(입구 통로 안쪽과 숙소 전체).")]
        [SerializeField] Bounds inside;
        [Tooltip("문 밖으로 옮겨 놓을 자리들(사람마다 다른 자리).")]
        [SerializeField] Vector3[] exitPoints = System.Array.Empty<Vector3>();
        [SerializeField] float exitYaw = 90f;
        [Header("녹슨 휴대 절단기")]
        [SerializeField] EscapeExitSaw saw;
        [Tooltip("세워 둔 모습(겉모습만). 넘어지는 연출 뒤 사라지고 실제 절단기가 그 자리에 나타난다.")]
        [SerializeField] Transform standingSaw;
        [Tooltip("넘어진 뒤 자세(실제 절단기 자리와 같게).")]
        [SerializeField] Vector3 fallenPosition;
        [SerializeField] Vector3 fallenEuler;
        [Header("카메라")]
        [SerializeField] Vector3 wideCamera, wideLook, closeCamera, closeLook;
        [Tooltip("절단기를 비춘 뒤 끊어야 하는 배선을 보여 주는 자리(2026-11-14 사용자).")]
        [SerializeField] Vector3 wireCamera, wireLook;
        [SerializeField, Min(.1f)] float wireSeconds = 2.6f;
        [SerializeField, Min(.1f)] float wideSeconds = 2.4f, fallSeconds = .7f, closeSeconds = 3.2f, returnSeconds = .8f;
        [SerializeField] string title = "잠긴 문";
        [SerializeField, TextArea] string message = "절단기를 이용해 배선을 끊어 숙소 문을 여세요.";

        Vector3 leafClosed, standPosition; Quaternion standRotation;
        float closedAmount, cutSeenAt = -1f;
        bool seen, wasJammed, playing, falling;

        // 한 번만 나오는 이벤트: 세대가 바뀌어도 닫힘·배선 절단 기록은 남긴다(문은 열린 채, 절단기는 사라진 채).
        void OnEnable() => ExplorationState.KeepAcrossGenerations(jammedId, cableId);

        void Awake()
        {
            if (leaf != null) leafClosed = leaf.localPosition;
            if (standingSaw != null) { standPosition = standingSaw.position; standRotation = standingSaw.rotation; }
        }

        bool Open(string id) => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(id);

        void Update()
        {
            var state = ExplorationState.Instance;
            if (state == null || !state.StateReady) return; // 판정 화면의 상태를 받은 뒤부터 본다(늦게 들어온 화면에서 다시 재생되지 않게)
            bool jammed = Open(jammedId), cut = Open(cableId);

            // 판정 화면: 첫 공연 전 동선 제한이 풀린 뒤, 누군가 입구 통로에 들어서면 닫는다.
            if (!jammed && !WaitingRoomRehearsal.IsPreTutorialRestricted)
            {
                bool entering = false; int players = 0;
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                    players++; entering |= trigger.Contains(player.transform.position + Vector3.up * .5f);
                }
                if (players == 0 && state.LocalFeet is Vector3 feet) entering = trigger.Contains(feet + Vector3.up * .5f);
                if (entering) state.MarkVisited(jammedId);
            }

            // 들어왔을 때 이미 닫혀 있었다면 연출 없이 그 상태로 둔다.
            if (seen && jammed && !wasJammed && !cut && !playing) StartCoroutine(Play());
            if (!seen) closedAmount = jammed && !cut ? 1f : 0f;
            seen = true; wasJammed = jammed;

            if (cut && cutSeenAt < 0f) cutSeenAt = Time.unscaledTime - (playing || closedAmount > .5f ? 0f : 10f);
            if (!playing) closedAmount = Mathf.MoveTowards(closedAmount, jammed && !cut ? 1f : 0f, Time.deltaTime / 1.2f);
            ApplyDoor();

            // 절단기: 문이 닫히기 전에는 세워 둔 모습만, 넘어진 뒤부터 실제 장치, 문이 열리고 잠시 뒤 부서져 사라진다.
            bool broken = cut && cutSeenAt >= 0f && Time.unscaledTime - cutSeenAt > 1.5f;
            if (saw != null) saw.Suppressed = !jammed || falling || broken;
            if (standingSaw != null)
            {
                bool show = (!jammed || falling) && !cut;
                if (standingSaw.gameObject.activeSelf != show) standingSaw.gameObject.SetActive(show);
            }
        }

        void ApplyDoor()
        {
            if (leaf != null) leaf.localPosition = leafClosed + leafOpenOffset * (1f - closedAmount);
            if (blocker != null) blocker.enabled = closedAmount > .1f;
        }

        IEnumerator Play()
        {
            playing = true; falling = true;
            float total = wideSeconds + fallSeconds + closeSeconds + wireSeconds;
            InputGate.SetBlocking(this, true);
            PursuitDirector.HoldForCutscene(total + .5f);
            // 문 안쪽(입구 통로 안·숙소 안)에 있던 내 캐릭터는 문 밖으로.
            var local = NetworkPlayer.Local;
            if (local != null && exitPoints.Length > 0 && inside.Contains(local.transform.position + Vector3.up * .5f) && local.TryGetComponent(out PlayerMover mover))
                mover.SetStartPose(exitPoints[(int)(local.OwnerClientId % (ulong)exitPoints.Length)], Quaternion.Euler(0f, exitYaw, 0f), true);

            // 들어갈 때는 빠르게 당겨 보고, 돌아올 때는 조금 천천히 내 시점으로 옮겨 간다. 끝나면 원래 전환 설정으로 되돌린다.
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            var blend = brain != null ? brain.DefaultBlend : default;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .5f);
            var go = new GameObject("DormEntryEventCamera");
            var cam = go.AddComponent<CinemachineCamera>();
            if (Camera.main != null) cam.Lens = LensSettings.FromCamera(Camera.main);
            cam.Priority = 240;
            go.transform.SetPositionAndRotation(wideCamera, Quaternion.LookRotation(wideLook - wideCamera, Vector3.up));

            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                PursuitDirector.HoldForCutscene(.5f);
                // 오작동: 쾅 닫혔다가 덜컹 들렸다가 다시 닫힌다.
                closedAmount = t < .35f ? Mathf.SmoothStep(0f, 1f, t / .35f)
                    : t < .7f ? Mathf.Lerp(1f, .72f, Mathf.Sin((t - .35f) / .35f * Mathf.PI))
                    : t < 1.1f ? Mathf.Lerp(1f, .88f, Mathf.Sin((t - .7f) / .4f * Mathf.PI)) : 1f;
                ApplyDoor();
                float f = Mathf.Clamp01((t - wideSeconds) / fallSeconds);
                if (standingSaw != null)
                {
                    // 넘어진다: 처음엔 천천히 기울다 빨라지고, 닿은 뒤 한 번 살짝 튄다.
                    float k = f * f, bounce = f >= 1f ? Mathf.Max(0f, .06f * Mathf.Sin(Mathf.Clamp01((t - wideSeconds - fallSeconds) / .3f) * Mathf.PI)) : 0f;
                    standingSaw.SetPositionAndRotation(Vector3.Lerp(standPosition, fallenPosition, k) + Vector3.up * bounce,
                        Quaternion.Slerp(standRotation, Quaternion.Euler(fallenEuler), k));
                }
                if (falling && t >= wideSeconds + fallSeconds + .35f) falling = false; // 실제 절단기가 넘어진 자리에 나타난다
                if (t >= wideSeconds - .2f)
                {
                    // 절단기 쪽으로 다가가 가까이 비춘다.
                    float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - (wideSeconds - .2f)) / .9f));
                    var at = Vector3.Lerp(wideCamera, closeCamera, c); var look = Vector3.Lerp(wideLook, closeLook, c);
                    // 절단기를 비춘 뒤 끊어야 하는 배선으로 옮겨 간다.
                    float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - (wideSeconds + fallSeconds + closeSeconds)) / .9f));
                    at = Vector3.Lerp(at, wireCamera, w); look = Vector3.Lerp(look, wireLook, w);
                    go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look - at, Vector3.up));
                }
                if (t >= wideSeconds + fallSeconds && !noticeShown)
                {
                    noticeShown = true;
                    WaitingRoomGuidePanel.ShowAs(WaitingRoomText.Get("cutscene.dorm-door.title", title), WaitingRoomText.Get("cutscene.dorm-door.body", message), closeSeconds + wireSeconds + 2f, RoomNotice.Notice);
                }
                yield return null;
            }
            falling = false; closedAmount = 1f;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, returnSeconds);
            Destroy(go); // 카메라가 내 시점으로 옮겨 간다
            for (float t = 0f; t < returnSeconds + .1f; t += Time.unscaledDeltaTime) { PursuitDirector.HoldForCutscene(.3f); yield return null; }
            if (brain != null) brain.DefaultBlend = blend;
            InputGate.SetBlocking(this, false);
            playing = false;
        }
        bool noticeShown;

        void OnDisable()
        {
            if (!playing) return;
            StopAllCoroutines(); InputGate.SetBlocking(this, false); playing = false; falling = false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireCube(trigger.center, trigger.size);
            Gizmos.color = Color.cyan; Gizmos.DrawWireCube(inside.center, inside.size);
        }
    }
}
