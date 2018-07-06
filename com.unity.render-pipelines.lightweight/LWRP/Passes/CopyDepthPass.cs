using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class CopyDepthPass : ScriptableRenderPass
    {
        Material m_DepthCopyMaterial;

        private RenderTargetHandle depthAttachmentHandle { get; set; }

        public CopyDepthPass(LightweightForwardRenderer renderer) : base(renderer)
        {
            // Copy Depth Pass
            m_DepthCopyMaterial = renderer.GetMaterial(MaterialHandles.DepthCopy);
        }

        public void Setup(RenderTargetHandle depthAttachmentHandle)
        {
            this.depthAttachmentHandle = depthAttachmentHandle;
        }

        public override void Execute(ref ScriptableRenderContext context, ref CullResults cullResults, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Depth Copy");
            RenderTargetIdentifier depthSurface = GetSurface(depthAttachmentHandle);
            RenderTargetIdentifier copyDepthSurface = GetSurface(RenderTargetHandles.DepthTexture);

            RenderTextureDescriptor descriptor = renderer.CreateRTDesc(ref renderingData.cameraData);
            descriptor.colorFormat = RenderTextureFormat.Depth;
            descriptor.depthBufferBits = 32; //TODO: fix this ;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            cmd.GetTemporaryRT(RenderTargetHandles.DepthTexture.id, descriptor, FilterMode.Point);

            if (renderingData.cameraData.msaaSamples > 1)
            {
                cmd.DisableShaderKeyword(LightweightKeywordStrings.DepthNoMsaa);
                if (renderingData.cameraData.msaaSamples == 4)
                {
                    cmd.DisableShaderKeyword(LightweightKeywordStrings.DepthMsaa2);
                    cmd.EnableShaderKeyword(LightweightKeywordStrings.DepthMsaa4);
                }
                else
                {
                    cmd.EnableShaderKeyword(LightweightKeywordStrings.DepthMsaa2);
                    cmd.DisableShaderKeyword(LightweightKeywordStrings.DepthMsaa4);
                }
                cmd.Blit(depthSurface, copyDepthSurface, m_DepthCopyMaterial);
            }
            else
            {
                cmd.EnableShaderKeyword(LightweightKeywordStrings.DepthNoMsaa);
                cmd.DisableShaderKeyword(LightweightKeywordStrings.DepthMsaa2);
                cmd.DisableShaderKeyword(LightweightKeywordStrings.DepthMsaa4);
                LightweightPipeline.CopyTexture(cmd, depthSurface, copyDepthSurface, m_DepthCopyMaterial);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
