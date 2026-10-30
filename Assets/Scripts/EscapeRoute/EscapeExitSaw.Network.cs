using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeExitSaw
    {
        string RequestName => "CurtainCall.Escape.ExitSaw.Request." + deviceId;
        string StateName => "CurtainCall.Escape.ExitSaw.State." + deviceId;
        const int RequestSize = 9, StateSize = 66, ShotSize = 36;
        CustomMessagingManager messaging;
        void RequestAction(byte action)
        {
            var player = NetworkPlayer.Local;
            if (!Ready || !Alive(player) || InputGate.IsBlocked) return;
            if (session.IsHost) HandleAction(player.OwnerClientId, action, ownership.Revision);
            else SendRequest(action, 0);
        }
        void HandleAction(ulong actor, byte action, uint expected, bool interactionReady = false)
        {
            if (!Ready || session == null || !session.IsHost || expected != ownership.Revision) return;
            var player = FindPlayer(actor);
            if (!Alive(player)) return;
            if (action == 1)
            {
                if (!CanGrab(player)) return;
                if (!interactionReady && HumanCannonSystem.Current.DelayInjuredInteraction(player, -777, expected,
                    () => this != null && CanGrab(player) && ownership.Revision == expected,
                    () => HandleAction(actor, action, expected, true))) return;
                if (!CanGrab(player) || !ownership.TryApply(CannonAction.Board, actor, expected)) return;
                ClearShots();
                HumanCannonSystem.Current.StartCarrying(actor);
                lastSafeFeet = player.transform.position;
                FollowCarrier(player, true);
            }
            else if (action == 2)
            {
                if (!ownership.TryApply(CannonAction.Release, actor, expected)) return;
                ClearShots();
                if (!Grounded(player)) Recover();
                if (!DoorOpen) cutProgress = 0;
                cutting = carryBlocked = false;
            }
            else return;
            Changed();
        }
        void Changed() { stateRevision++; Publish(); }
        void Attach()
        {
            var next = NetworkSessionManager.Instance;
            var manager = next != null && next.NetworkManager != null && next.NetworkManager.IsListening
                && (next.ConnectionState == SessionConnectionState.Host || next.ConnectionState == SessionConnectionState.Client)
                ? next.NetworkManager.CustomMessagingManager : null;
            if (manager == messaging) return;
            Detach(); session = next; messaging = manager;
            if (manager == null) return;
            if (session.IsHost) { synchronized = true; manager.RegisterNamedMessageHandler(RequestName, ReadRequest); }
            else { manager.RegisterNamedMessageHandler(StateName, ReadState); SendRequest(0, 0); }
        }
        void SendRequest(byte action, int token)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
            writer.WriteValueSafe(action); writer.WriteValueSafe(ownership.Revision); writer.WriteValueSafe(token);
            messaging.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            if (session == null || !session.IsHost || reader.Length - reader.Position != RequestSize) return;
            reader.ReadValueSafe(out byte action); reader.ReadValueSafe(out uint expected); reader.ReadValueSafe(out int token);
            if (action == 0 && token >= 0) SendState(sender, token);
            else if ((action == 1 || action == 2) && token == 0) { HandleAction(sender, action, expected); SendState(sender, 0); }
        }
        void Publish()
        {
            Present(0);
            if (messaging == null || session == null || !session.IsHost) return;
            foreach (ulong client in session.NetworkManager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) SendState(client, 0);
        }
        void SendState(ulong client, int token)
        {
            using var writer = new FastBufferWriter(StateSize + BitCount(cutMask) * 12 + shots.Count * ShotSize, Allocator.Temp);
            writer.WriteValueSafe(stateRevision); writer.WriteValueSafe(ownership.Revision); writer.WriteValueSafe(Carrier);
            writer.WriteValueSafe(CarryRoot.position); writer.WriteValueSafe(CarryRoot.rotation); writer.WriteValueSafe(cutProgress);
            writer.WriteValueSafe((byte)(cutting ? 1 : 0)); writer.WriteValueSafe(token);
            writer.WriteValueSafe(cutMask); writer.WriteValueSafe(shotWait); writer.WriteValueSafe((byte)shots.Count);
            writer.WriteValueSafe(courseEpoch);
            for (int i = 0; i < 32; i++) if ((cutMask & (1u << i)) != 0) writer.WriteValueSafe(cutPoints[i]);
            foreach (var shot in shots)
            {
                writer.WriteValueSafe(shot.Id); writer.WriteValueSafe(shot.Epoch); writer.WriteValueSafe(shot.Position);
                writer.WriteValueSafe(shot.Direction); writer.WriteValueSafe(shot.Age);
            }
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableSequenced);
        }
        void ReadState(ulong sender, FastBufferReader reader)
        {
            int bytes = reader.Length - reader.Position;
            if (session == null || session.IsHost || sender != NetworkManager.ServerClientId || bytes < StateSize
                || bytes > StateSize + 32 * 12 + MaxProjectiles * ShotSize) return;
            reader.ReadValueSafe(out uint incoming); reader.ReadValueSafe(out uint ownedRevision); reader.ReadValueSafe(out ulong carrier);
            reader.ReadValueSafe(out Vector3 position); reader.ReadValueSafe(out Quaternion rotation); reader.ReadValueSafe(out float progress);
            reader.ReadValueSafe(out byte active); reader.ReadValueSafe(out int token);
            reader.ReadValueSafe(out uint mask); reader.ReadValueSafe(out float wait); reader.ReadValueSafe(out byte count);
            reader.ReadValueSafe(out uint epoch);
            if (count > MaxProjectiles || bytes != StateSize + BitCount(mask) * 12 + count * ShotSize) return;
            var points = new Vector3[32];
            for (int i = 0; i < 32; i++) if ((mask & (1u << i)) != 0) reader.ReadValueSafe(out points[i]);
            var incomingShots = new Shot[count];
            for (int i = 0; i < count; i++)
            {
                var shot = new Shot(); reader.ReadValueSafe(out shot.Id); reader.ReadValueSafe(out shot.Epoch);
                reader.ReadValueSafe(out shot.Position); reader.ReadValueSafe(out shot.Direction); reader.ReadValueSafe(out shot.Age);
                incomingShots[i] = shot;
            }
            ApplySnapshot(incoming, ownedRevision, carrier, position, rotation, progress, active, token, mask, points, incomingShots, wait, epoch);
        }
        bool ApplyState(uint incoming, uint ownedRevision, ulong carrier, Vector3 position, Quaternion rotation, float progress, byte active, int token)
            => ApplySnapshot(incoming, ownedRevision, carrier, position, rotation, progress, active, token, 0, new Vector3[32], System.Array.Empty<Shot>(), Mathf.Max(.1f, shotInterval), courseEpoch);
        bool ApplySnapshot(uint incoming, uint ownedRevision, ulong carrier, Vector3 position, Quaternion rotation, float progress,
            byte active, int token, uint mask, Vector3[] points, Shot[] incomingShots, float wait, uint epoch)
        {
            bool wasWaiting = resetBarrier.Waiting;
            if (incoming < stateRevision || ownedRevision < ownership.Revision || epoch < courseEpoch || !Finite(position) || !Finite(rotation)
                || !float.IsFinite(progress) || progress < 0 || progress > cutSeconds || active > 1
                || active == 1 && (carrier == CannonSeatState.Nobody || progress >= cutSeconds)
                || synchronized && ownedRevision == ownership.Revision && carrier != Carrier
                || synchronized && incoming == stateRevision && (ownedRevision != ownership.Revision || progress != cutProgress || (active == 1) != cutting)
                || !ValidProjectileSnapshot(ownedRevision, carrier, mask, points, incomingShots, wait)
                || synchronized && incoming == stateRevision && (mask != cutMask || epoch != courseEpoch || !SameShots(incomingShots))
                || synchronized && epoch == courseEpoch && (mask & cutMask) != cutMask
                || synchronized && epoch == courseEpoch && !SameCutPoints(mask, points)
                || !resetBarrier.Accept(token)) return false;
            ulong before = Carrier;
            bool wasReady = synchronized;
            uint previousOwnership = ownership.Revision;
            if (ownedRevision != previousOwnership || carrier != before || wasWaiting || epoch != courseEpoch) ClearShots();
            ownership.Receive(ownedRevision, carrier); stateRevision = incoming; cutProgress = progress; cutting = active == 1; synchronized = true;
            bool sameCourse = epoch == courseEpoch;
            courseEpoch = epoch; cutMask = mask; System.Array.Copy(points, cutPoints, 32); shotWait = wait;
            if (!sameCourse) lastPresentedShot = 0;
            PresentCables(wasReady && !wasWaiting && sameCourse);
            bool cue = false;
            shots.Clear();
            foreach (var shot in incomingShots)
            {
                if (wasReady && !wasWaiting && sameCourse && before == carrier && previousOwnership == ownedRevision
                    && shot.Id > lastPresentedShot && shot.Age <= .4f) cue = true;
                if (shot.Id > lastPresentedShot) lastPresentedShot = shot.Id;
                shots.Add(shot);
            }
            if (cue) EmitLaunch();
            var player = FindPlayer(carrier);
            if (!wasReady || carrier != before || player == null || !player.IsLocal) CarryRoot.SetPositionAndRotation(position, rotation);
            if (carrier != CannonSeatState.Nobody && (before != carrier || !wasReady))
            {
                HumanCannonSystem.Current?.StartCarrying(carrier);
                if (Alive(player)) { lastSafeFeet = player.transform.position; FollowCarrier(player, false); }
            }
            Present(0, !wasReady);
            return true;
        }
        void Restart(NetworkPlayer player)
        {
            if (player == null || session == null) return;
            if (session.IsHost)
            {
                if (resetFrame == Time.frameCount) return;
                resetFrame = Time.frameCount; ownership.Reset(); courseEpoch++; cutProgress = 0; cutting = false; ResetProjectileCourse(); Recover(); Present(0, true); Changed();
            }
            else if (player.IsLocal)
            {
                synchronized = false; ownership.Receive(ownership.Revision, CannonSeatState.Nobody); cutProgress = 0; cutting = false;
                ResetProjectileCourse(); Recover(); Present(0, true); SendRequest(0, resetBarrier.Begin());
            }
        }
        void Detach()
        {
            if (messaging != null)
            { messaging.UnregisterNamedMessageHandler(RequestName); messaging.UnregisterNamedMessageHandler(StateName); }
            messaging = null; session = null; synchronized = false;
            ownership = new CannonSeatState(); stateRevision = courseEpoch = 0; cutProgress = 0; cutting = false;
            resetBarrier.Clear(); ResetProjectileCourse(); Recover(); Present(0, true);
        }
    }
}
