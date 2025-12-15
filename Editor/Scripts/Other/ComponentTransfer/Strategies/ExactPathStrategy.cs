using System.Collections.Generic;
using UnityEngine;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 策略1: 精确路径匹配
    /// 通过完全相同的层级路径来查找目标骨骼
    /// 优先级最高，如果路径完全匹配则置信度为 100%
    /// </summary>
    public class ExactPathStrategy : IBoneMappingStrategy
    {
        public string Name => "精确路径匹配";
        public int Priority => 1;
        
        public BoneMappingResult FindTarget(Transform source, Transform sourceRoot, Transform targetRoot, HashSet<Transform> excludeTransforms = null)
        {
            var relativePath = GetRelativePath(sourceRoot, source);
            
            // 如果源骨骼就是根对象，直接返回目标根对象
            if (string.IsNullOrEmpty(relativePath))
            {
                return new BoneMappingResult 
                { 
                    Target = targetRoot, 
                    Confidence = 1.0f, 
                    Strategy = Name 
                };
            }
            
            // 尝试在目标层级中找到相同路径的对象
            var target = FindByPath(targetRoot, relativePath);
            if (target != null)
            {
                return new BoneMappingResult
                {
                    Target = target,
                    Confidence = 1.0f,
                    Strategy = Name
                };
            }
            
            return null;
        }
        
        /// <summary>
        /// 获取相对于根对象的路径
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

            if (current != root)
                return "";

            return path.ToString();
        }
        
        /// <summary>
        /// 通过路径查找对象
        /// </summary>
        private Transform FindByPath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            var pathParts = path.Split('/');
            var current = root;

            foreach (var part in pathParts)
            {
                var child = current.Find(part);
                if (child == null)
                    return null;
                current = child;
            }

            return current;
        }
    }
}

