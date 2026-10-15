using UnityEngine;

namespace IntoTheDark.Lighting
{
    /// <summary>
    /// Fait varier la lueur (émission) d'un objet sans dupliquer son matériau.
    /// </summary>
    public sealed class EmissionControl
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly Renderer renderer;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        public Color BaseEmission { get; }

        public EmissionControl(Renderer renderer)
        {
            this.renderer = renderer;
            var material = renderer != null ? renderer.sharedMaterial : null;
            BaseEmission = material != null && material.HasProperty(EmissionColorId)
                ? material.GetColor(EmissionColorId)
                : Color.black;
        }

        /// <summary>0 = éteint, 1 = lueur d'origine.</summary>
        public void SetIntensity(float factor)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, BaseEmission * Mathf.Max(0f, factor));
            renderer.SetPropertyBlock(block);
        }
    }
}
