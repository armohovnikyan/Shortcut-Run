using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TagFieldAttribute))]
public class TagFieldDrawer: PropertyDrawer
{
    public override void OnGUI(
        Rect position, 
        SerializedProperty property, 
        GUIContent label)
    {
        property.stringValue = EditorGUI.TagField(
            position,
            label,
            property.stringValue);
    }
}
