using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>
    /// 연결 봉의 톱 관문. 봉을 처음 잡으면 톱이 멈추고(닿아도 안전) 잠시 뒤 가운데에서 좌우로 갈라져
    /// 트램펄린이 지나갈 틈을 열고, 공연을 다시 시작할 때까지 열린 채 남는다. 다 열리면 양쪽 톱이 다시 돌며 닿으면 다친다.
    /// 다 열린 뒤 지상 바닥 밖에 놓인 톱·철망 조각은 아래로 떨어지고, 플레이어 낙하 높이에 닿으면 사라진다.
    /// 호스트가 지상 담당의 통과와 접촉 부상을 확정한다.
    /// </summary>
    public sealed class HumanCannonSawGate : MonoBehaviour
    {
        [SerializeField] Transform sawVisual;
        [Tooltip("왼쪽으로 함께 밀리는 톱 묶음·철망·충돌 벽.")]
        [SerializeField] Transform[] leftParts = System.Array.Empty<Transform>();
        [Tooltip("오른쪽으로 함께 밀리는 톱 묶음·철망·충돌 벽.")]
        [SerializeField] Transform[] rightParts = System.Array.Empty<Transform>();
        [Tooltip("열린 가운데 틈 폭(m). 트램펄린 폭에 좌우 1m씩 더한 값."), Min(0f), SerializeField] float openGap = 7f;
        [Tooltip("봉을 잡아 톱이 멈춘 뒤 갈라지기 시작할 때까지(초)."), Min(0f), SerializeField] float stopSeconds = 0.5f;
        [Tooltip("좌우로 갈라지는 데 걸리는 시간(초)."), Min(0.05f), SerializeField] float openSeconds = 1f;
        [Tooltip("바닥 밖 톱의 낙하 가속(m/s²). 조각마다 ±20% 다르다."), Min(0.1f), SerializeField] float dropGravity = 20f;
        [Tooltip("바닥 밖 조각이 떨어지기 시작하는 시점 차이의 최대(초). 조각마다 0~이 값 사이."), Min(0f), SerializeField] float dropStagger = 0.6f;
        [Tooltip("톱 접촉 여유(m)."), Min(0f), SerializeField] float contactPadding = 0.15f;
        [Tooltip("톱 외형 회전 속도(도/초)."), Min(0f), SerializeField] float spinSpeed = 180f;
        [Tooltip("열릴 때 문짝이 덜컹거리는 최대 폭(m). 멈춤 중에는 절반."), Min(0f), SerializeField] float shakeAmplitude = 0.06f;
        [Tooltip("갈라지는 동안 바닥에 끌리며 튀는 스파크.")]
        [SerializeField] ParticleSystem[] dragSparks = System.Array.Empty<ParticleSystem>();
        // 경로 시스템이 없는 테스트 씬에서 떨어진 톱을 지울 관문 아래 거리(m).
        const float FallbackDropDepth = 30f;
        readonly HashSet<ulong> touching = new();
        readonly Dictionary<ulong, float> carrierSides = new();
        readonly List<ulong> stale = new();
        readonly RaycastHit[] floorHits = new RaycastHit[16];
        MaterialPropertyBlock tint;
        Renderer[] renderers;
        Transform[] blades;
        // 떨어질 수 있는 조각: 앞쪽은 blades와 같은 순서의 톱, 뒤쪽은 철망 조각.
        Transform[] pieces;
        Renderer[][] pieceRenderers;
        int[] bladeSides;
        Quaternion[] bladeRotations;
        Vector3[] piecePositions;
        BladeDrop[] drops;
        float[] dropSpeeds, dropDelays, dropGravityScales, tumbleSpeeds;
        Vector3[] dropDrifts, tumbleAxes, tumbleCenters, pieceScales;
        Quaternion[] pieceRotations;
        Transform[] pieceParents;
        Vector3[] leftBase, rightBase;
        float leftShift, rightShift, openAmount, stoppedFor;
        // 멈췄던 각도에서 다시 돌도록 공용 시계 각도에 더하는 값.
        float spinOffset, spinAngle;
        float shakeTime;
        bool dropsChecked, wasSpinning = true, sparking;
        enum BladeDrop : byte { Attached, Falling, Gone }
        public bool CarrierPassed { get; private set; }
        /// <summary>한 번 열리면 재시작 전까지 닫히지 않는다.</summary>
        public bool IsOpen { get; private set; }
        public float OpenAmount => openAmount;
        /// <summary>닫혀 있거나 다 열린 뒤에만 톱이 돌고 다치게 한다. 멈춤·갈라짐 중에는 정지.</summary>
        public bool IsSpinning => !IsOpen || openAmount >= 1f;
        public float OpenGap => openGap;
        public PoleSawPhase Phase { get; private set; } = PoleSawPhase.Active;

        void Awake()
        {
            leftBase = BasePositions(leftParts);
            rightBase = BasePositions(rightParts);
            if (sawVisual != null)
            {
                // 스파크 파티클은 톱 외형(회색 처리·절단 범위)에서 뺀다.
                renderers = System.Array.FindAll(sawVisual.GetComponentsInChildren<Renderer>(), item => item is not ParticleSystemRenderer);
                var found = new List<Transform>();
                foreach (var item in sawVisual.GetComponentsInChildren<Transform>())
                    if (item.name.StartsWith("GateSawDisc")) found.Add(item);
                blades = found.ToArray();
                tint = new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", Color.gray);
                tint.SetColor("_Color", Color.gray);
                foreach (var item in renderers) if (item != null) item.SetPropertyBlock(tint);
            }
            blades ??= System.Array.Empty<Transform>();
            var all = new List<Transform>(blades);
            foreach (var part in leftParts) Fences(part, all);
            foreach (var part in rightParts) Fences(part, all);
            pieces = all.ToArray();
            bladeRotations = new Quaternion[blades.Length];
            bladeSides = new int[blades.Length];
            piecePositions = new Vector3[pieces.Length];
            pieceRenderers = new Renderer[pieces.Length][];
            drops = new BladeDrop[pieces.Length];
            dropSpeeds = new float[pieces.Length];
            dropDelays = new float[pieces.Length];
            dropGravityScales = new float[pieces.Length];
            tumbleSpeeds = new float[pieces.Length];
            dropDrifts = new Vector3[pieces.Length];
            tumbleAxes = new Vector3[pieces.Length];
            pieceRotations = new Quaternion[pieces.Length];
            tumbleCenters = new Vector3[pieces.Length];
            pieceScales = new Vector3[pieces.Length];
            pieceParents = new Transform[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                piecePositions[i] = pieces[i].localPosition;
                pieceRotations[i] = pieces[i].localRotation;
                pieceScales[i] = pieces[i].localScale;
                pieceParents[i] = pieces[i].parent;
                // 꺼진 하위 외형은 원점 경계를 돌려주므로 제외한다.
                pieceRenderers[i] = pieces[i].GetComponentsInChildren<Renderer>();
            }
            for (int i = 0; i < blades.Length; i++)
            {
                bladeRotations[i] = blades[i].localRotation;
                bladeSides[i] = Contains(leftParts, blades[i]) ? -1 : Contains(rightParts, blades[i]) ? 1 : 0;
            }
            // 닫힌 자세의 안쪽 끝을 재서, 열렸을 때 톱날·철망·충돌 벽 모두가 틈 밖에 있도록 민다.
            leftShift = Mathf.Max(0f, InnerEdge(leftParts, 1f) + openGap * 0.5f);
            rightShift = Mathf.Max(0f, openGap * 0.5f - InnerEdge(rightParts, -1f));
        }

        public float Side(Vector3 point) => Vector3.Dot(point - transform.position, transform.forward);
        public int FallingBladeCount => Count(BladeDrop.Falling, true);
        public int GoneBladeCount => Count(BladeDrop.Gone, true);
        public int FallingFenceCount => Count(BladeDrop.Falling, false);
        public int GoneFenceCount => Count(BladeDrop.Gone, false);

        internal void Present(PoleSawPhase phase, bool passed, bool open, double time)
        {
            Phase = phase;
            CarrierPassed = passed;
            if (open || phase != PoleSawPhase.Active) IsOpen = true;
            bool spinning = IsSpinning;
            float clock = (float)(time * spinSpeed % 360.0);
            if (spinning && !wasSpinning) spinOffset = spinAngle - clock;
            wasSpinning = spinning;
            if (!spinning) return;
            spinAngle = Mathf.Repeat(clock + spinOffset, 360f);
            if (blades != null)
                for (int i = 0; i < blades.Length; i++)
                    // 떨어지는 톱은 회전 대신 제각각 구른다.
                    if (blades[i] != null && (drops == null || drops[i] == BladeDrop.Attached))
                        blades[i].localRotation = bladeRotations[i] * Quaternion.Euler(0f, 0f, spinAngle);
        }

        void Update() => Animate(Time.deltaTime);

        internal void Animate(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f) return;
            float opening = deltaTime;
            // 잡은 순간 멈춘 톱을 stopSeconds 동안 보여 준 뒤 남은 시간만큼 갈라진다.
            if (IsOpen && stoppedFor < stopSeconds)
            {
                float wait = Mathf.Min(deltaTime, stopSeconds - stoppedFor);
                stoppedFor += wait;
                opening -= wait;
            }
            openAmount = Mathf.MoveTowards(openAmount, IsOpen ? 1f : 0f, opening / openSeconds);
            // 풀림(멈춤 중)에는 작게, 갈라지는 동안에는 크게 덜컹거리고 다 열리기 직전에 잦아들어 정확한 자리에 멈춘다.
            bool unlocking = IsOpen && openAmount < 1f;
            bool dragging = unlocking && stoppedFor >= stopSeconds;
            if (unlocking) shakeTime += deltaTime;
            float amplitude = !unlocking ? 0f : shakeAmplitude * (dragging ? Mathf.Clamp01((1f - openAmount) * 5f) : 0.5f);
            Place(leftParts, leftBase, transform.right * (-leftShift * openAmount) + Shake(0f) * amplitude);
            Place(rightParts, rightBase, transform.right * (rightShift * openAmount) + Shake(1.7f) * amplitude);
            if (dragging != sparking)
            {
                sparking = dragging;
                foreach (var sparks in dragSparks)
                {
                    if (sparks == null) continue;
                    if (dragging) sparks.Play(true);
                    else sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
            if (drops == null) return;
            // 모든 클라이언트가 같은 바닥과 같은 열린 자세로 판정하므로 별도 동기화 없이 같은 조각이 떨어진다.
            if (IsOpen && openAmount >= 1f && !dropsChecked)
            {
                dropsChecked = true;
                Physics.SyncTransforms();
                for (int i = 0; i < pieces.Length; i++)
                    if (pieces[i] != null && drops[i] == BladeDrop.Attached && !HasFloorBelow(i)) BeginDrop(i);
            }
            for (int i = 0; i < pieces.Length; i++)
            {
                if (drops[i] != BladeDrop.Falling || pieces[i] == null) continue;
                float step = deltaTime;
                if (dropDelays[i] > 0f)
                {
                    float wait = Mathf.Min(dropDelays[i], step);
                    dropDelays[i] -= wait;
                    step -= wait;
                    if (step <= 0f) continue;
                }
                dropSpeeds[i] += dropGravity * dropGravityScales[i] * step;
                // 조각마다 다른 축으로 구르며, 바깥·앞뒤로 조금 튕겨 나간다.
                pieces[i].RotateAround(pieces[i].TransformPoint(tumbleCenters[i]), tumbleAxes[i], tumbleSpeeds[i] * step);
                pieces[i].position += (Vector3.down * dropSpeeds[i] + dropDrifts[i]) * step;
                if (!BelowCourse(pieces[i].position)) continue;
                drops[i] = BladeDrop.Gone;
                pieces[i].gameObject.SetActive(false);
            }
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
                if (carrying && IsOpen && actor != poleUser
                    && carrierSides.TryGetValue(actor, out float before) && before <= 0f && side > 0f)
                    CarrierPassed = true;
                if (carrying) carrierSides[actor] = side;
                else carrierSides.Remove(actor);
                bool hit = Touches(player);
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
            // 멈춘 톱(봉을 잡은 뒤 다 열리기 전)은 닿아도 다치지 않는다.
            if (blades == null || !IsSpinning || !player.TryGetComponent(out PlayerMover mover)) return false;
            Bounds body = new Bounds(player.transform.position + Vector3.up * mover.BodyHeight * 0.5f,
                new Vector3(mover.BodyRadius * 2f, mover.BodyHeight, mover.BodyRadius * 2f));
            body.Expand(contactPadding * 2f);
            // 철망까지 확장한 물리 벽과 톱날의 절단 범위를 구분한다. 열린 틈을 덮지 않게 좌우를 따로 재고,
            // 떨어지는 톱은 판정에서 뺀다.
            for (int side = -1; side <= 1; side++)
            {
                bool found = false;
                Bounds saw = default;
                for (int i = 0; i < blades.Length; i++)
                {
                    if (bladeSides[i] != side || drops[i] != BladeDrop.Attached) continue;
                    foreach (var visual in pieceRenderers[i])
                    {
                        if (visual == null) continue;
                        if (!found) { saw = visual.bounds; found = true; }
                        else saw.Encapsulate(visual.bounds);
                    }
                }
                if (found && saw.Intersects(body)) return true;
            }
            return false;
        }

        /// <summary>공중 가상면을 사용하지 않는다. 기존 유한한 물리 벽만 충돌을 처리한다.</summary>
        internal Vector3 ConstrainFlight(Vector3 from, Vector3 displacement, float radius) => displacement;

        internal void ResetGate()
        {
            CarrierPassed = false;
            IsOpen = false;
            openAmount = stoppedFor = 0f;
            spinOffset = spinAngle = 0f;
            wasSpinning = true;
            shakeTime = 0f;
            dropsChecked = false;
            touching.Clear();
            carrierSides.Clear();
            for (int i = 0; drops != null && i < pieces.Length; i++)
            {
                drops[i] = BladeDrop.Attached;
                dropSpeeds[i] = 0f;
                if (pieces[i] == null) continue;
                pieces[i].gameObject.SetActive(true);
                if (pieces[i].parent != pieceParents[i]) pieces[i].SetParent(pieceParents[i], false);
                pieces[i].localPosition = piecePositions[i];
                pieces[i].localRotation = pieceRotations[i];
                pieces[i].localScale = pieceScales[i];
            }
            Present(PoleSawPhase.Active, false, false, 0);
            Animate(0f);
        }

        /// <summary>
        /// 바닥 밖 조각 하나의 낙하를 시작한다. 조각 번호로 정한 값이라 모든 클라이언트에서 같은 모습으로 따로따로 떨어진다.
        /// </summary>
        void BeginDrop(int i)
        {
            drops[i] = BladeDrop.Falling;
            dropSpeeds[i] = 0f;
            // 철망은 비균일 배율 부모(Barrier) 아래라 구르면 찌그러진다. 배율 없는 관문 아래로 옮겨 굴린다.
            if (pieces[i].parent != transform) pieces[i].SetParent(transform, true);
            tumbleCenters[i] = pieces[i].InverseTransformPoint(Center(i));
            dropDelays[i] = Noise(i, 1) * dropStagger;
            dropGravityScales[i] = Mathf.Lerp(0.8f, 1.2f, Noise(i, 2));
            float outward = Mathf.Sign(Vector3.Dot(Center(i) - transform.position, transform.right));
            dropDrifts[i] = transform.right * (outward * Mathf.Lerp(0.5f, 2f, Noise(i, 3)))
                + transform.forward * Mathf.Lerp(-1.5f, 1.5f, Noise(i, 4));
            tumbleAxes[i] = new Vector3(Noise(i, 5) - 0.5f, Noise(i, 6) - 0.5f, Noise(i, 7) - 0.5f).normalized;
            if (tumbleAxes[i] == Vector3.zero) tumbleAxes[i] = transform.forward;
            tumbleSpeeds[i] = Mathf.Lerp(90f, 360f, Noise(i, 8)) * (Noise(i, 9) < 0.5f ? -1f : 1f);
        }

        static float Noise(int index, int salt)
        {
            float value = Mathf.Sin(index * 12.9898f + salt * 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        /// <summary>조각 중심 바로 아래, 관문 바닥 높이에 지상 바닥이 있는지. 관문 자신의 충돌 벽은 제외한다.</summary>
        bool HasFloorBelow(int piece)
        {
            Vector3 center = Center(piece);
            Vector3 origin = new Vector3(center.x, transform.position.y + 0.5f, center.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, floorHits, 1f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (floorHits[i].transform != null && !floorHits[i].transform.IsChildOf(transform)) return true;
            return false;
        }

        Vector3 Center(int piece)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (var visual in pieceRenderers[piece])
            {
                if (visual == null) continue;
                if (!found) { bounds = visual.bounds; found = true; }
                else bounds.Encapsulate(visual.bounds);
            }
            return found ? bounds.center : pieces[piece].position;
        }

        bool BelowCourse(Vector3 position) => HumanCannonRouteSystem.Current != null
            ? HumanCannonRouteSystem.IsBelowCourse(position)
            : position.y < transform.position.y - FallbackDropDepth;

        int Count(BladeDrop state, bool blade)
        {
            int count = 0;
            for (int i = 0; drops != null && i < drops.Length; i++)
                if (drops[i] == state && (i < blades.Length) == blade) count++;
            return count;
        }

        /// <summary>문짝 묶음(톱 묶음 밖)에 바로 붙은 철망·위 파이프 조각을 모은다.</summary>
        void Fences(Transform part, List<Transform> found)
        {
            if (part == null || (sawVisual != null && part.IsChildOf(sawVisual))) return;
            foreach (Transform item in part) found.Add(item);
        }

        float InnerEdge(Transform[] parts, float sign)
        {
            Vector3 right = transform.right;
            float edge = 0f;
            foreach (var part in parts)
            {
                if (part == null) continue;
                foreach (var item in part.GetComponentsInChildren<Renderer>())
                    if (item is not ParticleSystemRenderer) edge = Inner(edge, item.bounds, right, sign);
                foreach (var item in part.GetComponentsInChildren<Collider>()) edge = Inner(edge, item.bounds, right, sign);
            }
            return edge;
        }

        float Inner(float edge, Bounds bounds, Vector3 right, float sign)
        {
            float half = Mathf.Abs(right.x) * bounds.extents.x + Mathf.Abs(right.y) * bounds.extents.y + Mathf.Abs(right.z) * bounds.extents.z;
            float value = Vector3.Dot(bounds.center - transform.position, right) + sign * half;
            return sign > 0f ? Mathf.Max(edge, value) : Mathf.Min(edge, value);
        }

        static bool Contains(Transform[] parts, Transform item)
        {
            foreach (var part in parts) if (part != null && item.IsChildOf(part)) return true;
            return false;
        }

        static Vector3[] BasePositions(Transform[] parts)
        {
            var positions = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++) if (parts[i] != null) positions[i] = parts[i].localPosition;
            return positions;
        }

        Vector3 Shake(float phase) => transform.right * Mathf.Sin(shakeTime * 53f + phase)
            + transform.up * (0.5f * Mathf.Sin(shakeTime * 71f + phase * 2f))
            + transform.forward * (0.7f * Mathf.Sin(shakeTime * 37f + phase * 3f));

        void Place(Transform[] parts, Vector3[] positions, Vector3 world)
        {
            if (positions == null) return;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part == null) continue;
                part.localPosition = positions[i] + (part.parent != null ? part.parent.InverseTransformVector(world) : world);
            }
        }
    }
}
