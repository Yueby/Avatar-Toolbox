using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components
{
    public class DetailViewToolbar : Toolbar
    {
        public new class UxmlFactory : UxmlFactory<DetailViewToolbar> { }

        public event Action<string> OnSearch;
        public event Action<string> OnNameChanged;
        // public event Action OnBack; // Removed
        public event Action<bool> OnPreviewToggle;
        public event Action OnApply;
        public event Action OnCaptureThumbnail;
        public event Action OnClearPreviews;
        public event Action OnKeepPreviews;
        
        private TextField _nameField;
        private ToolbarSearchField _searchField;

        public DetailViewToolbar()
        {
            // 搜索框 (放到最左边)
            _searchField = new ToolbarSearchField();
            _searchField.style.width = 200;
            _searchField.style.marginRight = 10; // Add some spacing
            _searchField.RegisterValueChangedCallback(evt => OnSearch?.Invoke(evt.newValue));
            Add(_searchField);

            // 名称输入框
            _nameField = new TextField();
            _nameField.style.width = 150;
            _nameField.style.marginLeft = 5;
            _nameField.RegisterValueChangedCallback(evt => OnNameChanged?.Invoke(evt.newValue));
            Add(_nameField);

            // Spacer
            Add(new VisualElement { style = { flexGrow = 1 } });

            // 预览切换按钮
            var previewToggle = new ToolbarToggle();
            previewToggle.tooltip = "切换当前预设预览";
            var eyeIcon = EditorGUIUtility.IconContent("d_ViewToolOrbit").image as Texture2D;
            if (eyeIcon == null) eyeIcon = EditorGUIUtility.IconContent("ViewToolOrbit").image as Texture2D;
            
            if (eyeIcon != null)
            {
                previewToggle.style.backgroundImage = eyeIcon;
                previewToggle.style.width = 24;
                previewToggle.style.minWidth = 24;
                previewToggle.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                previewToggle.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                previewToggle.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                previewToggle.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var checkmark = previewToggle.Q<VisualElement>(className: "unity-toggle__checkmark");
                if (checkmark != null) checkmark.style.display = DisplayStyle.None;
            }
            else
            {
                previewToggle.text = "👁";
            }
            previewToggle.RegisterValueChangedCallback(evt => OnPreviewToggle?.Invoke(evt.newValue));
            Add(previewToggle);

            // 应用按钮
            var btnApply = new ToolbarButton(() => OnApply?.Invoke()) { tooltip = "应用预设到当前角色" };
            var applyIcon = EditorGUIUtility.IconContent("d_SaveAs").image as Texture2D;
            if (applyIcon == null) applyIcon = EditorGUIUtility.IconContent("SaveAs").image as Texture2D;
            
            if (applyIcon != null)
            {
                btnApply.style.backgroundImage = applyIcon;
                btnApply.style.width = 24;
                btnApply.style.minWidth = 24;
                btnApply.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                btnApply.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                btnApply.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                btnApply.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            else
            {
                btnApply.text = "应用";
            }
            Add(btnApply);

            // 捕获缩略图按钮
            var btnCapture = new ToolbarButton(() => OnCaptureThumbnail?.Invoke()) { tooltip = "捕获缩略图" };
            var camIcon = EditorGUIUtility.IconContent("d_FrameCapture").image as Texture2D;
            if (camIcon == null) camIcon = EditorGUIUtility.IconContent("Camera Icon").image as Texture2D;
            
            if (camIcon != null)
            {
                btnCapture.style.backgroundImage = camIcon;
                btnCapture.style.width = 24;
                btnCapture.style.minWidth = 24;
                btnCapture.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                btnCapture.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                btnCapture.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                btnCapture.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            else
            {
                btnCapture.text = "📷";
            }
            Add(btnCapture);

            // 选项菜单
            var menu = new ToolbarMenu { tooltip = "更多选项" };
            var menuIcon = EditorGUIUtility.IconContent("_Menu").image as Texture2D;
            if (menuIcon == null) menuIcon = EditorGUIUtility.IconContent("d__Menu").image as Texture2D;
            
            if (menuIcon != null)
            {
                menu.style.backgroundImage = menuIcon;
                menu.style.width = 40;
                menu.style.minWidth = 40;
                menu.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, new Length(4, LengthUnit.Pixel));
                menu.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                menu.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                menu.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                menu.style.paddingLeft = 4;
            }
            else
            {
                menu.text = "⋮"; // Fallback
            }
            
            menu.menu.AppendAction("清除所有预览", a => OnClearPreviews?.Invoke(), a => HasAnyPreview?.Invoke() == true ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("保持所有预览", a => OnKeepPreviews?.Invoke(), a => HasAnyPreview?.Invoke() == true ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            Add(menu);
        }

        public Func<bool> HasAnyPreview;

        public void SetPreviewToggleValue(bool value)
        {
            var toggle = this.Q<ToolbarToggle>();
            if (toggle != null)
            {
                toggle.SetValueWithoutNotify(value);
            }
        }

        public void SetGroupInfo(string name)
        {
            if (_nameField != null) _nameField.SetValueWithoutNotify(name);
        }
        
        public void ClearSearch()
        {
            if (_searchField != null) _searchField.SetValueWithoutNotify(string.Empty);
        }
    }
}

