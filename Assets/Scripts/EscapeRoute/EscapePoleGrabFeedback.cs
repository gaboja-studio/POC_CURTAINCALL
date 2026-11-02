using System;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Local conveyor feedback uses the same nearest available pole as the F interaction.</summary>
    [DefaultExecutionOrder(900)]
    public sealed class EscapePoleGrabFeedback : MonoBehaviour
    {
        [SerializeField] HumanCannonPole[] poles = Array.Empty<HumanCannonPole>();
        [SerializeField] Color availableColor = new(.08f, 1f, .2f, 1f);
        sealed class Surface
        {
            public HumanCannonPole Pole;
            public Renderer Renderer;
            public MaterialPropertyBlock Original, Available;
            public bool Highlighted;
        }
        readonly List<Surface> surfaces = new();

        void OnEnable()
        {
            Restore(); surfaces.Clear();
            foreach (var pole in poles)
            {
                if (pole == null || pole.VisualBar == null) continue;
                foreach (var renderer in pole.VisualBar.GetComponentsInChildren<Renderer>(true))
                {
                    var surface = new Surface { Pole = pole, Renderer = renderer,
                        Original = new MaterialPropertyBlock(), Available = new MaterialPropertyBlock() };
                    renderer.GetPropertyBlock(surface.Original); renderer.GetPropertyBlock(surface.Available);
                    surface.Available.SetColor("_BaseColor", availableColor);
                    surface.Available.SetColor("_Color", availableColor);
                    surfaces.Add(surface);
                }
            }
        }

        void LateUpdate()
        {
            var system = HumanCannonPoleSystem.Current;
            var player = NetworkPlayer.Local;
            int index = system != null && player != null && !InputGate.IsBlocked ? system.FindGrabbable(player) : -1;
            Present(index >= 0 ? system.Pole(index) : null);
        }

        void Present(HumanCannonPole available)
        {
            foreach (var surface in surfaces)
            {
                bool show = available != null && surface.Pole == available;
                if (show == surface.Highlighted || surface.Renderer == null) continue;
                surface.Highlighted = show;
                surface.Renderer.SetPropertyBlock(show ? surface.Available : surface.Original);
            }
        }

        void Restore()
        {
            foreach (var surface in surfaces)
                if (surface.Renderer != null && surface.Highlighted)
                { surface.Renderer.SetPropertyBlock(surface.Original); surface.Highlighted = false; }
        }
        void OnDisable() => Restore();
    }
}
