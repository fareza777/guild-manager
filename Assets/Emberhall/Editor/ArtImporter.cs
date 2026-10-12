using UnityEditor;
using UnityEngine;

namespace Emberhall.EditorTools
{
    /// <summary>Pixel-art import rules: point filtering, no mips, no compression, readable (we slice sprite sheets at runtime).</summary>
    public class ArtImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 5;

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Art/") && !assetPath.Contains("/Resources/Brand/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.textureShape = TextureImporterShape.Texture2D;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = assetPath.Contains("/Art/Generated/") ? FilterMode.Bilinear : FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = !assetPath.Contains("/Art/Generated/") || assetPath.EndsWith("guild_icon.png");
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            ti.sRGBTexture = true;
        }
    }
}
