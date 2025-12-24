using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset
{
    [Serializable]
    public class PresetFolder : IPresetItem
    {
        [SerializeField] private string _uuid;
        [SerializeField] private string _name = "New Folder";
        [SerializeField] private Texture2D _thumbnail;
        [SerializeField] private int _displayOrder = 0;
        
        // 统一树形结构
        [SerializeReference]
        private List<IPresetItem> _children = new List<IPresetItem>();
        
        public string UUID => _uuid;
        public string Name { get => _name; set => _name = value; }
        public Texture2D Thumbnail { get => _thumbnail; set => _thumbnail = value; }
        public PresetItemType ItemType => PresetItemType.Folder;
        public int DisplayOrder { get => _displayOrder; set => _displayOrder = value; }
        
        // 从统一结构中筛选（用于特定操作）
        public List<PresetGroup> Groups => _children.OfType<PresetGroup>().ToList();
        public List<PresetFolder> Folders => _children.OfType<PresetFolder>().ToList();
        
        // 直接访问内部列表（用于添加/删除操作）
        public List<IPresetItem> Children => _children;
        
        public PresetFolder()
        {
            _uuid = Guid.NewGuid().ToString();
        }
        
        // 辅助方法：获取所有子项（统一访问，按DisplayOrder排序）
        public IEnumerable<IPresetItem> GetAllChildren()
        {
            return _children.OrderBy(item => item.DisplayOrder);
        }
        
        // 递归获取所有 Group（用于批量预览）
        public List<PresetGroup> GetAllGroupsRecursive()
        {
            var result = new List<PresetGroup>();
            foreach (var child in _children)
            {
                if (child is PresetGroup group)
                    result.Add(group);
                else if (child is PresetFolder folder)
                    result.AddRange(folder.GetAllGroupsRecursive());
            }
            return result;
        }
        
        // 辅助方法：计算子项总数
        public int GetChildrenCount()
        {
            return _children.Count;
        }
    }
}

