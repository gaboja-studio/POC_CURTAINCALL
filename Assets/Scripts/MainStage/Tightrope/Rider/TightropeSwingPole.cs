using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>외줄의 접근·받기만 연결한다. 봉 반동·놓기 속도·공중 보정은 인간대포를 그대로 사용한다.</summary>
    [DefaultExecutionOrder(-55)]
    public sealed partial class TightropeSwingPole : MonoBehaviour
    {
        enum PoleMode : byte { Idle, Holding, Returning }
        const ulong Nobody = ulong.MaxValue;
        TightropeCourse course;
        TightropeRun run;
        HumanCannonPole physicalPole;
        PoleMode mode;
        ulong holder = Nobody;
        int lane = -1, round = -1, version;
        double startedAt, changedAt, pendingUntil, feedbackUntil, nextSnapshotAt, nextDriveAt, lastDriveAt;
        float angle, angularSpeed, drive, returnAngle, lastSentDrive;
        bool released, localAbort;
        string feedback;
        readonly Dictionary<ulong, double> flights = new();
        readonly Dictionary<ulong, PendingLanding> pendingLandings = new();
        readonly List<ulong> pendingLandingActors = new();
        struct PendingLanding
        {
            public int Round;
            public double FlightUntil, Expires;
        }
        public static TightropeSwingPole Current { get; private set; }
        public float AirControlSpeed => physicalPole != null ? physicalPole.AirControlSpeed : 0f;
        public float LandingDistance => course.SwingGapTo + SwingPoleMath.ExitOffset;
        bool OwnOccupied => Active && mode == PoleMode.Holding;
        float OwnAngle { get { Sample(out float value, out _); return value; } }
        bool OwnCanLetGo => OwnOccupied && course.GetDistance(PredictLanding(true)) >= course.SwingGapTo + .35f;
        public bool IsOccupied => LocalPole.OwnOccupied;
        public float SwingAngle => LocalPole.OwnAngle;
        public bool CanLetGo => LocalPole.OwnCanLetGo;
        public bool CanGrabPole(NetworkPlayer player) => mode == PoleMode.Idle && Clock >= pendingUntil
            && flightMover == null && CanGrab(player, out _);
        public string LocalFeedback => LatestFeedback();
        bool Active => isActiveAndEnabled && course != null && course.HasSwingGapOnLane(assignedLane) && course.IsGameplayActive
            && run != null && run.State == TightropeRunState.Running;
        double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;

        void Awake()
        {
            course = GetComponentInParent<TightropeCourse>();
            var pivot = new GameObject("HumanCannonPole physics (runtime)");
            pivot.transform.SetParent(transform, false);
            physicalPole = pivot.AddComponent<HumanCannonPole>();
            PlacePivot();
            EnsureLanePoles();
        }
        void OnEnable()
        {
            if (coordinator != null) return;
            Current = this;
            foreach (var pole in poles) if (pole != this && pole != null) pole.enabled = true;
        }
        void PlacePivot()
        {
            if (course == null || physicalPole == null) return;
            physicalPole.transform.SetPositionAndRotation(course.transform.position
                + course.Forward * (course.SwingGapFrom - SwingPoleMath.EntryOffset) + Vector3.up * physicalPole.HangLength,
                Quaternion.LookRotation(course.Forward));
        }
        void Update()
        {
            EnsureLanePoles(); run = TightropeRun.Current; PlacePivot(); Connect();
            int generation = run != null ? run.Round : -1;
            if (generation != round)
            {
                ResetLocal(); ResetState(); round = generation;
                if (session != null && session.IsHost) Publish(); else Send(Operation.Sync);
            }
            if (!Active) { ResetLocal(); ResetState(); return; }
            if (session != null && session.IsHost)
            {
                if (mode == PoleMode.Holding)
                {
                    var rider = Find(holder);
                    if (rider == null || !rider.IsSpawned || rider.State != PlayerState.Normal
                        || !course.IsLaneUsable(lane, course.SwingGapFrom - SwingPoleMath.EntryOffset)
                        || !course.IsLaneUsable(lane, LandingDistance)) BeginReturn(false);
                    else
                    {
                        if (Clock - lastDriveAt > .3d) drive = 0f; // 인간대포와 같은 입력 만료 시간.
                        physicalPole.Step(ref angle, ref angularSpeed, drive, Time.deltaTime, GameSettings.Base.Character.Gravity);
                        changedAt = Clock;
                        // 인간대포 봉의 0초는 무제한이다. 제한이 설정된 경우에만 자동으로 놓는다.
                        if (physicalPole.HoldSeconds > 0f && Clock - startedAt >= physicalPole.HoldSeconds) BeginReturn(true);
                        else if (Clock >= nextSnapshotAt) { version++; Publish(); nextSnapshotAt = Clock + .1d; }
                    }
                }
                if (mode == PoleMode.Returning && Clock - changedAt >= .65d)
                { mode = PoleMode.Idle; holder = Nobody; version++; Publish(); }
                RetryPendingLandings();
            }
            var local = NetworkPlayer.Local;
            if (OwnHeld(local) && local.State == PlayerState.Normal && !localAbort)
            {
                if (binding == null) Bind(local);
                PlaceLocal();
            }
            else if (binding != null) EndBinding(false);
        }

        public bool TryHandleInput(PlayerControlContext context, in PlayerInputFrame input)
        {
            var local = NetworkPlayer.Local;
            if (coordinator == null && ForPlayer(local) != this)
                return ForPlayer(local).TryHandleInput(context, input);
            if (!Active || local == null || context.Mover == null || context.Mover.gameObject != local.gameObject) return false;
            if (flightMover != null) { context.Mover.ClearPendingInput(); return true; }
            if (OwnHeld(local))
            {
                context.Mover.ClearPendingInput();
                if (!InputGate.IsBlocked)
                {
                    if (Clock >= nextDriveAt || Mathf.Abs(input.Move - lastSentDrive) > .01f)
                    { Send(Operation.Swing, input.Move); lastSentDrive = input.Move; nextDriveAt = Clock + .05d; }
                    if (input.ActionPressed) Send(Operation.LetGo);
                }
                return true;
            }
            if (Clock < pendingUntil) { context.Mover.ClearPendingInput(); return true; }
            if (!input.InteractTapped || InputGate.IsBlocked || !CanGrab(local, out _)) return false;
            TightropeCatchSystem.Current?.CancelLocal(); context.Mover.ClearPendingInput();
            if (mode != PoleMode.Idle) { Tell("동료가 회전봉을 사용 중이에요"); return true; }
            pendingUntil = Clock + .6d; Send(Operation.Grab); return true;
        }

        bool CanGrab(NetworkPlayer player, out int supported)
        {
            supported = -1;
            if (!Active || player == null || !player.IsSpawned || player.State != PlayerState.Normal
                || !player.TryGetComponent(out PlayerMover mover) || !mover.ControlEnabled || (player.IsLocal && !mover.Simulated) || mover.PassThrough
                || (player.IsLocal ? mover.IsAirborne : player.IsAirborne) || !course.TryGetSupportingRope(mover, out supported)
                || supported != assignedLane || !course.HasSwingGapOnLane(supported)
                || !course.IsLaneUsable(supported, LandingDistance)) return false;
            if (PiggybackSystem.Current != null && PiggybackSystem.Current.IsInStack(player)) return false;
            if (IsCrossing(player)) return false;
            float reach = player.TryGetComponent(out PlayerInteraction interaction) ? interaction.ReachMultiplier : 1f;
            return PlayerInteraction.InRange(player, WorldFeet(0f, assignedLane) + Vector3.up * SwingPoleMath.GripHeight, 1.75f * reach);
        }

        void ServerHandle(ulong sender, Operation operation, float input)
        {
            if (operation == Operation.Sync) { Publish(sender); return; }
            if (!Active) return;
            if (operation == Operation.Landed)
            {
                var player = Find(sender);
                if (!flights.TryGetValue(sender, out double until) || Clock > until || player == null
                    || !player.IsSpawned || player.State != PlayerState.Normal) return;
                if (!CompleteLanding(player) && !pendingLandings.ContainsKey(sender))
                    pendingLandings.Add(sender, new PendingLanding { Round = round, FlightUntil = until, Expires = Clock + .35d });
                return;
            }
            if (operation == Operation.Abort) { pendingLandings.Remove(sender); flights.Remove(sender); }
            if (operation == Operation.Grab && mode == PoleMode.Idle && CanGrab(Find(sender), out int targetLane))
            {
                holder = sender; lane = targetLane; startedAt = changedAt = lastDriveAt = Clock;
                angle = angularSpeed = drive = 0f; mode = PoleMode.Holding; released = false; version++; Publish(); return;
            }
            if (holder != sender || mode != PoleMode.Holding) { Publish(sender); return; }
            if (operation == Operation.Swing) { drive = Mathf.Clamp(input, -1f, 1f); lastDriveAt = Clock; }
            else if (operation == Operation.LetGo) BeginReturn(true);
            else if (operation == Operation.Abort) BeginReturn(false);
        }

        bool CompleteLanding(NetworkPlayer player)
        {
            if (!player.TryGetComponent(out PlayerMover mover) || !course.TryGetSupportingRope(mover, out int supported)
                || !course.IsLaneUsable(supported, course.GetDistance(player.transform.position))
                || Mathf.Abs(player.transform.position.y - course.transform.position.y) > .3f) return false;
            ulong actor = player.OwnerClientId;
            pendingLandings.Remove(actor); flights.Remove(actor);
            TightropeLandingWaves.Current?.RegisterExternalLanding(player);
            return true;
        }

        void RetryPendingLandings()
        {
            pendingLandingActors.Clear();
            pendingLandingActors.AddRange(pendingLandings.Keys);
            foreach (ulong actor in pendingLandingActors)
            {
                var pending = pendingLandings[actor];
                var player = Find(actor);
                // 생성 때의 비행을 그대로 확인한다. 새 비행이나 라운드로 요청을 넘기지 않는다.
                if (pending.Round != round || Clock > pending.Expires || Clock > pending.FlightUntil
                    || !flights.TryGetValue(actor, out double until) || until != pending.FlightUntil
                    || player == null || !player.IsSpawned || player.State != PlayerState.Normal)
                { pendingLandings.Remove(actor); continue; }
                CompleteLanding(player);
            }
        }

        void BeginReturn(bool launch)
        {
            returnAngle = angle; mode = PoleMode.Returning; changedAt = Clock; released = launch; version++;
            pendingLandings.Remove(holder);
            if (launch)
            {
                flights[holder] = Clock + 6d;
                TightropeLandingWaves.Current?.RegisterExternalLaunch(Find(holder));
                TightropeCatchSystem.Current?.RegisterSwingRelease(Find(holder), -1);
            }
            if (binding != null) EndBinding(launch);
            Publish();
        }
        void Sample(out float sampledAngle, out float sampledSpeed)
        {
            sampledAngle = angle; sampledSpeed = angularSpeed;
            if (mode == PoleMode.Returning)
            { sampledAngle = Mathf.Lerp(returnAngle, 0f, Mathf.Clamp01((float)(Clock - changedAt) / .65f)); sampledSpeed = 0f; }
            else if (mode == PoleMode.Idle) sampledAngle = sampledSpeed = 0f;
            else if (session == null || !session.IsHost)
                physicalPole.Step(ref sampledAngle, ref sampledSpeed, drive, Mathf.Clamp((float)(Clock - changedAt), 0f, .25f), GameSettings.Base.Character.Gravity);
        }
        Vector3 WorldFeet(float value, int slot) => physicalPole.HangingPosition(value) + course.Right * course.Layout.GetLaneSide(slot);
        void ResetState()
        {
            mode = PoleMode.Idle; holder = Nobody; lane = -1; version = 0;
            startedAt = changedAt = nextSnapshotAt = 0d; angle = angularSpeed = drive = returnAngle = 0f; released = false; flights.Clear();
            pendingLandings.Clear(); pendingLandingActors.Clear();
        }
        static NetworkPlayer Find(ulong id)
        { foreach (var player in NetworkPlayer.All) if (player != null && player.OwnerClientId == id) return player; return null; }
        void Tell(string message) { feedback = message; feedbackUntil = Clock + 2d; }
        void LateUpdate()
        {
            SimulateFlight(); UpdateViews();
            if (!OwnOccupied) return;
            var rider = Find(holder);
            if (rider != null && !rider.IsLocal && rider.State == PlayerState.Normal)
                rider.transform.SetPositionAndRotation(WorldFeet(OwnAngle, lane), Quaternion.LookRotation(course.Forward));
        }
        void OnDisable()
        {
            ResetLocal(); Disconnect(); ResetState();
            if (Current == this) Current = null;
            if (coordinator == null)
                foreach (var pole in poles) if (pole != this && pole != null) pole.enabled = false;
            if (viewRoot != null) viewRoot.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            DestroyViews();
            if (coordinator == null)
                foreach (var pole in poles) if (pole != this && pole != null) Destroy(pole.gameObject);
        }
    }
}
