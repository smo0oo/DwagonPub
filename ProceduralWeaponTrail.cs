using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralWeaponTrail : MonoBehaviour
{
    [Header("Trail Tracking Points")]
    [Tooltip("The transform at the base of the blade (near the hilt).")]
    public Transform basePoint;
    [Tooltip("The transform at the tip of the blade.")]
    public Transform tipPoint;

    [Header("Trail Settings")]
    [Tooltip("How long the trail lasts in seconds before fading out.")]
    public float trailDuration = 0.3f;
    [Tooltip("Maximum number of frames the trail can track. Keep this reasonable (30-60) to save memory.")]
    public int maxFrames = 60;
    [Tooltip("The material applied to the trail. Needs a shader that supports vertex colors and two-sided rendering.")]
    public Material trailMaterial;

    [Header("State")]
    public bool isEmitting = false;

    // --- Mesh Data ---
    private Mesh trailMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // --- History Tracking ---
    private class TrailSection
    {
        public Vector3 BasePosition;
        public Vector3 TipPosition;
        public float TimeCreated;
    }
    private List<TrailSection> sections = new List<TrailSection>();

    // --- Pre-allocated Arrays (Zero Garbage Collection) ---
    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uvs;
    private Color[] colors;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (trailMaterial != null)
        {
            meshRenderer.material = trailMaterial;
        }

        // --- Shadow Optimization ---
        // Prevent the 2D ribbon from casting weird barcode shadows on the environment
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // Initialize the dynamic mesh
        trailMesh = new Mesh();
        trailMesh.name = "WeaponTrailMesh";
        meshFilter.mesh = trailMesh;

        // Pre-allocate memory to prevent stuttering during combat
        vertices = new Vector3[maxFrames * 2];
        triangles = new int[(maxFrames - 1) * 6];
        uvs = new Vector2[maxFrames * 2];
        colors = new Color[maxFrames * 2];
    }

    /// <summary>
    /// Turns the trail on. Called by Animation Events.
    /// </summary>
    public void StartTrail()
    {
        isEmitting = true;
        ClearTrail(); // Ensure we don't connect a previous swing to this new swing!
    }

    /// <summary>
    /// Turns the trail off. Called by Animation Events.
    /// </summary>
    public void StopTrail()
    {
        isEmitting = false;
    }

    /// <summary>
    /// Instantly wipes the trail from the screen.
    /// </summary>
    public void ClearTrail()
    {
        sections.Clear();
        trailMesh.Clear();
    }

    void LateUpdate()
    {
        // 1. Record a new section if we are actively swinging
        if (isEmitting && basePoint != null && tipPoint != null)
        {
            sections.Add(new TrailSection
            {
                BasePosition = basePoint.position,
                TipPosition = tipPoint.position,
                TimeCreated = Time.time
            });
        }

        // 2. Remove old sections that have expired past the trailDuration
        while (sections.Count > 0 && Time.time > sections[0].TimeCreated + trailDuration)
        {
            sections.RemoveAt(0);
        }

        // Safety limit: Don't exceed our pre-allocated memory
        while (sections.Count > maxFrames)
        {
            sections.RemoveAt(0);
        }

        // 3. Rebuild the mesh
        UpdateMesh();
    }

    private void UpdateMesh()
    {
        trailMesh.Clear();

        if (sections.Count < 2) return;

        int vertexCount = 0;
        int triangleCount = 0;

        for (int i = 0; i < sections.Count; i++)
        {
            TrailSection currentSection = sections[i];

            // Calculate how old this section is (0.0 = brand new, 1.0 = about to expire)
            float age = Time.time - currentSection.TimeCreated;
            float lifePercent = 1f - (age / trailDuration);
            lifePercent = Mathf.Clamp01(lifePercent);

            // Create 2 vertices per section (one at the base, one at the tip)
            // Convert world space positions to local space so the mesh moves correctly with the weapon root
            vertices[vertexCount] = transform.InverseTransformPoint(currentSection.BasePosition);
            vertices[vertexCount + 1] = transform.InverseTransformPoint(currentSection.TipPosition);

            // UV Mapping: 
            // X-axis (u) tracks along the length of the trail (0 at the tail, 1 at the head)
            // Y-axis (v) tracks across the blade (0 at the base, 1 at the tip)
            float u = (float)i / (sections.Count - 1);
            uvs[vertexCount] = new Vector2(u, 0f);
            uvs[vertexCount + 1] = new Vector2(u, 1f);

            // Vertex Colors: Fade the alpha out as the trail gets older (near the tail)
            Color fadeColor = new Color(1f, 1f, 1f, lifePercent);
            colors[vertexCount] = fadeColor;
            colors[vertexCount + 1] = fadeColor;

            // Generate Triangles (We connect this section to the previous section)
            if (i > 0)
            {
                int previousBase = vertexCount - 2;
                int previousTip = vertexCount - 1;
                int currentBase = vertexCount;
                int currentTip = vertexCount + 1;

                // Triangle 1
                triangles[triangleCount] = previousBase;
                triangles[triangleCount + 1] = previousTip;
                triangles[triangleCount + 2] = currentBase;

                // Triangle 2
                triangles[triangleCount + 3] = currentBase;
                triangles[triangleCount + 4] = previousTip;
                triangles[triangleCount + 5] = currentTip;

                triangleCount += 6;
            }

            vertexCount += 2;
        }

        // Apply data to the mesh
        trailMesh.vertices = vertices;
        trailMesh.uv = uvs;
        trailMesh.colors = colors;

        // Only assign the exact number of triangles we actually used this frame
        int[] activeTriangles = new int[triangleCount];
        System.Array.Copy(triangles, activeTriangles, triangleCount);
        trailMesh.triangles = activeTriangles;

        // Recalculate bounds so Unity knows where the mesh is (prevents frustum culling from hiding it)
        trailMesh.RecalculateBounds();
        trailMesh.RecalculateNormals();
    }
}