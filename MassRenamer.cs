using UnityEngine;
using UnityEditor;
using System.Linq;

public class MassRenamer : EditorWindow
{
    private string _baseName = "";
    private string _prefix = "";
    private string _suffix = "";

    private bool _useBaseName = true;
    private bool _usePrefix = false;
    private bool _useSuffix = false;

    private bool _useNumbering = true;
    private int _startNumber = 0;
    private int _numberPadding = 2; // How many zeros (e.g., 01, 001)

    private Vector2 _scrollPos;

    [MenuItem("Tools/Mass Renamer")]
    public static void ShowWindow()
    {
        GetWindow<MassRenamer>("Mass Renamer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Mass Renamer Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // --- Configuration Section ---

        // Base Name
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _useBaseName = EditorGUILayout.ToggleLeft("Rename Base Object", _useBaseName);
        if (_useBaseName)
        {
            _baseName = EditorGUILayout.TextField("New Base Name", _baseName);
        }
        else
        {
            EditorGUILayout.HelpBox("Original object names will be kept.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // Prefix
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _usePrefix = EditorGUILayout.ToggleLeft("Add Prefix", _usePrefix);
        if (_usePrefix)
        {
            _prefix = EditorGUILayout.TextField("Prefix", _prefix);
        }
        EditorGUILayout.EndVertical();

        // Suffix
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _useSuffix = EditorGUILayout.ToggleLeft("Add Suffix", _useSuffix);
        if (_useSuffix)
        {
            _suffix = EditorGUILayout.TextField("Suffix", _suffix);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // Numbering
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _useNumbering = EditorGUILayout.ToggleLeft("Add Number Iteration", _useNumbering);
        if (_useNumbering)
        {
            _startNumber = EditorGUILayout.IntField("Start Number", _startNumber);
            _numberPadding = EditorGUILayout.IntSlider("Zero Padding (001)", _numberPadding, 1, 5);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // --- Action Section ---

        GameObject[] selectedObjects = Selection.gameObjects;
        // Sort by hierarchy order so numbering makes sense visually
        selectedObjects = selectedObjects.OrderBy(go => go.transform.GetSiblingIndex()).ToArray();

        int count = selectedObjects.Length;

        GUI.enabled = count > 0;
        if (GUILayout.Button($"Rename {count} Selected Object(s)"))
        {
            RenameObjects(selectedObjects);
        }
        GUI.enabled = true;

        if (count == 0)
        {
            EditorGUILayout.HelpBox("Select GameObjects in the scene or hierarchy to rename.", MessageType.Warning);
        }
        else
        {
            // Preview
            EditorGUILayout.LabelField("Preview of first item:", EditorStyles.boldLabel);
            string previewName = GenerateName(selectedObjects[0].name, _startNumber);
            EditorGUILayout.LabelField(previewName);
        }

        EditorGUILayout.EndScrollView();
    }

    private void RenameObjects(GameObject[] objects)
    {
        // Register undo so you can Ctrl+Z if you make a mistake
        Undo.RecordObjects(objects, "Mass Rename");

        int currentNumber = _startNumber;

        foreach (GameObject obj in objects)
        {
            obj.name = GenerateName(obj.name, currentNumber);

            if (_useNumbering)
            {
                currentNumber++;
            }
        }
    }

    private string GenerateName(string originalName, int number)
    {
        string finalName = _useBaseName ? _baseName : originalName;

        if (_usePrefix)
        {
            finalName = _prefix + finalName;
        }

        if (_useSuffix)
        {
            finalName = finalName + _suffix;
        }

        if (_useNumbering)
        {
            string numberString = number.ToString("D" + _numberPadding);
            // Add a logical space or underscore before number if explicit base name is used, 
            // otherwise just append it.
            finalName += "_" + numberString;
        }

        return finalName;
    }
}