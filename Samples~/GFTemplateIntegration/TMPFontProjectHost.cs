using System;
using GameFramework.Localization;
using GameFramework.Resource;
using TMPro;
using UnityGameFramework.Runtime;

namespace Lokas
{
    /// <summary>GF Template integration. Owned and maintained by the consuming project.</summary>
    public sealed class TMPFontProjectHost : ITMPFontHost
    {
        public Language CurrentLanguage => GameEntry.Localization.Language;
        public string GetFontAssetPath(string assetName) => AssetUtility.GetTMPFontAsset(assetName, true);

        public void LoadFontAsset(string assetPath, Action<TMP_FontAsset> onSuccess, Action onFailure)
        {
            GameEntry.Resource.LoadAsset(assetPath, typeof(TMP_FontAsset), new LoadAssetCallbacks(
                (path, asset, duration, data) => onSuccess?.Invoke(asset as TMP_FontAsset),
                (path, status, message, data) =>
                {
                    Log.Error("[TMPFontProjectHost] Failed to load {0}: {1}", path, message);
                    onFailure?.Invoke();
                }));
        }

        public void SetMainFont(TMP_FontAsset font) => UGuiForm.SetMainFont(font);
    }
}
