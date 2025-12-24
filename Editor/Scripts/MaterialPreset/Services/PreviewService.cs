using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yueby.Tools.AvatarToolbox.MaterialPreset;
using Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Utils;
using Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Services
{
    public class PreviewService
    {
        public class PreviewInstance
        {
            public GameObject Instance;
            public PresetGroup Group;
        }

        private MaterialPresetManager _manager;
        private PresetGridView _gridView;
        private PresetObjectFactory _objectFactory;

        public PreviewService(MaterialPresetManager manager, PresetGridView gridView, PresetObjectFactory objectFactory)
        {
            _manager = manager;
            _gridView = gridView;
            _objectFactory = objectFactory;
        }

        public void ValidatePreviews()
        {
            if (_manager == null) return;
            var currentList = _manager.ActivePreviews;
            for (int i = currentList.Count - 1; i >= 0; i--)
            {
                if (currentList[i] is PreviewInstance pi && pi.Instance == null)
                    currentList.RemoveAt(i);
            }
            var allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in allRoots)
            {
                if (root.name.StartsWith($"{_manager.name}_Preview_"))
                {
                    bool known = false;
                    foreach (var p in currentList)
                        if (p is PreviewInstance pi && pi.Instance == root) { known = true; break; }

                    if (!known)
                        currentList.Add(new PreviewInstance { Instance = root, Group = null });
                }
            }
        }

        public bool IsPreviewActive(PresetGroup group)
        {
            if (_manager == null || group == null) return false;
            
            foreach (var obj in _manager.ActivePreviews)
            {
                if (obj is PreviewInstance pi && pi.Group == group && pi.Instance != null)
                {
                    return true;
                }
            }
            return false;
        }

        public void CreatePreview(PresetGroup group)
        {
            if (group == null || _manager == null) return;

            var clone = _objectFactory.CreateObjectInstance(group, true);
            if (clone != null)
            {
                _manager.ActivePreviews.Add(new PreviewInstance { Instance = clone, Group = group });
                RepositionPreviews();
                Selection.activeGameObject = clone;
                _gridView?.Refresh(_manager);
            }
        }

        public void DestroyPreview(PresetGroup group)
        {
            if (group == null || _manager == null) return;
            var list = _manager.ActivePreviews;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is PreviewInstance pi && pi.Group == group && pi.Instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(pi.Instance);
                    list.RemoveAt(i);
                }
            }
            RepositionPreviews();
            _gridView?.Refresh(_manager);
        }

        public void LocatePreview(PresetGroup group)
        {
            GameObject previewObj = null;
            foreach (var p in _manager.ActivePreviews)
                if (p is PreviewInstance pi && pi.Group == group && pi.Instance != null) { previewObj = pi.Instance; break; }
            if (previewObj != null) EditorGUIUtility.PingObject(previewObj);
        }

        public void RepositionPreviews()
        {
            if (_manager == null || _manager.ActivePreviews.Count == 0) return;

            SortPreviewsByDisplayOrder();

            float stride = 1.5f;

            for (int i = 0; i < _manager.ActivePreviews.Count; i++)
            {
                var pi = _manager.ActivePreviews[i] as PreviewInstance;
                if (pi != null && pi.Instance != null)
                {
                    Vector3 offset = Vector3.right * stride * (i + 1);
                    pi.Instance.transform.position = _manager.transform.position + offset;
                }
            }
        }

        private void SortPreviewsByDisplayOrder()
        {
            if (_manager == null) return;

            var allGroups = DisplayOrderHelper.GetAllGroupsOrderedByDisplay(_manager);
            var groupIndexMap = new Dictionary<PresetGroup, int>();

            for (int i = 0; i < allGroups.Count; i++)
            {
                groupIndexMap[allGroups[i]] = i;
            }

            _manager.ActivePreviews.Sort((a, b) =>
            {
                var piA = a as PreviewInstance;
                var piB = b as PreviewInstance;

                if (piA == null || piB == null) return 0;
                if (piA.Group == null && piB.Group == null) return 0;
                if (piA.Group == null) return 1;
                if (piB.Group == null) return -1;

                int indexA = groupIndexMap.ContainsKey(piA.Group) ? groupIndexMap[piA.Group] : int.MaxValue;
                int indexB = groupIndexMap.ContainsKey(piB.Group) ? groupIndexMap[piB.Group] : int.MaxValue;

                return indexA.CompareTo(indexB);
            });
        }

        public void ClearAllPreviews()
        {
            if (_manager == null) return;
            var list = _manager.ActivePreviews;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is PreviewInstance pi && pi.Instance != null)
                    UnityEngine.Object.DestroyImmediate(pi.Instance);
            }
            list.Clear();
            _gridView?.Refresh(_manager);
        }

        public void KeepAllPreviews()
        {
            if (_manager == null) return;
            var list = _manager.ActivePreviews;
            bool changed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is PreviewInstance pi && pi.Instance != null)
                {
                    Undo.RegisterCompleteObjectUndo(pi.Instance, "保持所有预览");
                    if (pi.Group != null) pi.Instance.name = pi.Group.GroupName;
                    else pi.Instance.name = pi.Instance.name.Replace("_Preview_", "_");

                    pi.Instance.transform.position = Vector3.zero;
                    pi.Instance.transform.rotation = Quaternion.identity;
                    pi.Instance.transform.localScale = Vector3.one;

                    changed = true;
                }
            }
            list.Clear();
            if (changed) _gridView?.Refresh(_manager);
        }

        public void TogglePreviews(bool isOn, PresetFolder context)
        {
            if (_manager == null) return;

            if (isOn)
            {
                var groups = context == null ? _manager.GetAllGroups() : context.GetAllGroupsRecursive();

                if (groups.Count > 10)
                {
                    if (!EditorUtility.DisplayDialog("警告", $"您即将创建 {groups.Count} 个预览对象。是否继续?", "是", "取消"))
                        return;
                }

                foreach (var group in groups)
                {
                    bool has = false;
                    foreach (var p in _manager.ActivePreviews)
                        if (p is PreviewInstance pi && pi.Group == group && pi.Instance != null) { has = true; break; }

                    if (!has)
                    {
                        var clone = _objectFactory.CreateObjectInstance(group, true);
                        if (clone != null)
                        {
                            _manager.ActivePreviews.Add(new PreviewInstance { Instance = clone, Group = group });
                        }
                    }
                }
                RepositionPreviews();
                _gridView?.Refresh(_manager);
            }
            else
            {
                if (context == null)
                {
                    ClearAllPreviews();
                }
                else
                {
                    var groups = context.GetAllGroupsRecursive();
                    var list = _manager.ActivePreviews;
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        if (list[i] is PreviewInstance pi && pi.Group != null && groups.Contains(pi.Group) && pi.Instance != null)
                        {
                            UnityEngine.Object.DestroyImmediate(pi.Instance);
                            list.RemoveAt(i);
                        }
                    }
                    RepositionPreviews();
                    _gridView?.Refresh(_manager);
                }
            }
        }

        public void ToggleFolderPreviews(PresetFolder folder)
        {
            if (folder == null) return;

            var allGroups = folder.GetAllGroupsRecursive();

            bool anyActive = false;
            foreach (var group in allGroups)
            {
                foreach (var p in _manager.ActivePreviews)
                {
                    if (p is PreviewInstance pi && pi.Group == group && pi.Instance != null)
                    {
                        anyActive = true;
                        break;
                    }
                }
                if (anyActive) break;
            }

            if (anyActive)
            {
                foreach (var group in allGroups)
                {
                    DestroyPreview(group);
                }
            }
            else
            {
                foreach (var group in allGroups)
                {
                    bool alreadyExists = false;
                    foreach (var p in _manager.ActivePreviews)
                    {
                        if (p is PreviewInstance pi && pi.Group == group && pi.Instance != null)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists) CreatePreview(group);
                }
                RepositionPreviews();
            }

            _gridView?.Refresh(_manager);
        }
    }
}

