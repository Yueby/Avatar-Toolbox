using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yueby.Core.Utils;
using Yueby.Utils;

namespace YuebyAvatarTools.ComponentTransfer.Editor
{
    public class ComponentTransferWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private Transform _sourceRoot;
        private List<IComponentTransferPlugin> _plugins = new List<IComponentTransferPlugin>();
        private Vector2 _pluginScrollPos;
        private Vector2 _targetScrollPos;
        private bool _showTransferInfo = false; // 控制转移信息显示
        private bool _showTargetList = false; // 控制目标对象列表显示

        // SplitView 组件
        private SplitView _splitView;

        private void OnEnable()
        {
            InitializePlugins();
            InitializeSplitView();
        }

        private void InitializeSplitView()
        {
            // 创建分割视图，垂直分割（上下布局），初始分割位置为窗口高度的85%
            // 这样上半部分占85%，下半部分占15%
            float initialPosition = Mathf.Max(400f, Mathf.Min(position.height * 0.85f, position.height - 150f));
            _splitView = new SplitView(initialPosition, SplitView.SplitOrientation.Vertical, "ComponentTransferWindow_SplitPosition");

            // 设置面板绘制回调
            _splitView.SetPanelDrawers(DrawUpperPanel, DrawLowerPanel);
        }

        private void InitializePlugins()
        {
            _plugins.Clear();

            // 添加所有插件
            _plugins.Add(new Plugins.PhysBoneTransferPlugin());
            _plugins.Add(new Plugins.MaterialTransferPlugin());
            _plugins.Add(new Plugins.BlendShapeTransferPlugin());
            _plugins.Add(new Plugins.ConstraintTransferPlugin());
            _plugins.Add(new Plugins.ParticleSystemTransferPlugin());
            _plugins.Add(new Plugins.ActiveStateTransferPlugin());
            _plugins.Add(new Plugins.AnimatorTransferPlugin());
            _plugins.Add(new Plugins.ModularAvatarTransferPlugin());
        }

        [MenuItem("Tools/YuebyTools/Utils/Component Transfer", false, 21)]
        public static void OpenWindow()
        {
            var window = CreateWindow<ComponentTransferWindow>();
            window.titleContent = new GUIContent("Component Transfer");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnGUI()
        {
            var selections = Selection.gameObjects;
            var isSelectedObject = selections.Length > 0;

            // 确保SplitView已初始化
            if (_splitView == null)
            {
                InitializeSplitView();
            }

            // 更新位置限制（窗口大小可能已变化）
            _splitView.SetPositionLimits(400f, position.height - 150f);

            // 绘制分割视图
            _splitView.OnGUI(new Rect(0, 0, position.width, position.height));
        }

        /// <summary>
        /// 绘制上半部分面板（配置区域）
        /// </summary>
        private void DrawUpperPanel(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.BeginVertical();

            var selections = Selection.gameObjects;
            var isSelectedObject = selections.Length > 0;

            EditorUI.DrawEditorTitle("组件转移工具");

            // 源对象选择
            EditorUI.VerticalEGLTitled("源对象", () =>
            {
                EditorUI.HorizontalEGL(() =>
                {
                    _sourceRoot = (Transform)EditorUI.ObjectFieldVertical(_sourceRoot, "源根对象", typeof(Transform));
                    EditorUI.Line(LineType.Vertical);
                }, GUILayout.MaxHeight(40));
            });

            // 插件设置
            EditorUI.VerticalEGLTitled("转移插件", () =>
            {
                _pluginScrollPos = EditorUI.ScrollViewEGL(() =>
                {
                    foreach (var plugin in _plugins)
                    {
                        EditorUI.VerticalEGLTitled(plugin.Name, () =>
                        {
                            plugin.IsEnabled = EditorUI.Toggle(plugin.IsEnabled, "启用");

                            if (plugin.IsEnabled)
                            {
                                // EditorUI.Line(LineType.Horizontal);
                                EditorUI.VerticalEGL(() =>
                                {
                                    EditorGUILayout.LabelField(plugin.Description, EditorStyles.wordWrappedLabel);
                                    EditorUI.Line(LineType.Horizontal);
                                    plugin.DrawSettings();
                                });
                            }
                        });
                    }
                }, _pluginScrollPos, GUILayout.Height(rect.height - 200)); // 为标题和其他元素留出空间
            });

            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
        }

        /// <summary>
        /// 绘制下半部分面板（执行和结果显示区域）
        /// </summary>
        private void DrawLowerPanel(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EditorGUILayout.BeginVertical();

            var selections = Selection.gameObjects;
            var isSelectedObject = selections.Length > 0;
            var enabledPlugins = _plugins.Where(p => p.IsEnabled).ToList();

            // 执行转移按钮
            EditorUI.VerticalEGLTitled("执行转移", () =>
            {
                // 开始转移按钮（最上方）
                if (GUILayout.Button("开始转移", GUILayout.Height(30)))
                {
                    if (_sourceRoot == null)
                    {
                        EditorUtility.DisplayDialog("错误", "请先选择源根对象", "确定");
                        return;
                    }

                    if (!isSelectedObject)
                    {
                        EditorUtility.DisplayDialog("错误", "请选择目标对象", "确定");
                        return;
                    }

                    ExecuteTransfer(selections);
                }

                EditorUI.Line(LineType.Horizontal);

                // 按钮下方内容用scrollview包裹
                _targetScrollPos = EditorUI.ScrollViewEGL(() =>
                {
                    // 对象信息（不折叠）
                    EditorUI.VerticalEGL(() =>
                    {
                        EditorGUILayout.LabelField($"源对象: {(_sourceRoot != null ? _sourceRoot.name : "未选择")}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"目标对象: {selections.Length} 个", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"启用插件: {enabledPlugins.Count} 个", EditorStyles.miniLabel);
                    });

                    // 目标对象列表（可折叠）
                    if (isSelectedObject)
                    {
                        EditorUI.Line(LineType.Horizontal);
                        _showTargetList = UnityEditor.EditorGUILayout.Foldout(_showTargetList, "目标对象列表");
                        if (_showTargetList)
                        {

                            for (int i = 0; i < selections.Length; i++)
                            {
                                EditorUI.HorizontalEGL(() =>
                                {
                                    EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(25));
                                    EditorGUILayout.ObjectField(selections[i], typeof(GameObject), true);
                                });
                            }
                        }
                    }
                    else
                    {
                        EditorUI.Line(LineType.Horizontal);
                        EditorGUILayout.HelpBox("请在场景中选择目标对象", MessageType.Error);
                    }
                }, _targetScrollPos);
            });

            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void ExecuteTransfer(GameObject[] targets)
        {
            var enabledPlugins = _plugins.Where(p => p.IsEnabled).ToList();

            if (enabledPlugins.Count == 0)
            {
                EditorUtility.DisplayDialog("警告", "没有启用任何转移插件", "确定");
                return;
            }

            // 清空跨目标决策缓存（开始新的转移任务）
            ComponentTransferBase.ClearCrossTargetDecisions();
            YuebyLogger.LogInfo("ComponentTransfer", "已清空跨目标决策缓存，开始新的转移任务");

            // 重置所有插件的停止标志和全局窗口标志
            foreach (var plugin in enabledPlugins)
            {
                if (plugin is ComponentTransferBase basePlugin)
                {
                    basePlugin.ResetStopFlag();
                }
            }
            BoneMappingRecommendationWindow.ResetGlobalStopFlag();

            bool overallSuccess = true;
            int processedTargets = 0;
            bool userStopped = false;

            foreach (var target in targets)
            {
                if (target == null) continue;

                processedTargets++;
                YuebyLogger.LogInfo("ComponentTransfer", $"开始处理目标: {target.name}");

                foreach (var plugin in enabledPlugins)
                {
                    // 检查是否应该停止（支持ComponentTransferBase的插件）
                    if (plugin is ComponentTransferBase basePlugin && basePlugin.ShouldStopTransfer())
                    {
                        YuebyLogger.LogWarning("ComponentTransfer", "用户已停止转移流程");
                        userStopped = true;
                        break;
                    }
                    
                    if (plugin.CanTransfer(_sourceRoot, target.transform))
                    {
                        try
                        {
                            bool success = plugin.ExecuteTransfer(_sourceRoot, target.transform);
                            
                            // 执行后再次检查停止标志
                            if (plugin is ComponentTransferBase basePlugin2 && basePlugin2.ShouldStopTransfer())
                            {
                                YuebyLogger.LogWarning("ComponentTransfer", "用户已停止转移流程");
                                userStopped = true;
                                break;
                            }
                            
                            if (!success)
                            {
                                YuebyLogger.LogWarning("ComponentTransfer", $"插件 {plugin.Name} 转移失败");
                                overallSuccess = false;
                            }
                            else
                            {
                                YuebyLogger.LogInfo("ComponentTransfer", $"插件 {plugin.Name} 转移成功");
                            }
                        }
                        catch (System.Exception e)
                        {
                            YuebyLogger.LogError("ComponentTransfer", $"插件 {plugin.Name} 执行时发生错误: {e.Message}");
                            overallSuccess = false;
                        }
                    }
                    else
                    {
                        YuebyLogger.LogInfo("ComponentTransfer", $"插件 {plugin.Name} 跳过 (无可转移内容)");
                    }
                }
                
                // 如果用户停止了，跳出外层循环
                if (userStopped)
                    break;
            }

            if (processedTargets > 0)
            {
                string message;
                if (userStopped)
                {
                    message = "转移已被用户停止";
                    YuebyLogger.LogWarning("ComponentTransfer", "转移操作已被用户停止");
                }
                else if (overallSuccess)
                {
                    message = "转移完成";
                    YuebyLogger.LogInfo("ComponentTransfer", "所有转移操作完成");
                }
                else
                {
                    message = "转移过程中发生了一些错误";
                    YuebyLogger.LogWarning("ComponentTransfer", "转移操作完成，但存在一些错误");
                }
                
                EditorUtility.DisplayDialog("转移结果", message, "确定");
            }
        }
    }
}