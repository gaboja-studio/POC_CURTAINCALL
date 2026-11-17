using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    [CreateAssetMenu(menuName = "CurtainCall/UI/World Indicator Style", fileName = "WorldIndicatorStyle")]
    public sealed class WorldIndicatorStyle : ScriptableObject
    {
        [Tooltip("독립 레이어 원본: 원 테두리/채움, 사각 테두리/채움, 빗금, 대시, 화살촉, 진행 트랙, 표적.")]
        public Sprite[] layers;
        public Material stroke, circleFill, rectangleFill, arrow, target;
    }
}
