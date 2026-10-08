using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Lokas.Editor.FontCharset;

[CustomEditor(typeof(TMPStyleApplier))]
[CanEditMultipleObjects]
public class TMPStyleApplierEditor : Editor
{
    private const float RefreshButtonWidth = 72f;

    private static List<string> s_CachedKeys;
    private static Dictionary<string, List<string>> s_KeySourceMap;
    private FontStylePreset sourcePreset, draft;
    private TMPStyleApplier draftTarget;
    private string draftKey, sourceSnapshot, draftBaseline, newKey;
    private TMPFontProfile saveProfile;
    private bool showParameters = true, showSaveAs;
    private string authoringMessage;

    private void OnEnable() { EditorApplication.projectChanged += ProjectChanged; Undo.undoRedoPerformed += Repaint; }
    private void OnDisable()
    {
        EditorApplication.projectChanged -= ProjectChanged;
        Undo.undoRedoPerformed -= Repaint;
        ReleaseDraft();
    }
    private void ProjectChanged() { s_CachedKeys = null; Repaint(); }
    private void ReleaseDraft()
    {
        if (draftTarget != null) TMPStyleScenePreview.SetDraft(draftTarget, null);
        if (draft != null) { Undo.ClearUndo(draft); DestroyImmediate(draft); }
        draft = null;
        draftTarget = null;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty styleKeyProp = serializedObject.FindProperty("_styleKey");
        EditorGUILayout.Space(2);
        DrawStyleKeyDropdown(styleKeyProp, "Style Key", Repaint);

        if (serializedObject.ApplyModifiedProperties()) { ReleaseDraft(); TMPStyleScenePreview.Refresh(); }

        if (!Application.isPlaying)
        {
            EditorGUILayout.Space(4);
            bool enabled = EditorGUILayout.Toggle("编辑态预览", TMPStyleScenePreview.Enabled);
            if (enabled != TMPStyleScenePreview.Enabled) TMPStyleScenePreview.Enabled = enabled;
            if (enabled)
            {
                var profile = (TMPFontProfile)EditorGUILayout.ObjectField("预览 Profile", TMPStyleScenePreview.Profile, typeof(TMPFontProfile), false);
                if (profile != TMPStyleScenePreview.Profile) TMPStyleScenePreview.Profile = profile;
                string message = TMPStyleScenePreview.GetStatus((TMPStyleApplier)target);
                if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            }
            DrawPresetAuthoring();
        }
    }

    private void DrawPresetAuthoring()
    {
        if (targets.Length != 1) { EditorGUILayout.HelpBox("多选可设置 Style Key；调整预设参数请选中单个文本。", MessageType.Info); return; }
        var applier = (TMPStyleApplier)target;
        if (!TMPStyleScenePreview.TryGetPreset(applier, out var resolved, out _)) { ReleaseDraft(); return; }
        showParameters = EditorGUILayout.Foldout(showParameters, "样式参数（草稿）", true);
        if (!showParameters) return;
        bool changed = draft != null && EditorJsonUtility.ToJson(draft) != draftBaseline;
        string snapshot = resolved == null ? "default" : EditorJsonUtility.ToJson(resolved);
        if (draft == null || draftTarget != applier || draftKey != applier.StyleKey || sourcePreset != resolved ||
            (!changed && snapshot != sourceSnapshot))
        {
            ReleaseDraft();
            sourcePreset = resolved;
            draftTarget = applier;
            draftKey = applier.StyleKey;
            draft = resolved == null ? CreateInstance<FontStylePreset>() : Instantiate(resolved);
            draft.hideFlags = HideFlags.HideAndDontSave;
            draftBaseline = EditorJsonUtility.ToJson(draft);
            sourceSnapshot = snapshot;
            newKey = string.IsNullOrEmpty(draftKey) ? "NewStyle" : draftKey + "_Copy";
            saveProfile = ResolveSaveProfile(draftKey, resolved);
            TMPStyleScenePreview.SetDraft(applier, draft);
        }
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("当前预设", sourcePreset, typeof(FontStylePreset), false);
        if (changed && sourceSnapshot != snapshot)
            EditorGUILayout.HelpBox("来源预设已被其他操作修改。保存会用当前草稿覆盖；也可以另存为独立样式。", MessageType.Warning);
        var draftObject = new SerializedObject(draft);
        draftObject.Update();
        DrawPropertiesExcluding(draftObject, "m_Script");
        if (draftObject.ApplyModifiedProperties()) TMPStyleScenePreview.Refresh();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(sourcePreset == null))
                if (GUILayout.Button("保存预设"))
                {
                    TMPStylePresetAuthoring.Save(draft, sourcePreset);
                    sourceSnapshot = EditorJsonUtility.ToJson(sourcePreset);
                    draftBaseline = EditorJsonUtility.ToJson(draft);
                    authoringMessage = "预设已保存，引用它的文本会同步更新。";
                }
            if (GUILayout.Button("另存为新样式")) showSaveAs = !showSaveAs;
            if (GUILayout.Button("还原草稿")) { ReleaseDraft(); authoringMessage = "草稿已丢弃。"; Repaint(); return; }
        }
        if (showSaveAs)
        {
            saveProfile = (TMPFontProfile)EditorGUILayout.ObjectField("登记到 Profile", saveProfile, typeof(TMPFontProfile), false);
            newKey = EditorGUILayout.TextField("新样式 Key", newKey);
            bool duplicate = saveProfile != null && saveProfile.styles != null && saveProfile.styles.Any(e => e != null && e.key == newKey?.Trim());
            if (duplicate) EditorGUILayout.HelpBox("这个 Key 已存在，请填写新的 Key。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(saveProfile == null || string.IsNullOrWhiteSpace(newKey) || duplicate))
                if (GUILayout.Button("选择位置并另存为"))
                {
                    string directory = sourcePreset == null ? "Assets" : System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(sourcePreset)).Replace('\\', '/');
                    string path = EditorUtility.SaveFilePanelInProject("另存为独立样式", "Preset_" + newKey.Trim(), "asset", "保存新预设并登记新 Key", directory);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var copy = TMPStylePresetAuthoring.SaveAs(draft, applier, saveProfile, newKey, path);
                        EditorGUIUtility.PingObject(copy);
                        ReleaseDraft();
                        s_CachedKeys = null;
                        showSaveAs = false;
                        authoringMessage = "新预设和 Profile 已保存，当前文本已切换到新 Key；请保存 Prefab / 场景以保存绑定。";
                    }
                }
        }
        EditorGUILayout.HelpBox(authoringMessage ?? "调整仅预览当前文本的草稿。保存会更新共享预设；另存为保留原预设。未保存的草稿在切换对象/样式时丢弃。", MessageType.Info);
    }

    private static TMPFontProfile ResolveSaveProfile(string key, FontStylePreset preset)
    {
        if (TMPStyleScenePreview.Profile != null) return TMPStyleScenePreview.Profile;
        var profiles = AssetDatabase.FindAssets("t:TMPFontProfile").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMPFontProfile>).Where(p => p != null).ToArray();
        var matches = profiles.Where(p => p.styles != null && p.styles.Any(e => e != null && e.key == key) && p.GetPreset(key) == preset).ToArray();
        return matches.Length == 1 ? matches[0] : profiles.Length == 1 ? profiles[0] : null;
    }

    public static void DrawStyleKeyDropdown(SerializedProperty styleKeyProp, string label = "Style Key", Action repaint = null)
    {
        if (styleKeyProp == null)
            return;

        if (s_CachedKeys == null)
            RefreshCache();

        EditorGUILayout.BeginHorizontal();

        if (s_CachedKeys.Count == 0)
        {
            EditorGUILayout.PropertyField(styleKeyProp, new GUIContent(label));
            DrawRefreshButton(repaint);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("No TMPFontProfile assets found. Create and configure a profile first.", MessageType.Warning);
            return;
        }

        string[] options = new string[s_CachedKeys.Count + 1];
        options[0] = "-- Default Material --";
        for (int i = 0; i < s_CachedKeys.Count; i++)
            options[i + 1] = s_CachedKeys[i];

        string currentKey = styleKeyProp.stringValue;
        int currentIndex = string.IsNullOrEmpty(currentKey)
            ? 0
            : s_CachedKeys.IndexOf(currentKey) + 1;

        bool isOrphan = !string.IsNullOrEmpty(currentKey) && currentIndex == 0;
        if (isOrphan)
        {
            string[] warningOptions = options.Prepend($"Missing: {currentKey}").ToArray();
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 0.6f, 0.6f);
            int newIndex = EditorGUILayout.Popup(label, 0, warningOptions);
            if (newIndex > 0) styleKeyProp.stringValue = newIndex == 1 ? string.Empty : s_CachedKeys[newIndex - 2];
            GUI.color = previousColor;
        }
        else
        {
            string tooltip = currentIndex > 0 && s_KeySourceMap.TryGetValue(currentKey, out List<string> sources)
                ? "Defined in: " + string.Join(", ", sources)
                : "Use the font asset default material.";

            int newIndex = EditorGUILayout.Popup(new GUIContent(label, tooltip), currentIndex, options);
            if (newIndex != currentIndex) styleKeyProp.stringValue = newIndex == 0 ? string.Empty : s_CachedKeys[newIndex - 1];
        }

        DrawRefreshButton(repaint);
        EditorGUILayout.EndHorizontal();

        if (isOrphan)
            EditorGUILayout.HelpBox($"Style key '{currentKey}' was not found in any TMPFontProfile.", MessageType.Warning);
    }

    private static void DrawRefreshButton(Action repaint)
    {
        if (!GUILayout.Button("Refresh", GUILayout.Width(RefreshButtonWidth)))
            return;

        RefreshCache();
        repaint?.Invoke();
    }

    private static void RefreshCache()
    {
        s_KeySourceMap = new Dictionary<string, List<string>>();

        string[] guids = AssetDatabase.FindAssets("t:TMPFontProfile");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMPFontProfile profile = AssetDatabase.LoadAssetAtPath<TMPFontProfile>(path);
            if (profile == null || profile.styles == null)
                continue;

            foreach (var entry in profile.styles)
            {
                if (string.IsNullOrEmpty(entry.key))
                    continue;

                if (!s_KeySourceMap.TryGetValue(entry.key, out List<string> sources))
                {
                    sources = new List<string>();
                    s_KeySourceMap[entry.key] = sources;
                }

                sources.Add(profile.name);
            }
        }

        s_CachedKeys = s_KeySourceMap.Keys.OrderBy(key => key).ToList();
        TMPStyleScenePreview.RefreshProfiles();
    }
}
