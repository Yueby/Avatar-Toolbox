using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Yueby.Tools.AvatarToolbox.MaterialPreset;
using Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Services;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components
{
    public class PresetGridView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<PresetGridView> { }

        // Events - Groups
        public event Action<PresetGroup> OnOpenDetail;
        public event Action<PresetGroup> OnCreatePreview;
        public event Action<PresetGroup> OnCreateObject;
        public event Action<PresetFolder> OnCreateFolderObjects;
        public event Action<PresetGroup> OnDestroyPreview;
        public event Action<PresetGroup> OnApply;
        public event Action<PresetGroup> OnDelete;
        public event Action<PresetGroup, string> OnRename;
        public event Action<PresetGroup> OnCaptureThumbnail;
        public event Action<PresetGroup> OnLocatePreview;
        public event Action<IPresetItem, int> OnReorder; // draggedItem, newDisplayOrder
        
        // Events - Folders
        public event Action<PresetFolder> OnFolderOpen;
        public event Action<PresetFolder> OnFolderDelete;
        public event Action<PresetFolder, string> OnFolderRename;
        public event Action<PresetFolder> OnFolderPreviewToggle;
        public event Action<IPresetItem, PresetFolder> OnDropToFolder; // item, targetFolder

        // Events - Forwarded from Toolbar
        public event Action<bool, PresetFolder> OnPreviewToggle;
        public event Action OnClearPreviews;
        public event Action OnKeepPreviews;

        private VisualElement _gridContent;
        private ScrollView _scrollView;
        private VisualTreeAsset _cardTemplate;
        private VisualTreeAsset _folderTemplate;
        
        private MaterialPresetManager _manager;
        private string _currentFilter = "";
        private MainToolbar _toolbar;
        private BreadcrumbNavigation _breadcrumb; // 面包屑引用
        
        // Folder Navigation
        private PresetFolder _currentFolder; // null = root
        private List<PresetFolder> _folderPath = new List<PresetFolder>();
        
        // Navigation event for external breadcrumb sync
        public event Action<PresetFolder, List<PresetFolder>> OnNavigationChanged;

        // Drag & Drop
        private bool _isMouseDown;
        private Vector2 _mouseDownPos; 
        
        private VisualElement _dragGhost;
        private VisualElement _placeholder;
        private VisualElement _draggedCard;
        private IPresetItem _draggedItem; // Changed from PresetGroup to IPresetItem to support both

        public PresetGridView()
        {
            style.flexGrow = 1;
            
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.name = "grid-scroll";
            _scrollView.AddToClassList("grid-scroll-view");
            _scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            Add(_scrollView);

            _gridContent = new VisualElement();
            _gridContent.name = "grid-content";
            _gridContent.AddToClassList("grid-content");
            _scrollView.Add(_gridContent);

            // Drag & Drop Listeners
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DragLeaveEvent>(OnDragLeave);
        }

        public void Initialize(VisualTreeAsset cardTemplate, VisualTreeAsset folderTemplate, MainToolbar toolbar, BreadcrumbNavigation breadcrumb = null)
        {
            _cardTemplate = cardTemplate;
            _folderTemplate = folderTemplate;
            _toolbar = toolbar;
            _breadcrumb = breadcrumb;

            if (_toolbar != null)
            {
                _toolbar.OnSearch += (value) => Refresh(_manager, value);
                _toolbar.OnNewGroup += OnNewGroupClicked;
                _toolbar.OnNewFolder += OnNewFolderClicked;
                _toolbar.OnSnapshot += OnSnapshotClicked;
                
                // Forward these to Window controller
                _toolbar.OnClearPreviews += () => OnClearPreviews?.Invoke();
                _toolbar.OnKeepPreviews += () => OnKeepPreviews?.Invoke();
                _toolbar.OnPreviewToggle += (isOn) => OnPreviewToggle?.Invoke(isOn, _currentFolder);
            }
        }
        
        // Exposed navigation state
        public PresetFolder CurrentFolder => _currentFolder;
        public List<PresetFolder> FolderPath => new List<PresetFolder>(_folderPath);
        
        // Folder Navigation Methods
        public void NavigateToFolder(PresetFolder folder)
        {
            if (folder == null) // Navigate to root
            {
                _currentFolder = null;
                _folderPath.Clear();
            }
            else
            {
                _currentFolder = folder;
                if (!_folderPath.Contains(folder))
                    _folderPath.Add(folder);
            }
            
            OnNavigationChanged?.Invoke(_currentFolder, _folderPath);
            Refresh(_manager, _currentFilter);
        }
        
        public void NavigateToPath(List<PresetFolder> path)
        {
            _folderPath = new List<PresetFolder>(path ?? new List<PresetFolder>());
            _currentFolder = _folderPath.Count > 0 ? _folderPath[_folderPath.Count - 1] : null;
            OnNavigationChanged?.Invoke(_currentFolder, _folderPath);
            Refresh(_manager, _currentFilter);
        }

        public void Refresh(MaterialPresetManager manager, string filter = "")
        {
            _manager = manager;
            _currentFilter = filter;
            
            _gridContent.Clear();
            CleanupDragVisuals();

            if (_manager == null)
            {
                _gridContent.Add(new Label("No MaterialPresetManager found."));
                return;
            }
            
            // Migrate old data if needed
            #if UNITY_EDITOR
            _manager.MigrateToTreeStructure();
            #endif

            // Get items based on current folder
            IEnumerable<IPresetItem> items;
            if (_currentFolder == null)
                items = _manager.RootItems;
            else
                items = _currentFolder.GetAllChildren();
            
            int index = 0;
            foreach (var item in items)
            {
                // Apply filter
                if (!string.IsNullOrEmpty(_currentFilter) && !item.Name.ToLowerInvariant().Contains(_currentFilter.ToLowerInvariant()))
                    continue;
                
                if (item.ItemType == PresetItemType.Folder)
                    BindFolderCard(item as PresetFolder, index);
                else
                    BindGroupCard(item as PresetGroup, index);
                
                index++;
            }
            
            UpdateToolbarPreviewState();
        }
        
        private void UpdateToolbarPreviewState()
        {
            if (_toolbar == null || _manager == null) return;
            
            bool isActive = false;
            var groups = _currentFolder == null ? _manager.GetAllGroups() : _currentFolder.GetAllGroupsRecursive();
            
            foreach (var group in groups)
            {
                foreach(var obj in _manager.ActivePreviews)
                {
                    if (obj is PreviewService.PreviewInstance pi && pi.Group == group && pi.Instance != null)
                    {
                        isActive = true;
                        break;
                    }
                }
                if (isActive) break;
            }
            
            _toolbar.SetPreviewToggleWithoutNotify(isActive);
        }

        #region Toolbar Actions

        private void OnNewGroupClicked()
        {
            if (_manager == null) return;

            Undo.RecordObject(_manager, "添加新预设组");
            var newGroup = new PresetGroup 
            { 
                GroupName = "新预设组 " + (_manager.GetAllGroups().Count + 1),
                DisplayOrder = 0 // Will be handled by list
            };
            
            // Add to current folder or root
            if (_currentFolder != null)
            {
                newGroup.DisplayOrder = _currentFolder.Children.Any() ? _currentFolder.Children.Max(i => i.DisplayOrder) + 1 : 0;
                _currentFolder.Children.Add(newGroup);
            }
            else
            {
                newGroup.DisplayOrder = _manager.RootItems.Any() ? _manager.RootItems.Max(i => i.DisplayOrder) + 1 : 0;
                _manager.RootItemsList.Add(newGroup);
            }
            
            EditorUtility.SetDirty(_manager);
            Refresh(_manager, _currentFilter);
        }

        private void OnNewFolderClicked()
        {
            if (_manager == null) return;
            
            Undo.RecordObject(_manager, "添加新文件夹");
            var newFolder = new PresetFolder 
            { 
                Name = "新文件夹 " + (_manager.RootFolders.Count + 1),
                DisplayOrder = -1 // 设为 -1 确保排序在最前
            };
            
            // 获取目标列表
            List<IPresetItem> targetList;
            if (_currentFolder != null)
            {
                _currentFolder.Children.Add(newFolder);
                targetList = _currentFolder.Children;
            }
            else
            {
                _manager.RootItemsList.Add(newFolder);
                targetList = _manager.RootItemsList;
            }
            
            // 重新排序所有项目，确保顺序正确且 DisplayOrder 连续
            var sortedItems = targetList.OrderBy(item => item.DisplayOrder).ToList();
            for (int i = 0; i < sortedItems.Count; i++)
            {
                sortedItems[i].DisplayOrder = i;
            }
            
            EditorUtility.SetDirty(_manager);
            Refresh(_manager, _currentFilter);
            
            // 滚动到顶部
            _scrollView.scrollOffset = Vector2.zero;
        }

        private void OnSnapshotClicked()
        {
            if (_manager == null) return;
            var renderers = _manager.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0) { EditorUtility.DisplayDialog("快照", "未找到渲染器。", "确定"); return; }
            
            var group = new PresetGroup 
            {
                GroupName = "快照 " + DateTime.Now.ToString("HH:mm:ss"),
                DisplayOrder = 0 // Will be handled by list
            };
            Undo.RecordObject(_manager, "Snapshot");
             foreach (var r in renderers)
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
                group.RendererConfigs.Add(config);
            }
            
            // Add to current folder or root
            if (_currentFolder != null)
            {
                group.DisplayOrder = _currentFolder.Children.Any() ? _currentFolder.Children.Max(i => i.DisplayOrder) + 1 : 0;
                _currentFolder.Children.Add(group);
            }
            else
            {
                group.DisplayOrder = _manager.RootItems.Any() ? _manager.RootItems.Max(i => i.DisplayOrder) + 1 : 0;
                _manager.RootItemsList.Add(group);
            }
            
            EditorUtility.SetDirty(_manager);
            Refresh(_manager, _currentFilter);
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

        #endregion

        private void BindGroupCard(PresetGroup group, int index)
        {
            if (_cardTemplate == null) return;
            
            var cardInstance = _cardTemplate.Instantiate();
            cardInstance.userData = group;
            var root = cardInstance.Q("card-root");
            
            // Bind Logic
            BindCardData(cardInstance, group, index);

            // Mouse Interaction
            root.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button == 0) {
                    _isMouseDown = true;
                    _mouseDownPos = evt.mousePosition; 
                    // Offset calculation moved to StartDrag for better accuracy
                }
            });
            
            root.RegisterCallback<MouseMoveEvent>(evt => {
                if (_isMouseDown && evt.pressedButtons == 1) {
                    if (Vector2.Distance(_mouseDownPos, evt.mousePosition) > 5) {
                        _isMouseDown = false;
                        // root.LocalToWorld provides world pos of mouse
                        var worldPos = root.LocalToWorld(evt.mousePosition);
                        StartDrag(group, cardInstance, worldPos); 
                    }
                }
            });
            
            root.RegisterCallback<MouseUpEvent>(evt => {
                _isMouseDown = false;
            });

            root.RegisterCallback<ClickEvent>(evt => {
                if (evt.clickCount == 2)
                {
                    OnOpenDetail?.Invoke(group);
                }
            });

            // Context Menu
            root.AddManipulator(new ContextualMenuManipulator(evt => {
                var renameField = cardInstance.Q<TextField>("card-rename-field");
                var title = cardInstance.Q<Label>("card-title");
                
                evt.menu.AppendAction("重命名", _ => {
                    if (title != null && renameField != null) {
                        title.style.display = DisplayStyle.None;
                        renameField.style.display = DisplayStyle.Flex;
                        renameField.schedule.Execute(() => renameField.Focus()).ExecuteLater(10);
                    }
                });
                evt.menu.AppendAction("创建对象（非预览）", _ => OnCreateObject?.Invoke(group));
                evt.menu.AppendAction("捕获缩略图", _ => OnCaptureThumbnail?.Invoke(group));
                evt.menu.AppendAction("删除", _ => OnDelete?.Invoke(group));
            }));
            
            _gridContent.Add(cardInstance);
        }
        
        private void BindFolderCard(PresetFolder folder, int index)
        {
            if (_folderTemplate == null) return;
            
            var cardInstance = _folderTemplate.Instantiate();
            cardInstance.userData = folder;
            var root = cardInstance.Q("folder-root");
            
            if (root == null) return;
            
            // Bind Data
            var indexLabel = root.Q<Label>("folder-index");
            if (indexLabel != null) indexLabel.text = (index + 1).ToString("00");
            
            // 颜色条已移除（使用缩略图代替）
            
            var image = root.Q("folder-image");
            if (image != null && folder.Thumbnail != null) 
                image.style.backgroundImage = folder.Thumbnail;
            
            var title = root.Q<Label>("folder-title");
            if (title != null) title.text = folder.Name;
            
            var info = root.Q<Label>("folder-info");
            if (info != null) info.text = $"{folder.GetChildrenCount()} 项";
            
            // Click to navigate (Double click)
            root.RegisterCallback<ClickEvent>(evt => {
                if (evt.clickCount == 2)
                {
                    OnFolderOpen?.Invoke(folder);
                    evt.StopPropagation();
                }
            });
            
            // Preview Toggle - Recursively preview all groups in folder
            var previewToggle = root.Q<Toggle>("toggle-folder-preview");
            if (previewToggle != null)
            {
                // Check active state
                bool isAnyActive = false;
                if (_manager != null)
                {
                    var allGroups = folder.GetAllGroupsRecursive();
                    foreach (var group in allGroups)
                    {
                        foreach(var obj in _manager.ActivePreviews)
                        {
                            if (obj is PreviewService.PreviewInstance pi && pi.Group == group && pi.Instance != null)
                            {
                                isAnyActive = true;
                                break;
                            }
                        }
                        if (isAnyActive) break;
                    }
                }
                
                previewToggle.SetValueWithoutNotify(isAnyActive);
                
                previewToggle.RegisterValueChangedCallback(evt => {
                    OnFolderPreviewToggle?.Invoke(folder);
                    evt.StopPropagation();
                });
                previewToggle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            }
            
            // Context Menu
            root.AddManipulator(new ContextualMenuManipulator(evt => {
                var renameField = cardInstance.Q<TextField>("folder-rename-field");
                var titleLabel = cardInstance.Q<Label>("folder-title");
                
                evt.menu.AppendAction("重命名", _ => {
                    if (titleLabel != null && renameField != null) {
                        titleLabel.style.display = DisplayStyle.None;
                        renameField.style.display = DisplayStyle.Flex;
                        renameField.value = folder.Name;
                        renameField.schedule.Execute(() => renameField.Focus()).ExecuteLater(10);
                    }
                });
                evt.menu.AppendAction("创建该文件夹下所有对象（非预览）", _ => OnCreateFolderObjects?.Invoke(folder));
                evt.menu.AppendAction("删除", _ => OnFolderDelete?.Invoke(folder));
            }));
            
            // Rename field handlers
            var renameFieldElement = cardInstance.Q<TextField>("folder-rename-field");
            if (renameFieldElement != null && title != null)
            {
                renameFieldElement.value = folder.Name;
                EventCallback<FocusOutEvent> onCommit = _ => {
                    OnFolderRename?.Invoke(folder, renameFieldElement.value);
                    title.text = renameFieldElement.value;
                    title.style.display = DisplayStyle.Flex;
                    renameFieldElement.style.display = DisplayStyle.None;
                };
                renameFieldElement.RegisterCallback(onCommit);
                renameFieldElement.RegisterCallback<KeyDownEvent>(evt => {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) renameFieldElement.Blur();
                    else if (evt.keyCode == KeyCode.Escape) {
                        renameFieldElement.value = folder.Name;
                        title.style.display = DisplayStyle.Flex;
                        renameFieldElement.style.display = DisplayStyle.None;
                        evt.StopPropagation();
                    }
                });
                renameFieldElement.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            }
            
            // Drag support for folder
            root.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button == 0) {
                    _isMouseDown = true;
                    _mouseDownPos = evt.mousePosition;
                }
            });
            
            root.RegisterCallback<MouseMoveEvent>(evt => {
                if (_isMouseDown && evt.pressedButtons == 1) {
                    if (Vector2.Distance(_mouseDownPos, evt.mousePosition) > 5) {
                        _isMouseDown = false;
                        var worldPos = root.LocalToWorld(evt.mousePosition);
                        StartDrag(folder, cardInstance, worldPos);
                    }
                }
            });
            
            root.RegisterCallback<MouseUpEvent>(evt => {
                _isMouseDown = false;
            });
            
            // Drop zone for receiving items
            root.RegisterCallback<DragUpdatedEvent>(evt => {
                if (DragAndDrop.GetGenericData("PresetItem") is IPresetItem draggedItem)
                {
                    // Don't allow dropping folder into itself or its descendants
                    if (draggedItem == folder)
                    {
                        root.style.backgroundColor = Color.clear;
                        return;
                    }
                    if (draggedItem is PresetFolder draggedFolder && IsDescendant(folder, draggedFolder))
                    {
                        root.style.backgroundColor = Color.clear;
                        return;
                    }
                    
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    root.style.backgroundColor = new Color(0.3f, 0.6f, 0.3f, 0.3f);
                    // Don't stop propagation - let parent handle ghost movement
                }
            });
            
            root.RegisterCallback<DragLeaveEvent>(evt => {
                root.style.backgroundColor = Color.clear;
            });
            
            root.RegisterCallback<DragPerformEvent>(evt => {
                if (DragAndDrop.GetGenericData("PresetItem") is IPresetItem draggedItem)
                {
                    if (draggedItem != folder && !(draggedItem is PresetFolder df && IsDescendant(folder, df)))
                    {
                        OnDropToFolder?.Invoke(draggedItem, folder);
                        DragAndDrop.AcceptDrag();
                        evt.StopPropagation(); // Only stop propagation on perform
                    }
                }
                root.style.backgroundColor = Color.clear;
            });
            
            _gridContent.Add(cardInstance);
        }
        
        private bool IsDescendant(PresetFolder potentialChild, PresetFolder potentialParent)
        {
            foreach (var child in potentialParent.Children.OfType<PresetFolder>())
            {
                if (child == potentialChild) return true;
                if (IsDescendant(potentialChild, child)) return true;
            }
            return false;
        }

        private void BindCardData(TemplateContainer cardInstance, PresetGroup group, int index)
        {
            var indexLabel = cardInstance.Q<Label>("card-index");
            if (indexLabel != null) indexLabel.text = (index + 1).ToString("00");

            // 颜色条已移除（使用缩略图代替）
            
            var image = cardInstance.Q("card-image");
            if (image != null) image.style.backgroundImage = group.Thumbnail;

            var title = cardInstance.Q<Label>("card-title");
            if (title != null) title.text = group.GroupName;
            
            var info = cardInstance.Q<Label>("card-info");
            if (info != null) info.text = $"{group.RendererConfigs.Count} 项";

            var previewToggle = cardInstance.Q<Toggle>("toggle-preview");
            if (previewToggle != null)
            {
                bool isActive = false;
                if (_manager != null)
                {
                foreach(var obj in _manager.ActivePreviews)
                {
                    if (obj is PreviewService.PreviewInstance p && p.Group == group && p.Instance != null)
                    {
                        isActive = true;
                        break;
                        }
                    }
                }
                
                previewToggle.SetValueWithoutNotify(isActive);
                if (cardInstance.userData == group)
                {
                    previewToggle.RegisterValueChangedCallback(evt => {
                        if (evt.newValue) OnCreatePreview?.Invoke(group);
                        else OnDestroyPreview?.Invoke(group);
                        evt.StopPropagation();
                    });
                    previewToggle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                }
            }

            if (cardInstance.userData == group)
            {
                cardInstance.Q<Button>("btn-apply")?.RegisterCallback<ClickEvent>(evt => {
                    OnApply?.Invoke(group);
                    evt.StopPropagation();
                });

                var btnLocate = cardInstance.Q<Button>("btn-locate");
                if (btnLocate != null)
                {
                    bool hasPreview = false;
                    if (_manager != null)
                    {
                    foreach(var obj in _manager.ActivePreviews)
                    {
                        if (obj is PreviewService.PreviewInstance p && p.Group == group && p.Instance != null)
                        {
                            hasPreview = true;
                            break;
                            }
                        }
                    }
                    
                    btnLocate.SetEnabled(hasPreview);
                    btnLocate.style.opacity = hasPreview ? 1.0f : 0.3f;
                    btnLocate.RegisterCallback<ClickEvent>(evt => {
                        OnLocatePreview?.Invoke(group);
                        evt.StopPropagation();
                    });
                }

                var renameField = cardInstance.Q<TextField>("card-rename-field");
                if (renameField != null && title != null)
                {
                    renameField.value = group.GroupName;
                    EventCallback<FocusOutEvent> onCommit = _ => {
                        OnRename?.Invoke(group, renameField.value);
                        title.text = renameField.value;
                        title.style.display = DisplayStyle.Flex;
                        renameField.style.display = DisplayStyle.None;
                    };
                    renameField.RegisterCallback(onCommit);
                    renameField.RegisterCallback<KeyDownEvent>(evt => {
                        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) renameField.Blur();
                        else if (evt.keyCode == KeyCode.Escape) {
                            renameField.value = group.GroupName;
                            title.style.display = DisplayStyle.Flex;
                            renameField.style.display = DisplayStyle.None;
                            evt.StopPropagation();
                        }
                    });
                    renameField.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                }
            }
        }

        // --- Drag & Drop ---

        private void StartDrag(IPresetItem item, VisualElement visualSource, Vector2 mouseWorldPos)
        {
            if (_draggedCard != null) return; 

            _draggedItem = item;
            _draggedCard = visualSource; // TemplateContainer

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("PresetItem", item);
            DragAndDrop.StartDrag(item.ItemType == PresetItemType.Folder ? "移动文件夹" : "移动预设组");

            // 1. Create Ghost
            CreateDragGhost(item, mouseWorldPos);

            // 2. Create Placeholder (Visual Clone with low opacity)
            TemplateContainer placeholderInstance;
            if (item.ItemType == PresetItemType.Folder)
            {
                placeholderInstance = _folderTemplate.Instantiate();
                var folder = item as PresetFolder;
                var root = placeholderInstance.Q("folder-root");
                if (root != null)
                {
                    root.Q<Label>("folder-title").text = folder.Name;
                    // 颜色条已移除
                }
            }
            else
            {
                placeholderInstance = _cardTemplate.Instantiate();
                var group = item as PresetGroup;
                BindCardData(placeholderInstance, group, 0);
            }
            
            _placeholder = placeholderInstance;
            
            // Match layout properties
            _placeholder.style.width = _draggedCard.resolvedStyle.width;
            _placeholder.style.height = _draggedCard.resolvedStyle.height;
            _placeholder.style.marginRight = _draggedCard.resolvedStyle.marginRight;
            _placeholder.style.marginLeft = _draggedCard.resolvedStyle.marginLeft;
            _placeholder.style.marginTop = _draggedCard.resolvedStyle.marginTop;
            _placeholder.style.marginBottom = _draggedCard.resolvedStyle.marginBottom;
            
            // Visual style
            _placeholder.style.opacity = 0.3f; // Faint
            _placeholder.userData = "placeholder";
            
            // Disable interactions on placeholder
            _placeholder.pickingMode = PickingMode.Ignore;
            _placeholder.Query().ForEach(e => e.pickingMode = PickingMode.Ignore);

            int index = _gridContent.IndexOf(_draggedCard);
            _gridContent.Insert(index, _placeholder);

            _draggedCard.style.display = DisplayStyle.None;
        }

        private void CreateDragGhost(IPresetItem item, Vector2 worldPos)
        {
            if (_dragGhost != null) _dragGhost.RemoveFromHierarchy();

            // Instantiate appropriate template for the ghost
            TemplateContainer ghostInstance;
            if (item.ItemType == PresetItemType.Folder)
            {
                ghostInstance = _folderTemplate.Instantiate();
                var folder = item as PresetFolder;
                var root = ghostInstance.Q("folder-root");
                if (root != null)
                {
                    root.Q<Label>("folder-title").text = folder.Name;
                    // 颜色条已移除
                    root.Q<Label>("folder-info").text = $"{folder.GetChildrenCount()} 项";
                }
            }
            else
            {
                ghostInstance = _cardTemplate.Instantiate();
                var group = item as PresetGroup;
                BindCardData(ghostInstance, group, 0);
            }
            
            _dragGhost = ghostInstance;
            
            // Set styles to make it floating
            _dragGhost.style.position = Position.Absolute;
            // Let the inner content determine size, but cap it to original size to avoid expansion?
            // Actually, setting explicit width/height matching original is safer for Ghost.
            _dragGhost.style.width = _draggedCard.resolvedStyle.width;
            _dragGhost.style.height = _draggedCard.resolvedStyle.height;
            
            _dragGhost.pickingMode = PickingMode.Ignore; 
            _dragGhost.style.opacity = 0.8f; 
            
            // Disable interactions on ghost contents
            _dragGhost.Query().ForEach(e => e.pickingMode = PickingMode.Ignore);

            Add(_dragGhost); 
            UpdateGhostPos(worldPos);
        }

        // Simply center the ghost on the mouse
        private void UpdateGhostPos(Vector2 worldPos)
        {
            if (_dragGhost != null)
            {
                var localPos = this.WorldToLocal(worldPos);
                
                // Center the ghost on the mouse cursor
                // We use resolvedStyle width/height which should be accurate for the instantiated ghost
                float width = _dragGhost.resolvedStyle.width;
                float height = _dragGhost.resolvedStyle.height;
                
                // Fallback if resolved style is not yet ready (0)
                if (width < 1) width = 140; 
                if (height < 1) height = 180;

                _dragGhost.style.top = localPos.y - (height * 0.5f); 
                _dragGhost.style.left = localPos.x - (width * 0.5f);
            }
        }

        private void CleanupDragVisuals()
        {
            if (_dragGhost != null)
            {
                _dragGhost.RemoveFromHierarchy();
                _dragGhost = null;
            }
            if (_placeholder != null)
            {
                _placeholder.RemoveFromHierarchy();
                _placeholder = null;
            }
            if (_draggedCard != null)
            {
                _draggedCard.style.display = DisplayStyle.Flex;
                _draggedCard = null;
            }
            _draggedItem = null;
        }

        public void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (_draggedItem != null) 
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                // Calculate World Position safely regardless of who triggered the event
                Vector2 worldPos;
                if (evt.currentTarget is VisualElement targetElement)
                {
                    worldPos = targetElement.LocalToWorld(evt.localMousePosition);
                }
                else
                {
                    // Fallback
                    worldPos = this.LocalToWorld(evt.localMousePosition);
                }

                UpdateGhostPos(worldPos);
                
                // 检测鼠标是否在面包屑区域，动态调整ghost透明度
                if (_breadcrumb != null && _dragGhost != null)
                {
                    var breadcrumbLocalPos = _breadcrumb.WorldToLocal(worldPos);
                    bool isOverBreadcrumb = _breadcrumb.layout.Contains(breadcrumbLocalPos);
                    _dragGhost.style.opacity = isOverBreadcrumb ? 0.3f : 0.8f;
                }

                // Auto Scroll Logic
                // Convert world pos to local pos of THIS GridView to check bounds
                var gridLocalPos = this.WorldToLocal(worldPos);

                float scrollThreshold = 40f;
                float scrollSpeed = 15f;
                
                // 获取ScrollView的可滚动范围
                var contentHeight = _gridContent.layout.height;
                var viewportHeight = _scrollView.layout.height;
                var maxScrollOffset = Mathf.Max(0, contentHeight - viewportHeight);
                
                // 只在有可滚动内容时才执行自动滚动
                if (maxScrollOffset > 1f) // 至少有1px的可滚动空间
                {
                    var currentOffset = _scrollView.scrollOffset.y;
                    
                    // Check bounds using local pos
                    if (gridLocalPos.y < scrollThreshold && currentOffset > 0)
                    {
                        // 向上滚动（减小offset）
                        var newOffset = Mathf.Max(0, currentOffset - scrollSpeed);
                        _scrollView.scrollOffset = new Vector2(_scrollView.scrollOffset.x, newOffset);
                    }
                    else if (gridLocalPos.y > this.layout.height - scrollThreshold && currentOffset < maxScrollOffset)
                    {
                        // 向下滚动（增加offset），但不超过最大值
                        var newOffset = Mathf.Min(maxScrollOffset, currentOffset + scrollSpeed);
                        _scrollView.scrollOffset = new Vector2(_scrollView.scrollOffset.x, newOffset);
                    }
                }

                VisualElement targetCard = null;
                // Use WorldPos for hit testing to be consistent
                foreach (var child in _gridContent.Children())
                {
                    if (child == _placeholder || child == _draggedCard) continue;

                    // Check if point is inside child
                    if (child.worldBound.Contains(worldPos))
                    {
                        targetCard = child;
                        break;
                    }
                }

                if (targetCard != null)
                {
                    // Check if target is the same type as dragged item
                    var targetItem = targetCard.userData;
                    bool isSameType = false;
                    
                    if (_draggedItem.ItemType == PresetItemType.Folder && targetItem is PresetFolder)
                        isSameType = true;
                    else if (_draggedItem.ItemType == PresetItemType.Group && targetItem is PresetGroup)
                        isSameType = true;
                    
                    // Only allow reordering within same type
                    if (isSameType)
                    {
                        int targetIndex = _gridContent.IndexOf(targetCard);
                        int placeholderIndex = _gridContent.IndexOf(_placeholder);

                        if (targetIndex != placeholderIndex)
                        {
                            _gridContent.Remove(_placeholder);
                            _gridContent.Insert(targetIndex, _placeholder);
                        }
                    }
                }
            }
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
        }

        public void OnDragPerform(DragPerformEvent evt)
        {
            if (_draggedItem != null && _placeholder != null)
            {
                DragAndDrop.AcceptDrag();
                
                int placeholderIndex = _gridContent.IndexOf(_placeholder);
                
                // 计算placeholder位置对应的DisplayOrder
                // 方法：找到placeholder前后的items，计算中间值
                
                IPresetItem beforeItem = null;
                IPresetItem afterItem = null;
                
                // 向前查找（跳过placeholder和draggedCard）
                for (int i = placeholderIndex - 1; i >= 0; i--)
                {
                    var card = _gridContent[i];
                    if (card == _placeholder || card == _draggedCard) continue;
                    
                    beforeItem = card.userData as IPresetItem;
                    if (beforeItem != null) break;
                }
                
                // 向后查找（跳过placeholder和draggedCard）
                for (int i = placeholderIndex + 1; i < _gridContent.childCount; i++)
                {
                    var card = _gridContent[i];
                    if (card == _placeholder || card == _draggedCard) continue;
                    
                    afterItem = card.userData as IPresetItem;
                    if (afterItem != null) break;
                }
                
                // 计算新的DisplayOrder
                int newDisplayOrder;
                if (beforeItem == null && afterItem == null)
                {
                    // 唯一的item
                    newDisplayOrder = 0;
                }
                else if (beforeItem == null)
                {
                    // 插入到最前面
                    newDisplayOrder = afterItem.DisplayOrder - 1;
                }
                else if (afterItem == null)
                {
                    // 插入到最后面
                    newDisplayOrder = beforeItem.DisplayOrder + 1;
                }
                else
                {
                    // 插入到中间，使用平均值
                    newDisplayOrder = (beforeItem.DisplayOrder + afterItem.DisplayOrder) / 2;
                    
                    // 如果平均值等于beforeItem.DisplayOrder（说明它们相邻），需要重新分配
                    if (newDisplayOrder == beforeItem.DisplayOrder)
                    {
                        newDisplayOrder = beforeItem.DisplayOrder + 1;
                    }
                }
                
                OnReorder?.Invoke(_draggedItem, newDisplayOrder);
            }
            CleanupDragVisuals();
        }
    }
}