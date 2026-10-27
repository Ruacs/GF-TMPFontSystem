using System.Collections;
using TMPro;
using UnityEngine;
using Lokas;

/// <summary>Unified font/style, optional whole-text layers, gradient and per-label curve.</summary>
[RequireComponent(typeof(TMP_Text)), DisallowMultipleComponent]
public class TMPStyleApplier : MonoBehaviour
{
    [Tooltip("对应 TMPFontProfile 中配置的样式 key，留空使用默认材质")]
    [SerializeField] private string _styleKey = string.Empty;
    [SerializeField] private bool _enableCurve;
    [SerializeField, Min(0)] private float _curveStrength = 20f;
    [SerializeField] private bool _curveDown;
    // Serialized value snapshot, not a shared asset reference. Persists with this label/prefab.
    [SerializeField, HideInInspector] private string _localStyleJson = string.Empty;
    public string StyleKey => _styleKey;
    public bool HasLocalStyle => !string.IsNullOrEmpty(_localStyleJson);
    public bool EnableCurve => _enableCurve;
    public float CurveStrength => _curveStrength;
    public bool CurveDown => _curveDown;

    private TMP_Text _text;
    private FontStylePreset _localPreset;
    private string _loadedLocalJson;
    private Material _localMaterial, _materialBeforeLocal;
    private TMP_FontAsset _fontBeforeLocal;
    private TMPArtTextRenderer _effects;
    private TMPFontComponent FontComponent => UnityGameFramework.Runtime.GameEntry.GetComponent<TMPFontComponent>();

    void Awake() { _text = GetComponent<TMP_Text>(); }
    void OnEnable()
    {
        TMPFontComponent.OnFontProfileChanged += Apply;
        StartCoroutine(ApplyNextFrame());
    }
    void OnDisable()
    {
        TMPFontComponent.OnFontProfileChanged -= Apply;
        StopAllCoroutines();
        ClearEffects();
        // The owned local material must not remain assigned when it is destroyed.
        if (_localMaterial != null && _text != null && _text.fontSharedMaterial == _localMaterial)
        {
            var manager = FontComponent;
            _text.fontSharedMaterial = MaterialWithoutLocal(manager);
        }
        ReleaseLocalMaterial();
    }
    void OnDestroy()
    {
        ReleaseLocalMaterial();
        if (_effects != null) DestroyOwned(_effects);
        if (_localPreset != null) DestroyOwned(_localPreset);
    }
    private IEnumerator ApplyNextFrame()
    {
        yield return null;
        RefreshStyle();
    }
    public void SetStyleKey(string styleKey) => SetStyleKey(styleKey, true);
    public void SetStyleKey(string styleKey, bool applyImmediately = true)
    {
        _styleKey = styleKey ?? string.Empty;
        if (applyImmediately) RefreshStyle();
    }
    public void SetLocalStyle(FontStylePreset preset)
    {
        _localStyleJson = preset == null ? string.Empty : JsonUtility.ToJson(preset);
        if (Application.isPlaying) RefreshStyle();
    }
    public FontStylePreset GetLocalStylePreset()
    {
        if (!HasLocalStyle) return null;
        if (_localPreset == null)
        {
            _localPreset = ScriptableObject.CreateInstance<FontStylePreset>();
            _localPreset.hideFlags = HideFlags.HideAndDontSave;
        }
        if (_loadedLocalJson != _localStyleJson)
        {
            JsonUtility.FromJsonOverwrite(_localStyleJson, _localPreset);
            _loadedLocalJson = _localStyleJson;
        }
        return _localPreset;
    }
    public void SetCurve(bool enabled, float strength = 20f, bool down = false)
    {
        _enableCurve = enabled; _curveStrength = Mathf.Max(0, strength); _curveDown = down;
        if (Application.isPlaying) RefreshStyle();
    }
    public void RefreshStyle()
    {
        if (!isActiveAndEnabled) return;
        Apply(FontComponent != null ? FontComponent.GetCurrentProfile() : null);
    }
    private void Apply(TMPFontProfile profile)
    {
        if (!isActiveAndEnabled) return;
        if (_text == null) _text = GetComponent<TMP_Text>();
        var manager = FontComponent;
        var font = manager != null ? manager.GetCurrentFontAsset() : null;
        if (font != null) _text.font = font;
        font = _text.font;
        var preset = HasLocalStyle ? GetLocalStylePreset() : profile?.GetPreset(_styleKey);
        Material material = null;
        if (HasLocalStyle && font != null)
        {
            if (_localMaterial == null)
            {
                _fontBeforeLocal = font; _materialBeforeLocal = _text.fontSharedMaterial;
                _localMaterial = new Material(font.material) { hideFlags = HideFlags.HideAndDontSave };
            }
            _localMaterial.shader = font.material.shader;
            _localMaterial.CopyPropertiesFromMaterial(font.material);
            preset.ApplyTo(_localMaterial);
            material = _localMaterial;
        }
        else if (manager != null && profile != null) material = manager.GetOrCreateMaterial(font, preset);
        if (!HasLocalStyle && _localMaterial != null)
        {
            if (material == null && _text.fontSharedMaterial == _localMaterial) material = MaterialWithoutLocal(manager);
            if (material != null) _text.fontSharedMaterial = material;
            ReleaseLocalMaterial();
        }
        if (material != null) _text.fontSharedMaterial = material;
        ConfigureEffects(preset);
        _text.UpdateMeshPadding();
        _text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
    }
    /// <summary>Also used by the edit-only preview; never assigns serialized TMP references.</summary>
    public void ConfigureEffects(FontStylePreset preset, Material previewMaterial = null)
    {
        bool needed = _enableCurve || (preset != null && (preset.enableLayeredRendering || preset.enableFaceGradient));
        if (!needed) { ClearEffects(); return; }
        if (_effects == null)
        {
            _effects = gameObject.AddComponent<TMPArtTextRenderer>();
            _effects.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
        }
        _effects.enabled = true;
        _effects.Configure(preset, previewMaterial, _enableCurve, _curveStrength, _curveDown);
    }
    public void ClearEffects()
    {
        if (_effects == null) return;
        _effects.enabled = false;
        if (!Application.isPlaying) { DestroyImmediate(_effects); _effects = null; }
    }
    private Material MaterialWithoutLocal(TMPFontComponent manager)
    {
        var profile = manager != null ? manager.GetCurrentProfile() : null;
        if (profile != null) return manager.GetOrCreateMaterial(_text.font, profile.GetPreset(_styleKey));
        return _text.font == _fontBeforeLocal && _materialBeforeLocal != null ? _materialBeforeLocal : _text.font != null ? _text.font.material : null;
    }
    private void ReleaseLocalMaterial()
    {
        if (_localMaterial != null) DestroyOwned(_localMaterial);
        _localMaterial = null;
    }
    private static void DestroyOwned(Object value)
    {
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
