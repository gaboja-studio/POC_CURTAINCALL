using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Union mask -> screen-space exterior only. No normal/depth edges inside the selected shape.</summary>
    public sealed class SilhouetteOutlineFeature : ScriptableRendererFeature
    {
        Material composite;
        OutlinePass pass;
        public override void Create()
        {
            CoreUtils.Destroy(composite);
            var shader=Resources.Load<Shader>("SilhouetteOutline");
            if(shader!=null)composite=CoreUtils.CreateEngineMaterial(shader);
            pass=new OutlinePass(composite){renderPassEvent=RenderPassEvent.BeforeRenderingPostProcessing};
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if(composite!=null&&SilhouetteOutline.ActiveCount>0&&data.cameraData.cameraType==CameraType.Game)renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing){CoreUtils.Destroy(composite);composite=null;}

        sealed class OutlinePass : ScriptableRenderPass
        {
            readonly Material material;
            public OutlinePass(Material material){this.material=material;}
            sealed class MaskData { public SilhouetteOutline.Draw[] draws; }
            sealed class CompositeData { public Material material;public TextureHandle mask,info,sceneDepth;public Vector4 texel,bounds; }
            static Vector4 ScreenBounds(SilhouetteOutline.Draw[] draws,Camera camera)
            {
                var min=Vector2.one;var max=Vector2.zero;
                foreach(var draw in draws)
                {
                    var b=draw.Renderer.bounds;
                    for(int corner=0;corner<8;corner++)
                    {
                        var p=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                        var screen=camera.WorldToViewportPoint(p);
                        if(screen.z<=camera.nearClipPlane)return new Vector4(0,0,1,1);
                        min=Vector2.Min(min,screen);max=Vector2.Max(max,screen);
                    }
                }
                // Conservative in Y for both backbuffer and render-texture projection conventions.
                float yMin=Mathf.Min(min.y,1-max.y),yMax=Mathf.Max(max.y,1-min.y);
                return new Vector4(min.x,yMin,max.x,yMax);
            }
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
            {
                var camera=frameData.Get<UniversalCameraData>();
                var draws=SilhouetteOutline.Collect(camera.camera);if(draws.Length==0)return;
                var resources=frameData.Get<UniversalResourceData>();
                if(!resources.cameraDepthTexture.IsValid())return;
                var desc=graph.GetTextureDesc(resources.activeColorTexture);
                desc.msaaSamples=MSAASamples.None;desc.depthBufferBits=DepthBits.None;
                desc.colorFormat=GraphicsFormat.R16G16B16A16_SFloat;desc.clearBuffer=true;desc.clearColor=Color.clear;
                desc.name="Silhouette union color";var mask=graph.CreateTexture(desc);
                desc.name="Silhouette width and eye depth";var info=graph.CreateTexture(desc);
                var depthDesc=graph.GetTextureDesc(resources.activeDepthTexture);
                depthDesc.name="Silhouette private depth";depthDesc.msaaSamples=MSAASamples.None;
                depthDesc.colorFormat=GraphicsFormat.None;depthDesc.depthBufferBits=DepthBits.Depth32;depthDesc.clearBuffer=true;
                var depth=graph.CreateTexture(depthDesc);
                using(var builder=graph.AddRasterRenderPass<MaskData>("Silhouette / whole target mask",out var data))
                {
                    data.draws=draws;
                    builder.SetRenderAttachment(mask,0,AccessFlags.Write);
                    builder.SetRenderAttachment(info,1,AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth,AccessFlags.Write);
                    builder.SetRenderFunc(static (MaskData d,RasterGraphContext context)=>
                    {
                        foreach(var draw in d.draws)if(draw.Renderer!=null&&draw.Material!=null)context.cmd.DrawRenderer(draw.Renderer,draw.Material,draw.Submesh,0);
                    });
                }
                using(var builder=graph.AddRasterRenderPass<CompositeData>("Silhouette / exterior contour",out var data))
                {
                    data.material=material;data.mask=mask;data.info=info;data.sceneDepth=resources.cameraDepthTexture;
                    data.texel=new Vector4(1f/camera.cameraTargetDescriptor.width,1f/camera.cameraTargetDescriptor.height,0,0);
                    data.bounds=ScreenBounds(draws,camera.camera);
                    builder.UseTexture(mask,AccessFlags.Read);builder.UseTexture(info,AccessFlags.Read);builder.UseTexture(data.sceneDepth,AccessFlags.Read);
                    // Blending preserves the scene; no color-target sampling or read/write feedback loop.
                    builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositeData d,RasterGraphContext context)=>
                    {
                        var properties=new MaterialPropertyBlock();
                        properties.SetTexture("_SilhouetteMask",d.mask);
                        properties.SetTexture("_SilhouetteInfo",d.info);
                        properties.SetTexture("_SilhouetteSceneDepth",d.sceneDepth);
                        properties.SetVector("_SilhouetteTexel",d.texel);
                        properties.SetVector("_SilhouetteBounds",d.bounds);
                        context.cmd.DrawProcedural(Matrix4x4.identity,d.material,1,MeshTopology.Triangles,3,1,properties);
                    });
                }
            }
        }
    }
}
