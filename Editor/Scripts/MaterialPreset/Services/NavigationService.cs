using System.Collections.Generic;
using UnityEngine.UIElements;
using Yueby.Tools.AvatarToolbox.MaterialPreset;
using Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Services
{
    public class NavigationService
    {
        private MaterialPresetManager _manager;
        private VisualElement _gridViewContainer;
        private VisualElement _detailViewContainer;
        private MainToolbar _gridToolbar;
        private DetailViewToolbar _detailToolbar;
        private PresetGridView _presetGridView;
        private PresetDetailView _presetDetailView;
        private BreadcrumbNavigation _globalBreadcrumb;
        private PreviewService _previewService;

        private PresetGroup _currentEditingGroup;

        public PresetGroup CurrentEditingGroup => _currentEditingGroup;

        public NavigationService(
            MaterialPresetManager manager,
            VisualElement gridViewContainer,
            VisualElement detailViewContainer,
            MainToolbar gridToolbar,
            DetailViewToolbar detailToolbar,
            PresetGridView presetGridView,
            PresetDetailView presetDetailView,
            BreadcrumbNavigation globalBreadcrumb,
            PreviewService previewService)
        {
            _manager = manager;
            _gridViewContainer = gridViewContainer;
            _detailViewContainer = detailViewContainer;
            _gridToolbar = gridToolbar;
            _detailToolbar = detailToolbar;
            _presetGridView = presetGridView;
            _presetDetailView = presetDetailView;
            _globalBreadcrumb = globalBreadcrumb;
            _previewService = previewService;
        }

        public void SwitchToDetailView(PresetGroup group)
        {
            _currentEditingGroup = group;

            _gridViewContainer.style.display = DisplayStyle.None;
            _detailViewContainer.style.display = DisplayStyle.Flex;

            _gridToolbar.style.display = DisplayStyle.None;
            _detailToolbar.style.display = DisplayStyle.Flex;

            UpdateBreadcrumbForDetailView(group);

            UpdateDetailPreviewToggle(group);

            _presetDetailView.Bind(group, _manager);
        }

        public void SwitchToGridView()
        {
            _currentEditingGroup = null;

            _detailViewContainer.style.display = DisplayStyle.None;
            _gridViewContainer.style.display = DisplayStyle.Flex;

            _detailToolbar.style.display = DisplayStyle.None;
            _gridToolbar.style.display = DisplayStyle.Flex;

            UpdateBreadcrumbForGridView();

            _presetGridView.Refresh(_manager);
        }

        public void UpdateBreadcrumbForDetailView(PresetGroup group)
        {
            var path = _presetGridView.FolderPath;
            _globalBreadcrumb.SetPath(path, group.GroupName);
        }

        public void UpdateBreadcrumbForGridView()
        {
            var path = _presetGridView.FolderPath;
            _globalBreadcrumb.SetPath(path, null);
        }

        public void NavigateToPath(List<PresetFolder> path)
        {
            if (_detailViewContainer.style.display == DisplayStyle.Flex)
            {
                SwitchToGridView();
            }
            _presetGridView.NavigateToPath(path);
        }

        private void UpdateDetailPreviewToggle(PresetGroup group)
        {
            bool isActive = _previewService?.IsPreviewActive(group) ?? false;
            _detailToolbar.SetPreviewToggleValue(isActive);
        }
    }
}

