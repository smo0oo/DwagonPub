using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Linq;

// This tells Unity to use this drawer for any field of type SequenceEffect.
[CustomPropertyDrawer(typeof(SequenceEffect))]
public class SequenceEffectDrawer : PropertyDrawer
{
    private ReorderableList reorderableList;

    private ReorderableList GetReorderableList(SerializedProperty property)
    {
        if (reorderableList == null)
        {
            SerializedProperty listProperty = property.FindPropertyRelative("actions");
            reorderableList = new ReorderableList(property.serializedObject, listProperty, true, true, true, true);

            // --- Header ---
            reorderableList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "Action Sequence");
            };

            // --- How to draw each element ---
            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty element = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
                rect.y += 2;

                // Get the type name to display as the header for the element
                string typeName = element.managedReferenceFullTypename.Split('.').LastOrDefault();

                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, new GUIContent(typeName), true);
            };

            // --- Get the height of each element ---
            reorderableList.elementHeightCallback = (int index) =>
            {
                return EditorGUI.GetPropertyHeight(reorderableList.serializedProperty.GetArrayElementAtIndex(index));
            };

            // --- THIS IS THE CRITICAL PART: The Add button dropdown ---
            reorderableList.onAddDropdownCallback = (Rect buttonRect, ReorderableList l) =>
            {
                var menu = new GenericMenu();

                // Find all classes that inherit from SequenceAction
                var actionTypes = TypeCache.GetTypesDerivedFrom<SequenceAction>();

                foreach (var type in actionTypes)
                {
                    // Add an item to the menu for each action type
                    menu.AddItem(new GUIContent(type.Name), false, () => {
                        l.serializedProperty.serializedObject.Update();

                        // Create an instance of the chosen type and add it to the list
                        l.serializedProperty.InsertArrayElementAtIndex(l.serializedProperty.arraySize);
                        SerializedProperty newElement = l.serializedProperty.GetArrayElementAtIndex(l.serializedProperty.arraySize - 1);
                        newElement.managedReferenceValue = Activator.CreateInstance(type);

                        l.serializedProperty.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.ShowAsContext();
            };
        }
        return reorderableList;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GetReorderableList(property).DoList(position);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return GetReorderableList(property).GetHeight();
    }
}