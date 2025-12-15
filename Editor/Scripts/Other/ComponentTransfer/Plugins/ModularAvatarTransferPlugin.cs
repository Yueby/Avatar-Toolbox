using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Yueby.Core.Utils;
using nadena.dev.modular_avatar.core;

namespace YuebyAvatarTools.ComponentTransfer.Editor.Plugins
{
    /// <summary>
    /// ModularAvatar转移插件 - 自动支持所有MA组件
    /// </summary>
    public class ModularAvatarTransferPlugin : ComponentTransferBase
    {
        public override string Name => "ModularAvatar转移";
        public override string Description => "自动检测并转移所有ModularAvatar组件";

        private bool _showComponentList = false;

        // 缓存所有 MA 组件类型
        private static Type[] _allMAComponentTypes;
        private static Type[] AllMAComponentTypes
        {
            get
            {
                if (_allMAComponentTypes == null)
                {
                    _allMAComponentTypes = typeof(AvatarTagComponent).Assembly
                        .GetTypes()
                        .Where(t => t.IsSubclassOf(typeof(AvatarTagComponent))
                                 && !t.IsAbstract
                                 && t.IsPublic)
                        .ToArray();
                }
                return _allMAComponentTypes;
            }
        }

        public override void DrawSettings()
        {
            UnityEditor.EditorGUILayout.HelpBox(
                $"此插件将自动检测并转移所有 ModularAvatar 组件\n" +
                $"当前支持 {AllMAComponentTypes.Length} 种组件类型",
                UnityEditor.MessageType.Info);

            // 可选：显示将要转移的组件类型列表
            _showComponentList = UnityEditor.EditorGUILayout.Foldout(_showComponentList, "支持的组件列表");
            if (_showComponentList)
            {
                UnityEditor.EditorGUI.indentLevel++;
                foreach (var type in AllMAComponentTypes.OrderBy(t => t.Name))
                {
                    UnityEditor.EditorGUILayout.LabelField($"• {type.Name}", UnityEditor.EditorStyles.miniLabel);
                }
                UnityEditor.EditorGUI.indentLevel--;
            }
        }

        public override bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (!IsEnabled) return true;

            SetRootObjects(sourceRoot, targetRoot);
            bool success = true;
            int transferredCount = 0;

            var allTransforms = SourceRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform sourceTransform in allTransforms)
            {
                // 检查是否应该停止
                if (ShouldStopTransfer())
                {
                    YuebyLogger.LogWarning("ModularAvatarTransferPlugin", "转移已被用户停止");
                    return false;
                }

                try
                {
                    // 获取该对象上的所有组件
                    var allComponents = sourceTransform.GetComponents<Component>();

                    foreach (var component in allComponents)
                    {
                        if (component == null) continue;

                        var componentType = component.GetType();

                        // 检查是否为 MA 组件
                        if (IsMAComponent(componentType))
                        {
                            if (TransferMAComponent(sourceTransform, componentType))
                            {
                                transferredCount++;
                                YuebyLogger.LogInfo("ModularAvatarTransferPlugin",
                                    $"转移 {componentType.Name} 组件: {sourceTransform.name}");
                            }
                            else
                            {
                                success = false;
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("ModularAvatarTransferPlugin",
                        $"转移ModularAvatar组件时发生错误 {sourceTransform.name}: {e.Message}");
                    success = false;
                }
            }

            YuebyLogger.LogInfo("ModularAvatarTransferPlugin",
                $"转移完成，共处理 {transferredCount} 个组件");

            return success;
        }

        public override bool CanTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (sourceRoot == null || targetRoot == null) return false;

            var allTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform transform in allTransforms)
            {
                var components = transform.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component != null && IsMAComponent(component.GetType()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 检查类型是否为 MA 组件
        /// </summary>
        private bool IsMAComponent(Type type)
        {
            return AllMAComponentTypes.Contains(type);
        }

        /// <summary>
        /// 转移单个 MA 组件
        /// </summary>
        private bool TransferMAComponent(Transform sourceTransform, Type componentType)
        {
            var targetGo = GetOrCreateTargetObject(sourceTransform, TargetRoot);
            if (targetGo == null)
            {
                YuebyLogger.LogWarning("ModularAvatarTransferPlugin",
                    $"无法为 {componentType.Name} 创建目标对象: {sourceTransform.name}");
                return false;
            }

            // 使用反射调用泛型方法
            var method = typeof(ComponentTransferBase)
                .GetMethod("CopyComponentWithUndo",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .MakeGenericMethod(componentType);

            var result = method.Invoke(this, new object[] {
                sourceTransform.gameObject,
                targetGo,
                $"Transfer {componentType.Name}"
            });

            if (result == null)
            {
                YuebyLogger.LogError("ModularAvatarTransferPlugin",
                    $"复制 {componentType.Name} 组件失败: {sourceTransform.name}");
                return false;
            }

            // 执行后处理（混合模式）
            PostProcessComponent(result as Component, sourceTransform);

            return true;
        }

        /// <summary>
        /// 组件后处理（混合模式：仅对特殊组件处理）
        /// </summary>
        private void PostProcessComponent(Component targetComponent, Transform sourceTransform)
        {
            if (targetComponent == null) return;

            var componentType = targetComponent.GetType();

            // 未来可以在这里添加特殊组件的处理逻辑
            // 例如：
            // if (componentType.Name == "ModularAvatarMenuItem")
            // {
            //     // 处理菜单项的特殊引用
            // }

            // 目前使用通用复制即可满足大部分需求
        }
    }
}
