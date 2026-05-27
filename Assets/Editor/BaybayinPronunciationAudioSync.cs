using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class BaybayinPronunciationAudioSync
{
    private static readonly string[] SourceAudioFolders =
    {
        "Assets/Audio/BaybayinSounds",
        "Assets/Audio/Pronunciation",
    };

    private const string ConvertedAudioFolder = "Assets/Audio/Pronunciation";
    private static readonly string[] AssignableAudioFolders =
    {
        "Assets/Audio/Pronunciation",
        "Assets/Audio/BaybayinSounds",
    };

    [MenuItem("Salinlahi/Audio/Sync Baybayin Pronunciation Clips")]
    public static void SyncAll()
    {
        int converted = ConvertMp4SourcesToWav();
        int assigned = AssignPronunciationClips();
        Debug.Log($"BaybayinPronunciationAudioSync: converted={converted}, assigned={assigned}");
    }

    public static int ConvertMp4SourcesToWav()
    {
        EnsureDirectoryExists(ConvertedAudioFolder);

        int convertedCount = 0;
        bool changed = false;
        foreach (string sourceFolder in SourceAudioFolders)
        {
            string absoluteSourceFolder = ToAbsolutePath(sourceFolder);
            if (!Directory.Exists(absoluteSourceFolder))
                continue;

            foreach (string sourceFile in Directory.GetFiles(absoluteSourceFolder, "*.mp4", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileNameWithoutExtension(sourceFile);
                string outputAssetPath = $"{ConvertedAudioFolder}/{fileName}.wav";
                string outputAbsolutePath = ToAbsolutePath(outputAssetPath);

                if (!NeedsConversion(sourceFile, outputAbsolutePath))
                    continue;

                if (!TryConvertWithFfmpeg(sourceFile, outputAbsolutePath))
                    continue;

                convertedCount++;
                changed = true;
            }
        }

        if (changed)
            AssetDatabase.Refresh();

        return convertedCount;
    }

    public static int AssignPronunciationClips()
    {
        Dictionary<string, AudioClip> clipMap = BuildClipMap();
        if (clipMap.Count == 0)
            return 0;

        string[] characterGuids = AssetDatabase.FindAssets("t:BaybayinCharacterSO", new[] { "Assets/ScriptableObjects/Characters" });
        int assignedCount = 0;
        bool changedAny = false;

        foreach (string guid in characterGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BaybayinCharacterSO character = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(path);
            if (character == null)
                continue;

            string canonicalId = BaybayinIdCanonicalizer.Canonicalize(character.characterID);
            if (string.IsNullOrWhiteSpace(canonicalId))
                continue;

            if (!clipMap.TryGetValue(canonicalId, out AudioClip clip))
                continue;

            if (character.pronunciationClip == clip)
                continue;

            Undo.RecordObject(character, "Assign Baybayin Pronunciation Clip");
            character.pronunciationClip = clip;
            EditorUtility.SetDirty(character);
            assignedCount++;
            changedAny = true;
        }

        if (changedAny)
            AssetDatabase.SaveAssets();

        return assignedCount;
    }

    private static Dictionary<string, AudioClip> BuildClipMap()
    {
        var clipMap = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", AssignableAudioFolders);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                continue;

            string canonicalId = BaybayinIdCanonicalizer.Canonicalize(Path.GetFileNameWithoutExtension(path));
            if (string.IsNullOrWhiteSpace(canonicalId))
                continue;

            if (!clipMap.ContainsKey(canonicalId))
                clipMap.Add(canonicalId, clip);
        }

        return clipMap;
    }

    private static bool TryConvertWithFfmpeg(string inputPath, string outputPath)
    {
        string ffmpegExe = FindFfmpegExecutable();
        if (string.IsNullOrWhiteSpace(ffmpegExe))
        {
            Debug.LogWarning(
                $"BaybayinPronunciationAudioSync: Skipping conversion for '{inputPath}'. ffmpeg not found on PATH.");
            return false;
        }

        EnsureDirectoryExists(Path.GetDirectoryName(outputPath));

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = $"-y -i \"{inputPath}\" -vn -ar 44100 -ac 1 \"{outputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            }
        };

        process.Start();
        process.WaitForExit();

        if (process.ExitCode == 0 && File.Exists(outputPath))
            return true;

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        Debug.LogWarning(
            $"BaybayinPronunciationAudioSync: ffmpeg failed for '{inputPath}' with exit code {process.ExitCode}.\n{output}\n{error}");
        return false;
    }

    private static bool NeedsConversion(string inputPath, string outputPath)
    {
        if (!File.Exists(outputPath))
            return true;

        DateTime sourceWrite = File.GetLastWriteTimeUtc(inputPath);
        DateTime outputWrite = File.GetLastWriteTimeUtc(outputPath);
        return sourceWrite > outputWrite;
    }

    private static string FindFfmpegExecutable()
    {
        string[] candidates =
        {
            "ffmpeg",
            "ffmpeg.exe",
        };

        foreach (string candidate in candidates)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = "-version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };
                process.Start();
                process.WaitForExit(2000);
                if (!process.HasExited)
                    process.Kill();

                if (process.ExitCode == 0)
                    return candidate;
            }
            catch
            {
                // Ignore and continue to next candidate.
            }
        }

        return null;
    }

    private static void EnsureDirectoryExists(string assetPathOrDirectory)
    {
        if (string.IsNullOrWhiteSpace(assetPathOrDirectory))
            return;

        string absolutePath = assetPathOrDirectory;
        if (assetPathOrDirectory.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            absolutePath = ToAbsolutePath(assetPathOrDirectory);

        if (!Directory.Exists(absolutePath))
            Directory.CreateDirectory(absolutePath);
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(projectRoot ?? string.Empty, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}

public sealed class BaybayinPronunciationAudioPostprocessor : AssetPostprocessor
{
    private static bool s_isSyncRunning;

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (s_isSyncRunning)
            return;

        if (!HasBaybayinMp4Change(importedAssets) && !HasBaybayinMp4Change(movedAssets))
            return;

        try
        {
            s_isSyncRunning = true;
            BaybayinPronunciationAudioSync.SyncAll();
        }
        finally
        {
            s_isSyncRunning = false;
        }
    }

    private static bool HasBaybayinMp4Change(string[] assetPaths)
    {
        if (assetPaths == null || assetPaths.Length == 0)
            return false;

        for (int i = 0; i < assetPaths.Length; i++)
        {
            string assetPath = assetPaths[i];
            if (!assetPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                continue;

            if (assetPath.StartsWith("Assets/Audio/BaybayinSounds", StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith("Assets/Audio/Pronunciation", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
