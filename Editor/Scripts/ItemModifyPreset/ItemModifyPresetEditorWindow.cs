using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using Yueby.Utils;
using Yueby.ModalWindow;
using Object = UnityEngine.Object;

/// <summary>
/// 物品材质预设编辑器窗口 - 基础类
/// </summary>
public partial class ItemModifyPresetEditorWindow : EditorWindow
{
    // 选项卡枚举
    private enum TabType
    {
        Presets,    // 预设页面
        Groups      // 分组页面
    }

    // 当前选中的选项卡
    private TabType _currentTab = TabType.Presets;

    // 数据相关
    private ItemModifyPreset _target;
    private SerializedObject _serializedObject;

    private static ItemModifyPresetEditorWindow _window;

    // 显示窗口
    public static void ShowWindow(ItemModifyPreset target)
    {
        _window = GetWindow<ItemModifyPresetEditorWindow>("物品材质预设编辑器");
        _window.minSize = new Vector2(700, 500);
        _window._target = target;
        _window.Initialize();
        _window.Show();
    }

    // 初始化数据
    private void Initialize()
    {
        if (_target == null) return;

        _serializedObject = new SerializedObject(_target);

        // 初始化预设相关数据
        InitializePresets();

        // 初始化分组相关数据
        InitializeGroups();
    }

    // 窗口启用时调用
    private void OnEnable()
    {
        // 初始化数据
        if (_target != null)
        {
            Initialize();
        }
    }

    // 窗口禁用时调用
    private void OnDisable()
    {
        if (_target != null && !EditorApplication.isPlaying)
        {
            EditorUtility.SetDirty(_target);
        }
    }

    // GUI绘制
    private void OnGUI()
    {
        // 更新序列化对象
        _serializedObject.Update();

        // 绘制选项卡
        DrawTabs();

        // 根据当前选项卡绘制内容
        Rect contentRect = new(0, 30, position.width, position.height - 30);
        if (_currentTab == TabType.Presets)
        {
            // 绘制预设页面
            DrawPresetsTab(contentRect);
        }
        else
        {
            // 绘制分组页面
            DrawGroupsTab(contentRect);
        }

        // 应用修改
        _serializedObject.ApplyModifiedProperties();

        // 确保在编辑器模式下保存更改
        if (GUI.changed && !EditorApplication.isPlaying)
        {
            EditorUtility.SetDirty(_target);
        }
    }

    // 绘制选项卡
    private void DrawTabs()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // 预设选项卡
        bool presetsSelected = GUILayout.Toggle(_currentTab == TabType.Presets, "预设", EditorStyles.toolbarButton, GUILayout.Width(100));
        if (presetsSelected && _currentTab != TabType.Presets)
        {
            _currentTab = TabType.Presets;
            GUI.FocusControl(null); // 清除焦点，避免输入框保持焦点
        }

        // 分组选项卡
        bool groupsSelected = GUILayout.Toggle(_currentTab == TabType.Groups, "分组", EditorStyles.toolbarButton, GUILayout.Width(100));
        if (groupsSelected && _currentTab != TabType.Groups)
        {
            _currentTab = TabType.Groups;
            GUI.FocusControl(null); // 清除焦点，避免输入框保持焦点
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    #region 工具方法 - 用于优化重复代码

    /// <summary>
    /// 应用序列化对象修改并更新UI
    /// </summary>
    /// <param name="serializedObject">序列化对象</param>
    /// <param name="target">目标对象</param>
    /// <param name="repaintAction">重绘回调</param>
    private void ApplySerializedObjectChanges(SerializedObject serializedObject, UnityEngine.Object target, System.Action repaintAction = null)
    {
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        repaintAction?.Invoke();
    }

    /// <summary>
    /// 生成新的UUID
    /// </summary>
    /// <returns>新的UUID字符串</returns>
    private string GenerateNewUUID()
    {
        return System.Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 计算渲染器相对于根对象的路径
    /// </summary>
    /// <param name="renderer">渲染器</param>
    /// <param name="rootObject">根对象</param>
    /// <returns>相对路径字符串</returns>
    private string CalculateRendererPath(Renderer renderer, GameObject rootObject)
    {
        if (renderer == null || rootObject == null)
            return string.Empty;

        var pathParts = new System.Collections.Generic.List<string>();
        Transform current = renderer.transform;
        Transform componentRoot = rootObject.transform;

        // 从渲染器向上遍历，直到到达根对象（不包含根对象）
        while (current != null && current != componentRoot)
        {
            pathParts.Add(current.name);
            current = current.parent;
        }

        // 反转路径并用斜杠连接
        pathParts.Reverse();
        return string.Join("/", pathParts.ToArray());
    }

    /// <summary>
    /// 创建新的材质预设
    /// </summary>
    /// <param name="materialPresetsProperty">材质预设属性</param>
    /// <param name="presetName">预设名称</param>
    /// <param name="materials">材质数组</param>
    /// <param name="themeColor">主题色</param>
    /// <returns>新创建的预设索引</returns>
    private int CreateMaterialPreset(SerializedProperty materialPresetsProperty, string presetName, Material[] materials, Color themeColor)
    {
        int newIndex = materialPresetsProperty.arraySize;
        materialPresetsProperty.arraySize++;
        var newPreset = materialPresetsProperty.GetArrayElementAtIndex(newIndex);

        // 设置预设名称
        newPreset.FindPropertyRelative("presetName").stringValue = presetName;

        // 生成UUID
        var uuidProp = newPreset.FindPropertyRelative("_uuid");
        if (uuidProp != null)
        {
            uuidProp.stringValue = GenerateNewUUID();
        }

        // 设置材质数组
        var materialsProp = newPreset.FindPropertyRelative("materials");
        if (materialsProp != null && materials != null)
        {
            materialsProp.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++)
            {
                materialsProp.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            }
        }

        // 设置主题色
        var themeColorProp = newPreset.FindPropertyRelative("themeColor");
        if (themeColorProp != null)
        {
            themeColorProp.colorValue = themeColor;
        }

        return newIndex;
    }

    /// <summary>
    /// 创建新的渲染器预设
    /// </summary>
    /// <param name="rendererPresetsProperty">渲染器预设属性</param>
    /// <param name="rendererName">渲染器名称</param>
    /// <param name="renderer">渲染器引用</param>
    /// <param name="rendererPath">渲染器路径</param>
    /// <returns>新创建的预设索引</returns>
    private int CreateRendererPreset(SerializedProperty rendererPresetsProperty, string rendererName, Renderer renderer, string rendererPath)
    {
        int newIndex = rendererPresetsProperty.arraySize;
        rendererPresetsProperty.arraySize++;
        var newPreset = rendererPresetsProperty.GetArrayElementAtIndex(newIndex);

        // 设置渲染器名称
        newPreset.FindPropertyRelative("rendererName").stringValue = rendererName;

        // 设置渲染器引用
        newPreset.FindPropertyRelative("renderer").objectReferenceValue = renderer;

        // 设置渲染器路径
        newPreset.FindPropertyRelative("rendererPath").stringValue = rendererPath;

        // 生成UUID
        var uuidProp = newPreset.FindPropertyRelative("_uuid");
        if (uuidProp != null)
        {
            uuidProp.stringValue = GenerateNewUUID();
        }

        // 初始化材质预设列表（默认为空）
        var materialPresetsProp = newPreset.FindPropertyRelative("materialPresets");
        if (materialPresetsProp != null)
        {
            materialPresetsProp.ClearArray();
        }

        return newIndex;
    }

    /// <summary>
    /// 查找材质预设在渲染器预设中的索引
    /// </summary>
    /// <param name="rendererPreset">渲染器预设</param>
    /// <param name="materialUuid">材质UUID</param>
    /// <returns>材质预设索引，如果未找到返回0</returns>
    private int FindMaterialPresetIndex(ItemModifyPreset.RendererPreset rendererPreset, string materialUuid)
    {
        if (rendererPreset == null || string.IsNullOrEmpty(materialUuid))
            return 0;

        for (int i = 0; i < rendererPreset.materialPresets.Count; i++)
        {
            if (rendererPreset.materialPresets[i].UUID == materialUuid)
            {
                return i;
            }
        }
        return 0;
    }

    /// <summary>
    /// 创建材质预设选项列表
    /// </summary>
    /// <param name="rendererPreset">渲染器预设</param>
    /// <param name="selectedMaterialUuid">选中的材质UUID</param>
    /// <returns>选项列表、预设列表、选中索引和主题色</returns>
    private (System.Collections.Generic.List<string> options, System.Collections.Generic.List<ItemModifyPreset.MaterialPreset> presets, int selectedIndex, Color themeColor)
        CreateMaterialPresetOptions(ItemModifyPreset.RendererPreset rendererPreset, string selectedMaterialUuid)
    {
        var options = new System.Collections.Generic.List<string>();
        var presets = new System.Collections.Generic.List<ItemModifyPreset.MaterialPreset>();
        int selectedIndex = -1;
        Color themeColor = Color.white;

        if (rendererPreset != null)
        {
            for (int i = 0; i < rendererPreset.materialPresets.Count; i++)
            {
                var preset = rendererPreset.materialPresets[i];
                if (preset != null)
                {
                    options.Add(preset.presetName);
                    presets.Add(preset);

                    if (preset.UUID == selectedMaterialUuid)
                    {
                        selectedIndex = options.Count - 1;
                        themeColor = preset.themeColor;
                    }
                }
            }
        }

        return (options, presets, selectedIndex, themeColor);
    }

    /// <summary>
    /// 计算多个颜色的平均值
    /// </summary>
    /// <param name="colors">颜色列表</param>
    /// <returns>平均颜色</returns>
    private Color CalculateAverageColor(System.Collections.Generic.List<Color> colors)
    {
        if (colors == null || colors.Count == 0)
            return new Color(0.4f, 0.7f, 0.3f, 1f); // 默认绿色

        float r = 0, g = 0, b = 0, a = 0;
        foreach (var color in colors)
        {
            r += color.r;
            g += color.g;
            b += color.b;
            a += color.a;
        }

        return new Color(r / colors.Count, g / colors.Count, b / colors.Count, a / colors.Count);
    }

    #endregion
}
