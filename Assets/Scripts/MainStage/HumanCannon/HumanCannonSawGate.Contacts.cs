using System.Collections.Generic;
using System.Text;
using CurtainCall.Network.Session;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSawGate
    {
        sealed class ContactFrame
        {
            public double At;
            public readonly List<Bounds> Bounds = new();
        }
        readonly ContactFrame[] gateContactHistory = new ContactFrame[48];
        OwnerContactChannel ownerContacts;
        int nextGateContactFrame;
        double nextGateContactAt;
        double lastGateContactAt = -1;

        void LateUpdate()
        {
            var session = NetworkSessionManager.Instance;
            double now = session != null && session.NetworkManager != null && session.NetworkManager.IsListening
                ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
            if (session != null && session.IsHost) CaptureContactFrame(now);
            if (ownerContacts == null) {
                // Scene hierarchy is identical on each peer; instance IDs are not network identities.
                var path = new StringBuilder(gameObject.scene.name);
                for (var item = transform; item != null; item = item.parent) path.Append('/').Append(item.GetSiblingIndex());
                ownerContacts = new OwnerContactChannel("CurtainCall.HumanCannon.Gate.Contact.v2:" + path,
                    () => 1, _ => 0, ReportedGateContactAt,
                    (player, _) => player.ServerHazardCut());
            }
            ownerContacts.Tick(session, now, isActiveAndEnabled
                && (HumanCannonDestination.Current == null || !HumanCannonDestination.Current.IsResolved), false);
        }

        bool ReportedGateContactAt(int index, double at, Vector3 feet, float height, float radius)
        {
            // Resolve the current session on each report, including reconnects in the same scene.
            var current = NetworkSessionManager.Instance;
            return current != null && current.IsHost ? GateContactAt(index, at, feet, height, radius)
                : TouchesDisplayedBlades(feet, height, radius);
        }

        void CaptureContactFrame(double now)
        {
            if (now < lastGateContactAt) {
                // A new host clock can restart at zero in the same scene. Neither
                // old geometry nor the old receive-time floor belongs to that session.
                ownerContacts?.Dispose();
                foreach (var old in gateContactHistory) old?.Bounds.Clear();
                nextGateContactFrame = 0; nextGateContactAt = 0;
            }
            lastGateContactAt = now;
            if (now < nextGateContactAt) return;
            var frame = gateContactHistory[nextGateContactFrame] ??= new ContactFrame();
            frame.At = now; frame.Bounds.Clear();
            if (IsSpinning)
                for (int i = 0; blades != null && i < blades.Length; i++) {
                    if (drops[i] != BladeDrop.Attached) continue;
                    foreach (var renderer in pieceRenderers[i])
                        if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy) frame.Bounds.Add(renderer.bounds);
                }
            nextGateContactFrame = (nextGateContactFrame + 1) % gateContactHistory.Length;
            nextGateContactAt = now + 1d / 30;
        }

        bool GateContactAt(int index, double at, Vector3 feet, float height, float radius)
        {
            ContactFrame closest = null; double age = .075;
            foreach (var frame in gateContactHistory)
                if (frame != null && frame.At <= at && at - frame.At < age) { closest = frame; age = at - frame.At; }
            if (closest == null) return false;
            var body = new Bounds(feet + Vector3.up * height * .5f, new Vector3(radius * 2, height, radius * 2));
            body.Expand(contactPadding * 2);
            foreach (var blade in closest.Bounds) if (blade.Intersects(body)) return true;
            return false;
        }

        void OnDisable()
        {
            ownerContacts?.Dispose();
            foreach (var frame in gateContactHistory) frame?.Bounds.Clear();
            nextGateContactAt = 0;
            lastGateContactAt = -1;
        }
    }
}
