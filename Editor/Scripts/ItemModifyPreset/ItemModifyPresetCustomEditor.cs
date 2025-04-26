using UnityEngine;
using UnityEditor;
using Yueby.Utils;

[CustomEditor(typeof(ItemModifyPreset))]
public class ItemModifyPresetCustomEditor : Editor
{
    private ItemModifyPreset _target;
    
    private void OnEnable()
    {
        _target = (ItemModifyPreset)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorUI.VerticalEGL(() => 
        {
            EditorGUILayout.HelpBox("本组件用于管理多个渲染器的材质预设，每个渲染器可配置多套材质方案。", MessageType.Info);
            
            if (GUILayout.Button("打开预设编辑器", GUILayout.Height(40)))
            {
                ItemModifyPresetEditorWindow.ShowWindow(_target);
            }
        });
        
        serializedObject.ApplyModifiedProperties();
        
        // 确保在编辑器模式下保存更改
        if (GUI.changed && !EditorApplication.isPlaying)
        {
            EditorUtility.SetDirty(_target);
        }
    }
}
