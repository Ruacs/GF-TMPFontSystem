using UnityEngine;
using UnityEngine.UI;

namespace Lokas
{
    /// <summary>Generated, non-interactive Canvas layer. Does not own the TMP mesh.</summary>
    [AddComponentMenu(""), RequireComponent(typeof(CanvasRenderer))]
    public sealed class TMPArtLayerGraphic : MaskableGraphic
    {
        private Material m_OwnedMaterial;
        private Mesh m_SourceMesh;
        public Mesh SourceMesh => m_SourceMesh;
        public override Texture mainTexture => m_OwnedMaterial != null ? m_OwnedMaterial.mainTexture : s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        internal void SetSource(Material source, Shader shader, int layer, Material styleOverride = null, FontStylePreset preset = null)
        {
            if (m_OwnedMaterial == null)
            {
                m_OwnedMaterial = new Material(shader != null ? shader : source.shader) { hideFlags = HideFlags.HideAndDontSave };
                material = m_OwnedMaterial;
            }
            m_OwnedMaterial.CopyPropertiesFromMaterial(source);
            m_OwnedMaterial.shader = shader != null ? shader : source.shader;
            if (styleOverride != null)
            {
                m_OwnedMaterial.CopyPropertiesFromMaterial(styleOverride);
                m_OwnedMaterial.mainTexture = source.mainTexture;
                foreach (string property in new[] { "_GradientScale", "_TextureWidth", "_TextureHeight", "_WeightNormal", "_WeightBold" })
                    if (source.HasProperty(property)) m_OwnedMaterial.SetFloat(property, source.GetFloat(property));
            }
            if (shader != null)
            {
                if (preset != null) preset.ApplyTo(m_OwnedMaterial);
                m_OwnedMaterial.SetFloat("_Layer", layer);
                if (m_OwnedMaterial.GetFloat("_OutlineWidth") > 0) m_OwnedMaterial.EnableKeyword("OUTLINE_ON");
                else m_OwnedMaterial.DisableKeyword("OUTLINE_ON");
            }
            // Recreate the stencil derivative after a font/style change, otherwise an
            // existing Mask can retain the previous atlas and style in its material.
            RecalculateMasking();
        }

        internal void Draw(Mesh mesh, Color tint)
        {
            m_SourceMesh = mesh;
            canvasRenderer.SetColor(tint);
            UpdateMaterial();
            UpdateGeometry();
        }

        protected override void UpdateGeometry()
        {
            canvasRenderer.SetMesh(m_SourceMesh);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_OwnedMaterial == null) return;
            if (Application.isPlaying) Destroy(m_OwnedMaterial);
            else DestroyImmediate(m_OwnedMaterial);
        }
    }
}
