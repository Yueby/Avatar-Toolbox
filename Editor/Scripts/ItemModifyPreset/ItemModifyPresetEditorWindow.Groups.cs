using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using Yueby.Utils;

/// <summary>
/// 物品材质预设编辑器窗口 - 分组相关功能
/// </summary>
public partial class ItemModifyPresetEditorWindow
{
    // 分组相关属性
    private SerializedProperty _presetGroupsProperty;

    // 列表相关
    private SimpleReorderableList _groupsList;
    private SimpleReorderableList _groupEntriesList;
    private int _selectedGroupIndex = -1;

    // 滚动位置
    private Vector2 _leftGroupsScrollPosition;
    private Vector2 _rightGroupsScrollPosition;

    // 分割视图
    private SplitView _groupsSplitView;

    // 初始化分组相关数据
    private void InitializeGroups()
    {
        if (_target == null) return;

        _presetGroupsProperty = _serializedObject.FindProperty("presetGroups");

        // 创建分组列表
        CreateGroupsList();

        // 创建水平分割视图
        _groupsSplitView = new SplitView(
            250f, // 初始分割位置
            SplitView.SplitOrientation.Horizontal,
            "ItemModifyPresetEditorWindow.GroupsSplit" // 保存位置的键
        );

        // 使用默认分割线样式

        // 设置面板绘制回调
        _groupsSplitView.SetPanelDrawers(DrawGroupsLeftPanel, DrawGroupsRightPanel);
    }

    // 绘制分组页面
    private void DrawGroupsTab(Rect rect)
    {
        // 更新位置限制（随窗口大小变化）
        _groupsSplitView.SetPositionLimits(250f, rect.width - 200f);

        // 绘制分割视图
        _groupsSplitView.OnGUI(rect);
    }

    // 创建分组列表
    private void CreateGroupsList()
    {
        _groupsList = new SimpleReorderableList(
            _serializedObject,
            _presetGroupsProperty);

        // 设置列表标题
        _groupsList.Title = "预设分组";

        // 禁用自动增加数组大小，改为手动管理
        _groupsList.AutoIncreaseArraySize = false;

        // 如果有项目，默认选择第一个
        if (_presetGroupsProperty.arraySize > 0)
        {
            _groupsList.List.index = 0;
            _selectedGroupIndex = 0;
        }

        // 设置元素绘制回调
        _groupsList.OnDraw = (rect, index, isActive, isFocused) =>
        {
            var element = _presetGroupsProperty.GetArrayElementAtIndex(index);
            var groupNameProp = element.FindPropertyRelative("groupName");
            var entriesProp = element.FindPropertyRelative("entries");
            var themeColorProp = element.FindPropertyRelative("themeColor");
            var uuidProp = element.FindPropertyRelative("_uuid");

            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            // 绘制主题色标记
            var colorRect = new Rect(rect.x + 4, rect.y, 16, rect.height);
            EditorGUI.DrawRect(colorRect, themeColorProp.colorValue);
            // 绘制边框
            GUI.Box(new Rect(colorRect.x - 1, colorRect.y - 1, colorRect.width + 2, colorRect.height + 2), "");

            // 绘制分组名称
            var nameStyle = new GUIStyle(EditorStyles.boldLabel);
            nameStyle.normal.textColor = EditorGUIUtility.isProSkin ?
                Color.white : new Color(0.1f, 0.1f, 0.1f);

            var nameRect = new Rect(rect.x + 24, rect.y, rect.width - 70, rect.height);
            EditorGUI.LabelField(nameRect, groupNameProp.stringValue, nameStyle);

            // 添加应用按钮
            var buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 10;
            buttonStyle.alignment = TextAnchor.MiddleCenter;

            var applyButtonRect = new Rect(rect.x + rect.width - 40, rect.y, 40, rect.height);
            if (GUI.Button(applyButtonRect, "应用", buttonStyle))
            {
                if (uuidProp != null)
                {
                    // 应用当前绘制项的分组，而不是当前选中的分组
                    ApplyPresetGroup(uuidProp.stringValue);
                }
            }

            return EditorGUIUtility.singleLineHeight + 6; // 增加行高
        };

        // 设置列表选择回调
        _groupsList.OnSelected = (obj, index) =>
        {
            _selectedGroupIndex = index;

            // 重新创建条目列表
            if (_selectedGroupIndex >= 0 && _selectedGroupIndex < _presetGroupsProperty.arraySize)
            {
                CreateGroupEntriesList();
            }

            Repaint();
        };

        // 设置添加回调
        _groupsList.OnAdd = (list) =>
        {
            if (_target == null) return;

            // 先更新序列化对象，获取最新数据
            _serializedObject.UpdateIfRequiredOrScript();

            // 创建新分组名称
            string groupName = $"分组 {_target.presetGroups.Count + 1}";
            // 使用MaterialUtility生成随机颜色
            Color randomColor = MaterialUtility.GenerateRandomColor();

            // 使用SerializedProperty添加新分组
            int newIndex = _presetGroupsProperty.arraySize;
            _presetGroupsProperty.arraySize++;
            var newGroupProperty = _presetGroupsProperty.GetArrayElementAtIndex(newIndex);

            // 设置分组属性
            newGroupProperty.FindPropertyRelative("groupName").stringValue = groupName;
            newGroupProperty.FindPropertyRelative("themeColor").colorValue = randomColor;

            // 总是生成新的UUID
            var uuidProp = newGroupProperty.FindPropertyRelative("_uuid");
            uuidProp.stringValue = System.Guid.NewGuid().ToString();

            // 应用修改
            _serializedObject.ApplyModifiedProperties();

            // 更新序列化对象
            _serializedObject.UpdateIfRequiredOrScript();

            // 重新选择
            if (_presetGroupsProperty.arraySize > 0)
            {
                // 选择新添加的分组
                if (list != null && newIndex >= 0)
                {
                    list.index = newIndex;
                    _selectedGroupIndex = newIndex;
                }

                // 创建空的条目列表
                CreateGroupEntriesList();
            }

            EditorUtility.SetDirty(_target);
            Repaint();
        };
    }

    // 绘制左侧面板
    private void DrawGroupsLeftPanel(Rect rect)
    {
        GUILayout.BeginArea(rect);
        EditorGUILayout.BeginVertical();

        // _leftGroupsScrollPosition = EditorGUILayout.BeginScrollView(_leftGroupsScrollPosition);

        if (_groupsList != null)
        {
            _groupsList.DoLayout(GUILayout.ExpandWidth(true));
        }

        // EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
    }

    // 应用预设分组
    private void ApplyPresetGroup(string groupUuid)
    {
        if (_target == null || string.IsNullOrEmpty(groupUuid))
        {
            Debug.LogError("无法应用预设分组：目标为空或UUID为空");
            return;
        }

        // 保存编辑器变更
        EditorUtility.SetDirty(_target);

        try
        {
            // 先更新序列化对象，获取最新数据
            _serializedObject.UpdateIfRequiredOrScript();

            // 确保解析引用 - 应用预设组前必须解析
            _target.ResolveAllReferences();

            // 找到对应的分组
            var group = _target.FindPresetGroup(groupUuid);
            if (group == null)
            {
                Debug.LogError($"无法找到UUID为 {groupUuid} 的预设分组");
                return;
            }

            // 应用预设分组 - 现在ApplyPresetGroup内部不再调用ResolveAllReferences
            _target.ApplyPresetGroup(group);

            // 应用修改
            _serializedObject.ApplyModifiedProperties();

            // 确保对象标记为已修改
            EditorUtility.SetDirty(_target);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"应用预设分组时发生错误：{ex.Message}\n{ex.StackTrace}");
        }
    }

    // 绘制右侧面板
    private void DrawGroupsRightPanel(Rect rect)
    {
        GUILayout.BeginArea(rect);
        EditorGUILayout.BeginVertical();

        if (_selectedGroupIndex >= 0 && _selectedGroupIndex < _presetGroupsProperty.arraySize)
        {
            var groupElement = _presetGroupsProperty.GetArrayElementAtIndex(_selectedGroupIndex);
            var entriesProperty = groupElement.FindPropertyRelative("entries");
            var groupNameProp = groupElement.FindPropertyRelative("groupName");
            var themeColorProp = groupElement.FindPropertyRelative("themeColor");
            var uuidProp = groupElement.FindPropertyRelative("_uuid");

            // 分组基本信息面板
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("分组名称");

            // 使用EditorGUI.BeginChangeCheck检测输入框的变化
            EditorGUI.BeginChangeCheck();
            string newGroupName = EditorGUILayout.TextField(groupNameProp.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                // 如果发生变化，更新属性值并应用修改
                groupNameProp.stringValue = newGroupName;
                _serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(_target);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("主题颜色");

            // 同样检测颜色字段的变化
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUILayout.ColorField(themeColorProp.colorValue);
            if (EditorGUI.EndChangeCheck())
            {
                // 如果发生变化，更新属性值并应用修改
                themeColorProp.colorValue = newColor;
                _serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(_target);
            }

            if (GUILayout.Button("自动", GUILayout.Width(50)))
            {
                UpdateGroupThemeColor(_selectedGroupIndex);
            }
            EditorGUILayout.EndHorizontal();

            // 显示UUID信息并添加重置按钮
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("UUID");

            // 显示UUID值（只读）
            GUI.enabled = false;
            EditorGUILayout.TextField(uuidProp.stringValue);
            GUI.enabled = true;

            // 添加重置UUID按钮
            if (GUILayout.Button("重置", GUILayout.Width(50)))
            {
                uuidProp.stringValue = System.Guid.NewGuid().ToString();
                _serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(_target);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // _rightGroupsScrollPosition = EditorGUILayout.BeginScrollView(_rightGroupsScrollPosition);

            // 创建条目列表
            if (_groupEntriesList == null)
            {
                CreateGroupEntriesList();
            }

            if (_groupEntriesList != null)
            {
                _groupEntriesList.DoLayout(GUILayout.ExpandWidth(true));
            }

            // EditorGUILayout.EndScrollView();
        }
        else
        {
            // 简洁提示
            EditorGUILayout.LabelField("请选择一个分组", EditorStyles.boldLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
    }

    // 创建分组条目列表
    private void CreateGroupEntriesList()
    {
        if (_selectedGroupIndex < 0 || _selectedGroupIndex >= _presetGroupsProperty.arraySize)
            return;

        // 解析引用，确保在创建列表时数据是最新的
        if (_target != null)
        {
            _target.ResolveAllReferences();
        }

        var groupElement = _presetGroupsProperty.GetArrayElementAtIndex(_selectedGroupIndex);
        var entriesProperty = groupElement.FindPropertyRelative("entries");

        // 创建条目列表
        _groupEntriesList = new SimpleReorderableList(
            _serializedObject,
            entriesProperty);

        // 设置列表标题
        _groupEntriesList.Title = "预设条目";

        // 禁用自动增加数组大小，改为手动管理
        _groupEntriesList.AutoIncreaseArraySize = false;

        // 如果有项目，默认选择第一个
        if (entriesProperty.arraySize > 0)
        {
            _groupEntriesList.List.index = 0;
        }

        // 设置元素绘制回调
        _groupEntriesList.OnDraw = (rect, index, isActive, isFocused) =>
        {
            var element = entriesProperty.GetArrayElementAtIndex(index);
            var rendererUuidProp = element.FindPropertyRelative("_rendererPresetUuid");
            var materialUuidProp = element.FindPropertyRelative("_materialPresetUuid");

            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            // 创建渲染器选项
            List<string> rendererOptions = new List<string>();
            List<ItemModifyPreset.RendererPreset> rendererPresets = new List<ItemModifyPreset.RendererPreset>();
            int selectedRendererIndex = -1;

            for (int i = 0; i < _target.rendererPresets.Count; i++)
            {
                var preset = _target.rendererPresets[i];
                if (preset != null)
                {
                    rendererOptions.Add(preset.rendererName);
                    rendererPresets.Add(preset);

                    if (preset.UUID == rendererUuidProp.stringValue)
                    {
                        selectedRendererIndex = rendererOptions.Count - 1;
                    }
                }
            }

            // 创建材质预设选项
            List<string> materialOptions = new List<string>();
            List<ItemModifyPreset.MaterialPreset> materialPresets = new List<ItemModifyPreset.MaterialPreset>();
            int selectedMaterialIndex = -1;
            Color materialColor = Color.white; // 默认颜色

            // 查找当前选中的渲染器预设
            var rendererPreset = _target.FindRendererPreset(rendererUuidProp.stringValue);
            if (rendererPreset != null)
            {
                for (int i = 0; i < rendererPreset.materialPresets.Count; i++)
                {
                    var preset = rendererPreset.materialPresets[i];
                    if (preset != null)
                    {
                        materialOptions.Add(preset.presetName);
                        materialPresets.Add(preset);

                        if (preset.UUID == materialUuidProp.stringValue)
                        {
                            selectedMaterialIndex = materialOptions.Count - 1;
                            // 直接使用MaterialPreset的themeColor，而不是从材质推导
                            materialColor = preset.themeColor;
                        }
                    }
                }
            }

            // 绘制索引编号
            var indexStyle = new GUIStyle(EditorStyles.miniLabel);
            indexStyle.alignment = TextAnchor.MiddleCenter;
            indexStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

            var indexRect = new Rect(rect.x, rect.y, 20, rect.height);
            EditorGUI.LabelField(indexRect, (index + 1).ToString(), indexStyle);

            // 绘制渲染器选择下拉框
            var rendererRect = new Rect(rect.x + 20, rect.y, rect.width * 0.4f - 25, rect.height);
            var popupStyle = new GUIStyle(EditorStyles.popup);
            popupStyle.alignment = TextAnchor.MiddleLeft;

            EditorGUI.BeginChangeCheck();
            int newRendererIndex = EditorGUI.Popup(rendererRect, selectedRendererIndex, rendererOptions.ToArray(), popupStyle);
            if (EditorGUI.EndChangeCheck() && newRendererIndex >= 0 && newRendererIndex < rendererPresets.Count)
            {
                // 更新渲染器UUID
                rendererUuidProp.stringValue = rendererPresets[newRendererIndex].UUID;

                // 重置材质UUID，因为渲染器变了
                if (rendererPresets[newRendererIndex].materialPresets.Count > 0)
                {
                    materialUuidProp.stringValue = rendererPresets[newRendererIndex].materialPresets[0].UUID;
                }
                else
                {
                    materialUuidProp.stringValue = "";
                }

                // 应用修改
                _serializedObject.ApplyModifiedProperties();

                // 更新主题色
                UpdateGroupThemeColor(_selectedGroupIndex);
            }

            // 绘制材质预设选择下拉框
            var materialRect = new Rect(rect.x + rect.width * 0.4f + 5, rect.y, rect.width * 0.4f - 15, rect.height);
            EditorGUI.BeginChangeCheck();
            int newMaterialIndex = EditorGUI.Popup(materialRect, selectedMaterialIndex, materialOptions.ToArray(), popupStyle);
            if (EditorGUI.EndChangeCheck() && newMaterialIndex >= 0 && newMaterialIndex < materialPresets.Count)
            {
                // 更新材质UUID
                materialUuidProp.stringValue = materialPresets[newMaterialIndex].UUID;

                // 如果是第一个条目，并且有其他条目，询问是否同步更新后面所有条目
                if (index == 0 && entriesProperty.arraySize > 1)
                {
                    bool shouldSyncAll = EditorUtility.DisplayDialog(
                        "同步更新",
                        "是否将此材质预设索引同步应用到所有后续条目？",
                        "是", "否");

                    if (shouldSyncAll)
                    {
                        // 遍历所有其他条目
                        for (int i = 1; i < entriesProperty.arraySize; i++)
                        {
                            var otherEntry = entriesProperty.GetArrayElementAtIndex(i);
                            var otherRendererUuidProp = otherEntry.FindPropertyRelative("_rendererPresetUuid");

                            // 查找对应渲染器预设
                            var otherRendererPreset = _target.FindRendererPreset(otherRendererUuidProp.stringValue);
                            if (otherRendererPreset != null)
                            {
                                // 确保这个渲染器预设有足够多的材质预设
                                if (otherRendererPreset.materialPresets.Count > newMaterialIndex)
                                {
                                    // 更新到相同索引的材质预设
                                    var otherMaterialUuidProp = otherEntry.FindPropertyRelative("_materialPresetUuid");
                                    otherMaterialUuidProp.stringValue = otherRendererPreset.materialPresets[newMaterialIndex].UUID;
                                }
                            }
                        }
                    }
                }

                // 应用修改
                _serializedObject.ApplyModifiedProperties();

                // 更新主题色
                UpdateGroupThemeColor(_selectedGroupIndex);
            }

            // 绘制材质颜色方块
            var colorRect = new Rect(rect.x + rect.width - 30, rect.y + 2, 20, rect.height - 4);
            EditorGUI.DrawRect(colorRect, materialColor);
            // 绘制边框
            GUI.Box(new Rect(colorRect.x - 1, colorRect.y - 1, colorRect.width + 2, colorRect.height + 2), "");

            return EditorGUIUtility.singleLineHeight + 6; // 增加行高
        };

        // 设置列表选择回调
        _groupEntriesList.OnSelected = (obj, index) =>
        {
            Repaint();
        };

        // 设置添加回调
        _groupEntriesList.OnAdd = (list) =>
        {
            // 检查各种条件
            if (_target == null || _selectedGroupIndex < 0 || _selectedGroupIndex >= _presetGroupsProperty.arraySize)
                return;

            // 检查是否有渲染器预设
            if (_target.rendererPresets.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有可用的渲染器预设。请先在预设页面创建渲染器预设。", "确定");
                return;
            }

            // 先更新序列化对象，获取最新数据
            _serializedObject.UpdateIfRequiredOrScript();

            // 解析引用
            _target.ResolveAllReferences();

            // 获取当前分组的条目列表
            var entriesProperty = _presetGroupsProperty.GetArrayElementAtIndex(_selectedGroupIndex).FindPropertyRelative("entries");

            // 获取渲染器预设
            int rendererIndex = 0;
            // 如果有多个渲染器预设，尝试选择一个不同的
            if (_target.rendererPresets.Count > 1)
            {
                var group = _target.presetGroups[_selectedGroupIndex];
                if (group.entries.Count > 0)
                {
                    // 尝试找到一个未使用的渲染器预设
                    var usedRendererUuids = new HashSet<string>();
                    foreach (var entry in group.entries)
                    {
                        usedRendererUuids.Add(entry.RendererPresetUuid);
                    }

                    // 查找未使用的渲染器预设
                    for (int i = 0; i < _target.rendererPresets.Count; i++)
                    {
                        if (!usedRendererUuids.Contains(_target.rendererPresets[i].UUID))
                        {
                            rendererIndex = i;
                            break;
                        }
                    }
                }
            }

            var selectedRendererPreset = _target.rendererPresets[rendererIndex];
            if (selectedRendererPreset.materialPresets.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", $"渲染器预设 '{selectedRendererPreset.rendererName}' 没有材质预设。请先在预设页面创建材质预设。", "确定");
                return;
            }

            // 查找上一个条目使用的材质预设索引
            int materialPresetIndex = 0;

            if (entriesProperty.arraySize > 0)
            {
                // 获取最后一个条目
                var lastEntry = entriesProperty.GetArrayElementAtIndex(entriesProperty.arraySize - 1);
                var lastRendererUuid = lastEntry.FindPropertyRelative("_rendererPresetUuid").stringValue;
                var lastMaterialUuid = lastEntry.FindPropertyRelative("_materialPresetUuid").stringValue;

                // 查找上一个渲染器预设
                var lastRendererPreset = _target.FindRendererPreset(lastRendererUuid);
                if (lastRendererPreset != null)
                {
                    // 查找上一个材质预设在其渲染器预设中的索引
                    for (int i = 0; i < lastRendererPreset.materialPresets.Count; i++)
                    {
                        if (lastRendererPreset.materialPresets[i].UUID == lastMaterialUuid)
                        {
                            // 找到索引，检查新渲染器是否有足够的材质预设
                            if (i < selectedRendererPreset.materialPresets.Count)
                            {
                                materialPresetIndex = i;
                            }
                            break;
                        }
                    }
                }
            }

            // 获取选择的材质预设
            var firstMaterialPreset = selectedRendererPreset.materialPresets[materialPresetIndex];

            // 检查是否已存在相同渲染器的条目
            bool foundExisting = false;
            string rendererUuid = selectedRendererPreset.UUID;

            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                var entryProp = entriesProperty.GetArrayElementAtIndex(i);
                var rendererUuidProp = entryProp.FindPropertyRelative("_rendererPresetUuid");

                if (rendererUuidProp.stringValue == rendererUuid)
                {
                    // 更新现有条目
                    var materialUuidProp = entryProp.FindPropertyRelative("_materialPresetUuid");
                    materialUuidProp.stringValue = firstMaterialPreset.UUID;
                    foundExisting = true;
                    break;
                }
            }

            // 如果没有找到现有条目，添加新条目
            if (!foundExisting)
            {
                int newIndex = entriesProperty.arraySize;
                entriesProperty.arraySize++;

                var newEntryProp = entriesProperty.GetArrayElementAtIndex(newIndex);
                newEntryProp.FindPropertyRelative("_rendererPresetUuid").stringValue = selectedRendererPreset.UUID;
                newEntryProp.FindPropertyRelative("_materialPresetUuid").stringValue = firstMaterialPreset.UUID;
            }

            // 应用修改
            _serializedObject.ApplyModifiedProperties();

            // 更新序列化对象
            _serializedObject.UpdateIfRequiredOrScript();

            // 选择新添加的条目
            if (list != null && !foundExisting)
            {
                int newIndex = entriesProperty.arraySize - 1;
                list.index = newIndex;
            }

            // 更新主题色
            UpdateGroupThemeColor(_selectedGroupIndex);

            EditorUtility.SetDirty(_target);
            Repaint();
        };
    }

    // 更新分组主题色
    private void UpdateGroupThemeColor(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= _presetGroupsProperty.arraySize)
            return;

        // 先更新序列化对象，获取最新数据
        _serializedObject.UpdateIfRequiredOrScript();

        // 解析所有引用
        _target.ResolveAllReferences();

        // 获取分组
        if (groupIndex < _target.presetGroups.Count)
        {
            var group = _target.presetGroups[groupIndex];

            // 收集所有材质预设的主题色
            List<Color> colors = new List<Color>();
            foreach (var entry in group.entries)
            {
                if (entry.MaterialPreset != null)
                {
                    colors.Add(entry.MaterialPreset.themeColor);
                }
            }

            // 计算平均色
            if (colors.Count > 0)
            {
                group.themeColor = CalculateAverageColor(colors);
            }
        }

        // 应用修改
        _serializedObject.ApplyModifiedProperties();

        // 标记为已修改
        EditorUtility.SetDirty(_target);
    }

    // 计算多个颜色的平均值
    private Color CalculateAverageColor(List<Color> colors)
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

    // 计算两个颜色之间的差异（欧几里得距离）
    private float ColorDifference(Color a, Color b)
    {
        return Mathf.Sqrt(
            Mathf.Pow(a.r - b.r, 2) +
            Mathf.Pow(a.g - b.g, 2) +
            Mathf.Pow(a.b - b.b, 2)
        );
    }
}
