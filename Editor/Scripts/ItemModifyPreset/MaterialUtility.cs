using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Yueby.Utils
{
    /// <summary>
    /// 材质工具类，提供材质相关的实用函数
    /// </summary>
    public static class MaterialUtility
    {
        /// <summary>
        /// 颜色名称字典，用于从材质名称中提取颜色
        /// </summary>
        public static readonly Dictionary<string, Color> ColorNameMap = new Dictionary<string, Color>()
        {
            // 基本颜色
            {"red", new Color(0.9f, 0.2f, 0.2f)},
            {"red1", new Color(1.0f, 0.1f, 0.1f)},
            {"red2", new Color(0.8f, 0.2f, 0.2f)},
            {"red3", new Color(0.7f, 0.3f, 0.3f)},
            
            {"green", new Color(0.2f, 0.8f, 0.2f)},
            {"green1", new Color(0.1f, 0.9f, 0.1f)},
            {"green2", new Color(0.3f, 0.7f, 0.3f)},
            {"green3", new Color(0.4f, 0.6f, 0.4f)},
            
            {"blue", new Color(0.2f, 0.4f, 0.9f)},
            {"blue1", new Color(0.1f, 0.3f, 1.0f)},
            {"blue2", new Color(0.3f, 0.5f, 0.8f)},
            {"blue3", new Color(0.4f, 0.6f, 0.7f)},
            
            {"yellow", new Color(0.9f, 0.9f, 0.2f)},
            {"yellow1", new Color(1.0f, 1.0f, 0.0f)},
            {"yellow2", new Color(0.8f, 0.8f, 0.3f)},
            {"yellow3", new Color(0.7f, 0.7f, 0.4f)},
            
            {"orange", new Color(0.9f, 0.6f, 0.2f)},
            {"orange1", new Color(1.0f, 0.5f, 0.0f)},
            {"orange2", new Color(0.8f, 0.5f, 0.3f)},
            {"orange3", new Color(0.7f, 0.4f, 0.2f)},
            
            {"purple", new Color(0.6f, 0.2f, 0.8f)},
            {"purple1", new Color(0.7f, 0.1f, 0.9f)},
            {"purple2", new Color(0.5f, 0.3f, 0.7f)},
            {"purple3", new Color(0.4f, 0.2f, 0.6f)},
            
            {"pink", new Color(0.9f, 0.4f, 0.8f)},
            {"pink1", new Color(1.0f, 0.3f, 0.7f)},
            {"pink2", new Color(0.8f, 0.5f, 0.7f)},
            {"pink3", new Color(0.7f, 0.5f, 0.6f)},
            
            {"brown", new Color(0.6f, 0.4f, 0.2f)},
            {"brown1", new Color(0.7f, 0.3f, 0.1f)},
            {"brown2", new Color(0.5f, 0.3f, 0.2f)},
            {"brown3", new Color(0.4f, 0.3f, 0.25f)},
            
            {"black", new Color(0.2f, 0.2f, 0.2f)},
            {"black1", new Color(0.1f, 0.1f, 0.1f)},
            {"black2", new Color(0.15f, 0.15f, 0.15f)},
            {"black3", new Color(0.25f, 0.25f, 0.25f)},
            
            {"white", new Color(0.9f, 0.9f, 0.9f)},
            {"white1", new Color(1.0f, 1.0f, 1.0f)},
            {"white2", new Color(0.95f, 0.95f, 0.95f)},
            {"white3", new Color(0.85f, 0.85f, 0.85f)},
            
            {"grey", new Color(0.5f, 0.5f, 0.5f)},
            {"grey1", new Color(0.6f, 0.6f, 0.6f)},
            {"grey2", new Color(0.5f, 0.5f, 0.5f)},
            {"grey3", new Color(0.4f, 0.4f, 0.4f)},
            
            {"gray", new Color(0.5f, 0.5f, 0.5f)},
            {"gray1", new Color(0.6f, 0.6f, 0.6f)},
            {"gray2", new Color(0.5f, 0.5f, 0.5f)},
            {"gray3", new Color(0.4f, 0.4f, 0.4f)},

            // 深色调
            {"darkred", new Color(0.6f, 0.1f, 0.1f)},
            {"darkred1", new Color(0.5f, 0.05f, 0.05f)},
            {"darkred2", new Color(0.45f, 0.15f, 0.15f)},
            {"darkred3", new Color(0.55f, 0.2f, 0.2f)},
            
            {"darkgreen", new Color(0.1f, 0.5f, 0.1f)},
            {"darkgreen1", new Color(0.05f, 0.6f, 0.05f)},
            {"darkgreen2", new Color(0.15f, 0.4f, 0.15f)},
            {"darkgreen3", new Color(0.2f, 0.3f, 0.2f)},
            
            {"darkblue", new Color(0.1f, 0.2f, 0.6f)},
            {"darkblue1", new Color(0.05f, 0.1f, 0.7f)},
            {"darkblue2", new Color(0.15f, 0.25f, 0.5f)},
            {"darkblue3", new Color(0.2f, 0.3f, 0.4f)},
            
            {"darkyellow", new Color(0.6f, 0.6f, 0.1f)},
            {"darkyellow1", new Color(0.7f, 0.7f, 0.0f)},
            {"darkyellow2", new Color(0.5f, 0.5f, 0.15f)},
            {"darkyellow3", new Color(0.4f, 0.4f, 0.2f)},
            
            {"darkorange", new Color(0.7f, 0.4f, 0.1f)},
            {"darkorange1", new Color(0.8f, 0.3f, 0.0f)},
            {"darkorange2", new Color(0.6f, 0.35f, 0.15f)},
            {"darkorange3", new Color(0.5f, 0.3f, 0.2f)},
            
            {"darkpurple", new Color(0.4f, 0.1f, 0.5f)},
            {"darkpurple1", new Color(0.45f, 0.05f, 0.6f)},
            {"darkpurple2", new Color(0.35f, 0.15f, 0.4f)},
            {"darkpurple3", new Color(0.3f, 0.2f, 0.35f)},
            
            {"darkpink", new Color(0.7f, 0.3f, 0.5f)},
            {"darkpink1", new Color(0.8f, 0.2f, 0.4f)},
            {"darkpink2", new Color(0.6f, 0.35f, 0.45f)},
            {"darkpink3", new Color(0.5f, 0.3f, 0.4f)},
            
            {"darkbrown", new Color(0.4f, 0.3f, 0.1f)},
            {"darkbrown1", new Color(0.45f, 0.25f, 0.05f)},
            {"darkbrown2", new Color(0.35f, 0.25f, 0.15f)},
            {"darkbrown3", new Color(0.3f, 0.2f, 0.15f)},
            
            {"darkgrey", new Color(0.3f, 0.3f, 0.3f)},
            {"darkgrey1", new Color(0.25f, 0.25f, 0.25f)},
            {"darkgrey2", new Color(0.3f, 0.3f, 0.3f)},
            {"darkgrey3", new Color(0.35f, 0.35f, 0.35f)},
            
            {"darkgray", new Color(0.3f, 0.3f, 0.3f)},
            {"darkgray1", new Color(0.25f, 0.25f, 0.25f)},
            {"darkgray2", new Color(0.3f, 0.3f, 0.3f)},
            {"darkgray3", new Color(0.35f, 0.35f, 0.35f)},

            // 浅色调
            {"lightred", new Color(1.0f, 0.5f, 0.5f)},
            {"lightred1", new Color(1.0f, 0.4f, 0.4f)},
            {"lightred2", new Color(0.95f, 0.6f, 0.6f)},
            {"lightred3", new Color(0.9f, 0.7f, 0.7f)},
            
            {"lightgreen", new Color(0.5f, 1.0f, 0.5f)},
            {"lightgreen1", new Color(0.4f, 1.0f, 0.4f)},
            {"lightgreen2", new Color(0.6f, 0.9f, 0.6f)},
            {"lightgreen3", new Color(0.7f, 0.8f, 0.7f)},
            
            {"lightblue", new Color(0.5f, 0.7f, 1.0f)},
            {"lightblue1", new Color(0.4f, 0.6f, 1.0f)},
            {"lightblue2", new Color(0.6f, 0.8f, 0.95f)},
            {"lightblue3", new Color(0.7f, 0.85f, 0.9f)},
            
            {"lightyellow", new Color(1.0f, 1.0f, 0.5f)},
            {"lightyellow1", new Color(1.0f, 1.0f, 0.4f)},
            {"lightyellow2", new Color(0.95f, 0.95f, 0.6f)},
            {"lightyellow3", new Color(0.9f, 0.9f, 0.7f)},
            
            {"lightorange", new Color(1.0f, 0.8f, 0.5f)},
            {"lightorange1", new Color(1.0f, 0.7f, 0.4f)},
            {"lightorange2", new Color(0.95f, 0.8f, 0.6f)},
            {"lightorange3", new Color(0.9f, 0.85f, 0.7f)},
            
            {"lightpurple", new Color(0.8f, 0.5f, 1.0f)},
            {"lightpurple1", new Color(0.7f, 0.4f, 1.0f)},
            {"lightpurple2", new Color(0.85f, 0.6f, 0.95f)},
            {"lightpurple3", new Color(0.9f, 0.7f, 0.9f)},
            
            {"lightpink", new Color(1.0f, 0.7f, 0.9f)},
            {"lightpink1", new Color(1.0f, 0.6f, 0.8f)},
            {"lightpink2", new Color(0.95f, 0.75f, 0.85f)},
            {"lightpink3", new Color(0.9f, 0.8f, 0.85f)},
            
            {"lightbrown", new Color(0.8f, 0.6f, 0.4f)},
            {"lightbrown1", new Color(0.85f, 0.55f, 0.35f)},
            {"lightbrown2", new Color(0.75f, 0.65f, 0.45f)},
            {"lightbrown3", new Color(0.7f, 0.6f, 0.5f)},
            
            {"lightgrey", new Color(0.7f, 0.7f, 0.7f)},
            {"lightgrey1", new Color(0.75f, 0.75f, 0.75f)},
            {"lightgrey2", new Color(0.7f, 0.7f, 0.7f)},
            {"lightgrey3", new Color(0.65f, 0.65f, 0.65f)},
            
            {"lightgray", new Color(0.7f, 0.7f, 0.7f)},
            {"lightgray1", new Color(0.75f, 0.75f, 0.75f)},
            {"lightgray2", new Color(0.7f, 0.7f, 0.7f)},
            {"lightgray3", new Color(0.65f, 0.65f, 0.65f)},

            // 其他颜色
            {"cyan", new Color(0.0f, 0.9f, 0.9f)},
            {"cyan1", new Color(0.0f, 1.0f, 1.0f)},
            {"cyan2", new Color(0.2f, 0.8f, 0.8f)},
            {"cyan3", new Color(0.4f, 0.7f, 0.7f)},
            
            {"magenta", new Color(0.9f, 0.0f, 0.9f)},
            {"magenta1", new Color(1.0f, 0.0f, 1.0f)},
            {"magenta2", new Color(0.8f, 0.2f, 0.8f)},
            {"magenta3", new Color(0.7f, 0.3f, 0.7f)},
            
            {"lime", new Color(0.5f, 0.9f, 0.0f)},
            {"lime1", new Color(0.6f, 1.0f, 0.0f)},
            {"lime2", new Color(0.4f, 0.8f, 0.2f)},
            {"lime3", new Color(0.3f, 0.7f, 0.3f)},
            
            {"teal", new Color(0.0f, 0.5f, 0.5f)},
            {"teal1", new Color(0.0f, 0.6f, 0.6f)},
            {"teal2", new Color(0.2f, 0.5f, 0.5f)},
            {"teal3", new Color(0.3f, 0.4f, 0.4f)},
            
            {"navy", new Color(0.0f, 0.0f, 0.5f)},
            {"navy1", new Color(0.0f, 0.0f, 0.6f)},
            {"navy2", new Color(0.1f, 0.1f, 0.4f)},
            {"navy3", new Color(0.15f, 0.15f, 0.3f)},
            
            {"maroon", new Color(0.5f, 0.0f, 0.0f)},
            {"maroon1", new Color(0.6f, 0.0f, 0.0f)},
            {"maroon2", new Color(0.4f, 0.1f, 0.1f)},
            {"maroon3", new Color(0.3f, 0.15f, 0.15f)},
            
            {"olive", new Color(0.5f, 0.5f, 0.0f)},
            {"olive1", new Color(0.6f, 0.6f, 0.0f)},
            {"olive2", new Color(0.4f, 0.4f, 0.1f)},
            {"olive3", new Color(0.3f, 0.3f, 0.15f)},
            
            {"silver", new Color(0.75f, 0.75f, 0.75f)},
            {"silver1", new Color(0.8f, 0.8f, 0.8f)},
            {"silver2", new Color(0.7f, 0.7f, 0.7f)},
            {"silver3", new Color(0.65f, 0.65f, 0.65f)},
            
            {"gold", new Color(1.0f, 0.84f, 0.0f)},
            {"gold1", new Color(1.0f, 0.9f, 0.0f)},
            {"gold2", new Color(0.9f, 0.75f, 0.1f)},
            {"gold3", new Color(0.8f, 0.65f, 0.2f)},
            
            {"violet", new Color(0.93f, 0.51f, 0.93f)},
            {"violet1", new Color(1.0f, 0.4f, 1.0f)},
            {"violet2", new Color(0.85f, 0.55f, 0.85f)},
            {"violet3", new Color(0.75f, 0.6f, 0.75f)},
            
            {"indigo", new Color(0.29f, 0.0f, 0.51f)},
            {"indigo1", new Color(0.35f, 0.0f, 0.6f)},
            {"indigo2", new Color(0.25f, 0.1f, 0.45f)},
            {"indigo3", new Color(0.2f, 0.15f, 0.35f)},
            
            {"turquoise", new Color(0.25f, 0.88f, 0.82f)},
            {"turquoise1", new Color(0.2f, 1.0f, 0.9f)},
            {"turquoise2", new Color(0.3f, 0.8f, 0.75f)},
            {"turquoise3", new Color(0.4f, 0.7f, 0.65f)},
            
            {"coral", new Color(1.0f, 0.5f, 0.31f)},
            {"coral1", new Color(1.0f, 0.4f, 0.25f)},
            {"coral2", new Color(0.9f, 0.55f, 0.35f)},
            {"coral3", new Color(0.8f, 0.6f, 0.45f)},
            
            {"salmon", new Color(0.98f, 0.5f, 0.45f)},
            {"salmon1", new Color(1.0f, 0.4f, 0.35f)},
            {"salmon2", new Color(0.9f, 0.55f, 0.5f)},
            {"salmon3", new Color(0.8f, 0.6f, 0.55f)},
            
            {"khaki", new Color(0.94f, 0.9f, 0.55f)},
            {"khaki1", new Color(1.0f, 0.95f, 0.5f)},
            {"khaki2", new Color(0.9f, 0.85f, 0.6f)},
            {"khaki3", new Color(0.8f, 0.75f, 0.65f)},
            
            {"crimson", new Color(0.86f, 0.08f, 0.24f)},
            {"crimson1", new Color(0.95f, 0.0f, 0.2f)},
            {"crimson2", new Color(0.8f, 0.15f, 0.3f)},
            {"crimson3", new Color(0.7f, 0.2f, 0.35f)}
        };

        /// <summary>
        /// 从材质名称中提取颜色
        /// </summary>
        /// <param name="materialName">材质名称</param>
        /// <returns>提取到的颜色，如果没有匹配则返回默认颜色</returns>
        public static Color ExtractColorFromName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return Color.white;

            // 将材质名称转为小写
            string lowerName = materialName.ToLowerInvariant();

            // 存储最后一个匹配的颜色
            Color lastMatchedColor = Color.white;
            bool foundMatch = false;

            // 遍历颜色字典中的每个颜色名称
            foreach (var colorPair in ColorNameMap)
            {
                // 检查材质名称中是否包含颜色名称
                if (lowerName.Contains(colorPair.Key))
                {
                    lastMatchedColor = colorPair.Value;
                    foundMatch = true;
                    // 不直接返回，继续查找可能的更长匹配
                }
            }

            // 返回最后一个匹配到的颜色，如果没有匹配则返回默认颜色
            return foundMatch ? lastMatchedColor : Color.white;
        }

        /// <summary>
        /// 生成随机颜色
        /// </summary>
        /// <returns>随机颜色</returns>
        public static Color GenerateRandomColor()
        {
            // 生成随机颜色
            float h = Random.value;
            float s = 0.6f + Random.value * 0.4f; // 0.6-1.0的饱和度
            float v = 0.7f + Random.value * 0.3f; // 0.7-1.0的亮度

            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>
        /// 从材质提取主题色
        /// </summary>
        /// <param name="material">材质</param>
        /// <returns>提取到的颜色</returns>
        public static Color ExtractThemeColorFromMaterial(Material material)
        {
            if (material == null)
                return Color.white;

            // 先尝试从材质名称中提取颜色
            Color nameColor = ExtractColorFromName(material.name);
            if (nameColor != Color.white) // 如果从名称中提取到了颜色，则使用该颜色
                return nameColor;

            // 尝试从主要颜色属性中提取
            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");
            if (material.HasProperty("_MainColor"))
                return material.GetColor("_MainColor");

            // 默认颜色
            return new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        /// <summary>
        /// 将材质预设应用到渲染器
        /// </summary>
        /// <param name="renderer">渲染器</param>
        /// <param name="materials">材质数组</param>
        public static void ApplyMaterialsToRenderer(Renderer renderer, Material[] materials)
        {
            if (renderer == null || materials == null) return;

            // 计算要应用的材质数量
            int materialCount = Mathf.Min(renderer.sharedMaterials.Length, materials.Length);
            Material[] newMaterials = new Material[renderer.sharedMaterials.Length];

            // 复制原有材质
            System.Array.Copy(renderer.sharedMaterials, newMaterials, renderer.sharedMaterials.Length);

            // 应用预设中的材质
            for (int i = 0; i < materialCount; i++)
            {
                if (materials[i] != null)
                {
                    newMaterials[i] = materials[i];
                }
                else
                {
                    Debug.LogWarning($"材质预设 '{materials[i]?.name}' 中索引 {i} 的材质引用丢失");
                    // 可以选择跳过这个材质或使用默认材质
                }
            }

            // 设置材质
            renderer.sharedMaterials = newMaterials;
        }
    }
}
