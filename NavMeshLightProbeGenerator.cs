using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using System.Collections.Generic;

public class NavMeshLightProbeGenerator : EditorWindow
{
    [Header("Placement Settings")]
    [SerializeField] private float minProbeSpacing = 3.0f;
    [SerializeField] private float edgeBuffer = 1.0f;

    [Header("Volume Layers")]
    [SerializeField] private bool generateTwoLayers = true;
    [SerializeField] private float floorHeightOffset = 0.2f;
    [SerializeField] private float headHeightOffset = 1.6f;

    [MenuItem("Tools/Level Design/Generate NavMesh Light Probes")]
    public static void ShowWindow()
    {
        GetWindow<NavMeshLightProbeGenerator>("Probe Generator");
    }

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("NavMesh Light Probe Generator", EditorStyles.boldLabel);
        GUILayout.Label("Automatically places light probes along the walkable NavMesh.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        minProbeSpacing = EditorGUILayout.FloatField(new GUIContent("Min Spacing (m)", "Minimum distance between probe columns."), minProbeSpacing);
        edgeBuffer = EditorGUILayout.FloatField(new GUIContent("Edge Buffer (m)", "Pushes probes away from walls."), edgeBuffer);

        GUILayout.Space(10);
        GUILayout.Label("Volume Settings (Fixes Spaghetti Lines)", EditorStyles.boldLabel);
        generateTwoLayers = EditorGUILayout.Toggle(new GUIContent("Generate 3D Volume", "Generates a floor and head probe at each point to fix tessellation errors and improve lighting."), generateTwoLayers);

        if (generateTwoLayers)
        {
            floorHeightOffset = EditorGUILayout.FloatField("Floor Height (m)", floorHeightOffset);
            headHeightOffset = EditorGUILayout.FloatField("Head Height (m)", headHeightOffset);
        }
        else
        {
            headHeightOffset = EditorGUILayout.FloatField("Height Offset (m)", headHeightOffset);
        }

        GUILayout.Space(15);

        if (GUILayout.Button("Generate Light Probes", GUILayout.Height(30)))
        {
            GenerateProbes();
        }
    }

    private void GenerateProbes()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        if (triangulation.vertices.Length == 0)
        {
            Debug.LogWarning("No NavMesh found! Please ensure you have baked a NavMesh in the scene first.");
            return;
        }

        List<Vector3> rawPoints = new List<Vector3>(triangulation.vertices);

        for (int i = 0; i < triangulation.indices.Length; i += 3)
        {
            Vector3 v1 = triangulation.vertices[triangulation.indices[i]];
            Vector3 v2 = triangulation.vertices[triangulation.indices[i + 1]];
            Vector3 v3 = triangulation.vertices[triangulation.indices[i + 2]];
            Vector3 center = (v1 + v2 + v3) / 3f;
            rawPoints.Add(center);
        }

        List<Vector3> validColumns = new List<Vector3>();

        foreach (Vector3 originalPoint in rawPoints)
        {
            Vector3 adjustedPoint = originalPoint;

            if (edgeBuffer > 0f)
            {
                if (NavMesh.FindClosestEdge(adjustedPoint, out NavMeshHit edgeHit, NavMesh.AllAreas))
                {
                    if (edgeHit.distance < edgeBuffer)
                    {
                        float pushDistance = (edgeBuffer - edgeHit.distance) + 0.1f;
                        Vector3 pushedPosition = adjustedPoint - (edgeHit.normal * pushDistance);

                        if (NavMesh.SamplePosition(pushedPosition, out NavMeshHit safeHit, 1.0f, NavMesh.AllAreas))
                        {
                            adjustedPoint = safeHit.position;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }

            bool isTooClose = false;
            foreach (Vector3 existingColumn in validColumns)
            {
                // Only measure horizontal distance for spacing!
                float dist = Vector2.Distance(new Vector2(adjustedPoint.x, adjustedPoint.z), new Vector2(existingColumn.x, existingColumn.z));
                if (dist < minProbeSpacing)
                {
                    isTooClose = true;
                    break;
                }
            }

            if (!isTooClose)
            {
                validColumns.Add(adjustedPoint);
            }
        }

        if (validColumns.Count == 0)
        {
            Debug.LogWarning("No valid probe positions found. Try reducing the Edge Buffer or Min Spacing.");
            return;
        }

        // Generate the final list of 3D points
        List<Vector3> finalProbePositions = new List<Vector3>();

        foreach (Vector3 col in validColumns)
        {
            if (generateTwoLayers)
            {
                finalProbePositions.Add(col + (Vector3.up * floorHeightOffset));
                finalProbePositions.Add(col + (Vector3.up * headHeightOffset));
            }
            else
            {
                finalProbePositions.Add(col + (Vector3.up * headHeightOffset));
            }
        }

        GameObject probeObject = new GameObject("NavMesh_LightProbes");
        LightProbeGroup probeGroup = probeObject.AddComponent<LightProbeGroup>();
        probeGroup.probePositions = finalProbePositions.ToArray();

        Undo.RegisterCreatedObjectUndo(probeObject, "Generate NavMesh Light Probes");

        Debug.Log($"Successfully generated {finalProbePositions.Count} Light Probes!");
        Selection.activeGameObject = probeObject;
    }
}