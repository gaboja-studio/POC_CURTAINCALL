using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    public sealed partial class TightropeSaws
    {
        sealed class ReleasedGate
        {
            public int Index, Lane;
            public double At;
            public float InitialLift;
            public Transform View, Blade;
            public MeshCollider Collider;
            public LineRenderer Path;
        }

        // 해결한 관문과 다음 관문은 독립적이다. 이전 톱이 이동 중이어도 다음 관문이 정상 등장한다.
        readonly List<ReleasedGate> releasedGates = new();
        public int ReleasedGateCount
        {
            get { int count = 0; foreach (var gate in releasedGates) if (TryReleasedPosition(gate, out _)) count++; return count; }
        }

        public bool TryGetReleasedGatePosition(int index, out Vector3 position)
        {
            foreach (var gate in releasedGates)
                if (gate.Index == index) return TryReleasedPosition(gate, out position);
            position = default; return false;
        }

        void ReleaseCooperativeGate()
        {
            foreach (var gate in releasedGates) if (gate.Index == cooperativeGateIndex) return;
            releasedGates.Add(new ReleasedGate { Index = cooperativeGateIndex, Lane = cooperativeGateLane,
                At = CooperativeGateClock, InitialLift = CooperativeGateLiftOffset });
        }

        bool TryReleasedPosition(ReleasedGate gate, out Vector3 position)
        {
            position = default;
            if (course == null || !isActiveAndEnabled || run == null || run.State != TightropeRunState.Running
                || gate.Index < 0 || gate.Index >= (settings.cooperativeGateDistances?.Length ?? 0)) return false;
            Vector2 motion = CooperativeGateHop.Sample((float)(CooperativeGateClock - gate.At),
                settings.cooperativeGateHopDuration, settings.cooperativeGateHopDistance,
                GameSettings.Base.Character.CapsuleHeight * settings.cooperativeGateHopHeightMultiplier,
                settings.cooperativeGateApproachSpeed, gate.InitialLift);
            float distance = settings.cooperativeGateDistances[gate.Index] + motion.x;
            if (distance < 0f || distance >= course.FinishDistance || !course.IsLaneUsable(gate.Lane, distance)) return false;
            position = IncomingPosition(gate.Lane, distance, settings.cooperativeGateDistances[gate.Index], motion.y, gate.Index);
            return true;
        }

        void ServerJudgeReleasedGates(NetworkPlayer player, in BodyCapsule body)
        {
            foreach (var gate in releasedGates)
                if (TryReleasedPosition(gate, out var bottom)
                    && CooperativeGateGeometry.TryGetContact(body, bottom, course.Right,
                        settings.cooperativeGateHeight, settings.cooperativeGateThickness, out var contact))
                    ServerTouch(player, body, SawKind.CooperativeGate, -100000 - gate.Index, gate.Index, contact);
        }

        void UpdateReleasedGateViews()
        {
            foreach (var gate in releasedGates)
            {
                bool visible = TryReleasedPosition(gate, out var bottom);
                if (visible && gate.View == null)
                    gate.View = BuildGateDisc(transform.Find(GeneratedName), $"ReleasedGate{gate.Index}", out gate.Collider, out gate.Blade);
                if (gate.View == null) continue;
                gate.View.gameObject.SetActive(visible);
                gate.Collider.enabled = visible;
                if (!visible) continue;
                gate.View.SetPositionAndRotation(bottom + Vector3.up * settings.cooperativeGateHeight * .5f,
                    Quaternion.LookRotation(course.Forward, Vector3.up));
                gate.Blade.localRotation = Quaternion.AngleAxis(-Time.time * settings.spinSpeed, Vector3.right) * Quaternion.Euler(0f, 0f, -90f);
            }
        }

        Transform BuildGateDisc(Transform parent, string name, out MeshCollider collider, out Transform blade)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            float diameter = settings.cooperativeGateHeight, depth = settings.cooperativeGateThickness;
            blade = CreateSaw(root, "IncomingSaw", cooperativeGatePrefab, diameter, depth, SawKind.Vertical);
            var collision = new GameObject("DiscCollision").transform;
            collision.SetParent(root, false);
            collision.localRotation = Quaternion.Euler(0f, 0f, -90f);
            collision.localScale = new Vector3(diameter, depth * .5f, diameter);
            collider = collision.gameObject.AddComponent<MeshCollider>();
            // Collision stays independent of the authored visual's hierarchy.
            collider.sharedMesh = TightropeArtFit.UnitCylinder;
            collider.convex = true;
            return root;
        }

        void ClearReleasedGates()
        {
            foreach (var gate in releasedGates) RemoveReleasedView(gate);
            releasedGates.Clear();
        }

        static void RemoveReleasedView(ReleasedGate gate)
        {
            if (gate.Path != null) Destroy(gate.Path.gameObject);
            if (gate.View == null) return;
            gate.View.gameObject.SetActive(false);
            Destroy(gate.View.gameObject);
        }

        void SetReleasedGates(List<ReleasedGate> next)
        {
            for (int i = 0; i < next.Count; i++)
                foreach (var old in releasedGates)
                    if (old.Index == next[i].Index && old.Lane == next[i].Lane
                        && old.At == next[i].At && old.InitialLift == next[i].InitialLift) { next[i] = old; break; }
            foreach (var old in releasedGates) if (!next.Contains(old)) RemoveReleasedView(old);
            releasedGates.Clear(); releasedGates.AddRange(next);
        }
    }
}
