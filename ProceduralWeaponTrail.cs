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

    [Header("Smoothing & AAA Processing")]
    [Tooltip("Subdivides the frames into extra geometric segments to create a perfectly smooth curve. 3 to 5 is ideal.")]
    [Range(1, 8)] public int smoothingSegments = 3;
    [Tooltip("Prevents overlapping vertices if the weapon is moving too slowly.")]
    public float minVertexDistance = 0.02f;
    [Tooltip("Controls the width of the blade along the length of the trail. Left side (0) is the tail, right side (1) is the head.")]
    public AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

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

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        trailMesh = new Mesh();
        trailMesh.name = "WeaponTrailMesh";
        meshFilter.mesh = trailMesh;

        int maxPoints = maxFrames * Mathf.Max(1, smoothingSegments);
        vertices = new Vector3[maxPoints * 2];
        triangles = new int[(maxPoints - 1) * 6];
        uvs = new Vector2[maxPoints * 2];
        colors = new Color[maxPoints * 2];
    }

    public void StartTrail()
    {
        isEmitting = true;
        ClearTrail();
    }

    public void StopTrail()
    {
        isEmitting = false;
    }

    public void ClearTrail()
    {
        sections.Clear();
        trailMesh.Clear();
    }

    void LateUpdate()
    {
        if (isEmitting && basePoint != null && tipPoint != null)
        {
            bool shouldAdd = true;
            if (sections.Count > 0)
            {
                TrailSection lastSec = sections[sections.Count - 1];
                float distBase = Vector3.Distance(lastSec.BasePosition, basePoint.position);
                float distTip = Vector3.Distance(lastSec.TipPosition, tipPoint.position);

                if (distBase < minVertexDistance && distTip < minVertexDistance)
                {
                    shouldAdd = false;
                }
            }

            if (shouldAdd)
            {
                sections.Add(new TrailSection
                {
                    BasePosition = basePoint.position,
                    TipPosition = tipPoint.position,
                    TimeCreated = Time.time
                });
            }
        }

        while (sections.Count > 0 && Time.time > sections[0].TimeCreated + trailDuration)
        {
            sections.RemoveAt(0);
        }

        while (sections.Count > maxFrames)
        {
            sections.RemoveAt(0);
        }

        UpdateMesh();
    }

    private void UpdateMesh()
    {
        trailMesh.Clear();

        int validSectionCount = sections.Count;
        if (validSectionCount < 2) return;

        int vertexCount = 0;
        int triangleCount = 0;

        int totalGeneratedPoints = ((validSectionCount - 1) * smoothingSegments) + 1;
        int currentPointIndex = 0;

        for (int i = 0; i < validSectionCount - 1; i++)
        {
            TrailSection p0 = sections[Mathf.Max(0, i - 1)];
            TrailSection p1 = sections[i];
            TrailSection p2 = sections[i + 1];
            TrailSection p3 = sections[Mathf.Min(validSectionCount - 1, i + 2)];

            int limit = (i == validSectionCount - 2) ? smoothingSegments : smoothingSegments - 1;

            for (int j = 0; j <= limit; j++)
            {
                float t = (float)j / smoothingSegments;

                Vector3 interpBase = GetCatmullRomPosition(t, p0.BasePosition, p1.BasePosition, p2.BasePosition, p3.BasePosition);
                Vector3 interpTip = GetCatmullRomPosition(t, p0.TipPosition, p1.TipPosition, p2.TipPosition, p3.TipPosition);

                float u = (float)currentPointIndex / (totalGeneratedPoints - 1);

                float widthMultiplier = widthCurve.Evaluate(u);
                interpTip = Vector3.Lerp(interpBase, interpTip, widthMultiplier);

                vertices[vertexCount] = transform.InverseTransformPoint(interpBase);
                vertices[vertexCount + 1] = transform.InverseTransformPoint(interpTip);

                uvs[vertexCount] = new Vector2(u, 0f);
                uvs[vertexCount + 1] = new Vector2(u, 1f);

                Color fadeColor = new Color(1f, 1f, 1f, u);
                colors[vertexCount] = fadeColor;
                colors[vertexCount + 1] = fadeColor;

                if (currentPointIndex > 0)
                {
                    int prevBase = vertexCount - 2;
                    int prevTip = vertexCount - 1;
                    int currBase = vertexCount;
                    int currTip = vertexCount + 1;

                    triangles[triangleCount] = prevBase;
                    triangles[triangleCount + 1] = prevTip;
                    triangles[triangleCount + 2] = currBase;

                    triangles[triangleCount + 3] = currBase;
                    triangles[triangleCount + 4] = prevTip;
                    triangles[triangleCount + 5] = currTip;

                    triangleCount += 6;
                }

                vertexCount += 2;
                currentPointIndex++;
            }
        }

        trailMesh.vertices = vertices;
        trailMesh.uv = uvs;
        trailMesh.colors = colors;

        int[] activeTriangles = new int[triangleCount];
        System.Array.Copy(triangles, activeTriangles, triangleCount);
        trailMesh.triangles = activeTriangles;

        trailMesh.RecalculateBounds();
        trailMesh.RecalculateNormals();
    }

    private Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 a = 2f * p1;
        Vector3 b = p2 - p0;
        Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;

        return 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
    }
}