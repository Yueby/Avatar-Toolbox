using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Yueby.Tools.AvatarToolbox.MaterialPreset;
using Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components;
using Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Services;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor
{
    public class MaterialPresetEditorWindow : EditorWindow
    {
        private const string PACKAGE_PATH = "Packages/yueby.tools.avatar-toolbox/Editor/Scripts/MaterialPreset";
        private const string MAIN_UXML = PACKAGE_PATH + "/MaterialPresetEditorWindow.uxml";
        private const string CARD_UXML = PACKAGE_PATH + "/Components/PresetCard.uxml";
        private const string FOLDER_UXML = PACKAGE_PATH + "/Components/FolderCard.uxml";
        private const string DETAIL_UXML = PACKAGE_PATH + "/Components/PresetDetailView.uxml";
        private const string MAIN_USS = PACKAGE_PATH + "/MaterialPresetEditorWindow.uss";
        private const string CARD_USS = PACKAGE_PATH + "/Components/PresetCard.uss";
        private const string FOLDER_USS = PACKAGE_PATH + "/Components/FolderCard.uss";
        private const string DETAIL_USS = PACKAGE_PATH + "/Components/PresetDetailView.uss";

        [MenuItem("Tools/YuebyTools/Material Preset")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<MaterialPresetEditorWindow>("材质预设");
            wnd.minSize = new Vector2(600, 400);
        }

        private MaterialPresetManager _targetManager;
        
        private VisualElement _gridViewContainer;
        private VisualElement _detailViewContainer;
        
        private PresetGridView _presetGridView;
        private PresetDetailView _presetDetailView;
        private BreadcrumbNavigation _globalBreadcrumb;
        
        private MainToolbar _gridToolbar;
        private DetailViewToolbar _detailToolbar;
        
        private VisualTreeAsset _cardTemplate;
        private VisualTreeAsset _folderTemplate;
        private VisualTreeAsset _detailTemplate;

        private PreviewService _previewService;
        private ThumbnailService _thumbnailService;
        private DataManagementService _dataService;
        private NavigationService _navigationService;
        private PresetObjectFactory _objectFactory;

        public void CreateGUI()
        {
            LoadAssets();
            SetupUI();
            FindTargetManager();
            InitializeServices();
            BindEvents();
            
            _presetGridView.Refresh(_targetManager);
            _globalBreadcrumb.SetPath(_presetGridView.FolderPath);
        }

        private void LoadAssets()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MAIN_UXML);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(MAIN_USS);
            _cardTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_UXML);
            _folderTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(FOLDER_UXML);
            _detailTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DETAIL_UXML);
            
            visualTree.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(styleSheet);
            
            var cardStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(CARD_USS);
            var folderStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(FOLDER_USS);
            var detailStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(DETAIL_USS);
            if (cardStyle) rootVisualElement.styleSheets.Add(cardStyle);
            if (folderStyle) rootVisualElement.styleSheets.Add(folderStyle);
            if (detailStyle) rootVisualElement.styleSheets.Add(detailStyle);
        }

        private void SetupUI()
        {
            _gridViewContainer = rootVisualElement.Q("view-grid");
            _detailViewContainer = rootVisualElement.Q("view-detail");
            var toolbarContainer = rootVisualElement.Q("toolbar-container");
            var breadcrumbContainer = rootVisualElement.Q("breadcrumb-container");
            
            _globalBreadcrumb = new BreadcrumbNavigation();
            if (breadcrumbContainer != null)
            {
                breadcrumbContainer.Add(_globalBreadcrumb);
            }
            
            _gridToolbar = new MainToolbar();
            _gridToolbar.HasAnyPreview = () => _targetManager != null && _targetManager.ActivePreviews.Count > 0;
            _detailToolbar = new DetailViewToolbar();
            _detailToolbar.HasAnyPreview = () => _targetManager != null && _targetManager.ActivePreviews.Count > 0;
            _detailToolbar.style.display = DisplayStyle.None;
            
            if (toolbarContainer != null)
            {
                toolbarContainer.Add(_gridToolbar);
                toolbarContainer.Add(_detailToolbar);
            }

            var oldScroll = _gridViewContainer.Q("grid-scroll");
            if (oldScroll != null) oldScroll.RemoveFromHierarchy();

            _presetGridView = new PresetGridView();
            _presetGridView.Initialize(_cardTemplate, _folderTemplate, _gridToolbar, _globalBreadcrumb);
            _gridViewContainer.Add(_presetGridView);

            _presetDetailView = new PresetDetailView();
            _presetDetailView.Initialize(_detailTemplate, _detailToolbar);
            _detailViewContainer.Add(_presetDetailView);
        }

        private void InitializeServices()
        {
            _objectFactory = new PresetObjectFactory(_targetManager);
            _previewService = new PreviewService(_targetManager, _presetGridView, _objectFactory);
            _thumbnailService = new ThumbnailService(_targetManager, _presetGridView, _objectFactory);
            _dataService = new DataManagementService(_targetManager, _presetGridView, _previewService);
            _navigationService = new NavigationService(
                _targetManager,
                _gridViewContainer,
                _detailViewContainer,
                _gridToolbar,
                _detailToolbar,
                _presetGridView,
                _presetDetailView,
                _globalBreadcrumb,
                _previewService
            );
        }

        private void BindEvents()
        {
            _globalBreadcrumb.OnNavigate += _navigationService.NavigateToPath;
            _globalBreadcrumb.OnDropToFolder += _dataService.MoveItemToFolder;
            
            _presetGridView.OnOpenDetail += _navigationService.SwitchToDetailView;
            _presetGridView.OnCreatePreview += _previewService.CreatePreview;
            _presetGridView.OnCreateObject += _objectFactory.CreateObject;
            _presetGridView.OnCreateFolderObjects += _objectFactory.CreateFolderObjects;
            _presetGridView.OnDestroyPreview += _previewService.DestroyPreview;
            _presetGridView.OnApply += _objectFactory.ApplyGroup;
            _presetGridView.OnDelete += _dataService.DeleteGroup;
            _presetGridView.OnRename += _dataService.RenameGroup;
            _presetGridView.OnCaptureThumbnail += _thumbnailService.CaptureThumbnailWithDialog;
            _presetGridView.OnLocatePreview += _previewService.LocatePreview;
            _presetGridView.OnReorder += _dataService.ReorderItems;
            
            _presetGridView.OnFolderOpen += folder => _presetGridView.NavigateToFolder(folder);
            _presetGridView.OnFolderDelete += _dataService.DeleteFolder;
            _presetGridView.OnFolderRename += _dataService.RenameFolder;
            _presetGridView.OnFolderPreviewToggle += _previewService.ToggleFolderPreviews;
            _presetGridView.OnDropToFolder += _dataService.MoveItemToFolder;
            
            _presetGridView.OnClearPreviews += _previewService.ClearAllPreviews;
            _presetGridView.OnKeepPreviews += _previewService.KeepAllPreviews;
            _presetGridView.OnPreviewToggle += _previewService.TogglePreviews;
            
            _presetGridView.OnNavigationChanged += (folder, path) => {
                if (_gridViewContainer.style.display != DisplayStyle.None)
                {
                    _globalBreadcrumb.SetPath(path);
                }
            };

            _presetDetailView.OnBreadcrumbUpdate += _navigationService.UpdateBreadcrumbForDetailView;
            
            _detailToolbar.OnPreviewToggle += (isOn) => {
                if (isOn) _previewService.CreatePreview(_navigationService.CurrentEditingGroup);
                else _previewService.DestroyPreview(_navigationService.CurrentEditingGroup);
            };
            _detailToolbar.OnApply += () => _objectFactory.ApplyGroup(_navigationService.CurrentEditingGroup);
            _detailToolbar.OnCaptureThumbnail += () => _thumbnailService.CaptureThumbnailWithDialog(_navigationService.CurrentEditingGroup);
            _detailToolbar.OnClearPreviews += _previewService.ClearAllPreviews;
            _detailToolbar.OnKeepPreviews += _previewService.KeepAllPreviews;
            
            rootVisualElement.RegisterCallback<DragUpdatedEvent>(evt => {
                if (evt.target is VisualElement ve && !_presetGridView.Contains(ve) && _presetGridView != ve)
                {
                    _presetGridView.OnDragUpdated(evt);
                }
            });
            
            rootVisualElement.RegisterCallback<DragPerformEvent>(evt => {
                 if (evt.target is VisualElement ve && !_presetGridView.Contains(ve) && _presetGridView != ve)
                {
                    _presetGridView.OnDragPerform(evt);
                }
            });
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            if (_targetManager == null) FindTargetManager();
            if (_targetManager != null && _previewService != null) 
            {
                _previewService.ValidatePreviews();
                _previewService.RepositionPreviews();
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            if (_targetManager != null && _presetGridView != null)
            {
                _presetGridView.Refresh(_targetManager);
                
                if (_navigationService != null && _navigationService.CurrentEditingGroup != null && _detailViewContainer.style.display == DisplayStyle.Flex)
                {
                    if (!_targetManager.GetAllGroups().Contains(_navigationService.CurrentEditingGroup))
                    {
                        _navigationService.SwitchToGridView();
                    }
                    else
                    {
                        _presetDetailView.Bind(_navigationService.CurrentEditingGroup, _targetManager);
                        _navigationService.UpdateBreadcrumbForDetailView(_navigationService.CurrentEditingGroup);
                    }
                }
                else if (_navigationService != null)
                {
                    _navigationService.UpdateBreadcrumbForGridView();
                }
            }
        }

        private void FindTargetManager()
        {
            if (Selection.activeGameObject != null)
                _targetManager = Selection.activeGameObject.GetComponent<MaterialPresetManager>();
            
            if (_targetManager == null)
                _targetManager = FindObjectOfType<MaterialPresetManager>();
        }
    }
}
