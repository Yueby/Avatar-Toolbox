using System.Collections.Generic;
using UnityEngine;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 用户选择类型
    /// </summary>
    public enum BoneMappingUserChoice
    {
        SelectCandidate,  // 选择候选项
        CreateNew,        // 创建新骨骼
        Skip,            // 跳过当前骨骼
        Stop             // 停止整个转移
    }

    /// <summary>
    /// 骨骼映射匹配结果
    /// </summary>
    public class BoneMappingResult
    {
        public Transform Target { get; set; }
        public float Confidence { get; set; }
        public string Strategy { get; set; }
        public List<Transform> Candidates { get; set; }
        
        public bool IsHighConfidence => Confidence >= 0.8f;
        public bool NeedsUserConfirmation => Confidence < 0.8f && Candidates != null && Candidates.Count > 0;
    }

    /// <summary>
    /// 用户选择结果
    /// </summary>
    public class BoneMappingUserResult
    {
        public BoneMappingUserChoice ChoiceType { get; set; }
        public Transform SelectedTarget { get; set; }
    }

    /// <summary>
    /// 跨目标的用户决策缓存（用于多目标转移时复用决策）
    /// </summary>
    public class CrossTargetUserDecision
    {
        public BoneMappingUserChoice ChoiceType { get; set; }
        public string SelectedTargetRelativePath { get; set; }  // 候选项的相对路径（如果选择了候选项）
    }
}

