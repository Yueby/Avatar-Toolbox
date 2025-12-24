using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor
{
    public class RendererSelectionWindow : EditorWindow
    {
        private List<SkinnedMeshRenderer> _availableRenderers;
        private HashSet<SkinnedMeshRenderer> _selectedRenderers = new HashSet<SkinnedMeshRenderer>();
        private Action<List<SkinnedMeshRenderer>> _onConfirm;
        private ScrollView _listContainer;

        public static void ShowWindow(GameObject root, Action<List<SkinnedMeshRenderer>> onConfirm)
        {
            // Close existing to ensure fresh state or GetWindow and Re-init
            if (HasOpenInstances<RendererSelectionWindow>())
            {
                GetWindow<RendererSelectionWindow>().Close();
            }

            var wnd = GetWindow<RendererSelectionWindow>(true, "添加渲染器", true);
            wnd.Init(root, onConfirm);
            wnd.minSize = new Vector2(300, 400);
            wnd.CenterOnMainWin();
            wnd.ShowUtility();
        }

        public void Init(GameObject root, Action<List<SkinnedMeshRenderer>> onConfirm)
        {
            _availableRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToList();
            _selectedRenderers.Clear();
            foreach(var r in _availableRenderers) _selectedRenderers.Add(r);
            _onConfirm = onConfirm;
            
            // If GUI is already created, refresh it
            if (_listContainer != null)
            {
                PopulateList();
            }
        }
        
        private void CenterOnMainWin()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            Rect pos = position;
            float centerWidth = (main.width - pos.width) * 0.5f;
            float centerHeight = (main.height - pos.height) * 0.5f;
            pos.x = main.x + centerWidth;
            pos.y = main.y + centerHeight;
            position = pos;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.backgroundColor = new StyleColor(EditorGUILayout.GetControlRect().y == 0 ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f)); // Theme aware background roughly

            // Toolbar
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.marginBottom = 10;
            
            var btnAll = new Button(() => SetAll(true)) { text = "全选" };
            btnAll.style.flexGrow = 1;
            var btnNone = new Button(() => SetAll(false)) { text = "全不选" };
            btnNone.style.flexGrow = 1;
            
            toolbar.Add(btnAll);
            toolbar.Add(btnNone);
            root.Add(toolbar);

            // List
            _listContainer = new ScrollView();
            _listContainer.style.flexGrow = 1;
            _listContainer.style.borderTopWidth = 1;
            _listContainer.style.borderBottomWidth = 1;
            _listContainer.style.borderLeftWidth = 1;
            _listContainer.style.borderRightWidth = 1;
            
            var borderColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            _listContainer.style.borderTopColor = borderColor;
            _listContainer.style.borderBottomColor = borderColor;
            _listContainer.style.borderLeftColor = borderColor;
            _listContainer.style.borderRightColor = borderColor;
            _listContainer.style.backgroundColor = new Color(0, 0, 0, 0.1f);
            root.Add(_listContainer);

            PopulateList();

            // Footer
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.marginTop = 10;
            footer.style.justifyContent = Justify.FlexEnd;

            var btnCancel = new Button(Close) { text = "取消" };
            btnCancel.style.width = 80;
            
            var btnAdd = new Button(OnAddClicked) { text = "添加选中项" };
            btnAdd.style.width = 100;
            btnAdd.AddToClassList("unity-button-primary"); // Try to use built-in style or simple bold

            footer.Add(btnCancel);
            footer.Add(btnAdd);
            root.Add(footer);
        }

        private void PopulateList()
        {
            _listContainer.Clear();
            if (_availableRenderers == null) return;

            for (int i = 0; i < _availableRenderers.Count; i++)
            {
                var r = _availableRenderers[i];
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.paddingLeft = 5;
                row.style.paddingRight = 5;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;
                row.style.alignItems = Align.Center;
                
                // Zebra striping
                if (i % 2 == 0)
                {
                    row.style.backgroundColor = new Color(0, 0, 0, 0.1f);
                }

                var toggle = new Toggle();
                toggle.value = _selectedRenderers.Contains(r);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) _selectedRenderers.Add(r);
                    else _selectedRenderers.Remove(r);
                });
                
                var label = new Label(r.name);
                label.style.marginLeft = 5;
                label.style.flexGrow = 1;

                row.Add(toggle);
                row.Add(label);
                
                // Click row to toggle
                row.RegisterCallback<ClickEvent>(evt => 
                {
                    if (evt.target != toggle) // Avoid double toggle if clicking directly on checkbox
                    {
                        toggle.value = !toggle.value;
                    }
                });

                _listContainer.Add(row);
            }
        }

        private void SetAll(bool selected)
        {
            _selectedRenderers.Clear();
            if (selected && _availableRenderers != null)
            {
                foreach (var r in _availableRenderers) _selectedRenderers.Add(r);
            }
            PopulateList();
        }

        private void OnAddClicked()
        {
            _onConfirm?.Invoke(_selectedRenderers.ToList());
            Close();
        }
    }
}

