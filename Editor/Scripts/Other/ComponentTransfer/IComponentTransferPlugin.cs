using UnityEngine;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 组件转移插件接口
    /// </summary>
    public interface IComponentTransferPlugin
    {
        /// <summary>
        /// 插件名称
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 插件描述
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// 是否启用
        /// </summary>
        bool IsEnabled { get; set; }
        
        /// <summary>
        /// 绘制插件设置界面
        /// </summary>
        void DrawSettings();
        
        /// <summary>
        /// 执行转移操作
        /// </summary>
        /// <param name="sourceRoot">源根对象</param>
        /// <param name="targetRoot">目标根对象</param>
        /// <returns>转移是否成功</returns>
        bool ExecuteTransfer(Transform sourceRoot, Transform targetRoot);
        
        /// <summary>
        /// 验证是否可以执行转移
        /// </summary>
        /// <param name="sourceRoot">源根对象</param>
        /// <param name="targetRoot">目标根对象</param>
        /// <returns>是否可以转移</returns>
        bool CanTransfer(Transform sourceRoot, Transform targetRoot);
    }
} 