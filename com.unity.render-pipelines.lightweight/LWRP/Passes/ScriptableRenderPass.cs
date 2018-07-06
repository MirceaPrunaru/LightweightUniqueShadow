using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public abstract class ScriptableRenderPass
    {
        public LightweightForwardRenderer renderer { get; private set; }
        public RenderTargetHandle[] colorAttachmentHandles { get; private set; }

        public RenderTargetHandle colorAttachmentHandle { get; private set; }

        public RenderTargetHandle depthAttachmentHandle { get; private set; }

        public RenderTextureDescriptor descriptor { get; private set; }

        public SampleCount samples { get; private set; }

        protected List<ShaderPassName> m_ShaderPassNames = new List<ShaderPassName>();

        public ScriptableRenderPass(LightweightForwardRenderer renderer)
        {
            this.renderer = renderer;
        }


        public virtual void Setup(RenderTextureDescriptor baseDescriptor,
            RenderTargetHandle[] colorAttachmentHandles,
            RenderTargetHandle depthAttachmentHandle, SampleCount samples)
        {
            this.colorAttachmentHandles = colorAttachmentHandles;
            this.depthAttachmentHandle = depthAttachmentHandle;
            this.samples = samples;
            colorAttachmentHandle = (colorAttachmentHandles != null && colorAttachmentHandles.Length > 0)
                ? colorAttachmentHandles[0]
                : RenderTargetHandle.BackBuffer;

            descriptor = baseDescriptor;
        }

        public virtual void Dispose(CommandBuffer cmd)
        {
            if (colorAttachmentHandles != null)
            {
                for (int i = 0; i < colorAttachmentHandles.Length; ++i)
                    if (colorAttachmentHandles[i] != RenderTargetHandle.BackBuffer)
                        cmd.ReleaseTemporaryRT(colorAttachmentHandles[i].id);
            }

            if (depthAttachmentHandle != RenderTargetHandle.BackBuffer)
                cmd.ReleaseTemporaryRT(depthAttachmentHandle.id);
        }

        public abstract void Execute(ref ScriptableRenderContext context, ref CullResults cullResults, ref RenderingData renderingData);

        public RenderTargetIdentifier GetSurface(RenderTargetHandle handle)
        {
            if (renderer == null)
            {
                Debug.LogError("Pass has invalid renderer");
                return new RenderTargetIdentifier();
            }

            return renderer.GetSurface(handle);
        }

        public void RegisterShaderPassName(string passName)
        {
            m_ShaderPassNames.Add(new ShaderPassName(passName));
        }

        public DrawRendererSettings CreateDrawRendererSettings(Camera camera, SortFlags sortFlags, RendererConfiguration rendererConfiguration, bool supportsDynamicBatching)
        {
            DrawRendererSettings settings = new DrawRendererSettings(camera, m_ShaderPassNames[0]);
            for (int i = 1; i < m_ShaderPassNames.Count; ++i)
                settings.SetShaderPassName(i, m_ShaderPassNames[i]);
            settings.sorting.flags = sortFlags;
            settings.rendererConfiguration = rendererConfiguration;
            settings.flags = DrawRendererFlags.EnableInstancing;
            if (supportsDynamicBatching)
                settings.flags |= DrawRendererFlags.EnableDynamicBatching;
            return settings;
        }

        public void SetRenderTarget(CommandBuffer cmd, RenderTargetIdentifier colorAttachment, RenderBufferLoadAction colorLoadAction,
            RenderBufferStoreAction colorStoreAction, ClearFlag clearFlag, Color clearColor)
        {
            if (descriptor.dimension == TextureDimension.Tex2DArray)
                CoreUtils.SetRenderTarget(cmd, colorAttachment, clearFlag, clearColor, 0, CubemapFace.Unknown, -1);
            else
                CoreUtils.SetRenderTarget(cmd, colorAttachment, colorLoadAction, colorStoreAction, clearFlag, clearColor);
        }

        public void SetRenderTarget(CommandBuffer cmd, RenderTargetIdentifier colorAttachment, RenderBufferLoadAction colorLoadAction,
            RenderBufferStoreAction colorStoreAction, RenderTargetIdentifier depthAttachment, RenderBufferLoadAction depthLoadAction,
            RenderBufferStoreAction depthStoreAction, ClearFlag clearFlag, Color clearColor)
        {
            if (descriptor.dimension == TextureDimension.Tex2DArray)
                CoreUtils.SetRenderTarget(cmd, colorAttachment, depthAttachment,
                    clearFlag, clearColor, 0, CubemapFace.Unknown, -1);
            else
                CoreUtils.SetRenderTarget(cmd, colorAttachment, colorLoadAction, colorStoreAction,
                    depthAttachment, depthLoadAction, depthStoreAction, clearFlag, clearColor);
        }
    }
}
