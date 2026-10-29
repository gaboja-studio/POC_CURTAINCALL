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
        Transform[] blades;
        Quaternion[] bladeRotations;
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
                blades = new Transform[sawVisual.childCount];
                bladeRotations = new Quaternion[blades.Length];
                for (int i = 0; i < blades.Length; i++)
                {
                    blades[i] = sawVisual.GetChild(i);
                    bladeRotations[i] = blades[i].localRotation;
                }
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
                if (barrierRenderers != null) foreach (var visual in barrierRenderers)
                    if (visual != null) visual.enabled = phase == PoleSawPhase.Active && visual.transform != barrier.transform;
            }
            if (sawVisual == null) return;
            tint ??= new MaterialPropertyBlock();
            // 해제 봉을 잡으면 제자리에서 정지한다. 예고 중에도 멈춰 있고 Active부터 회전·접촉한다.
            sawVisual.localPosition = visualPosition;
            Color color = phase == PoleSawPhase.Warning
                ? (Mathf.Repeat((float)time, 0.5f) < 0.25f ? Color.red : Color.yellow)
                : phase == PoleSawPhase.Retracted ? Color.green : Color.gray;
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            if (renderers != null) foreach (var item in renderers) if (item != null) item.SetPropertyBlock(tint);
            if (phase == PoleSawPhase.Active && blades != null)
                for (int i = 0; i < blades.Length; i++)
                    if (blades[i] != null)
                        blades[i].localRotation = bladeRotations[i] * Quaternion.Euler(0f, 0f, (float)(time * spinSpeed % 360.0));
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
            // 철망까지 확장한 물리 벽과 톱날의 절단 범위를 구분한다.
            Bounds sawBounds = barrier.bounds;
            bool foundSaw = false;
            if (renderers != null) foreach (var visual in renderers)
            {
                if (visual == null) continue;
                if (!foundSaw) { sawBounds = visual.bounds; foundSaw = true; }
                else sawBounds.Encapsulate(visual.bounds);
            }
            return sawBounds.Intersects(body);
        }

        /// <summary>공중 가상면을 사용하지 않는다. 기존 유한한 물리 벽만 충돌을 처리한다.</summary>
        internal Vector3 ConstrainFlight(Vector3 from, Vector3 displacement, float radius) => displacement;

        internal void ResetGate()
        {
            CarrierPassed = false;
            touching.Clear();
            carrierSides.Clear();
            Present(PoleSawPhase.Active, false, 0);
        }
    }
}
