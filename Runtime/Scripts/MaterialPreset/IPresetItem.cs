using System;
using UnityEngine;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset
{
    public interface IPresetItem
    {
        string UUID { get; }
        string Name { get; set; }
        Texture2D Thumbnail { get; set; }
        PresetItemType ItemType { get; }
        int DisplayOrder { get; set; } // 用于在容器内混合排序
    }

    public enum PresetItemType
    {
        Group,
        Folder
    }
}

