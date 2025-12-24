using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Yueby.Tools.AvatarToolbox.MaterialPreset;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components
{
    public class PresetDetailView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<PresetDetailView> { }

        private PresetGroup _currentGroup;
        private MaterialPresetManager _manager;
        private DetailViewToolbar _toolbar;
        private int _selectedRendererIndex = -1;
        private List<RendererConfig> _filteredConfigs; // 保存过滤后的列表

        // UI References
        private ListView _rendererList;
        private VisualElement _slotsContainer;
        private Label _placeholder;

        // Events
        public event Action<PresetGroup> OnBreadcrumbUpdate;

        public void Initialize(VisualTreeAsset template, DetailViewToolbar toolbar)
        {
            // Ensure the view takes up available space
            style.flexGrow = 1;
            
            if (template != null)
            {
                template.CloneTree(this);
            }
            _toolbar = toolbar;

            _rendererList = this.Q<ListView>("list-renderers");
            _slotsContainer = this.Q("slots-container");
            _placeholder = this.Q<Label>("label-no-selection");

            // Bind Toolbar Events
            if (_toolbar != null)
            {
                _toolbar.OnSearch += FilterRendererList;
                _toolbar.OnNameChanged += OnGroupNameChanged;
            }

            // Bind UI Events
            this.Q<Button>("btn-add-renderer").clicked += OnAddRendererClicked;
            this.Q<Button>("btn-remove-renderer").clicked += OnRemoveRendererClicked;

            if (_slotsContainer != null)
            {
                _slotsContainer.RegisterCallback<DragUpdatedEvent>(OnSlotsDragUpdated);
                _slotsContainer.RegisterCallback<DragPerformEvent>(OnSlotsDragPerform);
            }
        }

        public void Bind(PresetGroup group, MaterialPresetManager manager)
        {
            _currentGroup = group;
            _manager = manager;
            _selectedRendererIndex = -1;
            _filteredConfigs = null;

            if (_currentGroup == null) return;

            // 清除搜索框
            _toolbar?.ClearSearch();
            
            // Sync Toolbar Info
            _toolbar?.SetGroupInfo(_currentGroup.GroupName);

            // Setup Renderer List
            if (_rendererList != null)
            {
                _rendererList.makeItem = () => new Label();
                _rendererList.bindItem = (e, i) =>
                {
                    var configs = _filteredConfigs ?? _currentGroup.RendererConfigs;
                    if (i >= 0 && i < configs.Count)
                    {
                        var path = configs[i].RendererPath;
                        (e as Label).text = !string.IsNullOrEmpty(path) ? path.Substring(path.LastIndexOf('/') + 1) : "Unknown";
                    }
                };
                _rendererList.itemsSource = _currentGroup.RendererConfigs;
                _rendererList.selectionType = SelectionType.Single;
                _rendererList.selectionChanged += objs =>
                {
                    _selectedRendererIndex = _rendererList.selectedIndex;
                    RefreshMaterialSlots();
                };
                
                // 清除选中状态，确保视觉和逻辑状态一致
                _rendererList.ClearSelection();
                _rendererList.RefreshItems();
            }

            RefreshMaterialSlots();
        }

        private void OnGroupNameChanged(string newName)
        {
            if (_currentGroup != null && _manager != null)
            {
                Undo.RecordObject(_manager, "修改预设组名称");
                _currentGroup.GroupName = newName;
                EditorUtility.SetDirty(_manager);
                OnBreadcrumbUpdate?.Invoke(_currentGroup);
            }
        }

        private void FilterRendererList(string filter)
        {
            if (_rendererList == null || _currentGroup == null) return;

            if (string.IsNullOrEmpty(filter))
            {
                _filteredConfigs = null;
                _rendererList.itemsSource = _currentGroup.RendererConfigs;
            }
            else
            {
                _filteredConfigs = _currentGroup.RendererConfigs
                    .Where(rc => rc.RendererPath.ToLowerInvariant().Contains(filter.ToLowerInvariant()))
                    .ToList();
                _rendererList.itemsSource = _filteredConfigs;
            }

            _selectedRendererIndex = -1;
            _rendererList.ClearSelection();
            _rendererList.RefreshItems();
            RefreshMaterialSlots();
        }

        private void RefreshMaterialSlots()
        {
            if (_slotsContainer == null || _placeholder == null) return;
            
            _slotsContainer.Clear();

            var configs = _filteredConfigs ?? _currentGroup.RendererConfigs;
            
            if (_selectedRendererIndex < 0 || _selectedRendererIndex >= configs.Count)
            {
                _placeholder.style.display = DisplayStyle.Flex;
                return;
            }
            _placeholder.style.display = DisplayStyle.None;
            var config = configs[_selectedRendererIndex];

            // Auto-populate slots if empty
            if (config.MaterialSlots.Count == 0 && config.TargetRenderer != null)
            {
                for (int i = 0; i < config.TargetRenderer.sharedMaterials.Length; i++)
                {
                    var mat = config.TargetRenderer.sharedMaterials[i];
                    config.MaterialSlots.Add(new MaterialSlotConfig { SlotIndex = i, SlotName = "材质 " + i, MaterialRef = mat });
                }
            }

            // Slots
            foreach (var slot in config.MaterialSlots)
            {
                var slotItem = new VisualElement();
                slotItem.AddToClassList("slot-item");

                var header = new VisualElement();
                header.AddToClassList("slot-header");
                header.Add(new Label($"Slot {slot.SlotIndex}: {slot.SlotName}") { name = "slot-name" });

                var matField = new ObjectField("目标材质") { objectType = typeof(Material), value = slot.MaterialRef };
                matField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_manager, "修改材质");
                    slot.MaterialRef = evt.newValue as Material;
                    if (slot.MaterialRef != null && string.IsNullOrEmpty(slot.FuzzyName)) slot.FuzzyName = slot.MaterialRef.name;
                    EditorUtility.SetDirty(_manager);
                });

                var fuzzyField = new TextField("模糊名称匹配") { value = slot.FuzzyName };
                fuzzyField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_manager, "修改模糊名称");
                    slot.FuzzyName = evt.newValue;
                    EditorUtility.SetDirty(_manager);
                });

                slotItem.Add(header);
                slotItem.Add(matField);
                slotItem.Add(fuzzyField);

                // Drag Logic for individual slots
                slotItem.RegisterCallback<DragUpdatedEvent>(evt =>
                {
                    if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is Material)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        evt.StopPropagation();
                    }
                });
                slotItem.RegisterCallback<DragPerformEvent>(evt =>
                {
                    if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is Material mat)
                    {
                        DragAndDrop.AcceptDrag();
                        Undo.RecordObject(_manager, "拖放材质");
                        slot.MaterialRef = mat;
                        if (string.IsNullOrEmpty(slot.FuzzyName)) slot.FuzzyName = mat.name;
                        EditorUtility.SetDirty(_manager);
                        RefreshMaterialSlots();
                        evt.StopPropagation();
                    }
                });
                _slotsContainer.Add(slotItem);
            }
        }

        private void OnAddRendererClicked()
        {
            if (_manager == null) return;
            RendererSelectionWindow.ShowWindow(_manager.gameObject, (selectedRenderers) =>
            {
                if (selectedRenderers == null || selectedRenderers.Count == 0) return;
                Undo.RecordObject(_manager, "添加渲染器");
                bool changed = false;
                foreach (var r in selectedRenderers)
                {
                    var config = new RendererConfig
                    {
                        RendererPath = GetRelativePath(r.transform, _manager.transform),
                        TargetRenderer = r,
                        MatchMode = MatchMode.NameThenIndex
                    };
                    for (int i = 0; i < r.sharedMaterials.Length; i++)
                    {
                        var mat = r.sharedMaterials[i];
                        config.MaterialSlots.Add(new MaterialSlotConfig
                        {
                            SlotIndex = i,
                            SlotName = mat ? mat.name : $"材质 {i}",
                            MaterialRef = mat,
                            FuzzyName = mat ? mat.name : ""
                        });
                    }
                    _currentGroup.RendererConfigs.Add(config);
                    changed = true;
                }
                if (changed)
                {
                    _rendererList.ClearSelection();
                    _rendererList.RefreshItems();
                    EditorUtility.SetDirty(_manager);
                }
            });
        }

        private void OnRemoveRendererClicked()
        {
            if (_currentGroup == null) return;
            if (_rendererList != null && _rendererList.selectedIndex >= 0)
            {
                Undo.RecordObject(_manager, "移除渲染器");
                int removedIndex = _rendererList.selectedIndex;
                _currentGroup.RendererConfigs.RemoveAt(removedIndex);
                _rendererList.RefreshItems();
                
                // 智能选中：优先选中下一个，如果没有则选中上一个
                var configs = _filteredConfigs ?? _currentGroup.RendererConfigs;
                if (configs.Count > 0)
                {
                    int newIndex = Mathf.Min(removedIndex, configs.Count - 1);
                    _rendererList.selectedIndex = newIndex;
                    _selectedRendererIndex = newIndex;
                }
                else
                {
                    _selectedRendererIndex = -1;
                }
                
                RefreshMaterialSlots();
                EditorUtility.SetDirty(_manager);
            }
        }

        private void OnSlotsDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is Material)
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        private void OnSlotsDragPerform(DragPerformEvent evt)
        {
            if (_currentGroup == null || _selectedRendererIndex < 0) return;
            var materials = new List<Material>();
            foreach (var obj in DragAndDrop.objectReferences) if (obj is Material m) materials.Add(m);

            if (materials.Count > 0)
            {
                DragAndDrop.AcceptDrag();
                Undo.RecordObject(_manager, "拖放材质");
                var config = _currentGroup.RendererConfigs[_selectedRendererIndex];
                for (int i = 0; i < Mathf.Min(materials.Count, config.MaterialSlots.Count); i++)
                {
                    config.MaterialSlots[i].MaterialRef = materials[i];
                    if (string.IsNullOrEmpty(config.MaterialSlots[i].FuzzyName))
                        config.MaterialSlots[i].FuzzyName = materials[i].name;
                }
                EditorUtility.SetDirty(_manager);
                RefreshMaterialSlots();
            }
        }

        private string GetRelativePath(Transform target, Transform root)
        {
            if (target == root) return "";
            if (target.parent == null) return target.name;
            var path = target.name;
            while (target.parent != root && target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }
    }
}
