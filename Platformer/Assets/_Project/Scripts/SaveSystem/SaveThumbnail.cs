using System.IO;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Captures and stores the little screenshot shown on a save slot.
    ///
    /// Thumbnails live beside the save file in persistentDataPath, not in Resources —
    /// Resources is baked at build time and cannot be written to at runtime, so the
    /// existing profile1/2/3.png in there can only ever be placeholders.
    /// </summary>
    public static class SaveThumbnail
    {
        public const int Width = 384;
        public const int Height = 216;
        const string FileName = "thumbnail.png";

        public static string PathFor(string profileId)
        {
            return Path.Combine(Application.persistentDataPath, profileId, FileName);
        }

        /// <summary>Renders a camera to an off-screen target and returns it as PNG bytes.
        /// Rendering deliberately rather than grabbing the framebuffer: that way HUD and
        /// menu layers can be excluded, so the shot is of the world, not of the pause screen
        /// the player was looking at when they saved.</summary>
        public static byte[] Capture(Camera source, LayerMask excludeLayers)
        {
            if (source == null) return null;

            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = source.targetTexture;
            int previousMask = source.cullingMask;
            RenderTexture previousActive = RenderTexture.active;

            Texture2D shot = null;
            try
            {
                source.targetTexture = target;
                source.cullingMask = previousMask & ~excludeLayers.value;
                source.Render();

                RenderTexture.active = target;
                shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply();

                return shot.EncodeToPNG();
            }
            finally
            {
                // Restore before anything else can render through this camera, or the next
                // frame draws into our temporary target.
                source.targetTexture = previousTarget;
                source.cullingMask = previousMask;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                if (shot != null) Object.Destroy(shot);
            }
        }

        public static void Write(string profileId, byte[] png)
        {
            if (string.IsNullOrEmpty(profileId) || png == null) return;

            try
            {
                string path = PathFor(profileId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, png);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveThumbnail] Could not write thumbnail for '{profileId}': {e.Message}");
            }
        }

        /// <summary>Returns null when the profile has no thumbnail yet, so callers can fall
        /// back to a placeholder rather than showing a blank frame.</summary>
        public static Texture2D Load(string profileId)
        {
            string path = PathFor(profileId);
            if (!File.Exists(path)) return null;

            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (texture.LoadImage(File.ReadAllBytes(path)))
                {
                    texture.wrapMode = TextureWrapMode.Clamp;
                    return texture;
                }
                Object.Destroy(texture);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveThumbnail] Could not read thumbnail for '{profileId}': {e.Message}");
            }
            return null;
        }

        public static void Delete(string profileId)
        {
            string path = PathFor(profileId);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
