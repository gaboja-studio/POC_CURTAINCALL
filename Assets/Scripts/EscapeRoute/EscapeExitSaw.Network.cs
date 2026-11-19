using System.Collections.Generic;
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
        // WaitingRoom and escape_route coexist during transfer and both author slots 0 and 1.
        string RequestName => "CurtainCall.Escape.ExitSaw." + gameObject.scene.name + ".Request." + deviceId;
        string StateName => "CurtainCall.Escape.ExitSaw." + gameObject.scene.name + ".State." + deviceId;
        string attachedRequestName, attachedStateName;
        const int RequestSize = 9, StateSize = 66, ShotSize = 36;
        const int AimedRequestSize = RequestSize + 12;
        const double RemoteGrabGrace = .35;
        readonly List<(ulong Actor, uint Revision, double Expires)> pendingGrabs = new();
        Vector3 pendingBossAim;
        CustomMessagingManager messaging, suspendedHostMessaging;
        void RequestAction(byte action)
        {
            var player = NetworkPlayer.Local;
            if (!Ready || !Alive(player) || InputGate.IsBlocked) return;
            if (action == 3 && finaleBoss && finaleBoss.CombatStarted && Camera.main)
            {
                var ray = Camera.main.ViewportPointToRay(new Vector3(.5f, .5f, 0));
                var origin = muzzle ? muzzle.position : CarryRoot.position;
                Vector3 point = ray.GetPoint(60);
                float closest = 60;
                foreach (var hit in Physics.RaycastAll(ray, 60, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    if (hit.distance >= closest || hit.transform.IsChildOf(CarryRoot)
                        || hit.transform.GetComponentInParent<NetworkPlayer>()) continue;
                    if (hit.collider.isTrigger && !hit.transform.GetComponentInParent<EscapeCuttableCable>()
                        && !finaleBoss.IsPart(hit.collider)) continue;
                    closest = hit.distance; point = hit.point;
                }
                Vector3 direction = (point - origin).normalized;
                if (session.IsHost) HandleAimedShot(player.OwnerClientId, ownership.Revision, direction);
                else
                {
                    using var writer = new FastBufferWriter(AimedRequestSize, Allocator.Temp);
                    writer.WriteValueSafe((byte)4); writer.WriteValueSafe(ownership.Revision); writer.WriteValueSafe(0); writer.WriteValueSafe(direction);
                    messaging.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
                }
                return;
            }
            if (session.IsHost) HandleAction(player.OwnerClientId, action, ownership.Revision);
            else SendRequest(action, 0);
        }
        void HandleAimedShot(ulong actor, uint expected, Vector3 direction)
        {
            if (!Ready || session == null || !session.IsHost || !finaleBoss || !finaleBoss.CombatStarted
                || expected != ownership.Revision || Carrier != actor || !Finite(direction)
                || Mathf.Abs(direction.sqrMagnitude - 1) > .01f || Mathf.Abs(direction.y) > .95f) return;
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (Vector3.Dot(flat, Vector3.ProjectOnPlane(CarryRoot.forward, Vector3.up).normalized) < .25f) return;
            pendingBossAim = direction;
            try { HandleAction(actor, 3, expected); }
            finally { pendingBossAim = Vector3.zero; }
        }
        void HandleAction(ulong actor, byte action, uint expected, bool interactionReady = false)
        {
            if (!Ready || session == null || !session.IsHost || expected != ownership.Revision) return;
            var player = FindPlayer(actor);
            if (!Alive(player) || !CanUseCampaignLauncher(actor)) return;
            if (action == 1)
            {
                if (!TryGetGrabPose(player, out var position, out var rotation)) return;
                if (!interactionReady && HumanCannonSystem.Current.DelayInjuredInteraction(player, -777, expected,
                    () => this != null && CanGrab(player) && ownership.Revision == expected,
                    () => HandleAction(actor, action, expected, true))) return;
                if (!ownership.TryApply(CannonAction.Board, actor, expected)) return;
                if (!player.IsLocal) Debug.Log($"[ExitSaw] {RequestName} host granted guest {actor}, revision {ownership.Revision}", this);
                CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Mechanism(player,this,"CUTTER","ACQUIRED",CarryRoot.position,true);
                CarrierChanged();
                HumanCannonSystem.Current.StartCarrying(actor);
                lastSafeFeet = player.transform.position;
                CarryRoot.SetPositionAndRotation(position, rotation);
                followedCarrier = actor;
                CurtainCall.WaitingRoom.Pursuit.PursuitStimuli.Emit(CurtainCall.WaitingRoom.Pursuit.PursuitNoiseKind.SawGrab, Handle.position, CarryRoot);
            }
            else if (action == 2)
            {
                if (Carrier != actor) return;
                FollowCarrier(player, false);
                if (!CanLeaveAtCurrentPose(player) || !ownership.TryApply(CannonAction.Release, actor, expected)) return;
                CarrierChanged();
                if (!DoorOpen) cutProgress = 0;
                cutting = carryBlocked = false;
            }
            else if (action == 3)
            {
                if (Carrier != actor) return;
                // Requests arrive before LateUpdate. Shoot from the latest replicated
                // hand pose, not the previous frame's position across a pillar/rail.
                FollowCarrier(player, false);
                if (!TryFire(player)) return;
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
            bool resumingHost = manager != null && manager == suspendedHostMessaging && next.IsHost;
            if (!resumingHost) Detach();
            suspendedHostMessaging = null;
            session = next; messaging = manager;
            if (manager == null) return;
            attachedRequestName = RequestName; attachedStateName = StateName;
            if (session.IsHost)
            {
                synchronized = true;
                manager.RegisterNamedMessageHandler(attachedRequestName, ReadRequest);
                if (resumingHost)
                {
                    RestoreSharedCableState();
                    Publish();
                }
            }
            else { manager.RegisterNamedMessageHandler(attachedStateName, ReadState); SendRequest(0, 0); }
        }
        void SendRequest(byte action, int token)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(RequestSize, Allocator.Temp);
            WriteRequest(writer, action, token);
            if (action == 1) Debug.Log($"[ExitSaw] {RequestName} guest grab sent, revision {ownership.Revision}", this);
            messaging.SendNamedMessage(RequestName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
        void WriteRequest(FastBufferWriter writer, byte action, int token)
        {
            writer.WriteValueSafe(action); writer.WriteValueSafe(ownership.Revision); writer.WriteValueSafe(token);
        }
        void ReadRequest(ulong sender, FastBufferReader reader)
        {
            int bytes = reader.Length - reader.Position;
            if (session == null || !session.IsHost || (bytes != RequestSize && bytes != AimedRequestSize)) return;
            reader.ReadValueSafe(out byte action); reader.ReadValueSafe(out uint expected); reader.ReadValueSafe(out int token);
            if (bytes == AimedRequestSize)
            {
                reader.ReadValueSafe(out Vector3 direction);
                if (action == 4 && token == 0) { HandleAimedShot(sender, expected, direction); SendState(sender, 0); }
                return;
            }
            if (action == 0 && token >= 0) SendState(sender, token);
            else if ((action == 1 || action == 2 || action == 3) && token == 0)
            {
                if (action == 1 && Ready && !Suppressed && !ownership.IsOccupied && expected == ownership.Revision
                    && !CanGrab(FindPlayer(sender)))
                {
                    // The reliable input can reach the host before the interpolated owner pose catches up.
                    // Keep one request per guest and recheck the normal pickup rules on the host.
                    if (!pendingGrabs.Exists(request => request.Actor == sender && request.Revision == expected))
                        pendingGrabs.Add((sender, expected, Time.unscaledTimeAsDouble + RemoteGrabGrace));
                }
                else
                {
                    if (action == 1 && (!Ready || Suppressed || ownership.IsOccupied || expected != ownership.Revision))
                        Debug.Log($"[ExitSaw] {RequestName} guest {sender} grab rejected: ready={Ready}, hidden={Suppressed}, carrier={Carrier}, requested/current revision={expected}/{ownership.Revision}", this);
                    HandleAction(sender, action, expected);
                }
                SendState(sender, 0);
            }
        }
        void TickPendingGrabs(double now)
        {
            for (int i = 0; i < pendingGrabs.Count;)
            {
                var request = pendingGrabs[i];
                var player = FindPlayer(request.Actor);
                if (!Ready || Suppressed || ownership.IsOccupied || request.Revision != ownership.Revision
                    || request.Expires < now)
                {
                    if (request.Expires < now && !ownership.IsOccupied && request.Revision == ownership.Revision)
                        Debug.LogWarning($"[ExitSaw] {gameObject.scene.name}/{deviceId} guest {request.Actor} grab expired: {CheckGrab(player, out _, out _)}", this);
                    pendingGrabs.RemoveAt(i);
                    continue;
                }
                if (!CanGrab(player)) { i++; continue; }
                pendingGrabs.RemoveAt(i);
                HandleAction(request.Actor, 1, request.Revision);
            }
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
            if (messaging == null) return;
            using var writer = new FastBufferWriter(StateSize + BitCount(cutMask) * 12 + shots.Count * ShotSize, Allocator.Temp);
            WriteState(writer, token);
            // Click firing can leave more than one MTU of in-flight saws. Keep the complete snapshot reliable.
            messaging.SendNamedMessage(StateName, client, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
        void WriteState(FastBufferWriter writer, int token)
        {
            writer.WriteValueSafe(stateRevision); writer.WriteValueSafe(ownership.Revision); writer.WriteValueSafe(Carrier);
            writer.WriteValueSafe(CarryRoot.position); writer.WriteValueSafe(CarryRoot.rotation); writer.WriteValueSafe(cutProgress);
            // Two independent flags in the existing state byte. Join/resync receives the same latch as a live update.
            writer.WriteValueSafe((byte)((cutting ? 1 : 0) | (TeamReachedExitZone ? 0x80 : 0))); writer.WriteValueSafe(token);
            writer.WriteValueSafe(cutMask); writer.WriteValueSafe(shotWait); writer.WriteValueSafe((byte)shots.Count);
            writer.WriteValueSafe(courseEpoch);
            for (int i = 0; i < 32; i++) if ((cutMask & (1u << i)) != 0) writer.WriteValueSafe(cutPoints[i]);
            foreach (var shot in shots)
            {
                writer.WriteValueSafe(shot.Id); writer.WriteValueSafe(shot.Epoch); writer.WriteValueSafe(shot.Position);
                writer.WriteValueSafe(shot.Direction); writer.WriteValueSafe(shot.Age);
            }
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
            => ApplySnapshot(incoming, ownedRevision, carrier, position, rotation, progress, active, token, 0, new Vector3[32], System.Array.Empty<Shot>(), shotInterval, courseEpoch);
        bool ApplySnapshot(uint incoming, uint ownedRevision, ulong carrier, Vector3 position, Quaternion rotation, float progress,
            byte active, int token, uint mask, Vector3[] points, Shot[] incomingShots, float wait, uint epoch)
        {
            bool wasWaiting = resetBarrier.Waiting;
            bool visited = (active & 0x80) != 0;
            active &= 0x7f;
            if (incoming < stateRevision || ownedRevision < ownership.Revision || epoch < courseEpoch || !Finite(position) || !Finite(rotation)
                || synchronized && epoch == courseEpoch && TeamReachedExitZone && !visited
                || synchronized && incoming == stateRevision && TeamReachedExitZone != visited
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
            TeamReachedExitZone = visited;
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
                TeamReachedExitZone = false;
                resetFrame = Time.frameCount; ownership.Reset(); courseEpoch++; cutProgress = 0; cutting = false; ResetProjectileCourse(); Recover(); Present(0, true); Changed();
            }
            else if (player.IsLocal)
            {
                synchronized = false; ownership.Receive(ownership.Revision, CannonSeatState.Nobody); cutProgress = 0; cutting = false;
                TeamReachedExitZone = false;
                ResetProjectileCourse(); Recover(); Present(0, true); SendRequest(0, resetBarrier.Begin());
            }
        }
        void Detach(bool suspendHost = false)
        {
            // Additive scene activation can disable the host root before the client root.
            // That client must see a newer empty snapshot when this channel is enabled again.
            suspendedHostMessaging = suspendHost && session != null && session.IsHost ? messaging : null;
            if (suspendedHostMessaging != null)
            {
                ownership.Reset();
                stateRevision++;
                ClearShots();
            }
            else
            {
                ownership = new CannonSeatState();
                stateRevision = courseEpoch = 0;
                TeamReachedExitZone = false;
                cutProgress = 0;
                ResetProjectileCourse();
            }
            if (messaging != null)
            {
                if (attachedRequestName != null) messaging.UnregisterNamedMessageHandler(attachedRequestName);
                if (attachedStateName != null) messaging.UnregisterNamedMessageHandler(attachedStateName);
            }
            attachedRequestName = attachedStateName = null;
            pendingGrabs.Clear();
            messaging = null; session = null; synchronized = false;
            resetBarrier.Clear();
            Recover(); Present(0, true);
        }
    }
}
