using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Yueby.Utils;
using Yueby.ModalWindow;
using Object = UnityEngine.Object;

/// <summary>
/// 物品材质预设编辑器窗口 - 预设相关功能
/// </summary>
public partial class ItemModifyPresetEditorWindow
{
    // 预设相关属性
    private SerializedProperty _rendererPresetsProperty;

    // 列表相关
    private SimpleReorderableList _rendererPresetsList;
    private SimpleReorderableList _materialPresetsList;
    private int _selectedRendererIndex = -1;
    private int _selectedMaterialIndex = -1;

    // 滚动位置
    private Vector2 _leftScrollPosition;
    private Vector2 _rightScrollPosition;
    private Vector2 _detailScrollPosition;

    // 分割视图
    private SplitView _horizontalSplitView;
    private SplitView _verticalSplitView;

    // 拖放处理器
    private DragDropHandler _materialDragDropHandler;

    // 绘制预设页面
    private void DrawPresetsTab(Rect rect)
    {
        // 更新位置限制（随窗口大小变化）
        _horizontalSplitView.SetPositionLimits(250f, rect.width - 200f); // 增加最小宽度以适应SimpleReorderableList
        _verticalSplitView.SetPositionLimits(300f, rect.height - 200f); // 增加最小高度以容纳渲染器信息

        // 绘制分割视图
        _horizontalSplitView.OnGUI(rect);
    }

    // 初始化预设相关数据
    private void InitializePresets()
    {
        if (_target == null) return;

        _rendererPresetsProperty = _serializedObject.FindProperty("rendererPresets");

        // 创建渲染器预设列表
        CreateRendererPresetsList();

        // 如果有渲染器预设，创建材质预设列表
        if (_selectedRendererIndex >= 0 && _selectedRendererIndex < _rendererPresetsProperty.arraySize)
        {
            CreateMaterialPresetsList();
        }

        // 创建水平分割视图
        _horizontalSplitView = new SplitView(
            250f, // 初始分割位置，增加宽度以适应SimpleReorderableList
            SplitView.SplitOrientation.Horizontal,
            "ItemModifyPresetEditorWindow.HorizontalSplit" // 保存位置的键
        );

        // 设置面板绘制回调
        _horizontalSplitView.SetPanelDrawers(DrawLeftPanel, DrawRightPanel);

        // 创建垂直分割视图
        _verticalSplitView = new SplitView(
            300f, // 初始分割位置，增加高度以容纳渲染器信息和预设列表
            SplitView.SplitOrientation.Vertical,
            "ItemModifyPresetEditorWindow.VerticalSplit" // 保存位置的键
        );

        // 设置面板绘制回调
        _verticalSplitView.SetPanelDrawers(DrawPresetListPanel, DrawPresetDetailPanel);

        // 初始化拖放处理器
        _materialDragDropHandler = new DragDropHandler
        {
            AcceptedTypes = new[] { typeof(Material) },
            HighlightColor = new Color(0.3f, 0.6f, 0.9f),
            ShowVisualFeedback = true,
            ShowOnlyWhenDragging = true
        };

        // 注册拖放事件回调
        _materialDragDropHandler.OnObjectsDropped = HandleMaterialsDropped;

        // 添加自定义绘制回调
        _materialDragDropHandler.OnDrawOverlay = (rect, state, isAccepted) =>
        {
            if (state != DragDropState.None)
            {
                var labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };

                if (isAccepted)
                {
                    labelStyle.normal.textColor = new Color(0.3f, 0.6f, 0.9f, 0.8f);
                    GUI.Label(rect, "拖放材质到此处创建新预设", labelStyle);
                }
                else
                {
                    labelStyle.normal.textColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
                    GUI.Label(rect, "只接受材质类型", labelStyle);
                }
            }
        };
    }

    // 创建渲染器预设列表
    private void CreateRendererPresetsList()
    {
        _rendererPresetsList = new SimpleReorderableList(
            _serializedObject,
            _rendererPresetsProperty);

        // 设置列表标题
        _rendererPresetsList.Title = "渲染器预设";

        // 禁用自动增加数组大小，改为手动管理，与材质预设列表保持一致
        _rendererPresetsList.AutoIncreaseArraySize = false;

        // 添加标题绘制回调，在标题区域添加“获取所有渲染器”按钮
        _rendererPresetsList.OnDrawTitle = () =>
        {
            if(_rendererPresetsProperty.arraySize > 0)
                return;

            GUILayout.FlexibleSpace(); // 将按钮推到右侧

            if (GUILayout.Button("获取所有渲染器"))
            {
                GetAllRenderers();
            }
        };

        // 如果有项目，默认选择第一个
        if (_rendererPresetsProperty.arraySize > 0)
        {
            _rendererPresetsList.List.index = 0;
            _selectedRendererIndex = 0;
        }

        // 设置元素绘制回调
        _rendererPresetsList.OnDraw = (rect, index, isActive, isFocused) =>
        {
            var element = _rendererPresetsProperty.GetArrayElementAtIndex(index);
            var rendererNameProp = element.FindPropertyRelative("rendererName");
            var materialPresetsProp = element.FindPropertyRelative("materialPresets");

            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            // 绘制渲染器名称 - 使用粗体
            var nameStyle = new GUIStyle(EditorStyles.boldLabel);
            nameStyle.normal.textColor = EditorGUIUtility.isProSkin ?
                Color.white : new Color(0.1f, 0.1f, 0.1f);

            var nameRect = new Rect(rect.x + 4, rect.y, rect.width - 80, rect.height);
            EditorGUI.LabelField(nameRect, rendererNameProp.stringValue, nameStyle);

            // 显示材质预设数量
            var countStyle = new GUIStyle(EditorStyles.miniLabel);
            countStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

            var countRect = new Rect(rect.x + rect.width - 75, rect.y + 2, 70, rect.height);
            EditorGUI.LabelField(countRect, $"预设: {materialPresetsProp.arraySize}", countStyle);

            return EditorGUIUtility.singleLineHeight + 6; // 增加行高
        };

        // 设置列表选择回调
        _rendererPresetsList.OnSelected = (obj, index) =>
        {
            _selectedRendererIndex = index;
            _selectedMaterialIndex = -1;

            // 重新创建材质预设列表
            if (_selectedRendererIndex >= 0 && _selectedRendererIndex < _rendererPresetsProperty.arraySize)
            {
                CreateMaterialPresetsList();
            }

            Repaint();
        };

        // 添加添加回调，使用 AddEmptyRenderer 方法
        _rendererPresetsList.OnAdd = (list) =>
        {
            AddEmptyRenderer();
        };
    }

    // 创建材质预设列表
    private void CreateMaterialPresetsList()
    {
        if (_selectedRendererIndex < 0 || _selectedRendererIndex >= _rendererPresetsProperty.arraySize)
            return;

        var rendererElement = _rendererPresetsProperty.GetArrayElementAtIndex(_selectedRendererIndex);
        var materialPresetsProperty = rendererElement.FindPropertyRelative("materialPresets");

        // 创建材质预设列表
        _materialPresetsList = new SimpleReorderableList(
            _serializedObject,
            materialPresetsProperty);

        // 设置列表标题
        _materialPresetsList.Title = "材质预设";

        // 禁用自动增加数组大小，改为手动管理
        _materialPresetsList.AutoIncreaseArraySize = false;

        // 如果有项目，默认选择第一个
        if (materialPresetsProperty.arraySize > 0)
        {
            _materialPresetsList.List.index = 0;
            _selectedMaterialIndex = 0;
        }

        // 设置元素绘制回调
        _materialPresetsList.OnDraw = (rect, index, isActive, isFocused) =>
        {
            var element = materialPresetsProperty.GetArrayElementAtIndex(index);
            var presetNameProp = element.FindPropertyRelative("presetName");
            var materialsProp = element.FindPropertyRelative("materials");
            var themeColorProp = element.FindPropertyRelative("themeColor");

            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            // 绘制主题色标记
            var colorRect = new Rect(rect.x + 4, rect.y, 16, rect.height);
            EditorGUI.DrawRect(colorRect, themeColorProp.colorValue);
            // 绘制边框
            GUI.Box(new Rect(colorRect.x - 1, colorRect.y - 1, colorRect.width + 2, colorRect.height + 2), "");

            // 绘制预设名称
            var nameStyle = new GUIStyle(EditorStyles.boldLabel);
            nameStyle.normal.textColor = EditorGUIUtility.isProSkin ?
                Color.white : new Color(0.1f, 0.1f, 0.1f);

            var nameRect = new Rect(rect.x + 24, rect.y, rect.width - 140, rect.height);
            EditorGUI.LabelField(nameRect, presetNameProp.stringValue, nameStyle);

            // 显示材质数量
            var countStyle = new GUIStyle(EditorStyles.miniLabel);
            countStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

            var countRect = new Rect(rect.x + rect.width - 125, rect.y + 2, 70, rect.height);
            EditorGUI.LabelField(countRect, $"材质: {materialsProp.arraySize}", countStyle);

            // 添加应用按钮
            var buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 10;
            buttonStyle.alignment = TextAnchor.MiddleCenter;

            var applyButtonRect = new Rect(rect.x + rect.width - 50, rect.y, 40, rect.height);
            if (GUI.Button(applyButtonRect, "应用", buttonStyle))
            {
                var renderer = rendererElement.FindPropertyRelative("renderer").objectReferenceValue as Renderer;
                if (renderer != null)
                {
                    Undo.RecordObject(renderer, "Apply Material Preset");
                    ApplyMaterialPreset(renderer, element);
                    rendererElement.FindPropertyRelative("currentPresetIndex").intValue = index;
                    EditorUtility.SetDirty(renderer);
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", "渲染器引用为空，无法应用预设", "确定");
                }
            }

            return EditorGUIUtility.singleLineHeight + 6; // 增加行高
        };

        // 设置列表选择回调
        _materialPresetsList.OnSelected = (obj, index) =>
        {
            _selectedMaterialIndex = index;
            Repaint();
        };

        // 设置添加回调
        _materialPresetsList.OnAdd = (list) =>
        {
            var rendererProp = rendererElement.FindPropertyRelative("renderer");
            var renderer = rendererProp.objectReferenceValue as Renderer;

            // 检查渲染器是否有效
            if (renderer == null)
            {
                EditorUtility.DisplayDialog("错误", "请先设置有效的渲染器引用", "确定");
                return;
            }

            // 先更新序列化对象，获取最新数据
            _serializedObject.UpdateIfRequiredOrScript();

            // 直接从组件获取当前的渲染器预设，无需通过UUID查找
            if (_selectedRendererIndex >= 0 && _selectedRendererIndex < _target.rendererPresets.Count)
            {
                var rendererPreset = _target.rendererPresets[_selectedRendererIndex];

                // 创建新预设名称
                string presetName = $"预设 {rendererPreset.materialPresets.Count + 1}";

                // 使用SerializedProperty添加新的材质预设
                var materialPresetsProperty = _rendererPresetsProperty
                    .GetArrayElementAtIndex(_selectedRendererIndex)
                    .FindPropertyRelative("materialPresets");

                int newIndex = materialPresetsProperty.arraySize;
                materialPresetsProperty.arraySize++;
                var newPreset = materialPresetsProperty.GetArrayElementAtIndex(newIndex);

                // 设置预设名称
                newPreset.FindPropertyRelative("presetName").stringValue = presetName;

                // 生成UUID
                var uuidProp = newPreset.FindPropertyRelative("_uuid");
                if (uuidProp != null)
                {
                    uuidProp.stringValue = System.Guid.NewGuid().ToString();
                }

                // 设置材质数组
                var materialsProp = newPreset.FindPropertyRelative("materials");
                if (materialsProp != null && renderer.sharedMaterials != null)
                {
                    materialsProp.arraySize = renderer.sharedMaterials.Length;
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        materialsProp.GetArrayElementAtIndex(i).objectReferenceValue = renderer.sharedMaterials[i];
                    }
                }

                // 设置主题色
                var themeColorProp = newPreset.FindPropertyRelative("themeColor");
                if (themeColorProp != null)
                {
                    themeColorProp.colorValue = MaterialUtility.ExtractThemeColorFromMaterial(
                        renderer.sharedMaterials.Length > 0 ? renderer.sharedMaterials[0] : null);
                }

                // 应用修改
                _serializedObject.ApplyModifiedProperties();

                // 更新序列化对象以获取最新状态
                _serializedObject.UpdateIfRequiredOrScript();

                // 选择新添加的预设
                list.index = newIndex;
                _selectedMaterialIndex = newIndex;

                EditorUtility.SetDirty(_target);
            }
            else
            {
                Debug.LogError($"无法找到渲染器预设：选中索引 {_selectedRendererIndex}，实际数量 {_target.rendererPresets.Count}");
            }
        };
    }

    // 绘制左侧面板
    private void DrawLeftPanel(Rect rect)
    {
        GUILayout.BeginArea(rect);
        EditorGUILayout.BeginVertical();

        // 恢复使用EditorGUILayout的滚动视图，简单可靠
        // _leftScrollPosition = EditorGUILayout.BeginScrollView(_leftScrollPosition);

        if (_rendererPresetsList != null)
        {
            // 使用标准的布局方式，确保列表正常显示
            _rendererPresetsList.DoLayout(GUILayout.ExpandWidth(true));
        }

        // EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
    }

    // 绘制右侧面板
    private void DrawRightPanel(Rect rect)
    {
        if (_selectedRendererIndex >= 0 && _selectedRendererIndex < _rendererPresetsProperty.arraySize)
        {
            // 确保序列化对象是最新的
            _serializedObject.UpdateIfRequiredOrScript();

            // 直接使用垂直分割视图绘制预设列表和详情
            _verticalSplitView.OnGUI(rect);
        }
        else
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.BeginVertical();
            // 简洁提示
            EditorGUILayout.LabelField("请选择一个渲染器", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }

    // 绘制预设列表面板（垂直分割视图的上半部分）
    private void DrawPresetListPanel(Rect rect)
    {
        GUILayout.BeginArea(rect);
        EditorGUILayout.BeginVertical();

        // 确保序列化对象是最新的
        _serializedObject.UpdateIfRequiredOrScript();

        var rendererElement = _rendererPresetsProperty.GetArrayElementAtIndex(_selectedRendererIndex);
        var rendererNameProp = rendererElement.FindPropertyRelative("rendererName");
        var rendererProp = rendererElement.FindPropertyRelative("renderer");

        // 渲染器基本信息面板 - 使用更紧凑的布局
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("渲染器名称");

        // 检测名称变化
        EditorGUI.BeginChangeCheck();
        string newName = EditorGUILayout.TextField(rendererNameProp.stringValue);
        if (EditorGUI.EndChangeCheck())
        {
            rendererNameProp.stringValue = newName;
            // 应用修改
            _serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_target);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("渲染器引用");

        // 检测引用变化
        EditorGUI.BeginChangeCheck();
        Object newRenderer = EditorGUILayout.ObjectField(rendererProp.objectReferenceValue, typeof(Renderer), true);
        if (EditorGUI.EndChangeCheck())
        {
            // 记录撤销操作
            Undo.RecordObject(_target, "Change Renderer Reference");

            // 更新引用
            rendererProp.objectReferenceValue = newRenderer;

            // 如果是新渲染器且名称为空，自动设置名称
            if (newRenderer != null && string.IsNullOrEmpty(rendererNameProp.stringValue))
            {
                rendererNameProp.stringValue = newRenderer.name;
            }

            // 更新渲染器路径
            if (newRenderer != null)
            {
                Renderer renderer = newRenderer as Renderer;
                if (renderer != null)
                {
                    string rendererPath = CalculateRendererPath(renderer, _target.gameObject);
                    rendererElement.FindPropertyRelative("rendererPath").stringValue = rendererPath;
                }
            }

            // 应用修改
            _serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_target);

            // 如果有效，重新创建材质预设列表
            if (_selectedRendererIndex >= 0 && _selectedRendererIndex < _rendererPresetsProperty.arraySize)
            {
                CreateMaterialPresetsList();
            }
        }

        EditorGUILayout.EndHorizontal();

        // 显示渲染器路径
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("渲染器路径");
        var rendererPathProp = rendererElement.FindPropertyRelative("rendererPath");

        // 如果路径为空但渲染器引用不为空，自动计算路径
        if (string.IsNullOrEmpty(rendererPathProp.stringValue) && rendererProp.objectReferenceValue != null)
        {
            Renderer renderer = rendererProp.objectReferenceValue as Renderer;
            if (renderer != null)
            {
                string rendererPath = CalculateRendererPath(renderer, _target.gameObject);
                rendererPathProp.stringValue = rendererPath;
                _serializedObject.ApplyModifiedProperties();
            }
        }

        EditorGUI.BeginDisabledGroup(true); // 路径信息只读，由系统自动计算
        EditorGUILayout.TextField(rendererPathProp.stringValue);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // 恢复滚动视图，参照Groups.cs的实现
        // _rightScrollPosition = EditorGUILayout.BeginScrollView(_rightScrollPosition);

        if (_materialPresetsList != null)
        {
            _materialPresetsList.DoLayout(GUILayout.ExpandWidth(true));
        }

        // EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
        GUILayout.EndArea();

        _materialDragDropHandler.DrawDragAndDrop(rect);
    }

    // 绘制预设详情面板（垂直分割视图的下半部分）
    private void DrawPresetDetailPanel(Rect rect)
    {
        GUILayout.BeginArea(rect);
        EditorGUILayout.BeginVertical();

        _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);

        if (_selectedRendererIndex >= 0 && _selectedRendererIndex < _rendererPresetsProperty.arraySize &&
            _selectedMaterialIndex >= 0)
        {
            // 确保序列化对象是最新的
            _serializedObject.UpdateIfRequiredOrScript();

            var rendererElement = _rendererPresetsProperty.GetArrayElementAtIndex(_selectedRendererIndex);
            var materialPresetsProperty = rendererElement.FindPropertyRelative("materialPresets");

            if (_selectedMaterialIndex < materialPresetsProperty.arraySize)
            {
                var materialPreset = materialPresetsProperty.GetArrayElementAtIndex(_selectedMaterialIndex);
                var presetNameProp = materialPreset.FindPropertyRelative("presetName");
                var themeColorProp = materialPreset.FindPropertyRelative("themeColor");
                var materialsProp = materialPreset.FindPropertyRelative("materials");

                // 预设属性面板
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                bool changed = false;

                // 预设名称
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("预设名称");

                // 检测名称变化
                EditorGUI.BeginChangeCheck();
                string newPresetName = EditorGUILayout.TextField(presetNameProp.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    presetNameProp.stringValue = newPresetName;
                    changed = true;
                }

                EditorGUILayout.EndHorizontal();

                // 主题颜色
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("主题颜色");

                // 检测颜色变化
                EditorGUI.BeginChangeCheck();
                Color newThemeColor = EditorGUILayout.ColorField(themeColorProp.colorValue);
                if (EditorGUI.EndChangeCheck())
                {
                    themeColorProp.colorValue = newThemeColor;
                    changed = true;
                }

                // 添加自动提取颜色按钮
                if (GUILayout.Button("提取", GUILayout.Width(40)))
                {
                    // 获取当前材质预设的第一个材质
                    if (materialsProp.arraySize > 0)
                    {
                        var firstMaterialProp = materialsProp.GetArrayElementAtIndex(0);
                        var material = firstMaterialProp.objectReferenceValue as Material;

                        if (material != null)
                        {
                            themeColorProp.colorValue = MaterialUtility.ExtractThemeColorFromMaterial(material);
                            changed = true;
                        }
                    }
                }

                // 添加随机颜色按钮
                if (GUILayout.Button("随机", GUILayout.Width(40)))
                {
                    themeColorProp.colorValue = MaterialUtility.GenerateRandomColor();
                    changed = true;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // 材质列表
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(materialsProp, new GUIContent("材质列表"), true);
                if (EditorGUI.EndChangeCheck())
                {
                    changed = true;
                }

                // 应用所有修改
                if (changed)
                {
                    _serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_target);
                }
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
    }

    // 获取所有渲染器
    private void GetAllRenderers()
    {
        // 检查目标对象是否为空
        if (_target == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个ItemModifyPreset组件", "确定");
            return;
        }

        // 使用组件所在的GameObject作为根对象
        GameObject rootObject = _target.gameObject;

        // 获取所有渲染器
        Renderer[] renderers = rootObject.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "选中的物体下没有渲染器组件", "确定");
            return;
        }

        // 创建Undo记录
        Undo.RecordObject(_target, "Add Renderers");

        // 更新序列化对象
        _serializedObject.UpdateIfRequiredOrScript();

        // 添加渲染器
        int count = 0;
        foreach (Renderer renderer in renderers)
        {
            // 跳过已存在的渲染器
            bool exists = false;
            foreach (var preset in _target.rendererPresets)
            {
                if (preset.renderer == renderer)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                // 使用SerializedProperty添加新的渲染器预设
                int newIndex = _rendererPresetsProperty.arraySize;
                _rendererPresetsProperty.arraySize++;
                var newPreset = _rendererPresetsProperty.GetArrayElementAtIndex(newIndex);

                // 设置渲染器名称
                newPreset.FindPropertyRelative("rendererName").stringValue = renderer.name;
                newPreset.FindPropertyRelative("renderer").objectReferenceValue = renderer;

                // 计算并设置渲染器路径
                string rendererPath = CalculateRendererPath(renderer, rootObject);
                newPreset.FindPropertyRelative("rendererPath").stringValue = rendererPath;

                // 生成UUID
                var uuidProp = newPreset.FindPropertyRelative("_uuid");
                if (uuidProp != null)
                {
                    uuidProp.stringValue = System.Guid.NewGuid().ToString();
                }

                // 初始化材质预设列表
                var materialPresetsProp = newPreset.FindPropertyRelative("materialPresets");
                if (materialPresetsProp != null)
                {
                    // 创建默认的材质预设
                    materialPresetsProp.arraySize = 1;
                    var defaultPreset = materialPresetsProp.GetArrayElementAtIndex(0);

                    // 设置默认预设名称
                    defaultPreset.FindPropertyRelative("presetName").stringValue = "默认";

                    // 生成UUID
                    var presetUuidProp = defaultPreset.FindPropertyRelative("_uuid");
                    if (presetUuidProp != null)
                    {
                        presetUuidProp.stringValue = System.Guid.NewGuid().ToString();
                    }

                    // 设置材质数组
                    var materialsProp = defaultPreset.FindPropertyRelative("materials");
                    if (materialsProp != null && renderer.sharedMaterials != null)
                    {
                        materialsProp.arraySize = renderer.sharedMaterials.Length;
                        for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                        {
                            materialsProp.GetArrayElementAtIndex(i).objectReferenceValue = renderer.sharedMaterials[i];
                        }
                    }

                    // 设置主题色 - 从材质中提取
                    var themeColorProp = defaultPreset.FindPropertyRelative("themeColor");
                    if (themeColorProp != null)
                    {
                        themeColorProp.colorValue = MaterialUtility.ExtractThemeColorFromMaterial(
                            renderer.sharedMaterials.Length > 0 ? renderer.sharedMaterials[0] : null);
                    }
                }

                count++;
            }
        }

        // 应用修改
        _serializedObject.ApplyModifiedProperties();

        // 更新序列化对象
        _serializedObject.UpdateIfRequiredOrScript();

        // 如果列表为空，就创建一个新的
        if (_rendererPresetsList == null || _rendererPresetsList.List.count == 0)
        {
            CreateRendererPresetsList();
        }

        // 选择第一个预设
        if (_rendererPresetsList.List.count > 0)
        {
            _rendererPresetsList.List.index = 0;
            _selectedRendererIndex = 0;
            CreateMaterialPresetsList();
        }

        EditorUtility.DisplayDialog("完成", $"已成功添加 {count} 个渲染器到预设列表中", "确定");

        EditorUtility.SetDirty(_target);
    }

    // 处理材质拖放
    private void HandleMaterialsDropped(Object[] droppedObjects)
    {
        // 首先检查数组是否为空
        if (droppedObjects == null || droppedObjects.Length == 0)
        {
            Debug.LogWarning("No materials were dropped");
            return;
        }

        // 检查序列化对象是否有效
        if (_serializedObject == null || _rendererPresetsProperty == null)
        {
            Debug.LogError("Serialized object or renderer presets property is null");
            return;
        }

        // 确保序列化对象是最新的
        _serializedObject.UpdateIfRequiredOrScript();

        // 检查是否选中了渲染器
        if (_selectedRendererIndex < 0 || _selectedRendererIndex >= _rendererPresetsProperty.arraySize)
        {
            AddEmptyRenderer();

            // 如果仍然没有选中渲染器，则返回
            if (_selectedRendererIndex < 0 || _selectedRendererIndex >= _rendererPresetsProperty.arraySize)
            {
                Debug.LogWarning("无法选中渲染器");
                return;
            }
        }

        // 获取当前选中的渲染器预设
        var rendererElement = _rendererPresetsProperty.GetArrayElementAtIndex(_selectedRendererIndex);
        if (rendererElement == null)
        {
            Debug.LogError("Renderer element is null");
            return;
        }

        var materialPresetsProperty = rendererElement.FindPropertyRelative("materialPresets");
        if (materialPresetsProperty == null)
        {
            Debug.LogError("Material presets property is null");
            return;
        }

        var rendererProp = rendererElement.FindPropertyRelative("renderer");
        if (rendererProp == null)
        {
            Debug.LogError("Renderer property is null");
            return;
        }

        var renderer = rendererProp.objectReferenceValue as Renderer;
        // 如果渲染器引用为空，仍然允许创建预设
        // 但需要提示用户渲染器引用为空
        if (renderer == null)
        {
            Debug.LogWarning("渲染器引用为空，将创建预设但无法应用");
            // 不返回，继续创建预设
        }

        // 显示模态对话框，让用户设置预设属性
        var drawer = new MaterialPresetDrawer();

        // 设置预设数据
        drawer.Data.rendererIndex = _selectedRendererIndex;
        drawer.Data.renderer = renderer;

        // 获取渲染器的原始材质球
        if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
        {
            drawer.Data.originalMaterials = renderer.sharedMaterials;
        }

        // 创建材质项
        foreach (var obj in droppedObjects)
        {
            if (obj is Material material)
            {
                // 创建材质项
                var item = new MaterialPresetDrawer.MaterialItem
                {
                    material = material,
                    presetName = $"{material.name}", // 使用材质名称初始化预设名称
                    themeColor = MaterialUtility.ExtractThemeColorFromMaterial(material) // 使用工具类从材质提取主题色
                };

                // 不再检查材质是否已存在，直接创建新预设

                drawer.Data.materialItems.Add(item);
            }
        }

        // 显示模态对话框
        ModalEditorWindow.Show(drawer, result =>
        {
            if (result) // 如果用户点击了确定按钮
            {
                // 创建预设
                CreateMaterialPresetWithItems(
                    "", // 不再使用全局预设名称
                    drawer.Data.materialItems,
                    renderer,
                    materialPresetsProperty,
                    drawer.Data.globalSlotIndex);
            }
        }, "创建", "取消");
    }

    // 使用材质项列表创建预设
    private void CreateMaterialPresetWithItems(string _, List<MaterialPresetDrawer.MaterialItem> materialItems, Renderer renderer, SerializedProperty materialPresetsProperty, int globalSlotIndex)
    {
        // 开始Undo记录
        Undo.RecordObject(_target, "Add Material Preset");

        // 确保序列化对象是最新的
        _serializedObject.UpdateIfRequiredOrScript();

        // 记录创建的预设数量
        int createdPresets = 0;
        int lastCreatedIndex = -1;

        // 为每个材质项创建一个预设
        foreach (var item in materialItems)
        {
            // 不再检查材质是否已存在，直接创建新预设

            // 检查渲染器是否为空
            if (renderer == null)
            {
                // 如果渲染器为空，仍然创建预设，但不需要检查槽位索引
                if (item.material != null)
                {
                    // 创建新的材质预设
                    int newIndex = materialPresetsProperty.arraySize;
                    materialPresetsProperty.arraySize++;
                    var newPreset = materialPresetsProperty.GetArrayElementAtIndex(newIndex);
                    if (newPreset == null)
                    {
                        Debug.LogError("Failed to create new material preset");
                        continue;
                    }

                    // 设置预设名称 - 使用材质项的预设名称
                    var presetNameProp = newPreset.FindPropertyRelative("presetName");
                    if (presetNameProp == null)
                    {
                        Debug.LogError("Preset name property is null");
                        continue;
                    }
                    presetNameProp.stringValue = item.presetName;

                    // 生成UUID - 注意：在MaterialPreset类中，UUID是属性，而实际字段是_uuid
                    var uuidProp = newPreset.FindPropertyRelative("_uuid");
                    if (uuidProp != null)
                    {
                        uuidProp.stringValue = System.Guid.NewGuid().ToString();
                    }
                    else
                    {
                        Debug.LogWarning("_uuid property not found in material preset");
                    }

                    // 设置材质数组 - 对于空渲染器，创建一个只有一个槽位的材质数组
                    var materialsProp = newPreset.FindPropertyRelative("materials");
                    if (materialsProp == null)
                    {
                        Debug.LogError("Materials property is null");
                        continue;
                    }

                    materialsProp.arraySize = 1;
                    var slotMatProp = materialsProp.GetArrayElementAtIndex(0);
                    slotMatProp.objectReferenceValue = item.material;

                    // 设置主题色 - 使用当前材质项的主题色
                    var themeColorProp = newPreset.FindPropertyRelative("themeColor");
                    if (themeColorProp == null)
                    {
                        Debug.LogWarning("Theme color property is null");
                    }
                    else
                    {
                        themeColorProp.colorValue = item.themeColor;
                    }

                    // 记录创建的预设
                    createdPresets++;
                    lastCreatedIndex = newIndex;
                }
            }
            // 如果渲染器不为空，检查槽位索引是否有效
            else if (globalSlotIndex >= 0 && globalSlotIndex < renderer.sharedMaterials.Length && item.material != null)
            {
                // 创建新的材质预设
                int newIndex = materialPresetsProperty.arraySize;
                materialPresetsProperty.arraySize++;
                var newPreset = materialPresetsProperty.GetArrayElementAtIndex(newIndex);
                if (newPreset == null)
                {
                    Debug.LogError("Failed to create new material preset");
                    continue;
                }

                // 设置预设名称 - 使用材质项的预设名称
                var presetNameProp = newPreset.FindPropertyRelative("presetName");
                if (presetNameProp == null)
                {
                    Debug.LogError("Preset name property is null");
                    continue;
                }
                presetNameProp.stringValue = item.presetName;

                // 生成UUID - 注意：在MaterialPreset类中，UUID是属性，而实际字段是_uuid
                var uuidProp = newPreset.FindPropertyRelative("_uuid");
                if (uuidProp != null)
                {
                    uuidProp.stringValue = System.Guid.NewGuid().ToString();
                }
                else
                {
                    Debug.LogWarning("_uuid property not found in material preset");
                }

                // 设置材质数组
                var materialsProp = newPreset.FindPropertyRelative("materials");
                if (materialsProp == null)
                {
                    Debug.LogError("Materials property is null");
                    continue;
                }

                if (renderer.sharedMaterials == null)
                {
                    Debug.LogWarning("Renderer has no shared materials");
                    materialsProp.arraySize = 0;
                    continue;
                }

                materialsProp.arraySize = renderer.sharedMaterials.Length;

                // 复制原有材质
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    var slotMatProp = materialsProp.GetArrayElementAtIndex(i);
                    slotMatProp.objectReferenceValue = renderer.sharedMaterials[i];
                }

                // 应用当前材质到选中的槽位
                var targetMatProp = materialsProp.GetArrayElementAtIndex(globalSlotIndex);
                targetMatProp.objectReferenceValue = item.material;

                // 设置主题色 - 使用当前材质项的主题色
                var themeColorProp = newPreset.FindPropertyRelative("themeColor");
                if (themeColorProp == null)
                {
                    Debug.LogWarning("Theme color property is null");
                }
                else
                {
                    themeColorProp.colorValue = item.themeColor;
                }

                // 记录创建的预设
                createdPresets++;
                lastCreatedIndex = newIndex;
            }
        }

        // 更新序列化对象
        _serializedObject.ApplyModifiedProperties();

        // 如果创建了预设，选中最后一个创建的预设
        if (createdPresets > 0 && lastCreatedIndex >= 0)
        {
            _selectedMaterialIndex = lastCreatedIndex;

            // 标记为已修改
            EditorUtility.SetDirty(_target);

            Debug.Log($"成功创建了 {createdPresets} 个材质预设（包含新增和覆盖）");
        }
        else
        {
            Debug.Log("没有新材质被添加，预设创建已取消");
        }
    }

    // 计算渲染器相对于根对象的路径
    private string CalculateRendererPath(Renderer renderer, GameObject rootObject)
    {
        if (renderer == null || rootObject == null)
            return string.Empty;

        // 获取渲染器的完整路径
        List<string> pathParts = new List<string>();
        Transform current = renderer.transform;
        Transform rootTransform = rootObject.transform;

        // 从渲染器向上遍历直到根对象
        while (current != null && current != rootTransform.parent)
        {
            pathParts.Add(current.name);

            // 如果到达根对象，停止遍历
            if (current == rootTransform)
                break;

            current = current.parent;
        }

        // 反转路径并用斜杠连接
        pathParts.Reverse();
        return string.Join("/", pathParts.ToArray());
    }

    // 添加空渲染器
    private void AddEmptyRenderer()
    {
        // 开始Undo记录
        Undo.RecordObject(_target, "Add Empty Renderer");

        // 先应用当前修改，确保组件数据是最新的
        _serializedObject.ApplyModifiedProperties();

        // 添加空渲染器
        // 创建一个新的渲染器预设并手动添加到列表中
        int newIndex = _rendererPresetsProperty.arraySize;
        _rendererPresetsProperty.arraySize++;
        var newPreset = _rendererPresetsProperty.GetArrayElementAtIndex(newIndex);

        // 设置渲染器名称
        var rendererNameProp = newPreset.FindPropertyRelative("rendererName");
        rendererNameProp.stringValue = $"渲染器 {newIndex + 1}";

        // 设置空路径
        newPreset.FindPropertyRelative("rendererPath").stringValue = string.Empty;

        // 生成UUID
        var uuidProp = newPreset.FindPropertyRelative("_uuid");
        if (uuidProp != null)
        {
            uuidProp.stringValue = System.Guid.NewGuid().ToString();
        }

        // 初始化材质预设列表
        var materialPresetsProp = newPreset.FindPropertyRelative("materialPresets");
        if (materialPresetsProp != null)
        {
            materialPresetsProp.ClearArray();
        }

        // 更新序列化对象
        _serializedObject.UpdateIfRequiredOrScript();

        // 重新创建渲染器预设列表
        CreateRendererPresetsList();

        // 选择新添加的渲染器
        if (_rendererPresetsList != null && _rendererPresetsList.List.count > 0)
        {
            _rendererPresetsList.List.index = _rendererPresetsList.List.count - 1;
            _selectedRendererIndex = _rendererPresetsList.List.count - 1;
            CreateMaterialPresetsList();
        }

        EditorUtility.SetDirty(_target);
    }

    // 应用材质预设
    private void ApplyMaterialPreset(Renderer renderer, SerializedProperty materialPresetProp)
    {
        if (renderer == null || materialPresetProp == null)
            return;

        var materialsProp = materialPresetProp.FindPropertyRelative("materials");
        if (materialsProp.arraySize == 0)
            return;

        // 创建材质数组
        Material[] materials = new Material[materialsProp.arraySize];
        for (int i = 0; i < materialsProp.arraySize; i++)
        {
            var matProp = materialsProp.GetArrayElementAtIndex(i);
            if (matProp != null)
            {
                materials[i] = matProp.objectReferenceValue as Material;
            }
        }

        // 使用工具类应用材质
        MaterialUtility.ApplyMaterialsToRenderer(renderer, materials);
    }
}
