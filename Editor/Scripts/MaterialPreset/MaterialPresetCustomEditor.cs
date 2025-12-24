using UnityEditor;
using UnityEngine;
using Yueby.Tools.AvatarToolbox.MaterialPreset;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor
{
    [CustomEditor(typeof(MaterialPresetManager))]
    public class MaterialPresetCustomEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Open Window Button
            if (GUILayout.Button("Open Editor Window", GUILayout.Height(30)))
            {
                MaterialPresetEditorWindow.ShowWindow();
            }

            EditorGUILayout.Space();

            // Default Inspector
            DrawDefaultInspector();
        }
    }
}

