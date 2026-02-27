using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

[Serializable]
public class FolderCategory
{
    public string categoryName = "New Category";
    public bool isExpanded = true;
    public List<string> folderPaths = new List<string>();
}

[Serializable]
public class FavoriteFoldersData
{
    public List<FolderCategory> categories = new List<FolderCategory>();
}

public class FavoriteFoldersWindow : EditorWindow
{
    private FavoriteFoldersData data = new FavoriteFoldersData();
    private Vector2 scrollPosition;
    private string prefsKey;
    private string newCategoryInput = "";

    [MenuItem("Window/General/Favorite Folders")]
    public static void ShowWindow()
    {
        FavoriteFoldersWindow window = GetWindow<FavoriteFoldersWindow>("Favorite Folders");
        window.minSize = new Vector2(280, 400);
        window.Show();
    }

    private void OnEnable()
    {
        prefsKey = $"FavoriteFolders_Categorized_{Application.dataPath.GetHashCode()}";
        LoadFavorites();

        // Ensure we always have at least one category to drop things into
        if (data.categories.Count == 0)
        {
            data.categories.Add(new FolderCategory() { categoryName = "General" });
            SaveFavorites();
        }
    }

    private void OnGUI()
    {
        DrawTopToolbar();
        DrawCategoryList();
        DrawGlobalDropZone();
    }

    private void DrawTopToolbar()
    {
        EditorGUILayout.Space(5);

        GUILayout.BeginHorizontal();
        newCategoryInput = EditorGUILayout.TextField(newCategoryInput, EditorStyles.toolbarSearchField);

        if (GUILayout.Button("Add Category", EditorStyles.miniButton, GUILayout.Width(100)))
        {
            if (!string.IsNullOrWhiteSpace(newCategoryInput))
            {
                data.categories.Add(new FolderCategory() { categoryName = newCategoryInput });
                newCategoryInput = "";
                GUI.FocusControl(null); // Deselect text field
                SaveFavorites();
            }
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Clean Up All Missing Folders", GUILayout.Height(25)))
        {
            foreach (var category in data.categories)
            {
                category.folderPaths.RemoveAll(path => !AssetDatabase.IsValidFolder(path));
            }
            SaveFavorites();
        }

        DrawUILine(Color.gray, 1, 10);
    }

    private void DrawCategoryList()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < data.categories.Count; i++)
        {
            DrawCategory(data.categories[i], i);
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCategory(FolderCategory category, int index)
    {
        // Category Header Area
        GUILayout.BeginHorizontal();

        category.isExpanded = EditorGUILayout.Foldout(category.isExpanded, "", true);

        // Editable Category Name
        category.categoryName = EditorGUILayout.TextField(category.categoryName, EditorStyles.boldLabel);

        // Delete Category Button
        GUI.color = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25)))
        {
            if (EditorUtility.DisplayDialog("Delete Category", $"Are you sure you want to delete '{category.categoryName}' and all its shortcuts?", "Yes", "Cancel"))
            {
                data.categories.RemoveAt(index);
                SaveFavorites();
                GUIUtility.ExitGUI(); // Prevent layout errors during loop
            }
        }
        GUI.color = Color.white;

        GUILayout.EndHorizontal();

        // Draw the Folders inside a "Box" so we have a distinct visual area for Drag & Drop
        if (category.isExpanded)
        {
            Rect dropRect = EditorGUILayout.BeginVertical("box");

            if (category.folderPaths.Count == 0)
            {
                GUILayout.Label("Drag folders here...", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int j = 0; j < category.folderPaths.Count; j++)
                {
                    DrawFolderShortcut(category, j);
                }
            }

            EditorGUILayout.EndVertical();

            // Handle Drag and Drop for this specific category
            HandleDragAndDrop(dropRect, category);
        }
    }

    private void DrawFolderShortcut(FolderCategory category, int folderIndex)
    {
        string path = category.folderPaths[folderIndex];
        bool isValid = AssetDatabase.IsValidFolder(path);

        GUI.color = isValid ? Color.white : new Color(1f, 0.5f, 0.5f);

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        GUIContent folderIcon = EditorGUIUtility.IconContent(isValid ? "Folder Icon" : "console.erroricon.sml");
        GUILayout.Label(folderIcon, GUILayout.Width(20), GUILayout.Height(20));

        string folderName = Path.GetFileName(path);
        GUIStyle buttonStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Normal };

        if (GUILayout.Button(new GUIContent(folderName, path), buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(20)))
        {
            if (isValid)
            {
                OpenAndSelectFolder(path);
            }
            else
            {
                Debug.LogWarning($"[Favorite Folders] The directory '{path}' no longer exists.");
            }
        }

        GUI.color = Color.white;

        if (GUILayout.Button(new GUIContent("", "Remove shortcut"), EditorStyles.miniButtonRight, GUILayout.Width(25), GUILayout.Height(20)))
        {
            category.folderPaths.RemoveAt(folderIndex);
            SaveFavorites();
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawGlobalDropZone()
    {
        GUILayout.FlexibleSpace();

        // Fallback drop zone at the very bottom. Drops into the first category.
        Rect dropArea = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "\nGlobal Drop Zone (Adds to first category)", EditorStyles.helpBox);

        if (data.categories.Count > 0)
        {
            HandleDragAndDrop(dropArea, data.categories[0]);
        }
    }

    private void HandleDragAndDrop(Rect dropArea, FolderCategory targetCategory)
    {
        Event evt = Event.current;

        if (!dropArea.Contains(evt.mousePosition)) return;

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:

                bool containsValidFolder = false;
                foreach (string path in DragAndDrop.paths)
                {
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        containsValidFolder = true;
                        break;
                    }
                }

                if (containsValidFolder)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        bool changesMade = false;
                        foreach (string path in DragAndDrop.paths)
                        {
                            if (AssetDatabase.IsValidFolder(path) && !targetCategory.folderPaths.Contains(path))
                            {
                                targetCategory.folderPaths.Add(path);
                                changesMade = true;
                            }
                        }

                        if (changesMade)
                        {
                            // Optional: Alphabetize folders within the category
                            targetCategory.folderPaths = targetCategory.folderPaths.OrderBy(p => Path.GetFileName(p)).ToList();
                            SaveFavorites();
                        }
                    }
                    evt.Use();
                }
                break;
        }
    }

    private void OpenAndSelectFolder(string path)
    {
        DefaultAsset folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        if (folderAsset != null)
        {
            // Focus the Project Window first
            EditorUtility.FocusProjectWindow();

            // Set the object as selected
            Selection.activeObject = folderAsset;

            // Ping it so it highlights in the hierarchy tree
            EditorGUIUtility.PingObject(folderAsset);

            // Force Unity to "double-click" and dive into the folder
            AssetDatabase.OpenAsset(folderAsset);
        }
    }

    private void SaveFavorites()
    {
        string json = JsonUtility.ToJson(data);
        EditorPrefs.SetString(prefsKey, json);
    }

    private void LoadFavorites()
    {
        if (EditorPrefs.HasKey(prefsKey))
        {
            string json = EditorPrefs.GetString(prefsKey);
            if (!string.IsNullOrEmpty(json))
            {
                data = JsonUtility.FromJson<FavoriteFoldersData>(json);
            }
        }
        else
        {
            data = new FavoriteFoldersData();
        }
    }

    private void DrawUILine(Color color, int thickness = 2, int padding = 10)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, color);
    }
}