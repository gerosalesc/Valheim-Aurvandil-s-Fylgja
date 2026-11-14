using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Loads and caches custom UI icons for Star Platinum.
    /// Supports embedded assembly resources and override files on disk.
    /// </summary>
    internal static class IconManager
    {
        private static Sprite s_starPlatinumIcon;
        private static Texture2D s_bossStoneTexture;

        public static Sprite StarPlatinumIcon
        {
            get
            {
                if (s_starPlatinumIcon == null)
                {
                    s_starPlatinumIcon = LoadStarPlatinumIcon();
                }
                return s_starPlatinumIcon;
            }
        }

        public static Texture2D BossStoneTexture
        {
            get
            {
                if (s_bossStoneTexture == null)
                {
                    s_bossStoneTexture = LoadTextureResource(
                        "bossstone_starplatinum.png",
                        "GP_StarPlatinum_BossStoneTex",
                        new[]
                        {
                            "bossstone_starplatinum.png",
                            Path.Combine("resources", "bossstone_starplatinum.png")
                        });
                }
                return s_bossStoneTexture;
            }
        }

        private static Sprite LoadStarPlatinumIcon()
        {
            try
            {
                byte[] pngBytes = null;

                // 1. Check disk in plugins folder
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(dllDir))
                {
                    string[] candidates = {
                        "icon_starplatinum.png",
                        "starplatinum_icon.png",
                        "star_platinum.png",
                        Path.Combine("resources", "icon_starplatinum.png")
                    };
                    foreach (var name in candidates)
                    {
                        string path = Path.Combine(dllDir, name);
                        if (File.Exists(path))
                        {
                            pngBytes = File.ReadAllBytes(path);
                            Plugin.Log?.LogInfo($"IconManager: Loaded icon from disk: {path}");
                            break;
                        }
                    }
                }

                // 2. Fallback to embedded resource in DLL
                if (pngBytes == null)
                {
                    var asm = Assembly.GetExecutingAssembly();
                    foreach (var res in asm.GetManifestResourceNames())
                    {
                        if (res.EndsWith("icon_starplatinum.png", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var stream = asm.GetManifestResourceStream(res))
                            {
                                if (stream != null)
                                {
                                    pngBytes = new byte[stream.Length];
                                    stream.Read(pngBytes, 0, pngBytes.Length);
                                    Plugin.Log?.LogInfo($"IconManager: Loaded embedded icon: {res}");
                                    break;
                                }
                            }
                        }
                    }
                }

                if (pngBytes != null && pngBytes.Length > 0)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    var method = typeof(ImageConversion).GetMethod("LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]) });
                    if (method != null)
                    {
                        method.Invoke(null, new object[] { tex, pngBytes });
                        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        sprite.name = "GP_StarPlatinum_Icon";
                        return sprite;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"IconManager: Failed to load icon: {ex.Message}");
            }

            return null;
        }

        private static Texture2D LoadTextureResource(string embeddedSuffix, string texName, string[] diskCandidates)
        {
            try
            {
                byte[] pngBytes = null;

                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(dllDir) && diskCandidates != null)
                {
                    foreach (var name in diskCandidates)
                    {
                        string path = Path.Combine(dllDir, name);
                        if (File.Exists(path))
                        {
                            pngBytes = File.ReadAllBytes(path);
                            Plugin.Log?.LogInfo($"IconManager: Loaded texture from disk: {path}");
                            break;
                        }
                    }
                }

                if (pngBytes == null)
                {
                    var asm = Assembly.GetExecutingAssembly();
                    foreach (var res in asm.GetManifestResourceNames())
                    {
                        if (res.EndsWith(embeddedSuffix, StringComparison.OrdinalIgnoreCase))
                        {
                            using (var stream = asm.GetManifestResourceStream(res))
                            {
                                if (stream != null)
                                {
                                    pngBytes = new byte[stream.Length];
                                    stream.Read(pngBytes, 0, pngBytes.Length);
                                    Plugin.Log?.LogInfo($"IconManager: Loaded embedded texture: {res}");
                                    break;
                                }
                            }
                        }
                    }
                }

                if (pngBytes != null && pngBytes.Length > 0)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    var method = typeof(ImageConversion).GetMethod("LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]) });
                    if (method != null)
                    {
                        method.Invoke(null, new object[] { tex, pngBytes });
                        tex.name = texName;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Bilinear;
                        return tex;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"IconManager: Failed to load texture '{embeddedSuffix}': {ex.Message}");
            }

            return null;
        }
    }
}
