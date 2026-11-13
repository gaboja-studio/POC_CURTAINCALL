using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Camera-space union contour of the selected hierarchy; original meshes/materials/LODs stay untouched.</summary>
    public sealed class InteractionOutline : System.IDisposable
    {
        readonly Material style;
        readonly SilhouetteOutline silhouette;
        public Transform Target { get; }
        public InteractionOutline(Transform target, Material material)
        {
            Target=target;style=material;
            silhouette=new SilhouetteOutline(target,Color.white,3,priority:10);
            RenderPipelineManager.beginCameraRendering+=BeforeRender;
            Update();
        }
        void BeforeRender(ScriptableRenderContext _,Camera camera)=>Update();
        public void Update()
        {
            if(style==null)return;
            silhouette.Color=style.GetColor("_OutlineColor");
            silhouette.Pixels=style.GetFloat("_OutlinePixels");
        }
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeRender;
            silhouette.Dispose();
        }
    }
}
