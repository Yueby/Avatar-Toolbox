using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 策略2: 同名搜索（限制在相同层级深度）
    /// 在目标层级中搜索同名对象，但必须位于相同的层级深度
    /// 这样可以避免误匹配到不同层级的同名骨骼
    /// </summary>
    public class ExactNameStrategy : IBoneMappingStrategy
    {
        public string Name => "同名搜索";
        public int Priority => 2;
        
        public BoneMappingResult FindTarget(Transform source, Transform sourceRoot, Transform targetRoot, HashSet<Transform> excludeTransforms = null)
        {
            // 获取源骨骼的深度
            var sourceDepth = GetDepth(source, sourceRoot);
            
            var allTransforms = targetRoot.GetComponentsInChildren<Transform>(true);
            var matches = allTransforms
                .Where(t => 
                {
                    if (t == targetRoot || t.name != source.name)
                        return false;
                    
                    // 排除自动创建的对象
                    if (excludeTransforms != null && excludeTransforms.Contains(t))
                        return false;
                    
                    // 严格层级检测：必须完全相同的深度
                    var targetDepth = GetDepth(t, targetRoot);
                    return sourceDepth == targetDepth;
                })
                .ToList();
            
            // 找到唯一匹配：高置信度
            if (matches.Count == 1)
            {
                return new BoneMappingResult
                {
                    Target = matches[0],
                    Confidence = 0.85f,
                    Strategy = Name
                };
            }
            // 找到多个匹配：需要用户确认
            else if (matches.Count > 1)
            {
                return new BoneMappingResult
                {
                    Target = matches[0],
                    Confidence = 0.6f,
                    Strategy = Name,
                    Candidates = matches
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// 获取对象在层级中的深度
        /// </summary>
        private int GetDepth(Transform transform, Transform root)
        {
            int depth = 0;
            var current = transform;
            while (current != null && current != root)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }
    }
}

