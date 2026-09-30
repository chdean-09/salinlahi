using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Saves QA Session captures to <c>QA/screenshots</c> as <c>level{NN}-{slug}.png</c>. A repeat
/// capture is numbered (<c>-2</c>, <c>-3</c>, ...) instead of overwriting earlier evidence. The image
/// is the Game view (or Device Simulator) at its render resolution, and Unity writes it at the end
/// of the frame, so the file may not exist yet when <see cref="Capture"/> returns.
/// </summary>
public static class QaScreenshotCapture
{
    public const string ScreenshotFolder = "QA/screenshots";
    private const string FallbackSlug = "capture";

    /// <summary>
    /// Requests the capture and returns its project-relative path, or null when the request
    /// could not be made.
    /// </summary>
    public static string Capture(int levelNumber, string slug)
    {
        try
        {
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), ScreenshotFolder);
            Directory.CreateDirectory(directory);
            string fileName = ResolveFileName(directory, levelNumber, slug);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, fileName));

            // Outside Play Mode nothing repaints the Game view on its own, and the capture is
            // only written when it renders.
            if (!EditorApplication.isPlaying)
                InternalEditorUtility.RepaintAllViews();

            return ScreenshotFolder + "/" + fileName;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }
    }

    /// <summary>
    /// The first unused <c>level{NN}-{slug}[-n].png</c> name in <paramref name="directory"/>.
    /// </summary>
    public static string ResolveFileName(string directory, int levelNumber, string slug)
    {
        string stem = $"level{levelNumber:00}-{SanitizeSlug(slug)}";
        string fileName = stem + ".png";
        for (int n = 2; File.Exists(Path.Combine(directory, fileName)); n++)
            fileName = $"{stem}-{n}.png";
        return fileName;
    }

    /// <summary>
    /// Lowercase letters and digits, with every other run of characters collapsed to one dash,
    /// so a typed slug is always a safe file name.
    /// </summary>
    public static string SanitizeSlug(string slug)
    {
        StringBuilder builder = new StringBuilder();
        bool pendingDash = false;
        foreach (char c in (slug ?? string.Empty).ToLowerInvariant())
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                if (pendingDash && builder.Length > 0) builder.Append('-');
                builder.Append(c);
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }
        return builder.Length > 0 ? builder.ToString() : FallbackSlug;
    }
}
