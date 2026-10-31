using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.SpaceLink;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom;
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
        bool simulatedBefore, localCaptured, localPlaced, holding;
        Vector3 localPosition;
        Quaternion localRotation, localLook, arrivalLook;
        Camera sourceCamera;
        CameraViewDirector sourceDirector;
        float nextAck, retryAt;
        public static EscapeSceneTransfer Current { get; private set; }
        public bool HasDeparture => snapshot.phase > 0;
        public bool Ready => snapshot.phase == 2;
        public bool Pending => snapshot.phase == 1;
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
                next.positions[i] = source.InverseTransformPoint(member.transform.position);
                next.rotations[i] = Quaternion.Inverse(source.rotation) * member.transform.rotation;
            }
            snapshot = next;
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
                // A transfer state can arrive just before the campaign message on a client.
                if (run != null && run.State.Generation == snapshot.generation && run.State.Phase == CampaignPhase.RestTime) return;
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
                || Array.IndexOf(snapshot.participants, player.OwnerClientId) < 0) return;
            localPosition = source.InverseTransformPoint(player.transform.position);
            localRotation = Quaternion.Inverse(source.rotation) * player.transform.rotation;
            // A client can cross the rear doorway during network delay. Keep that player inside
            // using the host's already accepted assembly pose instead of spawning outside A.
            var area = source.GetComponent<BoxCollider>();
            int index = Array.IndexOf(snapshot.participants, player.OwnerClientId);
            if (area != null && !EscapeEntrance.Contains(area, player.transform.position))
            {
                localPosition = snapshot.positions[index];
                localRotation = snapshot.rotations[index];
            }
            sourceCamera = Camera.main;
            sourceDirector = CameraViewDirector.Current;
            localLook = Quaternion.Inverse(source.rotation) * (sourceCamera != null ? sourceCamera.transform.rotation : player.transform.rotation);
            heldMover = player.GetComponent<PlayerMover>();
            simulatedBefore = heldMover.Simulated;
            heldMover.ClearPendingInput();
            heldMover.Simulated = false;
            holding = true;
            InputGate.SetBlocking(this, true);
            localCaptured = true;
            Acknowledge("captured");
        }
        public bool PlaceLocal(EscapeSceneArrival destination)
        {
            if (!HasDeparture || destination == null) return false;
            if (localPlaced) return true;
            if (!localCaptured || heldMover == null) return false;
            var content = EscapeCampaignContent.Current;
            if (content == null || !content.IsActive || !content.IsSceneryReady) return false;
            MoveCamera(sourceCamera != null ? sourceCamera.transform : null, destination.gameObject.scene);
            MoveCamera(sourceDirector != null ? sourceDirector.transform : null, destination.gameObject.scene);
            if (sourceDirector == null || sourceCamera == null) return false;
            var anchor = destination.AssemblyAnchor;
            heldMover.SetStartPose(anchor.TransformPoint(localPosition), anchor.rotation * localRotation);
            arrivalLook = anchor.rotation * localLook;
            sourceDirector.SetLookRotation(arrivalLook);
            localPlaced = true;
            Acknowledge("placed");
            return true;
        }
        static void MoveCamera(Transform camera, Scene destination)
        {
            // Global/Hub cameras already survive W unloading; do not steal their lifetime.
            if (camera == null || camera.gameObject.scene == destination
                || camera.gameObject.scene.name != GameSettings.Base.SpaceTransition.WaitingRoomScene) return;
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
            if (value == null || run == null || value.generation != run.State.Generation || value.phase < 0 || value.phase > 2
                || value.participants == null || value.participants.Length < 1
                || value.positions == null || value.positions.Length != value.participants.Length
                || value.rotations == null || value.rotations.Length != value.participants.Length) return;
            if (snapshot.generation != value.generation) Cancel();
            snapshot = value;
            if (HasDeparture && !localCaptured) CaptureLocal();
        }
        public void Complete()
        {
            if (!channel.IsHost || !AllPlaced) return;
            snapshot.phase = 2; snapshot.failure = "";
            Publish();
            // The curtain still blocks input until its reveal animation completes.
            Release();
        }
        public void Failed(string reason)
        {
            if (!channel.IsHost) return;
            snapshot.failure = reason;
            retryAt = Time.unscaledTime + 5;
            Publish();
        }
        public void Cancel()
        {
            Release(); snapshot = new(); captured.Clear(); placed.Clear();
            localCaptured = localPlaced = false;
            heldMover = null; sourceCamera = null; sourceDirector = null;
        }
        void Release()
        {
            if (!holding) return;
            holding = false;
            if (heldMover != null && NetworkPlayer.Local != null && NetworkPlayer.Local.State == PlayerState.Normal)
                heldMover.Simulated = simulatedBefore;
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
