using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.DummySpaces
{
    /// <summary>
    /// 3D 더미 공간 안의 플레이어 자리 토큰. 진행값(도착·구역·행)을 공간의 시작~끝 지점 사이 위치로 보여 준다.
    /// 실제 플레이어·물리·결과 판정은 소유하지 않는다.
    /// </summary>
    public sealed class DummySpacePreview : MonoBehaviour
    {
        public Transform[] tokens;
        [Tooltip("토큰별 시작 지점(진행 0). 비어 있으면 일렬 배치")]
        public Transform[] starts;
        [Tooltip("토큰별 끝 지점(진행 1)")]
        public Transform[] ends;
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
                if (starts != null && ends != null && i < starts.Length && i < ends.Length && starts[i] != null && ends[i] != null)
                    tokens[i].position = Vector3.Lerp(starts[i].position, ends[i].position, progress);
                else tokens[i].localPosition = new Vector3((i - 1.5f) * 1.4f, 0.65f, progress * 2f);
                if (tokenRenderers[i].sharedMaterial != material) tokenRenderers[i].sharedMaterial = material;
            }
        }
    }
}
