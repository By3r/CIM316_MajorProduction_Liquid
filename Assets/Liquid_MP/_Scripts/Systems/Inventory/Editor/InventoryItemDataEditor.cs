using UnityEditor;
using UnityEngine;

namespace _Scripts.Systems.Inventory.Editor
{
    /// <summary>
    /// Custom editor for <see cref="InventoryItemData"/> that shows the itemType
    /// dropdown ONLY on the base class (Basic Items like Misc, PowerCell, KeyItem).
    /// Derived types (WeaponItemData, SchematicItemData, etc.) auto-set itemType
    /// so the field is hidden to avoid confusion.
    /// </summary>
    [CustomEditor(typeof(InventoryItemData), editorForChildClasses: true)]
    public class InventoryItemDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw all properties, inserting itemType only for the base class.
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Always skip itemType — we draw it manually when needed.
                if (iterator.name == "itemType")
                {
                    // Only show if this is exactly the base class, not a derived type.
                    if (target.GetType() == typeof(InventoryItemData))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                    continue;
                }

                // Skip the script field from being editable.
                if (iterator.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
