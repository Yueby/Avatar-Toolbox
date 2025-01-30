using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yueby.Utils;

namespace Yueby
{
    public class MaterialSwitcherEditorWindow : EditorWindow
    {
        private MaterialSwitcher _targetComponent;
        private SerializedObject _serializedObject;
        private List<RendererData> _rendererDatas = new List<RendererData>();
        private Vector2 _scrollPosition;
        private bool _globalPreviewMode;
        private Dictionary<string, Material> _originalMaterials = new Dictionary<string, Material>();
        private string _searchString = "";

        [MenuItem("Tools/YuebyTools/Utils/MaterialSwitcher", false, 11)]
        private static void ShowWindow()
        {
            var window = GetWindow<MaterialSwitcherEditorWindow>();
            window.titleContent = new GUIContent("Material Switcher");
            window.Show();
        }

        private void OnEnable()
        {
            if (_targetComponent != null)
            {
                _serializedObject = new SerializedObject(_targetComponent);
            }
        }

        public void InitializeWithComponent(MaterialSwitcher component)
        {
            _targetComponent = component;
            _serializedObject = new SerializedObject(_targetComponent);
            LoadFromComponent();
        }

        private void LoadFromComponent()
        {
            _rendererDatas.Clear();

            foreach (var config in _targetComponent.rendererConfigs)
            {
                var data = new RendererData(config, _targetComponent);

                if (data.Renderer != null)
                {
                    UpdateMaterialSlots(data);
                    data.SelectedMaterialIndex = config.selectedMaterialIndex;

                    // 初始化应用状态
                    if (config.materials.Count > 0 &&
                        config.appliedMaterialIndex >= 0 &&
                        config.appliedMaterialIndex < config.materials.Count)
                    {
                        data.IsApplied = true;
                        data.AppliedMaterial = config.materials[config.appliedMaterialIndex];
                    }
                }

                _rendererDatas.Add(data);
            }
        }

        private void SaveToComponent()
        {
            if (_targetComponent == null) return;

            _targetComponent.rendererConfigs.Clear();
            foreach (var data in _rendererDatas)
            {
                var config = new MaterialSwitcher.RendererConfig
                {
                    name = data.Name,
                    rendererPath = data.Renderer != null ?
                        GetRelativePath(_targetComponent.transform, data.Renderer.transform) : "",
                    selectedMaterialIndex = data.SelectedMaterialIndex,
                    appliedMaterialIndex = data.IsApplied ? data.AppliedMaterialIndex : -1,
                    materials = new List<Material>(data.Materials),
                    isFoldout = data.IsFoldout
                };

                _targetComponent.rendererConfigs.Add(config);
            }

            EditorUtility.SetDirty(_targetComponent);
        }

        private void OnDisable()
        {
            // 先退出所有预览状态
            ResetAllPreviews();

            // 再保存数据
            SaveToComponent();
        }

        private void OnGUI()
        {
            if (_serializedObject != null)
            {
                _serializedObject.Update();

                EditorGUI.BeginChangeCheck();
                var newConfigName = EditorGUILayout.TextField("Config Name", _targetComponent.configName);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_targetComponent, "Change Config Name");
                    _targetComponent.configName = newConfigName;
                    EditorUtility.SetDirty(_targetComponent);
                }
                // 添加提示信息
                if (_targetComponent == null)
                {
                    EditorGUILayout.HelpBox("Currently in temporary preview mode. To save the configuration, please add MaterialSwitcher component to a scene object.", MessageType.Info);
                }

                EditorUI.VerticalEGL(() =>
                {
                    // 添加搜索栏
                    DrawSearchBar();

                    // 添加拖放区域
                    DrawDropArea();

                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                    // 渲染器列表
                    for (int i = 0; i < _rendererDatas.Count; i++)
                    {
                        var data = _rendererDatas[i];
                        if (ShouldShowRenderer(data))
                        {
                            DrawRendererSection(data, i);
                        }
                    }

                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.Space(5);

                    DrawToolbar();
                });

                _serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // 全局预览控制
                EditorGUI.BeginChangeCheck();
                _globalPreviewMode = GUILayout.Toggle(_globalPreviewMode,
                    new GUIContent(EditorGUIUtility.IconContent("d_scenevis_visible_hover").image, "Toggle global preview mode"),
                    EditorStyles.toolbarButton);
                if (EditorGUI.EndChangeCheck())
                {
                    ToggleAllPreviews(_globalPreviewMode);
                }

                // 全局预览控制按钮
                using (new EditorGUI.DisabledGroupScope(!_globalPreviewMode))
                {
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Animation.PrevKey").image, "Previous Material"),
                        EditorStyles.toolbarButton, GUILayout.Width(24)))
                    {
                        SwitchAllMaterials(-1);
                    }

                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Animation.NextKey").image, "Next Material"),
                        EditorStyles.toolbarButton, GUILayout.Width(24)))
                    {
                        SwitchAllMaterials(1);
                    }

                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Valid").image, "Apply All Materials"),
                        EditorStyles.toolbarButton, GUILayout.Width(24)))
                    {
                        ApplyAllMaterials();
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Add Renderer", EditorStyles.toolbarButton))
                {
                    AddNewRenderer();
                }
            }
        }

        private void AddNewRenderer()
        {
            var newConfig = new MaterialSwitcher.RendererConfig
            {
                name = $"Renderer {_rendererDatas.Count + 1}",
                isFoldout = true
            };

            if (_targetComponent != null)
            {
                Undo.RecordObject(_targetComponent, "Add Renderer");
                _targetComponent.rendererConfigs.Add(newConfig);
                EditorUtility.SetDirty(_targetComponent);
            }
            _rendererDatas.Add(new RendererData(newConfig, _targetComponent));
        }

        private void DrawRendererSection(RendererData data, int index)
        {
            // 使用 helpBox 样式包裹整个 section，让它有独立的背景和边距
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Title bar with toolbar style
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    // 折叠箭头和标题
                    var titleText = string.IsNullOrEmpty(data.Name) ? $"Renderer {index + 1}" : data.Name;
                    if (data.Renderer != null && data.Materials.Count > 0)
                    {
                        string materialName = null;

                        if (data.IsPreviewMode && data.PreviewMaterialIndex >= 0 && data.PreviewMaterialIndex < data.Materials.Count)
                        {
                            var previewMaterial = data.Materials[data.PreviewMaterialIndex];
                            if (previewMaterial != null)
                            {
                                materialName = $"{previewMaterial.name} (Preview)";
                            }
                        }
                        else if (data.IsApplied && data.AppliedMaterial != null)
                        {
                            materialName = data.AppliedMaterial.name;
                        }

                        if (!string.IsNullOrEmpty(materialName))
                        {
                            titleText += $" - {materialName}";
                        }
                    }

                    // 使用 toolbarButton 样式的折叠按钮
                    data.IsFoldout = GUILayout.Toggle(data.IsFoldout,
                        data.IsFoldout ? "▼" : "►",
                        EditorStyles.toolbarButton, GUILayout.Width(20));

                    // 标题文本
                    GUILayout.Label(titleText, EditorStyles.toolbarButton);

                    GUILayout.FlexibleSpace();

                    if (data.Renderer != null)
                    {
                        // 预览按钮
                        var previewContent = data.GetIconContent();
                        bool newPreviewMode = GUILayout.Toggle(data.IsPreviewMode, previewContent,
                            EditorStyles.toolbarButton, GUILayout.Width(24));
                        if (newPreviewMode != data.IsPreviewMode)
                        {
                            if (newPreviewMode)
                            {
                                StoreOriginalMaterial(data);
                            }
                            data.IsPreviewMode = newPreviewMode;
                            if (!newPreviewMode)
                            {
                                RestoreOriginalMaterial(data);
                                _globalPreviewMode = false;
                            }
                        }

                        // 上一个材质
                        using (new EditorGUI.DisabledGroupScope(!data.IsPreviewMode || data.PreviewMaterialIndex <= 0))
                        {
                            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Animation.PrevKey").image, "Previous Material"),
                                EditorStyles.toolbarButton, GUILayout.Width(24)))
                            {
                                data.SwitchMaterial(-1);
                            }
                        }

                        // 下一个材质
                        using (new EditorGUI.DisabledGroupScope(!data.IsPreviewMode || data.Materials.Count == 0 || data.PreviewMaterialIndex >= data.Materials.Count - 1))
                        {
                            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Animation.NextKey").image, "Next Material"),
                                EditorStyles.toolbarButton, GUILayout.Width(24)))
                            {
                                data.SwitchMaterial(1);
                            }
                        }

                        // 应用按钮
                        using (new EditorGUI.DisabledGroupScope(!data.IsPreviewMode))
                        {
                            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Valid").image, "Apply current material"),
                                EditorStyles.toolbarButton, GUILayout.Width(24)))
                            {
                                data.ApplyMaterial();
                            }
                        }
                    }

                    // 添加删除按钮
                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_TreeEditor.Trash"),
                        EditorStyles.toolbarButton, GUILayout.Width(24)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Renderer",
                            "Are you sure you want to delete this renderer?", "Yes", "No"))
                        {
                            _rendererDatas.RemoveAt(index);
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                // Content area
                if (data.IsFoldout)
                {
                    EditorGUILayout.Space(5);

                    // 使用 VerticalScope 确保内容区域有合适的缩进和间距
                    using (new EditorGUILayout.VerticalScope(GUILayout.MinHeight(80)))
                    {
                        // Name field
                        EditorGUI.BeginChangeCheck();
                        var newName = EditorGUILayout.TextField("Name", data.Name);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(_targetComponent, "Change Config Name");
                            data.Name = newName;
                            EditorUtility.SetDirty(_targetComponent);
                        }

                        // Renderer field
                        EditorGUI.BeginChangeCheck();
                        var newRenderer = EditorGUILayout.ObjectField("Renderer",
                            data.Renderer, typeof(Renderer), true) as Renderer;
                        if (EditorGUI.EndChangeCheck() && newRenderer != data.Renderer)
                        {
                            data.Renderer = newRenderer;
                            if (newRenderer != null)
                            {
                                UpdateMaterialSlots(data);
                            }
                        }

                        if (data.Renderer != null)
                        {
                            // Material slot selection
                            data.SelectedMaterialIndex = EditorGUILayout.Popup(
                                "Material Index",
                                data.SelectedMaterialIndex,
                                data.MaterialSlotNames);

                            EditorGUILayout.Space(5);

                            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.MinHeight(150)))
                            {
                                data.MaterialList.DoLayout
                                (
                                    "",
                                    new Vector2(0, 150),
                                false,
                                    false,
                                    true,
                                    data.HandleMaterialDrop,
                                    Repaint
                                );
                            }
                        }
                    }
                }
            }
        }

        private void UpdateMaterialSlots(RendererData data)
        {
            var materials = data.Renderer.sharedMaterials;
            data.MaterialSlotNames = new string[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                data.MaterialSlotNames[i] = $"Slot {i} ({(materials[i] != null ? materials[i].name : "None")})";
            }
            data.SelectedMaterialIndex = 0;
        }

        private void ToggleAllPreviews(bool enabled)
        {
            foreach (var data in _rendererDatas)
            {
                if (data.Renderer == null) continue;

                // 只切换预览状态
                if (data.IsPreviewMode != enabled)
                {
                    data.IsPreviewMode = enabled;
                }
            }
        }

        private void SwitchAllMaterials(int direction)
        {
            foreach (var data in _rendererDatas)
            {
                if (data.IsPreviewMode && data.Renderer != null && data.Materials.Count > 0)
                {
                    data.SwitchMaterial(direction);
                }
            }
        }

        private void ApplyAllMaterials()
        {
            foreach (var data in _rendererDatas)
            {
                if (data.IsPreviewMode)
                {
                    data.ApplyMaterial();
                }
            }
        }

        private void ResetAllPreviews()
        {
            foreach (var data in _rendererDatas)
            {
                data.ResetToOriginal();
            }
            _globalPreviewMode = false;
        }

        private void StoreOriginalMaterial(RendererData data)
        {
            if (data.Renderer == null) return;
            string key = $"{data.Renderer.GetInstanceID()}_{data.SelectedMaterialIndex}";
            if (!_originalMaterials.ContainsKey(key))
            {
                _originalMaterials[key] = data.Renderer.sharedMaterials[data.SelectedMaterialIndex];
            }
        }

        private void RestoreOriginalMaterial(RendererData data)
        {
            if (data.Renderer == null) return;
            string key = $"{data.Renderer.GetInstanceID()}_{data.SelectedMaterialIndex}";
            if (_originalMaterials.TryGetValue(key, out Material originalMaterial))
            {
                var materials = data.Renderer.sharedMaterials;
                materials[data.SelectedMaterialIndex] = originalMaterial;
                data.Renderer.sharedMaterials = materials;
            }
        }

        private void ApplyMaterialChange(RendererData data, Material newMaterial)
        {
            if (data.Renderer == null) return;

            Undo.RecordObject(data.Renderer, "Change Material");
            if (_targetComponent != null)
            {
                Undo.RecordObject(_targetComponent, "Update Material Config");
            }

            // 应用材质变更
            var materials = data.Renderer.sharedMaterials;
            materials[data.SelectedMaterialIndex] = newMaterial;
            data.Renderer.sharedMaterials = materials;

            EditorUtility.SetDirty(data.Renderer);
            if (_targetComponent != null)
            {
                EditorUtility.SetDirty(_targetComponent);
            }
        }

        private void DrawSearchBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                _searchString = EditorGUILayout.TextField(_searchString, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                {
                    // 重新计算滚动视图高度
                    Repaint();
                }
            }
        }

        private bool ShouldShowRenderer(RendererData data)
        {
            if (string.IsNullOrEmpty(_searchString)) return true;

            return data.Name.ToLower().Contains(_searchString.ToLower()) ||
                   (data.Renderer != null && data.Renderer.name.ToLower().Contains(_searchString.ToLower())) ||
                   (data.AppliedMaterial != null && data.AppliedMaterial.name.ToLower().Contains(_searchString.ToLower()));
        }

        private void DrawDropArea()
        {
            // 只在列表为空时显示大的拖放区域
            if (_rendererDatas.Count == 0)
            {
                var dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "Drag Renderers Here", EditorStyles.helpBox);
                HandleRenderersDrop(dropArea);
            }
            else
            {
                // 列表不为空时显示小的拖放提示
                var dropArea = GUILayoutUtility.GetRect(0.0f, 20.0f, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "Drop Renderers to Add", EditorStyles.toolbarButton);
                HandleRenderersDrop(dropArea);
            }
        }

        private void HandleRenderersDrop(Rect dropArea)
        {
            var currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition)) return;

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    // 检查是否有Renderer
                    bool hasRenderer = DragAndDrop.objectReferences.Any(obj =>
                        obj is GameObject go && go.GetComponent<Renderer>() != null);

                    if (hasRenderer)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                        if (currentEvent.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            AddRenderers(DragAndDrop.objectReferences);
                        }
                    }
                    currentEvent.Use();
                    break;
            }
        }

        private void AddRenderers(UnityEngine.Object[] objects)
        {
            Undo.RecordObject(_targetComponent, "Add Renderers");

            foreach (var obj in objects)
            {
                if (obj is GameObject go)
                {
                    // 获取GameObject上的所有Renderer
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in renderers)
                    {
                        // 检查是否已经存在
                        bool exists = _rendererDatas.Any(data =>
                            data.Renderer != null && data.Renderer == renderer);

                        if (!exists)
                        {
                            var newConfig = new MaterialSwitcher.RendererConfig
                            {
                                name = renderer.name,
                                isFoldout = true
                            };

                            if (_targetComponent != null)
                            {
                                _targetComponent.rendererConfigs.Add(newConfig);
                            }

                            var newData = new RendererData(newConfig, _targetComponent);
                            newData.Renderer = renderer;
                            UpdateMaterialSlots(newData);
                            _rendererDatas.Add(newData);
                        }
                    }
                }
            }

            if (_targetComponent != null)
            {
                EditorUtility.SetDirty(_targetComponent);
            }
        }

        private class RendererData
        {
            private MaterialSwitcher _component;
            private Renderer _cachedRenderer;
            private MaterialSwitcher.RendererConfig _config;
            private MaterialSwitcherEditorWindow _window;
            private GUIStyle _titleStyle;
            private GUIContent _iconContent;

            // 序列化数据的属性包装
            public Renderer Renderer
            {
                get
                {
                    if (_cachedRenderer == null && !string.IsNullOrEmpty(_config.rendererPath) && _component != null)
                    {
                        // 从组件所在物体开始查找相对路径
                        Transform target = _component.transform.Find(_config.rendererPath);
                        if (target != null)
                        {
                            _cachedRenderer = target.GetComponent<Renderer>();
                        }
                    }
                    return _cachedRenderer;
                }
                set
                {
                    _cachedRenderer = value;
                    if (value != null && _component != null)
                    {
                        // 使用保存的窗口用
                        _config.rendererPath = _window.GetRelativePath(_component.transform, value.transform);
                    }
                    else
                    {
                        _config.rendererPath = "";
                    }
                }
            }
            public string Name
            {
                get => _config.name;
                set => _config.name = value;
            }
            public int SelectedMaterialIndex
            {
                get => _config.selectedMaterialIndex;
                set => _config.selectedMaterialIndex = value;
            }
            public List<Material> Materials => _config.materials;
            public bool IsFoldout
            {
                get => _config.isFoldout;
                set => _config.isFoldout = value;
            }

            // 编辑器运行时数
            public string[] MaterialSlotNames = new string[0];
            public ReorderableListDroppable MaterialList;
            private bool _isPreviewMode;
            public bool IsPreviewMode
            {
                get => _isPreviewMode;
                set
                {
                    if (_isPreviewMode == value) return;
                    
                    if (value)
                    {
                        // 进入预览模式
                        EnterPreviewMode();
                    }
                    else
                    {
                        // 退出预览模式
                        ExitPreviewMode();
                    }
                    
                    _isPreviewMode = value;
                }
            }
            private int _previewMaterialIndex = -1;
            private Material _originalMaterial;  // 保存原始材质
            private Material _appliedMaterial;   // 保存已应用的材质
            private int _appliedMaterialIndex = -1;  // 保存已应用的材质索引
            private bool _isApplied;

            public int PreviewMaterialIndex
            {
                get => _previewMaterialIndex;
                set
                {
                    if (_previewMaterialIndex == value) return;
                    _previewMaterialIndex = value;
                    if (IsPreviewMode)
                    {
                        PreviewMaterial();
                    }
                }
            }

            // 添加属性来访问应用的材质索引
            public int AppliedMaterialIndex
            {
                get => _config.appliedMaterialIndex;
                private set => _config.appliedMaterialIndex = value;
            }

            public Material OriginalMaterial;
            public Material AppliedMaterial;
            public bool IsApplied;

            public RendererData(MaterialSwitcher.RendererConfig config, MaterialSwitcher component)
            {
                _config = config;
                _component = component;
                _window = EditorWindow.GetWindow<MaterialSwitcherEditorWindow>();

                // 初始化预览索引为-1（未预览状态）
                _previewMaterialIndex = -1;

                // 初始化应用状态
                IsApplied = config.materials.Count > 0 &&
                            config.appliedMaterialIndex >= 0 &&
                            config.appliedMaterialIndex < config.materials.Count;

                if (IsApplied)
                {
                    AppliedMaterial = config.materials[config.appliedMaterialIndex];
                }

                InitializeList();
            }

            private void InitializeList()
            {
                MaterialList = new ReorderableListDroppable(
                    Materials,
                    typeof(Material),
                    EditorGUIUtility.singleLineHeight,
                    EditorWindow.focusedWindow.Repaint,
                    true,
                    true
                );

                MaterialList.OnDrawTitle = () =>
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();

                        // 复制按钮
                        if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_TreeEditor.Duplicate").image, "Copy Materials"),
                            GUILayout.Width(24), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                        {
                            CopyMaterialsToClipboard();
                        }

                        // 粘贴按钮
                        using (new EditorGUI.DisabledGroupScope(!CanPasteMaterials()))
                        {
                            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Toolbar Plus").image, "Paste Materials"),
                                GUILayout.Width(24), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                            {
                                PasteMaterialsFromClipboard();
                            }
                        }
                    }
                };

                MaterialList.OnDraw = (rect, index, active, focused) =>
                {
                    var material = Materials[index];
                    EditorGUI.ObjectField(rect, material, typeof(Material), false);
                    return EditorGUIUtility.singleLineHeight;
                };

                MaterialList.OnAdd += _ =>
                {
                    Materials.Add(null);
                    CheckMaterialIndexes(); // 添加时检查
                };

                MaterialList.OnRemove += list =>
                {
                    if (list.index >= 0 && list.index < Materials.Count)
                    {
                        CheckMaterialIndexes(); // 删除时检查
                    }
                };

                MaterialList.OnChanged += _ =>
                {
                    // 只在重排序时更新预览
                    if (IsPreviewMode && PreviewMaterialIndex >= 0)
                    {
                        PreviewMaterial();
                    }
                };

                MaterialList.RefreshElementHeights();
            }

            private void CopyMaterialsToClipboard()
            {
                if (Materials == null || Materials.Count == 0) return;

                var materialGuids = new List<string>();
                foreach (var material in Materials)
                {
                    if (material != null)
                    {
                        string path = AssetDatabase.GetAssetPath(material);
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        materialGuids.Add(guid);
                    }
                }

                if (materialGuids.Count > 0)
                {
                    string json = JsonUtility.ToJson(new MaterialClipboardData { materialGuids = materialGuids });
                    EditorGUIUtility.systemCopyBuffer = json;
                }
            }

            private bool CanPasteMaterials()
            {
                try
                {
                    var clipboardData = JsonUtility.FromJson<MaterialClipboardData>(
                        EditorGUIUtility.systemCopyBuffer);
                    return clipboardData != null && clipboardData.materialGuids != null &&
                           clipboardData.materialGuids.Count > 0;
                }
                catch
                {
                    return false;
                }
            }

            private void PasteMaterialsFromClipboard()
            {
                try
                {
                    var clipboardData = JsonUtility.FromJson<MaterialClipboardData>(
                        EditorGUIUtility.systemCopyBuffer);

                    if (clipboardData?.materialGuids == null) return;

                    Undo.RecordObject(_component, "Paste Materials");

                    Materials.Clear();
                    foreach (string guid in clipboardData.materialGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material != null)
                        {
                            Materials.Add(material);
                        }
                    }

                    EditorUtility.SetDirty(_component);
                    MaterialList.RefreshElementHeights();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to paste materials: {e.Message}");
                }
            }

            // 添加新方法来检查索引
            private void CheckMaterialIndexes()
            {
                // 如果删除了正在预览或应用的材质，重置状态
                if (PreviewMaterialIndex >= Materials.Count)
                {
                    PreviewMaterialIndex = Materials.Count - 1;
                }
                if (_config.appliedMaterialIndex >= Materials.Count)
                {
                    IsApplied = false;
                    _config.appliedMaterialIndex = -1;
                }
            }

            public void HandleMaterialDrop(UnityEngine.Object[] objects)
            {
                foreach (var obj in objects)
                {
                    if (obj is Material material && !Materials.Contains(material))
                    {
                        Materials.Add(material);
                    }
                }
            }

            public void SwitchMaterial(int direction)
            {
                if (!IsPreviewMode || Materials.Count == 0) return;

                // 如果当前预览索引无效，从0开始
                if (_previewMaterialIndex < 0)
                {
                    _previewMaterialIndex = 0;
                }

                int newIndex = Mathf.Clamp(
                    PreviewMaterialIndex + direction,
                    0,
                    Materials.Count - 1
                );

                if (newIndex != PreviewMaterialIndex)
                {
                    PreviewMaterialIndex = newIndex;
                    PreviewMaterial(); // 改为调用 PreviewMaterial 而不是直接 ApplyMaterialChange
                }
            }

            private void EnterPreviewMode()
            {
                if (Renderer == null) return;

                // 1. 保存当前材质状态
                var currentMaterials = Renderer.sharedMaterials;
                _originalMaterial = currentMaterials[SelectedMaterialIndex];

                // 2. 设置预览索引并确保它是有效的
                if (_previewMaterialIndex < 0 || _previewMaterialIndex >= Materials.Count)
                {
                    _previewMaterialIndex = _isApplied ? _appliedMaterialIndex : 0;
                }

                // 3. 立即应用预览材质
                if (Materials.Count > 0)
                {
                    var materials = Renderer.sharedMaterials;
                    materials[SelectedMaterialIndex] = Materials[_previewMaterialIndex];
                    Renderer.sharedMaterials = materials;
                }
            }

            private void ExitPreviewMode()
            {
                if (Renderer == null) return;

                // 恢复材质状态
                var materials = Renderer.sharedMaterials;
                materials[SelectedMaterialIndex] = _isApplied ? _appliedMaterial : _originalMaterial;
                Renderer.sharedMaterials = materials;

                // 重置预览状态
                _previewMaterialIndex = -1;
            }

            public void PreviewMaterial()
            {
                if (!_isPreviewMode || Renderer == null || Materials.Count == 0) return;

                var materials = Renderer.sharedMaterials;
                materials[SelectedMaterialIndex] = Materials[_previewMaterialIndex];
                Renderer.sharedMaterials = materials;
            }

            public void ApplyMaterial()
            {
                if (!_isPreviewMode || Renderer == null || Materials.Count == 0) return;

                // 1. 记录当前预览的材质作为应用材质
                _appliedMaterial = Materials[_previewMaterialIndex];
                _appliedMaterialIndex = _previewMaterialIndex;
                _isApplied = true;

                // 2. 应用材质变更（包含 Undo 支持）
                _window.ApplyMaterialChange(this, _appliedMaterial);

                // 3. 更新配置
                _config.appliedMaterialIndex = _appliedMaterialIndex;
            }

            public void ResetToOriginal()
            {
                if (!_isPreviewMode) return;

                ExitPreviewMode();
                _isPreviewMode = false;
            }

            // 缓存常用的GUIStyle和GUIContent
            public GUIStyle GetTitleStyle()
            {
                if (_titleStyle == null)
                {
                    _titleStyle = new GUIStyle(EditorStyles.boldLabel);
                }
                return _titleStyle;
            }

            public GUIContent GetIconContent()
            {
                if (_iconContent == null)
                {
                    _iconContent = EditorGUIUtility.IconContent("d_scenevis_visible_hover");
                    _iconContent.tooltip = "Click to toggle preview state";
                }
                return _iconContent;
            }
        }

        [Serializable]
        private class MaterialClipboardData
        {
            public List<string> materialGuids = new List<string>();
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            if (root == target) return "";

            string path = target.name;
            Transform parent = target.parent;

            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}