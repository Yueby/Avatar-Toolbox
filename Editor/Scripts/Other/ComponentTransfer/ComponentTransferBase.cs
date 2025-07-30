using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 组件转移基础类
    /// </summary>
    public abstract class ComponentTransferBase : IComponentTransferPlugin
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public bool IsEnabled { get; set; } = true;

        // 源根对象和目标根对象
        protected Transform SourceRoot { get; private set; }
        protected Transform TargetRoot { get; private set; }

        // 缓存字典
        protected Dictionary<Transform, string> _pathCache = new Dictionary<Transform, string>();
        protected Dictionary<Transform, Transform> _targetCache = new Dictionary<Transform, Transform>();

        protected virtual void ClearCaches()
        {
            _pathCache.Clear();
            _targetCache.Clear();
        }

        protected string GetCachedPath(Transform transform, Transform root)
        {
            if (transform == null || root == null)
                return "";

            var cacheKey = transform;
            if (_pathCache.TryGetValue(cacheKey, out var cachedPath))
                return cachedPath;

            var path = GetRelativePath(root, transform);
            _pathCache[cacheKey] = path;
            return path;
        }

        protected Transform FindTargetInCache(Transform source)
        {
            if (source == null)
                return null;

            return _targetCache.TryGetValue(source, out var cachedTarget) ? cachedTarget : null;
        }

        protected Transform FindExistingTarget(Transform source, Transform targetRoot)
        {
            if (source == null || targetRoot == null)
                return null;

            var relativePath = GetRelativePath(SourceRoot, source);
            if (string.IsNullOrEmpty(relativePath))
                return targetRoot;

            var pathParts = relativePath.Split('/');
            var current = targetRoot;

            foreach (var part in pathParts)
            {
                var child = current.Find(part);
                if (child == null)
                    return null;
                current = child;
            }

            return current;
        }

        protected Transform FindObjectByNameRecursive(string sourceName, Transform targetRoot)
        {
            Transform[] allChildren = targetRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child != targetRoot && child.name == sourceName)
                    return child;
            }

            return null;
        }

        protected GameObject GetOrCreateTargetObject(Transform source, Transform targetRoot, bool createIfNotExist = true)
        {
            if (source == null || targetRoot == null)
                return null;

            // 1. 检查缓存
            var cachedTarget = FindTargetInCache(source);
            if (cachedTarget != null)
                return cachedTarget.gameObject;

            // 2. 获取相对路径
            var relativePath = GetRelativePath(SourceRoot, source);
            if (string.IsNullOrEmpty(relativePath))
                return targetRoot.gameObject;

            // 3. 查找现有对象
            var existingTarget = FindExistingTarget(source, targetRoot);
            if (existingTarget != null)
            {
                _targetCache[source] = existingTarget;
                return existingTarget.gameObject;
            }

            // 4. 查找同名对象（不考虑层级）
            var sameNameTarget = FindObjectByNameRecursive(source.name, targetRoot);
            if (sameNameTarget != null)
            {
                _targetCache[source] = sameNameTarget;
                return sameNameTarget.gameObject;
            }

            // 5. 如果允许创建，创建新对象
            if (createIfNotExist)
            {
                var newTarget = CreateTargetObject(source, targetRoot, relativePath);
                if (newTarget != null)
                {
                    _targetCache[source] = newTarget;
                    return newTarget.gameObject;
                }
            }

            return null;
        }

        protected Transform CreateTargetObject(Transform source, Transform targetRoot, string relativePath)
        {
            var current = targetRoot;
            var pathParts = relativePath.Split('/');

            foreach (var part in pathParts)
            {
                var child = current.Find(part);
                if (child == null)
                {
                    var newGo = new GameObject(part);
                    newGo.transform.SetParent(current, false);

                    // 复制Transform信息
                    newGo.transform.localPosition = source.localPosition;
                    newGo.transform.localRotation = source.localRotation;
                    newGo.transform.localScale = source.localScale;

                    child = newGo.transform;
                    UnityEditor.Undo.RegisterCreatedObjectUndo(newGo, $"Create Object: {part}");
                    YuebyLogger.LogInfo("ComponentTransfer", $"Created object: {part} at {VRC.Core.ExtensionMethods.GetHierarchyPath(newGo.transform)}");
                }
                current = child;
            }
            return current;
        }

        protected string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
                return "";

            var path = new System.Text.StringBuilder();
            var current = target;

            while (current != null && current != root)
            {
                if (path.Length > 0)
                    path.Insert(0, "/");
                path.Insert(0, current.name);
                current = current.parent;
            }

            if (current != root)
                return "";

            return path.ToString();
        }

        protected T CopyComponentWithUndo<T>(GameObject source, GameObject target, string undoName = null) where T : Component
        {
            if (source == null || target == null)
            {
                YuebyLogger.LogError("ComponentTransfer", $"Source or target is null when copying {typeof(T).Name}");
                return null;
            }

            var sourceComponent = source.GetComponent<T>();
            if (sourceComponent == null)
                return null;

            try
            {
                UnityEditor.Undo.RegisterCompleteObjectUndo(target, undoName ?? $"Transfer {typeof(T).Name}");

                ComponentUtility.CopyComponent(sourceComponent);
                var targetComponent = target.GetComponent<T>();

                if (targetComponent != null)
                {
                    UnityEditor.Undo.RegisterCompleteObjectUndo(targetComponent, $"Modify {typeof(T).Name}");
                    ComponentUtility.PasteComponentValues(targetComponent);
                }
                else
                {
                    ComponentUtility.PasteComponentAsNew(target);
                    targetComponent = target.GetComponent<T>();
                    if (targetComponent != null)
                    {
                        UnityEditor.Undo.RegisterCreatedObjectUndo(targetComponent, $"Create {typeof(T).Name}");
                    }
                }

                if (targetComponent != null)
                {
                    UnityEditor.EditorUtility.SetDirty(target);
                    UnityEditor.EditorUtility.SetDirty(targetComponent);
                }

                return targetComponent;
            }
            catch (System.Exception e)
            {
                YuebyLogger.LogError("ComponentTransfer", $"Exception while copying {typeof(T).Name} from {source.name} to {target.name}: {e.Message}");
                return null;
            }
        }

        public abstract void DrawSettings();
        
        public abstract bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot);
        
        public abstract bool CanTransfer(Transform sourceRoot, Transform targetRoot);

        // 设置根对象的方法
        protected void SetRootObjects(Transform sourceRoot, Transform targetRoot)
        {
            SourceRoot = sourceRoot;
            TargetRoot = targetRoot;
            ClearCaches();
        }
    }
}