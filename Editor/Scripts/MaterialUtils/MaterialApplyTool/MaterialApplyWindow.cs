using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;
using Yueby.Utils;

[System.Serializable]
public class MaterialKeywordPair
{
    public Material material;
    public string keyword = "";
}

public class MaterialApplyWindow : EditorWindow
{
    private GameObject targetGameObject;
    private List<MaterialKeywordPair> materialKeywordPairs = new List<MaterialKeywordPair>();
    private ReorderableList reorderableList;
    private Vector2 scrollPosition;

    // Drag and drop handler
    private DragDropHandler dragDropHandler;

    // Add menu item
    [MenuItem("GameObject/YuebyTools/Material Replacement Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<MaterialApplyWindow>("Material Replacement");
        window.minSize = new Vector2(300, 150);
        window.Show();
    }

    private void OnEnable()
    {
        // 自动获取当前选中的游戏对象
        if (Selection.activeGameObject != null)
        {
            targetGameObject = Selection.activeGameObject;
        }

        // 初始化 ReorderableList
        InitializeReorderableList();

        // 初始化拖放处理器
        InitializeDragDropHandler();
    }

    private void InitializeDragDropHandler()
    {
        // 创建拖放处理器
        dragDropHandler = new DragDropHandler();

        // 设置接受的对象类型
        dragDropHandler.AcceptedTypes = new System.Type[] { typeof(Material) };

        // 设置拖放区域的视觉效果
        dragDropHandler.ShowVisualFeedback = true;
        dragDropHandler.ShowOnlyWhenDragging = true;
        dragDropHandler.HighlightColor = new Color(0.0f, 0.5f, 1.0f, 0.8f);

        // 设置拖放区域上层绘制回调
        dragDropHandler.OnDrawOverlay = (rect, state, isAccepted) =>
        {
            if (state != DragDropState.None)
            {
                string message = isAccepted ? "Drop Materials Here" : "Cannot Drop Here";
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = isAccepted ? Color.white : Color.red;

                GUI.Label(rect, message, style);
            }
        };

        // 设置对象放下事件
        dragDropHandler.OnObjectsDropped = (objects) =>
        {
            if (objects != null && objects.Length > 0)
            {
                // 处理拖入的材质
                HandleDroppedMaterials(objects);

                // 重绘窗口
                Repaint();
            }
        };
    }

    private void InitializeReorderableList()
    {
        reorderableList = new ReorderableList(materialKeywordPairs, typeof(MaterialKeywordPair), true, true, true, true);

        // Set list header
        reorderableList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Material and Keyword Pairs");
        };

        // 设置列表项绘制
        reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            var element = materialKeywordPairs[index];
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            // 计算各个字段的宽度和位置
            float materialWidth = rect.width * 0.6f;
            float keywordWidth = rect.width * 0.4f - 5;

            // 绘制材质字段
            Rect materialRect = new Rect(rect.x, rect.y, materialWidth, rect.height);

            // 检测材质是否发生变化
            EditorGUI.BeginChangeCheck();
            Material newMaterial = (Material)EditorGUI.ObjectField(materialRect, element.material, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
            {
                element.material = newMaterial;

                // 如果材质不为空且关键字为空，自动提取关键字
                if (newMaterial != null && string.IsNullOrEmpty(element.keyword))
                {
                    element.keyword = ExtractKeywordFromMaterialName(newMaterial.name);
                }
            }

            // 绘制关键字字段
            Rect keywordRect = new Rect(rect.x + materialWidth + 5, rect.y, keywordWidth, rect.height);
            element.keyword = EditorGUI.TextField(keywordRect, element.keyword);
        };

        // 设置列表项高度
        reorderableList.elementHeightCallback = (int index) =>
        {
            return EditorGUIUtility.singleLineHeight + 4;
        };

        // 设置添加按钮回调
        reorderableList.onAddCallback = (ReorderableList list) =>
        {
            materialKeywordPairs.Add(new MaterialKeywordPair());
        };
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Material Replacement Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Target object selection
        EditorGUI.BeginChangeCheck();
        targetGameObject = EditorGUILayout.ObjectField("Target Object", targetGameObject, typeof(GameObject), true) as GameObject;
        if (EditorGUI.EndChangeCheck())
        {
            // Additional logic when target object changes
        }

        EditorGUILayout.Space(5);

        // Draw material keyword list
        GUILayout.Label("Material List", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        reorderableList.DoLayoutList();

        EditorGUILayout.EndScrollView();
        // 处理拖放
        Rect dropRect = GUILayoutUtility.GetLastRect();
        dragDropHandler.DrawDragAndDrop(dropRect);
        EditorGUILayout.Space(5);

        // Apply button
        bool hasValidMaterials = false;
        foreach (var pair in materialKeywordPairs)
        {
            if (pair.material != null)
            {
                hasValidMaterials = true;
                break;
            }
        }

        GUI.enabled = targetGameObject != null && hasValidMaterials;
        if (GUILayout.Button("Replace Materials"))
        {
            ApplyMaterial();
        }
        GUI.enabled = true;

        // Display info messages
        if (targetGameObject == null)
        {
            EditorGUILayout.HelpBox("Please select a target object", MessageType.Info);
        }
        else if (!hasValidMaterials)
        {
            EditorGUILayout.HelpBox("Please add at least one material", MessageType.Info);
        }
    }

    /// <summary>
    /// 处理拖入的材质对象
    /// </summary>
    private void HandleDroppedMaterials(UnityEngine.Object[] objects)
    {
        if (objects == null || objects.Length == 0)
            return;

        // 处理每个拖入的对象
        foreach (var obj in objects)
        {
            // 确保对象是材质
            Material material = obj as Material;
            if (material == null)
                continue;

            // 创建新的材质关键字对
            MaterialKeywordPair newPair = new MaterialKeywordPair
            {
                material = material,
                keyword = ExtractKeywordFromMaterialName(material.name)
            };

            // 添加到列表
            materialKeywordPairs.Add(newPair);
        }
    }

    /// <summary>
    /// Extract keyword from material name
    /// </summary>
    /// <param name="materialName">Material name</param>
    /// <returns>Extracted keyword</returns>
    private string ExtractKeywordFromMaterialName(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
            return "";

        // Define common separators
        char[] separators = new char[] { '_', '-', '.', ' ', '(', ')', '[', ']' };

        // Try to split the name using separators
        string[] parts = materialName.Split(separators, System.StringSplitOptions.RemoveEmptyEntries);

        // If there are multiple parts, return the first part
        if (parts.Length > 0)
        {
            return parts[0];
        }

        // If splitting fails, return the original name
        return materialName;
    }

    private void ApplyMaterial()
    {
        if (targetGameObject == null)
            return;

        // Check if there are valid materials
        bool hasValidMaterials = false;
        foreach (var pair in materialKeywordPairs)
        {
            if (pair.material != null)
            {
                hasValidMaterials = true;
                break;
            }
        }

        if (!hasValidMaterials)
            return;

        // Get all renderers
        Renderer[] renderers = targetGameObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("No renderers found in target object or its children");
            return;
        }

        int replacedCount = 0;
        var keywordReplaceCounts = new Dictionary<string, int>();

        // 遍历所有渲染器
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool materialChanged = false;

            // 遍历渲染器的所有材质
            for (int i = 0; i < materials.Length; i++)
            {
                // 如果材质为空，跳过
                if (materials[i] == null)
                    continue;

                // 遍历所有材质关键字对
                foreach (var pair in materialKeywordPairs)
                {
                    if (pair.material == null)
                        continue;

                    // 检查材质名称是否包含关键字
                    if (string.IsNullOrEmpty(pair.keyword) || materials[i].name.Contains(pair.keyword))
                    {
                        materials[i] = pair.material;
                        materialChanged = true;
                        replacedCount++;

                        // 记录每个关键字的替换数量
                        string key = string.IsNullOrEmpty(pair.keyword) ? "No Keyword" : pair.keyword;
                        if (!keywordReplaceCounts.ContainsKey(key))
                            keywordReplaceCounts[key] = 0;
                        keywordReplaceCounts[key]++;

                        // 找到匹配后就跳出当前材质的循环
                        break;
                    }
                }
            }

            // 如果有材质被替换，更新渲染器的材质
            if (materialChanged)
            {
                Undo.RecordObject(renderer, "Replace Material");
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }

        if (replacedCount > 0)
        {
            string detailMessage = "Replacement Details:\n";
            foreach (var kvp in keywordReplaceCounts)
            {
                detailMessage += $"Keyword '{kvp.Key}': {kvp.Value} materials\n";
            }
            Debug.Log($"Replaced {replacedCount} materials\n{detailMessage}");
        }
        else
        {
            Debug.LogWarning("No matching materials found");
        }
    }
}
