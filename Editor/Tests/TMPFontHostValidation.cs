using System;
using System.Collections.Generic;
using System.IO;
using GameFramework.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lokas.Editor.FontCharset.Tests
{
    /// <summary>Checks public loading/host protocol in a preview scene; runtime lifecycle is validated separately.</summary>
    public static class TMPFontHostValidation
    {
        private sealed class Host : ITMPFontHost
        {
            public Language CurrentLanguage => Language.ChineseSimplified;
            public int Requests;
            public TMP_FontAsset MainFont;
            public readonly Dictionary<string, Action<TMP_FontAsset>> Pending = new Dictionary<string, Action<TMP_FontAsset>>();
            public string GetFontAssetPath(string name) => "fixture://" + name;
            public void LoadFontAsset(string path, Action<TMP_FontAsset> success, Action failure)
            {
                Requests++;
                Pending.Add(path, success);
            }
            public void Complete(string name, TMP_FontAsset font)
            {
                string path = GetFontAssetPath(name);
                var callback = Pending[path];
                Pending.Remove(path);
                callback(font);
            }
            public void SetMainFont(TMP_FontAsset font) => MainFont = font;
        }

        [MenuItem("Game Framework/字体与字符集/运行接入协议验证", priority = 162)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑态验证协议；生命周期需另行运行验证。");
            var originalScene = EditorSceneManager.GetActiveScene();
            bool wasDirty = originalScene.isDirty;
            var scene = EditorSceneManager.NewPreviewScene();
            TMP_FontAsset first = null, second = null;
            TMPLanguageFontConfig config = null;
            TMPFontProfile profile = null;
            LanguageEntry entry = null;
            Action<TMPFontProfile> listener = null;
            try
            {
                string[] fonts = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" });
                Assert(fonts.Length > 0, "需要目标工程已有 TMP 字体。");
                var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fonts[0]));
                string before = EditorJsonUtility.ToJson(source);
                first = UnityEngine.Object.Instantiate(source);
                second = UnityEngine.Object.Instantiate(source);
                first.hideFlags = second.hideFlags = HideFlags.HideAndDontSave;
                second.fallbackFontAssetTable = new List<TMP_FontAsset>();
                var go = new GameObject("TMP host protocol fixture") { hideFlags = HideFlags.HideAndDontSave };
                EditorSceneManager.MoveGameObjectToScene(go, scene);
                var component = go.AddComponent<TMPFontComponent>();
                var host = new Host();
                component.Configure(host);
                int loaded = 0;
                component.LoadFontAsset("Shared", f => { Assert(f == first, "首个请求的字体"); loaded++; });
                component.LoadFontAsset("Shared", f => { Assert(f == first, "合并请求的字体"); loaded++; });
                Assert(host.Requests == 1 && loaded == 0, "同名请求只调用加载器一次，等待实际完成。");
                host.Complete("Shared", first);
                Assert(loaded == 2, "完成后通知全部请求方。");
                component.LoadFontAsset("Shared", f => loaded++);
                Assert(host.Requests == 1 && loaded == 3, "缓存命中不重新加载。");
                profile = ScriptableObject.CreateInstance<TMPFontProfile>();
                config = ScriptableObject.CreateInstance<TMPLanguageFontConfig>();
                entry = ScriptableObject.CreateInstance<LanguageEntry>();
                entry.languageKey = Language.ChineseSimplified;
                entry.fontAssetName = "Target";
                entry.fontProfile = profile;
                config.languageProfiles.Add(entry);
                int notified = 0, completed = 0;
                listener = p =>
                {
                    Assert(p == profile && host.MainFont == second, "Profile 通知前先同步 UI 主字体。");
                    notified++;
                };
                TMPFontComponent.OnFontProfileChanged += listener;
                component.SetLanguageConfig(config, () => completed++);
                Assert(host.Pending.ContainsKey("fixture://MFont_BASE"), "基础字体先加载。");
                host.Complete("MFont_BASE", first);
                Assert(host.Pending.ContainsKey("fixture://Target") && completed == 0, "目标字体完成前不开出完成回调。");
                host.Complete("Target", second);
                Assert(completed == 1 && notified == 1 && component.GetCurrentFontAsset() == second, "语言配置、Profile 和字体完整应用。");
                Assert(second.fallbackFontAssetTable.Contains(first), "目标字体保留基础字体回退行为。");
                Assert(EditorJsonUtility.ToJson(source) == before, "协议验证不修改源字体。");
                Assert(originalScene.isDirty == wasDirty, "预览场景测试不弄脏原场景。");
                Directory.CreateDirectory("Library/FontCharset");
                File.WriteAllText("Library/FontCharset/host-validation.txt", "PASS: host path mapping, request coalescing, cache, language load order, UI/font/profile ordering, fallback and source isolation.\nRuntime lifecycle requires PlayMode verification.");
                Debug.Log("TMP 字体接入协议验证通过。报告: Library/FontCharset/host-validation.txt");
            }
            finally
            {
                if (listener != null) TMPFontComponent.OnFontProfileChanged -= listener;
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (UnityEngine.Object owned in new UnityEngine.Object[] { config, profile, entry, first, second })
                    if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
