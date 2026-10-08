using Lokas.Editor.FontCharset;
using UnityEditor;

namespace Lokas.Editor
{
    [InitializeOnLoad]
    public static class TMPFontProjectEditorBridge
    {
        static TMPFontProjectEditorBridge()
        {
            FontCharsetProjectBridge.FontAssetPathResolver = name => AssetUtility.GetTMPFontAsset(name, true);
        }
    }
}
