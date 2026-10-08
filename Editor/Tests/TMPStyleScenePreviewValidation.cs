using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lokas.Editor.FontCharset.Tests
{
    /// <summary>Canvas mesh/material pixels and prefab serialization; leaves the user's scene and prefab stage intact.</summary>
    public sealed class TMPStyleScenePreviewValidation : EditorWindow
    {
        private bool executed;
        private string result = "正在验证编辑态样式……";
        private const string Output = "Library/FontCharset/ScenePreviewValidation";

        [MenuItem("Game Framework/字体与字符集/运行页面样式预览验证", priority = 163)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑态运行。");
            var window = GetWindow<TMPStyleScenePreviewValidation>(true, "页面样式预览验证");
            window.executed = false;
            window.minSize = new Vector2(620, 180);
            window.Show();
            window.Repaint();
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox(result, MessageType.Info);
            if (executed || Event.current.type != EventType.Repaint) return;
            executed = true;
            Directory.CreateDirectory(Output);
            try
            {
                Validate();
                result = "PASS：Canvas 网格像素、预设更新/撤销、样式/Profile 切换、十次 Inspector 重建保持展开和草稿、草稿隔离、保存/另存为、缺失/冲突 Key、默认样式、字体/fallback、停用恢复、遮罩、Prefab 保存/加载及源资产隔离。";
                File.WriteAllText(Output + "/result.txt", result);
                Debug.Log(result);
            }
            catch (Exception ex) { result = "FAIL\n" + ex; File.WriteAllText(Output + "/result.txt", result); Debug.LogException(ex); }
            Repaint();
        }

        private static void Validate()
        {
            TMP_FontAsset font = null;
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" }))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.atlasPopulationMode == AtlasPopulationMode.Static && candidate.HasCharacter('A'))
                { font = candidate; break; }
            }
            Assert(font != null, "需要含 A 字符的 Static TMP 字体。");
            bool enabled = TMPStyleScenePreview.Enabled;
            var originalProfile = TMPStyleScenePreview.Profile;
            bool sceneDirty = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty;
            string folderName = "__TMPStyleSceneValidation_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            string key = folderName + "_style";
            PreviewRenderUtility renderer = null;
            GameObject contents = null;
            try
            {
                TMPStyleScenePreview.Enabled = false;
                TMPStyleScenePreview.Profile = null;
                AssetDatabase.CreateFolder("Assets", folderName);
                var preset = CreateInstance<FontStylePreset>();
                preset.faceColor = Color.red;
                preset.outlineWidth = 0.25f;
                preset.enableUnderlay = true;
                AssetDatabase.CreateAsset(preset, folder + "/Preset.asset");
                var profile = CreateInstance<TMPFontProfile>();
                profile.styles.Add(new TMPFontProfile.StyleEntry { key = key, preset = preset });
                AssetDatabase.CreateAsset(profile, folder + "/Profile.asset");
                AssetDatabase.SaveAssetIfDirty(profile);
                AssetDatabase.SaveAssetIfDirty(preset);

                renderer = new PreviewRenderUtility();
                renderer.camera.orthographic = true;
                renderer.camera.orthographicSize = 2f;
                renderer.camera.transform.position = new Vector3(0, 0, -10);
                renderer.camera.clearFlags = CameraClearFlags.SolidColor;
                renderer.camera.backgroundColor = Color.black;
                var canvasObject = new GameObject("StylePreviewCanvas", typeof(RectTransform), typeof(Canvas));
                renderer.AddSingleGO(canvasObject);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = renderer.camera;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(600, 180);
                canvas.transform.localScale = Vector3.one * 0.01f;
                var maskObject = new GameObject("Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
                maskObject.transform.SetParent(canvas.transform, false);
                ((RectTransform)maskObject.transform).sizeDelta = new Vector2(500, 150);
                maskObject.GetComponent<Mask>().showMaskGraphic = false;
                maskObject.GetComponent<Mask>().enabled = false;
                var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(TMPStyleApplier));
                textObject.transform.SetParent(maskObject.transform, false);
                var text = textObject.GetComponent<TextMeshProUGUI>();
                ((RectTransform)text.transform).sizeDelta = new Vector2(460, 140);
                text.font = font;
                text.text = "AAAA";
                text.fontSize = 72;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.Center;
                text.ForceMeshUpdate(true);
                Canvas.ForceUpdateCanvases();
                var originalMaterial = text.fontSharedMaterial;
                string fontJson = EditorJsonUtility.ToJson(font), materialJson = EditorJsonUtility.ToJson(font.material);
                bool fontDirty = EditorUtility.IsDirty(font), materialDirty = EditorUtility.IsDirty(font.material);
                var applier = textObject.GetComponent<TMPStyleApplier>();
                applier.SetStyleKey(key, false);
                TMPStyleScenePreview.Enabled = true;
                TMPStyleScenePreview.RefreshProfiles();
                TMPStyleScenePreview.Refresh();
                Assert(text.fontSharedMaterial == originalMaterial && text.font == font, "预览替换了序列化字体/材质。");
                double red = Pixels(renderer, text, "red.png");
                TMPStyleScenePreview.Refresh();
                Canvas.ForceUpdateCanvases();
                TMPStyleScenePreview.Refresh();
                int meshCount = TMPStyleScenePreview.MeshRefreshCount;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 100; i++) Canvas.ForceUpdateCanvases();
                clock.Stop();
                int idleRebuilds = TMPStyleScenePreview.MeshRefreshCount - meshCount;
                File.WriteAllText(Output + "/idle-performance.txt", "100 Canvas redraws: " + clock.Elapsed.TotalMilliseconds.ToString("F2") + " ms; preview mesh refreshes=" + idleRebuilds);
                Assert(idleRebuilds == 0, "空闲 Canvas 重绘重复生成了文字网格。");
                Undo.RecordObject(preset, "Scene preview preset");
                preset.faceColor = Color.green;
                TMPStyleScenePreview.Refresh();
                double green = Pixels(renderer, text, "green.png");
                Assert(red > green + 10, "修改 Face 颜色后 Canvas 像素没有变化。");
                Undo.PerformUndo();
                TMPStyleScenePreview.Refresh();
                Assert(Pixels(renderer, text, null) > green + 10, "撤销没有恢复渲染。");
                var rendered = text.canvasRenderer.GetMaterial();
                Assert(rendered.IsKeywordEnabled("OUTLINE_ON") && rendered.IsKeywordEnabled("UNDERLAY_ON"), "描边/阴影关键字未应用。");
                maskObject.GetComponent<Mask>().enabled = true;
                Canvas.ForceUpdateCanvases();
                TMPStyleScenePreview.Refresh();
                rendered = text.canvasRenderer.GetMaterial();
                Assert(rendered.GetFloat("_Stencil") == text.materialForRendering.GetFloat("_Stencil"), "遮罩状态未保留。");

                string prefabPath = folder + "/Preview.prefab";
                PrefabUtility.SaveAsPrefabAsset(canvasObject, prefabPath);
                TMPStyleScenePreview.AfterSave();
                string saved = File.ReadAllText(prefabPath);
                Assert(!saved.Contains("TMP editor preview:"), "Prefab 写入了临时预览材质。");
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var loaded = contents.GetComponentInChildren<TextMeshProUGUI>();
                Assert(loaded != null && loaded.fontSharedMaterial == originalMaterial && loaded.font == font, "Prefab 字体/材质引用丢失。");
                TMPStyleScenePreview.Refresh();
                Assert(loaded.GetComponent<TMPStyleApplier>().StyleKey == key, "Prefab 样式 Key 没有保存。");
                Assert(loaded.canvasRenderer.GetMaterial() != originalMaterial, "重新加载 Prefab 后没有预览。");
                PrefabUtility.UnloadPrefabContents(contents);
                contents = null;
                TMPStyleScenePreview.AfterSave();

                applier.SetStyleKey("", false);
                TMPStyleScenePreview.Refresh();
                Assert(!text.canvasRenderer.GetMaterial().IsKeywordEnabled("OUTLINE_ON"), "默认样式没有还原。");
                applier.SetStyleKey(key, false);
                var otherPreset = CreateInstance<FontStylePreset>();
                otherPreset.faceColor = Color.green;
                AssetDatabase.CreateAsset(otherPreset, folder + "/OtherPreset.asset");
                var otherProfile = CreateInstance<TMPFontProfile>();
                otherProfile.styles.Add(new TMPFontProfile.StyleEntry { key = key, preset = otherPreset });
                AssetDatabase.CreateAsset(otherProfile, folder + "/OtherProfile.asset");
                TMPStyleScenePreview.RefreshProfiles();
                TMPStyleScenePreview.Profile = null;
                TMPStyleScenePreview.Refresh();
                Assert(TMPStyleScenePreview.GetStatus(applier).Contains("多个 Profile"), "冲突 Profile 没有明确提示。");
                Assert(text.canvasRenderer.GetMaterial() == text.materialForRendering, "冲突 Key 应恢复原材质。");
                TMPStyleScenePreview.Profile = profile;
                Assert(text.canvasRenderer.GetMaterial().GetColor("_FaceColor") == preset.faceColor, "显式 Profile 未生效。");
                applier.SetStyleKey(key + "_missing", false);
                TMPStyleScenePreview.Refresh();
                Assert(text.canvasRenderer.GetMaterial() == text.materialForRendering, "缺失 Key 应恢复原材质。");
                applier.SetStyleKey(key, false);

                bool fallbackTested = false;
                foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" }))
                {
                    var candidate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate == null || candidate == font || candidate.material.mainTexture == font.material.mainTexture ||
                        candidate.atlasPopulationMode != AtlasPopulationMode.Static || !candidate.HasCharacter('A')) continue;
                    var copy = Instantiate(candidate);
                    try
                    {
                        copy.hideFlags = HideFlags.HideAndDontSave;
                        copy.characterTable.Clear();
                        copy.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { font };
                        copy.ReadFontAssetDefinition();
                        text.font = copy;
                        TMPStyleScenePreview.Refresh();
                        Assert(text.fontSharedMaterial.mainTexture == candidate.material.mainTexture, "字体切换未使用新图集。");
                        var sub = text.GetComponentInChildren<TMP_SubMeshUI>();
                        Assert(sub != null && sub.canvasRenderer.GetMaterial().GetColor("_FaceColor") == preset.faceColor, "fallback 子网格未应用样式。");
                        fallbackTested = true;
                    }
                    finally { text.font = font; TMPStyleScenePreview.Refresh(); DestroyImmediate(copy); }
                    break;
                }
                Assert(fallbackTested, "需要第二个 Static 字体来验证字体切换和 fallback。");
                var draft = TMPStylePresetAuthoring.CreateDraft(preset);
                try
                {
                    using (var editable = new SerializedObject(draft))
                        Assert(editable.FindProperty("faceColor").editable, "草稿参数被锁为只读。");
                    draft.faceColor = Color.blue;
                    ValidateInspectorRebuild(applier, draft);
                    string originalPreset = EditorJsonUtility.ToJson(preset);
                    TMPStyleScenePreview.SetDraft(applier, draft);
                    Assert(text.canvasRenderer.GetMaterial().GetColor("_FaceColor") == Color.blue, "当前文本未预览草稿。");
                    Assert(EditorJsonUtility.ToJson(preset) == originalPreset, "草稿修改污染原预设。");
                    var savedCopy = TMPStylePresetAuthoring.SaveAs(draft, applier, profile, key + "_copy", folder + "/Copy.asset");
                    Assert(savedCopy != preset && savedCopy.faceColor == Color.blue && savedCopy.hideFlags == HideFlags.None, "另存为预设数据错误。");
                    Assert(profile.GetPreset(key + "_copy") == savedCopy && applier.StyleKey == key + "_copy", "另存为没有登记和绑定新 Key。");
                    Assert(EditorJsonUtility.ToJson(preset) == originalPreset, "另存为改变原预设。");
                    TMPStylePresetAuthoring.Save(draft, preset);
                    Assert(preset.faceColor == Color.blue && preset.hideFlags == HideFlags.None && EditorUtility.IsPersistent(preset), "保存没有更新持久化预设。");
                    Undo.ClearUndo(profile);
                    Undo.ClearUndo(applier);
                    Undo.ClearUndo(savedCopy);
                }
                finally { TMPStyleScenePreview.SetDraft(applier, null); DestroyImmediate(draft); }
                applier.enabled = false;
                TMPStyleScenePreview.Refresh();
                Assert(text.canvasRenderer.GetMaterial() == text.materialForRendering, "停用组件未恢复原渲染材质。");
                Assert(EditorJsonUtility.ToJson(font) == fontJson && EditorJsonUtility.ToJson(font.material) == materialJson, "源字体或共享材质被改写。");
                Assert(EditorUtility.IsDirty(font) == fontDirty && EditorUtility.IsDirty(font.material) == materialDirty, "源资产 dirty 状态改变。");
                Assert(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty == sceneDirty, "用户场景 dirty 状态改变。");
                Undo.ClearUndo(preset);
            }
            finally
            {
                TMPStyleScenePreview.Enabled = false;
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                renderer?.Cleanup();
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                TMPStyleScenePreview.Profile = originalProfile;
                TMPStyleScenePreview.Enabled = enabled;
            }
        }
        private static double Pixels(PreviewRenderUtility renderer, TextMeshProUGUI text, string filename)
        {
            TMPStyleScenePreview.Refresh();
            Canvas.ForceUpdateCanvases();
            renderer.BeginPreview(new Rect(0, 0, 500, 220), GUIStyle.none);
            Canvas.ForceUpdateCanvases();
            // Preview cameras do not composite native Canvas objects. Draw the actual Canvas-generated
            // glyph mesh with the actual CanvasRenderer material, rather than using a separate TMP clone.
            renderer.DrawMesh(text.mesh, text.transform.localToWorldMatrix, text.canvasRenderer.GetMaterial(), 0);
            renderer.Render(true);
            var texture = (RenderTexture)renderer.EndPreview();
            var old = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply();
                if (filename != null) File.WriteAllBytes(Output + "/" + filename, copy.EncodeToPNG());
                double value = 0;
                foreach (var color in copy.GetPixels32()) value += color.r - color.g;
                return value;
            }
            finally { RenderTexture.active = old; DestroyImmediate(copy); }
        }
        private static void ValidateInspectorRebuild(TMPStyleApplier applier, FontStylePreset draft)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(TMPStyleApplierEditor);
            UnityEditor.Editor editor = null;
            try
            {
                editor = UnityEditor.Editor.CreateEditor(applier);
                type.GetProperty("showParameters", flags).SetValue(editor, true);
                type.GetProperty("draft", flags).SetValue(editor, draft);
                type.GetProperty("draftTarget", flags).SetValue(editor, applier);
                for (int i = 0; i < 10; i++)
                {
                    DestroyImmediate(editor);
                    editor = UnityEditor.Editor.CreateEditor(applier);
                    Assert((bool)type.GetProperty("showParameters", flags).GetValue(editor), "Inspector 重建后样式参数折叠。");
                    Assert(type.GetProperty("draft", flags).GetValue(editor) == draft && draft.faceColor == Color.blue, "Inspector 重建丢失草稿参数。");
                }
            }
            finally
            {
                if (editor != null)
                {
                    type.GetProperty("draft", flags).SetValue(editor, null);
                    type.GetProperty("draftTarget", flags).SetValue(editor, null);
                    DestroyImmediate(editor);
                }
            }
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
