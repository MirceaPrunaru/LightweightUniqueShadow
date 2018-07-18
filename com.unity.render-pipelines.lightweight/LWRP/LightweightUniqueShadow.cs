using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.LightweightPipeline;
using Pipeline = UnityEngine.Experimental.Rendering.LightweightPipeline;

public class LightweightUniqueShadow : MonoBehaviour
{
    public Pipeline.ShadowResolution shadowMapSize = Pipeline.ShadowResolution._2048;
    public LayerMask inclusionMask = ~0;
    public float cullingDistance = 100f;
    public float fallbackFilterWidth = 6f;

    [Header( "Focus" )]
    public bool autoFocus;
    public float autoFocusRadiusBias;
    public Transform target;
    public Vector3 offset;
    public float radius = 1f;
	public float depthBias = 0.005f;
    public float sceneCaptureDistance = 4f;
    public Vector3 position
    {
        get
        {
            return target.position
                + target.right * offset.x
                + target.up * offset.y
                + target.forward * offset.z;
        }
    }

    [Space]
    public Light m_lightSource;
    private Light GetLight()
    {
        var l = m_lightSource ? m_lightSource : RenderSettings.sun;
        return l.shadows != LightShadows.None ? l : null;
    }
    UniqueShadowCamera m_shadowCamera = new UniqueShadowCamera();

    Pipeline.LightweightPipelineAsset m_Asset;
    void Awake()
    {
        m_Asset = GraphicsSettings.renderPipelineAsset as LightweightPipelineAsset;
        if( m_Asset == null )
            return;

        m_shadowCamera.Init(shadowMapSize);
        // Instantiating material due to calling renderer.material during edit mode. So, It doesn't support ExecuteInEditorMode.
        foreach( var r in GetComponentsInChildren<Renderer>() )
        {
            if( r.receiveShadows )
                m_shadowCamera.AddMaterials( r.materials );
        }
        SetFocus();

        if( GetComponent( typeof( MeshRenderer ) ) == null )
            InitMeshRenderer();
    }
        
    void UpdateFocusRadius()
    {
        var self = m_DecoyMesh ? GetComponent<Renderer>() : null;
        var bounds = new Bounds( position, Vector3.one * 0.1f );
        foreach( var r in GetComponentsInChildren<Renderer>() )
        {
            if( r != self )
                bounds.Encapsulate( r.bounds );
        }

        offset = bounds.center - target.position;
        radius = autoFocusRadiusBias + bounds.extents.magnitude;
    }

    void SetFocus()
    {
        m_shadowCamera.cullingMask = inclusionMask;
        m_shadowCamera.fallbackFilterWidth = fallbackFilterWidth;

        if( autoFocus )
            UpdateFocusRadius();

        radius = Mathf.Max( radius, Mathf.Epsilon );
        m_shadowCamera.Projection( radius, sceneCaptureDistance, depthBias );
    }

    bool CheckVisibility(Light light)
    {
        if( target == null || light == null)
            return false;

        if( autoFocus )
            UpdateFocusRadius();

        m_shadowCamera.Transform( position, light.transform.forward, light.transform.rotation );

        var targetPos = position;
        var bounds = new Bounds( targetPos, Vector3.one * radius * 2f );

        return m_shadowCamera.Distance( targetPos, cullingDistance ) && m_shadowCamera.TestAABB( bounds );
    }
        
    private Mesh m_DecoyMesh = null;
    private const float m_kDecoyRadiusScale = 2.0f;
    void InitMeshRenderer()
    {
        // for call OnWillRenderObject
        m_DecoyMesh = new Mesh();
        m_DecoyMesh.bounds = new Bounds( Vector3.zero, Vector3.one * radius * m_kDecoyRadiusScale );
        m_DecoyMesh.hideFlags = HideFlags.HideAndDontSave;

        var mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = m_DecoyMesh;
        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
    }

    void OnDestroy()
    {
        if( m_DecoyMesh )
            Object.DestroyImmediate( m_DecoyMesh );

        m_shadowCamera.Destroy();
    }

    void OnEnable()
    {
        if( target == null )
        {
            target = this.transform;
            UpdateFocusRadius();
        }
    }

    void OnDisable()
    {
        m_shadowCamera.Clear();
    }

    void OnValidate()
    {
        if( !Application.isPlaying || !m_shadowCamera.camera )
            return;

        SetFocus();
    }

    void OnWillRenderObject()
    {
        if( Camera.current.isActiveAndEnabled == false || m_Asset == null)
            return;

        Light light = GetLight();
        if( !CheckVisibility(light) )
            m_shadowCamera.Disable();
        else
            m_shadowCamera.AddUniqueShadow( m_Asset );
    }

    void OnDrawGizmosSelected()
    {
        if( target == null )
            return;

        Gizmos.color = m_shadowCamera.camera ? autoFocus ? Color.cyan : Color.green : Color.red;

        Gizmos.DrawWireSphere( position, radius + ( autoFocus ? autoFocusRadiusBias : 0f ) );

        if( m_shadowCamera.camera )
        {
            Gizmos.color = Color.gray;
            Gizmos.matrix = m_shadowCamera.camera.cameraToWorldMatrix;

            Vector3 center = new Vector3( 0, 0, -radius );
            Vector3 size = Vector3.one * radius * 2;
            center.z += -sceneCaptureDistance * 0.5f;
            size.z += sceneCaptureDistance;
            Gizmos.DrawWireCube( center, size );
        }
    }
}
