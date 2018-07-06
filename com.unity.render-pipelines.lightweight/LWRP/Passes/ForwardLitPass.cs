using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class ForwardLitPass : ScriptableRenderPass
    {
        //RenderTextureFormat m_ColorFormat;
        Material m_BlitMaterial;

        // Opaque Copy Pass
        Material m_SamplingMaterial;
        float[] m_OpaqueScalerValues = {1.0f, 0.5f, 0.25f, 0.25f};
        int m_SampleOffsetShaderHandle;

        public ForwardLitPass(LightweightForwardRenderer renderer) : base(renderer)
        {
            PerCameraBuffer._MainLightPosition = Shader.PropertyToID("_MainLightPosition");
            PerCameraBuffer._MainLightColor = Shader.PropertyToID("_MainLightColor");
            PerCameraBuffer._MainLightCookie = Shader.PropertyToID("_MainLightCookie");
            PerCameraBuffer._WorldToLight = Shader.PropertyToID("_WorldToLight");
            PerCameraBuffer._AdditionalLightCount = Shader.PropertyToID("_AdditionalLightCount");
            PerCameraBuffer._AdditionalLightPosition = Shader.PropertyToID("_AdditionalLightPosition");
            PerCameraBuffer._AdditionalLightColor = Shader.PropertyToID("_AdditionalLightColor");
            PerCameraBuffer._AdditionalLightDistanceAttenuation = Shader.PropertyToID("_AdditionalLightDistanceAttenuation");
            PerCameraBuffer._AdditionalLightSpotDir = Shader.PropertyToID("_AdditionalLightSpotDir");
            PerCameraBuffer._AdditionalLightSpotAttenuation = Shader.PropertyToID("_AdditionalLightSpotAttenuation");
            PerCameraBuffer._LightIndexBuffer = Shader.PropertyToID("_LightIndexBuffer");


            m_BlitMaterial = renderer.GetMaterial(MaterialHandles.Blit);

            // Copy Opaque Color Pass
            m_SamplingMaterial = renderer.GetMaterial(MaterialHandles.Sampling);
            m_SampleOffsetShaderHandle = Shader.PropertyToID("_SampleOffset");
        }

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
            var configureForwardRTs = new ConfigureForwardRTs(renderer);
            configureForwardRTs.Setup(descriptor, colorAttachmentHandle, depthAttachmentHandle, samples);
            configureForwardRTs.Execute(ref context, ref cullResults, ref renderingData);

            if (renderingData.cameraData.isStereoEnabled)
            {
                var beginXr = new BeginXRRendering(renderer);
                beginXr.Execute(ref context, ref cullResults, ref renderingData);
            }

            Camera camera = renderingData.cameraData.camera;
            bool dynamicBatching = renderingData.supportsDynamicBatching;
            RendererConfiguration rendererConfiguration = GetRendererConfiguration(renderingData.lightData.totalAdditionalLightsCount);

            var renderForwardOpaque = new RenderOpaqueForward(renderer);
            renderForwardOpaque.Setup(descriptor, colorAttachmentHandle, depthAttachmentHandle, GetCameraClearFlag(camera), camera.backgroundColor, rendererConfiguration,dynamicBatching);
            renderForwardOpaque.Execute(ref context, ref cullResults, ref renderingData);

            if (renderingData.cameraData.postProcessEnabled &&
                renderingData.cameraData.postProcessLayer.HasOpaqueOnlyEffects(renderer.postProcessRenderContext))
            {
                var renderPostOpaque = new OpaquePostProcessPass(renderer);
                renderPostOpaque.Setup(descriptor, colorAttachmentHandle);
                renderPostOpaque.Execute(ref context, ref cullResults, ref renderingData);
            }

            if (depthAttachmentHandle != RenderTargetHandle.BackBuffer)
            {
                var copyDepth = new CopyDepthPass(renderer);
                copyDepth.Setup(depthAttachmentHandle);
                copyDepth.Execute(ref context, ref cullResults, ref renderingData);
            }

            if (renderingData.cameraData.requiresOpaqueTexture)
                CopyColorSubpass(ref context, ref renderingData.cameraData);

            var renderForwardTransparent = new RenderTransparentForward(renderer);
            renderForwardTransparent.Setup(descriptor, colorAttachmentHandle, depthAttachmentHandle, ClearFlag.None, camera.backgroundColor, rendererConfiguration,dynamicBatching);
            renderForwardTransparent.Execute(ref context, ref cullResults, ref renderingData);

            if (renderingData.cameraData.postProcessEnabled)
            {
                var renderPost = new TransparentPostProcessPass(renderer);
                renderPost.Setup(descriptor, colorAttachmentHandle);
                renderPost.Execute(ref context, ref cullResults, ref renderingData);
            }
            else if (!renderingData.cameraData.isOffscreenRender && colorAttachmentHandle != RenderTargetHandle.BackBuffer)
                FinalBlitPass(ref context, ref renderingData.cameraData);

            if (renderingData.cameraData.isStereoEnabled)
            {
                var endXR = new EndXRRendering(renderer);
                endXR.Execute(ref context, ref cullResults, ref renderingData);
            }
        }

        public override void Dispose(CommandBuffer cmd)
        {
            if (colorAttachmentHandle != RenderTargetHandle.BackBuffer)
            {
                cmd.ReleaseTemporaryRT(colorAttachmentHandle.id);
                colorAttachmentHandle = RenderTargetHandle.BackBuffer;
            }

            if (depthAttachmentHandle != RenderTargetHandle.BackBuffer)
            {
                cmd.ReleaseTemporaryRT(depthAttachmentHandle.id);
                depthAttachmentHandle = RenderTargetHandle.BackBuffer;
            }
        }

        private static ClearFlag GetCameraClearFlag(Camera camera)
        {
            ClearFlag clearFlag = ClearFlag.None;
            CameraClearFlags cameraClearFlags = camera.clearFlags;
            if (cameraClearFlags != CameraClearFlags.Nothing)
            {
                clearFlag |= ClearFlag.Depth;
                if (cameraClearFlags == CameraClearFlags.Color || cameraClearFlags == CameraClearFlags.Skybox)
                    clearFlag |= ClearFlag.Color;
            }

            return clearFlag;
        }

        RendererConfiguration GetRendererConfiguration(int localLightsCount)
        {
            RendererConfiguration configuration = RendererConfiguration.PerObjectReflectionProbes | RendererConfiguration.PerObjectLightmaps | RendererConfiguration.PerObjectLightProbe;
            if (localLightsCount > 0)
            {
                if (renderer.useComputeBufferForPerObjectLightIndices)
                    configuration |= RendererConfiguration.ProvideLightIndices;
                else
                    configuration |= RendererConfiguration.PerObjectLightIndices8;
            }

            return configuration;
        }

        void FinalBlitPass(ref ScriptableRenderContext context, ref CameraData cameraData)
        {
            Material material = cameraData.isStereoEnabled ? null : m_BlitMaterial;
            RenderTargetIdentifier sourceRT = GetSurface(colorAttachmentHandle);

            CommandBuffer cmd = CommandBufferPool.Get("Final Blit Pass");
            cmd.SetGlobalTexture("_BlitTex", sourceRT);

            // We need to handle viewport on a RT. We do it by rendering a fullscreen quad + viewport
            if (!cameraData.isDefaultViewport)
            {
                SetRenderTarget(
                    cmd,
                    BuiltinRenderTextureType.CameraTarget,
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store,
                    ClearFlag.None,
                    Color.black,
                    descriptor.dimension);

                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmd.SetViewport(cameraData.camera.pixelRect);
                LightweightPipeline.DrawFullScreen(cmd, material);
            }
            else
            {
                cmd.Blit(GetSurface(colorAttachmentHandle), BuiltinRenderTextureType.CameraTarget, material);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void CopyColorSubpass(ref ScriptableRenderContext context, ref CameraData cameraData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Copy Opaque Color");
            Downsampling downsampling = cameraData.opaqueTextureDownsampling;
            float opaqueScaler = m_OpaqueScalerValues[(int)downsampling];

            RenderTextureDescriptor opaqueDesc = renderer.CreateRTDesc(ref cameraData, opaqueScaler);
            RenderTargetIdentifier colorRT = GetSurface(colorAttachmentHandle);
            RenderTargetIdentifier opaqueColorRT = GetSurface(RenderTargetHandles.OpaqueColor);

            cmd.GetTemporaryRT(RenderTargetHandles.OpaqueColor.id, opaqueDesc, cameraData.opaqueTextureDownsampling == Downsampling.None ? FilterMode.Point : FilterMode.Bilinear);
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
