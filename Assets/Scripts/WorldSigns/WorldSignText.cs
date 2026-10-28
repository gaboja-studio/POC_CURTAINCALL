using TMPro;
using UnityEngine;

namespace CurtainCall.WorldSigns
{
    [DisallowMultipleComponent]
    public sealed class WorldSignText : MonoBehaviour
    {
        public TMP_FontAsset[] fonts;
        public string[] fontNames;
        public TextMeshPro target;
        [TextArea] public string text = "대기실";
        [Min(0)] public int fontIndex;

        public void Apply()
        {
            if (target == null || fonts == null || fonts.Length == 0) return;
            fontIndex = Mathf.Clamp(fontIndex, 0, fonts.Length - 1);
            target.font = fonts[fontIndex];
            target.text = text;
            target.ForceMeshUpdate();
        }

        public void SelectFont(int index)
        {
            if (fonts == null || fonts.Length == 0) return;
            fontIndex = (index % fonts.Length + fonts.Length) % fonts.Length;
            Apply();
        }
    }
}
