using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPole
    {
        Transform commonVisual;
        GameObject shownVisual;
        public bool PresentationVisible { get; set; } = true;

        void EnsureCommonVisual()
        {
            if (!Application.isPlaying) return;
            var source = Rules.Visual(IsSwitch || linkedGate != null);
            if (source == null || shownVisual == source) return;
            DisableVisual(visualBar); DisableVisual(leftCable); DisableVisual(rightCable);
            if (commonVisual != null) { commonVisual.gameObject.SetActive(false); Destroy(commonVisual.gameObject); }
            var instance = Instantiate(source, Grip.position, Grip.rotation);
            instance.name = "Common pole visual";
            instance.transform.SetParent(transform, true);
            commonVisual = instance.transform;
            shownVisual = source;
            visualBar = commonVisual.Find("VisualBar");
            leftCable = commonVisual.Find("LeftCable"); rightCable = commonVisual.Find("RightCable");
            leftAnchor = commonVisual.Find("LeftAnchor"); rightAnchor = commonVisual.Find("RightAnchor");
            // 원본 인간대포 외형의 보조 케이블을 중복 표시하지 않는다. 운동 케이블은 위의 두 개를 사용한다.
            foreach (string name in new[] { "LeftSwingCable", "RightSwingCable" })
            { var duplicate = commonVisual.Find(name); if (duplicate != null) duplicate.gameObject.SetActive(false); }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
        static void DisableVisual(Transform part)
        {
            if (part == null) return;
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var collider in part.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
    }
}
