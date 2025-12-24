using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Yueby.Tools.AvatarToolbox.MaterialPreset;
using Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Services
{
    public class DataManagementService
    {
        private MaterialPresetManager _manager;
        private PresetGridView _gridView;
        private PreviewService _previewService;

        public DataManagementService(MaterialPresetManager manager, PresetGridView gridView, PreviewService previewService)
        {
            _manager = manager;
            _gridView = gridView;
            _previewService = previewService;
        }

        public void DeleteGroup(PresetGroup group)
        {
            if (EditorUtility.DisplayDialog("删除预设组", $"确定删除 '{group.GroupName}'?", "是", "否"))
            {
                Undo.RecordObject(_manager, "删除预设组");

                _previewService?.DestroyPreview(group);

                RemoveItemFromParent(group);
                EditorUtility.SetDirty(_manager);
                _gridView?.Refresh(_manager);
            }
        }

        public void RenameGroup(PresetGroup group, string newName)
        {
            Undo.RecordObject(_manager, "重命名预设组");
            group.GroupName = newName;
            EditorUtility.SetDirty(_manager);
        }

        public void ReorderItems(IPresetItem draggedItem, int newDisplayOrder)
        {
            if (_manager == null || draggedItem == null) return;

            Undo.RecordObject(_manager, "重新排序项目");

            draggedItem.DisplayOrder = newDisplayOrder;

            List<IPresetItem> siblings = null;
            if (_manager.RootItemsList.Contains(draggedItem))
                siblings = _manager.RootItemsList;
            else
            {
                var parent = FindParentFolder(draggedItem, _manager.RootFolders);
                if (parent != null) siblings = parent.Children;
            }

            if (siblings != null)
            {
                bool needsReorganize = false;
                var sortedItems = siblings.OrderBy(item => item.DisplayOrder).ToList();
                for (int i = 1; i < sortedItems.Count; i++)
                {
                    if (sortedItems[i].DisplayOrder <= sortedItems[i - 1].DisplayOrder)
                    {
                        needsReorganize = true;
                        break;
                    }
                }

                if (needsReorganize)
                {
                    for (int i = 0; i < sortedItems.Count; i++)
                    {
                        sortedItems[i].DisplayOrder = i;
                    }
                }
            }

            EditorUtility.SetDirty(_manager);
            _gridView?.Refresh(_manager);

            _previewService?.RepositionPreviews();
        }

        public PresetFolder FindParentFolder(IPresetItem item, List<PresetFolder> folders)
        {
            foreach (var folder in folders)
            {
                if (folder.Children.Contains(item)) return folder;
                var found = FindParentFolder(item, folder.Folders);
                if (found != null) return found;
            }
            return null;
        }

        public void DeleteFolder(PresetFolder folder)
        {
            if (EditorUtility.DisplayDialog("删除文件夹", $"确定删除 '{folder.Name}' 及其所有内容?", "是", "否"))
            {
                Undo.RecordObject(_manager, "删除文件夹");
                RemoveItemFromParent(folder);
                EditorUtility.SetDirty(_manager);
                _gridView?.Refresh(_manager);
            }
        }

        public void RenameFolder(PresetFolder folder, string newName)
        {
            Undo.RecordObject(_manager, "重命名文件夹");
            folder.Name = newName;
            EditorUtility.SetDirty(_manager);
        }

        public void MoveItemToFolder(IPresetItem item, PresetFolder targetFolder)
        {
            if (item == null) return;

            Undo.RecordObject(_manager, targetFolder == null ? "移动到根目录" : "移动到文件夹");

            RemoveItemFromParent(item);

            if (targetFolder == null)
            {
                _manager.RootItemsList.Add(item);
            }
            else
            {
                targetFolder.Children.Add(item);
            }

            EditorUtility.SetDirty(_manager);
            _gridView?.Refresh(_manager);

            _previewService?.RepositionPreviews();
        }

        private void RemoveItemFromParent(IPresetItem item)
        {
            if (_manager.RootItemsList.Remove(item))
                return;

            RemoveItemFromFolders(item, _manager.RootFolders);
        }

        private bool RemoveItemFromFolders(IPresetItem item, List<PresetFolder> folders)
        {
            foreach (var folder in folders)
            {
                if (folder.Children.Remove(item))
                    return true;

                if (RemoveItemFromFolders(item, folder.Folders))
                    return true;
            }
            return false;
        }
    }
}

