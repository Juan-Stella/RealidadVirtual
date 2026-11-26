using UnityEngine;

namespace FrankaVR.V6
{
    /// <summary>
    /// Texto de interfaz nitido con TextMesh, sin paquetes extra:
    /// - los glifos se rasterizan siempre a <see cref="FontSize"/> y se compensa el tamano con
    ///   characterSize, asi el texto ocupa lo mismo en el mundo que antes;
    /// - un material compartido con el shader FrankaVR/UIText (Resources), que promedia 4 muestras
    ///   por pixel: la fuente dinamica no tiene mipmaps y sin esto el texto chico se ve borroso.
    /// Si el shader no esta, el texto queda con el material de la fuente (igual que antes).
    /// </summary>
    public static class CrispTextV6
    {
        public const int FontSize = 64;
        static Material _material;
        static Font _font;

        /// <summary>Configura tamano y material. fontSize/characterSize son los de antes.</summary>
        public static TextMesh Setup(TextMesh mesh, int fontSize, float characterSize)
        {
            if (mesh == null) return null;
            mesh.fontSize = FontSize;
            mesh.characterSize = characterSize * fontSize / FontSize;
            Apply(mesh);
            return mesh;
        }

        public static void Apply(TextMesh mesh)
        {
            Renderer renderer = mesh != null ? mesh.GetComponent<Renderer>() : null;
            if (renderer == null) return;
            if (_material == null)
            {
                Shader shader = Resources.Load<Shader>("FrankaVRUIText");
                Material fontMaterial = mesh.font != null ? mesh.font.material : renderer.sharedMaterial;
                if (shader == null || fontMaterial == null) return;
                _font = mesh.font;
                _material = new Material(shader) { name = "FrankaVR_UI_Text" };
                _material.mainTexture = fontMaterial.mainTexture;
                Font.textureRebuilt -= OnFontRebuilt;
                Font.textureRebuilt += OnFontRebuilt;
            }
            renderer.sharedMaterial = _material;
        }

        // La fuente dinamica agranda su atlas cuando aparecen glifos nuevos: seguir la textura nueva.
        static void OnFontRebuilt(Font font)
        {
            if (_material == null || font == null || font.material == null) return;
            if (_font != null && font != _font) return;
            _material.mainTexture = font.material.mainTexture;
        }
    }
}
