using UnityEngine;
using UnityEngine.UI;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager instance;

    [Header("Map Dimensions")]
    [Tooltip("The exact center of your 3D world map.")]
    public Vector3 mapCenter = Vector3.zero;
    [Tooltip("How wide/tall the world map is in Unity units.")]
    public float mapSize = 1000f;

    [Header("Fog Texture")]
    [Tooltip("Higher resolution means smoother fog edges, but costs more RAM. 1024 or 2048 is standard.")]
    public int fogResolution = 2048;
    [Tooltip("If assigned, the script will automatically apply the fog mask to this UI image.")]
    public RawImage uiFogOverlay;
    [HideInInspector]
    public RenderTexture fogMaskTexture;

    [Header("Painting Settings")]
    [Tooltip("The soft white circle texture we will stamp onto the map.")]
    public Texture2D brushTexture;
    [Tooltip("The Additive UI material we created (Mat_FogInk).")]
    public Material paintMaterial;
    [Tooltip("How large the brush is in world units (e.g., 50) for the wagon.")]
    public float brushSizeWorld = 50f;
    [Tooltip("Optimization: Only paint if the wagon has moved this far since the last paint.")]
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
        // 0 depth buffer because we don't need a camera anymore
        fogMaskTexture = new RenderTexture(fogResolution, fogResolution, 0, RenderTextureFormat.ARGB32);
        fogMaskTexture.name = "FogOfWarMask";
        fogMaskTexture.filterMode = FilterMode.Bilinear;

        // Fill canvas with pure black
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = previous;

        if (uiFogOverlay != null)
        {
            uiFogOverlay.texture = fogMaskTexture;
        }

        // Broadcast the texture and map dimensions so any 3D Shader Graph can read it
        Shader.SetGlobalTexture("_GlobalFogMask", fogMaskTexture);
        Shader.SetGlobalVector("_FogMapParams", new Vector4(mapCenter.x, mapSize, mapCenter.z, 0));
    }

    /// <summary>
    /// Explicitly stamps the brush texture onto the digital canvas.
    /// </summary>
    public void PaintFog(Vector3 worldPos)
    {
        if (fogMaskTexture == null || brushTexture == null || paintMaterial == null) return;

        // Optimization: Don't stamp 140 times a second if we are standing still
        if (Vector3.Distance(worldPos, lastPaintedPos) < paintDistanceThreshold) return;
        lastPaintedPos = worldPos;

        // 1. Convert World Position to 2D Percentage (0.0 to 1.0)
        float pctX = (worldPos.x - mapCenter.x) / mapSize + 0.5f;
        float pctZ = (worldPos.z - mapCenter.z) / mapSize + 0.5f;

        // 2. Convert Percentage to exact Pixel Coordinates on the texture
        float pixelX = pctX * fogResolution;
        float pixelY = pctZ * fogResolution;

        // 3. Convert Brush World Size to Brush Pixel Size
        float brushPixelSize = (brushSizeWorld / mapSize) * fogResolution;

        // 4. Create the bounding box for the stamp
        Rect drawRect = new Rect(pixelX - (brushPixelSize / 2f), pixelY - (brushPixelSize / 2f), brushPixelSize, brushPixelSize);

        // 5. Explicitly draw onto the RenderTexture
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, fogResolution, fogResolution, 0); // Set up 2D drawing matrix

        Graphics.DrawTexture(drawRect, brushTexture, paintMaterial); // STAMP

        GL.PopMatrix();
        RenderTexture.active = previous;
    }

    /// <summary>
    /// Instantly punches a custom-sized hole in the fog. 
    /// Perfect for Watchtowers, Towns, or massive reveals.
    /// </summary>
    public void RevealArea(Vector3 worldPos, float revealRadius)
    {
        if (fogMaskTexture == null || brushTexture == null || paintMaterial == null) return;

        // 1. Convert World Position to 2D Percentage (0.0 to 1.0)
        float pctX = (worldPos.x - mapCenter.x) / mapSize + 0.5f;
        float pctZ = (worldPos.z - mapCenter.z) / mapSize + 0.5f;

        // 2. Convert Percentage to exact Pixel Coordinates
        float pixelX = pctX * fogResolution;
        float pixelY = pctZ * fogResolution;

        // 3. Convert Custom Radius to Pixel Size
        float brushPixelSize = (revealRadius / mapSize) * fogResolution;

        // 4. Create the bounding box
        Rect drawRect = new Rect(pixelX - (brushPixelSize / 2f), pixelY - (brushPixelSize / 2f), brushPixelSize, brushPixelSize);

        // 5. Stamp the texture
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, fogResolution, fogResolution, 0);

        Graphics.DrawTexture(drawRect, brushTexture, paintMaterial);

        GL.PopMatrix();
        RenderTexture.active = previous;
    }

    /// <summary>
    /// Extracts the fog texture from the GPU, compresses it to a PNG, and returns it as a string.
    /// Call this from your SaveManager right before the game saves.
    /// </summary>
    public string GetFogSaveData()
    {
        if (fogMaskTexture == null) return "";

        // Temporarily lock the active render texture
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;

        // Create a temporary Texture2D in standard RAM to read the GPU data
        Texture2D cpuTexture = new Texture2D(fogMaskTexture.width, fogMaskTexture.height, TextureFormat.RGB24, false);

        // Read the pixels from the GPU into the CPU
        cpuTexture.ReadPixels(new Rect(0, 0, fogMaskTexture.width, fogMaskTexture.height), 0, 0);
        cpuTexture.Apply();

        RenderTexture.active = previous;

        // Compress it down to a PNG byte array
        byte[] pngData = cpuTexture.EncodeToPNG();

        // Destroy the temporary texture immediately to prevent memory leaks
        Destroy(cpuTexture);

        // Convert the raw bytes into a Base64 String
        return System.Convert.ToBase64String(pngData);
    }

    /// <summary>
    /// Takes a saved Base64 string, converts it back into an image, and permanently stamps it onto the map.
    /// Call this from your SaveManager when loading a game.
    /// </summary>
    public void LoadFogSaveData(string base64FogData)
    {
        if (string.IsNullOrEmpty(base64FogData)) return;

        // Convert the string back into bytes
        byte[] pngData = System.Convert.FromBase64String(base64FogData);

        // Load the bytes back into a Texture2D (Unity automatically sizes it based on the PNG data)
        Texture2D savedTexture = new Texture2D(2, 2);
        savedTexture.LoadImage(pngData);

        // Stamp the saved texture permanently onto the active Fog RenderTexture
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = fogMaskTexture;

        Graphics.Blit(savedTexture, fogMaskTexture);

        RenderTexture.active = previous;

        // Clean up the RAM
        Destroy(savedTexture);
    }
}