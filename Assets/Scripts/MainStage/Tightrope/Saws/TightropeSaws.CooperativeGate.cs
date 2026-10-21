using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope.Fire;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    public sealed partial class TightropeSaws
    {
        int cooperativeGateIndex = -1, cooperativeGateLane = TightropeCourse.NoLane;
        bool cooperativeGateActive;
        float cooperativeGateStartedAt, cooperativeGateOpenedAt = -1f;
        double cooperativeGateClockStartedAt;
        Transform cooperativeGateView, cooperativePlateRoot;
        MeshCollider cooperativeGateCollider;
        Transform cooperativeBlade;
        Transform[] cooperativePlates = Array.Empty<Transform>();
        Transform[] cooperativeLaunchMarkers = Array.Empty<Transform>();

        public bool IsCooperativeGateActive => settings.cooperativeGates && cooperativeGateActive && isActiveAndEnabled;
        public int CooperativeGateIndex => cooperativeGateIndex;
        public int CooperativeGateLane => cooperativeGateLane;
        public float CooperativeGateStopDistance => ValidCooperativeGateIndex ? settings.cooperativeGateDistances[cooperativeGateIndex] : float.NaN;
        public float CooperativeGateDistance => !ValidCooperativeGateIndex ? float.NaN : CooperativeGateStopDistance
            + Mathf.Max(0f, settings.cooperativeGateApproachDistance - Mathf.Max(0f, GameElapsed - cooperativeGateStartedAt) * settings.cooperativeGateApproachSpeed);
        public bool IsCooperativeGateStopped => IsCooperativeGateActive && CooperativeGateLiftOffset <= .001f
            && CooperativeGateDistance <= CooperativeGateStopDistance + .001f;
        public bool IsCooperativeGateOpening => IsCooperativeGateActive && cooperativeGateOpenedAt >= 0f;
        // 기존 표시 코드의 호환 별칭. 협동 관문은 실제로 위로 열린다.
        public bool IsCooperativeGateLowering => IsCooperativeGateOpening;
        public bool IsCooperativeGatePassable => !IsCooperativeGateActive
            || cooperativeGateOpenedAt >= 0f
            || CooperativeGateLiftOffset >= GameSettings.Base.Character.CapsuleHeight + .25f;
        public float CooperativePlateDistance => CooperativeGateDistance + settings.cooperativeGatePlateOffset;
        public bool IsFinaleSoloGate => settings.finaleSoloAutoOpen && !IsRequiredFinaleIndex(cooperativeGateIndex)
            && course != null && FinaleGateCycle.Applies(cooperativeGateIndex,
            settings.cooperativeGateDistances, course.FinishDistance, settings.finaleSoloStartFraction);
        public float FinaleGateUntilOpen => FinalePhase.UntilOpen;
        public float FinaleGateOpenRemaining => FinalePhase.OpenRemaining;
        public bool IsFinaleGateClosing => FinalePhase.Closing;
        double CooperativeGateClock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
        FinaleGateCycle.Phase FinalePhase => IsFinaleSoloGate ? FinaleGateCycle.Sample(
            (float)(CooperativeGateClock - cooperativeGateClockStartedAt)
                - settings.cooperativeGateApproachDistance / Mathf.Max(.1f, settings.cooperativeGateApproachSpeed),
            settings.finaleGateClosedSeconds, settings.finaleGateOpenSeconds, settings.cooperativeGateOpenDuration) : default;
        public float CooperativeJumpLaunchDistance => CooperativeGateStopDistance
            - Mathf.Max(2.2f, settings.cooperativeGateHeight * .5f + GameSettings.Base.Character.CapsuleRadius
                + settings.cooperativeGateLaunchTolerance + .25f);

        bool ValidCooperativeGateIndex => settings.cooperativeGateDistances != null && cooperativeGateIndex >= 0
            && cooperativeGateIndex < settings.cooperativeGateDistances.Length;
        float CooperativeGateLiftOffset
        {
            get
            {
                float height = Mathf.Max(settings.cooperativeGateHeight + .4f, GameSettings.Base.Character.CapsuleHeight + .5f);
                if (cooperativeGateOpenedAt < 0f) return IsFinaleSoloGate ? height * FinalePhase.Lift : 0f;
                float t = Mathf.Clamp01((GameElapsed - cooperativeGateOpenedAt) / Mathf.Max(.01f, settings.cooperativeGateOpenDuration));
                return height * (2f * t - t * t); // 위로 튀어 정점에서 멈추는 포물선 상승 구간.
            }
        }
        Vector3 CooperativeGateBase => IncomingPosition(cooperativeGateLane, CooperativeGateDistance,
            CooperativeGateStopDistance, CooperativeGateLiftOffset, cooperativeGateIndex);

        public bool TryGetCooperativePlatePosition(int lane, out Vector3 position)
        {
            position = default;
            if (!IsCooperativeGateActive || course == null || lane != cooperativeGateLane || !course.IsLaneUsable(lane, CooperativePlateDistance)) return false;
            position = course.GetLanePosition(lane, CooperativePlateDistance);
            return true;
        }

        public bool TryGetCooperativeGateApproachPosition(int lane, out Vector3 position)
        {
            position = default;
            if (!IsCooperativeGateActive || course == null || lane != cooperativeGateLane || !course.IsLaneUsable(lane, CooperativeJumpLaunchDistance)) return false;
            position = course.GetLanePosition(lane, CooperativeJumpLaunchDistance);
            return true;
        }

        /// <summary>정지 관문의 표시된 출발선에 있는 실제 목마 최상층만 시작 궤적을 보정한다.</summary>
        public bool TryGetCooperativeLaunch(NetworkPlayer player, out float jumpHeight, out float alongSpeed)
        {
            jumpHeight = alongSpeed = 0f;
            var stack = PiggybackSystem.Current;
            if (!IsCooperativeGateStopped || course == null || !course.IsGameplayActive || stack == null
                || player == null || !player.IsSpawned || (player.State != PlayerState.Normal || (run != null && !run.IsParticipantReady(player))) || player.IsAirborne
                || !player.TryGetComponent(out PlayerMover mover) || mover.IsAirborne || mover.FreeMovement
                || mover.JumpHeightMultiplier <= 0f || stack.GetAbove(player) != null
                || !IsApprovedCooperativeLaunch(player, stack)) return false;
            var bottom = stack.GetBelow(player);
            if (bottom == null) return false;
            while (stack.GetBelow(bottom) is { } next) bottom = next;
            if (!bottom.IsSpawned || bottom.State != PlayerState.Normal || bottom.IsAirborne
                || !bottom.TryGetComponent(out PlayerMover bottomMover) || (bottom.IsOwner && bottomMover.IsAirborne)
                || !course.TryGetSupportingRope(bottomMover, out int lane)
                || !TryGetCooperativeGateApproachPosition(lane, out var approach)) return false;
            Vector3 offset = player.transform.position - approach;
            if (Mathf.Abs(Vector3.Dot(offset, course.Forward)) > settings.cooperativeGateLaunchTolerance
                || Mathf.Abs(Vector3.Dot(offset, course.Right)) > settings.plateWidth * .5f) return false;
            float gravity = mover.JumpGravity;
            float normalHeight = mover.JumpSpeed * mover.JumpSpeed / (2f * gravity);
            float landing = CooperativeLandingDistance(player);
            if (!course.IsLaneUsable(lane, landing)) return false;
            return CooperativeGateGeometry.TryCalculateLaunch(player.transform.position.y - course.transform.position.y,
                normalHeight, gravity, CooperativeGateStopDistance - course.GetDistance(player.transform.position),
                mover.BodyRadius, landing - CooperativeGateStopDistance, settings, out jumpHeight, out alongSpeed);
        }

        void ResetCooperativeGates()
        {
            ClearReleasedGates();
            ResetCooperativeAdmission();
            cooperativeGateRevision = receivedCooperativeGateRevision = 0;
            cooperativeGateIndex = -1;
            cooperativeGateLane = TightropeCourse.NoLane;
            cooperativeGateActive = false;
            cooperativeGateStartedAt = 0f;
            cooperativeGateClockStartedAt = 0d;
            cooperativeGateOpenedAt = -1f;
            if (cooperativeGateCollider != null) cooperativeGateCollider.enabled = false;
            if (cooperativeGateView != null) cooperativeGateView.gameObject.SetActive(false);
            if (cooperativePlateRoot != null) cooperativePlateRoot.gameObject.SetActive(false);
        }

        void ServerUpdateCooperativeGate()
        {
            if (!settings.cooperativeGates || settings.cooperativeGateDistances == null) return;
            if (!cooperativeGateActive)
            {
                int next = cooperativeGateIndex + 1;
                if (next >= settings.cooperativeGateDistances.Length) return;
                float distance = settings.cooperativeGateDistances[next];
                if (!float.IsFinite(distance) || distance < 0f || distance + settings.cooperativeGatePlateOffset >= course.FinishDistance) return;
                float leader = GetLeaderDistance();
                if (float.IsNaN(leader) || leader < distance - settings.cooperativeGateTriggerLead) return;
                // 소각 예정 줄은 먼저 고정하지만, 한 줄만 남았다고 속이거나 선두의 임의 줄을 선택하지 않는다.
                if (IsRequiredFinaleIndex(next)
                    && (TightropeFire.Current == null || TightropeFire.Current.SurvivingLaneAt(distance) == TightropeCourse.NoLane)) return;
                SetCooperativeGate(next, true, GameElapsed, -1f);
                BroadcastCooperativeGate();
                return;
            }
            if (cooperativeGateOpenedAt >= 0f)
            {
                if (GameElapsed - cooperativeGateOpenedAt < settings.cooperativeGateHopDuration) return;
                cooperativeGateActive = false;
                BroadcastCooperativeGate();
                return;
            }
            // 불타 사라진 줄에 보이지 않는 관문이나 발판을 남기지 않는다.
            if (!IsFourStackFinaleGate
                && (!course.IsLaneUsable(cooperativeGateLane, CooperativeGateStopDistance) || EveryonePassedCooperativeGate()))
            {
                cooperativeGateActive = false;
                BroadcastCooperativeGate();
                return;
            }
            if (ServerApproveCooperativeStack()) BroadcastCooperativeGate();
            if (CooperativeGateDistance > CooperativeGateStopDistance + .001f
                || !TryFindCooperativePlatePlayer(out var player, out int lane)) return;
            OpenTeamPassageAt(lane, CooperativePlateDistance);
            ReleaseCooperativeGate();
            cooperativeGateOpenedAt = GameElapsed;
            BroadcastCooperativeGate();
        }

        bool EveryonePassedCooperativeGate()
        {
            bool found = false;
            float rear = CooperativeGateStopDistance + settings.cooperativeGatePlateOffset
                + settings.cooperativeGatePlateLength * .5f;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || !player.isActiveAndEnabled || (player.State != PlayerState.Normal || (run != null && !run.IsParticipantReady(player)))) continue;
                found = true;
                if (course.GetDistance(player.transform.position) <= rear) return false;
            }
            return found;
        }

        bool TryFindCooperativePlatePlayer(out NetworkPlayer found, out int lane)
        {
            found = null;
            lane = TightropeCourse.NoLane;
            if (!IsCooperativeStackApproved) return false;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || !player.isActiveAndEnabled || (player.State != PlayerState.Normal || (run != null && !run.IsParticipantReady(player))) || player.IsAirborne
                    || (IsFourStackFinaleGate && CooperativeAdmittedFloor(player) == 0)
                    || !player.TryGetComponent(out PlayerMover mover) || (player.IsOwner && (mover.IsAirborne || mover.PassThrough))
                    || !course.TryGetSupportingRope(mover, out int supported) || !TryGetCooperativePlatePosition(supported, out var plate)) continue;
                Vector3 offset = player.transform.position - plate;
                if (Mathf.Abs(offset.y) > settings.plateHeightTolerance
                    || Mathf.Abs(Vector3.Dot(offset, course.Right)) > settings.plateWidth * .5f
                    || Mathf.Abs(Vector3.Dot(offset, course.Forward)) > settings.cooperativeGatePlateLength * .5f) continue;
                found = player;
                lane = supported;
                return true;
            }
            return false;
        }

        void ServerJudgeCooperativeGate(NetworkPlayer player, in BodyCapsule body)
        {
            ServerJudgeReleasedGates(player, body);
            if (!IsCooperativeGateActive || IsCooperativeGatePassable) return;
            if (CooperativeGateGeometry.TryGetContact(body, CooperativeGateBase, course.Right,
                settings.cooperativeGateHeight, settings.cooperativeGateThickness, out var contact))
                ServerTouch(player, body, SawKind.CooperativeGate, -100000 - cooperativeGateIndex, cooperativeGateIndex, contact);
        }

        void SetCooperativeGate(int index, bool active, float started, float opened)
        {
            if (index != cooperativeGateIndex) ResetCooperativeAdmission();
            if (index != cooperativeGateIndex || cooperativeGateLane == TightropeCourse.NoLane)
            {
                cooperativeGateLane = SelectCooperativeGateLane(index);
                cooperativeGateClockStartedAt = CooperativeGateClock + started - GameElapsed;
            }
            cooperativeGateIndex = index;
            cooperativeGateActive = active;
            cooperativeGateStartedAt = started;
            cooperativeGateOpenedAt = opened;
        }

        int SelectCooperativeGateLane(int index)
        {
            if (course == null || index < 0 || index >= settings.cooperativeGateDistances.Length) return TightropeCourse.NoLane;
            if (IsRequiredFinaleIndex(index))
                return TightropeFire.Current != null ? TightropeFire.Current.SurvivingLaneAt(settings.cooperativeGateDistances[index])
                    : TightropeCourse.NoLane;
            int selected = TightropeCourse.NoLane;
            float leader = float.NegativeInfinity, stop = settings.cooperativeGateDistances[index];
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || (player.State != PlayerState.Normal || (run != null && !run.IsParticipantReady(player)))) continue;
                int lane = course.Layout.GetLaneSlotAt(course.GetSide(player.transform.position));
                float distance = course.GetDistance(player.transform.position);
                if (distance <= leader || !course.IsLaneUsable(lane, stop)) continue;
                selected = lane;
                leader = distance;
            }
            if (selected != TightropeCourse.NoLane) return selected;
            for (int lane = 0; lane < course.LaneCount; lane++)
                if (course.IsLaneUsable(lane, stop)) return lane;
            return TightropeCourse.NoLane;
        }

        void BuildCooperativeGateViews(Transform root)
        {
            // 기존 정면 톱과 같은 원판·톱니·회전축. 원판 하나의 크기만 확대한다.
            cooperativeGateView = BuildGateDisc(root, "CooperativeGate", out cooperativeGateCollider, out cooperativeBlade);
            cooperativePlateRoot = new GameObject("CooperativeGatePlates").transform;
            cooperativePlateRoot.SetParent(root, false);
            cooperativePlates = new Transform[course.LaneCount];
            cooperativeLaunchMarkers = new Transform[course.LaneCount];
            for (int lane = 0; lane < cooperativePlates.Length; lane++)
            {
                cooperativePlates[lane] = cooperativePlatePrefab != null
                    ? Instantiate(cooperativePlatePrefab, cooperativePlateRoot).transform
                    : CreatePrimitive(PrimitiveType.Cube, cooperativePlateRoot, $"Plate{lane + 1}",
                        new Vector3(settings.plateWidth, .04f, settings.cooperativeGatePlateLength), plateColor, true);
                TightropeOutline.AttachToHierarchy(cooperativePlates[lane].gameObject);
                float markerLength = settings.cooperativeGateLaunchTolerance * 2f;
                cooperativeLaunchMarkers[lane] = CreatePrimitive(PrimitiveType.Cube, cooperativePlateRoot, $"Launch{lane + 1}",
                    new Vector3(settings.plateWidth, .02f, markerLength), Color.white, true);
                // 흰 테두리와 청록 띠는 표시만 한다. 실제 발밑 지지는 계속 줄이 담당한다.
                CreatePrimitive(PrimitiveType.Cube, cooperativeLaunchMarkers[lane], "ReadyStrip", new Vector3(.82f, 1.05f, .8f),
                    new Color(.04f, .85f, .75f), true).localPosition = Vector3.up * .08f;
            }
            cooperativeGateView.gameObject.SetActive(false);
            cooperativePlateRoot.gameObject.SetActive(false);
        }

        void UpdateCooperativeGateViews()
        {
            UpdateReleasedGateViews();
            if (cooperativeGateView == null) return;
            bool visible = IsCooperativeGateActive && cooperativeGateOpenedAt < 0f;
            cooperativeGateView.gameObject.SetActive(visible);
            cooperativePlateRoot.gameObject.SetActive(visible);
            cooperativeGateCollider.enabled = IsCooperativeGateActive && !IsCooperativeGatePassable;
            if (!visible) return;
            Quaternion rotation = Quaternion.LookRotation(course.Forward, Vector3.up);
            cooperativeGateView.SetPositionAndRotation(CooperativeGateBase + Vector3.up * settings.cooperativeGateHeight * .5f, rotation);
            float spin = Time.time * settings.spinSpeed;
            cooperativeBlade.localRotation = Quaternion.AngleAxis(-spin, Vector3.right) * Quaternion.Euler(0f, 0f, -90f);
            for (int lane = 0; lane < cooperativePlates.Length; lane++)
            {
                bool usable = TryGetCooperativePlatePosition(lane, out var position);
                cooperativePlates[lane].gameObject.SetActive(usable);
                if (usable) cooperativePlates[lane].SetPositionAndRotation(position + Vector3.up * .02f, rotation);
                Vector3 approach = default;
                bool approachUsable = !IsCooperativeGateOpening && TryGetCooperativeGateApproachPosition(lane, out approach);
                cooperativeLaunchMarkers[lane].gameObject.SetActive(approachUsable);
                if (approachUsable) cooperativeLaunchMarkers[lane].SetPositionAndRotation(approach + Vector3.up * .025f, rotation);
            }
        }
    }
}
