namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class BeginXRRendering : ScriptableRenderPass
    {
        public BeginXRRendering(LightweightForwardRenderer renderer) : base(renderer)
        {}

        public override void Execute(ref ScriptableRenderContext context, ref CullResults cullResults, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            context.StartMultiEye(camera);
        }
    }
}