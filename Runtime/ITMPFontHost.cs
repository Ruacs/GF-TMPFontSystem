using System;
using GameFramework.Localization;
using TMPro;

/// <summary>Project-owned language, resource loading and UI integration.</summary>
public interface ITMPFontHost
{
    Language CurrentLanguage { get; }
    string GetFontAssetPath(string assetName);
    void LoadFontAsset(string assetPath, Action<TMP_FontAsset> onSuccess, Action onFailure);
    void SetMainFont(TMP_FontAsset font);
}
