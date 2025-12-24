using System;
using UnityEditor; // Added this
using UnityEditor.UIElements;
using UnityEngine; // Added this for ScaleMode
using UnityEngine.UIElements;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components
{
    public class MainToolbar : Toolbar
    {
        public new class UxmlFactory : UxmlFactory<MainToolbar> { }

        public event Action<string> OnSearch;
        public event Action<bool> OnPreviewToggle; // New Event
        public event Action OnSnapshot;
        public event Action OnNewGroup;
        public event Action OnNewFolder; // New Event for folders
        public event Action OnClearPreviews;
        public event Action OnKeepPreviews;

        public MainToolbar()
        {
            // Search Field
            var searchField = new ToolbarSearchField();
            searchField.style.width = 200;
            searchField.RegisterValueChangedCallback(evt => OnSearch?.Invoke(evt.newValue));
            Add(searchField);

            // Spacer
            Add(new VisualElement { style = { flexGrow = 1 } });

            // Global Preview Toggle
            var previewToggle = new ToolbarToggle();
            previewToggle.tooltip = "切换所有预览";
            var eyeIcon = EditorGUIUtility.IconContent("d_ViewToolOrbit").image as Texture2D;
            if (eyeIcon == null) eyeIcon = EditorGUIUtility.IconContent("ViewToolOrbit").image as Texture2D;
            
            if (eyeIcon != null)
            {
                // We need to style the checkmark or the toggle itself?
                // ToolbarToggle behaves like a button that stays pressed.
                // Setting backgroundImage on the toggle element itself usually works for ToolbarToggle.
                previewToggle.style.backgroundImage = eyeIcon;
                previewToggle.style.width = 24;
                previewToggle.style.minWidth = 24;
                previewToggle.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                previewToggle.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                previewToggle.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                previewToggle.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                // Hide default checkmark if we use icon as background
                var checkmark = previewToggle.Q<VisualElement>(className: "unity-toggle__checkmark");
                if (checkmark != null) checkmark.style.display = DisplayStyle.None;
            }
            else
            {
                previewToggle.text = "👁";
            }
            previewToggle.RegisterValueChangedCallback(evt => OnPreviewToggle?.Invoke(evt.newValue));
            Add(previewToggle);

            // Buttons
            var btnSnapshot = new ToolbarButton(() => OnSnapshot?.Invoke()) { tooltip = "快照当前状态" };
            // Load camera icon
            var camIcon = EditorGUIUtility.IconContent("d_FrameCapture").image as Texture2D; 
            // Fallback if d_FrameCapture not found (e.g. older Unity)
            if (camIcon == null) camIcon = EditorGUIUtility.IconContent("Camera Icon").image as Texture2D;
            
            if (camIcon != null) 
            {
                btnSnapshot.style.backgroundImage = camIcon;
                btnSnapshot.style.width = 24; // Ensure icon fits
                btnSnapshot.style.minWidth = 24;
                // Center the icon and remove tint if needed (ToolbarButton usually handles tints)
                btnSnapshot.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                btnSnapshot.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                btnSnapshot.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                btnSnapshot.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            else
            {
                btnSnapshot.text = "快照"; // Fallback text
            }
            Add(btnSnapshot);

            // Create Menu (Plus Icon)
            var createMenu = new ToolbarMenu { tooltip = "创建..." };
            var plusIcon = EditorGUIUtility.IconContent("Toolbar Plus").image as Texture2D;
            if (plusIcon == null) plusIcon = EditorGUIUtility.IconContent("d_Toolbar Plus").image as Texture2D;

            if (plusIcon != null)
            {
                createMenu.style.backgroundImage = plusIcon;
                createMenu.style.width = 32; // Slightly wider for the arrow
                createMenu.style.minWidth = 32;
                createMenu.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, new Length(2, LengthUnit.Pixel));
                createMenu.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                createMenu.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                createMenu.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                createMenu.style.paddingLeft = 18; // Make space for icon
            }
            else
            {
                createMenu.text = "+"; 
            }
            
            createMenu.menu.AppendAction("创建预设", a => OnNewGroup?.Invoke());
            createMenu.menu.AppendAction("创建文件夹", a => OnNewFolder?.Invoke());
            
            Add(createMenu);

            // Menu (Options)
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
            
            // Note: Menu actions are appended dynamically when clicked/displayed usually,
            // but for ToolbarMenu we pre-populate it.
            // However, we want to invoke the external events.
            
            // We use a lambda to ensure we capture the current delegate when the item is clicked,
            // not when it's created (though delegates are reference types so it should be fine).
            menu.menu.AppendAction("清除所有预览", a => OnClearPreviews?.Invoke(), a => HasAnyPreview?.Invoke() == true ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("保持所有预览", a => OnKeepPreviews?.Invoke(), a => HasAnyPreview?.Invoke() == true ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            
            Add(menu);
        }

        public Func<bool> HasAnyPreview;

        public void SetPreviewToggleWithoutNotify(bool value)
        {
            var toggle = this.Q<ToolbarToggle>();
            if (toggle != null)
            {
                toggle.SetValueWithoutNotify(value);
            }
        }
    }
}