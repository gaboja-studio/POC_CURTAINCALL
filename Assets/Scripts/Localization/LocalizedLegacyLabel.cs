using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.Localization
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class LocalizedLegacyLabel : MonoBehaviour
    {
        Text ui;
        TextMesh world;
        string source, rendered;

        public void Track(Component label)
        {
            if (label is Text text) ui = text;
            else if (label is TextMesh mesh) world = mesh;
            Refresh();
        }

        void OnEnable() => Refresh();
        void LateUpdate()
        {
            if ((ui ? ui.text : world ? world.text : null) != rendered) Refresh();
        }

        public void Refresh()
        {
            if (!ui && !world) return;
            string current = ui ? ui.text : world.text;
            if (current != rendered || source == null) source = current;
            rendered = GameLanguage.Display(source);
            if (current == rendered) return;
            if (ui) ui.text = rendered;
            else world.text = rendered;
        }
    }
}

