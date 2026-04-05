using UnityEngine;
using UnityEngine.UI;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager instance;

    [Header("Map Dimensions")]
    public Vector3 mapCenter = Vector3.zero;
    public float mapSize = 1000f;

    [Header("Fog Texture")]
    public int fogResolution = 2048;
    public RawImage uiFogOverlay;
    [HideInInspector]
    public RenderTexture fogMaskTexture;

    [Header("Painting Settings")]
    public Texture2D brushTexture;
    public Material paintMaterial;
    public float brushSizeWorld = 50f;
    public float paintDistanceThreshold = 2f;

    private Vector3 lastPaintedPos = new Vector3(9999f, 9999f, 9999f);

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        InitializeFogTexture();
    }

    private void InitializeFogTexture()
    {
        fogMaskTexture = new RenderTexture(fogResolution, fogResolution, 0, RenderTextureFormat.ARGB32);
        fogMaskTexture.name = "FogOfWarMask";
        fogMaskTexture.filterMode = FilterMode.Bilinear;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = previous;

        if (uiFogOverlay != null) uiFogOverlay.texture = fogMaskTexture;

        Shader.SetGlobalTexture("_GlobalFogMask", fogMaskTexture);
        Shader.SetGlobalVector("_FogMapParams", new Vector4(mapCenter.x, mapSize, mapCenter.z, 0));
    }

    public void PaintFog(Vector3 worldPos)
    {
        if (fogMaskTexture == null || brushTexture == null || paintMaterial == null) return;

        if (Vector3.Distance(worldPos, lastPaintedPos) < paintDistanceThreshold) return;
        lastPaintedPos = worldPos;

        float pctX = (worldPos.x - mapCenter.x) / mapSize + 0.5f;
        float pctZ = (worldPos.z - mapCenter.z) / mapSize + 0.5f;

        float pixelX = pctX * fogResolution;

        // --- AAA FIX: Invert the Z-to-Y mapping ---
        // Because GL.LoadPixelMatrix puts 0,0 at the top-left, moving North (+Z) 
        // natively paints towards the bottom. We invert it so North paints North.
        float pixelY = (1.0f - pctZ) * fogResolution;

        float brushPixelSize = (brushSizeWorld / mapSize) * fogResolution;

        Rect drawRect = new Rect(pixelX - (brushPixelSize / 2f), pixelY - (brushPixelSize / 2f), brushPixelSize, brushPixelSize);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, fogResolution, fogResolution, 0);
        Graphics.DrawTexture(drawRect, brushTexture, paintMaterial);
        GL.PopMatrix();

        RenderTexture.active = previous;
        Shader.SetGlobalTexture("_GlobalFogMask", fogMaskTexture);
    }

    public void RevealArea(Vector3 worldPos, float revealRadius)
    {
        if (fogMaskTexture == null || brushTexture == null || paintMaterial == null) return;

        float pctX = (worldPos.x - mapCenter.x) / mapSize + 0.5f;
        float pctZ = (worldPos.z - mapCenter.z) / mapSize + 0.5f;

        float pixelX = pctX * fogResolution;

        // --- AAA FIX: Invert the Z-to-Y mapping here as well ---
        float pixelY = (1.0f - pctZ) * fogResolution;

        float brushPixelSize = (revealRadius / mapSize) * fogResolution;

        Rect drawRect = new Rect(pixelX - (brushPixelSize / 2f), pixelY - (brushPixelSize / 2f), brushPixelSize, brushPixelSize);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, fogResolution, fogResolution, 0);
        Graphics.DrawTexture(drawRect, brushTexture, paintMaterial);
        GL.PopMatrix();

        RenderTexture.active = previous;
        Shader.SetGlobalTexture("_GlobalFogMask", fogMaskTexture);
    }

    public string GetFogSaveData()
    {
        if (fogMaskTexture == null) return "";
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;
        Texture2D cpuTexture = new Texture2D(fogMaskTexture.width, fogMaskTexture.height, TextureFormat.RGB24, false);
        cpuTexture.ReadPixels(new Rect(0, 0, fogMaskTexture.width, fogMaskTexture.height), 0, 0);
        cpuTexture.Apply();
        RenderTexture.active = previous;
        byte[] pngData = cpuTexture.EncodeToPNG();
        Destroy(cpuTexture);
        return System.Convert.ToBase64String(pngData);
    }

    public void LoadFogSaveData(string base64FogData)
    {
        if (string.IsNullOrEmpty(base64FogData)) return;
        byte[] pngData = System.Convert.FromBase64String(base64FogData);
        Texture2D savedTexture = new Texture2D(2, 2);
        savedTexture.LoadImage(pngData);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;
        Graphics.Blit(savedTexture, fogMaskTexture);
        RenderTexture.active = previous;
        Destroy(savedTexture);

        Shader.SetGlobalTexture("_GlobalFogMask", fogMaskTexture);
    }
}