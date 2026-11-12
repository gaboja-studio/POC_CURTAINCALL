using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    public sealed partial class TightropeSaws
    {
        const string OwnerContactMessage = "CurtainCall.Tightrope.Saws.OwnerContact.v2";
        const int OwnerContactBytes = 42;
        const double ContactAge = 1.25, ShapeSampleInterval = 1d / 30;
        readonly ContactFrame[] contactHistory = new ContactFrame[48];
        readonly List<ContactShape> shownContactShapes = new();
        readonly HashSet<int> localTouching = new(), localTouchingNow = new();
        readonly List<int> departedContacts = new();
        readonly HashSet<(ulong owner, int saw)> reportedTouching = new();
        readonly Dictionary<ulong, uint> ownerContactSequences = new();
        int nextContactFrame;
        uint nextOwnerContactSequence;
        double nextShapeSample;
        float nextTouchRetry;

        readonly struct ContactShape
        {
            public readonly SawKind Kind;
            public readonly int Index;
            public readonly SawDisc Disc;
            public int Key => Kind == SawKind.Horizontal ? Index : Kind == SawKind.Vertical ? -Index : -100000 - Index;
            public ContactShape(SawKind kind, int index, SawDisc disc) { Kind = kind; Index = index; Disc = disc; }
        }
        sealed class ContactFrame
        {
            public double At;
            public readonly List<ContactShape> Shapes = new();
        }

        bool CanJudgeContact => isActiveAndEnabled && run != null && run.IsPerformanceStarted
            && run.State == TightropeRunState.Running && course != null && course.IsGameplayActive;

        void ClearOwnerContacts()
        {
            foreach (var frame in contactHistory) frame?.Shapes.Clear();
            nextContactFrame = 0; nextShapeSample = 0; nextTouchRetry = 0;
            localTouching.Clear(); localTouchingNow.Clear(); reportedTouching.Clear(); ownerContactSequences.Clear();
        }

        void CollectContactShapes(List<ContactShape> result)
        {
            result.Clear();
            for (int i = 0; i < tracks.Length; i++)
                if (IsHorizontalEnabled(i) && !IsHorizontalSafeForTeamPassage(i))
                    result.Add(new ContactShape(SawKind.Horizontal, i, new SawDisc(GetHorizontalPosition(i), Vector3.up,
                        settings.horizontalColliderDiameter * .5f, settings.horizontalThickness * .5f)));
            if (!IsVerticalFalling && TryGetVerticalPosition(out var vertical))
                result.Add(new ContactShape(SawKind.Vertical, verticalSerial, new SawDisc(vertical, course.Right,
                    settings.verticalColliderDiameter * .5f, settings.verticalThickness * .5f)));
            if (IsCooperativeGateActive && !IsCooperativeGatePassable) AddGate(cooperativeGateIndex, CooperativeGateBase, result);
            foreach (var gate in releasedGates)
                if (TryReleasedPosition(gate, out var bottom)) AddGate(gate.Index, bottom, result);
        }

        void AddGate(int index, Vector3 bottom, List<ContactShape> result) => result.Add(new ContactShape(
            SawKind.CooperativeGate, index, new SawDisc(bottom + Vector3.up * settings.cooperativeGateHeight * .5f,
                course.Right, settings.cooperativeGateHeight * .5f, settings.cooperativeGateThickness * .5f)));

        void RememberContactShapes()
        {
            if (!CanJudgeContact || SawClock < nextShapeSample) return;
            var frame = contactHistory[nextContactFrame] ??= new ContactFrame();
            frame.At = SawClock; CollectContactShapes(frame.Shapes);
            nextContactFrame = (nextContactFrame + 1) % contactHistory.Length;
            nextShapeSample = SawClock + ShapeSampleInterval;
        }

        void LateUpdate()
        {
            if (session == null || session.ConnectionState != SessionConnectionState.Client || messaging == null || !CanJudgeContact) return;
            var player = NetworkPlayer.Local;
            if (player == null || !player.IsOwner || !player.IsSpawned || player.State != PlayerState.Normal
                || !player.TryGetComponent(out PlayerMover mover)) return;
            var body = new BodyCapsule(player.transform.position, mover.BodyHeight, mover.BodyRadius);
            CollectContactShapes(shownContactShapes); localTouchingNow.Clear();
            bool retry = Time.unscaledTime >= nextTouchRetry;
            foreach (var shape in shownContactShapes) {
                if (!SawMath.TryGetContact(shape.Disc, body, out _)) continue;
                localTouchingNow.Add(shape.Key);
                if (retry || !localTouching.Contains(shape.Key)) SendOwnerContact(player, body, shape.Kind, shape.Index, true);
            }
            departedContacts.Clear();
            foreach (int key in localTouching) if (!localTouchingNow.Contains(key)) departedContacts.Add(key);
            foreach (int key in departedContacts) {
                var kind = key >= 0 ? SawKind.Horizontal : key > -100000 ? SawKind.Vertical : SawKind.CooperativeGate;
                int index = kind == SawKind.Horizontal ? key : kind == SawKind.Vertical ? -key : -100000 - key;
                SendOwnerContact(player, body, kind, index, false);
            }
            localTouching.Clear(); localTouching.UnionWith(localTouchingNow);
            if (retry) nextTouchRetry = Time.unscaledTime + .15f;
        }

        void SendOwnerContact(NetworkPlayer player, in BodyCapsule body, SawKind kind, int index, bool touching)
        {
            using var writer = new FastBufferWriter(OwnerContactBytes, Allocator.Temp);
            writer.WriteValueSafe(++nextOwnerContactSequence); writer.WriteValueSafe(run.Round);
            writer.WriteValueSafe(player.RestartVersion); writer.WriteValueSafe(SawClock);
            writer.WriteValueSafe(body.Feet); writer.WriteValueSafe(body.Height);
            writer.WriteValueSafe((byte)kind); writer.WriteValueSafe(index); writer.WriteValueSafe(touching);
            messaging.SendNamedMessage(OwnerContactMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleOwnerContact(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || !session.NetworkManager.ConnectedClients.ContainsKey(sender)
                || reader.Length - reader.Position != OwnerContactBytes) return;
            reader.ReadValueSafe(out uint serial); reader.ReadValueSafe(out int round); reader.ReadValueSafe(out int restart);
            reader.ReadValueSafe(out double at); reader.ReadValueSafe(out Vector3 feet); reader.ReadValueSafe(out float height);
            reader.ReadValueSafe(out byte kind); reader.ReadValueSafe(out int index); reader.ReadValueSafe(out bool contact);
            ValidateOwnerContact(sender, serial, round, restart, at, feet, height, (SawKind)kind, index, contact);
        }

        bool ValidateOwnerContact(ulong sender, uint serial, int round, int restart, double at, Vector3 feet,
            float height, SawKind kind, int index, bool contact)
        {
            if (session == null || !session.IsHost || !CanJudgeContact || round != run.Round || index < 0
                || kind < SawKind.Horizontal || kind > SawKind.CooperativeGate || !double.IsFinite(at)
                || (kind == SawKind.Horizontal && index >= settings.HorizontalCount)
                || (kind == SawKind.Vertical && (index == 0 || index >= 100000))
                || (kind == SawKind.CooperativeGate && index >= (settings.cooperativeGateDistances?.Length ?? 0))
                || at > SawClock + .05 || SawClock - at > ContactAge || !float.IsFinite(feet.x)
                || !float.IsFinite(feet.y) || !float.IsFinite(feet.z) || !float.IsFinite(height) || height < .1f
                || ownerContactSequences.TryGetValue(sender, out uint previous) && unchecked((int)(serial - previous)) <= 0) return false;
            NetworkPlayer player = null;
            foreach (var candidate in NetworkPlayer.All)
                if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == sender) { player = candidate; break; }
            if (player == null || player.State != PlayerState.Normal || player.RestartVersion != restart
                || !run.IsParticipantReady(player) || !player.TryGetComponent(out PlayerMover mover)
                || height > Mathf.Max(mover.BodyHeight, GameSettings.Base.Character.CapsuleHeight) + .05f) return false;
            int key = kind == SawKind.Horizontal ? index : kind == SawKind.Vertical ? -index : -100000 - index;
            if (!contact) {
                ownerContactSequences[sender] = serial; reportedTouching.Remove((sender, key)); return true;
            }
            float envelope = 1f + 30f * (float)Math.Max(0, SawClock - at + .1);
            if ((feet - player.transform.position).sqrMagnitude > envelope * envelope) return false;
            ContactFrame closest = null; double difference = .075;
            foreach (var frame in contactHistory) {
                if (frame == null || frame.At > at) continue;
                double delta = Math.Abs(frame.At - at);
                if (delta < difference) { closest = frame; difference = delta; }
            }
            if (closest == null) return false;
            var body = new BodyCapsule(feet, height, mover.BodyRadius);
            foreach (var shape in closest.Shapes) {
                if (shape.Kind != kind || shape.Index != index || !SawMath.TryGetContact(shape.Disc, body, out var point)) continue;
                ownerContactSequences[sender] = serial;
                if (reportedTouching.Add((sender, key))) ServerTouch(player, body, kind, key, index, point);
                return true;
            }
            return false;
        }
    }
}
