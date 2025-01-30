using UnityEditor;
using UnityEngine;
using Yueby.Utils;

namespace Yueby
{
    [CustomEditor(typeof(MaterialSwitcher))]
    public class MaterialSwitcherInspector : Editor
    {
        private Vector2 _scrollPosition;
        private static bool _showConfigs = false;
        private readonly Color _warningColor = new Color(1f, 0.92f, 0.016f, 1f);
        private readonly Color _defaultColor = Color.white;
        private MaterialSwitcher _component;

        private void OnEnable()
        {
            _component = (MaterialSwitcher)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_component == null) return;

            try
            {
                // 配置名称使用更醒目的样式
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.BeginHorizontal("box");
                    var newConfigName = EditorGUILayout.TextField(
                        new GUIContent("Config Name", "The name of this material configuration"),
                        _component.configName,
                        EditorStyles.boldLabel
                    );

                    if (check.changed)
                    {
                        Undo.RecordObject(_component, "Change Config Name");
                        _component.configName = newConfigName;
                        EditorUtility.SetDirty(_component);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(5);

                // 优化打开窗口按钮的样式
                if (GUILayout.Button(
                    new GUIContent("Open Material Switcher", EditorGUIUtility.IconContent("d_Settings").image),
                    GUILayout.Height(30)))
                {
                    var window = EditorWindow.GetWindow<MaterialSwitcherEditorWindow>();
                    window.titleContent = new GUIContent("Material Switcher");
                    window.InitializeWithComponent(_component);
                    window.Show();
                }

                EditorGUILayout.Space(5);

                // 显示配置预览
                using (new EditorGUILayout.VerticalScope())
                {
                    _showConfigs = EditorGUILayout.Foldout(_showConfigs, "Renderer Configs", true);
                    if (_showConfigs && _component.rendererConfigs != null)
                    {
                        // 计算内容高度
                        float contentHeight = CalculateContentHeight(_component);
                        float maxHeight = 300f;
                        float scrollHeight = Mathf.Min(contentHeight, maxHeight);

                        _scrollPosition = EditorGUILayout.BeginScrollView(
                            _scrollPosition,
                            GUILayout.Height(scrollHeight)
                        );

                        EditorGUI.indentLevel++;

                        // 添加配置数量显示
                        EditorGUILayout.LabelField(
                            $"Total Configs: {_component.rendererConfigs.Count}",
                            EditorStyles.miniLabel
                        );

                        DrawRendererConfigs(_component);

                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndScrollView();
                    }
                }

                // 添加底部提示
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Open Material Switcher window to edit configurations.", MessageType.Info);
            }
            finally
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private float CalculateContentHeight(MaterialSwitcher component)
        {
            float height = EditorGUIUtility.singleLineHeight; // 标题行高度

            // 配置数量显示的高度
            height += EditorGUIUtility.singleLineHeight;

            foreach (var config in component.rendererConfigs)
            {
                if (config == null) continue;

                // 基础高度（标题行）
                height += EditorGUIUtility.singleLineHeight;

                // 路径显示的高度
                if (!string.IsNullOrEmpty(config.rendererPath))
                {
                    height += EditorGUIUtility.singleLineHeight;
                }

                // 材质数量显示的高度
                height += EditorGUIUtility.singleLineHeight;

                // 间距
                height += 4f; // 每个配置之间的间距
            }

            // 添加一些额外的padding
            height += 10f;

            return height;
        }

        private void DrawRendererConfigs(MaterialSwitcher component)
        {
            for (int i = 0; i < component.rendererConfigs.Count; i++)
            {
                var config = component.rendererConfigs[i];
                if (config == null) continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    // 添加配置状态图标
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var icon = config.materials.Count > 0
                            ? EditorGUIUtility.IconContent("d_Favorite")
                            : EditorGUIUtility.IconContent("d_console.warnicon");

                        GUILayout.Label(icon, GUILayout.Width(20));

                        // 配置标题和材质信息
                        var materialInfo = GetMaterialInfo(config);

                        // 使用更醒目的标题样式
                        var titleStyle = new GUIStyle(EditorStyles.boldLabel);
                        titleStyle.normal.textColor = config.materials.Count > 0 ? _defaultColor : _warningColor;
                        EditorGUILayout.LabelField($"{config.name} ({materialInfo})", titleStyle);
                    }

                    // 路径显示优化
                    if (!string.IsNullOrEmpty(config.rendererPath))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(20);
                            EditorGUILayout.LabelField(
                                new GUIContent("Path:", "Relative path to the renderer"),
                                new GUIContent(config.rendererPath),
                                EditorStyles.miniLabel
                            );
                        }
                    }

                    // 添加材质数量显示
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(20);
                        EditorGUILayout.LabelField(
                            $"Materials: {config.materials.Count}",
                            EditorStyles.miniLabel
                        );
                    }
                }

                EditorGUILayout.Space(2);
            }
        }

        private string GetMaterialInfo(MaterialSwitcher.RendererConfig config)
        {
            if (config.materials.Count > 0 &&
                config.appliedMaterialIndex >= 0 &&
                config.appliedMaterialIndex < config.materials.Count)
            {
                var currentMaterial = config.materials[config.appliedMaterialIndex];
                return currentMaterial != null ? currentMaterial.name : "None";
            }
            return "None";
        }
    }
}