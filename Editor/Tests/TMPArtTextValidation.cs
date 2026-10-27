using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GameFramework.Localization;

namespace Lokas.Editor.FontCharset.Tests
{
    /// <summary>Behavior checks with real TMP geometry, Canvas layers and language notifications.</summary>
    [InitializeOnLoad]
    public sealed class TMPArtTextValidation : EditorWindow
    {
        private const string Output = "Library/FontCharset/ArtTextValidation";
        private const string Running = "TMPArtTextValidation.Play";
        private const string SceneKey = "TMPArtTextValidation.Scenes";
        private bool executed;
        private string result = "正在验证……";
        [Serializable] private class Setup { public SceneSetup[] scenes; }
        static TMPArtTextValidation() { EditorApplication.playModeStateChanged += PlayChanged; }

        [MenuItem("Game Framework/字体与字符集/运行艺术字编辑验证", priority = 164)]
        public static void Run()
        {
            var window = GetWindow<TMPArtTextValidation>(true, "艺术字验证");
            window.executed = false; window.Show(); window.Repaint();
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox(result, MessageType.Info);
            if (executed || Event.current.type != EventType.Repaint) return;
            executed = true;
            try { ValidateEdit(); result = "PASS: 分层、弧形、渐变、覆盖、Prefab 序列化与还原。"; }
            catch (Exception ex) { result = "FAIL\n" + ex; Debug.LogException(ex); }
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/edit.txt", result);
        }
        private static FontStylePreset Preset()
        {
            var p = CreateInstance<FontStylePreset>();
            p.enableLayeredRendering = true; p.enableFaceGradient = true;
            p.gradientTop = new Color(1, 1, 0.5f); p.gradientBottom = new Color(1, 0.5f, 0.1f);
            p.faceSoftness = 0.02f; p.outlineWidth = 0.25f; p.outlineColor = new Color(0.55f, 0.1f, 0.9f);
            p.enableUnderlay = true; p.underlayColor = new Color(0.15f, 0.02f, 0.3f);
            p.underlayOffset = new Vector2(0, -0.7f); p.underlayDilate = 0.45f;
            return p;
        }
        private static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Art label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(TMPStyleApplier));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = "AVATAR"; text.fontSize = 72;
            text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(600, 220);
            text.characterSpacing = -8;
            return text;
        }
        private static void CheckLayers(TextMeshProUGUI text)
        {
            text.ForceMeshUpdate(); Canvas.ForceUpdateCanvases();
            var layers = text.GetComponentsInChildren<TMPArtLayerGraphic>();
            Assert(layers.Length == text.textInfo.materialCount * 3, "三层数量与图集数量不符。");
            for (int i = 0; i < layers.Length; i++)
            {
                int atlas = i % text.textInfo.materialCount;
                Assert(layers[i].SourceMesh == text.textInfo.meshInfo[atlas].mesh, "分层没有复用最终 TMP 网格。");
                if (!layers[i].material.HasProperty("_FaceSoftness") || !Mathf.Approximately(layers[i].material.GetFloat("_FaceSoftness"), 0.02f))
                {
                    Directory.CreateDirectory(Output);
                    File.WriteAllText(Output + "/material-diagnostic.txt", AssetDatabase.GetAssetPath(layers[i].material.shader) + "\n" + EditorJsonUtility.ToJson(layers[i].material, true));
                    throw new InvalidOperationException("字面柔化参数未传递到分层材质。atlas=" + atlas + " layer=" + i);
                }
                Assert(layers[i].material.GetFloat("_Layer") == i / text.textInfo.materialCount - 2, "分层顺序错误。");
                Assert(layers[i].mainTexture == text.textInfo.meshInfo[atlas].material.mainTexture, "字体图集未同步。");
            }
        }
        private static void ValidateEdit()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GameMain/Fonts/MFont_BASE.asset");
            Assert(font != null, "需要 MFont_BASE 字体。");
            var active = EditorSceneManager.GetActiveScene(); bool dirty = active.isDirty;
            bool preview = TMPStyleScenePreview.Enabled;
            string folder = "Assets/AAA_DevAssets/__TMPArtValidation_" + Guid.NewGuid().ToString("N");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Art validation", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var preset = Preset(); GameObject contents = null;
            var diagnostic = new Material(Resources.Load<Shader>("TMPArtLayeredSDF"));
            preset.ApplyTo(diagnostic);
            Assert(Mathf.Approximately(diagnostic.GetFloat("_FaceSoftness"), preset.faceSoftness), "自带分层 Shader 未接收预设柔化参数。");
            DestroyImmediate(diagnostic);
            try
            {
                var text = Label(root.transform, font); var applier = text.GetComponent<TMPStyleApplier>();
                var original = text.fontSharedMaterial;
                applier.SetLocalStyle(preset); applier.SetCurve(true, 7);
                TMPStyleScenePreview.Enabled = true; TMPStyleScenePreview.Refresh();
                CheckLayers(text);
                Assert(text.fontSharedMaterial == original, "编辑预览改写了序列化材质。");
                var local = applier.GetLocalStylePreset();
                preset.outlineColor = Color.green;
                Assert(local.outlineColor != preset.outlineColor, "当前文字覆盖仍与共享对象联动。");
                text.ForceMeshUpdate(); var curved = text.mesh.vertices;
                applier.SetCurve(false); TMPStyleScenePreview.Refresh(); text.ForceMeshUpdate();
                var flat = text.mesh.vertices;
                Assert(curved.Where((v, i) => (v - flat[i]).sqrMagnitude > 0.001f).Any(), "弧形未改变网格。");
                Assert(text.mesh.colors32.Select(c => c.g).Distinct().Count() > 1, "渐变未写入顶点。");
                AssetDatabase.CreateFolder("Assets/AAA_DevAssets", Path.GetFileName(folder));
                string path = folder + "/Label.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                contents = PrefabUtility.LoadPrefabContents(path);
                var saved = contents.GetComponentInChildren<TMPStyleApplier>();
                Assert(saved.HasLocalStyle && saved.GetLocalStylePreset().enableLayeredRendering, "本地样式没有随 Prefab 保存。");
                Assert(!File.ReadAllText(path).Contains("TMPArtTextRenderer"), "临时渲染器写入了 Prefab。");
                PrefabUtility.UnloadPrefabContents(contents); contents = null;
                TMPStyleScenePreview.AfterSave();
                var savedProfile = CreateInstance<TMPFontProfile>();
                AssetDatabase.CreateAsset(savedProfile, folder + "/Profile.asset");
                TMPStylePresetAuthoring.SaveAs(local, applier, savedProfile, "LocalCopy", folder + "/CopiedStyle.asset");
                Assert(!applier.HasLocalStyle && applier.StyleKey == "LocalCopy", "另存为没有从本地覆盖切换到新 Key。");
                applier.SetStyleKey("", false);
                applier.SetLocalStyle(null); applier.SetCurve(false); TMPStyleScenePreview.Refresh();
                Canvas.ForceUpdateCanvases();
                Assert(text.GetComponentsInChildren<TMPArtLayerGraphic>().Length == 0, "清除覆盖后分层残留。");
                Assert(text.canvasRenderer.GetMaterial() == text.materialForRendering, "清除覆盖未恢复普通 TMP。");
                Assert(active.isDirty == dirty, "验证弄脏用户场景。");
                using (var image = new FontStylePreview())
                {
                    preset.outlineColor = local.outlineColor;
                    var texture = image.Render(new Rect(0, 0, 640, 230), font, preset, "AVATAR", 72, new Color(0.08f, 0.08f, 0.12f));
                    SaveImage((RenderTexture)texture, "edit-layered.png");
                }
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                DestroyImmediate(root); DestroyImmediate(preset); EditorSceneManager.ClosePreviewScene(scene);
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                TMPStyleScenePreview.Enabled = preview; TMPStyleScenePreview.AfterSave();
            }
        }
        [MenuItem("Game Framework/字体与字符集/运行艺术字运行验证", priority = 165)]
        public static void RunPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play Mode。");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null || EditorSceneManager.GetSceneManagerSetup().Any(s => string.IsNullOrEmpty(s.path)) ||
                Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("请先保存场景并关闭 Prefab Stage，以便运行后恢复。");
            SessionState.SetString(SceneKey, JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(Running, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void PlayChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += ValidatePlay;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Running, false);
                EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Setup>(SessionState.GetString(SceneKey, "")).scenes);
            }
        }
        private sealed class Host : ITMPFontHost
        {
            internal readonly System.Collections.Generic.Dictionary<string, TMP_FontAsset> Fonts = new System.Collections.Generic.Dictionary<string, TMP_FontAsset>();
            public Language CurrentLanguage => Language.English;
            public string GetFontAssetPath(string key) => key;
            public void LoadFontAsset(string path, Action<TMP_FontAsset> success, Action failure) { if (Fonts.TryGetValue(path, out var f)) success(f); else failure(); }
            public void SetMainFont(TMP_FontAsset font) { }
        }
        private static void ValidatePlay()
        {
            string report;
            try
            {
                var host = new Host();
                foreach (string key in new[] { "BASE", "CNS", "JP", "KR" })
                {
                    var font = Instantiate(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GameMain/Fonts/MFont_" + key + ".asset"));
                    font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                    host.Fonts.Add("MFont_" + key, font);
                }
                var manager = new GameObject("Test font host").AddComponent<TMPFontComponent>(); manager.Configure(host);
                var preset = Preset(); var profile = CreateInstance<TMPFontProfile>();
                profile.styles.Add(new TMPFontProfile.StyleEntry { key = "Art", preset = preset });
                var config = CreateInstance<TMPLanguageFontConfig>(); config.defaultProfile = profile;
                var languages = new[] { Language.English, Language.ChineseSimplified, Language.Japanese, Language.Korean };
                var keys = new[] { "BASE", "CNS", "JP", "KR" };
                for (int i = 0; i < keys.Length; i++)
                {
                    var entry = CreateInstance<LanguageEntry>(); entry.languageKey = languages[i]; entry.fontAssetName = "MFont_" + keys[i];
                    config.languageProfiles.Add(entry);
                }
                var root = new GameObject("Runtime canvas", typeof(RectTransform), typeof(Canvas));
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var text = Label(root.transform, host.Fonts["MFont_BASE"]); var applier = text.GetComponent<TMPStyleApplier>();
                applier.SetStyleKey("Art", false); applier.SetCurve(true, 7);
                manager.SetLanguageConfig(config);
                for (int i = 0; i < languages.Length; i++)
                {
                    manager.SwitchLanguage(languages[i]);
                    text.text = new[] { "AVATAR", "奖励 AVATAR", "ボーナス", "보너스" }[i];
                    CheckLayers(text);
                    Assert(text.font == host.Fonts["MFont_" + keys[i]], "语言字体未应用。");
                }
                applier.SetLocalStyle(preset); manager.SwitchLanguage(Language.English);
                text.text = "AVATAR"; CheckLayers(text);
                var camera = new GameObject("Validation camera").AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 150;
                camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
                var rt = new RenderTexture(900, 400, 24); camera.targetTexture = rt;
                try { camera.Render(); SaveImage(rt, "play-layered.png"); }
                finally { camera.targetTexture = null; rt.Release(); DestroyImmediate(rt); }
                root.AddComponent<RectMask2D>(); Canvas.ForceUpdateCanvases(); CheckLayers(text);
                root.AddComponent<Image>(); var mask = root.AddComponent<Mask>(); mask.showMaskGraphic = false;
                Canvas.ForceUpdateCanvases(); CheckLayers(text);
                Assert(text.GetComponentsInChildren<TMPArtLayerGraphic>().All(g => g.materialForRendering.GetFloat("_Stencil") > 0), "普通 Mask 未传递到各层。");
                root.SetActive(false); root.SetActive(true); applier.RefreshStyle(); CheckLayers(text);
                text.text = ""; text.ForceMeshUpdate(); Canvas.ForceUpdateCanvases();
                Assert(text.GetComponentsInChildren<TMPArtLayerGraphic>().Length == 0, "空文本仍显示分层。");
                text.text = "AVATAR"; applier.RefreshStyle(); CheckLayers(text);
                applier.enabled = false; Canvas.ForceUpdateCanvases();
                Assert(text.GetComponentsInChildren<TMPArtLayerGraphic>().Length == 0, "停用组件后分层残留。");
                applier.enabled = true; applier.RefreshStyle(); CheckLayers(text);
                applier.SetLocalStyle(null); applier.SetCurve(false);
                var plain = CreateInstance<FontStylePreset>(); profile.styles[0].preset = plain;
                manager.InvalidateCache(preset); text.ForceMeshUpdate(); Canvas.ForceUpdateCanvases();
                Assert(text.GetComponentsInChildren<TMPArtLayerGraphic>().Length == 0, "切回旧样式后分层未清理。");
                Assert(!text.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"), "切回旧样式后阴影残留。");
                report = "PASS: 真正 Play Mode 中英日韩 SwitchLanguage、混合 fallback、分层/弧形/渐变、本地覆盖、RectMask2D/Mask、金色字面像素、清空文本、父节点关闭重开、组件停用/恢复。";
            }
            catch (Exception ex) { report = "FAIL\n" + ex; Debug.LogException(ex); }
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/play.txt", report);
            EditorApplication.isPlaying = false;
        }
        private static void SaveImage(RenderTexture texture, string filename)
        {
            var previous = RenderTexture.active; var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try { RenderTexture.active = texture; copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply(); Assert(copy.GetPixels32().Count(c => c.r > 150 && c.g > 60 && c.b < 180) > 100, "实际像素中缺少金色字面，检查分层顺序。"); Directory.CreateDirectory(Output); File.WriteAllBytes(Output + "/" + filename, copy.EncodeToPNG()); }
            finally { RenderTexture.active = previous; DestroyImmediate(copy); }
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
