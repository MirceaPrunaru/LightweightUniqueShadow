using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public class UniqueShadowCamera
	{
        public void Init( ShadowResolution shadowMapSize)
		{
			var shadowCameraGO = new GameObject( "__Unique Camera__" );
            shadowCameraGO.hideFlags = HideFlags.DontSave | HideFlags.NotEditable | HideFlags.HideInHierarchy;

			camera = shadowCameraGO.AddComponent<Camera>();
			camera.renderingPath = RenderingPath.Forward;
			camera.clearFlags = CameraClearFlags.Depth;
			camera.depthTextureMode = DepthTextureMode.None;
			camera.useOcclusionCulling = false;
			camera.cullingMask = ~0;
			camera.orthographic = true;
			camera.depth = -100;
			camera.aspect = 1f;
			camera.enabled = false;
            
            m_textureSize = (int)shadowMapSize;
			m_ShadowmapFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Shadowmap)
                ? RenderTextureFormat.Shadowmap
                : RenderTextureFormat.Depth;
		}
        
        public void AddUniqueShadow( LightweightPipelineAsset asset )
        {
            AllocRenderTarget();
            SetUniforms();
            asset.AddUniqueShadow( camera );
        }

		public void Clear()
		{
			DisposeRenderTarget();

			Disable();
			m_materialInstances.Clear();
		}

        public void Destroy()
        {
            if (camera)
                Object.DestroyImmediate( camera.gameObject );
        }
        
		#region camera
		public Camera camera { get; private set; }
		Matrix4x4 m_shadowMatrix;
		Matrix4x4 m_shadowSpaceMatrix;
		float m_far;
        int m_textureSize;
        
        RenderTextureFormat m_ShadowmapFormat;
        const int k_ShadowmapBufferBits = 16;
		public int cullingMask
		{
			set { camera.cullingMask = value; }
		}

		public void Projection( float radius, float sceneCaptureDistance, float depthBias)
		{
			m_far = radius + sceneCaptureDistance;

			camera.orthographicSize = radius;
			// RenderPipeline Culling doesn't support minus nearClipPlane.
			//m_shadowCamera.nearClipPlane = -sceneCaptureDistance;
			//m_shadowCamera.farClipPlane = focus.radius * 2f;
			camera.nearClipPlane = 0f;
			camera.farClipPlane = radius * 2f + sceneCaptureDistance;
			var shadowProjection = Matrix4x4.Ortho( -radius, radius, -radius, radius, camera.nearClipPlane, camera.farClipPlane );
			camera.projectionMatrix = shadowProjection;

			var db = SystemInfo.usesReversedZBuffer ? depthBias : -depthBias;
		    m_shadowSpaceMatrix.SetRow(0, new Vector4(0.5f, 0.0f, 0.0f, 0.5f));
		    m_shadowSpaceMatrix.SetRow(1, new Vector4(0.0f, 0.5f, 0.0f, 0.5f));
		    m_shadowSpaceMatrix.SetRow(2, new Vector4(0.0f, 0.0f, 0.5f, 0.5f + db));
		    m_shadowSpaceMatrix.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        }

		public void Transform( Vector3 target, Vector3 lightDir, Quaternion lightOri )
		{
			// RenderPipeline Culling doesn't support minus nearClipPlane.
			//m_shadowCamera.transform.position = target - lightDir * radius;
			camera.transform.position = target - lightDir * m_far;
			camera.transform.rotation = lightOri;

			//TODO: Texel snap? (probably doesn't matter too much since the targets are always animated)
			var shadowViewMat = camera.worldToCameraMatrix;
			var shadowProjection = camera.projectionMatrix;
			if( SystemInfo.usesReversedZBuffer )
			{
				shadowProjection[2, 0] = -shadowProjection[2, 0];
				shadowProjection[2, 1] = -shadowProjection[2, 1];
				shadowProjection[2, 2] = -shadowProjection[2, 2];
				shadowProjection[2, 3] = -shadowProjection[2, 3];
			}
			m_shadowMatrix = m_shadowSpaceMatrix * shadowProjection * shadowViewMat;
		}

		public bool Distance( Vector3 target, float cullingDistance )
		{
            Debug.Assert( camera );
			return ( target - camera.transform.position ).sqrMagnitude < ( cullingDistance * cullingDistance );
		}
		public bool TestAABB( Bounds b )
		{
            Debug.Assert( camera );
			return GeometryUtility.TestPlanesAABB( GeometryUtility.CalculateFrustumPlanes( camera ), b );
		}
		#endregion

		#region Material
		static class Uniforms
		{
			internal static readonly int _UniqueShadowMatrix = Shader.PropertyToID( "_UniqueShadowMatrix" );
			internal static readonly int _UniqueShadowFilterWidth = Shader.PropertyToID( "_UniqueShadowFilterWidth" );
			internal static readonly int _UniqueShadowTexture = Shader.PropertyToID( "_UniqueShadowTexture" );
		}
		const string UNIQUE_SHADOW = "_UNIQUE_SHADOW";

		private List<Material> m_materialInstances = new List<Material>();
		private float m_shadowFilterWidth;

		public float fallbackFilterWidth
		{
			// We want the same 'softness' regardless of texture resolution.
			set { m_shadowFilterWidth = value / 2048f; }
		}
		public void AddMaterials( Material[] materials )
		{
			m_materialInstances.AddRange( materials );
		}
		public void Disable()
		{
			for( int i = 0, n = m_materialInstances.Count; i < n; ++i )
			{
				var m = m_materialInstances[i];
				m.DisableKeyword( UNIQUE_SHADOW );
			}
		}
		private void SetUniforms()
		{
			for( int i = 0, n = m_materialInstances.Count; i < n; ++i )
			{
				var m = m_materialInstances[i];
				m.EnableKeyword( UNIQUE_SHADOW );
				m.SetMatrix( Uniforms._UniqueShadowMatrix, m_shadowMatrix );
				m.SetFloat( Uniforms._UniqueShadowFilterWidth, m_shadowFilterWidth );
				m.SetTexture( Uniforms._UniqueShadowTexture, m_UniqueShadowTexture );
			}
		}
		#endregion

		#region RenderTexturte
		private RenderTexture m_UniqueShadowTexture;
		private void AllocRenderTarget()
		{
			if( m_UniqueShadowTexture == null )
			{
                m_UniqueShadowTexture = RenderTexture.GetTemporary(m_textureSize, m_textureSize,
                    k_ShadowmapBufferBits, m_ShadowmapFormat);
				m_UniqueShadowTexture.filterMode = FilterMode.Bilinear;
				m_UniqueShadowTexture.wrapMode = TextureWrapMode.Clamp;
#if UNITY_EDITOR
                m_UniqueShadowTexture.name = "UniqueShadowMap";
#endif
            }
            camera.targetTexture = m_UniqueShadowTexture;
		}
		private void DisposeRenderTarget()
		{
			if (m_UniqueShadowTexture)
				RenderTexture.ReleaseTemporary( m_UniqueShadowTexture );
			m_UniqueShadowTexture = null;
		}
		#endregion
	}

    public class UniqueShadowsPass : ScriptableRenderPass
    {
        const string k_UniqueShadowPassTag = "Unique Shadow Pass";

        public UniqueShadowsPass(LightweightForwardRenderer renderer) : base(renderer)
        {
            RegisterShaderPassName("DepthOnly");
        }

        public override void Setup( CommandBuffer cmd, RenderTextureDescriptor baseDescriptor, int[] colorAttachmentHandles = null, int depthAttachmentHandle = -1, int samples = 1 )
        {}
        
        public override void Dispose(CommandBuffer cmd)
        {}

        void RenderUniqueShadowMap(ref ScriptableRenderContext context, Camera camera, bool supportsDynamicBatching )
        {
            // Culling
            ScriptableCullingParameters cullingParameters;
            if (!CullResults.GetCullingParameters(camera, out cullingParameters))
                return;
			
			CullResults cullResult = CullResults.Cull(ref cullingParameters, context);
            
			// Depth Only Draw
            CommandBuffer cmd = CommandBufferPool.Get(k_UniqueShadowPassTag);
            using( new ProfilingSample( cmd, k_UniqueShadowPassTag ) )
            {
                SetRenderTarget(cmd, camera.targetTexture, RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store, ClearFlag.Depth, Color.black);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var drawSettings = CreateDrawRendererSettings( camera, SortFlags.CommonOpaque, RendererConfiguration.None, supportsDynamicBatching );
                context.DrawRenderers( cullResult.visibleRenderers, ref drawSettings, renderer.opaqueFilterSettings );
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void Execute(ref ScriptableRenderContext context, ref CullResults cullResults, ref RenderingData renderingData)
        {
			var CurrentCamera = Camera.current;
            for( int i = 0, n = renderingData.uniqueShadowData.cameras.Length; i < n; ++i )
            {
                var camera = renderingData.uniqueShadowData.cameras[i];
			    Debug.Assert( camera.isActiveAndEnabled == false );

                Camera.SetupCurrent( camera );	// for UniqueShadow.OnWillRenderObject()
                context.SetupCameraProperties( camera, false );

                RenderUniqueShadowMap( ref context, camera, renderingData.supportsDynamicBatching );
            }

            // Reset Camera
            context.SetupCameraProperties(renderingData.cameraData.camera, renderingData.cameraData.isStereoEnabled);
			Camera.SetupCurrent( CurrentCamera );
        }
    }
}
