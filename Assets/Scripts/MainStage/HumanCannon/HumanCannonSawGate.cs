using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>연결 봉의 톱 관문. 호스트가 지상 담당의 통과와 접촉 부상을 확정한다.</summary>
    public sealed class HumanCannonSawGate : MonoBehaviour
    {
        [SerializeField] Collider barrier;
        [SerializeField] Transform sawVisual;
        [Tooltip("톱 접촉 여유(m)."), Min(0f), SerializeField] float contactPadding = 0.15f;
        [Tooltip("톱 외형 회전 속도(도/초)."), Min(0f), SerializeField] float spinSpeed = 180f;
        readonly HashSet<ulong> touching = new();
        readonly Dictionary<ulong, float> carrierSides = new();
        readonly List<ulong> stale = new();
        MaterialPropertyBlock tint;
        Renderer[] renderers;
        Renderer[] barrierRenderers;
        Vector3 visualPosition;
        public bool CarrierPassed { get; private set; }
        public PoleSawPhase Phase { get; private set; } = PoleSawPhase.Active;

        void Awake()
        {
            if (barrier != null) barrierRenderers = barrier.GetComponentsInChildren<Renderer>();
            if (sawVisual != null)
            {
                visualPosition = sawVisual.localPosition;
                renderers = sawVisual.GetComponentsInChildren<Renderer>();
            }
        }

        public float Side(Vector3 point) => Vector3.Dot(point - transform.position, transform.forward);

        internal void Present(PoleSawPhase phase, bool passed, double time)
        {
            Phase = phase;
            CarrierPassed = passed;
            if (barrier != null)
            {
                barrier.enabled = phase == PoleSawPhase.Active;
                if (barrierRenderers != null) foreach (var visual in barrierRenderers) visual.enabled = phase == PoleSawPhase.Active;
            }
            if (sawVisual == null) return;
            tint ??= new MaterialPropertyBlock();
            // 지상 운반자의 관문이다. 접힌 톱을 봉 도약 높이로 올리지 않는다.
            // 재동작 예고는 원래 지상 위치에서 점멸하되 충돌은 Active부터 켠다.
            sawVisual.localPosition = visualPosition + (phase == PoleSawPhase.Retracted ? Vector3.down * 4f : Vector3.zero);
            Color color = phase == PoleSawPhase.Warning
                ? (Mathf.Repeat((float)time, 0.5f) < 0.25f ? Color.red : Color.yellow)
                : phase == PoleSawPhase.Retracted ? Color.green : Color.gray;
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            if (renderers != null) foreach (var item in renderers) if (item != null) item.SetPropertyBlock(tint);
            foreach (Transform blade in sawVisual)
                blade.localRotation = Quaternion.Euler(0f, 0f, (float)(time * spinSpeed % 360.0));
        }

        internal bool HostTick(PoleSawPhase phase, ulong poleUser)
        {
            bool wasPassed = CarrierPassed;
            var trampoline = HumanCannonTrampoline.Current;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                ulong actor = player.OwnerClientId;
                float side = Side(player.transform.position);
                bool carrying = trampoline != null && trampoline.Carrier == actor;
                if (carrying && phase != PoleSawPhase.Active && actor != poleUser
                    && carrierSides.TryGetValue(actor, out float before) && before <= 0f && side > 0f)
                    CarrierPassed = true;
                if (carrying) carrierSides[actor] = side;
                else carrierSides.Remove(actor);
                bool hit = phase == PoleSawPhase.Active && Touches(player);
                if (hit && touching.Add(actor)) player.ServerCut();
                if (!hit) touching.Remove(actor);
            }
            stale.Clear();
            foreach (ulong actor in touching)
            {
                bool alive = false;
                foreach (var player in NetworkPlayer.All)
                    if (player != null && player.IsSpawned && player.OwnerClientId == actor && player.State == PlayerState.Normal)
                        alive = true;
                if (!alive) stale.Add(actor);
            }
            foreach (ulong actor in stale) { touching.Remove(actor); carrierSides.Remove(actor); }
            return wasPassed != CarrierPassed;
        }

        bool Touches(NetworkPlayer player)
        {
            if (barrier == null || !barrier.enabled || !player.TryGetComponent(out PlayerMover mover)) return false;
            Bounds body = new Bounds(player.transform.position + Vector3.up * mover.BodyHeight * 0.5f,
                new Vector3(mover.BodyRadius * 2f, mover.BodyHeight, mover.BodyRadius * 2f));
            body.Expand(contactPadding * 2f);
            return barrier.bounds.Intersects(body);
        }

        /// <summary>지상 담당 통과 전 비행자의 위/옆 경로 건너뛰기를 막는다.</summary>
        internal Vector3 ConstrainFlight(Vector3 from, Vector3 displacement, float radius)
        {
            if (CarrierPassed) return displacement;
            float before = Side(from);
            float after = Side(from + displacement);
            float boundary = Mathf.Max(before, -radius);
            if (before <= 0f && after > boundary)
                displacement -= transform.forward * (after - boundary);
            return displacement;
        }

        internal void ResetGate()
        {
            CarrierPassed = false;
            touching.Clear();
            carrierSides.Clear();
            Present(PoleSawPhase.Active, false, 0);
        }
    }
}
