using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.DummySpaces
{
    /// <summary>더미 토큰 보조 표시. 실제 플레이어·물리·결과 판정은 소유하지 않는다.</summary>
    public sealed class DummySpacePreview : MonoBehaviour
    {
        public Transform[] tokens;
        public Renderer[] tokenRenderers;
        public Material idle;
        public Material active;
        public Material complete;
        public Material danger;

        public void Refresh(int selected, DummyPerformance performance, DummyWaitingRoom waiting, DummyExploration exploration, DummyEscapeRoute escape)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                Material material = idle;
                bool visible = false;
                float progress = 0;
                if (performance != null)
                {
                    var people = performance.Rewards != null ? performance.Rewards.State?.Segments : null;
                    visible = people != null && i < people.Count;
                    if (visible)
                    {
                        var p = people[i];
                        material = p.Dead ? danger : performance.HasArrived(p.ParticipantId) ? complete : selected == i ? active : idle;
                        progress = performance.HasArrived(p.ParticipantId) ? 1 : 0;
                    }
                }
                else if (waiting != null)
                {
                    visible = i < 4;
                    material = i < waiting.PreviewLostCount ? danger : active;
                }
                else if (exploration != null)
                {
                    visible = i == 0;
                    progress = exploration.Zone == "무대 입구" ? 1 : exploration.Zone == "백스테이지" ? 0.5f : 0;
                    material = exploration.IsSafe ? complete : danger;
                }
                else if (escape != null)
                {
                    visible = i < escape.InputRows;
                    string row = escape.RowState(i);
                    material = row.Contains("도착") ? complete : row.Contains("리타이어") ? danger : selected == i ? active : idle;
                    progress = row.Contains("도착") ? 1 : 0;
                }
                tokens[i].gameObject.SetActive(visible);
                tokens[i].localPosition = new Vector3((i - 1.5f) * 1.4f, 0.65f, progress * 2f);
                if (tokenRenderers[i].sharedMaterial != material) tokenRenderers[i].sharedMaterial = material;
            }
        }
    }
}
