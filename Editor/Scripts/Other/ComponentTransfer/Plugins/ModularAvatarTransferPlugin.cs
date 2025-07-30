using UnityEngine;
using Yueby.Core.Utils;
using nadena.dev.modular_avatar.core;

namespace YuebyAvatarTools.ComponentTransfer.Editor.Plugins
{
    /// <summary>
    /// ModularAvatar转移插件
    /// </summary>
    public class ModularAvatarTransferPlugin : ComponentTransferBase
    {
        public override string Name => "ModularAvatar转移";
        public override string Description => "转移ModularAvatar相关组件";

        private bool _transferBoneProxy = true;
        private bool _transferMergeArmature = true;
        private bool _transferMergeAnimator = true;

        public override void DrawSettings()
        {
            _transferBoneProxy = UnityEditor.EditorGUILayout.Toggle("转移BoneProxy", _transferBoneProxy);
            _transferMergeArmature = UnityEditor.EditorGUILayout.Toggle("转移MergeArmature", _transferMergeArmature);
            _transferMergeAnimator = UnityEditor.EditorGUILayout.Toggle("转移MergeAnimator", _transferMergeAnimator);
        }

        public override bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (!IsEnabled) return true;

            // 设置根对象
            SetRootObjects(sourceRoot, targetRoot);
            bool success = true;

            var allTransforms = SourceRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform sourceTransform in allTransforms)
            {
                try
                {
                    if (HasAnyModularAvatarComponent(sourceTransform))
                    {
                        success &= TransferModularAvatarComponents(sourceTransform);
                    }
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("ModularAvatarTransferPlugin", $"转移ModularAvatar组件时发生错误 {sourceTransform.name}: {e.Message}");
                    success = false;
                }
            }

            return success;
        }

        public override bool CanTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (sourceRoot == null || targetRoot == null) return false;

            var allTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in allTransforms)
            {
                if (HasAnyModularAvatarComponent(transform))
                    return true;
            }

            return false;
        }

        private bool HasAnyModularAvatarComponent(Transform transform)
        {
            return transform.GetComponent<ModularAvatarBoneProxy>() != null ||
                   transform.GetComponent<ModularAvatarMergeArmature>() != null ||
                   transform.GetComponent<ModularAvatarMergeAnimator>() != null;
        }

        private bool TransferModularAvatarComponents(Transform sourceTransform)
        {
            bool success = true;

            var targetGo = GetOrCreateTargetObject(sourceTransform, TargetRoot);
            if (targetGo == null) return false;

            // 转移 ModularAvatarBoneProxy
            if (_transferBoneProxy && sourceTransform.GetComponent<ModularAvatarBoneProxy>() != null)
            {
                success &= TransferComponent<ModularAvatarBoneProxy>(sourceTransform.gameObject, targetGo);
            }

            // 转移 ModularAvatarMergeArmature
            if (_transferMergeArmature && sourceTransform.GetComponent<ModularAvatarMergeArmature>() != null)
            {
                success &= TransferComponent<ModularAvatarMergeArmature>(sourceTransform.gameObject, targetGo);
            }

            // 转移 ModularAvatarMergeAnimator
            if (_transferMergeAnimator && sourceTransform.GetComponent<ModularAvatarMergeAnimator>() != null)
            {
                success &= TransferComponent<ModularAvatarMergeAnimator>(sourceTransform.gameObject, targetGo);
            }

            return success;
        }

        private bool TransferComponent<T>(GameObject source, GameObject target) where T : Component
        {
            var components = source.GetComponents<T>();
            bool success = true;

            foreach (var component in components)
            {
                if (component == null) continue;

                var result = CopyComponentWithUndo<T>(source, target);
                if (result != null)
                {
                    YuebyLogger.LogInfo("ModularAvatarTransferPlugin", $"转移{typeof(T).Name}组件: {source.name}");
                }
                else
                {
                    YuebyLogger.LogError("ModularAvatarTransferPlugin", $"转移{typeof(T).Name}组件失败: {source.name}");
                    success = false;
                }
            }

            return success;
        }
    }
} 