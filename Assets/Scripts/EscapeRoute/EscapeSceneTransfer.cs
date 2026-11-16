using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Telemetry.Pipeline;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.SpaceLink;
using CurtainCall.WaitingRoom;
using CurtainCall.WaitingRoom.Pursuit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Departure roster and each owner's pose survive unloading the waiting room.</summary>
    [DefaultExecutionOrder(-150)]
    public sealed class EscapeSceneTransfer : MonoBehaviour
    {
        [Serializable] sealed class Snapshot
        {
            public int generation = -1, phase;
            public ulong[] participants = Array.Empty<ulong>();
            public Vector3[] positions = Array.Empty<Vector3>();
            public Quaternion[] rotations = Array.Empty<Quaternion>();
            public string failure = "";
        }
        [Serializable] sealed class Ack { public int generation; public string kind; }
        Snapshot snapshot = new();
        readonly HashSet<ulong> captured = new(), placed = new();
        ShopChannel channel;
        Transform source;
        PlayerMover heldMover;
        bool localCaptured, localPlaced, holding, capturedView;
        Vector3 localPosition;
        Quaternion localRotation, localLook, arrivalLook;
        Camera sourceCamera;
        CameraViewDirector sourceDirector;
        float nextAck, retryAt;
        public static EscapeSceneTransfer Current { get; private set; }
        public bool HasDeparture => snapshot.phase > 0;
        public bool Ready => snapshot.phase == 2;
        public bool Pending => snapshot.phase == 1;
        public bool Coordinating => Pending && channel.IsHost;
        public static bool HoldingForDeparture => Current != null && Current.holding;
        public string Failure => snapshot.failure;
        public IReadOnlyList<ulong> Participants => snapshot.participants;
        public bool AllCaptured => AllConnectedAcknowledged(captured);
        public bool AllPlaced => AllConnectedAcknowledged(placed);

        public static EscapeSceneTransfer EnsureExists()
        {
            if (Current != null) return Current;
            var go = new GameObject(nameof(EscapeSceneTransfer));
            DontDestroyOnLoad(go);
            return go.AddComponent<EscapeSceneTransfer>();
        }
        void Awake()
        {
            if (Current != null && Current != this) { Destroy(gameObject); return; }
            Current = this;
            channel = new ShopChannel("CurtainCall.Escape.Transfer", Request, Receive, _ => { }, Cancel,
                () => channel.Request("sync"));
            channel.Update();
        }
        public void SetSource(Transform anchor) => source = anchor;
        public bool Begin(IReadOnlyList<ulong> ids, int generation)
        {
            channel.Update();
            if (!channel.IsHost || HasDeparture || source == null || ids.Count < 1 || ids.Count != channel.Session.PlayerCount) return false;
            var unique = new HashSet<ulong>(ids);
            if (unique.Count != ids.Count) return false;
            var next = new Snapshot { generation = generation, phase = 1, participants = new ulong[ids.Count],
                positions = new Vector3[ids.Count], rotations = new Quaternion[ids.Count] };
            for (int i = 0; i < ids.Count; i++)
            {
                NetworkPlayer member = null;
                foreach (var player in NetworkPlayer.All)
                    if (player != null && player.IsSpawned && player.OwnerClientId == ids[i]) { member = player; break; }
                if (member == null || !channel.IsConnected(ids[i])) return false;
                next.participants[i] = ids[i];
                bool caught = PursuitDirector.IsCaptured(member);
                next.positions[i] = caught
                    ? PursuitDirector.ArrivalLocal(source.GetComponent<BoxCollider>(), i, ids.Count)
                    : source.InverseTransformPoint(member.transform.position);
                next.rotations[i] = caught ? Quaternion.identity : Quaternion.Inverse(source.rotation) * member.transform.rotation;
            }
            snapshot = next;
            RecordTransfer("TRANSFER_STARTED", source.position, true);
            retryAt = Time.unscaledTime + 10;
            CaptureLocal();
            Publish();
            return true;
        }
        void Update()
        {
            channel.Update();
            if (!HasDeparture) return;
            var run = CampaignRun.Instance;
            if (run == null || run.State.Generation != snapshot.generation || run.State.Phase != CampaignPhase.Escape)
            {
                // The transfer can arrive before the campaign step in either departure phase.
                if (Pending && run != null && run.State.Generation == snapshot.generation
                    && PursuitDirector.IsPursuitPhase(run.State.Phase)) return;
                Cancel(); return;
            }
            if (!localCaptured) CaptureLocal();
            if (Pending && Time.unscaledTime >= nextAck)
            {
                nextAck = Time.unscaledTime + .5f;
                // ShopChannel rate-limits requests; send only the most advanced acknowledgement.
                if (localPlaced) Acknowledge("placed");
                else if (localCaptured) Acknowledge("captured");
            }
            if (Ready && localPlaced) Release();
            if (Pending && channel.IsHost && Time.unscaledTime >= retryAt)
            {
                var director = SpaceLinkDirector.Instance;
                if (director != null && !director.IsTransitioning)
                {
                    retryAt = Time.unscaledTime + 10;
                    director.Request(SpaceId.EscapeRoute);
                }
            }
        }
        void CaptureLocal()
        {
            var player = NetworkPlayer.Local;
            if (localCaptured || source == null || player == null || !player.IsSpawned
                || !player.TryGetComponent(out PlayerMover mover)) return;
            int index = Array.IndexOf(snapshot.participants, player.OwnerClientId);
            if (index < 0) return;
            localPosition = source.InverseTransformPoint(player.transform.position);
            localRotation = Quaternion.Inverse(source.rotation) * player.transform.rotation;
            // A client can cross the rear doorway during network delay. Keep that player inside
            // using the host's already accepted assembly pose instead of spawning outside A.
            var area = source.GetComponent<BoxCollider>();
            if (area != null && !EscapeEntrance.Contains(area, player.transform.position))
            {
                localPosition = snapshot.positions[index];
                localRotation = snapshot.rotations[index];
            }
            sourceCamera = Camera.main;
            sourceDirector = CameraViewDirector.Current;
            capturedView = sourceCamera != null;
            if (capturedView) localLook = Quaternion.Inverse(source.rotation) * sourceCamera.transform.rotation;
            // PlayerMover checks HoldingForDeparture. Its simulation flag belongs to the
            // device or piggyback link currently controlling the player.
            heldMover = mover;
            heldMover.ClearPendingInput();
            holding = true;
            InputGate.SetBlocking(this, true);
            localCaptured = true;
            Acknowledge("captured");
        }
        public bool PlaceLocal(EscapeSceneArrival destination)
        {
            if (!HasDeparture || destination == null) return false;
            if (localPlaced) return true;
            var player = NetworkPlayer.Local;
            if (!localCaptured || source == null || heldMover == null || player == null || !player.IsSpawned) return false;
            var content = EscapeCampaignContent.Current;
            if (content == null || !content.IsActive || !content.IsSceneryReady) return false;
            int index = Array.IndexOf(snapshot.participants, player.OwnerClientId);
            if (index < 0 || !TryGetArrivalView()) return false;
            var scene = destination.gameObject.scene;
            var localPose = new Pose(localPosition, localRotation);
            var hostPose = new Pose(snapshot.positions[index], snapshot.rotations[index]);
            if (!TryResolveArrivalPose(scene, destination.AssemblyArea, localPose, hostPose, index, snapshot.participants.Length, out var pose)) return false;

            MoveCamera(sourceCamera.transform, source.gameObject.scene, scene);
            MoveCamera(sourceDirector.transform, source.gameObject.scene, scene);
            heldMover.SetStartPose(pose.position, pose.rotation);
            arrivalLook = destination.AssemblyAnchor.rotation * localLook;
            sourceDirector.SetLookRotation(arrivalLook);
            localPlaced = true;
            RecordTransfer("TRANSFER_PLACED", pose.position, false);
            Acknowledge("placed");
            return true;
        }

        bool TryGetArrivalView()
        {
            if (sourceCamera == null || !sourceCamera.isActiveAndEnabled)
            {
                sourceCamera = Camera.main;
                capturedView = false;
            }
            if (sourceDirector == null || !sourceDirector.isActiveAndEnabled) sourceDirector = CameraViewDirector.Current;
            if (sourceCamera == null || sourceDirector == null || !sourceDirector.isActiveAndEnabled) return false;
            if (!capturedView)
            {
                localLook = Quaternion.Inverse(source.rotation) * sourceCamera.transform.rotation;
                capturedView = true;
            }
            return true;
        }

        static bool TryResolveArrivalPose(Scene scene, BoxCollider area, Pose localPose, Pose hostPose, int index, int count, out Pose pose)
        {
            pose = default;
            if (!area || !scene.isLoaded) return false;
            var anchor = area.transform;
            var position = anchor.TransformPoint(localPose.position);
            if (Supported(position)) { pose = new Pose(position, anchor.rotation * localPose.rotation); return true; }
            position = anchor.TransformPoint(hostPose.position);
            if (Supported(position)) { pose = new Pose(position, anchor.rotation * hostPose.rotation); return true; }
            position = anchor.TransformPoint(PursuitDirector.ArrivalLocal(area, index, count));
            if (!Supported(position)) return false;
            pose = new Pose(position, anchor.rotation * hostPose.rotation);
            return true;

            bool Supported(Vector3 feet) => EscapeEntrance.Contains(area, feet)
                && EscapeDevelopmentSession.HasArrivalFloor(scene, feet);
        }

        static void MoveCamera(Transform camera, Scene source, Scene destination)
        {
            // Global/Hub cameras already survive W unloading; do not steal their lifetime.
            if (source == destination || camera.gameObject.scene != source) return;
            camera.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(camera.gameObject, destination);
        }
        bool AllConnectedAcknowledged(HashSet<ulong> acknowledgements)
        {
            if (!HasDeparture || !channel.IsHost) return false;
            foreach (ulong id in snapshot.participants)
                if (channel.IsConnected(id) && !acknowledgements.Contains(id)) return false;
            return true;
        }
        void Acknowledge(string kind)
        {
            if (channel.IsHost)
            {
                if (kind == "captured") captured.Add(channel.LocalClientId);
                else placed.Add(channel.LocalClientId);
            }
            else channel.Request(JsonUtility.ToJson(new Ack { generation = snapshot.generation, kind = kind }));
        }
        void Request(ulong sender, string json)
        {
            if (json == "sync") { Publish(); return; }
            if (!Pending || Array.IndexOf(snapshot.participants, sender) < 0) return;
            var ack = JsonUtility.FromJson<Ack>(json);
            if (ack == null || ack.generation != snapshot.generation) return;
            if (ack.kind == "captured") captured.Add(sender);
            else if (ack.kind == "placed") { captured.Add(sender); placed.Add(sender); }
        }
        void Publish() { if (channel.IsHost) channel.SendState(JsonUtility.ToJson(snapshot)); }
        void Receive(string json)
        {
            var value = JsonUtility.FromJson<Snapshot>(json);
            var run = CampaignRun.Instance;
            if (run == null || value == null || value.generation != run.State.Generation) return;
            ApplySnapshot(value);
        }
        bool ApplySnapshot(Snapshot value)
        {
            if (value == null || value.generation < 0 || value.phase < 1 || value.phase > 2
                || value.participants == null || value.participants.Length < 1
                || value.positions == null || value.positions.Length != value.participants.Length
                || value.rotations == null || value.rotations.Length != value.participants.Length) return false;
            // A delayed sync reply must not return a released player to the pending phase.
            if (snapshot.generation == value.generation && value.phase < snapshot.phase) return false;
            if (new HashSet<ulong>(value.participants).Count != value.participants.Length) return false;
            if (snapshot.generation == value.generation && HasDeparture)
            {
                if (snapshot.participants.Length != value.participants.Length) return false;
                for (int i = 0; i < snapshot.participants.Length; i++)
                    if (snapshot.participants[i] != value.participants[i]) return false;
            }
            if (snapshot.generation != value.generation) Cancel();
            snapshot = value;
            if (HasDeparture && !localCaptured) CaptureLocal();
            return true;
        }
        static void RecordTransfer(string state, Vector3 at, bool host)
        {
            var runtime = TelemetryRuntime.Instance;
            if (runtime == null || runtime.CurrentRunId == null) return;
            runtime.Emit("WORLD_STATE_CHANGED", new() { ["entityId"] = "ESCAPE_ENTRANCE", ["entityKind"] = "OBJECT",
                ["state"] = state, ["observation"] = "ESCAPE_TRANSFER", ["position"] = DetailLogIds.Position(at),
                ["subjectKind"] = host ? "TEAM" : "PLAYER" }, host ? "HOST" : "LOCAL");
        }
        public void Complete()
        {
            if (!channel.IsHost || !AllPlaced) return;
            if (snapshot.phase != 2 && EscapeSceneArrival.Current != null) RecordTransfer("TRANSFER_COMPLETE", EscapeSceneArrival.Current.AssemblyAnchor.position, true);
            snapshot.phase = 2; snapshot.failure = "";
            Publish();
            // The curtain still blocks input until its reveal animation completes.
            Release();
        }
        public void Failed(string reason)
        {
            if (!channel.IsHost) return;
            if (snapshot.failure != reason && source != null) RecordTransfer("TRANSFER_FAILED", source.position, true);
            snapshot.failure = reason;
            retryAt = Time.unscaledTime + 5;
            Publish();
        }
        public void Cancel()
        {
            Release(); snapshot = new(); captured.Clear(); placed.Clear();
            localCaptured = localPlaced = capturedView = false;
            heldMover = null; sourceCamera = null; sourceDirector = null;
        }
        void Release()
        {
            if (!holding) return;
            holding = false;
            if (localPlaced && sourceDirector != null) sourceDirector.SetLookRotation(arrivalLook);
            InputGate.SetBlocking(this, false);
        }
        void OnDestroy()
        {
            Release(); channel?.Dispose();
            if (Current == this) Current = null;
        }
    }
}
