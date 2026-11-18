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
    public sealed class DormEntryEvent : MonoBehaviour, IShopTarget, IShopPresentation, IShopHintOnly
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
        [SerializeField] string title = "숙소 문";
        [SerializeField, TextArea] string message = "절단기를 이용해 배선을 끊어 숙소 문을 여세요.";

        Vector3 leafClosed, standPosition; Quaternion standRotation;
        float closedAmount, cutSeenAt = -1f;
        bool seen, wasJammed, playing, falling;
        CurtainCall.EscapeRoute.EscapeCuttableCable[] wires;

        /// <summary>숙소 입구 문이 다 열리지 않았다(대기실 튜토리얼 전 봉인, 오작동 연출 중, 배선을 끊기 전, 열리는 중). 잠자기 안내를 미루고 문 열기 안내를 띄우는 데 쓴다.</summary>
        public static bool IsDoorClosed => active != null && active.isActiveAndEnabled && (active.closedAmount > .001f || active.playing);
        /// <summary>숙소 입구 문이 완전히 닫힌 채 멈춰 있다(연출·여닫는 움직임 중이 아님). 문 열기 안내는 이때만 띄운다.</summary>
        public static bool IsDoorShut => active != null && active.isActiveAndEnabled && !active.playing && active.closedAmount >= .999f;
        static DormEntryEvent active;

        // The entrance remains a valid place to guide players while they need to cut the door cable.
        internal static bool IsInsideDormitory(Vector3 feet) => active != null && active.isActiveAndEnabled
            && active.inside.Contains(feet + Vector3.up * .5f);

        internal static Vector3? EntranceGuideDestination()
        {
            if (active == null || !active.isActiveAndEnabled) return null;
            var entry = active.trigger.center;
            entry.y = active.trigger.min.y;
            if (IsDoorClosed && active.exitPoints.Length > 0) {
                var outside = Vector3.ProjectOnPlane(active.exitPoints[0] - entry, Vector3.up).normalized;
                entry = active.trigger.ClosestPoint(entry + 10f * outside) + .5f * outside;
            }
            return entry;
        }

        // ---- 닫힌 문에 다가갔을 때의 상호작용 설명(2026-11-17 사용자) ----
        // 문이 오작동으로 닫혔고 아직 배선을 끊지 않은 동안에만 대상이 된다. 누를 수 있는 동작은 없고("사용 불가") 여는 방법만 알려 준다.
        bool NeedsCutter => !playing && closedAmount >= .999f && !Open(cableId);
        public Transform VisualRoot => leaf != null ? leaf : transform;
        public Vector3 PresentationPoint => blocker != null && blocker.enabled ? blocker.bounds.center : VisualRoot.position + Vector3.up * 1.2f;
        public string ActionLabel => WaitingRoomText.Get("target.dorm:entry-door.locked", "휴대 절단기를 이용해 전력을 끊어야 합니다.");
        public bool IsAvailable => false;
        public float Reach(Vector3 feet, Component actor = null)
        {
            if (!NeedsCutter) return -1f;
            // 문을 막는 충돌체가 문짝과 다른 물체라, 그 충돌체를 대상으로 삼아야 시야 검사가 자기 자신에게 막히지 않는다. 거리는 문에서 가장 가까운 점으로 잰다.
            var origin = actor != null ? CurtainCall.Player.PlayerInteraction.Center(actor) : feet + Vector3.up;
            bool solid = blocker != null && blocker.enabled;
            return ExplorationReach.From(solid ? blocker.transform : VisualRoot, solid ? blocker.ClosestPoint(origin) : PresentationPoint, feet,
                CurtainCall.Settings.GameSettings.Base.Interaction.Distance, actor: actor);
        }
        public void Interact() { }

        // 한 번만 나오는 이벤트: 세대가 바뀌어도 닫힘·배선 절단 기록은 남긴다(문은 열린 채, 절단기는 사라진 채).
        void OnEnable()
        {
            ExplorationState.KeepAcrossGenerations(jammedId, cableId);
            active = this;
            ShopReach.Add(this);
        }

        void Awake()
        {
            if (leaf != null) leafClosed = leaf.localPosition;
            if (standingSaw != null)
            {
                // 실제 절단기는 실행 중 공통 크기 설정(SawScale)만큼 줄어든다. 세워 둔 모형도 같은 배율로 맞춰, 넘어진 뒤 실제 절단기로 바뀔 때 크기가 튀지 않게 한다(2026-11-15 사용자).
                if (saw != null)
                {
                    var real = saw.CarryRoot;
                    var twin = real == saw.transform ? standingSaw : standingSaw.Find(real.name) ?? standingSaw;
                    float before = Bottom(standingSaw);
                    twin.localScale *= CurtainCall.Settings.GameSettings.Base.CarryDevices.SawScale;
                    // 줄어든 만큼 뜨거나 묻히지 않게 바닥에 닿은 높이를 유지한다.
                    standingSaw.position += Vector3.up * (before - Bottom(standingSaw));
                }
                standPosition = standingSaw.position; standRotation = standingSaw.rotation;
            }
        }

        static float Bottom(Transform root)
        {
            float y = float.MaxValue;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) y = Mathf.Min(y, r.bounds.min.y);
            return y == float.MaxValue ? root.position.y : y;
        }

        bool Open(string id) => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(id);

        void Update()
        {
            var state = ExplorationState.Instance;
            if (state == null || !state.StateReady) return; // 판정 화면의 상태를 받은 뒤부터 본다(늦게 들어온 화면에서 다시 재생되지 않게)
            bool jammed = Open(jammedId), cut = Open(cableId);
            // 첫 공연을 마치고 돌아와 대기실 튜토리얼이 끝나기 전에는 문이 닫혀 있어 숙소에 들어갈 수 없다(2026-11-15 사용자). 이벤트는 그 뒤부터.
            bool sealedBefore = WaitingRoomRehearsal.IsFirstTutorialPending;

            // 판정 화면: 대기실 튜토리얼이 끝나 동선 제한이 풀린 뒤, 누군가 입구 통로에 들어서면 닫는다.
            if (!jammed && !sealedBefore)
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
            if (!seen) closedAmount = sealedBefore || (jammed && !cut) ? 1f : 0f;
            seen = true; wasJammed = jammed;

            // 장부에는 끊김으로 남았는데 배선 모습만 처음으로 돌아온 경우(씬을 다시 불러온 뒤)를 맞춘다.
            if (cut)
            {
                wires ??= FindObjectsByType<CurtainCall.EscapeRoute.EscapeCuttableCable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var wire in wires) if (wire != null && wire.OpenTarget == cableId && !wire.IsCut) wire.SetCut(true);
            }
            if (cut && cutSeenAt < 0f)
            {
                bool witnessed = playing || closedAmount > .5f; // 들어왔을 때 이미 끊겨 있던 경우는 알리지 않는다
                cutSeenAt = Time.unscaledTime - (witnessed ? 0f : 10f);
                announceBroken = witnessed;
            }
            if (!playing) closedAmount = Mathf.MoveTowards(closedAmount, sealedBefore || (jammed && !cut) ? 1f : 0f, Time.deltaTime / 1.2f);
            ApplyDoor();

            // 절단기: 문이 닫히기 전에는 세워 둔 모습만, 넘어진 뒤부터 실제 장치, 문이 열리고 잠시 뒤 부서져 사라진다.
            bool broken = cut && cutSeenAt >= 0f && Time.unscaledTime - cutSeenAt > 1.5f;
            if (saw != null) saw.Suppressed = !jammed || falling || broken;
            // 절단기가 부서져 사라질 때 기존 기물 파괴 알림을 한 번 띄운다(2026-11-15 사용자).
            if (broken && announceBroken)
            {
                announceBroken = false;
                WaitingRoomItemBrokenNotice.Show(WaitingRoomText.Get("cutscene.dorm-door.saw-name", "녹슨 휴대 절단기"));
            }
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
                    // 제목은 작업실·소품실의 "잠긴 문" 안내와 구분한다(2026-11-17 사용자: 잠긴 문 설명은 작업실·소품실에서만).
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
        bool noticeShown, announceBroken;

        void OnDisable()
        {
            ShopReach.Remove(this);
            if (active == this) active = null;
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
