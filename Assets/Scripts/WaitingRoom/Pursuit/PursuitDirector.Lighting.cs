using System.Collections.Generic;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>방 깜빡임 이벤트는 호스트가 고르고 스냅샷으로 전송한다. 깜빡임 모양은 각 클라이언트가 같은 시계로 계산한다.</summary>
    public sealed partial class PursuitDirector
    {
        /// <summary>마지막으로 고른 방 깜빡임 방 인덱스(Rooms 기준)와 시작 경과 시간. 없으면 -1.</summary>
        public int RoomFlickerIndex { get; private set; } = -1;
        public float RoomFlickerAt { get; private set; } = -1;
        /// <summary>호스트/클라이언트 공통 시드. 같은 런·공연이면 모든 클라이언트가 같은 지직임을 본다.</summary>
        public int LightSeed => generation * 7919 + day * 131 + performance;
        float nextRoomFlicker = -1;
        readonly List<int> flickerRooms = new();

        void ResetLighting()
        {
            RoomFlickerIndex = -1; RoomFlickerAt = -1; nextRoomFlicker = -1;
            if (Rooms != null) foreach (var room in Rooms) if (room != null) room.ClearFlicker();
        }
        void TickLighting()
        {
            var lighting = GameSettings.WaitingRoom.Pursuit.Lighting;
            var rule = lighting != null && lighting.Enabled ? lighting.StageFor(CurrentStageIndex) : null;
            if (rule == null || rule.RoomFlickerInterval <= 0) { nextRoomFlicker = -1; return; }
            if (nextRoomFlicker < 0) nextRoomFlicker = Elapsed + Random.Range(.35f, 1f) * rule.RoomFlickerInterval;
            if (Elapsed < nextRoomFlicker) return;
            nextRoomFlicker = Elapsed + Mathf.Max(rule.RoomFlickerSeconds + 1,
                rule.RoomFlickerInterval + Random.Range(-rule.RoomFlickerJitter, rule.RoomFlickerJitter));
            int room = PickFlickerRoom(rule.NeighborDistance);
            if (room < 0) return;
            RoomFlickerIndex = room; RoomFlickerAt = Elapsed;
        }
        /// <summary>살아 있는 플레이어가 있는 방과 그 근처 방 중 하나. 포획 중인 방은 제외한다.</summary>
        int PickFlickerRoom(float neighbor)
        {
            flickerRooms.Clear();
            if (Rooms == null) return -1;
            var occupied = new List<Bounds>();
            foreach (var player in candidates) {
                if (!IsAvailable(player)) continue;
                for (int i = 0; i < Rooms.Length; i++)
                    if (Rooms[i] != null && Rooms[i].Contains(player.transform.position)) occupied.Add(Rooms[i].Area.bounds);
            }
            for (int i = 0; i < Rooms.Length; i++) {
                var room = Rooms[i];
                if (room == null || room.Area == null || room.CaptureFlickering || room.Lights == null || room.Lights.Length == 0) continue;
                var bounds = room.Area.bounds; bounds.Expand(neighbor * 2);
                foreach (var b in occupied) if (bounds.Intersects(b)) { flickerRooms.Add(i); break; }
            }
            return flickerRooms.Count == 0 ? -1 : flickerRooms[Random.Range(0, flickerRooms.Count)];
        }
        /// <summary>개발 미리보기: 지정 방(없으면 자동 선택)을 바로 깜빡이고 전송한다.</summary>
        public bool DebugRoomFlicker(int room = -1)
        {
            if (!CanDebug || !active) return false;
            var rule = GameSettings.WaitingRoom.Pursuit.Lighting.StageFor(CurrentStageIndex);
            if (room < 0) room = PickFlickerRoom(rule != null ? rule.NeighborDistance : 4);
            if (Rooms == null || room < 0 || room >= Rooms.Length) return false;
            RoomFlickerIndex = room; RoomFlickerAt = Elapsed; Broadcast();
            return true;
        }
        /// <summary>개발 미리보기: 포획 암전만 로컬에서 재생한다(포획 판정 없음).</summary>
        public bool DebugCaptureBlackout(int room, float seconds)
        {
            if (!CanDebug || Rooms == null || room < 0 || room >= Rooms.Length || Rooms[room] == null) return false;
            Rooms[room].Flicker(Time.time + Mathf.Max(.2f, seconds));
            return true;
        }
        void ReceiveLighting(int room, float at)
        {
            RoomFlickerIndex = Rooms != null && room >= 0 && room < Rooms.Length ? room : -1;
            RoomFlickerAt = RoomFlickerIndex >= 0 ? at : -1;
        }
    }
}
