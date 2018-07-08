using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class CopyColorPass : ScriptableRenderPass
    {
        Material m_SamplingMaterial;
        float[] m_OpaqueScalerValues = {1.0f, 0.5f, 0.25f, 0.25f};
        int m_SampleOffsetShaderHandle;

        private RenderTargetHandle colorAttachmentHandle { get; set; }

        public CopyColorPass(LightweightForwardRenderer renderer) : base(renderer)
        {
            m_SamplingMaterial = renderer.GetMaterial(MaterialHandles.Sampling);
            m_SampleOffsetShaderHandle = Shader.PropertyToID("_SampleOffset");
        }

        public void Setup(RenderTargetHandle colorAttachmentHandle)
        {
            this.colorAttachmentHandle = colorAttachmentHandle;
        }

        public override void Execute(ref ScriptableRenderContext context, ref CullResults cullResults, ref RenderingData renderingData)
        {
            
            CommandBuffer cmd = CommandBufferPool.Get("Copy Opaque Color");
            Downsampling downsampling = renderingData.cameraData.opaqueTextureDownsampling;
            float opaqueScaler = m_OpaqueScalerValues[(int)downsampling];

            RenderTextureDescriptor opaqueDesc = renderer.CreateRTDesc(ref renderingData.cameraData, opaqueScaler);
            RenderTargetIdentifier colorRT = GetSurface(colorAttachmentHandle);
            RenderTargetIdentifier opaqueColorRT = GetSurface(RenderTargetHandles.OpaqueColor);

            cmd.GetTemporaryRT(RenderTargetHandles.OpaqueColor.id, opaqueDesc, renderingData.cameraData.opaqueTextureDownsampling == Downsampling.None ? FilterMode.Point : FilterMode.Bilinear);
            switch (downsampling)
            {
                case Downsampling.None:
                    cmd.Blit(colorRT, opaqueColorRT);
                    break;
                case Downsampling._2xBilinear:
                    cmd.Blit(colorRT, opaqueColorRT);
                    break;
                case Downsampling._4xBox:
                    m_SamplingMaterial.SetFloat(m_SampleOffsetShaderHandle, 2);
                    cmd.Blit(colorRT, opaqueColorRT, m_SamplingMaterial, 0);
                    break;
                case Downsampling._4xBilinear:
                    cmd.Blit(colorRT, opaqueColorRT);
                    break;
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}