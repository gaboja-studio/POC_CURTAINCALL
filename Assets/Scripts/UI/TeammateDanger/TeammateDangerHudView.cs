using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.UI.TeammateDanger
{
    /// <summary>
    /// 위험한 동료 표시 화면(내 화면 전용). 프리팹에는 원형 아이콘 한 벌을 보이는 상태로 두고, 더 필요하면 복제한다.
    /// </summary>
    public sealed class TeammateDangerHudView : MonoBehaviour
    {
        [SerializeField] RectTransform area;
        [Tooltip("복제 원본. 프리팹에서는 보이는 상태로 둔다(실행 중에는 쓰지 않을 때 숨김).")]
        [SerializeField] TeammateDangerBadge template;
        readonly List<TeammateDangerBadge> badges = new();

        public RectTransform Area => area != null ? area : (RectTransform)transform;

        void Awake()
        {
            if (template != null) { badges.Add(template); template.Hide(); }
        }

        public TeammateDangerBadge Badge(int index)
        {
            while (badges.Count <= index && template != null)
            {
                var copy = Instantiate(template, template.transform.parent, false);
                copy.name = template.name + " " + badges.Count;
                badges.Add(copy);
            }
            return index < badges.Count ? badges[index] : null;
        }

        public void HideFrom(int index)
        {
            for (int i = index; i < badges.Count; i++) if (badges[i] != null) badges[i].Hide();
        }
    }
}
