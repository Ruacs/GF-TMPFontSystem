using System;

namespace Lokas.Editor.FontCharset
{
    /// <summary>Editor integration supplied by the consuming project's Editor code.</summary>
    public static class FontCharsetProjectBridge
    {
        public static Func<string, string> FontAssetPathResolver { get; set; }

        public static string GetFontAssetPath(string assetName)
        {
            if (FontAssetPathResolver == null)
                throw new InvalidOperationException("未配置字体项目接入层。请导入 GF Template 接入示例，或设置 FontCharsetProjectBridge.FontAssetPathResolver。");
            string path = FontAssetPathResolver(assetName);
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("字体接入层返回了空资源路径: " + assetName);
            return path;
        }
    }
}
