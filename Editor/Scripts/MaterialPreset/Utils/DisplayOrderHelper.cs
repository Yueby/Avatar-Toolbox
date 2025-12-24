using System.Collections.Generic;
using System.Linq;
using Yueby.Tools.AvatarToolbox.MaterialPreset;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Utils
{
    public static class DisplayOrderHelper
    {
        public static List<PresetGroup> GetAllGroupsOrderedByDisplay(MaterialPresetManager manager)
        {
            if (manager == null) return new List<PresetGroup>();
            
            var result = new List<PresetGroup>();
            
            var sortedRootItems = manager.RootItemsList.OrderBy(item => item.DisplayOrder).ToList();
            
            foreach (var item in sortedRootItems)
            {
                if (item is PresetGroup group)
                {
                    result.Add(group);
                }
                else if (item is PresetFolder folder)
                {
                    result.AddRange(GetGroupsFromFolderOrdered(folder));
                }
            }
            
            return result;
        }
        
        public static List<PresetGroup> GetGroupsFromFolderOrdered(PresetFolder folder)
        {
            var result = new List<PresetGroup>();
            
            var sortedChildren = folder.Children.OrderBy(item => item.DisplayOrder).ToList();
            
            foreach (var child in sortedChildren)
            {
                if (child is PresetGroup group)
                {
                    result.Add(group);
                }
                else if (child is PresetFolder subFolder)
                {
                    result.AddRange(GetGroupsFromFolderOrdered(subFolder));
                }
            }
            
            return result;
        }
    }
}

