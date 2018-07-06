using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class RenderOpaqueForward : LwForwardPass
    {
        const string k_RenderOpaquesTag = "Render Opaques";

        public RenderOpaqueForward(LightweightForwardRenderer renderer) : base(renderer)
        {
        }

        public override void Execute(ref ScriptableRenderContext context, ref CullResults cullResults,
            ref RenderingData renderingData)
        {
            base.Execute(ref context, ref cullResults, ref renderingData);

            Camera camera = renderingData.cameraData.camera;
            var drawSettings =
                CreateDrawRendererSettings(camera, SortFlags.CommonOpaque, rendererConfiguration, dynamicBatching);
            context.DrawRenderers(cullResults.visibleRenderers, ref drawSettings, renderer.opaqueFilterSettings);

            // Render objects that did not match any shader pass with error shader
            RenderObjectsWithError(ref context, ref cullResults, camera, renderer.opaqueFilterSettings, SortFlags.None);

            if (camera.clearFlags == CameraClearFlags.Skybox)
                context.DrawSkybox(camera);



        }
    }
}
