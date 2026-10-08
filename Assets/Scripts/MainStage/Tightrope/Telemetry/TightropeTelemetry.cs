using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Telemetry;
using CurtainCall.Tightrope.Fire;
using CurtainCall.Tightrope.Results;
using CurtainCall.Tightrope.Saws;
using UnityEngine;

namespace CurtainCall.Tightrope.Telemetry
{
    /// <summary>공개 사건을 개인 시도에 연결한다. 판정·네트워크 신원 인증·외부 전송은 하지 않는다.</summary>
    [RequireComponent(typeof(GameplayRecorder))]
    public sealed class TightropeTelemetry : MonoBehaviour
    {
        const string ContentId = "tightrope";
        const float SampleInterval = 0.1f;
        readonly Dictionary<string, bool> presence = new();
        GameplayRecorder recorder;
        TightropeRun run;
        TightropeResults results;
        TightropeCourse course;
        TightropeFire fire;
        TightropeSaws saws;
        PiggybackSystem piggyback;
        TightropeSettings settings;
        NetworkPlayer player, below, above;
        PlayerMover mover;
        PlayerBalance balance;
        PlayerCondition condition;
        PlayerInputReader input;
        PlayerInteraction interaction;
        PlayerInputFrame previousInput;
        float nextSample;
        int previousLane = TightropeCourse.NoLane;
        bool landingPending;
        JumpKind landedJump;
        int landedDirection;

        [Serializable]
        sealed class StartingSettings
        {
            public string configuredBase;
            public string configuredTightrope;
            public string appliedCourse;
            public string appliedFire;
        }

        public GameplayRecorder Recorder => recorder;
        bool Capturing => recorder != null && recorder.Current != null && recorder.Current.captureStatus.recording
            && run != null && results != null && run.Round == recorder.Current.round
            && results.AttemptId == recorder.Current.attemptId && results.RoomId == recorder.Current.roomId
            && results.SessionId == recorder.Current.sessionId;
        float HostElapsed => run == null ? 0f : Mathf.Max(0f, run.TimeLimit - run.TimeRemaining);

        void Awake() { recorder = GetComponent<GameplayRecorder>(); }

        void Update()
        {
            Bind();
            BindLocal();
            TryBegin();
            if (!Capturing || run.State != TightropeRunState.Running || input == null) return;
            var frame = input.Current;
            if (frame.Move != previousInput.Move || frame.Posture != previousInput.Posture)
                Record(GameplayActions.InputChanged, ObservationKind.LocalIntent, frame.Move, frame.Posture);
            if (frame.AuxDirection != previousInput.AuxDirection)
                Record(GameplayActions.InputDirectionChanged, ObservationKind.LocalIntent, frame.AuxDirection);
            if (frame.ActionPressed)
                Record(GameplayActions.Jump, ObservationKind.LocalIntent,
                    (float)(frame.AuxDirection == 0 ? JumpKind.InPlace : JumpKind.Lane), frame.AuxDirection);
            previousInput = frame;
        }

        void LateUpdate()
        {
            if (!Capturing) { landingPending = false; return; }
            if (landingPending)
            {
                landingPending = false;
                Record(GameplayActions.Landing, ObservationKind.Observed, (float)landedJump, landedDirection);
            }
            if (run.State != TightropeRunState.Running)
            {
                recorder.Stop("runStoppedWithoutResult");
                return;
            }
            if (Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + SampleInterval;
            CaptureSample();
        }

        void Bind()
        {
            if (run == null && TightropeRun.Current != null)
            {
                run = TightropeRun.Current;
                course = run.GetComponent<TightropeCourse>();
                run.StateChanged += HandleRunState;
                run.RoundChanged += HandleRound;
            }
            if (results == null && run != null && run.Results != null)
            {
                results = run.Results;
                results.AttemptStarted += TryBegin;
                results.RosterChanged += HandleRoster;
                results.ResultConfirmed += HandleResult;
                results.Cleared += HandleCleared;
            }
            if (fire == null && TightropeFire.Current != null)
            {
                fire = TightropeFire.Current;
                fire.Hit += HandleFire;
            }
            if (saws == null && TightropeSaws.Current != null)
            {
                saws = TightropeSaws.Current;
                saws.Hit += HandleSaw;
            }
            if (piggyback == null && PiggybackSystem.Current != null)
            {
                piggyback = PiggybackSystem.Current;
                piggyback.LinksChanged += HandleLinks;
                below = player == null ? null : piggyback.GetBelow(player);
                above = player == null ? null : piggyback.GetAbove(player);
            }
            if (settings == null)
            {
                settings = GameSettings.Tightrope;
                if (settings != null) settings.Changed += HandleSettings;
            }
        }

        void BindLocal()
        {
            var local = NetworkPlayer.Local;
            if (ReferenceEquals(player, local) && (ReferenceEquals(player, null) || player != null)) return;
            if (Capturing) recorder.Stop("localPlayerChanged");
            UnbindLocal();
            player = local;
            if (player == null) { if (Capturing) recorder.Stop("localPlayerDespawned"); return; }
            mover = player.GetComponent<PlayerMover>();
            balance = player.GetComponent<PlayerBalance>();
            condition = player.GetComponent<PlayerCondition>();
            input = player.GetComponent<PlayerInputReader>();
            interaction = player.GetComponent<PlayerInteraction>();
            player.StateChanged += HandlePlayerState;
            if (mover != null) { mover.AirborneChanged += HandleAirborne; mover.Teleported += HandleTeleport; }
            if (condition != null) condition.Changed += HandleBody;
            if (interaction != null)
            {
                interaction.InteractRequested += HandleInteract;
                interaction.ReleaseRequested += HandleRelease;
            }
            below = piggyback == null ? null : piggyback.GetBelow(player);
            above = piggyback == null ? null : piggyback.GetAbove(player);
            previousInput = default;
        }

        void TryBegin()
        {
            if (run == null || results == null || course == null || player == null
                || run.State != TightropeRunState.Running || run.Round <= 0 || run.Round != results.Round
                || !results.IsAttemptOpen || string.IsNullOrEmpty(results.AttemptId)
                || results.Find(results.AttemptId) != null) return;
            if (Capturing) return;
            var unsupported = new List<string>
            {
                "participantIdentityNetworkMapping", "handholdOutcome", "platformContact",
                "developerCommandHooks", "baseSettingsChanged", "clientFinalPositionBeforeCeremony"
            };
            var session = NetworkSessionManager.Instance;
            if (session == null || !session.IsHost) unsupported.Add("clientFireHitCause");
            if (fire == null || fire.Round != run.Round) unsupported.Add("appliedFireSettingsUnavailable");
            string snapshot = JsonUtility.ToJson(new StartingSettings
            {
                configuredBase = JsonUtility.ToJson(GameSettings.Base),
                configuredTightrope = JsonUtility.ToJson(settings),
                appliedCourse = course.Shape.ToJson(),
                appliedFire = fire != null && fire.Round == run.Round ? fire.ExportSettings() : ""
            });
            recorder.Begin(ContentId, results.RoomId, results.SessionId, results.AttemptId, results.Round, snapshot, unsupported);
            if (!Capturing) return;
            presence.Clear();
            landingPending = false;
            previousInput = default;
            previousLane = TightropeCourse.NoLane;
            nextSample = Time.unscaledTime + SampleInterval;
            below = piggyback == null ? null : piggyback.GetBelow(player);
            above = piggyback == null ? null : piggyback.GetAbove(player);
            HandleRoster();
            CaptureSample();
            recorder.Checkpoint();
        }

        void HandleRoster()
        {
            if (!Capturing) return;
            var people = ConvertParticipants(results.Participants);
            foreach (var person in people)
            {
                bool known = presence.TryGetValue(person.participantKey, out bool connected);
                if (!known || connected != person.connected)
                    recorder.Record(person.connected ? GameplayActions.ParticipantJoined : GameplayActions.ParticipantLeft,
                        ObservationKind.HostConfirmed, person.participantKey, HostElapsed, false, person.slot);
                presence[person.participantKey] = person.connected;
            }
            recorder.SetParticipants(people);
        }

        static List<ParticipantRecord> ConvertParticipants(IReadOnlyList<TightropeParticipantResult> source)
        {
            var people = new List<ParticipantRecord>(source.Count);
            foreach (var person in source)
                people.Add(new ParticipantRecord
                {
                    participantKey = person.ParticipantKey, clientId = person.ClientId, slot = person.Slot,
                    connected = person.Connected, startingLostParts = (int)person.StartingLostParts,
                    lostParts = (int)person.LostParts, outcome = person.Outcome.ToString()
                });
            return people;
        }

        void HandleResult(TightropeResult result)
        {
            if (Capturing && recorder.Current.attemptId == result.AttemptId
                && run.State == TightropeRunState.Running)
            {
                // 호스트 확정 콜백은 세리머니 순간이동 전이다. 클라이언트 수신 순서는 보장하지 않는다.
                var session = NetworkSessionManager.Instance;
                if (session != null && session.IsHost) CaptureSample();
            }
            if (recorder.Current != null && recorder.Current.attemptId == result.AttemptId)
                landingPending = false;
            recorder.Confirm(new ConfirmedResultRecord
            {
                roomId = result.RoomId, sessionId = result.SessionId, attemptId = result.AttemptId,
                round = result.Round, succeeded = result.Succeeded, reason = result.Reason.ToString(),
                elapsed = result.Elapsed, remaining = result.Remaining, performanceElapsed = result.PerformanceElapsed,
                finishedUtc = result.FinishedUtc, participants = ConvertParticipants(result.Participants)
            }, ContentId);
        }

        void CaptureSample()
        {
            if (!Capturing || player == null || mover == null || course == null) return;
            Vector3 position = player.transform.position;
            bool airborne = mover.IsAirborne;
            int lane = TightropeCourse.NoLane;
            bool onRope = !airborne && course.IsOnRope(position, mover.BodyRadius, out lane)
                && course.IsLaneUsable(lane, course.GetDistance(position));
            if (!onRope) lane = TightropeCourse.NoLane;
            if (lane != previousLane)
            {
                Record(GameplayActions.LaneChanged, ObservationKind.Observed, previousLane, lane);
                previousLane = lane;
            }
            var frame = input == null ? default : input.Current;
            recorder.Sample(new GameplaySample
            {
                hostElapsed = HostElapsed, hostTimeSynchronized = false,
                x = position.x, y = position.y, z = position.z, distance = course.GetDistance(position),
                airborne = airborne, onRope = onRope, movement = frame.Move, balanceInput = frame.Posture,
                balance = balance == null ? 0f : balance.Value, lane = lane,
                lostParts = (int)player.LostParts, state = player.State.ToString()
            });
        }

        string ParticipantKey(NetworkPlayer target)
        {
            if (target == null || results == null) return null;
            foreach (var person in results.Participants)
                if (person.Connected && person.ClientId == target.OwnerClientId) return person.ParticipantKey;
            return null;
        }

        void Record(string action, ObservationKind kind, float value = 0f, float secondary = 0f, string cause = "unknown")
        {
            if (Capturing && run.State == TightropeRunState.Running)
                recorder.Record(action, kind, ParticipantKey(player), HostElapsed, false, value, secondary, cause);
        }

        void HandleAirborne(bool airborne)
        {
            if (!Capturing || run.State != TightropeRunState.Running || mover == null) return;
            if (airborne)
            {
                landingPending = false;
                Record(GameplayActions.Jump, ObservationKind.Observed, (float)mover.CurrentJump, mover.LaneJumpDirection);
            }
            else
            {
                landedJump = mover.CurrentJump;
                landedDirection = mover.LaneJumpDirection;
                landingPending = true;
            }
        }

        void HandleTeleport()
        {
            landingPending = false;
            Record(GameplayActions.Teleported, ObservationKind.Observed);
        }
        void HandleBody(BodyPart previous, BodyPart next) =>
            Record(GameplayActions.BodyDamage, ObservationKind.Observed, (int)(next & ~previous), (int)(previous & ~next));
        void HandlePlayerState(PlayerState previous, PlayerState next) =>
            Record(GameplayActions.PlayerState, ObservationKind.Observed, (float)previous, (float)next);
        void HandleInteract() => Record(GameplayActions.InteractionRequested, ObservationKind.LocalIntent);
        void HandleRelease() => Record(GameplayActions.InteractionReleased, ObservationKind.LocalIntent);
        void HandleSettings() => Record(GameplayActions.SettingsChanged, ObservationKind.Observed);
        void HandleLinks()
        {
            if (piggyback == null || player == null) return;
            var nextBelow = piggyback.GetBelow(player);
            var nextAbove = piggyback.GetAbove(player);
            if (below != nextBelow || above != nextAbove)
                Record(GameplayActions.Piggyback, ObservationKind.Observed,
                    nextBelow == null ? -1 : nextBelow.Slot, nextAbove == null ? -1 : nextAbove.Slot);
            below = nextBelow;
            above = nextAbove;
        }
        void HandleSaw(SawHit hit)
        {
            if (!Capturing || run.State != TightropeRunState.Running) return;
            recorder.Record(GameplayActions.Saw, ObservationKind.HostConfirmed, ParticipantKey(hit.Player),
                HostElapsed, false, (int)hit.Part, (int)hit.Requested, "saw");
        }
        void HandleFire(FireHit hit)
        {
            if (!Capturing || run.State != TightropeRunState.Running || hit.Round != recorder.Current.round) return;
            recorder.Record(GameplayActions.Fire, ObservationKind.HostConfirmed, ParticipantKey(hit.Player),
                HostElapsed, false, (float)hit.Kind, hit.Segment, "fire");
        }
        void HandleRunState(TightropeRunState previous, TightropeRunState next)
        {
            if (next == TightropeRunState.Running) TryBegin();
        }
        void HandleRound()
        {
            landingPending = false;
            if (recorder.Current != null && recorder.Current.captureStatus.recording && run.Round != recorder.Current.round)
                recorder.Stop("roundChanged");
        }
        void HandleCleared() { landingPending = false; recorder.Stop("sessionCleared"); presence.Clear(); }

        void UnbindLocal()
        {
            if (player != null) player.StateChanged -= HandlePlayerState;
            if (mover != null) { mover.AirborneChanged -= HandleAirborne; mover.Teleported -= HandleTeleport; }
            if (condition != null) condition.Changed -= HandleBody;
            if (interaction != null)
            {
                interaction.InteractRequested -= HandleInteract;
                interaction.ReleaseRequested -= HandleRelease;
            }
            player = null;
            mover = null;
            balance = null;
            condition = null;
            input = null;
            interaction = null;
            below = above = null;
            landingPending = false;
        }

        void OnDisable()
        {
            recorder.Stop("adapterDisabled");
            UnbindLocal();
            if (results != null)
            {
                results.AttemptStarted -= TryBegin;
                results.RosterChanged -= HandleRoster;
                results.ResultConfirmed -= HandleResult;
                results.Cleared -= HandleCleared;
            }
            if (run != null) { run.StateChanged -= HandleRunState; run.RoundChanged -= HandleRound; }
            if (fire != null) fire.Hit -= HandleFire;
            if (saws != null) saws.Hit -= HandleSaw;
            if (piggyback != null) piggyback.LinksChanged -= HandleLinks;
            if (settings != null) settings.Changed -= HandleSettings;
            run = null;
            results = null;
            course = null;
            fire = null;
            saws = null;
            piggyback = null;
            settings = null;
            presence.Clear();
        }
    }
}
