using UnityEngine;
namespace CurtainCall.Presentation
{
    public sealed class PerformanceSpotlightFixture : MonoBehaviour
    {
        [Tooltip("천장 고정 등기구의 회전 헤드. 위치는 움직이지 않습니다.")] public Transform head;
        [Tooltip("렌즈 위치의 실제 Spot Light.")] public Light spot;
        [Tooltip("광원에서 시작하는 좁은 빛기둥.")] public MeshRenderer beam;
        [Tooltip("등기구 렌즈 발광 표면.")] public Renderer lens;
        [System.NonSerialized] public float fade;
    }
}
