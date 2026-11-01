using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 소지 칸을 골랐을 때 보이는 테두리. CommonMenuUI 씬의 슬롯 템플릿(00_SlotTemplate) 아래에 배치하고
    /// 그림·두께·색은 씬에서 고친다. 게임 중 슬롯은 템플릿 복제본이라 같은 테두리를 갖고, CommonInventoryUI가 켜고 끈다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class InventorySlotSelectionBorder : MonoBehaviour
    {
        [SerializeField] Image image;

        void Reset() => image = GetComponent<Image>();

        public bool Visible => image != null && image.enabled;

        public void Show(bool visible)
        {
            if (image != null) image.enabled = visible;
        }
    }
}
