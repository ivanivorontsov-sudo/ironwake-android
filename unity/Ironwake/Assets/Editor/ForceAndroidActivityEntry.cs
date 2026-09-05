#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ForceAndroidActivityEntry : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        // Activity = 1 — ensures UnityPlayerActivity + MAIN/LAUNCHER in the APK.
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        Debug.Log("ForceAndroidActivityEntry: applicationEntry = Activity");
    }
}
#endif
