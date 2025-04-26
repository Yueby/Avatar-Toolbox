using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using VRC.SDKBase;

// 组件仅在编辑器中使用，运行时不会执行任何方法
public class ItemModifyPreset : MonoBehaviour, IEditorOnly
{
    [Serializable]
    public class MaterialPreset
    {
        // 默认主题色 - 蓝色
        private static readonly Color DEFAULT_THEME_COLOR = new Color(0.3f, 0.6f, 0.9f, 1f);

        [SerializeField]
        private string _uuid;

        public string presetName;
        public Material[] materials;

        // 主题色数据
        public Color themeColor = DEFAULT_THEME_COLOR; // 默认蓝色

        public string UUID
        {
            get { return _uuid; }
        }

        // 默认构造函数，仅用于Unity序列化
        public MaterialPreset()
        {
            _uuid = Guid.NewGuid().ToString();
            presetName = "新预设";
            materials = new Material[0];
            themeColor = DEFAULT_THEME_COLOR;
        }

        // 从渲染器创建预设，获取所有材质
        public MaterialPreset(string name, Renderer renderer)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");

            _uuid = Guid.NewGuid().ToString();
            presetName = name;

            // 复制当前渲染器的所有材质
            materials = new Material[renderer.sharedMaterials.Length];
            Array.Copy(renderer.sharedMaterials, materials, renderer.sharedMaterials.Length);

            // 尝试从材质提取主题色，如果无法提取则使用默认色
            themeColor = ExtractThemeColorFromMaterials(materials);
        }

        // 从已有材质数组创建预设
        public MaterialPreset(string name, Material[] sourceMaterials)
        {
            _uuid = Guid.NewGuid().ToString();
            presetName = name;

            if (sourceMaterials != null)
            {
                materials = new Material[sourceMaterials.Length];
                Array.Copy(sourceMaterials, materials, sourceMaterials.Length);

                // 尝试从材质提取主题色
                themeColor = ExtractThemeColorFromMaterials(materials);
            }
            else
            {
                materials = new Material[0];
                themeColor = DEFAULT_THEME_COLOR;
            }
        }

        // 从材质中提取主题色（通常取第一个非黑非白材质的主色）
        private Color ExtractThemeColorFromMaterials(Material[] mats)
        {
            if (mats == null || mats.Length == 0 || mats[0] == null)
                return DEFAULT_THEME_COLOR;

            // 尝试从第一个有效材质中获取颜色
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null)
                {
                    // 检查材质是否有主颜色属性
                    if (mats[i].HasProperty("_Color"))
                    {
                        Color color = mats[i].color;
                        // 避免全黑或全白的颜色
                        if (!IsColorBlackOrWhite(color))
                            return color;
                    }
                }
            }

            // 如果没有找到合适的颜色，使用默认颜色
            return DEFAULT_THEME_COLOR;
        }

        // 判断颜色是否接近黑色或白色
        private bool IsColorBlackOrWhite(Color color)
        {
            float brightness = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return brightness < 0.2f || brightness > 0.8f;
        }
    }

    [Serializable]
    public class RendererPreset
    {
        [SerializeField]
        private string _uuid;

        public string rendererName;

        // 保留直接引用以兼容现有数据
        public Renderer renderer;

        // 新增：存储渲染器的相对路径
        [SerializeField]
        private string rendererPath;

        public List<MaterialPreset> materialPresets = new List<MaterialPreset>();
        public int currentPresetIndex = -1;

        public string UUID
        {
            get { return _uuid; }
        }

        // 获取渲染器路径
        public string RendererPath
        {
            get { return rendererPath; }
            set { rendererPath = value; }
        }

        // 创建渲染器预设，并添加默认的材质预设
        public RendererPreset(Renderer renderer)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");

            _uuid = Guid.NewGuid().ToString();
            this.renderer = renderer;
            rendererName = renderer.name;
            materialPresets = new List<MaterialPreset>();

            // 设置渲染器路径
            rendererPath = CalculateRendererPath(renderer);
        }

        // 计算渲染器相对于根对象的路径
        private string CalculateRendererPath(Renderer renderer)
        {
            if (renderer == null)
                return string.Empty;

            // 获取渲染器的完整路径
            List<string> pathParts = new List<string>();
            Transform current = renderer.transform;

            // 从渲染器向上遍历到根对象
            while (current != null)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            // 反转路径并用斜杠连接
            pathParts.Reverse();
            return string.Join("/", pathParts.ToArray());
        }

        // 根据路径查找渲染器
        public Renderer FindRendererByPath(GameObject rootObject)
        {
            if (rootObject == null || string.IsNullOrEmpty(rendererPath))
                return null;

            // 如果路径为空但直接引用存在，返回直接引用
            if (string.IsNullOrEmpty(rendererPath) && renderer != null)
                return renderer;

            // 分割路径
            string[] pathParts = rendererPath.Split('/');

            // 如果路径只有一个部分，直接在根对象上查找
            if (pathParts.Length == 1)
            {
                return rootObject.GetComponent<Renderer>();
            }

            // 查找路径对应的对象
            Transform current = rootObject.transform;

            // 从第二个部分开始查找（第一个是根对象自己）
            for (int i = 1; i < pathParts.Length; i++)
            {
                // 在当前层级查找子对象
                Transform child = current.Find(pathParts[i]);
                if (child == null)
                {
                    Debug.LogWarning($"无法找到路径 {rendererPath} 的第 {i} 部分: {pathParts[i]}");
                    return null;
                }

                current = child;
            }

            // 在找到的对象上获取渲染器组件
            Renderer foundRenderer = current.GetComponent<Renderer>();
            if (foundRenderer == null)
            {
                Debug.LogWarning($"在路径 {rendererPath} 找到的对象上没有渲染器组件");
            }

            return foundRenderer;
        }

        // 更新渲染器引用和路径
        public void UpdateRenderer(Renderer newRenderer)
        {
            if (newRenderer == null)
                return;

            renderer = newRenderer;
            rendererName = newRenderer.name;
            rendererPath = CalculateRendererPath(newRenderer);
        }

        // 通过UUID查找材质预设
        public MaterialPreset FindMaterialPreset(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
                return null;

            for (int i = 0; i < materialPresets.Count; i++)
            {
                if (materialPresets[i] != null && materialPresets[i].UUID == uuid)
                {
                    return materialPresets[i];
                }
            }

            return null;
        }
    }

    [Serializable]
    public class PresetGroupEntry
    {
        [SerializeField]
        private string _rendererPresetUuid;

        [SerializeField]
        private string _materialPresetUuid;

        // 引用缓存，非序列化
        [NonSerialized]
        private RendererPreset _rendererPreset;

        [NonSerialized]
        private MaterialPreset _materialPreset;

        public string RendererPresetUuid { get { return _rendererPresetUuid; } }
        public string MaterialPresetUuid { get { return _materialPresetUuid; } }

        public RendererPreset RendererPreset
        {
            get { return _rendererPreset; }
            set
            {
                _rendererPreset = value;
                _rendererPresetUuid = value != null ? value.UUID : null;
            }
        }

        public MaterialPreset MaterialPreset
        {
            get { return _materialPreset; }
            set
            {
                _materialPreset = value;
                _materialPresetUuid = value != null ? value.UUID : null;
            }
        }

        public PresetGroupEntry() { }

        public PresetGroupEntry(RendererPreset rendererPreset, MaterialPreset materialPreset)
        {
            RendererPreset = rendererPreset;
            MaterialPreset = materialPreset;
        }
    }

    [Serializable]
    public class PresetGroup
    {
        [SerializeField]
        private string _uuid;

        public string groupName;
        public List<PresetGroupEntry> entries = new List<PresetGroupEntry>();

        // 添加主题色属性
        public Color themeColor = new Color(0.4f, 0.7f, 0.3f, 1f); // 默认绿色，与MaterialPreset区分

        public string UUID { get { return _uuid; } }

        public PresetGroup()
        {
            _uuid = Guid.NewGuid().ToString();
            groupName = "新预设组";
        }

        public PresetGroup(string name)
        {
            _uuid = Guid.NewGuid().ToString();
            groupName = name;
        }

        // 解析UUID引用，将字符串UUID转换为对象引用
        public void ResolveReferences(ItemModifyPreset owner)
        {
            if (owner == null) return;

            foreach (var entry in entries)
            {
                // 查找并设置渲染器预设引用
                RendererPreset rendererPreset = owner.FindRendererPreset(entry.RendererPresetUuid);
                if (rendererPreset != null)
                {
                    entry.RendererPreset = rendererPreset;

                    // 查找并设置材质预设引用
                    MaterialPreset materialPreset = rendererPreset.FindMaterialPreset(entry.MaterialPresetUuid);
                    if (materialPreset != null)
                    {
                        entry.MaterialPreset = materialPreset;
                    }
                }
            }
        }
    }

    public List<RendererPreset> rendererPresets = new List<RendererPreset>();
    public List<PresetGroup> presetGroups = new List<PresetGroup>();

    // 查找渲染器预设
    public RendererPreset FindRendererPreset(string uuid)
    {
        if (string.IsNullOrEmpty(uuid))
            return null;

        foreach (var preset in rendererPresets)
        {
            if (preset != null && preset.UUID == uuid)
            {
                return preset;
            }
        }

        return null;
    }

    // 查找预设分组
    public PresetGroup FindPresetGroup(string uuid)
    {
        if (string.IsNullOrEmpty(uuid))
            return null;

        foreach (var group in presetGroups)
        {
            if (group != null && group.UUID == uuid)
            {
                return group;
            }
        }

        return null;
    }

    // 应用指定预设组的所有设置
    public void ApplyPresetGroup(string groupUuid)
    {
        if (string.IsNullOrEmpty(groupUuid))
        {
            Debug.LogError("尝试应用空UUID的预设组");
            return;
        }

        PresetGroup group = FindPresetGroup(groupUuid);
        if (group == null)
        {
            Debug.LogError($"找不到UUID为 {groupUuid} 的预设组");
            return;
        }

        ApplyPresetGroup(group);
    }

    // 应用指定预设组的所有设置
    public void ApplyPresetGroup(PresetGroup group)
    {
        if (group == null)
        {
            Debug.LogError("尝试应用空预设组");
            return;
        }

        int appliedCount = 0;
        // 应用每个条目的预设
        foreach (var entry in group.entries)
        {
            if (entry.RendererPreset == null)
            {
                Debug.LogWarning($"渲染器预设为空 UUID: {entry.RendererPresetUuid}");
                continue;
            }

            // 获取渲染器 - 优先使用路径查找
            Renderer targetRenderer = null;

            // 尝试通过路径查找渲染器
            if (!string.IsNullOrEmpty(entry.RendererPreset.RendererPath))
            {
                targetRenderer = entry.RendererPreset.FindRendererByPath(this.gameObject);
            }

            // 如果路径查找失败，尝试使用直接引用
            if (targetRenderer == null)
            {
                targetRenderer = entry.RendererPreset.renderer;
            }

            // 如果仍然找不到渲染器，记录警告并继续下一个条目
            if (targetRenderer == null)
            {
                Debug.LogWarning($"无法找到渲染器 (预设: {entry.RendererPreset.rendererName}, 路径: {entry.RendererPreset.RendererPath})");
                continue;
            }

            if (entry.MaterialPreset == null)
            {
                Debug.LogWarning($"材质预设为空 UUID: {entry.MaterialPresetUuid} (渲染器: {entry.RendererPreset.rendererName})");
                continue;
            }

            // 应用材质预设到找到的渲染器
            ApplyMaterialsToRenderer(targetRenderer, entry.MaterialPreset);

            // 通过UUID匹配更新当前预设索引
            int foundIndex = -1;
            for (int i = 0; i < entry.RendererPreset.materialPresets.Count; i++)
            {
                if (entry.RendererPreset.materialPresets[i] != null &&
                    entry.RendererPreset.materialPresets[i].UUID == entry.MaterialPreset.UUID)
                {
                    foundIndex = i;
                    entry.RendererPreset.currentPresetIndex = i;
                    break;
                }
            }

            if (foundIndex >= 0)
            {
                appliedCount++;
            }
            else
            {
                Debug.LogWarning($"在渲染器预设中找不到匹配的材质预设 (渲染器: {entry.RendererPreset.rendererName}, 材质预设: {entry.MaterialPreset.presetName})");
            }
        }
    }

    // 将材质应用到渲染器
    private void ApplyMaterialsToRenderer(Renderer renderer, MaterialPreset preset)
    {
        if (renderer == null)
        {
            Debug.LogError("尝试应用材质到空渲染器");
            return;
        }

        if (preset == null)
        {
            Debug.LogError("尝试应用空材质预设");
            return;
        }

        if (preset.materials == null || preset.materials.Length == 0)
        {
            Debug.LogWarning($"材质预设 '{preset.presetName}' 不包含任何材质");
            return;
        }

        // 检查渲染器材质
        if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
        {
            Debug.LogWarning($"渲染器 '{renderer.name}' 没有材质槽位");
            return;
        }

        // 计算要应用的材质数量
        int materialCount = Mathf.Min(renderer.sharedMaterials.Length, preset.materials.Length);
        Material[] materials = new Material[renderer.sharedMaterials.Length];

        // 复制原有材质
        Array.Copy(renderer.sharedMaterials, materials, renderer.sharedMaterials.Length);

        // 应用预设中的材质
        int appliedCount = 0;
        for (int i = 0; i < materialCount; i++)
        {
            if (preset.materials[i] != null)
            {
                materials[i] = preset.materials[i];
                appliedCount++;
            }
        }

        // 设置材质
        renderer.sharedMaterials = materials;
    }

    // 解析所有预设组的引用
    public void ResolveAllReferences()
    {
        int resolvedGroupCount = 0;

        foreach (var group in presetGroups)
        {
            if (group == null) continue;
            ResolveGroupReferences(group);
            resolvedGroupCount++;
        }

    }

    // 解析单个预设组的引用
    private void ResolveGroupReferences(PresetGroup group)
    {
        if (group == null) return;
        int resolvedEntryCount = 0;

        foreach (var entry in group.entries)
        {
            if (entry == null) continue;

            bool resolved = false;

            // 查找并设置渲染器预设引用
            RendererPreset rendererPreset = FindRendererPreset(entry.RendererPresetUuid);
            if (rendererPreset != null)
            {
                entry.RendererPreset = rendererPreset;

                // 查找并设置材质预设引用
                MaterialPreset materialPreset = rendererPreset.FindMaterialPreset(entry.MaterialPresetUuid);
                if (materialPreset != null)
                {
                    entry.MaterialPreset = materialPreset;
                    resolved = true;
                    resolvedEntryCount++;
                }
                else
                {
                    Debug.LogWarning($"预设组 '{group.groupName}' 中找不到材质预设 (UUID: {entry.MaterialPresetUuid})");
                }
            }
            else
            {
                Debug.LogWarning($"预设组 '{group.groupName}' 中找不到渲染器预设 (UUID: {entry.RendererPresetUuid})");
            }

            if (!resolved)
            {
                Debug.LogWarning($"预设组 '{group.groupName}' 中的条目引用解析失败: 渲染器UUID={entry.RendererPresetUuid}, 材质UUID={entry.MaterialPresetUuid}");
            }
        }
    }
}
