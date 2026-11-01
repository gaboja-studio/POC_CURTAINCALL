using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeExitSaw
    {
        CharacterController ignoredCarrierBody;
        readonly List<Collider> carriedColliders = new();
        readonly List<bool> previousIgnoredPairs = new();

        // Only the current bearer ignores their own held machine. Its colliders still block the world and other players.
        void RefreshCarrierCollision(NetworkPlayer carrier)
        {
            var body = carrier != null && carrier.TryGetComponent(out PlayerMover mover) ? mover.Body : null;
            if (body != ignoredCarrierBody)
            {
                RestoreCarrierCollision();
                if (body == null) return;
                ignoredCarrierBody = body;
                CarryRoot.GetComponentsInChildren(true, carriedColliders);
                for (int i = carriedColliders.Count - 1; i >= 0; i--)
                    if (carriedColliders[i] == body || carriedColliders[i].isTrigger) carriedColliders.RemoveAt(i);
                foreach (var collider in carriedColliders) previousIgnoredPairs.Add(Physics.GetIgnoreCollision(body, collider));
            }
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) return;
            // CharacterController re-enabling clears IgnoreCollision, including respawn and inspection transitions.
            foreach (var collider in carriedColliders)
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(body, collider, true);
        }

        void RestoreCarrierCollision()
        {
            if (ignoredCarrierBody != null)
                for (int i = 0; i < carriedColliders.Count; i++)
                    if (carriedColliders[i] != null)
                        Physics.IgnoreCollision(ignoredCarrierBody, carriedColliders[i], previousIgnoredPairs[i]);
            ignoredCarrierBody = null;
            carriedColliders.Clear(); previousIgnoredPairs.Clear();
        }
    }
}
