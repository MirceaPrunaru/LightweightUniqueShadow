using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class ConfigureForwardRTs : ScriptableRenderPass
    {
        public ConfigureForwardRTs(LightweightForwardRenderer renderer) : base(renderer)
        {}

        const int k_DepthStencilBufferBits = 32;
        private RenderTargetHandle colorAttachmentHandle { get; set; }
        private RenderTargetHandle depthAttachmentHandle { get; set; }
        private RenderTextureDescriptor descriptor { get; set; }
        private SampleCount samples { get; set; }

        public void Setup(
            RenderTextureDescriptor baseDescriptor,
            RenderTargetHandle colorAttachmentHandle,
            RenderTargetHandle depthAttachmentHandle,
            SampleCount samples)
        {
            this.colorAttachmentHandle = colorAttachmentHandle;
            this.depthAttachmentHandle = depthAttachmentHandle;
            this.samples = samples;
            descriptor = baseDescriptor;
        }

        public override void Execute(ref ScriptableRenderContext context, ref CullResults cullResults, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("");
            if (colorAttachmentHandle != RenderTargetHandle.BackBuffer)
            {
                var colorDescriptor = descriptor;
                colorDescriptor.depthBufferBits = k_DepthStencilBufferBits; // TODO: does the color RT always need depth?
                colorDescriptor.sRGB = true;
                colorDescriptor.msaaSamples = (int) samples;
                cmd.GetTemporaryRT(colorAttachmentHandle.id, colorDescriptor, FilterMode.Bilinear);
            }

            if (depthAttachmentHandle != RenderTargetHandle.BackBuffer)
            {
                var depthDescriptor = descriptor;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
                depthDescriptor.msaaSamples = (int) samples;
                depthDescriptor.bindMS = (int) samples > 1;
                cmd.GetTemporaryRT(depthAttachmentHandle.id, depthDescriptor, FilterMode.Point);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}