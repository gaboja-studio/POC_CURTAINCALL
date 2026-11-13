using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>
    /// 지면불 반복: 예고(보이는 부분 빨간 깜빡임) → 분출 → 다시 예고. 자식 아래 모든 지면불을 같은 시각으로 맞춘다.
    /// 지정한 줄은 반복마다 그 줄에서 무작위 몇 개만 예고·분출한다(반복 번호로 고르므로 모든 화면이 같은 개를 고른다).
    /// 시각은 HumanCannonHazardCourse의 공유 시각(네트워크 서버 시각)을 쓰고, 없으면 로컬 시간을 쓴다.
    /// </summary>
    public sealed class HumanCannonGroundFireCycle : MonoBehaviour
    {
        [Tooltip("불이 나오기 전 빨간 깜빡임 시간(초).")]
        [SerializeField, Min(0f)] float warnSeconds = 3.5f;
        [Tooltip("불이 나오는 시간(초).")]
        [SerializeField, Min(0.1f)] float fireSeconds = 2f;
        [Tooltip("깜빡임 한 번(빨강 + 원래 색)의 길이(초).")]
        [SerializeField, Min(0.05f)] float blinkSeconds = 0.5f;
        [SerializeField] Color warnColor = new Color(1f, 0.08f, 0.04f, 1f);
        [Tooltip("무작위로 일부만 나오는 줄. 자식 이름의 z 번호(예: '... z3_x1'의 3).")]
        [SerializeField] int[] randomRows = { 1, 3, 4 };
        [Tooltip("무작위 줄에서 반복마다 동시에 나오는 개수.")]
        [SerializeField, Min(0)] int randomCount = 4;
        [Tooltip("분출 중 부상 판정 상자 크기(m, 가로 x·높이 y·앞뒤 z). 분사판 가운데 바닥에서 위로 잡는다.")]
        [SerializeField] Vector3 damageSize = new Vector3(2.5f, 6f, 2.4f);
        [System.Serializable]
        public sealed class SwitchGroup
        {
            [Tooltip("하나라도 한 번 잡으면 rows 줄 지면불을 재시작 전까지 끄는 공통 그네(톱날벽 연결 그네와 같은 유지 방식).")]
            public HumanCannonPole[] poles = System.Array.Empty<HumanCannonPole>();
            [Tooltip("이 그네로 꺼지는 줄. 자식 이름의 z 번호.")]
            public int[] rows = System.Array.Empty<int>();
            [System.NonSerialized] public bool off;
        }
        [SerializeField] SwitchGroup[] switchGroups = System.Array.Empty<SwitchGroup>();
        [Tooltip("이 톱날벽이 열린 뒤(연결 그네를 잡은 뒤)에만 예고·분출하는 지면불. 열리기 전에는 외형도 숨긴다.")]
        [SerializeField] HumanCannonSawGate openingGate;
        [SerializeField] Transform[] gateFires = System.Array.Empty<Transform>();
        [Tooltip("이 단계 이상을 골랐을 때만 지면불·끄는 그네가 나온다(단계 = 대기실에서 고른 HumanCannonHazardCourse.Difficulty).")]
        [SerializeField, Range(1, 3)] int minimumStage = 2;
        [Tooltip("이 단계 이하일 때만 나온다. 3단계는 아직 미구현이라 1단계 구성으로 나온다(2026-11-13 사용자 결정).")]
        [SerializeField, Range(1, 3)] int maximumStage = 2;
        [Tooltip("단계에 따라 함께 켜고 끄는 외형(끄는 그네 천장 등).")]
        [SerializeField] GameObject[] stageObjects = System.Array.Empty<GameObject>();
        int stageShown = -1;

        sealed class Fire
        {
            public Vector3 baseCenter;
            public bool gated;
            public int row = -1;
            public MeshRenderer[] renderers;
            public ParticleSystem[] flames;
            public int firing = -1, red = -1, shown = -1;
        }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        static readonly Regex RowName = new(@" z(\d+)_x\d+$");
        readonly List<Fire> fires = new();
        readonly Dictionary<int, List<Fire>> rows = new();
        readonly HashSet<Fire> chosen = new();
        MaterialPropertyBlock block;
        long chosenCycle = long.MinValue;
        HumanCannonPoleSystem watchedPoles;

        void OnEnable()
        {
            block ??= new MaterialPropertyBlock();
            stageShown = -1;
            foreach (var group in switchGroups)
            {
                if (group == null) continue;
                group.off = false;
                foreach (var pole in group.poles) if (pole != null) pole.IsSwitch = true;
            }
            CurtainCall.Network.PlayerSync.NetworkPlayer.Restarted += OnRestarted;
            fires.Clear(); rows.Clear(); chosen.Clear(); chosenCycle = long.MinValue;
            foreach (Transform child in transform)
            {
                var fire = new Fire { renderers = child.GetComponentsInChildren<MeshRenderer>(true),
                    flames = child.GetComponentsInChildren<ParticleSystem>(true) };
                fire.baseCenter = child.position;
                fire.gated = System.Array.IndexOf(gateFires, child) >= 0;
                if (fire.renderers.Length > 0)
                {
                    var bounds = fire.renderers[0].bounds;
                    foreach (var view in fire.renderers) bounds.Encapsulate(view.bounds);
                    fire.baseCenter = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                }
                var match = RowName.Match(child.name);
                if (match.Success && System.Array.IndexOf(randomRows, int.Parse(match.Groups[1].Value)) >= 0)
                {
                    fire.row = int.Parse(match.Groups[1].Value);
                    if (!rows.TryGetValue(fire.row, out var list)) rows[fire.row] = list = new List<Fire>();
                    list.Add(fire);
                }
                fires.Add(fire);
            }
        }

        void Update()
        {
            double now = HumanCannonHazardCourse.Current != null ? HumanCannonHazardCourse.Current.Clock : Time.timeAsDouble;
            double period = warnSeconds + fireSeconds;
            long cycle = (long)System.Math.Floor(now / period);
            float t = (float)(now - cycle * period);
            bool firePhase = t >= warnSeconds;
            bool blink = !firePhase && t % blinkSeconds < blinkSeconds * 0.5f;
            if (cycle != chosenCycle) Choose(cycle);
            WatchPoles();
            int stage = HumanCannonHazardCourse.Current != null ? HumanCannonHazardCourse.Current.Difficulty : 1;
            bool stageOn = stage >= minimumStage && stage <= maximumStage;
            ApplyStage(stageOn);
            LatchHeldSwitches();
            foreach (var fire in fires)
            {
                bool off = fire.row >= 0 && SwitchedOff(fire.row);
                bool active = stageOn && (fire.row < 0 || !off && chosen.Contains(fire))
                    && (!fire.gated || openingGate != null && openingGate.IsOpen);
                // 이번 반복에 예고·분출하는 장치만 외형을 보인다.
                SetShown(fire, active);
                // 그네로 끈 불은 이미 예고·분출 중이어도 남은 불꽃까지 바로 지운다.
                SetFiring(fire, firePhase && active, off);
                SetRed(fire, blink && active);
            }
            if (firePhase) InjureTouching();
        }

        /// <summary>호스트만: 분출 중인 지면불에 닿은 생존자를 일반 불(부상)로 처리한다. 연속 부상은 공통 피해 대기시간이 막는다.</summary>
        void InjureTouching()
        {
            var session = CurtainCall.Network.Session.NetworkSessionManager.Instance;
            if (session == null || !session.IsHost) return;
            string obstacle = null;
            foreach (var player in CurtainCall.Network.PlayerSync.NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != CurtainCall.Network.PlayerSync.PlayerState.Normal
                    || !player.TryGetComponent(out CurtainCall.Player.PlayerMover mover)) continue;
                Vector3 feet = player.transform.position;
                foreach (var fire in fires)
                {
                    if (fire.firing != 1 || !Touches(fire, feet, mover.BodyRadius, mover.BodyHeight)) continue;
                    if (CurtainCall.Network.PlayerSync.NetworkPlayer.ServerCanChangeState != null
                        && !CurtainCall.Network.PlayerSync.NetworkPlayer.ServerCanChangeState(player, CurtainCall.Network.PlayerSync.PlayerState.Fallen)) break;
                    obstacle ??= CurtainCall.Telemetry.Pipeline.DetailLogIds.ObjectId(this, "OB_");
                    player.ServerHazardContact(CurtainCall.Damage.HazardDamageKind.OrdinaryFire, obstacle,
                        CurtainCall.Network.PlayerSync.NetworkPlayer.DeathOrigin.Hazard);
                    break;
                }
            }
        }

        bool Touches(Fire fire, Vector3 feet, float radius, float height)
        {
            Vector3 half = damageSize * 0.5f;
            Vector3 c = fire.baseCenter;
            return feet.x + radius > c.x - half.x && feet.x - radius < c.x + half.x
                && feet.z + radius > c.z - half.z && feet.z - radius < c.z + half.z
                && feet.y + height > c.y && feet.y < c.y + damageSize.y;
        }

        /// <summary>고른 단계가 minimumStage 미만이면 끄는 그네를 숨기고 잡을 수 없게 한다.</summary>
        void ApplyStage(bool on)
        {
            if (stageShown == (on ? 1 : 0)) return;
            stageShown = on ? 1 : 0;
            foreach (var group in switchGroups)
                if (group != null) foreach (var pole in group.poles)
                    if (pole != null) { pole.InteractionAvailable = on; pole.PresentationVisible = on; }
            foreach (var item in stageObjects) if (item != null && item.activeSelf != on) item.SetActive(on);
        }

        void WatchPoles()
        {
            var system = HumanCannonPoleSystem.Current;
            if (system == watchedPoles) return;
            if (watchedPoles != null) watchedPoles.OccupancyChanged -= OnOccupancy;
            watchedPoles = system;
            if (watchedPoles != null) watchedPoles.OccupancyChanged += OnOccupancy;
        }

        // 짧게 잡았다 놓아도 놓치지 않도록 잡는 순간도 받는다.
        void OnOccupancy(int index, ulong actor, bool occupied)
        {
            if (!occupied || watchedPoles == null) return;
            var pole = watchedPoles.Pole(index);
            foreach (var group in switchGroups)
                if (group != null && System.Array.IndexOf(group.poles, pole) >= 0) group.off = true;
        }

        void OnRestarted(CurtainCall.Network.PlayerSync.NetworkPlayer player)
        {
            foreach (var group in switchGroups) if (group != null) group.off = false;
        }

        void LatchHeldSwitches()
        {
            var system = HumanCannonPoleSystem.Current;
            if (system == null) return;
            for (int i = 0; i < system.PoleCount; i++)
            {
                if (system.OccupantCount(i) <= 0) continue;
                var pole = system.Pole(i);
                foreach (var group in switchGroups)
                    if (group != null && !group.off && System.Array.IndexOf(group.poles, pole) >= 0) group.off = true;
            }
        }

        bool SwitchedOff(int row)
        {
            foreach (var group in switchGroups)
                if (group != null && group.off && System.Array.IndexOf(group.rows, row) >= 0) return true;
            return false;
        }

        void Choose(long cycle)
        {
            chosenCycle = cycle; chosen.Clear();
            foreach (var pair in rows)
            {
                var list = pair.Value;
                // 반복 번호와 줄 번호로 정해진 난수: 호스트·클라이언트가 같은 개를 고른다.
                var random = new System.Random(unchecked((int)(cycle * 7919 + pair.Key * 104729)));
                var order = new List<Fire>(list);
                int count = Mathf.Min(randomCount, order.Count);
                for (int i = 0; i < count; i++)
                {
                    int pick = random.Next(i, order.Count);
                    (order[i], order[pick]) = (order[pick], order[i]);
                    chosen.Add(order[i]);
                }
            }
        }

        void OnDisable()
        {
            foreach (var fire in fires) { SetRed(fire, false); SetShown(fire, true); fire.red = fire.firing = fire.shown = -1; }
            CurtainCall.Network.PlayerSync.NetworkPlayer.Restarted -= OnRestarted;
            if (watchedPoles != null) watchedPoles.OccupancyChanged -= OnOccupancy;
            watchedPoles = null;
        }

        static void SetShown(Fire fire, bool on)
        {
            if (fire.shown == (on ? 1 : 0)) return;
            fire.shown = on ? 1 : 0;
            foreach (var view in fire.renderers) if (view != null) view.enabled = on;
        }

        static void SetFiring(Fire fire, bool on, bool clear = false)
        {
            if (fire.firing == (on ? 1 : 0))
            {
                if (clear) foreach (var flame in fire.flames) if (flame != null && flame.particleCount > 0) flame.Clear(true);
                return;
            }
            fire.firing = on ? 1 : 0;
            foreach (var flame in fire.flames)
            {
                if (flame == null) continue;
                if (on) flame.Play(true);
                else flame.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void SetRed(Fire fire, bool on)
        {
            if (fire.red == (on ? 1 : 0)) return;
            fire.red = on ? 1 : 0;
            foreach (var view in fire.renderers)
            {
                if (view == null) continue;
                if (!on) { view.SetPropertyBlock(null); continue; }
                block.Clear();
                block.SetColor(BaseColor, warnColor);
                block.SetColor(LegacyColor, warnColor);
                view.SetPropertyBlock(block);
            }
        }
    }
}
