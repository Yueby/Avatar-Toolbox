using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    /// <summary>
    /// 骨骼映射推荐窗口
    /// 用于向用户展示骨骼匹配候选项并收集用户决策
    /// </summary>
    public class BoneMappingRecommendationWindow : EditorWindow
    {
        private Transform _sourceBone;
        private List<Transform> _candidates;
        private System.Action<BoneMappingUserResult> _onConfirm;
        private Vector2 _scrollPos;
        private int _selectedIndex = 0;
        private Transform _sourceRoot;
        private Transform _targetRoot;
        
        // 全局停止标志，用于防止多个窗口排队显示
        private static bool _globalStopRequested = false;
        
        public static void ResetGlobalStopFlag()
        {
            _globalStopRequested = false;
        }
        
        public static void Show(Transform sourceBone, Transform sourceRoot, Transform targetRoot, List<Transform> candidates, System.Action<BoneMappingUserResult> onConfirm)
        {
            // 如果已经请求停止，直接返回停止结果，不显示窗口
            if (_globalStopRequested)
            {
                onConfirm?.Invoke(new BoneMappingUserResult
                {
                    ChoiceType = BoneMappingUserChoice.Stop,
                    SelectedTarget = null
                });
                return;
            }
            
            var window = GetWindow<BoneMappingRecommendationWindow>(true, "骨骼映射推荐", true);
            window._sourceBone = sourceBone;
            window._sourceRoot = sourceRoot;
            window._targetRoot = targetRoot;
            window._candidates = candidates ?? new List<Transform>();
            window._onConfirm = onConfirm;
            window.minSize = new Vector2(600, 500);
            window.maxSize = new Vector2(800, 800);
            window.ShowModal();
        }
        
        private void OnGUI()
        {
            // 在右上角绘制停止按钮
            DrawStopButton();
            
            EditorGUILayout.LabelField("骨骼映射推荐", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("源骨骼信息", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"名称: {_sourceBone.name}");
            EditorGUILayout.LabelField($"完整路径: {GetFullPath(_sourceBone, _sourceRoot)}");
            EditorGUILayout.ObjectField("对象引用", _sourceBone, typeof(Transform), true);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            if (_candidates.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到匹配的候选骨骼，建议创建新骨骼或手动选择", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField($"找到 {_candidates.Count} 个候选目标骨骼:", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);
                
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
                
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var candidate = _candidates[i];
                    if (candidate == null)
                        continue;
                    
                    var isSelected = i == _selectedIndex;
                    
                    var bgColor = GUI.backgroundColor;
                    if (isSelected)
                        GUI.backgroundColor = new Color(0.5f, 0.8f, 1f, 0.3f);
                    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    var buttonContent = new GUIContent(
                        i == 0 ? "★ " : "   ",
                        i == 0 ? "最佳匹配" : ""
                    );
                    
                    if (GUILayout.Toggle(isSelected, buttonContent, EditorStyles.radioButton, GUILayout.Width(20)))
                    {
                        _selectedIndex = i;
                    }
                    
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField($"候选 {i + 1}: {candidate.name}", EditorStyles.boldLabel);
                    
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"路径: {GetFullPath(candidate, _targetRoot)}", EditorStyles.miniLabel);
                    EditorGUILayout.ObjectField("对象", candidate, typeof(Transform), true);
                    EditorGUI.indentLevel--;
                    
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.EndVertical();
                    
                    GUI.backgroundColor = bgColor;
                    
                    EditorGUILayout.Space(3);
                }
                
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = _candidates.Count > 0;
            if (GUILayout.Button("✓ 确认选择", GUILayout.Height(30)))
            {
                _onConfirm?.Invoke(new BoneMappingUserResult
                {
                    ChoiceType = BoneMappingUserChoice.SelectCandidate,
                    SelectedTarget = _candidates[_selectedIndex]
                });
                Close();
                GUIUtility.ExitGUI();
            }
            GUI.enabled = true;
            
            if (GUILayout.Button("○ 创建新骨骼", GUILayout.Height(30)))
            {
                _onConfirm?.Invoke(new BoneMappingUserResult
                {
                    ChoiceType = BoneMappingUserChoice.CreateNew,
                    SelectedTarget = null
                });
                Close();
                GUIUtility.ExitGUI();
            }
            
            if (GUILayout.Button("✕ 跳过", GUILayout.Height(30)))
            {
                _onConfirm?.Invoke(new BoneMappingUserResult
                {
                    ChoiceType = BoneMappingUserChoice.Skip,
                    SelectedTarget = null
                });
                Close();
                GUIUtility.ExitGUI();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "提示:\n" +
                "• \"确认选择\" - 使用选中的候选骨骼\n" +
                "• \"创建新骨骼\" - 在目标层级中创建新的骨骼\n" +
                "• \"跳过\" - 跳过当前骨骼，不创建对象，继续转移下一个", 
                MessageType.Info);
        }
        
        private void DrawStopButton()
        {
            // 在右上角绘制停止按钮
            var buttonWidth = 90f;
            var buttonHeight = 25f;
            var margin = 10f;
            
            var rect = new Rect(
                position.width - buttonWidth - margin,
                margin,
                buttonWidth,
                buttonHeight
            );
            
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            
            if (GUI.Button(rect, "⚠ 停止", EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("确认停止", 
                    "确定要停止整个转移流程吗？\n已完成的转移不会回退。", 
                    "停止", "取消"))
                {
                    // 设置全局停止标志，防止后续窗口显示
                    _globalStopRequested = true;
                    
                    _onConfirm?.Invoke(new BoneMappingUserResult
                    {
                        ChoiceType = BoneMappingUserChoice.Stop,
                        SelectedTarget = null
                    });
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
            
            GUI.backgroundColor = originalColor;
        }
        
        private string GetFullPath(Transform transform, Transform root)
        {
            if (transform == root || transform == null || root == null)
                return transform?.name ?? "";

            var path = new System.Text.StringBuilder();
            var current = transform;

            while (current != null && current != root)
            {
                if (path.Length > 0)
                    path.Insert(0, "/");
                path.Insert(0, current.name);
                current = current.parent;
            }

            return path.ToString();
        }
        
        private void OnDestroy()
        {
            if (_onConfirm != null)
            {
                _onConfirm = null;
            }
        }
    }
}

