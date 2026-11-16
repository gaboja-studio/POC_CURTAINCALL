using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Camera-space union contour of the selected hierarchy; original meshes/materials/LODs stay untouched.</summary>
    public sealed class InteractionOutline : System.IDisposable
    {
        Material style;
        /// <summary>외곽선 두께·색을 읽는 재질. 범위 안 대상(얇게)과 선택 대상(강조)을 바꿔 끼운다.</summary>
        public Material Style { get => style; set { if (style == value) return; style = value; Update(); } }
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
