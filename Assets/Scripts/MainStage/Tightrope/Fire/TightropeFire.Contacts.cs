using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    public sealed partial class TightropeFire
    {
        const string ContactMessage = "CurtainCall.Tightrope.Fire.Contact.v2";
        const int ContactBytes = 37;
        const double MaxContactAge = 1.25;
        readonly Dictionary<ulong, uint> contactSequences = new();
        readonly float[] segmentActivatedAt = new float[64];
        uint nextContactSequence;
        float nextContactReport;

        void ClearContactReports()
        {
            contactSequences.Clear(); nextContactReport = 0;
            Array.Fill(segmentActivatedAt, float.PositiveInfinity);
        }

        void LateUpdate()
        {
            if (session == null || session.ConnectionState != SessionConnectionState.Client || messaging == null
                || !IsPerformanceActive || Time.unscaledTime < nextContactReport) return;
            var player = NetworkPlayer.Local;
            if (player == null || !player.IsSpawned || !player.IsOwner || player.State != PlayerState.Normal
                || !player.TryGetComponent(out PlayerMover mover)) return;
            double at = Elapsed;
            var feet = player.transform.position;
            if (!TryContact(feet, mover.BodyRadius, (float)at, false, out var kind, out int index)) return;
            // Retry while still touching; a rejected/stale packet must not permanently disable a hazard.
            nextContactReport = Time.unscaledTime + .15f;
            using var writer = new FastBufferWriter(ContactBytes, Allocator.Temp);
            writer.WriteValueSafe(++nextContactSequence); writer.WriteValueSafe(round);
            writer.WriteValueSafe(player.RestartVersion); writer.WriteValueSafe(at); writer.WriteValueSafe(feet);
            writer.WriteValueSafe((byte)kind); writer.WriteValueSafe(index);
            messaging.SendNamedMessage(ContactMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void HandleContact(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || !session.NetworkManager.ConnectedClients.ContainsKey(sender)
                || reader.Length - reader.Position != ContactBytes) return;
            reader.ReadValueSafe(out uint serial); reader.ReadValueSafe(out int contactRound);
            reader.ReadValueSafe(out int restart); reader.ReadValueSafe(out double at); reader.ReadValueSafe(out Vector3 feet);
            reader.ReadValueSafe(out byte kind); reader.ReadValueSafe(out int index);
            ValidateContact(sender, serial, contactRound, restart, at, feet, (FireKind)kind, index);
        }

        bool ValidateContact(ulong sender, uint serial, int contactRound, int restart, double at, Vector3 feet, FireKind kind, int index)
        {
            if (session == null || !session.IsHost || !IsPerformanceActive || contactRound != round
                || !double.IsFinite(at) || at < 0 || at > Elapsed + .05 || Elapsed - at > MaxContactAge
                || !float.IsFinite(feet.x) || !float.IsFinite(feet.y) || !float.IsFinite(feet.z)
                || (contactSequences.TryGetValue(sender, out uint previous) && unchecked((int)(serial - previous)) <= 0)) return false;
            NetworkPlayer player = null;
            foreach (var candidate in NetworkPlayer.All)
                if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == sender) { player = candidate; break; }
            if (player == null || player.State != PlayerState.Normal || player.RestartVersion != restart || !run.IsParticipantReady(player)
                || !player.TryGetComponent(out PlayerMover mover)) return false;
            float envelope = 1f + 30f * (float)Math.Max(0, Elapsed - at + .1);
            if ((player.transform.position - feet).sqrMagnitude > envelope * envelope
                || !TryContact(feet, mover.BodyRadius, (float)at, true, out var actualKind, out int actualIndex)
                || actualKind != kind || actualIndex != index) return false;
            contactSequences[sender] = serial;
            Kill(player, kind, index, course.GetDistance(feet));
            return true;
        }

        bool TryContact(Vector3 feet, float radius, float at, bool historical, out FireKind kind, out int index)
        {
            kind = FireKind.Chase; index = -1;
            if (snapshot == null || course == null) return false;
            float distance = course.GetDistance(feet);
            if (distance >= snapshot.safeDistance) return false;
            if (snapshot.chaseEnabled && at >= snapshot.chaseDelay && distance <= FireMath.ChaseDistance(snapshot, at)) return true;
            float side = course.GetSide(feet);
            for (int i = 0; i < snapshot.segments.Length; i++) {
                var segment = snapshot.segments[i]; int lane = segment.lane - 1;
                if (!IsSegmentActive(i) || lane < 0 || lane >= course.LaneCount || (historical && at < segmentActivatedAt[i])) continue;
                GetSegmentSpans(i, segmentSpans);
                foreach (var span in segmentSpans) {
                    segment.from = span.x; segment.to = span.y;
                    if (!FireMath.SegmentContains(segment, distance, side, course.Layout.GetLaneSide(lane), course.Shape.RopeWalkWidth * .5f + radius)) continue;
                    kind = FireKind.Segment; index = i; return true;
                }
            }
            return false;
        }
    }
}
