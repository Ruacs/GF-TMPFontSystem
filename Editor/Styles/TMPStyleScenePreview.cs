using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lokas.Editor.FontCharset
{
    /// <summary>Edit-only renderer overrides. Serialized TMP font/material references are never replaced.</summary>
    [InitializeOnLoad]
    public static class TMPStyleScenePreview
    {
        private sealed class Preview
        {
            internal TMPStyleApplier Applier;
            internal TMP_Text Text;
            internal Material Material;
            internal int Signature;
            internal bool GeometryDirty, Rendering;
            internal Action<TMP_TextInfo> TextChanged;
            internal Material[] MeshMaterials;
            internal FontStylePreset Preset;
            internal readonly Dictionary<TMP_SubMeshUI, Material> SubMaterials = new Dictionary<TMP_SubMeshUI, Material>();
        }

        private const string EnabledKey = "Ruacs.TMPFont.ScenePreview.Enabled";
        private const string ProfileKey = "Ruacs.TMPFont.ScenePreview.Profile";
        private static readonly Dictionary<int, Preview> Previews = new Dictionary<int, Preview>();
        private static readonly Dictionary<int, FontStylePreset> Drafts = new Dictionary<int, FontStylePreset>();
        // TMP 3.0.9 calculates glyph geometry from nonserialized padding. This protected overload
        // updates only that render state, without assigning m_sharedMaterial / m_fontSharedMaterials.
        private static readonly MethodInfo Padding = typeof(TMP_Text).GetMethod("GetPaddingForMaterial",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Material) }, null);
        private static readonly int[] MaskProperties = {
            Shader.PropertyToID("_Stencil"), Shader.PropertyToID("_StencilComp"), Shader.PropertyToID("_StencilOp"),
            Shader.PropertyToID("_StencilReadMask"), Shader.PropertyToID("_StencilWriteMask"),
            Shader.PropertyToID("_ColorMask"), Shader.PropertyToID("_UseUIAlphaClip")
        };
        private static TMPFontProfile[] profiles;
        private static bool refreshing, suspended;
        private static double nextUpdate, nextScan;
        private static bool scanNeeded = true;
        internal static int MeshRefreshCount { get; private set; }

        static TMPStyleScenePreview()
        {
            EditorApplication.update += Update;
            EditorApplication.projectChanged += Invalidate;
            EditorApplication.hierarchyChanged += () => { scanNeeded = true; nextUpdate = 0; };
            Undo.undoRedoPerformed += Refresh;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
            Canvas.willRenderCanvases += RenderUI;
            Camera.onPreRender += BeginCamera;
            Camera.onPostRender += EndCamera;
            RenderPipelineManager.beginCameraRendering += BeginSRP;
            RenderPipelineManager.endCameraRendering += EndSRP;
        }

        public static bool Enabled
        {
            get => SessionState.GetBool(EnabledKey, true);
            set { SessionState.SetBool(EnabledKey, value); Refresh(); }
        }
        public static TMPFontProfile Profile
        {
            get => AssetDatabase.LoadAssetAtPath<TMPFontProfile>(AssetDatabase.GUIDToAssetPath(SessionState.GetString(ProfileKey, "")));
            set { SessionState.SetString(ProfileKey, value == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value))); Refresh(); }
        }
        private static bool CanPreview => Enabled && !suspended && !EditorApplication.isPlayingOrWillChangePlaymode;
        internal static void SetDraft(TMPStyleApplier applier, FontStylePreset draft)
        {
            if (applier == null) return;
            if (draft == null) Drafts.Remove(applier.GetInstanceID());
            else Drafts[applier.GetInstanceID()] = draft;
            Refresh();
        }
        public static bool TryGetPreset(TMPStyleApplier applier, out FontStylePreset preset, out string message)
        {
            preset = null;
            message = "";
            return applier != null && Resolve(applier.StyleKey, out preset, out message);
        }

        public static string GetStatus(TMPStyleApplier applier)
        {
            if (applier == null || !applier.enabled) return "组件已停用，不应用编辑态样式。";
            var text = applier.GetComponent<TMP_Text>();
            if (text == null || text.font == null) return "请先给文本设置 TMP 字体。";
            if (!Resolve(applier.StyleKey, out var preset, out string message)) return message;
            return preset == null ? "当前字体的默认材质。预览不会替换已保存的材质引用。" :
                "使用当前字体预览 " + preset.name + "；运行时仍按语言 Profile 应用。";
        }

        public static void Refresh()
        {
            if (refreshing) return;
            scanNeeded = true;
            nextUpdate = 0;
            Update();
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
        internal static void RefreshProfiles() { profiles = null; Refresh(); }
        private static void Invalidate() => RefreshProfiles();
        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Clear();
            if (state == PlayModeStateChange.EnteredEditMode) Refresh();
        }
        private static void Update()
        {
            if (refreshing || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!CanPreview) { if (Previews.Count > 0) Clear(); return; }
            if (EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = EditorApplication.timeSinceStartup + 0.25;
            refreshing = true;
            try
            {
                var live = new HashSet<int>();
                bool scan = scanNeeded || EditorApplication.timeSinceStartup >= nextScan;
                if (scan) nextScan = EditorApplication.timeSinceStartup + 1;
                var appliers = scan ? Resources.FindObjectsOfTypeAll<TMPStyleApplier>() :
                    Previews.Values.Select(p => p.Applier).ToArray();
                scanNeeded = false;
                foreach (var applier in appliers)
                {
                    if (applier == null || !applier.enabled || !applier.gameObject.activeInHierarchy || EditorUtility.IsPersistent(applier) ||
                        !applier.gameObject.scene.IsValid() || !applier.gameObject.scene.isLoaded) continue;
                    var text = applier.GetComponent<TMP_Text>();
                    if (text == null || text.font == null || text.font.material == null || Padding == null ||
                        !Resolve(applier.StyleKey, out var preset, out _)) continue;
                    int id = applier.GetInstanceID();
                    if (Drafts.TryGetValue(id, out var draft) && draft != null) preset = draft;
                    live.Add(id);
                    int signature = Fingerprint(applier.StyleKey, text.font, preset);
                    if (!Previews.TryGetValue(id, out var preview))
                    {
                        preview = new Preview { Applier = applier, Text = text };
                        var observed = preview;
                        preview.TextChanged = info => { if (!observed.Rendering) observed.GeometryDirty = true; };
                        text.OnPreRenderText += preview.TextChanged;
                        Previews.Add(id, preview);
                    }
                    if (preview.Signature == signature && preview.Material != null)
                    {
                        if (preview.GeometryDirty) { Render(preview); SceneView.RepaintAll(); }
                        continue;
                    }
                    Restore(preview);
                    DestroyMaterials(preview);
                    preview.Material = new Material(text.font.material) {
                        name = "TMP editor preview: " + (preset == null ? "Default" : preset.name),
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    if (preset != null) preset.ApplyTo(preview.Material);
                    preview.Preset = preset;
                    preview.Signature = signature;
                    Render(preview);
                    SceneView.RepaintAll();
                    EditorApplication.QueuePlayerLoopUpdate();
                }
                foreach (int id in Previews.Keys.Where(id => !live.Contains(id)).ToArray()) Release(id);
            }
            finally { refreshing = false; }
        }
        private static int Fingerprint(string key, TMP_FontAsset font, FontStylePreset preset)
        {
            unchecked
            {
                int hash = (key ?? "").GetHashCode();
                hash = hash * 31 + font.GetInstanceID();
                hash = hash * 31 + font.material.GetInstanceID();
                hash = hash * 31 + EditorUtility.GetDirtyCount(font.material);
                hash = hash * 31 + (font.material.mainTexture == null ? 0 : font.material.mainTexture.GetInstanceID());
                if (preset == null) return hash;
                hash = hash * 31 + preset.GetInstanceID();
                hash = hash * 31 + preset.faceColor.GetHashCode();
                hash = hash * 31 + preset.faceDilate.GetHashCode();
                hash = hash * 31 + preset.faceSoftness.GetHashCode();
                hash = hash * 31 + preset.outlineWidth.GetHashCode();
                hash = hash * 31 + preset.outlineColor.GetHashCode();
                hash = hash * 31 + preset.outlineSoftness.GetHashCode();
                hash = hash * 31 + preset.enableUnderlay.GetHashCode();
                hash = hash * 31 + preset.underlayColor.GetHashCode();
                hash = hash * 31 + preset.underlayOffset.GetHashCode();
                hash = hash * 31 + preset.underlayDilate.GetHashCode();
                return hash * 31 + preset.underlaySoftness.GetHashCode();
            }
        }

        private static bool Resolve(string key, out FontStylePreset preset, out string message)
        {
            preset = null;
            message = "";
            if (string.IsNullOrEmpty(key)) return true;
            var selected = Profile;
            if (selected != null)
            {
                if (selected.styles != null && selected.styles.Any(e => e != null && e.key == key))
                { preset = selected.GetPreset(key); return true; }
                message = "预览 Profile 中没有样式 " + key + "。";
                return false;
            }
            if (profiles == null) profiles = AssetDatabase.FindAssets("t:TMPFontProfile")
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TMPFontProfile>).Where(p => p != null).ToArray();
            var matches = profiles.Where(p => p.styles != null && p.styles.Any(e => e != null && e.key == key))
                .Select(p => p.GetPreset(key)).Distinct().ToArray();
            if (matches.Length == 1) { preset = matches[0]; return true; }
            message = matches.Length == 0 ? "未找到样式 " + key + "，请检查 Profile 并刷新。" :
                "多个 Profile 为此 Key 配置了不同预设，请指定预览 Profile。";
            return false;
        }

        private static void RenderUI()
        {
            if (!CanPreview || refreshing) return;
            foreach (var preview in Previews.Values)
                if (preview.Text is TextMeshProUGUI ui && ui != null && ui.isActiveAndEnabled &&
                    (ui.canvasRenderer.materialCount == 0 || ui.canvasRenderer.GetMaterial() != preview.Material)) ApplyRenderMaterials(preview);
        }
        private static void Render(Preview preview)
        {
            if (preview.Text == null || preview.Material == null) return;
            preview.Rendering = true;
            try { RefreshMesh(preview); }
            finally { preview.Rendering = false; preview.GeometryDirty = false; }
        }
        private static void RefreshMesh(Preview preview)
        {
            MeshRefreshCount++;
            var text = preview.Text;
            // Resolve ordinary TMP dirtiness first; then calculate padding against the preview only.
            text.UpdateMeshPadding();
            text.ForceMeshUpdate(ignoreActiveState: true);
            Padding.Invoke(text, new object[] { preview.Material });
            var originalPadding = new Dictionary<TMP_SubMeshUI, float>();
            if (text is TextMeshProUGUI)
            {
                foreach (var sub in text.GetComponentsInChildren<TMP_SubMeshUI>(true))
                {
                    // Explicit <material> and sprite overrides retain their authored appearance.
                    bool inherited = sub.isDefaultMaterial || (sub.fallbackMaterial != null && sub.fallbackSourceMaterial == text.fontSharedMaterial);
                    if (sub.textComponent != text || !inherited || sub.fontAsset == null || sub.sharedMaterial == null) continue;
                    if (!preview.SubMaterials.TryGetValue(sub, out var material) || material == null ||
                        material.mainTexture != sub.sharedMaterial.mainTexture)
                    {
                        if (material != null) UnityEngine.Object.DestroyImmediate(material);
                        material = new Material(sub.sharedMaterial) { name = "TMP editor fallback preview", hideFlags = HideFlags.HideAndDontSave };
                        if (preview.Preset != null) preview.Preset.ApplyTo(material);
                        preview.SubMaterials[sub] = material;
                    }
                    originalPadding[sub] = sub.padding;
                    sub.padding = sub.GetPaddingForMaterial(material);
                }
            }
            try { text.ForceMeshUpdate(ignoreActiveState: true); }
            finally { foreach (var pair in originalPadding) if (pair.Key != null) pair.Key.padding = pair.Value; }
            ApplyRenderMaterials(preview);
        }
        private static void ApplyRenderMaterials(Preview preview)
        {
            var text = preview.Text;
            if (text is TextMeshProUGUI ui && ui.canvasRenderer != null)
            {
                CopyMask(ui.materialForRendering, preview.Material);
                ui.canvasRenderer.materialCount = Math.Max(1, ui.canvasRenderer.materialCount);
                ui.canvasRenderer.SetMaterial(preview.Material, 0);
                foreach (var pair in preview.SubMaterials)
                {
                    if (pair.Key == null || pair.Value == null) continue;
                    CopyMask(pair.Key.materialForRendering, pair.Value);
                    pair.Key.canvasRenderer.materialCount = Math.Max(1, pair.Key.canvasRenderer.materialCount);
                    pair.Key.canvasRenderer.SetMaterial(pair.Value, 0);
                }
            }
        }
        private static void CopyMask(Material source, Material destination)
        {
            foreach (int property in MaskProperties)
                if (source != null && source.HasProperty(property) && destination.HasProperty(property))
                    destination.SetFloat(property, source.GetFloat(property));
            foreach (string keyword in new[] { "UNITY_UI_CLIP_RECT", "UNITY_UI_ALPHACLIP" })
            {
                if (source != null && source.IsKeywordEnabled(keyword)) destination.EnableKeyword(keyword);
                else destination.DisableKeyword(keyword);
            }
        }

        private static void BeginSRP(ScriptableRenderContext context, Camera camera) => BeginCamera(camera);
        private static void EndSRP(ScriptableRenderContext context, Camera camera) => EndCamera(camera);
        private static void BeginCamera(Camera camera)
        {
            if (!CanPreview) return;
            foreach (var preview in Previews.Values)
            {
                if (!(preview.Text is TextMeshPro text) || text == null || !text.isActiveAndEnabled) continue;
                var renderer = text.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                if (preview.MeshMaterials != null) continue;
                if (preview.GeometryDirty) Render(preview);
                preview.MeshMaterials = renderer.sharedMaterials;
                var materials = (Material[])preview.MeshMaterials.Clone();
                if (materials.Length > 0) materials[0] = preview.Material;
                renderer.sharedMaterials = materials;
            }
        }
        private static void EndCamera(Camera camera)
        {
            foreach (var preview in Previews.Values) RestoreMeshRenderer(preview);
        }
        private static void RestoreMeshRenderer(Preview preview)
        {
            if (preview.MeshMaterials == null) return;
            if (preview.Text != null)
            {
                var renderer = preview.Text.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterials = preview.MeshMaterials;
            }
            preview.MeshMaterials = null;
        }
        private static void Restore(Preview preview)
        {
            RestoreMeshRenderer(preview);
            if (preview.Text == null) return;
            preview.Text.UpdateMeshPadding();
            preview.Text.ForceMeshUpdate(ignoreActiveState: true);
            if (preview.Text is TextMeshProUGUI ui && ui.canvasRenderer != null)
            {
                ui.canvasRenderer.materialCount = Math.Max(1, ui.canvasRenderer.materialCount);
                ui.canvasRenderer.SetMaterial(ui.materialForRendering, 0);
            }
            foreach (var pair in preview.SubMaterials)
                if (pair.Key != null)
                {
                    pair.Key.padding = pair.Key.GetPaddingForMaterial();
                    pair.Key.canvasRenderer.materialCount = Math.Max(1, pair.Key.canvasRenderer.materialCount);
                    pair.Key.canvasRenderer.SetMaterial(pair.Key.materialForRendering, 0);
                }
        }
        private static void Release(int id)
        {
            var preview = Previews[id];
            if (preview.Text != null) preview.Text.OnPreRenderText -= preview.TextChanged;
            Restore(preview);
            DestroyMaterials(preview);
            Previews.Remove(id);
        }
        private static void Clear()
        {
            foreach (int id in Previews.Keys.ToArray()) Release(id);
        }
        private static void DestroyMaterials(Preview preview)
        {
            if (preview.Material != null) UnityEngine.Object.DestroyImmediate(preview.Material);
            foreach (var material in preview.SubMaterials.Values)
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            preview.SubMaterials.Clear();
        }
        internal static void BeforeSave()
        {
            suspended = true;
            Clear();
            EditorApplication.delayCall += AfterSave;
        }
        internal static void AfterSave() { suspended = false; Refresh(); }
    }

    internal sealed class TMPStyleScenePreviewSaveGuard : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths) { TMPStyleScenePreview.BeforeSave(); return paths; }
    }
}
