using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lokas.Editor.FontCharset
{
    internal static class TMPStylePresetAuthoring
    {
        internal static void Save(FontStylePreset draft, FontStylePreset destination)
        {
            if (draft == null || destination == null || !EditorUtility.IsPersistent(destination))
                throw new InvalidOperationException("请选择已有预设，默认材质请使用另存为。");
            Undo.RecordObject(destination, "Save TMP style preset");
            string name = destination.name;
            HideFlags flags = destination.hideFlags;
            EditorUtility.CopySerialized(draft, destination);
            destination.name = name;
            destination.hideFlags = flags;
            EditorUtility.SetDirty(destination);
            AssetDatabase.SaveAssetIfDirty(destination);
            TMPStyleScenePreview.Refresh();
        }

        internal static FontStylePreset SaveAs(FontStylePreset draft, TMPStyleApplier applier, TMPFontProfile profile, string key, string path)
        {
            key = key?.Trim();
            if (draft == null || applier == null || profile == null || !EditorUtility.IsPersistent(profile) || string.IsNullOrEmpty(key))
                throw new InvalidOperationException("请设置新样式 Key 和目标 Profile。");
            if (profile.styles != null && profile.styles.Any(entry => entry != null && entry.key == key))
                throw new InvalidOperationException("目标 Profile 已存在样式 Key: " + key);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("新预设必须保存到项目 Assets 下的 .asset 文件。");
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var copy = UnityEngine.Object.Instantiate(draft);
            copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
            copy.hideFlags = HideFlags.None;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create independent TMP style");
            AssetDatabase.CreateAsset(copy, path);
            Undo.RegisterCreatedObjectUndo(copy, "Create TMP style preset");
            Undo.RecordObject(profile, "Register TMP style key");
            if (profile.styles == null) profile.styles = new System.Collections.Generic.List<TMPFontProfile.StyleEntry>();
            profile.styles.Add(new TMPFontProfile.StyleEntry { key = key, preset = copy });
            EditorUtility.SetDirty(profile);
            var serialized = new SerializedObject(applier);
            serialized.FindProperty("_styleKey").stringValue = key;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(copy);
            AssetDatabase.SaveAssetIfDirty(profile);
            Undo.CollapseUndoOperations(group);
            if (TMPStyleScenePreview.Profile != null && TMPStyleScenePreview.Profile != profile) TMPStyleScenePreview.Profile = profile;
            TMPStyleScenePreview.Refresh();
            return copy;
        }
    }
}
