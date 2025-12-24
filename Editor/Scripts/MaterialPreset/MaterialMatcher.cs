using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Yueby.Tools.AvatarToolbox.MaterialPreset;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor
{
    public static class MaterialMatcher
    {
        private static readonly Regex SuffixRegex = new Regex(@"(\s?\(Instance\)|\.00\d|_Mat|_Material|\sCopy)$", RegexOptions.IgnoreCase);
        
        /// <summary>
        /// 智能匹配材质：优先在同槽位精确匹配，其次在同槽位模糊匹配，最后在其他槽位查找
        /// </summary>
        public static Material FindBestMatch(MaterialSlotConfig slotConfig, Material[] candidates, MatchMode matchMode)
        {
            if (candidates == null || candidates.Length == 0) return null;

            // 获取目标名称
            string targetName = slotConfig.FuzzyName;
            if (string.IsNullOrEmpty(targetName) && slotConfig.MaterialRef != null)
            {
                targetName = slotConfig.MaterialRef.name;
            }

            if (string.IsNullOrEmpty(targetName))
            {
                // 没有名称信息，使用槽位索引
                if (slotConfig.SlotIndex >= 0 && slotConfig.SlotIndex < candidates.Length)
                    return candidates[slotConfig.SlotIndex];
                return null;
            }

            string cleanTargetName = CleanName(targetName);
            
            // 第一步：检查同槽位是否匹配（优先同槽位）
            if (slotConfig.SlotIndex >= 0 && slotConfig.SlotIndex < candidates.Length)
            {
                Material sameSlotCandidate = candidates[slotConfig.SlotIndex];
                if (sameSlotCandidate != null)
                {
                    string cleanCandidateName = CleanName(sameSlotCandidate.name);
                    int score = CalculateScore(cleanTargetName, cleanCandidateName);
                    
                    // 同槽位匹配度足够（≥85），直接返回
                    // 包括精确匹配(100)、高相似(90)、颜色变体(85)
                    if (score >= 85)
                    {
                        return sameSlotCandidate;
                    }
                }
            }
            
            // 第二步：在所有槽位中查找精确匹配（分数100）
            for (int i = 0; i < candidates.Length; i++)
            {
                Material candidate = candidates[i];
                if (candidate == null) continue;
                
                string cleanCandidateName = CleanName(candidate.name);
                int score = CalculateScore(cleanTargetName, cleanCandidateName);
                
                if (score == 100)
                {
                    return candidate;
                }
            }
            
            // 第三步：在所有槽位中查找所有符合条件的候选材质（分数≥85）
            var qualifiedCandidates = new List<(Material material, int score, int distance)>();

            for (int i = 0; i < candidates.Length; i++)
            {
                Material candidate = candidates[i];
                if (candidate == null) continue;

                string cleanCandidateName = CleanName(candidate.name);
                int score = CalculateScore(cleanTargetName, cleanCandidateName);

                if (score >= 85)
                {
                    // 计算编辑距离（越小越相似）
                    int distance = LevenshteinDistance(cleanTargetName, cleanCandidateName);
                    qualifiedCandidates.Add((candidate, score, distance));
                }
            }

            // 如果有多个符合条件的候选，选择编辑距离最小的（名字最相似的）
            if (qualifiedCandidates.Count > 0)
            {
                // 先按编辑距离排序（越小越好），再按分数排序（越大越好）
                var bestCandidate = qualifiedCandidates
                    .OrderBy(c => c.distance)
                    .ThenByDescending(c => c.score)
                    .First();
                
                return bestCandidate.material;
            }

            // 都不匹配，返回null（不应用任何材质）
            return null;
        }

        private static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            // Remove common suffixes
            string clean = SuffixRegex.Replace(name, "");
            // Remove special chars
            clean = clean.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
            return clean;
        }

        private static int CalculateScore(string target, string candidate)
        {
            if (target == candidate) return 100; // Exact match (cleaned)
            
            // 子字符串包含关系：需要检查长度相似度，避免误匹配
            bool targetContainsCandidate = target.Contains(candidate);
            bool candidateContainsTarget = candidate.Contains(target);
            
            if (targetContainsCandidate || candidateContainsTarget)
            {
                // 计算长度差异比例
                int maxLength = Math.Max(target.Length, candidate.Length);
                int minLength = Math.Min(target.Length, candidate.Length);
                float lengthRatio = (float)minLength / maxLength;
                
                // 如果长度相似度高（>80%），才认为是有效的包含匹配
                if (lengthRatio > 0.8f)
                {
                    return 90; // 长度相近的包含关系
                }
                else
                {
                    // 长度差异大，可能是误匹配（如 "bag" vs "bagfur"），降低分数
                    return (int)(70 * lengthRatio);
                }
            }
            
            // Common prefix matching (for color variants like "Fuyu_cloth_white" vs "Fuyu_cloth_blue")
            int commonPrefixLength = GetCommonPrefixLength(target, candidate);
            
            if (commonPrefixLength > 0)
            {
                // 获取后缀部分
                string targetSuffix = target.Substring(commonPrefixLength);
                string candidateSuffix = candidate.Substring(commonPrefixLength);
                
                // 核心策略：检查后缀是否是前缀包含关系（物体名延续）
                // - 前缀包含：一个后缀从头开始包含另一个（bag vs bagfur 中，空后缀被fur包含）
                // - 非前缀包含：互不包含或只是中间包含（blue vs darkblue，blue在dark后面）
                
                bool suffixContainment = false;
                
                if (targetSuffix.Length == 0 || candidateSuffix.Length == 0)
                {
                    // 其中一个后缀为空（如 bag vs bagfur），认为是物体名的延续
                    suffixContainment = true;
                }
                else
                {
                    // 检查是否是"从头开始"的包含关系
                    // 如果较短的后缀是较长后缀的前缀，认为是物体名延续
                    if (targetSuffix.Length < candidateSuffix.Length)
                    {
                        suffixContainment = candidateSuffix.StartsWith(targetSuffix);
                    }
                    else if (candidateSuffix.Length < targetSuffix.Length)
                    {
                        suffixContainment = targetSuffix.StartsWith(candidateSuffix);
                    }
                    // 长度相同的情况下，如果完全不同就不是包含关系
                }
                
                // 如果后缀无包含关系，且前缀占比合理，认为是颜色变体
                int minLength = Math.Min(target.Length, candidate.Length);
                float prefixRatio = (float)commonPrefixLength / minLength;
                
                // 只要前缀占较短字符串的40%以上，就认为是同一物体
                if (prefixRatio >= 0.40f && !suffixContainment)
                {
                    return 85;
                }
            }

            // Levenshtein / Edit Distance based score
            int dist = LevenshteinDistance(target, candidate);
            int distMaxLength = Math.Max(target.Length, candidate.Length);
            if (distMaxLength == 0) return 0;

            float similarity = 1.0f - ((float)dist / distMaxLength);
            return (int)(similarity * 80); // Max 80 for fuzzy match
        }
        
        private static int GetCommonPrefixLength(string s1, string s2)
        {
            int len = Math.Min(s1.Length, s2.Length);
            for (int i = 0; i < len; i++)
            {
                if (s1[i] != s2[i]) return i;
            }
            return len;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }
    }
}

