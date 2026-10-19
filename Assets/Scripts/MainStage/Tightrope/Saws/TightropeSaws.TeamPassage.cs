using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope.Saws
{
    public sealed partial class TightropeSaws
    {
        const string TeamPassageMessage = "CurtainCall.Tightrope.Saws.TeamPassage";
        readonly List<TeamPassageWindow> teamPassages = new();

        readonly struct TeamPassageWindow
        {
            public readonly float Start, End, First, Last, PlateDistance;
            public readonly int Lane;
            public TeamPassageWindow(float start, float end, float first, float last, int lane, float plateDistance)
            { Start = start; End = end; First = first; Last = last; Lane = lane; PlateDistance = plateDistance; }
            public bool Contains(float distance) => distance >= First && distance <= Last;
            public bool IsOpen(float time) => time >= Start && time < End;
        }

        /// <summary>살아 있는 동료가 뒤 발판을 밟아 주변 톱을 멈춘 동안만 true.</summary>
        public bool IsTeamPassageOpen => TeamPassageRemaining > 0f;
        public float TeamPassageRemaining => teamPassages.Count == 0 || !isActiveAndEnabled || run == null || run.State != TightropeRunState.Running
            ? 0f : Mathf.Max(0f, teamPassages[^1].End - GameElapsed);
        public float TeamPassageStartDistance => IsTeamPassageOpen ? teamPassages[^1].First : float.NaN;
        public float TeamPassageEndDistance => IsTeamPassageOpen ? teamPassages[^1].Last : float.NaN;
        public bool IsTeamPassageWarning => IsTeamPassageOpen && TeamPassageRemaining <= settings.teamPassageWarning;

        /// <summary>이 톱만 일시적으로 판정을 끈다. 나머지 구간·화재·추락은 그대로 유지한다.</summary>
        public bool IsHorizontalSafeForTeamPassage(int index)
        {
            if (index < 0 || index >= settings.HorizontalCount || !isActiveAndEnabled || run == null || run.State != TightropeRunState.Running) return false;
            float distance = settings.GetHorizontalDistance(index), elapsed = GameElapsed;
            foreach (var passage in teamPassages) if (passage.IsOpen(elapsed) && passage.Contains(distance)) return true;
            return false;
        }

        void ServerOpenTeamPassage()
        {
            if (session == null || !session.IsHost || run == null || run.State != TightropeRunState.Running
                || !verticalActive || settings.teamPassageDuration <= 0f) return;
            float plateDistance = VerticalDistance + settings.plateOffset;
            OpenTeamPassageAt(verticalLane, plateDistance);
        }

        void OpenTeamPassageAt(int lane, float plateDistance)
        {
            if (settings.teamPassageDuration <= 0f) return;
            var passage = new TeamPassageWindow(GameElapsed, GameElapsed + settings.teamPassageDuration,
                Mathf.Max(0f, plateDistance - settings.teamPassageHalfLength),
                Mathf.Min(course.FinishDistance, plateDistance + settings.teamPassageHalfLength), lane, plateDistance);
            teamPassages.Add(passage);
            PauseTracks(passage);
            var manager = Messaging;
            if (manager == null) return;
            using var writer = WriteTeamPassages();
            manager.SendNamedMessageToAll(TeamPassageMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        void PauseTracks(TeamPassageWindow passage)
        {
            for (int i = 0; i < tracks.Length; i++)
                if (passage.Contains(settings.GetHorizontalDistance(i))) tracks[i].Pause(passage.Start, passage.End - passage.Start);
        }

        void RebuildHorizontalTracks()
        {
            foreach (var track in tracks) track.Clear();
            // 두 메시지의 도착 순서와 무관하게 같은 이동 위상을 재구성한다.
            foreach (var passage in teamPassages) PauseTracks(passage);
            foreach (var (action, first, last, at) in horizontalCommands)
                for (int i = Mathf.Max(0, first - 1); i < Mathf.Min(last, tracks.Length); i++)
                    if (action == StepAction.StartHorizontal) tracks[i].Start(at);
                    else tracks[i].Stop(at);
        }

        void ClearTeamPassages()
        {
            teamPassages.Clear();
            RebuildHorizontalTracks();
        }

        float TeamPassageLowerOffset(int index)
        {
            if (!IsTeamPassageOpen) return 0f;
            float offset = 0f, elapsed = GameElapsed;
            foreach (var passage in teamPassages)
            {
                if (!passage.IsOpen(elapsed) || !passage.Contains(settings.GetHorizontalDistance(index))) continue;
                float down = Mathf.Clamp01((elapsed - passage.Start) / Mathf.Max(0.1f, settings.verticalLowerDuration));
                float up = Mathf.Clamp01((passage.End - elapsed) / Mathf.Max(0.1f, settings.teamPassageWarning));
                offset = Mathf.Max(offset, Mathf.Min(down, up) * settings.verticalLowerDepth);
            }
            return offset;
        }

        void UpdateTeamPassagePlate(bool hasVertical, Quaternion rotation)
        {
            plateView.gameObject.SetActive(hasVertical || IsTeamPassageOpen);
            if (hasVertical || !IsTeamPassageOpen) return;
            var passage = teamPassages[^1];
            plateView.SetPositionAndRotation(course.GetLanePosition(passage.Lane, passage.PlateDistance) + Vector3.up * 0.02f, rotation);
            Color color = IsTeamPassageWarning ? plateColor : platePressedColor;
            foreach (var material in plateMaterials) if (material != null) material.color = color;
        }

        FastBufferWriter WriteTeamPassages()
        {
            var writer = new FastBufferWriter(8 + teamPassages.Count * 24, Allocator.Temp, 64 * 1024);
            writer.WriteValueSafe(run == null ? -1 : run.Round);
            writer.WriteValueSafe(teamPassages.Count);
            foreach (var passage in teamPassages)
            {
                writer.WriteValueSafe(passage.Start);
                writer.WriteValueSafe(passage.End);
                writer.WriteValueSafe(passage.First);
                writer.WriteValueSafe(passage.Last);
                writer.WriteValueSafe(passage.Lane);
                writer.WriteValueSafe(passage.PlateDistance);
            }
            return writer;
        }

        void HandleTeamPassageMessage(ulong senderId, FastBufferReader reader)
        {
            if (senderId != NetworkManager.ServerClientId || run == null || !reader.TryBeginRead(8)) return;
            reader.ReadValueSafe(out int round);
            reader.ReadValueSafe(out int count);
            if (round != run.Round || count < 0 || count > 256 || !reader.TryBeginRead(count * 24)) return;
            var received = new List<TeamPassageWindow>(count);
            float previousStart = -1f;
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out float start);
                reader.ReadValueSafe(out float end);
                reader.ReadValueSafe(out float first);
                reader.ReadValueSafe(out float last);
                reader.ReadValueSafe(out int lane);
                reader.ReadValueSafe(out float plateDistance);
                if (!float.IsFinite(start) || !float.IsFinite(end) || !float.IsFinite(first) || !float.IsFinite(last)
                    || !float.IsFinite(plateDistance) || start < previousStart || end <= start || first < 0f || last < first
                    || course == null || last > course.FinishDistance || lane < 0 || lane >= course.LaneCount) return;
                received.Add(new TeamPassageWindow(start, end, first, last, lane, plateDistance));
                previousStart = start;
            }
            teamPassages.Clear();
            teamPassages.AddRange(received);
            RebuildHorizontalTracks();
        }
    }
}
