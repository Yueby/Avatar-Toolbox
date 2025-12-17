using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Yueby.Core.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 策略3: 模糊名称匹配
    /// 最复杂但最智能的匹配策略，支持：
    /// - 前缀相似度匹配（主体名称）
    /// - 智能单复数处理（Breast vs Breasts）
    /// - 数字编号归一化（1 vs 001）
    /// - L/R 侧向识别（.L, _L, .L.001 等）
    /// - 层级深度严格匹配
    /// - 路径相似度计算
    /// </summary>
    public class FuzzyNameStrategy : IBoneMappingStrategy
    {
        public string Name => "模糊名称匹配";
        public int Priority => 3;
        
        private static readonly Dictionary<string, string[]> CommonVariants = new Dictionary<string, string[]>
        {
            { "left", new[] { "left", "l", "_l", ".l", "_left", "left_" } },
            { "right", new[] { "right", "r", "_r", ".r", "_right", "right_" } },
            
            // 手部和手臂
            { "shoulder", new[] { "shoulder", "clavicle", "collar" } },
            { "upperarm", new[] { "upperarm", "upper_arm", "uparm", "arm" } },
            { "lowerarm", new[] { "lowerarm", "lower_arm", "lowarm", "forearm", "elbow" } },
            { "hand", new[] { "hand", "wrist", "palm" } },
            { "finger", new[] { "finger", "thumb", "index", "middle", "ring", "pinky" } },
            
            // 腿部
            { "upperleg", new[] { "upperleg", "upper_leg", "upleg", "thigh", "leg" } },
            { "lowerleg", new[] { "lowerleg", "lower_leg", "lowleg", "knee", "calf", "shin" } },
            { "foot", new[] { "foot", "ankle", "feet" } },
            { "toe", new[] { "toe", "toes" } },
            
            // 躯干
            { "spine", new[] { "spine", "back", "torso" } },
            { "chest", new[] { "chest", "breast", "bust" } },
            { "hips", new[] { "hips", "pelvis", "hip" } },
            
            // 头部
            { "head", new[] { "head", "skull" } },
            { "neck", new[] { "neck" } },
        };
        
        public BoneMappingResult FindTarget(Transform source, Transform sourceRoot, Transform targetRoot, HashSet<Transform> excludeTransforms = null)
        {
            var allTransforms = targetRoot.GetComponentsInChildren<Transform>(true);
            var candidates = new List<(Transform transform, float score, int lcsLength, float pathScore, float nameScore)>();
            
            var sourceName = NormalizeName(source.name);
            var sourceOriginal = source.name.ToLower();
            var sourcePath = GetRelativePath(sourceRoot, source);
            
            foreach (var target in allTransforms)
            {
                if (target == targetRoot)
                    continue;
                
                // 排除自动创建的对象
                if (excludeTransforms != null && excludeTransforms.Contains(target))
                    continue;
                    
                var targetName = NormalizeName(target.name);
                var targetOriginal = target.name.ToLower();
                var targetPath = GetRelativePath(targetRoot, target);
                
                // === 优先级1：前缀匹配（主体名称） ===
                var sourcePrefix = ExtractNamePrefix(sourceName);
                var targetPrefix = ExtractNamePrefix(targetName);
                var prefixSimilarity = CalculatePrefixSimilarity(sourcePrefix, targetPrefix);

                // 降低阈值，允许语义匹配但字符匹配度较低的情况通过
                // 因为 CheckPrefixVariants 已经在 CalculatePrefixSimilarity 中处理了语义匹配
                if (prefixSimilarity < 0.4f)
                    continue;
                
                // === 优先级2：同级别（层级深度）- 强制约束 ===
                var sourceDepth = GetPathDepth(sourcePath);
                var targetDepth = GetPathDepth(targetPath);
                
                if (sourceDepth != targetDepth)
                    continue;
                
                // === 优先级3：同侧（L/R）- 强制约束 ===
                var sourceSide = GetSideSuffix(sourceOriginal);
                var targetSide = GetSideSuffix(targetOriginal);
                
                if (sourceSide != "" && targetSide != "" && sourceSide != targetSide)
                    continue;
                
                // === 综合评分 ===
                var nameSimilarity = CalculateSimilarity(sourceName, targetName, sourceOriginal, targetOriginal);
                var pathSimilarity = CalculatePathSimilarity(sourcePath, targetPath);
                var finalScore = (prefixSimilarity * 0.8f) + (pathSimilarity * 0.2f);
                
                if (finalScore > 0.65f)
                {
                    var consecutiveMatchCount = 0;
                    var minLength = Math.Min(sourcePrefix.Length, targetPrefix.Length);
                    for (int i = 0; i < minLength; i++)
                    {
                        if (sourcePrefix[i] == targetPrefix[i])
                            consecutiveMatchCount++;
                        else
                            break;
                    }
                    
                    YuebyLogger.LogInfo("FuzzyNameStrategy", 
                        $"候选项: {source.name} -> {target.name} | " +
                        $"前缀: {prefixSimilarity:F2} | 深度: {sourceDepth} (严格相同) | 路径: {pathSimilarity:F2} | " +
                        $"总分: {finalScore:F2}");
                    
                    candidates.Add((target, finalScore, consecutiveMatchCount, pathSimilarity, nameSimilarity));
                }
            }
            
            if (candidates.Count == 0)
            {
                var sourcePrefix = ExtractNamePrefix(sourceName);
                YuebyLogger.LogInfo("FuzzyNameStrategy", 
                    $"未找到候选项: {source.name} (规范化: {sourceName}, 前缀: {sourcePrefix}, 路径: {sourcePath})");
                return null;
            }
            
            candidates = candidates
                .OrderByDescending(c => c.score)
                .ThenByDescending(c => c.lcsLength)
                .ThenByDescending(c => c.pathScore)
                .ToList();
            
            var best = candidates[0];
            
            return new BoneMappingResult
            {
                Target = best.transform,
                Confidence = best.score,
                Strategy = Name,
                Candidates = candidates.Take(5).Select(c => c.transform).ToList()
            };
        }
        
        #region 路径处理
        
        private string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return "";
            
            var path = target.name;
            var current = target.parent;
            
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }
        
        private float CalculatePathSimilarity(string sourcePath, string targetPath)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetPath))
                return 0f;
            
            var sourceParts = sourcePath.ToLower().Split('/');
            var targetParts = targetPath.ToLower().Split('/');
            
            var depthDiff = Math.Abs(sourceParts.Length - targetParts.Length);
            var maxDepth = Math.Max(sourceParts.Length, targetParts.Length);
            var depthScore = depthDiff == 0 ? 1.0f : 1.0f - (depthDiff / (float)maxDepth);
            
            float matchingNodes = 0f;
            int minLength = Math.Min(sourceParts.Length, targetParts.Length);
            
            for (int i = 0; i < minLength; i++)
            {
                var sourceNode = NormalizeName(sourceParts[i]);
                var targetNode = NormalizeName(targetParts[i]);
                float positionWeight = 1.0f + (0.5f * (minLength - i) / (float)minLength);
                
                if (sourceNode == targetNode)
                {
                    matchingNodes += positionWeight;
                }
                else
                {
                    var nodeSimilarity = GetLongestCommonSubstring(sourceNode, targetNode);
                    var nodeMaxLength = Math.Max(sourceNode.Length, targetNode.Length);
                    if (nodeSimilarity >= 3 && nodeMaxLength > 0)
                    {
                        var partialScore = ((float)nodeSimilarity / nodeMaxLength) * positionWeight * 0.5f;
                        
                        var sourceNodeSide = GetSideSuffix(sourceParts[i]);
                        var targetNodeSide = GetSideSuffix(targetParts[i]);
                        if (sourceNodeSide != "" && targetNodeSide != "" && sourceNodeSide != targetNodeSide)
                        {
                            partialScore *= 0.3f;
                        }
                        
                        matchingNodes += partialScore;
                    }
                }
            }
            
            float maxPossibleScore = 0f;
            for (int i = 0; i < minLength; i++)
            {
                maxPossibleScore += 1.0f + (0.5f * (minLength - i) / (float)minLength);
            }
            var nodeScore = maxPossibleScore > 0 ? matchingNodes / maxPossibleScore : 0f;
            
            return (depthScore * 0.5f) + (nodeScore * 0.5f);
        }
        
        private int GetPathDepth(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;
            return path.Split('/').Length;
        }
        
        #endregion
        
        #region 名称归一化和前缀提取
        
        private string NormalizeName(string name)
        {
            return name.ToLower()
                .Replace("_", "")
                .Replace(".", "")
                .Replace("-", "")
                .Replace(" ", "");
        }
        
        private string ExtractNamePrefix(string normalizedName)
        {
            if (string.IsNullOrEmpty(normalizedName))
                return "";
            
            var descriptors = new[] { "root", "base" };
            var result = normalizedName;
            
            foreach (var descriptor in descriptors)
            {
                result = result.Replace(descriptor, "");
            }
            
            // 智能单复数处理（支持 L/R 标记）
            var lMatch = System.Text.RegularExpressions.Regex.Match(result, @"l((\d+)|((root|base)*))$");
            var rMatch = System.Text.RegularExpressions.Regex.Match(result, @"r((\d+)|((root|base)*))$");
            
            string sideMarker = null;
            string afterSide = null;
            int sideIndex = -1;
            
            if (lMatch.Success)
            {
                sideMarker = "l";
                sideIndex = lMatch.Index;
                afterSide = lMatch.Groups[1].Value;
            }
            else if (rMatch.Success)
            {
                sideMarker = "r";
                sideIndex = rMatch.Index;
                afterSide = rMatch.Groups[1].Value;
            }
            
            if (sideMarker != null)
            {
                var beforeSide = result.Substring(0, sideIndex);
                
                if (beforeSide.Length > 3 && beforeSide.EndsWith("s"))
                {
                    if (beforeSide.Length >= 2 && beforeSide[beforeSide.Length - 2] != 's')
                    {
                        beforeSide = beforeSide.Substring(0, beforeSide.Length - 1);
                    }
                }
                
                result = beforeSide + sideMarker + afterSide;
            }
            else
            {
                if (result.Length > 3 && result.EndsWith("s"))
                {
                    if (result.Length >= 2 && result[result.Length - 2] != 's')
                    {
                        result = result.Substring(0, result.Length - 1);
                    }
                }
            }
            
            if (result.Length < 2)
                return normalizedName;
            
            return result;
        }
        
        #endregion
        
        #region L/R 侧向识别
        
        private string DetectSideMarker(string text, bool isNormalized = false)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            
            var lowerText = text.ToLower();
            
            if (isNormalized)
            {
                var lMatch = System.Text.RegularExpressions.Regex.Match(lowerText, @"l(\d+)$");
                var rMatch = System.Text.RegularExpressions.Regex.Match(lowerText, @"r(\d+)$");
                
                if (lMatch.Success) return "l";
                if (rMatch.Success) return "r";
                
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerText, @"l(root|base)*$"))
                    return "l";
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerText, @"r(root|base)*$"))
                    return "r";
            }
            else
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerText, @"[\._\-\s]l([\._\-\s\d]|$)"))
                    return "l";
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerText, @"[\._\-\s]r([\._\-\s\d]|$)"))
                    return "r";
                
                if (lowerText.EndsWith("l") && lowerText.Length > 1)
                {
                    var secondLast = lowerText[lowerText.Length - 2];
                    if (!"aeiou".Contains(secondLast))
                        return "l";
                }
                if (lowerText.EndsWith("r") && lowerText.Length > 1)
                {
                    var secondLast = lowerText[lowerText.Length - 2];
                    if (!"aeiou".Contains(secondLast))
                        return "r";
                }
                
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerText, @"\bleft\b"))
                    return "l";
                if (System.Text.RegularExpressions.Regex.IsMatch(lowerText, @"\bright\b"))
                    return "r";
            }
            
            return "";
        }
        
        private string GetSideSuffix(string name)
        {
            return DetectSideMarker(name, isNormalized: false);
        }
        
        #endregion
        
        #region 数字编号处理
        
        private string NormalizeNumbers(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            return System.Text.RegularExpressions.Regex.Replace(text, @"\d+", m => {
                if (int.TryParse(m.Value, out int num))
                {
                    return num <= 999 ? num.ToString("000") : m.Value;
                }
                return m.Value;
            });
        }
        
        private List<int> ExtractNumbers(string normalizedName)
        {
            var numbers = new List<int>();
            var matches = System.Text.RegularExpressions.Regex.Matches(normalizedName, @"\d+");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (int.TryParse(match.Value, out int num))
                {
                    numbers.Add(num);
                }
            }
            return numbers;
        }
        
        #endregion
        
        #region 前缀相似度计算
        
        private float CalculatePrefixSimilarity(string sourcePrefix, string targetPrefix)
        {
            if (string.IsNullOrEmpty(sourcePrefix) || string.IsNullOrEmpty(targetPrefix))
                return 0f;
            
            if (sourcePrefix == targetPrefix)
                return 1.0f;

            // === 优先级1：检查 CommonVariants 语义映射 ===
            // 先检查两个前缀是否属于同一个语义组（如 leg 组包含 lowerleg, knee, thigh 等）
            var variantSimilarity = CheckPrefixVariants(sourcePrefix, targetPrefix);
            if (variantSimilarity > 0.7f)
            {
                // 如果语义匹配度高，直接返回高分，避免被后续的字符匹配过滤掉
                return variantSimilarity;
            }

            var sourceNumbers = ExtractNumbers(sourcePrefix);
            var targetNumbers = ExtractNumbers(targetPrefix);
            var sourceSide = DetectSideMarker(sourcePrefix, isNormalized: true);
            var targetSide = DetectSideMarker(targetPrefix, isNormalized: true);

            // 提取纯主体名称（移除数字和 L/R 后缀）
            var sourcePureName = System.Text.RegularExpressions.Regex.Replace(sourcePrefix, @"[lr]$", "");
            var targetPureName = System.Text.RegularExpressions.Regex.Replace(targetPrefix, @"[lr]$", "");
            sourcePureName = System.Text.RegularExpressions.Regex.Replace(sourcePureName, @"\d+", "");
            targetPureName = System.Text.RegularExpressions.Regex.Replace(targetPureName, @"\d+", "");

            // 移除开头的 "left" 和 "right"
            if (sourcePureName.StartsWith("left"))
                sourcePureName = sourcePureName.Substring(4);
            else if (sourcePureName.StartsWith("right"))
                sourcePureName = sourcePureName.Substring(5);

            if (targetPureName.StartsWith("left"))
                targetPureName = targetPureName.Substring(4);
            else if (targetPureName.StartsWith("right"))
                targetPureName = targetPureName.Substring(5);

            if (string.IsNullOrEmpty(sourcePureName) || string.IsNullOrEmpty(targetPureName) ||
                sourcePureName.Length < 2 || targetPureName.Length < 2)
            {
                sourcePureName = sourcePrefix;
                targetPureName = targetPrefix;
            }
            
            var minLength = Math.Min(sourcePureName.Length, targetPureName.Length);
            var maxLength = Math.Max(sourcePureName.Length, targetPureName.Length);
            var consecutiveMatchCount = 0;
            
            for (int i = 0; i < minLength; i++)
            {
                if (sourcePureName[i] == targetPureName[i])
                    consecutiveMatchCount++;
                else
                    break;
            }

            // 如果语义匹配度较高，降低字符匹配的要求
            var minMatchThreshold = variantSimilarity > 0.5f ? 1 : 2;
            if (consecutiveMatchCount < minMatchThreshold)
            {
                // 即使字符匹配度低，如果语义匹配度高，也给予一定分数
                if (variantSimilarity > 0.5f)
                {
                    return variantSimilarity * 0.8f;
                }
                return 0f;
            }

            var baseSimilarity = (float)consecutiveMatchCount / maxLength;
            
            // 主体名称完全匹配加成
            var pureNameBonus = sourcePureName == targetPureName ? 0.3f : 0f;
            
            // 数字匹配加成
            var numberBonus = 0f;
            if (sourceNumbers.Count > 0 && targetNumbers.Count > 0)
            {
                var matchingNumbers = sourceNumbers.Intersect(targetNumbers).Count();
                var totalNumbers = Math.Max(sourceNumbers.Count, targetNumbers.Count);
                if (matchingNumbers > 0)
                {
                    numberBonus = ((float)matchingNumbers / totalNumbers) * 0.2f;
                }
            }
            
            // L/R 匹配加成
            var sideBonus = (!string.IsNullOrEmpty(sourceSide) && !string.IsNullOrEmpty(targetSide) && sourceSide == targetSide) ? 0.05f : 0f;
            
            // 前缀匹配加成
            var prefixBonus = (consecutiveMatchCount == minLength) ? 0.05f : 0f;

            // 语义变体匹配加成（如果之前已经检查过）
            var variantBonus = variantSimilarity > 0.5f ? variantSimilarity * 0.2f : 0f;

            var finalSimilarity = Math.Min(1.0f, baseSimilarity + pureNameBonus + numberBonus + sideBonus + prefixBonus + variantBonus);

            return finalSimilarity;
        }

        /// <summary>
        /// 检查两个前缀是否属于 CommonVariants 中的同一个语义组
        /// 例如：lowerleg 和 knee 都属于 leg 组，应该给予高分
        /// </summary>
        private float CheckPrefixVariants(string sourcePrefix, string targetPrefix)
        {
            if (string.IsNullOrEmpty(sourcePrefix) || string.IsNullOrEmpty(targetPrefix))
                return 0f;

            // 步骤1: 移除末尾的 L/R 标记（如 .L, .R, l, r）
            var sourcePure = System.Text.RegularExpressions.Regex.Replace(sourcePrefix, @"[lr]$", "");
            var targetPure = System.Text.RegularExpressions.Regex.Replace(targetPrefix, @"[lr]$", "");

            // 步骤2: 移除所有数字
            sourcePure = System.Text.RegularExpressions.Regex.Replace(sourcePure, @"\d+", "");
            targetPure = System.Text.RegularExpressions.Regex.Replace(targetPure, @"\d+", "");

            // 步骤3: 移除开头的 "left" 和 "right"
            if (sourcePure.StartsWith("left"))
                sourcePure = sourcePure.Substring(4);
            else if (sourcePure.StartsWith("right"))
                sourcePure = sourcePure.Substring(5);

            if (targetPure.StartsWith("left"))
                targetPure = targetPure.Substring(4);
            else if (targetPure.StartsWith("right"))
                targetPure = targetPure.Substring(5);


            // 检查每个变体组
            foreach (var kvp in CommonVariants)
            {
                bool sourceInGroup = false;
                bool targetInGroup = false;
                string matchedSourceVariant = null;
                string matchedTargetVariant = null;

                // 检查源前缀是否属于这个组
                foreach (var variant in kvp.Value)
                {
                    // 更精确的匹配：检查是否是完整单词匹配或包含关系
                    // 处理情况：
                    // 1. 完全匹配：lowerleg == lowerleg
                    // 2. 包含关系：lowerleg 包含 leg，knee 包含在 rightknee 中
                    // 3. 复合词：lowerleg 和 leg 的关系
                    if (sourcePure == variant)
                    {
                        sourceInGroup = true;
                        matchedSourceVariant = variant;
                        break;
                    }

                    // 检查是否包含（至少3个字符，避免误匹配）
                    if (variant.Length >= 3 && sourcePure.Contains(variant))
                    {
                        sourceInGroup = true;
                        matchedSourceVariant = variant;
                        break;
                    }

                    if (sourcePure.Length >= 3 && variant.Contains(sourcePure))
                    {
                        sourceInGroup = true;
                        matchedSourceVariant = variant;
                        break;
                    }
                }

                // 检查目标前缀是否属于这个组
                foreach (var variant in kvp.Value)
                {
                    if (targetPure == variant)
                    {
                        targetInGroup = true;
                        matchedTargetVariant = variant;
                        break;
                    }

                    if (variant.Length >= 3 && targetPure.Contains(variant))
                    {
                        targetInGroup = true;
                        matchedTargetVariant = variant;
                        break;
                    }

                    if (targetPure.Length >= 3 && variant.Contains(targetPure))
                    {
                        targetInGroup = true;
                        matchedTargetVariant = variant;
                        break;
                    }
                }

                // 如果两者都属于同一个组，返回高分
                if (sourceInGroup && targetInGroup)
                {
                    return 0.85f; // 返回高相似度
                }
            }

            return 0f;
        }

        #endregion

        #region 名称相似度计算

        private float CalculateSimilarity(string normalizedSource, string normalizedTarget, string originalSource, string originalTarget)
        {
            if (string.IsNullOrEmpty(normalizedSource) || string.IsNullOrEmpty(normalizedTarget))
                return 0f;
            
            var maxLength = Math.Max(normalizedSource.Length, normalizedTarget.Length);
            var minLength = Math.Min(normalizedSource.Length, normalizedTarget.Length);
            
            var lcsLength = GetLongestCommonSubstring(normalizedSource, normalizedTarget);
            var lcsScore = maxLength > 0 ? (float)lcsLength / maxLength : 0f;
            
            var variantScore = CheckCommonVariants(normalizedSource, normalizedTarget);
            
            var substringScore = 0f;
            if (minLength >= 3)
            {
                if (normalizedSource.Contains(normalizedTarget) || normalizedTarget.Contains(normalizedSource))
                    substringScore = 1.0f;
            }
            
            var levenshteinScore = maxLength > 0 ? 1.0f - (LevenshteinDistance(normalizedSource, normalizedTarget) / (float)maxLength) : 0f;
            
            var initialScore = (normalizedSource[0] == normalizedTarget[0]) ? 1.0f : 0f;
            
            var finalScore = (lcsScore * 0.60f) + 
                            (variantScore * 0.20f) + 
                            (substringScore * 0.10f) + 
                            (levenshteinScore * 0.08f) + 
                            (initialScore * 0.02f);
            
            return finalScore;
        }
        
        private int GetLongestCommonSubstring(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return 0;
            
            int maxLength = 0;
            int[,] lengths = new int[s1.Length + 1, s2.Length + 1];
            
            for (int i = 0; i < s1.Length; i++)
            {
                for (int j = 0; j < s2.Length; j++)
                {
                    if (s1[i] == s2[j])
                    {
                        lengths[i + 1, j + 1] = lengths[i, j] + 1;
                        if (lengths[i + 1, j + 1] > maxLength)
                        {
                            maxLength = lengths[i + 1, j + 1];
                        }
                    }
                }
            }
            
            return maxLength;
        }
        
        private int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
        
        private float CheckCommonVariants(string source, string target)
        {
            foreach (var kvp in CommonVariants)
            {
                bool sourceMatch = kvp.Value.Any(v => source.Contains(v));
                bool targetMatch = kvp.Value.Any(v => target.Contains(v));
                
                if (sourceMatch && targetMatch)
                    return 1.0f;
            }
            return 0f;
        }
        
        #endregion
    }
}

