using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 모니터(2026-11-14 사용자). 플레이 시작 때 단일 대형 모니터를 천장에 암으로 매달고, 아래 규칙으로 단장 그림을 띄운다.
    /// - 처음에는 꺼져 있다. 첫 외줄타기 리허설을 마치고 무대 입구 통로로 갈 때 치직 소리와 함께 켜진다. 그 뒤(첫 공연 뒤)에는 켜진 채로 둔다.
    /// - 켜져 있는 동안 주기적으로 치직거리는 잡음이 낀다(<see cref="RingmasterMonitor.ShowImage"/>).
    /// - 평소: 지켜보는 단장. 누군가 허용 구역 밖에 있거나 남은 탐색 시간이 <see cref="boredSeconds"/>초 이하: 시큰둥한 단장.
    ///   남은 시간이 <see cref="goneSeconds"/>초 이하(추격 포함): 단장이 사라진 무대.
    /// - 밤(밤·새벽 단계)에는 꺼진다. 다음 날·다음 공연으로 타이머가 초기화되면 다시 지켜보는 단장.
    /// 사람 위치·단계·타이머는 모두에게 공유되므로 화면마다 따로 판단해도 같은 그림이 된다.
    /// </summary>
    public sealed class LobbyMonitorDirector : MonoBehaviour
    {
        public static LobbyMonitorDirector Instance { get; private set; }

        [Header("모니터(관리실·폐기물 보관소의 여러 모니터를 이어 붙인 크기의 단일 모니터, 천장에 암으로 매닮)")]
        [Tooltip("화면 크기(m).")]
        [SerializeField] Vector2 screenSize = new(6f, 3f);
        [Tooltip("모니터 바로 아래 바닥 자리. 여기서 위로 천장을 재서 암 길이를 정한다.")]
        [SerializeField] Vector3 floorPoint = new(-9.7f, 0f, 91f);
        [Tooltip("화면이 바라보는 방향(로비 쪽).")]
        [SerializeField] Vector3 facing = Vector3.back;
        [Tooltip("화면 아래 끝의 바닥 위 높이(m). 사람이 아래로 지나간다.")]
        [SerializeField, Min(2f)] float bottomHeight = 3.2f;

        [Header("그림")]
        [SerializeField] Texture watching;
        [SerializeField] Texture bored;
        [SerializeField] Texture gone;
        [Tooltip("남은 탐색 시간이 이 값(초) 이하이면 시큰둥한 그림.")]
        [SerializeField, Min(0f)] float boredSeconds = 170f;
        [Tooltip("남은 탐색 시간이 이 값(초) 이하이면 단장이 사라진 그림.")]
        [SerializeField, Min(0f)] float goneSeconds = 60f;
        [Tooltip("이 구역들에만 사람이 있으면 지켜보는 그림(탐색 구역 물체 이름). 이 밖의 구역에 한 명이라도 있으면 시큰둥한 그림. 구역이 없는 곳(Area_Main)은 로비로 본다.")]
        [SerializeField] string[] calmZones = { "Area_Main", "Lobby", "ServiceGallery", "Area_S08_ElevatorHall", "Area_S02_Dormitory", "F8_Dorm", "Area_S03_Shop",
            "Area_S09_PracticeRoom", "F8_Practice", "Area_S10_Workshop", "WorkshopBranch", "Area_S04_PropRoom", "F8_PropRoom" };

        RingmasterMonitor monitor;
        BoxCollider approach, passage;
        ExplorationTimerZones zones;
        bool poweredOn;

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (watching == null) watching = Resources.Load<Texture>("WaitingRoom/Monitor/ringmaster-watching");
            if (bored == null) bored = Resources.Load<Texture>("WaitingRoom/Monitor/ringmaster-bored");
            if (gone == null) gone = Resources.Load<Texture>("WaitingRoom/Monitor/ringmaster-gone");
            Material bezel = null;
            var screenBezel = GameObject.Find("MonitorBezel");
            if (screenBezel != null && screenBezel.TryGetComponent(out Renderer renderer)) bezel = renderer.sharedMaterial;
            // 실제 천장에 매단다(허공에 띄우지 않는다). 천장을 못 찾거나 너무 낮으면 만들지 않는다.
            if (Physics.Raycast(floorPoint + Vector3.up * 1.5f, Vector3.up, out var ceiling, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                float half = screenSize.y * .5f + .09f, center = floorPoint.y + bottomHeight + half;
                float hang = ceiling.point.y - (center + half);
                if (hang >= .05f)
                    monitor = RingmasterMonitor.Create(transform, "RingmasterMonitor", new Vector3(floorPoint.x, center, floorPoint.z), facing, screenSize, bezel, hang);
                else Debug.LogWarning("[Monitor] 천장이 낮아 대기실 모니터를 달지 않습니다.");
            }
            else Debug.LogWarning("[Monitor] 천장을 찾지 못해 대기실 모니터를 달지 않습니다.");
            approach = GameObject.Find("Zone_StageApproach")?.GetComponent<BoxCollider>();
            passage = GameObject.Find("Zone_StagePassage")?.GetComponent<BoxCollider>();
        }

        void Update()
        {
            if (monitor == null) return;
            var run = CampaignRun.Instance;
            if (run == null) { if (monitor.IsOn) monitor.TurnOff(); return; }
            var state = run.State;
            if (zones == null) zones = FindFirstObjectByType<ExplorationTimerZones>();

            // 사람들이 어디에 있는지: 무대 입구 통로 쪽인지, 허용 구역 밖인지.
            bool atStage = false, away = false;
            void See(Vector3 feet)
            {
                atStage |= Inside(approach, feet) || Inside(passage, feet);
                if (zones != null && System.Array.IndexOf(calmZones, zones.ZoneAt(feet)) < 0) away = true;
            }
            int seen = 0;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                seen++; See(player.transform.position);
            }
            if (seen == 0 && ExplorationState.Instance != null && ExplorationState.Instance.LocalFeet is Vector3 feet) See(feet);

            // 켜짐: 첫 공연 전에는 리허설을 마치고 무대 입구 통로로 갈 때부터, 그 뒤로는 늘. 밤에는 꺼진다.
            bool night = state.Phase == CampaignPhase.Night || state.Phase == CampaignPhase.Dawn;
            if (WaitingRoomRehearsal.IsPreTutorialRestricted)
            {
                if (!poweredOn && atStage && (WaitingRoomRehearsal.IsCompleted || state.Phase == CampaignPhase.Performance)) poweredOn = true;
            }
            else poweredOn = true;
            if (!poweredOn || night) { if (monitor.IsOn) monitor.TurnOff(); return; }

            // 그림: 남은 시간이 적을수록 우선한다. 첫 공연 전(리허설 단계)에는 지켜보는 그림만.
            bool counting = state.Timer == RestTimerState.Running, chasing = state.Timer == RestTimerState.Pursuit;
            Texture image = watching;
            if (!WaitingRoomRehearsal.IsPreTutorialRestricted)
            {
                if (chasing || counting && state.TimeRemaining <= goneSeconds) image = gone;
                else if (away || counting && state.TimeRemaining <= boredSeconds) image = bored;
            }
            monitor.ShowImage(image != null ? image : watching);
        }

        static bool Inside(BoxCollider box, Vector3 feet) => box != null && box.enabled && box.gameObject.activeInHierarchy
            && new Bounds(box.center, box.size).Contains(box.transform.InverseTransformPoint(feet + Vector3.up * .4f));
    }
}
