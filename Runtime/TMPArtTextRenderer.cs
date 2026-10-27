using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lokas
{
    /// <summary>
    /// Draws the final TMP geometry in whole-text order: Underlay -2, Outline -1, Face 0.
    /// The source TMP remains the sole owner of text, layout, font and material settings.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    public sealed class TMPArtTextRenderer : MonoBehaviour
    {
        [Tooltip("Assign the Lokas/UI/TMP Layered SDF shader so it is included in builds.")]
        [SerializeField] private Shader m_LayerShader;

        private TMP_Text m_Text;
        private FontStylePreset m_Preset;
        private Material m_PreviewMaterial;
        private bool m_Curve;
        private Behaviour m_LegacyLayers, m_LegacyCurve;
        private float m_Radius = 2000f;
        private readonly List<TMPArtLayerGraphic> m_Graphics = new List<TMPArtLayerGraphic>();
        private readonly List<TMP_SubMeshUI> m_SubMeshes = new List<TMP_SubMeshUI>();
        private RectTransform m_LayerRoot;
        private bool m_Dirty = true;

        private void Reset()
        {
            m_LayerShader = Resources.Load<Shader>("TMPArtLayeredSDF");
        }

        private void OnEnable()
        {
            m_Text = GetComponent<TMP_Text>();
            m_Text.OnPreRenderText += ModifyGeometry;
            // Initialize the registry before subscribing: copy AFTER TMP's Canvas rebuild,
            // including all OnPreRenderText modifiers, independent of component order.
            CanvasUpdateRegistry.IsRebuildingGraphics();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Canvas.willRenderCanvases += RenderLayers;
            m_Dirty = true;
            m_Text.SetVerticesDirty();
        }

        private void OnValidate()
        {
            m_Dirty = true;
            if (m_Text != null) m_Text.SetVerticesDirty();
        }

        private void OnTextChanged(Object changed)
        {
            if (changed == m_Text) m_Dirty = true;
        }

        public void Configure(FontStylePreset preset, Material previewMaterial, bool curve, float strength, bool down)
        {
            m_Preset = preset;
            m_LegacyLayers = m_LegacyCurve = null;
            foreach (var component in GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                if (component.GetType().Name == "TMPLayeredText") m_LegacyLayers = component;
                if (component.GetType().Name == "TMPCurvedText") m_LegacyCurve = component;
            }
            m_PreviewMaterial = previewMaterial;
            m_Curve = curve && strength > 0.001f;
            m_Radius = Mathf.Max(0.001f, strength) * 100f * (down ? -1f : 1f);
            m_LayerShader = Resources.Load<Shader>("TMPArtLayeredSDF");
            m_Dirty = true;
            m_Text.SetVerticesDirty();
        }

        private void ModifyGeometry(TMP_TextInfo info)
        {
            if (info == null || info.characterCount == 0) return;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                var vertices = info.meshInfo[c.materialReferenceIndex].vertices;
                for (int k = 0; k < 4; k++)
                {
                    var v = vertices[c.vertexIndex + k];
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
            }
            if (minX > maxX) return;
            float center = (minX + maxX) * 0.5f;
            bool curve = m_Curve && (m_LegacyCurve == null || !m_LegacyCurve.isActiveAndEnabled);
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                var mesh = info.meshInfo[c.materialReferenceIndex];
                for (int k = 0; k < 4; k++)
                {
                    int index = c.vertexIndex + k;
                    var v = mesh.vertices[index];
                    if (m_Preset != null && m_Preset.enableFaceGradient)
                        mesh.colors32[index] = (Color)mesh.colors32[index] * Color.Lerp(m_Preset.gradientBottom, m_Preset.gradientTop, Mathf.InverseLerp(minY, maxY, v.y));
                    if (curve)
                    {
                        float angle = (v.x - center) / m_Radius;
                        mesh.vertices[index] = new Vector3(center + Mathf.Sin(angle) * m_Radius, v.y + (Mathf.Cos(angle) - 1f) * m_Radius, v.z);
                    }
                }
            }
        }

        private void RenderLayers()
        {
            if (m_Preset == null || !m_Preset.enableLayeredRendering || !(m_Text is TextMeshProUGUI) ||
                (m_LegacyLayers != null && m_LegacyLayers.isActiveAndEnabled))
            {
                SetLayersActive(false);
                return;
            }
            if (m_Text == null || !m_Text.isActiveAndEnabled || m_Text.canvas == null || m_LayerShader == null)
            {
                SetLayersActive(false);
                return;
            }

            TMP_TextInfo info = m_Text.textInfo;
            if (info == null || info.characterCount == 0)
            {
                SetLayersActive(false);
                return;
            }

            if (m_LayerRoot == null)
            {
                m_LayerRoot = CreateChild("TMP generated layers (-2, -1, 0)", transform);
                m_Dirty = true;
            }
            SetLayersActive(true);
            // TMP may append fallback submeshes on a language change. Keep all generated
            // layers together; their order is effect-first, then font atlas.
            if (m_LayerRoot.GetSiblingIndex() != transform.childCount - 1) m_LayerRoot.SetAsLastSibling();
            AlignPivot(m_LayerRoot, m_Text.rectTransform.pivot);
            int count = info.materialCount;
            int needed = count * 3;
            while (m_Graphics.Count < needed)
            {
                RectTransform child = CreateChild("Layer", m_LayerRoot);
                m_Graphics.Add(child.gameObject.AddComponent<TMPArtLayerGraphic>());
                m_Dirty = true;
            }

            if (m_Dirty)
            {
                m_SubMeshes.Clear();
                m_Text.GetComponentsInChildren(true, m_SubMeshes);
            }
            Color tint = m_Text.canvasRenderer.GetColor();
            for (int i = 0; i < m_Graphics.Count; i++)
            {
                TMPArtLayerGraphic graphic = m_Graphics[i];
                if (i >= needed)
                {
                    graphic.gameObject.SetActive(false);
                    continue;
                }
                int layer = i / count - 2;
                int atlas = i % count;
                TMP_MeshInfo meshInfo = info.meshInfo[atlas];
                Material source = atlas == 0 && m_PreviewMaterial != null ? m_PreviewMaterial : meshInfo.material;
                bool sdf = source != null && source.HasProperty(ShaderUtilities.ID_GradientScale);
                bool visible = meshInfo.vertexCount > 0 && source != null && (sdf || layer == 0);
                if (graphic.gameObject.activeSelf != visible) graphic.gameObject.SetActive(visible);
                if (!visible) continue;
                AlignPivot(graphic.rectTransform, m_Text.rectTransform.pivot);
                graphic.maskable = m_Text.maskable;
                if (m_Dirty)
                {
                    graphic.name = (layer == -2 ? "Underlay -2" : layer == -1 ? "Outline -1" : "Face 0") + " / atlas " + atlas;
                    bool inherited = atlas == 0;
                    foreach (var sub in m_SubMeshes)
                        if (sub != null && sub.textComponent == m_Text && sub.sharedMaterial == meshInfo.material &&
                            (sub.isDefaultMaterial || sub.fallbackSourceMaterial == m_Text.fontSharedMaterial)) inherited = true;
                    graphic.SetSource(source, sdf ? m_LayerShader : null, layer,
                        sdf && atlas > 0 && inherited ? m_PreviewMaterial : null, inherited ? m_Preset : null);
                }
                graphic.Draw(meshInfo.mesh, tint);
            }

            // Hide only the source render submission, never its mesh/material/text data.
            // The same mesh is reused by all three layers and by existing curve scripts.
            m_Text.canvasRenderer.SetMesh(null);
            foreach (TMP_SubMeshUI sub in m_SubMeshes)
                if (sub != null && sub.textComponent == m_Text) sub.canvasRenderer.SetMesh(null);
            m_Dirty = false;
        }

        private static RectTransform CreateChild(string label, Transform parent)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = ((RectTransform)parent).pivot;
            return rect;
        }

        private static void AlignPivot(RectTransform rect, Vector2 pivot)
        {
            if (rect.pivot == pivot) return;
            rect.pivot = pivot;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void SetLayersActive(bool active)
        {
            if (m_LayerRoot != null && m_LayerRoot.gameObject.activeSelf != active)
                m_LayerRoot.gameObject.SetActive(active);
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= RenderLayers;
            if (m_Text != null) m_Text.OnPreRenderText -= ModifyGeometry;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            SetLayersActive(false);
            if (m_LayerRoot != null)
            {
                if (Application.isPlaying) Destroy(m_LayerRoot.gameObject);
                else DestroyImmediate(m_LayerRoot.gameObject);
            }
            m_LayerRoot = null;
            m_Graphics.Clear();
            m_SubMeshes.Clear();
            if (m_Text != null) m_Text.SetVerticesDirty();
        }
    }
}
