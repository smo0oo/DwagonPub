using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class HDRPCookieRenderer : MonoBehaviour
{
    public Material cookieMaterial;
    public RenderTexture cookieTarget;
    public Vector2 scrollSpeed = new Vector2(0.05f, 0);
    [Range(0.1f, 500f)] public float size = 1f;
    public bool previewInEditor = true;

    private HDAdditionalLightData hdLight;
    private Vector2 uvOffset;
    private double lastTime;

#if UNITY_EDITOR
    void OnEnable()
    {
        hdLight = GetComponent<HDAdditionalLightData>();
        if (hdLight && cookieTarget) hdLight.SetCookie(cookieTarget);
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
        lastTime = EditorApplication.timeSinceStartup;
    }

    void OnDisable()
    {
        EditorApplication.update -= EditorTick;
    }
#endif

    void Update()
    {
        if (Application.isPlaying)
            RenderCookie(Time.deltaTime);
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (!previewInEditor || !this) return;
        double now = EditorApplication.timeSinceStartup;
        double dt = now - lastTime;
        lastTime = now;
        RenderCookie((float)dt);
        SceneView.RepaintAll();
    }
#endif

    void RenderCookie(float delta)
    {
        if (cookieMaterial == null || cookieTarget == null) return;

        uvOffset += scrollSpeed * delta;

        // Invert size for intuitive "zoom"
        float tiling = 1f / Mathf.Max(0.0001f, size);

        cookieMaterial.SetVector("_Tiling", new Vector2(tiling, tiling));
        cookieMaterial.SetVector("_Offset", uvOffset);

        Graphics.Blit(null, cookieTarget, cookieMaterial);

        if (hdLight)
            hdLight.SetCookie(cookieTarget);
    }
}
