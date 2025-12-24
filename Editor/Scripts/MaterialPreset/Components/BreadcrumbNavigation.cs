using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using Yueby.Tools.AvatarToolbox.MaterialPreset;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components
{
    public class BreadcrumbNavigation : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<BreadcrumbNavigation> { }
        
        public event Action<List<PresetFolder>> OnNavigate;
        public event Action<IPresetItem, PresetFolder> OnDropToFolder; // item, targetFolder (null = root)
        
        private List<PresetFolder> _currentPath = new List<PresetFolder>();
        private string _currentGroupName;
        private VisualElement _container;
        
        public BreadcrumbNavigation()
        {
            // Load Stylesheet
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/yueby.tools.avatar-toolbox/Editor/Scripts/MaterialPreset/Components/BreadcrumbNavigation.uss");
            if (styleSheet != null) styleSheets.Add(styleSheet);
            
            AddToClassList("breadcrumb-bar");
            
            _container = new VisualElement();
            _container.style.flexDirection = FlexDirection.Row;
            _container.style.alignItems = Align.Center;
            _container.style.flexWrap = Wrap.NoWrap; // 不换行，保持单行显示
            Add(_container);
            
            // Build initial state (Root)
            Rebuild();
        }
        
        public void SetPath(List<PresetFolder> path, string groupName = null)
        {
            _currentPath = new List<PresetFolder>(path ?? new List<PresetFolder>());
            _currentGroupName = groupName;
            Rebuild();
        }
        
        private void Rebuild()
        {
            _container.Clear();
            
            // Root button with drop support
            var rootBtn = new Button(() => OnNavigate?.Invoke(new List<PresetFolder>())) 
            { 
                text = "Root"
            };
            rootBtn.AddToClassList("breadcrumb-item");
            
            // Add drop zone to move items to root
            rootBtn.RegisterCallback<DragUpdatedEvent>(evt => {
                if (DragAndDrop.GetGenericData("PresetItem") is IPresetItem)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    rootBtn.AddToClassList("breadcrumb-item--drag-hover");
                }
            });
            
            rootBtn.RegisterCallback<DragLeaveEvent>(evt => {
                rootBtn.RemoveFromClassList("breadcrumb-item--drag-hover");
            });
            
            rootBtn.RegisterCallback<DragPerformEvent>(evt => {
                if (DragAndDrop.GetGenericData("PresetItem") is IPresetItem item)
                {
                    OnDropToFolder?.Invoke(item, null); // null = root
                    DragAndDrop.AcceptDrag();
                    evt.StopPropagation();
                }
                rootBtn.RemoveFromClassList("breadcrumb-item--drag-hover");
            });
            
            _container.Add(rootBtn);
            
            // Path buttons
            for (int i = 0; i < _currentPath.Count; i++)
            {
                // Separator
                var separator = new Label("›"); // 使用更有设计感的符号
                separator.AddToClassList("breadcrumb-separator");
                _container.Add(separator);
                
                // Folder button with drop support
                var folder = _currentPath[i];
                var pathToFolder = _currentPath.GetRange(0, i + 1);
                
                // Check if this is the current folder (leaf node in Grid View)
                bool isCurrent = string.IsNullOrEmpty(_currentGroupName) && i == _currentPath.Count - 1;
                
                var btn = new Button(() => {
                    // Don't navigate if clicking on the current folder
                    if (!isCurrent)
                    {
                        OnNavigate?.Invoke(pathToFolder);
                    }
                }) 
                { 
                    text = folder.Name
                };
                btn.AddToClassList("breadcrumb-item");
                
                // Highlight current folder only if we are strictly IN that folder view (no group name suffix)
                if (isCurrent)
                {
                    btn.AddToClassList("breadcrumb-item--current");
                    // Don't disable - just handle click to do nothing above
                }
                
                // Add drop zone to move items to this folder
                var targetFolder = folder;
                btn.RegisterCallback<DragUpdatedEvent>(evt => {
                    if (DragAndDrop.GetGenericData("PresetItem") is IPresetItem draggedItem)
                    {
                        // Don't allow dropping folder into itself or its descendants
                        if (draggedItem == targetFolder) return;
                        if (draggedItem is PresetFolder df && IsDescendant(targetFolder, df)) return;
                        
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                        btn.AddToClassList("breadcrumb-item--drag-hover");
                    }
                });
                
                btn.RegisterCallback<DragLeaveEvent>(evt => {
                    btn.RemoveFromClassList("breadcrumb-item--drag-hover");
                });
                
                btn.RegisterCallback<DragPerformEvent>(evt => {
                    if (DragAndDrop.GetGenericData("PresetItem") is IPresetItem item)
                    {
                        if (item != targetFolder && !(item is PresetFolder df && IsDescendant(targetFolder, df)))
                        {
                            OnDropToFolder?.Invoke(item, targetFolder);
                            DragAndDrop.AcceptDrag();
                            evt.StopPropagation();
                        }
                    }
                    btn.RemoveFromClassList("breadcrumb-item--drag-hover");
                });
                
                _container.Add(btn);
            }
            
            // 如果有 groupName，添加最后一个不可点击的项
            if (!string.IsNullOrEmpty(_currentGroupName))
            {
                var separator = new Label("›");
                separator.AddToClassList("breadcrumb-separator");
                _container.Add(separator);
                
                var groupLabel = new Label(_currentGroupName);
                groupLabel.AddToClassList("breadcrumb-item");
                groupLabel.AddToClassList("breadcrumb-item--current");
                _container.Add(groupLabel);
            }
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
    }
}

