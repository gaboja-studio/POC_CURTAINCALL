#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope.Fire;
using CurtainCall.Tightrope.Saws;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.Tightrope.Tests
{
    public sealed partial class TightropeMultiplayerProbe
    {
        readonly List<InputDevice> journeyDevices = new();
        readonly List<string> journeyEvents = new();
        TightropePracticeBotBrain journeyBrain;
        bool journey;
        int journeyPhase;
        float journeyJumpAt, journeyTapAt, journeyLongUntil, journeyTraceAt;
        string journeyLastEvent;
        string journeyReportPath;
        float journeyReportAt;

        public static void SetJourneyReportPath(string path) { if (current != null) current.journeyReportPath = path; }

        void LateUpdate()
        {
            var p = NetworkPlayer.Local;
            if (journey && p != null && journeyPhase == 3 && p.TryGetComponent(out PlayerInputReader reader)
                && (reader.Current.InteractTapped || reader.Current.ActionPressed) && journeyEvents.Count < 2048)
            {
                var t = TightropeTrapeze.Current;
                journeyEvents.Add($"INPUT {Time.time:F3} F={reader.Current.InteractTapped} Space={reader.Current.ActionPressed} d={TightropeCourse.Current.GetDistance(p.transform.position):F3} y={p.transform.position.y:F3} held={t.HeldPole(p)} grab0={t.CanGrabPole(0,p)} attach0={t.CanAttachToPole(0,p)}");
            }
            if (!journey || string.IsNullOrEmpty(journeyReportPath) || Time.unscaledTime < journeyReportAt) return;
            journeyReportAt = Time.unscaledTime + 2f;
            System.IO.File.WriteAllText(journeyReportPath, Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                probe = Newtonsoft.Json.Linq.JObject.Parse(SnapshotJson()),
                journey = Newtonsoft.Json.Linq.JObject.Parse(JourneyStatus())
            }));
        }

        /// <summary>4개의 실제 소유자 프로세스에서 입력만 생성한다. 위치·부상·관문 판정·난이도는 바꾸지 않는다.</summary>
        public static bool BeginJourney()
        {
            if (current == null || NetworkPlayer.Local == null || NetworkPlayer.All.Count != 4) return false;
            var self = NetworkPlayer.Local;
            var partner = NetworkPlayer.All.First(p => p.OwnerClientId == (self.OwnerClientId ^ 1UL));
            current.journeyBrain = new TightropePracticeBotBrain(self, partner);
            current.journeyBrain.SetRole((self.OwnerClientId & 1UL) == 0 ? TightropeBotRole.Lower : TightropeBotRole.Upper);
            current.journey = true; current.journeyPhase = 0;
            InputSystem.onBeforeUpdate -= current.JourneyBeforeInputUpdate;
            InputSystem.onBeforeUpdate += current.JourneyBeforeInputUpdate;
            current.journeyEvents.Clear(); current.journeyLastEvent = null;
            TightropeTrapeze.Current.RequestObserved += current.JourneyRequestObserved;
            current.journeyJumpAt = current.journeyTapAt = current.journeyLongUntil = 0f;
            current.walking = false; current.autoBalance = false;
            DevCache.SetValue(null, false);
            foreach (var device in InputSystem.devices.ToArray())
                if (device.native && device.enabled && (device is Mouse || device is Keyboard))
                { current.journeyDevices.Add(device); InputSystem.DisableDevice(device); }
            return true;
        }

        public static string JourneyStatus() => current == null ? "inactive" : Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            current.journeyPhase, current.status, events = current.journeyEvents,
            players = NetworkPlayer.All.Select(p => new
            {
                id = p.OwnerClientId, state = p.State.ToString(), d = TightropeCourse.Current.GetDistance(p.transform.position),
                lane = TightropeCourse.Current.Layout.GetLaneSlotAt(TightropeCourse.Current.GetSide(p.transform.position)),
                y = p.transform.position.y - TightropeCourse.Current.transform.position.y, parts = (int)p.LostParts,
                floor = PiggybackSystem.Current.GetFloor(p), trapeze = TightropeTrapeze.Current.HeldPole(p),
                lower = TightropeTrapeze.Current.IsLower(p), flight = TightropeTrapeze.Current.IsInFlight(p)
            }).ToArray()
        });

        void RestoreJourneyDevices()
        {
            if (TightropeTrapeze.Current != null) TightropeTrapeze.Current.RequestObserved -= JourneyRequestObserved;
            InputSystem.onBeforeUpdate -= JourneyBeforeInputUpdate;
            journey = false;
            foreach (var device in journeyDevices) if (device.added) InputSystem.EnableDevice(device);
            journeyDevices.Clear();
        }

        void JourneyRequestObserved(string value)
        {
            if (journeyEvents.Count < 2048) journeyEvents.Add($"REQUEST {Time.time:F3} {value}");
        }

        void JourneyBeforeInputUpdate()
        {
            if (journey && UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType == UnityEngine.InputSystem.LowLevel.InputUpdateType.Dynamic)
                QueueProbeInput();
        }

        bool JourneyKeys(List<Key> keys)
        {
            if (!journey) return false;
            var self = NetworkPlayer.Local;
            var course = TightropeCourse.Current;
            var run = TightropeRun.Current;
            if (self == null || course == null || run == null) return true;
            if (run.State != TightropeRunState.Running) { JourneyEvent("run " + run.State); return true; }
            if (NetworkPlayer.All.Any(p => p.State == PlayerState.Fallen))
            { JourneyEvent("FAIL: participant fell"); return true; }
            var mover = self.GetComponent<PlayerMover>();
            var balance = self.GetComponent<PlayerBalance>();
            float distance = course.GetDistance(self.transform.position);
            int ownLane = course.Layout.GetLaneSlotAt(course.GetSide(self.transform.position));
            var trap = TightropeTrapeze.Current;
            var relay = TightropeRelayGate.Current;
            var saws = TightropeSaws.Current;
            var stack = PiggybackSystem.Current;
            int survivor = TightropeFire.Current.SurvivingLaneAt(course.FinishDistance - 1f);
            var input = new PlayerInputFrame();
            string detail = "";
            switch (journeyPhase)
            {
                case 0:
                    input = JourneyTravel(34f, (int)self.OwnerClientId + 1);
                    if (distance >= 33.8f) journeyPhase = 1;
                    break;
                case 1:
                    if (distance < relay.EntryDistance - 5f) input = JourneyTravel(34f, ownLane);
                    else if (distance >= relay.GapTo + .4f && relay.ReturnOpen && !relay.IsHeld(self)) input = JourneyTravel(55f, ownLane);
                    else { input = journeyBrain.BuildInput(); detail = journeyBrain.Status; }
                    if (distance >= 54.8f) journeyPhase = 2;
                    break;
                case 2:
                    input = JourneyTravel(94f + ((self.OwnerClientId & 1UL) == 0 ? 1.5f : 0f), (int)self.OwnerClientId + 1);
                    if (EveryoneAfter(93.8f)) journeyPhase = 3;
                    break;
                case 3:
                    bool firstPairDone = NetworkPlayer.All.Where(p => p.OwnerClientId < 2).All(p => course.GetDistance(p.transform.position) > course.TrapezeGapTo + 1f);
                    if (self.OwnerClientId < 2 || firstPairDone)
                    {
                        if (distance < trap.EntryDistance - 6f) input = JourneyTravel(94f, trap.LaneIndex);
                        else { input = journeyBrain.BuildInput(); detail = journeyBrain.Status; }
                    }
                    if (distance > course.TrapezeGapTo + 1f && !trap.IsCrossing(self) && !mover.IsAirborne) journeyPhase = 4;
                    break;
                case 4:
                    input = JourneyTravel(126f - .8f * self.OwnerClientId, survivor);
                    if (EveryoneAfter(122f)) journeyPhase = 5;
                    break;
                case 5:
                    float launch = saws.IsCooperativeGateActive ? saws.CooperativeJumpLaunchDistance : 155.8f;
                    var bottom = NetworkPlayer.All.First(p => p.OwnerClientId == 0);
                    if (saws.AreRequiredGatesCleared) { journeyPhase = 6; break; }
                    if (saws.IsCooperativeStackApproved && saws.IsCooperativeGateActive)
                    { input = journeyBrain.BuildInput(); detail = journeyBrain.Status; }
                    else if (stack.GetBelow(self) != null) { detail = "waiting for four-stack approval"; }
                    else if (self.OwnerClientId == 0) input = JourneyTravel(launch, survivor);
                    else
                    {
                        int size = stack.GetStackSize(bottom);
                        bool turn = size == (int)self.OwnerClientId;
                        input = JourneyTravel(launch - (turn ? .7f : 1.8f + .8f * (int)self.OwnerClientId), survivor);
                        if (turn && Mathf.Abs(course.GetDistance(bottom.transform.position) - distance) < 1.1f
                            && Mathf.Abs(course.GetDistance(bottom.transform.position) - launch) < .2f && Time.time > journeyTapAt)
                        { input.Move = .01f; input.InteractTapped = true; journeyTapAt = Time.time + .8f; }
                    }
                    break;
                case 6:
                    input = JourneyTravel(course.FinishDistance + .6f, survivor);
                    break;
            }
            if (!mover.FreeMovement && balance.IsActive && !trap.IsCrossing(self) && !relay.IsHeld(self))
                input.Posture = TightropePracticeBotBrain.Correction(balance.Value);
            if (input.Move > .001f) keys.Add(Key.W);
            else if (input.Move < -.001f) keys.Add(Key.S);
            if (input.Posture > .15f) keys.Add(Key.D);
            else if (input.Posture < -.15f) keys.Add(Key.A);
            if (input.AuxDirection != 0) keys.Add(input.AuxDirection > 0 ? Key.E : Key.Q);
            if (input.ActionPressed) keys.Add(Key.Space);
            if (input.InteractLongPressed) journeyLongUntil = Mathf.Max(journeyLongUntil, Time.time + .6f);
            if (input.InteractTapped || Time.time < journeyLongUntil) keys.Add(Key.F);
            if (journeyPhase == 3 && (input.InteractTapped || input.ActionPressed) && journeyEvents.Count < 2048)
                journeyEvents.Add($"QUEUE {Time.time:F3} F={input.InteractTapped} Space={input.ActionPressed} d={distance:F3}");
            JourneyEvent($"phase {journeyPhase}: {detail}");
            if (journeyPhase == 3 && distance > 96f && Time.time >= journeyTraceAt && journeyEvents.Count < 2048)
            {
                journeyTraceAt = Time.time + .1f;
                journeyEvents.Add($"TRACE {Time.time:F2} d={distance:F2} y={self.transform.position.y-course.transform.position.y:F2} floor={stack.GetFloor(self)} air={self.IsAirborne}/{mover.IsAirborne} held={trap.HeldPole(self)} grab={trap.CanGrabPole(0,self)} attach={trap.CanAttachToPole(0,self)} keys={string.Join(',',keys)}");
            }
            return true;
        }

        static bool EveryoneAfter(float distance) => NetworkPlayer.All.Count == 4
            && NetworkPlayer.All.All(p => p.State == PlayerState.Normal && TightropeCourse.Current.GetDistance(p.transform.position) >= distance);

        PlayerInputFrame JourneyTravel(float target, int lane)
        {
            var input = new PlayerInputFrame();
            var self = NetworkPlayer.Local; var course = TightropeCourse.Current;
            var mover = self.GetComponent<PlayerMover>();
            float distance = course.GetDistance(self.transform.position);
            if (mover.IsAirborne) return input;
            int currentLane = course.Layout.GetLaneSlotAt(course.GetSide(self.transform.position));
            if (lane < 0 || lane >= course.LaneCount) return input;
            if (mover.FreeMovement)
            {
                float side = course.Layout.GetLaneSide(lane) - course.GetSide(self.transform.position);
                if (Mathf.Abs(side) > .08f) input.Posture = Mathf.Sign(side);
                else if (Mathf.Abs(target - distance) > .08f) input.Move = Mathf.Sign(target - distance);
                return input;
            }
            if (currentLane != lane)
            {
                if (Time.time >= journeyJumpAt)
                { input.AuxDirection = System.Math.Sign(lane - currentLane); input.ActionPressed = true; journeyJumpAt = Time.time + .8f; }
                return input;
            }
            if (Mathf.Abs(target - distance) > .08f) input.Move = Mathf.Sign(target - distance);
            var saws = TightropeSaws.Current;
            if (input.Move > 0 && Time.time >= journeyJumpAt)
                for (int i = 0; i < saws.HorizontalCount; i++)
                {
                    float gap = saws.Settings.GetHorizontalDistance(i) - distance;
                    if (gap >= .7f && gap <= .95f && saws.IsHorizontalMoving(i))
                    { input.ActionPressed = true; journeyJumpAt = Time.time + .8f; break; }
                }
            return input;
        }

        void JourneyEvent(string text)
        {
            status = text;
            if (text == journeyLastEvent) return;
            journeyLastEvent = text;
            if (journeyEvents.Count < 2048) journeyEvents.Add($"{Time.time:F2} {text}");
        }
    }
}
#endif
