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
}
