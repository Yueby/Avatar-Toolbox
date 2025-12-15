using System.Collections.Generic;
using UnityEngine;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 骨骼映射策略接口
    /// 所有骨骼匹配策略必须实现此接口
    /// </summary>
    public interface IBoneMappingStrategy
    {
        /// <summary>
        /// 策略名称（用于日志和调试）
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 策略优先级（数字越小优先级越高）
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// 查找目标骨骼
        /// </summary>
        /// <param name="source">源骨骼</param>
        /// <param name="sourceRoot">源根对象</param>
        /// <param name="targetRoot">目标根对象</param>
        /// <param name="excludeTransforms">需要排除的对象集合（如用户创建的临时对象）</param>
        /// <returns>匹配结果，如果没有找到则返回 null</returns>
        BoneMappingResult FindTarget(Transform source, Transform sourceRoot, Transform targetRoot, HashSet<Transform> excludeTransforms = null);
    }
}

