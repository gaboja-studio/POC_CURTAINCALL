using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonHazardCourse
    {
        const string ContactName = "CurtainCall.HumanCannon.Hazards.Contact.v3";
        const int ContactBytes = 53;
        readonly HashSet<int> ownerTouching = new(), ownerTouchingNow = new();
        readonly HashSet<(int Hazard, ulong Actor, ulong Avatar, int Revive)> remoteTouching = new();
        readonly Dictionary<ulong, uint> contactSequences = new();

        uint contactSequence;
        float nextOwnerContactRetry;
        NetworkPlayer contactAvatar;
        int contactRevive = -1;
        void ClearOwnerContacts()
        {
            ClearLocalHazardContacts();
            remoteTouching.Clear(); contactSequences.Clear();
        }

        void ClearLocalHazardContacts()
        {
            if (contactAvatar != null) previousFeet.Remove(contactAvatar.OwnerClientId);
            contactAvatar = null; contactRevive = -1; nextOwnerContactRetry = 0;
            ownerTouching.Clear(); ownerTouchingNow.Clear();
        }
        void ForgetOwnerContacts(ulong actor)
        { remoteTouching.RemoveWhere(k => k.Actor == actor); contactSequences.Remove(actor); if (NetworkPlayer.Local?.OwnerClientId == actor) { ownerTouching.Clear(); ownerTouchingNow.Clear(); } }

        void OwnerContacts(double elapsed, float speed)
        {
            var player = NetworkPlayer.Local;
            if (messaging == null || player == null || !player.IsSpawned || !player.IsOwner || player.State != PlayerState.Normal
                || !player.TryGetComponent(out PlayerMover mover))
            {
                ClearLocalHazardContacts();
                return;
            }
            if (contactAvatar != player || contactRevive != player.ReviveVersion)
            {
                ClearLocalHazardContacts();
                contactAvatar = player;
                contactRevive = player.ReviveVersion;
            }

            float now = Time.unscaledTime;
            bool retry = now >= nextOwnerContactRetry;
            if (retry) nextOwnerContactRetry = now + .15f;
            var feet = player.transform.position;
            double start = previousElapsed >= 0 && elapsed > previousElapsed ? Math.Max(previousElapsed, elapsed - .5) : elapsed;
            var before = previousFeet.TryGetValue(player.OwnerClientId, out var old) && (old - feet).sqrMagnitude < 16f ? old : feet;
            if (previousElapsed >= 0 && elapsed > previousElapsed) before = Vector3.Lerp(before, feet, (float)((start - previousElapsed) / (elapsed - previousElapsed)));
            ownerTouchingNow.Clear();
            for (int h = 0; h < hazards.Length; h++) {
                var hazard = hazards[h];
                if (hazard == null || !hazard.isActiveAndEnabled || !IsHazardEnabled(h)) continue;
                float scale = hazard.IsPoleSaw ? 1f : speed;
                int steps = hazard.SweepSteps(before, feet, elapsed - start, scale);
                for (int i = 0; i <= steps; i++) {
                    float t = i / (float)steps; var point = Vector3.Lerp(before, feet, t); double at = start + (elapsed - start) * t;
                    if (!hazard.Touches(point, mover.BodyHeight, mover.BodyRadius, at, scale)) continue;
                    ownerTouchingNow.Add(h);
                    if (retry || !ownerTouching.Contains(h)) SendOwnerContact(player, h, true, at, point);
                    break;
                }
            }
            foreach (int h in ownerTouching) if (!ownerTouchingNow.Contains(h)) SendOwnerContact(player, h, false, elapsed, feet);
            ownerTouching.Clear(); ownerTouching.UnionWith(ownerTouchingNow); previousFeet[player.OwnerClientId] = feet;
        }
        void SendOwnerContact(NetworkPlayer player, int index, bool begin, double at, Vector3 feet)
        {
            using var writer = new FastBufferWriter(ContactBytes, Allocator.Temp);
            writer.WriteValueSafe((byte)(begin ? 1 : 0)); writer.WriteValueSafe(++contactSequence); writer.WriteValueSafe(index);
            writer.WriteValueSafe(player.RestartVersion); writer.WriteValueSafe(player.NetworkObjectId);
            writer.WriteValueSafe(player.ReviveVersion); writer.WriteValueSafe(epoch); writer.WriteValueSafe(at); writer.WriteValueSafe(feet);
            messaging.SendNamedMessage(ContactName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReceiveOwnerContact(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || !session.NetworkManager.ConnectedClients.ContainsKey(sender)
                || reader.Length - reader.Position != ContactBytes) return;
            reader.ReadValueSafe(out byte begin); reader.ReadValueSafe(out uint sequence); reader.ReadValueSafe(out int index);
            reader.ReadValueSafe(out int restart); reader.ReadValueSafe(out ulong avatar); reader.ReadValueSafe(out int revive);
            reader.ReadValueSafe(out double generation); reader.ReadValueSafe(out double at); reader.ReadValueSafe(out Vector3 feet);
            if (begin > 1) return;
            ValidateOwnerContact(sender, index, begin == 1, sequence, restart, avatar, revive, generation, at, feet);
        }
        bool ValidateOwnerContact(ulong sender, int index, bool begin, uint sequence, int restart, ulong avatar, int revive, double generation, double at, Vector3 feet)
        {
            if (session == null || !session.IsHost || !IsSynchronized || Resolved || generation != epoch || index < 0 || index >= hazards.Length) return false;
            NetworkPlayer player = null;
            foreach (var candidate in NetworkPlayer.All)
                if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == sender
                    && MatchesContactAvatar(candidate, restart, avatar, revive)) { player = candidate; break; }
            if (player == null || player.State != PlayerState.Normal
                || (contactSequences.TryGetValue(sender, out uint previous) && unchecked((int)(sequence - previous)) <= 0)) return false;
            remoteTouching.RemoveWhere(contact => contact.Actor == sender && (contact.Avatar != avatar || contact.Revive != revive));
            var key = (index, sender, avatar, revive);
            if (!begin) { contactSequences[sender] = sequence; remoteTouching.Remove(key); return true; }
            var hazard = hazards[index];
            if (hazard == null || !hazard.isActiveAndEnabled || !IsHazardEnabled(index) || !double.IsFinite(at)
                || at < 0 || at > Elapsed + .05 || Elapsed - at > 1.25 || !float.IsFinite(feet.x) || !float.IsFinite(feet.y) || !float.IsFinite(feet.z)
                || !player.TryGetComponent(out PlayerMover mover)) return false;
            float tolerance = 1f + 30f * (float)Math.Max(0, Elapsed - at + .1);
            if ((player.LatestOwnerPosition - feet).sqrMagnitude > tolerance * tolerance
                || !hazard.Touches(feet, mover.BodyHeight, mover.BodyRadius, at, hazard.IsPoleSaw ? 1f : CannonDifficulty.HazardSpeed(difficulty))) return false;
            if (NetworkPlayer.ServerCanChangeState != null && !NetworkPlayer.ServerCanChangeState(player, PlayerState.Fallen)) return false;
            contactSequences[sender] = sequence;
            if (!remoteTouching.Add(key)) return true;
            BodyPart cut = BodyPart.None;
            NotifyContact(player, hazard, feet);
            string obstacle=CurtainCall.Telemetry.Pipeline.DetailLogIds.ObjectId(hazard,"OB_");
            if (hazard.KillsOnContact) player.ServerKill(CurtainCall.Network.PlayerSync.NetworkPlayer.DeathOrigin.Hazard,obstacle); else cut = player.ServerHazardCut(obstacle,CurtainCall.Network.PlayerSync.NetworkPlayer.DeathOrigin.Hazard);
            if (cut != BodyPart.None) NotifyHit(player, hazard, cut, feet);
            NotifyResolved(player, hazard, cut, feet, true);
            return true;
        }
        static bool MatchesContactAvatar(NetworkPlayer player, int restart, ulong avatar, int revive)
            => player.RestartVersion == restart && player.NetworkObjectId == avatar && player.ReviveVersion == revive;

        void NotifyContact(NetworkPlayer player, HumanCannonHazard hazard, Vector3 feet)
        { if (ContactDetected != null) foreach (Action<NetworkPlayer, HumanCannonHazard, Vector3> observer in ContactDetected.GetInvocationList()) try { observer(player, hazard, feet); } catch (Exception error) { Debug.LogException(error, this); } }
        void NotifyHit(NetworkPlayer player, HumanCannonHazard hazard, BodyPart cut, Vector3 feet)
        { if (HitConfirmed != null) foreach (Action<NetworkPlayer, HumanCannonHazard, BodyPart, Vector3> observer in HitConfirmed.GetInvocationList()) try { observer(player, hazard, cut, feet); } catch (Exception error) { Debug.LogException(error, this); } }
        void NotifyResolved(NetworkPlayer player, HumanCannonHazard hazard, BodyPart cut, Vector3 feet, bool evaluated)
        { if (ContactResolved != null) foreach (Action<NetworkPlayer, HumanCannonHazard, BodyPart, Vector3, bool> observer in ContactResolved.GetInvocationList()) try { observer(player, hazard, cut, feet, evaluated); } catch (Exception error) { Debug.LogException(error, this); } }
    }
}
