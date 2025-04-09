using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Yueby;
using Yueby.Core.Utils;
using Yueby.Utils;

namespace YuebyAvatarTools.PhysBoneTransfer.Editor
{
    public class PhysboneTransfer : EditorWindow
    {
        private class TransferStats
        {
            public int TotalComponents = 0;
            public int SuccessfulTransfers = 0;
            public int FailedTransfers = 0;
            public List<string> Errors = new List<string>();
            public List<string> SuccessfulComponents = new List<string>();

            public void AddError(string componentType, string objectPath, string error)
            {
                Errors.Add($"[{componentType}] {objectPath}: {error}");
            }

            public void AddSuccess(string componentType, string sourcePath, string targetPath, bool targetFound)
            {
                SuccessfulComponents.Add($"[{componentType}] {sourcePath}{(targetFound ? " ✓" : "")}");
            }

            public string GetSummary()
            {
                var summary = $"Transfer completed: {SuccessfulTransfers} successful, {FailedTransfers} failed, {TotalComponents} total components\n\n";

                if (SuccessfulComponents.Count > 0)
                {
                    summary += "Successfully transferred components:\n" + string.Join("\n", SuccessfulComponents);
                }

                return summary;
            }
        }

        private Vector2 _pos;
        private Transform _origin;
        private static bool _isOnlyTransferInArmature = false;
        private static bool _isTransferMAMergeArmature = true;
        private static bool _isTransferMAMergeAnimator = true;
        private static bool _isTransferMABoneProxy = true;
        private static bool _isTransferMaterials = true;
        private static bool _isTransferParticleSystems = true;
        private static bool _isTransferConstraints = true;
        private static bool _isTransferMaterialSwitcher = true;
        private static bool _isSearchSameNameObjects = true;

        // 添加缓存字典
        private Dictionary<Transform, string> _pathCache = new Dictionary<Transform, string>();
        private Dictionary<Transform, Transform> _targetCache = new Dictionary<Transform, Transform>();

        private void ClearCaches()
        {
            _pathCache.Clear();
            _targetCache.Clear();
        }

        private string GetCachedPath(Transform transform, Transform root)
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

        private Transform FindTargetInCache(Transform source)
        {
            if (source == null)
                return null;

            return _targetCache.TryGetValue(source, out var cachedTarget) ? cachedTarget : null;
        }

        private Transform FindExistingTarget(Transform source, Transform targetRoot)
        {
            if (source == null || targetRoot == null)
                return null;

            var relativePath = GetRelativePath(_origin, source);
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

        private Transform CreateTargetObject(Transform source, Transform targetRoot, string relativePath)
        {
            if (!_isOnlyTransferInArmature)
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
                        Undo.RegisterCreatedObjectUndo(newGo, $"Create Object: {part}");
                        YuebyLogger.LogInfo("PhysBoneTransfer", $"Created object: {part} at {VRC.Core.ExtensionMethods.GetHierarchyPath(newGo.transform)}");
                    }
                    current = child;
                }
                return current;
            }
            return null;
        }

        /// <summary>
        /// 在目标骨骼中查找与源对象同名的对象，不考虑层级结构
        /// </summary>
        /// <param name="sourceName">源对象名称</param>
        /// <param name="targetRoot">目标根对象</param>
        /// <returns>找到的同名对象，如有多个则返回第一个</returns>
        private Transform FindObjectByNameRecursive(string sourceName, Transform targetRoot)
        {
            // 递归搜索所有子对象
            Transform[] allChildren = targetRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child != targetRoot && child.name == sourceName)
                    return child;
            }
            
            return null;
        }

        private GameObject GetOrCreateTargetObject(Transform source, Transform targetRoot, bool createIfNotExist = true)
        {
            if (source == null || targetRoot == null)
                return null;

            // 1. 检查缓存
            var cachedTarget = FindTargetInCache(source);
            if (cachedTarget != null)
                return cachedTarget.gameObject;

            // 2. 获取相对路径
            var relativePath = GetRelativePath(_origin, source);
            if (string.IsNullOrEmpty(relativePath))
                return targetRoot.gameObject;

            // 3. 查找现有对象
            var existingTarget = FindExistingTarget(source, targetRoot);
            if (existingTarget != null)
            {
                _targetCache[source] = existingTarget;
                return existingTarget.gameObject;
            }

            // 4. 新增: 如果启用了同名对象查找，尝试查找同名对象（不考虑层级）
            if (_isSearchSameNameObjects && source.name != "root")
            {
                var sameNameTarget = FindObjectByNameRecursive(source.name, targetRoot);
                if (sameNameTarget != null)
                {
                    _targetCache[source] = sameNameTarget;
                    return sameNameTarget.gameObject;
                }
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

        private void OnEnable()
        {
            ClearCaches();
        }

        private void OnDisable()
        {
            ClearCaches();
        }

        [MenuItem("Tools/YuebyTools/VRChat/Avatar/PhysBone Transfer", false, 21)]
        public static void OpenWindow()
        {
            var window = CreateWindow<PhysboneTransfer>();
            window.titleContent = new GUIContent("Avatar Component Transfer");
            window.minSize = new Vector2(200, 250);
            window.Show();
        }

        private void OnGUI()
        {
            var selections = Selection.gameObjects;
            var isSelectedObject = selections.Length > 0;

            EditorUI.DrawEditorTitle("Avatar Component Transfer");
            EditorUI.VerticalEGLTitled("Configuration", () =>
            {
                EditorUI.HorizontalEGL(() =>
                {
                    _origin = (Transform)EditorUI.ObjectFieldVertical(_origin, "Source Armature", typeof(Transform));
                    EditorUI.Line(LineType.Vertical);
                }, GUILayout.MaxHeight(40));
            });

            EditorUI.VerticalEGLTitled("Settings", () =>
            {
                _isOnlyTransferInArmature = EditorUI.Toggle(_isOnlyTransferInArmature, "Only Transfer In Armature");
                _isSearchSameNameObjects = EditorUI.Toggle(_isSearchSameNameObjects, "Search For Same Name Objects");
                EditorUI.Line(LineType.Horizontal);
                _isTransferMAMergeArmature = EditorUI.Toggle(_isTransferMAMergeArmature, "Transfer ModularAvatarMergeArmature");
                _isTransferMAMergeAnimator = EditorUI.Toggle(_isTransferMAMergeAnimator, "Transfer ModularAvatarMergeAnimator");
                _isTransferMABoneProxy = EditorUI.Toggle(_isTransferMABoneProxy, "Transfer ModularAvatarBoneProxy");
                _isTransferMaterials = EditorUI.Toggle(_isTransferMaterials, "Transfer Materials");
                _isTransferParticleSystems = EditorUI.Toggle(_isTransferParticleSystems, "Transfer Particle Systems");
                _isTransferConstraints = EditorUI.Toggle(_isTransferConstraints, "Transfer Constraints");
                _isTransferMaterialSwitcher = EditorUI.Toggle(_isTransferMaterialSwitcher, "Transfer MaterialSwitcher");

                if (GUILayout.Button("Transfer"))
                {
                    if (_origin == null)
                    {
                        EditorUtility.DisplayDialog("Error", "Please select source armature first", "OK");
                        return;
                    }

                    var stats = new TransferStats();
                    var isCancelled = false;

                    try
                    {
                        foreach (var selection in selections)
                        {
                            if (!ProcessTransfer(selection, stats, ref isCancelled))
                                break;
                        }

                        if (isCancelled)
                        {
                            EditorUtility.DisplayDialog("Cancelled", "Operation cancelled by user", "OK");
                            YuebyLogger.LogWarning("PhysBoneTransfer", stats.GetSummary());
                        }
                        else
                        {
                            var message = stats.GetSummary();
                            if (stats.Errors.Count > 0)
                            {
                                message += "\n\nError details:\n" + string.Join("\n", stats.Errors);
                            }
                            YuebyLogger.LogInfo("PhysBoneTransfer", "Transfer completed!");
                            YuebyLogger.LogInfo("PhysBoneTransfer", message);
                        }
                    }
                    catch (Exception e)
                    {
                        YuebyLogger.LogError("PhysBoneTransfer", $"Error during transfer: {e}");
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            });

            EditorUI.VerticalEGLTitled("Selected Objects", () =>
            {
                if (isSelectedObject)
                    _pos = EditorUI.ScrollViewEGL(() =>
                    {
                        foreach (var selection in selections)
                            EditorGUILayout.ObjectField(selection, typeof(GameObject), true);
                    }, _pos, GUILayout.Height(200));
                else
                    EditorGUILayout.HelpBox("Please select target objects in the scene", MessageType.Error);
            });
        }

        private void TransferMaterialSwitcher(GameObject targetRoot, TransferStats stats)
        {
            var sourceComponents = _origin.GetComponents<MaterialSwitcher>();
            if (sourceComponents == null || sourceComponents.Length == 0) return;

            // 获取目标对象上已有的MaterialSwitcher组件
            var existingComponents = targetRoot.GetComponents<MaterialSwitcher>();

            // 确保目标对象有足够的组件
            for (int i = existingComponents.Length; i < sourceComponents.Length; i++)
            {
                var newComponent = targetRoot.AddComponent<MaterialSwitcher>();
                Undo.RegisterCreatedObjectUndo(newComponent, "Create MaterialSwitcher");
            }

            // 重新获取目标组件（包括新创建的）
            var targetComponents = targetRoot.GetComponents<MaterialSwitcher>();

            // 复制每个组件的值
            for (int i = 0; i < sourceComponents.Length; i++)
            {
                stats.TotalComponents++;
                try
                {
                    ComponentUtility.CopyComponent(sourceComponents[i]);
                    ComponentUtility.PasteComponentValues(targetComponents[i]);

                    Undo.RegisterCompleteObjectUndo(targetComponents[i], "Transfer MaterialSwitcher");
                    EditorUtility.SetDirty(targetComponents[i]);

                    stats.SuccessfulTransfers++;
                    stats.AddSuccess("MaterialSwitcher", _origin.name, targetRoot.name, true);
                }
                catch (Exception e)
                {
                    stats.FailedTransfers++;
                    stats.AddError("MaterialSwitcher", targetRoot.name, e.Message);
                }
            }
        }

        private void TransferMaterials(Transform current, TransferStats stats)
        {
            var originRenderers = _origin.GetComponentsInChildren<Renderer>(true);

            if (originRenderers.Length <= 0) return;
            foreach (var originRenderer in originRenderers)
            {
                stats.TotalComponents++;
                try
                {
                    var sourcePath = VRC.Core.ExtensionMethods.GetHierarchyPath(originRenderer.transform);
                    var targetGo = GetOrCreateTargetObject(originRenderer.transform, current);
                    var found = targetGo != null;

                    if (!found)
                    {
                        stats.FailedTransfers++;
                        stats.AddSuccess("Materials", sourcePath, GetTargetPath(originRenderer.transform, current), false);
                        continue;
                    }

                    var targetRenderer = targetGo.GetComponent<Renderer>();
                    if (targetRenderer == null)
                    {
                        YuebyLogger.LogWarning("PhysBoneTransfer", $"Skipping material transfer for {targetGo.name}: No Renderer component found");
                        stats.FailedTransfers++;
                        continue;
                    }

                    var materials = originRenderer.sharedMaterials;
                    targetRenderer.sharedMaterials = materials;
                    Undo.RegisterFullObjectHierarchyUndo(targetGo, "TransferMaterial");

                    stats.SuccessfulTransfers++;
                    stats.AddSuccess("Materials", sourcePath, GetTargetPath(originRenderer.transform, current), true);
                }
                catch (Exception e)
                {
                    stats.FailedTransfers++;
                    stats.AddError("Materials", originRenderer.name, e.Message);
                    YuebyLogger.LogError("PhysBoneTransfer", $"Error transferring materials for {originRenderer.name}: {e}");
                }
            }
        }

        private void Recursive(Transform sourceParent, Transform targetRoot, TransferStats stats)
        {
            if (sourceParent == null || targetRoot == null)
            {
                stats.AddError("Recursive", "Unknown", "Source or target transform is null");
                return;
            }

            var allTransforms = sourceParent.GetComponentsInChildren<Transform>(true);
            var totalObjects = allTransforms.Length;
            var processedObjects = 0;
            var currentDepth = GetHierarchyDepth(sourceParent);

            foreach (Transform sourceChild in sourceParent)
            {
                var progress = processedObjects / (float)totalObjects;
                var currentPath = VRC.Core.ExtensionMethods.GetHierarchyPath(sourceChild);

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Transferring Components",
                    $"Processing: {currentPath}\n" +
                    $"Progress: {processedObjects}/{totalObjects} ({(progress * 100):F1}%)\n" +
                    $"Depth: {currentDepth}",
                    progress))
                {
                    EditorUtility.ClearProgressBar();
                    stats.AddError("Recursive", currentPath, "User cancelled the operation");
                    return;
                }

                try
                {
                    // PhysBone 转移
                    if (sourceChild.GetComponent<VRCPhysBone>() != null)
                    {
                        TransferPhysBone(sourceChild, targetRoot, stats);
                    }

                    // Constraint 转移
                    if (_isTransferConstraints && HasAnyConstraint(sourceChild))
                    {
                        TransferConstraint(sourceChild, targetRoot, stats);
                    }

                    // ParticleSystem 转移
                    if (_isTransferParticleSystems && HasParticleSystem(sourceChild))
                    {
                        TransferParticleSystems(sourceChild, targetRoot, stats);
                    }

                    // 其他组件转移
                    if (HasAnyTargetComponent(sourceChild))
                    {
                        TransferComponents(sourceChild, targetRoot, stats);
                    }
                }
                catch (Exception e)
                {
                    stats.AddError("Recursive", currentPath, $"Exception during transfer: {e.Message}");
                    YuebyLogger.LogError("PhysBoneTransfer", $"Processing object {currentPath} encountered an error: {e}");
                }

                processedObjects++;
                Recursive(sourceChild, targetRoot, stats);
            }
        }

        private bool HasAnyConstraint(Transform transform)
        {
            return transform.GetComponent<ParentConstraint>() != null ||
                   transform.GetComponent<RotationConstraint>() != null ||
                   transform.GetComponent<PositionConstraint>() != null;
        }

        private bool HasParticleSystem(Transform transform)
        {
            return transform.GetComponent<ParticleSystem>() != null;
        }

        private bool HasAnyTargetComponent(Transform transform)
        {
            return transform.GetComponent<ModularAvatarBoneProxy>() != null ||
                   transform.GetComponent<ModularAvatarMergeArmature>() != null ||
                   transform.GetComponent<ModularAvatarMergeAnimator>() != null;
        }

        private int GetHierarchyDepth(Transform transform)
        {
            int depth = 0;
            var current = transform;
            while (current != null && current != _origin)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }

        private void CopyComponentByType<T>(GameObject targetGo, Transform child) where T : Component
        {
            var components = child.GetComponents<T>();
            foreach (var component in components)
            {
                if (component == null)
                    continue;

                ComponentUtility.CopyComponent(component);
                var targetComponent = targetGo.GetComponent<T>();
                if (targetComponent != null)
                {
                    ComponentUtility.PasteComponentValues(targetComponent);
                }
                else
                {
                    ComponentUtility.PasteComponentAsNew(targetGo);
                }
            }
        }

        private void TransferComponents(Transform child, Transform current, TransferStats stats)
        {
            var targetGo = GetOrCreateTargetObject(child, current);
            if (targetGo == null) return;

            if (_isTransferMABoneProxy)
                CopyComponentWithStats<ModularAvatarBoneProxy>(child.gameObject, targetGo, stats);
            if (_isTransferMAMergeArmature)
                CopyComponentWithStats<ModularAvatarMergeArmature>(child.gameObject, targetGo, stats);
            if (_isTransferMAMergeAnimator)
                CopyComponentWithStats<ModularAvatarMergeAnimator>(child.gameObject, targetGo, stats);
        }

        private void CopyComponentWithStats<T>(GameObject source, GameObject target, TransferStats stats) where T : Component
        {
            var components = source.GetComponents<T>();
            foreach (var component in components)
            {
                if (component == null) continue;

                stats.TotalComponents++;
                var result = CopyComponentWithUndo<T>(source, target);
                if (result != null)
                {
                    stats.SuccessfulTransfers++;
                    stats.AddSuccess(typeof(T).Name, VRC.Core.ExtensionMethods.GetHierarchyPath(source.transform),
                        VRC.Core.ExtensionMethods.GetHierarchyPath(target.transform), target != null);
                }
                else
                {
                    stats.FailedTransfers++;
                }
            }
        }

        private void TransferConstraint(Transform child, Transform current, TransferStats stats)
        {
            var targetGo = GetOrCreateTargetObject(child, current);
            if (targetGo == null) return;

            CopyConstraintComponents<ParentConstraint>(child.gameObject, targetGo, current, stats);
            CopyConstraintComponents<RotationConstraint>(child.gameObject, targetGo, current, stats);
            CopyConstraintComponents<PositionConstraint>(child.gameObject, targetGo, current, stats);
        }

        private void CopyConstraintComponents<T>(GameObject sourceObj, GameObject target, Transform current, TransferStats stats) where T : Component, IConstraint
        {
            var constraints = sourceObj.GetComponents<T>();
            foreach (var constraint in constraints)
            {
                stats.TotalComponents++;

                var lastLocked = constraint.locked;
                var lastActive = constraint.constraintActive;

                constraint.locked = false;
                constraint.constraintActive = false;

                var targetConstraint = CopyComponentWithUndo<T>(sourceObj, target);
                if (targetConstraint == null)
                {
                    stats.FailedTransfers++;
                    continue;
                }

                // 恢复源约束的状态
                constraint.locked = lastLocked;
                constraint.constraintActive = lastActive;

                // 处理约束源
                bool allSourcesFound = true;
                for (var i = 0; i < constraint.sourceCount; i++)
                {
                    var source = constraint.GetSource(i);
                    var targetSourceObject = GetOrCreateTargetObject(source.sourceTransform, current);
                    if (targetSourceObject == null)
                    {
                        allSourcesFound = false;
                        continue;
                    }

                    targetConstraint.SetSource(i, new ConstraintSource()
                    {
                        sourceTransform = targetSourceObject.transform,
                        weight = source.weight
                    });
                }

                targetConstraint.constraintActive = constraint.constraintActive;
                stats.SuccessfulTransfers++;
                stats.AddSuccess(typeof(T).Name, VRC.Core.ExtensionMethods.GetHierarchyPath(sourceObj.transform),
                    VRC.Core.ExtensionMethods.GetHierarchyPath(target.transform), allSourcesFound);
            }
        }

        private void TransferPhysBone(Transform child, Transform current, TransferStats stats)
        {
            var physBone = child.GetComponent<VRCPhysBone>();
            if (physBone == null) return;

            try
            {
                stats.TotalComponents++;
                var sourcePath = VRC.Core.ExtensionMethods.GetHierarchyPath(child.transform);
                var targetPath = GetTargetPath(child, current);

                var targetGo = GetOrCreateTargetObject(child, current);
                if (targetGo == null)
                {
                    YuebyLogger.LogWarning("PhysBoneTransfer", $"Cannot create target object for PhysBone: {child.name}");
                    stats.FailedTransfers++;
                    stats.AddSuccess("VRCPhysBone", sourcePath, targetPath, false);
                    return;
                }

                var targetPhysBone = CopyComponentWithUndo<VRCPhysBone>(child.gameObject, targetGo);
                if (targetPhysBone == null)
                {
                    YuebyLogger.LogError("PhysBoneTransfer", $"Cannot copy PhysBone component: {child.name}");
                    stats.FailedTransfers++;
                    return;
                }

                bool rootFound = true;
                if (physBone.rootTransform != null)
                {
                    var rootTransGo = GetOrCreateTargetObject(physBone.rootTransform, current);
                    targetPhysBone.rootTransform = rootTransGo?.transform;
                    if (rootTransGo == null)
                    {
                        YuebyLogger.LogWarning("PhysBoneTransfer", $"Cannot find PhysBone rootTransform: {physBone.rootTransform.name}");
                        rootFound = false;
                    }
                }

                targetPhysBone.colliders = GetColliders(targetPhysBone.colliders, current, stats);

                // 只有在所有操作都成功完成后才记录成功
                bool success = targetGo != null && targetPhysBone != null;
                if (success)
                {
                    stats.SuccessfulTransfers++;
                    stats.AddSuccess("VRCPhysBone", sourcePath, targetPath, rootFound);
                }
                else
                {
                    stats.FailedTransfers++;
                }
            }
            catch (Exception e)
            {
                stats.FailedTransfers++;
                YuebyLogger.LogError("PhysBoneTransfer", $"Transferring PhysBone component encountered an error ({child.name}): {e}");
            }
        }

        private List<VRCPhysBoneColliderBase> GetColliders(List<VRCPhysBoneColliderBase> colliders, Transform current, TransferStats stats)
        {
            List<VRCPhysBoneColliderBase> list = new List<VRCPhysBoneColliderBase>();
            foreach (var originCollider in colliders)
            {
                if (originCollider == null) continue;

                stats.TotalComponents++;
                var sourcePath = VRC.Core.ExtensionMethods.GetHierarchyPath(originCollider.transform);
                var targetPath = GetTargetPath(originCollider.transform, current);

                var targetGo = GetOrCreateTargetObject(originCollider.transform, current);
                if (targetGo != null)
                {
                    var colBase = CopyComponentWithUndo<VRCPhysBoneColliderBase>(originCollider.gameObject, targetGo);
                    if (colBase == null)
                    {
                        stats.FailedTransfers++;
                        continue;
                    }

                    bool rootFound = true;
                    if (originCollider.rootTransform != null)
                    {
                        var rootTransGo = GetOrCreateTargetObject(originCollider.rootTransform, current);
                        colBase.rootTransform = rootTransGo?.transform;
                        if (rootTransGo == null)
                            rootFound = false;
                    }

                    list.Add(colBase);
                    stats.SuccessfulTransfers++;
                    stats.AddSuccess("VRCPhysBoneColliderBase", sourcePath, targetPath, rootFound);
                }
                else
                {
                    stats.FailedTransfers++;
                    stats.AddSuccess("VRCPhysBoneColliderBase", sourcePath, targetPath, false);
                }
            }
            return list;
        }

        private void TransferParticleSystems(Transform child, Transform current, TransferStats stats)
        {
            if (child == null || current == null)
            {
                stats.AddError("ParticleSystem", "Unknown", "Source or target transform is null");
                return;
            }

            var sourcePath = VRC.Core.ExtensionMethods.GetHierarchyPath(child.transform);
            var targetPath = GetTargetPath(child, current);

            var targetGo = GetOrCreateTargetObject(child, current);
            if (targetGo == null)
            {
                stats.AddError("ParticleSystem", sourcePath, "Failed to create target object");
                return;
            }

            try
            {
                // 主粒子系统
                if (child.TryGetComponent<ParticleSystem>(out var sourcePS))
                {
                    stats.TotalComponents++;
                    var particleSystem = CopyComponentWithUndo<ParticleSystem>(child.gameObject, targetGo);
                    if (particleSystem != null)
                    {
                        // 转移 ParticleSystemRenderer 组件
                        if (child.TryGetComponent<ParticleSystemRenderer>(out var renderer))
                        {
                            stats.TotalComponents++;
                            var rendererComponent = CopyComponentWithUndo<ParticleSystemRenderer>(child.gameObject, targetGo);
                            if (rendererComponent != null)
                            {
                                stats.SuccessfulTransfers++;
                                stats.AddSuccess("ParticleSystemRenderer", sourcePath, targetPath, true);
                            }
                            else
                            {
                                stats.FailedTransfers++;
                                stats.AddError("ParticleSystemRenderer", sourcePath, "Failed to copy renderer component");
                            }
                        }

                        stats.SuccessfulTransfers++;
                        stats.AddSuccess("ParticleSystem", sourcePath, targetPath, true);
                    }
                    else
                    {
                        stats.FailedTransfers++;
                        stats.AddError("ParticleSystem", sourcePath, "Failed to copy particle system component");
                    }
                }

                // 处理子粒子系统
                foreach (var childPS in child.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (childPS.transform == child.transform) continue;

                    var childSourcePath = VRC.Core.ExtensionMethods.GetHierarchyPath(childPS.transform);
                    var childTargetPath = GetTargetPath(childPS.transform, current);

                    stats.TotalComponents++;
                    var childTargetGo = GetOrCreateTargetObject(childPS.transform, current);
                    if (childTargetGo == null)
                    {
                        stats.FailedTransfers++;
                        stats.AddError("ParticleSystem", childSourcePath, "Failed to create target object for child particle system");
                        continue;
                    }

                    var childParticleSystem = CopyComponentWithUndo<ParticleSystem>(childPS.gameObject, childTargetGo);
                    if (childParticleSystem != null)
                    {
                        if (childPS.TryGetComponent<ParticleSystemRenderer>(out var childRenderer))
                        {
                            stats.TotalComponents++;
                            var childRendererComponent = CopyComponentWithUndo<ParticleSystemRenderer>(childPS.gameObject, childTargetGo);
                            if (childRendererComponent != null)
                            {
                                stats.SuccessfulTransfers++;
                                stats.AddSuccess("ParticleSystemRenderer", childSourcePath, childTargetPath, true);
                            }
                            else
                            {
                                stats.FailedTransfers++;
                                stats.AddError("ParticleSystemRenderer", childSourcePath, "Failed to copy renderer component");
                            }
                        }

                        stats.SuccessfulTransfers++;
                        stats.AddSuccess("ParticleSystem", childSourcePath, childTargetPath, true);
                    }
                    else
                    {
                        stats.FailedTransfers++;
                        stats.AddError("ParticleSystem", childSourcePath, "Failed to copy child particle system component");
                    }
                }
            }
            catch (Exception e)
            {
                stats.FailedTransfers++;
                stats.AddError("ParticleSystem", sourcePath, $"Exception during transfer: {e.Message}");
                YuebyLogger.LogError("PhysBoneTransfer", $"Transferring particle system encountered an error ({sourcePath}): {e}");
            }
        }

        /// <summary>
        /// 根据源对象获取目标对象的路径
        /// </summary>
        private string GetTargetPath(Transform source, Transform targetRoot)
        {
            var sourcePath = GetCachedPath(source, _origin);
            if (string.IsNullOrEmpty(sourcePath))
                return targetRoot.name;

            return $"{targetRoot.name}/{sourcePath}";
        }

        /// <summary>
        /// 获取相对路径
        /// </summary>
        private string GetRelativePath(Transform root, Transform target)
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

            // 如果没有找到根节点，返回空
            if (current != root)
                return "";

            return path.ToString();
        }

        /// <summary>
        /// 统一的组件复制方法
        /// </summary>
        private T CopyComponentWithUndo<T>(GameObject source, GameObject target, string undoName = null) where T : Component
        {
            if (source == null || target == null)
            {
                YuebyLogger.LogError("PhysBoneTransfer", $"Source or target is null when copying {typeof(T).Name}");
                return null;
            }

            var sourceComponent = source.GetComponent<T>();
            if (sourceComponent == null)
            {
                // YuebyLogger.LogWarning("PhysBoneTransfer", $"Source component {typeof(T).Name} not found on {source.name}");
                return null;
            }

            try
            {
                // 保存当前状态用于撤销
                Undo.RegisterCompleteObjectUndo(target, undoName ?? $"Transfer {typeof(T).Name}");

                // 复制组件
                ComponentUtility.CopyComponent(sourceComponent);
                var targetComponent = target.GetComponent<T>();

                if (targetComponent != null)
                {
                    // 如果目标已有组件，保存它用于撤销
                    Undo.RegisterCompleteObjectUndo(targetComponent, $"Modify {typeof(T).Name}");
                    ComponentUtility.PasteComponentValues(targetComponent);
                }
                else
                {
                    ComponentUtility.PasteComponentAsNew(target);
                    targetComponent = target.GetComponent<T>();
                    if (targetComponent != null)
                    {
                        // 新组件已经由Undo.RegisterCreatedObjectUndo在内部处理
                        Undo.RegisterCreatedObjectUndo(targetComponent, $"Create {typeof(T).Name}");
                    }
                }

                if (targetComponent != null)
                {
                    // 确保修改被保存
                    EditorUtility.SetDirty(target);
                    EditorUtility.SetDirty(targetComponent);
                }
                else
                {
                    YuebyLogger.LogError("PhysBoneTransfer",
                        $"Failed to create or modify component {typeof(T).Name} on {target.name}");
                }

                return targetComponent;
            }
            catch (System.Exception e)
            {
                YuebyLogger.LogError("PhysBoneTransfer",
                    $"Exception while copying {typeof(T).Name} from {source.name} to {target.name}: {e.Message}");
                return null;
            }
        }

        private bool ProcessTransfer(GameObject selection, TransferStats stats, ref bool isCancelled)
        {
            if (isCancelled) return false;

            try
            {
                ClearCaches(); // 每次传输前清除缓存
                Recursive(_origin, selection.transform, stats);

                if (_isTransferMaterials)
                    TransferMaterials(selection.transform, stats);
                if (_isTransferMaterialSwitcher)
                    TransferMaterialSwitcher(selection, stats);

                return true;
            }
            catch (Exception e)
            {
                stats.AddError(typeof(GameObject).Name, selection.name, e.Message);
                return false;
            }
            finally
            {
                ClearCaches(); // 传输完成后清除缓存
            }
        }
    }
}