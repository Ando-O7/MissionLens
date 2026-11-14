using System.IO;
using UnityEditor;
using UnityEngine;

namespace MissionLens.EditorTools
{
    /// <summary>
    /// Import settings for the data written by tools/build_unity_data.py. The images are NASA data
    /// laid out on known grids, so nothing may be rescaled to a power of two or cropped.
    /// </summary>
    public class MLImport : AssetPostprocessor
    {
        const string Root = "Assets/MissionLens/Resources/ML/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var name = Path.GetFileNameWithoutExtension(assetPath);
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.isReadable = false;

            int max = 2048; bool mips = false; var format = TextureImporterFormat.ASTC_6x6; var filter = FilterMode.Bilinear; int aniso = 1;
            // Compressed textures with mipmaps must be power-of-two here (otherwise Unity falls back to RGBA32), so the
            // export stretches ortho to a square; UVs are normalised, so the mapping is unchanged. The panoramas stay
            // at their own size without mipmaps: they are seen at about one texel per display pixel.
            if (name == "ortho") { max = 4096; mips = true; aniso = 8; }
            else if (name.StartsWith("pano_")) { max = 8192; aniso = 2; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; }
            else if (name == "slope") { max = 2048; format = TextureImporterFormat.RGBA32; }
            else if (name.StartsWith("crism")) { max = 128; format = TextureImporterFormat.RGBA32; filter = FilterMode.Point; }  // show the 18 m cells as they are
            ti.maxTextureSize = max; ti.mipmapEnabled = mips; ti.filterMode = filter; ti.anisoLevel = aniso;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = max, format = format, compressionQuality = 50 });
        }
    }
}
