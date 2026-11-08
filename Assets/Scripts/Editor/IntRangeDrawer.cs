using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(IntRange))]
public class IntRangeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect r = EditorGUI.PrefixLabel(position, label);

        SerializedProperty min = property.FindPropertyRelative("min");
        SerializedProperty max = property.FindPropertyRelative("max");

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float dash = 14f;
        float w = (r.width - dash) * 0.5f;
        Rect a = new Rect(r.x, r.y, w, r.height);
        Rect d = new Rect(r.x + w, r.y, dash, r.height);
        Rect b = new Rect(r.x + w + dash, r.y, w, r.height);

        EditorGUI.BeginChangeCheck();
        int newMin = EditorGUI.IntField(a, min.intValue);
        EditorGUI.LabelField(d, "–", EditorStyles.centeredGreyMiniLabel);
        int newMax = EditorGUI.IntField(b, max.intValue);
        if (EditorGUI.EndChangeCheck())
        {
            newMin = Mathf.Max(0, newMin);
            min.intValue = newMin;
            max.intValue = Mathf.Max(newMin, newMax);
        }

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
