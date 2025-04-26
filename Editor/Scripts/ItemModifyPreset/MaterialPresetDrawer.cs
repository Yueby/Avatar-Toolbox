using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Yueby.ModalWindow;
using System.Collections.Generic;
using Yueby.Utils;

/// <summary>
/// 材质预设对话框绘制器
/// </summary>
class MaterialPresetDrawer : ModalEditorWindowDrawer<MaterialPresetDrawer.PresetData>
{
    /// <summary>
    /// 预设数据类
    /// </summary>
    public class PresetData
    {
        /// <summary>
        /// 拖拽的材质
        /// </summary>
        public List<MaterialItem> materialItems = new List<MaterialItem>();

        /// <summary>
        /// 渲染器索引
        /// </summary>
        public int rendererIndex;

        /// <summary>
        /// 渲染器引用
        /// </summary>
        public Renderer renderer;

        /// <summary>
        /// 原始材质球
        /// </summary>
        public Material[] originalMaterials;

        /// <summary>
        /// 全局槽位索引
        /// </summary>
        public int globalSlotIndex = 0;
    }

    /// <summary>
    /// 材质项
    /// </summary>
    public class MaterialItem
    {
        /// <summary>
        /// 材质
        /// </summary>
        public Material material;

        /// <summary>
        /// 预设名称
        /// </summary>
        public string presetName = "新预设";

        /// <summary>
        /// 主题色
        /// </summary>
        public Color themeColor = Color.white;
    }

    // 材质列表
    private SimpleReorderableList _materialsList;

    // 滚动位置
    private Vector2 _scrollPosition = Vector2.zero;

    // 样式缓存
    private GUIStyle _headerStyle;
    private GUIStyle _subHeaderStyle;
    private GUIStyle _itemLabelStyle;
    private GUIStyle _buttonStyle;

    public MaterialPresetDrawer()
    {
        Title = "创建材质预设";
        position = new Rect(Screen.width / 2 - 350, Screen.height / 2 - 400, 700, 600);
        Data = new PresetData();
    }

    public override void OnEnable()
    {
        base.OnEnable();

        // 初始化样式
        InitializeStyles();

        // 初始化材质列表
        InitializeMaterialsList();
    }

    // 初始化样式
    private void InitializeStyles()
    {
        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            margin = new RectOffset(0, 0, 6, 6)
        };

        _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            margin = new RectOffset(0, 0, 4, 4)
        };

        _itemLabelStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Normal,
            fontSize = 12
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            padding = new RectOffset(8, 8, 4, 4),
            margin = new RectOffset(2, 2, 2, 2)
        };
    }

    // 初始化材质列表
    private void InitializeMaterialsList()
    {
        // 确保数据存在
        if (Data == null)
        {
            Data = new PresetData();
        }

        // 创建可重排序列表
        _materialsList = new SimpleReorderableList(
            Data.materialItems,  // 列表数据源
            typeof(MaterialItem), // 列表元素类型
            EditorGUIUtility.singleLineHeight * 3 + 12);

        // 设置列表标题
        _materialsList.Title = "材质预设列表";
        _materialsList.AddButtonEnabled = false;
        _materialsList.RemoveButtonEnabled = false;

        // 设置列表元素绘制
        _materialsList.OnDraw = (rect, index, isActive, isFocused) =>
        {
            // 检查索引是否有效
            if (index < 0 || index >= Data.materialItems.Count)
                return EditorGUIUtility.singleLineHeight;

            var item = Data.materialItems[index];
            if (item == null)
                return EditorGUIUtility.singleLineHeight;

            // 调整矩形位置
            rect.y += 2;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 4f;
            
            // 左侧颜色条
            Color themeColor = item.themeColor;
            themeColor.a = 1f; // 确保不透明
            EditorGUI.DrawRect(new Rect(rect.x, rect.y - 2, 4, lineHeight * 3 + spacing * 2 + 4), themeColor);

            // 第一行 - 材质名称
            string materialName = item.material != null ? item.material.name : "未知材质";
            EditorGUI.LabelField(new Rect(rect.x + 8, rect.y, rect.width - 8, lineHeight), materialName, EditorStyles.boldLabel);

            // 第二行 - 预设名称
            rect.y += lineHeight + spacing;
            EditorGUI.LabelField(new Rect(rect.x + 10, rect.y, 80, lineHeight), "预设名称:", _itemLabelStyle);
            
            // 设置一个较好的默认名称
            if (item.presetName == "新预设" && item.material != null)
            {
                item.presetName = item.material.name + " 预设";
            }
            
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUI.TextField(new Rect(rect.x + 90, rect.y, rect.width - 180, lineHeight), item.presetName);
            if (EditorGUI.EndChangeCheck())
            {
                item.presetName = newName;
            }

            // 第三行 - 主题色和生成按钮
            rect.y += lineHeight + spacing;
            EditorGUI.LabelField(new Rect(rect.x + 10, rect.y, 80, lineHeight), "主题色:", _itemLabelStyle);
            
            // 主题色字段
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUI.ColorField(new Rect(rect.x + 90, rect.y, 120, lineHeight), GUIContent.none, item.themeColor, true, true, false);
            if (EditorGUI.EndChangeCheck())
            {
                item.themeColor = newColor;
            }
            
            // 自动提取按钮
            if (GUI.Button(new Rect(rect.x + 220, rect.y, 80, lineHeight), "从材质"))
            {
                if (item.material != null)
                {
                    item.themeColor = MaterialUtility.ExtractThemeColorFromMaterial(item.material);
                }
            }
            
            // 自动生成按钮
            if (GUI.Button(new Rect(rect.x + 305, rect.y, 80, lineHeight), "随机色"))
            {
                item.themeColor = MaterialUtility.GenerateRandomColor();
            }
            
            return (lineHeight * 3) + (spacing * 2) + 4;
        };
    }

    public override void OnDraw()
    {
        // 确保数据存在
        if (Data == null)
        {
            Data = new PresetData();
        }

        // 确保列表初始化
        if (_materialsList == null)
        {
            InitializeMaterialsList();
        }
        
        // 确保样式初始化
        if (_headerStyle == null)
        {
            InitializeStyles();
        }

        // 开始绘制
        EditorGUILayout.BeginVertical();

        // 标题区域
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("材质预设创建工具", _headerStyle);
        EditorGUILayout.Space(5);
        
        // 基本信息区域（使用Box提供视觉分组）
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 渲染器信息
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("渲染器信息", _subHeaderStyle);

        // 渲染器引用字段
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("渲染器");
            EditorGUILayout.ObjectField(Data.renderer, typeof(Renderer), true);
            EditorGUILayout.EndHorizontal();
        }
        
        // 渲染器路径
        if (Data.renderer != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("路径");
            EditorGUILayout.SelectableLabel(GetGameObjectPath(Data.renderer.gameObject), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();
        
        // 槽位选择区域
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("预设设置", _subHeaderStyle);

        // 槽位选择
        if (Data.originalMaterials != null && Data.originalMaterials.Length > 0)
        {
            // 创建槽位标签
            string[] slotLabels = new string[Data.originalMaterials.Length];
            for (int i = 0; i < slotLabels.Length; i++)
            {
                string materialName = Data.originalMaterials[i] != null ? Data.originalMaterials[i].name : "空";
                slotLabels[i] = $"槽位 {i}: {materialName}";
            }

            // 显示槽位选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("目标槽位");
            EditorGUI.BeginChangeCheck();
            Data.globalSlotIndex = EditorGUILayout.Popup(Data.globalSlotIndex, slotLabels, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            // 如果渲染器为空或没有材质，显示提示
            EditorGUILayout.HelpBox("渲染器为空或没有材质。将创建预设，但无法应用到渲染器。", MessageType.Warning);
            // 设置默认槽位索引为0
            Data.globalSlotIndex = 0;
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();

        // 材质列表区域
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("材质列表", _subHeaderStyle);

        if (Data.materialItems != null && Data.materialItems.Count > 0)
        {
            // 辅助操作按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("全部自动命名", _buttonStyle, GUILayout.Width(120)))
            {
                foreach (var item in Data.materialItems)
                {
                    if (item.material != null)
                    {
                        item.presetName = item.material.name + " 预设";
                    }
                }
            }
            
            if (GUILayout.Button("全部自动配色", _buttonStyle, GUILayout.Width(120)))
            {
                foreach (var item in Data.materialItems)
                {
                    if (item.material != null)
                    {
                        item.themeColor = MaterialUtility.ExtractThemeColorFromMaterial(item.material);
                    }
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 开始滚动视图 - 使用固定高度确保可以滚动
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, 
                false, true, GUILayout.ExpandHeight(true));

            // 绘制材质列表
            _materialsList.DoLayout();

            // 结束滚动视图
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("没有拖拽任何材质。请将材质拖拽到此处创建预设。", MessageType.Warning);
        }
    }
    
    // 获取游戏对象的层级路径
    private string GetGameObjectPath(GameObject obj)
    {
        if (obj == null) return "未知路径";
        
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
}
