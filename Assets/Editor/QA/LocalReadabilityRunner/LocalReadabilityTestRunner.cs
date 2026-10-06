using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
internal static class LocalReadabilityTestRunner
{
    private static readonly string Folder = Path.GetFullPath("Temp/MobileReadability");
    private static TestRunnerApi _api;
    private static bool _requestRefreshed;
    static LocalReadabilityTestRunner()
    {
        EditorApplication.update += Poll;
        _api = ScriptableObject.CreateInstance<TestRunnerApi>();
        _api.RegisterCallbacks(new Results());
    }
    private static void Poll()
    {
        string input = Path.Combine(Folder, "tests-request.json");
        if (!File.Exists(input)) { _requestRefreshed = false; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!_requestRefreshed)
        {
            _requestRefreshed = true;
            AssetDatabase.Refresh();
            return;
        }
        try
        {
            Filter filter = JsonUtility.FromJson<Filter>(File.ReadAllText(input));
            File.Delete(input);
            _api.Execute(new ExecutionSettings(filter));
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(Folder, "tests-error.txt"), e.ToString()); Debug.LogException(e); }
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { File.WriteAllText(Path.Combine(Folder, "tests-status.txt"), "RUNNING"); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren && result.FailCount > 0)
                File.AppendAllText(Path.Combine(Folder, "tests-failures.txt"), result.Test.FullName + "\n" + result.Message + "\n" + result.StackTrace + "\n");
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            string mode = result.Test.TestMode.ToString();
            TestRunnerApi.SaveResultToFile(result, Path.Combine(Folder, "tests-" + mode + ".xml"));
            File.WriteAllText(Path.Combine(Folder, "tests-status.txt"), $"{result.ResultState}: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}");
        }
    }
}
