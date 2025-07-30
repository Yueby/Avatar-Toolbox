using System.Collections.Generic;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor.Plugins
{
    /// <summary>
    /// PhysBone转移插件
    /// </summary>
    public class PhysBoneTransferPlugin : ComponentTransferBase
    {
        public override string Name => "PhysBone转移";
        public override string Description => "转移VRCPhysBone和VRCPhysBoneCollider组件";

        private bool _transferPhysBone = true;
        private bool _transferPhysBoneCollider = true;

        public override void DrawSettings()
        {
            _transferPhysBone = UnityEditor.EditorGUILayout.Toggle("转移PhysBone", _transferPhysBone);
            _transferPhysBoneCollider = UnityEditor.EditorGUILayout.Toggle("转移PhysBoneCollider", _transferPhysBoneCollider);
        }

        public override bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (!IsEnabled) return true;

            YuebyLogger.LogInfo("PhysBoneTransferPlugin", $"开始转移PhysBone，源根对象: {sourceRoot?.name}, 目标根对象: {targetRoot?.name}");

            // 设置根对象
            SetRootObjects(sourceRoot, targetRoot);
            bool success = true;

            if (_transferPhysBone)
            {
                YuebyLogger.LogInfo("PhysBoneTransferPlugin", "开始转移PhysBone组件");
                success &= TransferPhysBones();
            }

            if (_transferPhysBoneCollider)
            {
                YuebyLogger.LogInfo("PhysBoneTransferPlugin", "开始转移PhysBone碰撞器");
                success &= TransferPhysBoneColliders();
            }

            YuebyLogger.LogInfo("PhysBoneTransferPlugin", $"PhysBone转移完成，结果: {(success ? "成功" : "部分失败")}");
            return success;
        }

        public override bool CanTransfer(Transform sourceRoot, Transform targetRoot)
        {
            if (sourceRoot == null || targetRoot == null) return false;

            var physBones = sourceRoot.GetComponentsInChildren<VRCPhysBone>(true);
            var colliders = sourceRoot.GetComponentsInChildren<VRCPhysBoneColliderBase>(true);

            return physBones.Length > 0 || colliders.Length > 0;
        }

        private bool TransferPhysBones()
        {
            bool success = true;

            // 递归遍历所有子对象
            var allTransforms = SourceRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform sourceTransform in allTransforms)
            {
                var physBone = sourceTransform.GetComponent<VRCPhysBone>();
                if (physBone == null) continue;

                try
                {
                    var targetGo = GetOrCreateTargetObject(sourceTransform, TargetRoot);
                    if (targetGo == null)
                    {
                        YuebyLogger.LogWarning("PhysBoneTransferPlugin", $"无法为目标PhysBone创建对象: {sourceTransform.name}");
                        success = false;
                        continue;
                    }

                    var targetPhysBone = CopyComponentWithUndo<VRCPhysBone>(sourceTransform.gameObject, targetGo);
                    if (targetPhysBone == null)
                    {
                        YuebyLogger.LogError("PhysBoneTransferPlugin", $"复制PhysBone组件失败: {sourceTransform.name}");
                        success = false;
                        continue;
                    }

                    // 处理根变换
                    if (physBone.rootTransform != null)
                    {
                        var rootTransGo = GetOrCreateTargetObject(physBone.rootTransform, TargetRoot);
                        targetPhysBone.rootTransform = rootTransGo?.transform;
                        if (rootTransGo == null)
                        {
                            YuebyLogger.LogWarning("PhysBoneTransferPlugin", $"未找到PhysBone根变换: {physBone.rootTransform.name}");
                        }
                    }

                    // 处理碰撞器
                    if (_transferPhysBoneCollider)
                    {
                        targetPhysBone.colliders = GetColliders(physBone.colliders);
                    }

                    YuebyLogger.LogInfo("PhysBoneTransferPlugin", $"转移PhysBone组件: {sourceTransform.name}");
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("PhysBoneTransferPlugin", $"转移PhysBone组件时发生错误 {sourceTransform.name}: {e.Message}");
                    success = false;
                }
            }

            return success;
        }

        private bool TransferPhysBoneColliders()
        {
            bool success = true;

            // 递归遍历所有子对象
            var allTransforms = SourceRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform sourceTransform in allTransforms)
            {
                var collider = sourceTransform.GetComponent<VRCPhysBoneColliderBase>();
                if (collider == null) continue;

                try
                {
                    var targetGo = GetOrCreateTargetObject(sourceTransform, TargetRoot);
                    if (targetGo == null)
                    {
                        YuebyLogger.LogWarning("PhysBoneTransferPlugin", $"无法为目标碰撞器创建对象: {sourceTransform.name}");
                        success = false;
                        continue;
                    }

                    var targetCollider = CopyComponentWithUndo<VRCPhysBoneColliderBase>(sourceTransform.gameObject, targetGo);
                    if (targetCollider == null)
                    {
                        YuebyLogger.LogError("PhysBoneTransferPlugin", $"复制碰撞器组件失败: {sourceTransform.name}");
                        success = false;
                        continue;
                    }

                    // 处理根变换
                    if (collider.rootTransform != null)
                    {
                        var rootTransGo = GetOrCreateTargetObject(collider.rootTransform, TargetRoot);
                        targetCollider.rootTransform = rootTransGo?.transform;
                        if (rootTransGo == null)
                        {
                            YuebyLogger.LogWarning("PhysBoneTransferPlugin", $"未找到碰撞器根变换: {collider.rootTransform.name}");
                        }
                    }

                    YuebyLogger.LogInfo("PhysBoneTransferPlugin", $"转移碰撞器组件: {sourceTransform.name}");
                }
                catch (System.Exception e)
                {
                    YuebyLogger.LogError("PhysBoneTransferPlugin", $"转移碰撞器组件时发生错误 {sourceTransform.name}: {e.Message}");
                    success = false;
                }
            }

            return success;
        }

        private List<VRCPhysBoneColliderBase> GetColliders(List<VRCPhysBoneColliderBase> colliders)
        {
            List<VRCPhysBoneColliderBase> list = new List<VRCPhysBoneColliderBase>();

            if (colliders == null) return list;

            foreach (var collider in colliders)
            {
                if (collider == null) continue;

                var targetGo = GetOrCreateTargetObject(collider.transform, TargetRoot);
                if (targetGo != null)
                {
                    var colBase = CopyComponentWithUndo<VRCPhysBoneColliderBase>(collider.gameObject, targetGo);
                    if (colBase != null)
                    {
                        if (collider.rootTransform != null)
                        {
                            var rootTransGo = GetOrCreateTargetObject(collider.rootTransform, TargetRoot);
                            colBase.rootTransform = rootTransGo?.transform;
                            if (rootTransGo == null)
                            {
                                YuebyLogger.LogWarning("PhysBoneTransferPlugin", $"未找到碰撞器根变换: {collider.rootTransform.name}");
                            }
                        }
                        list.Add(colBase);
                        YuebyLogger.LogInfo("PhysBoneTransferPlugin", $"转移碰撞器引用: {collider.name}");
                    }
                    else
                    {
                        YuebyLogger.LogError("PhysBoneTransferPlugin", $"复制碰撞器组件失败: {collider.name}");
                    }
                }
                else
                {
                    YuebyLogger.LogWarning("PhysBoneTransferPlugin", $"无法为目标碰撞器引用创建对象: {collider.name}");
                }
            }

            return list;
        }
    }
}