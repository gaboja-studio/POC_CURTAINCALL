using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 전기로 열리는 문의 알림 조명(2026-11-13 사용자). 문이 닫혀 있으면 빨강, 열리면 초록으로 빛난다.
    /// 열림 여부는 탐색 장부를 본다(여러 대상을 "|"로 적으면 하나만 열려도 열림).
    /// </summary>
    public sealed class PowerDoorLamp : MonoBehaviour
    {
        [SerializeField] string openedBy = "";
        [SerializeField] Renderer[] lamps;
        [SerializeField] Light glow;
        [SerializeField] Color closedColor = new(1f, .12f, .08f), openColor = new(.15f, 1f, .3f);
        [Tooltip("이 대상이 열려 있으면(회로가 고장 나 문이 열린 채 굳음) 초록 대신 고장 색으로 빛난다(2026-11-14 사용자: 노란색).")]
        [SerializeField] string brokenBy = "";
        [SerializeField] Color brokenColor = new(1f, .78f, .1f);
        MaterialPropertyBlock block; int shown = -1;

        void Update()
        {
            int state = !string.IsNullOrEmpty(brokenBy) && ExplorationGate.IsAnyOpen(brokenBy) ? 2 : ExplorationGate.IsAnyOpen(openedBy) ? 1 : 0;
            if (shown == state) return;
            shown = state; var color = state == 2 ? brokenColor : state == 1 ? openColor : closedColor;
            block ??= new MaterialPropertyBlock();
            if (lamps != null) foreach (var lamp in lamps)
            {
                if (lamp == null) continue;
                lamp.GetPropertyBlock(block); block.SetColor("_BaseColor", color); block.SetColor("_EmissionColor", color * 2.5f); lamp.SetPropertyBlock(block);
            }
            if (glow != null) glow.color = color;
        }
    }
}
