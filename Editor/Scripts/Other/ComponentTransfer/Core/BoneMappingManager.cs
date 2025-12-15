using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 混合骨骼映射管理器
    /// 管理多个骨骼匹配策略，按优先级顺序执行
    /// </summary>
    public class BoneMappingManager
    {
        private List<IBoneMappingStrategy> _strategies;
        private Dictionary<Transform, Transform> _manualMappings;
        
        public BoneMappingManager()
        {
            _strategies = new List<IBoneMappingStrategy>
            {
                new ExactPathStrategy(),
                new ExactNameStrategy(),
                new FuzzyNameStrategy()
            };
            
            _strategies = _strategies.OrderBy(s => s.Priority).ToList();
            _manualMappings = new Dictionary<Transform, Transform>();
        }
        
        /// <summary>
        /// 查找最佳匹配的目标骨骼
        /// </summary>
        public BoneMappingResult FindBestMatch(Transform source, Transform sourceRoot, Transform targetRoot, HashSet<Transform> excludeTransforms = null)
        {
            // 优先返回手动映射
            if (_manualMappings.TryGetValue(source, out var manualTarget))
            {
                return new BoneMappingResult
                {
                    Target = manualTarget,
                    Confidence = 1.0f,
                    Strategy = "手动映射"
                };
            }
            
            BoneMappingResult bestResult = null;
            
            // 按优先级顺序尝试每个策略
            foreach (var strategy in _strategies)
            {
                var result = strategy.FindTarget(source, sourceRoot, targetRoot, excludeTransforms);
                if (result != null && result.Target != null)
                {
                    // 如果找到高置信度匹配，立即返回
                    if (result.IsHighConfidence)
                    {
                        return result;
                    }
                    
                    // 否则记录最佳结果
                    if (bestResult == null || result.Confidence > bestResult.Confidence)
                    {
                        bestResult = result;
                    }
                }
            }
            
            return bestResult;
        }
        
        /// <summary>
        /// 添加手动映射（用户明确指定的映射关系）
        /// </summary>
        public void AddManualMapping(Transform source, Transform target)
        {
            _manualMappings[source] = target;
        }
        
        /// <summary>
        /// 清空所有手动映射
        /// </summary>
        public void ClearManualMappings()
        {
            _manualMappings.Clear();
        }
    }
}

