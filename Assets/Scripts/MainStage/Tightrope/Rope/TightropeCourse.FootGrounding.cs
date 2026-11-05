using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCourse
    {
        // CharacterController는 skin 두께만큼 바닥 위에 떠서 멈춘다. 외줄타기 코스에서만 보이는 모델을
        // 그만큼 내려 플랫폼·줄에 발이 닿게 한다(2026-11-05 사용자 요청). 충돌체·이동·판정 위치는 그대로다.
        // 탈출로처럼 장면 지형을 쓰는 코스는 대상이 아니다.
        readonly Dictionary<PlayerModelSlot, float> groundedSlots = new();
        readonly List<PlayerModelSlot> releasedSlots = new();

        void GroundPlayerModels()
        {
            if (useSceneGeometry) return;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.TryGetComponent(out PlayerModelSlot modelSlot)
                    || !player.TryGetComponent(out CharacterController body)) continue;
                var slot = modelSlot.Slot;
                if (!groundedSlots.TryGetValue(modelSlot, out float original))
                    groundedSlots[modelSlot] = original = slot.localPosition.y;
                Vector3 position = slot.localPosition;
                position.y = original - body.skinWidth;
                slot.localPosition = position;
            }
            // 나간 플레이어는 목록에서만 뺀다.
            releasedSlots.Clear();
            foreach (var pair in groundedSlots) if (pair.Key == null) releasedSlots.Add(pair.Key);
            foreach (var slot in releasedSlots) groundedSlots.Remove(slot);
        }

        void ReleaseModelGrounding()
        {
            foreach (var pair in groundedSlots)
            {
                if (pair.Key == null) continue;
                var slot = pair.Key.Slot;
                Vector3 position = slot.localPosition;
                position.y = pair.Value;
                slot.localPosition = position;
            }
            groundedSlots.Clear();
        }
    }
}
