using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Hausi
{
    /// <summary>
    /// PNGs off disk, turned into Sprites.
    ///
    /// Valheim builds both its item icons and its piece icons from a camera rig in
    /// the editor, and there is no editor at runtime - so a rendered PNG beside the
    /// dll and Sprite.Create over it is the whole of the answer. This started life
    /// private inside HeartwoodPrefab; the sapling needed exactly the same thing for
    /// Piece.m_icon, and two copies of a reflection lookup is one too many.
    ///
    /// The stowing post kept a third copy while it was its own mod, on the reasoning that
    /// reaching into a sibling repo for one file would reverse which mod knows about which.
    /// There is no sibling repo now, so that copy is this one.
    /// </summary>
    internal static class Icons
    {
        /// <summary>
        /// Reads a PNG beside the dll. Null if it is missing or unreadable, and the
        /// caller is expected to carry on without it rather than fail - an icon is
        /// the most cosmetic thing in the mod, and the donor's own picture is a poor
        /// but survivable fallback.
        /// </summary>
        public static Sprite Load(string fileName, string label)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            var directory = Path.GetDirectoryName(typeof(Icons).Assembly.Location);
            var path = Path.Combine(directory, fileName);

            if (!File.Exists(path))
            {
                HausiPlugin.LogOnce(
                    "No " + fileName + " beside the dll - " + label + " will use the "
                    + "donor's icon, which is someone else's picture.");
                return null;
            }

            try
            {
                // Bilinear, not point, even though everything else in these mods wants
                // point. The source is 128px and the inventory draws it smaller, so it
                // is always being minified, and point-sampling a minified image is how
                // you get a shimmering mess as the slot moves.
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                if (!LoadPng(texture, File.ReadAllBytes(path))) return null;

                texture.name = label + "_icon";
                texture.hideFlags = HideFlags.HideAndDontSave;

                return Sprite.Create(texture,
                                     new Rect(0f, 0f, texture.width, texture.height),
                                     new Vector2(0.5f, 0.5f));
            }
            catch (System.Exception e)
            {
                HausiPlugin.Log.LogError("Could not read " + fileName + ": " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// The icon that goes with a model file: stow_post_rack.obj ->
        /// stow_post_rack_icon.png.
        ///
        /// Derived rather than configured, deliberately, even though this repo's rule is
        /// to make filenames settings. The model file is already a setting, and a second
        /// one for the icon could only ever disagree with it - a post wearing the rack and
        /// showing a picture of the barrow is worse than either, and is exactly the sort
        /// of mismatch that takes an hour to notice.
        /// </summary>
        public static string For(string modelFile)
        {
            if (string.IsNullOrEmpty(modelFile)) return null;

            var stem = Path.GetFileNameWithoutExtension(modelFile);
            return string.IsNullOrEmpty(stem) ? null : stem + "_icon.png";
        }

        /// <summary>
        /// Texture2D.LoadImage, by reflection.
        ///
        /// It lives in UnityEngine.ImageConversionModule, which targets netstandard 2.1
        /// while this builds against net462 - referencing it outright fails the build
        /// with CS1705. The method is present at runtime regardless, so reaching it
        /// this way costs one lookup and removes the whole problem.
        /// </summary>
        private static bool LoadPng(Texture2D texture, byte[] data)
        {
            var type = AccessTools.TypeByName("UnityEngine.ImageConversion");
            if (type == null)
            {
                HausiPlugin.LogOnce("UnityEngine.ImageConversion is missing - "
                                    + "cannot read icons.");
                return false;
            }

            var method = AccessTools.Method(type, "LoadImage",
                                            new[] { typeof(Texture2D), typeof(byte[]) })
                         ?? AccessTools.Method(type, "LoadImage",
                                               new[] { typeof(Texture2D), typeof(byte[]),
                                                       typeof(bool) });

            if (method == null)
            {
                HausiPlugin.LogOnce("No LoadImage overload found on "
                                    + "UnityEngine.ImageConversion.");
                return false;
            }

            var args = method.GetParameters().Length == 3
                ? new object[] { texture, data, false }
                : new object[] { texture, data };

            return (bool)method.Invoke(null, args);
        }
    }
}
