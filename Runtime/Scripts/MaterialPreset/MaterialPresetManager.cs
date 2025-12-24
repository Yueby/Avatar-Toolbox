using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDKBase;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset
{
    [AddComponentMenu("Yueby/Avatar Toolbox/Material Preset Manager")]
    public class MaterialPresetManager : MonoBehaviour, IEditorOnly
    {
        // 统一树形结构（使用 SerializeReference 支持多态序列化）
        [SerializeReference]
        private List<IPresetItem> _rootItems = new List<IPresetItem>();
        
        // 统一访问接口（按DisplayOrder排序）
        public IEnumerable<IPresetItem> RootItems => _rootItems.OrderBy(item => item.DisplayOrder);
        
        // 直接访问内部列表（用于添加/删除操作）
        public List<IPresetItem> RootItemsList => _rootItems;
        
        // 筛选访问器（从统一结构中筛选特定类型）
        public List<PresetGroup> RootGroups => _rootItems.OfType<PresetGroup>().ToList();
        public List<PresetFolder> RootFolders => _rootItems.OfType<PresetFolder>().ToList();
        
        // 获取所有PresetGroup（包括嵌套在文件夹中的）
        public List<PresetGroup> GetAllGroups()
        {
            var result = new List<PresetGroup>();
            foreach (var item in _rootItems)
            {
                if (item is PresetGroup group)
                    result.Add(group);
                else if (item is PresetFolder folder)
                    result.AddRange(folder.GetAllGroupsRecursive());
            }
            return result;
        }

#if UNITY_EDITOR
        // Runtime/Editor only, not serialized
        // We use a custom class for tracking previews. 
        // To survive assembly reloads, we rely on finding objects by naming convention in EditorWindow OnEnable.
        public List<object> ActivePreviews = new List<object>();
        
        // 数据迁移方法（保留接口以避免旧代码报错）
        public void MigrateToTreeStructure()
        {
            // 迁移已完成，此方法保留为空以保持向后兼容
        }
#endif
    }

    [Serializable]
    public class PresetGroup : IPresetItem
    {
        [SerializeField]
        private string _uuid;
        
        [SerializeField]
        public string GroupName = "New Group";

        [SerializeField]
        public Texture2D Thumbnail;

        [SerializeField]
        public List<RendererConfig> RendererConfigs = new List<RendererConfig>();

        [SerializeField]
        private int _displayOrder = 0;

        public string UUID => _uuid;
        
        // IPresetItem interface implementation
        string IPresetItem.Name { get => GroupName; set => GroupName = value; }
        Texture2D IPresetItem.Thumbnail { get => Thumbnail; set => Thumbnail = value; }
        public PresetItemType ItemType => PresetItemType.Group;
        public int DisplayOrder { get => _displayOrder; set => _displayOrder = value; }

        public PresetGroup()
        {
            _uuid = Guid.NewGuid().ToString();
        }
    }

    [Serializable]
    public class RendererConfig
    {
        [SerializeField]
        public string RendererPath;

        // Editor helper, not strictly relied upon at runtime if path works, but good for drag-drop
        [SerializeField]
        public SkinnedMeshRenderer TargetRenderer; 

        [SerializeField]
        public List<MaterialSlotConfig> MaterialSlots = new List<MaterialSlotConfig>();
        
        [SerializeField]
        public MatchMode MatchMode = MatchMode.NameSimilarity;
    }

    [Serializable]
    public class MaterialSlotConfig
    {
        [SerializeField]
        public int SlotIndex;
        
        [SerializeField]
        public string SlotName; // For UI display

        [SerializeField]
        public Material MaterialRef;

        [SerializeField]
        public string FuzzyName; // For backup or fuzzy matching
    }

    public enum MatchMode
    {
        NameSimilarity = 0,
        SlotIndex = 1,
        NameThenIndex = 2
    }
}

