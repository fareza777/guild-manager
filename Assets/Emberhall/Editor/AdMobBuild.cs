using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Emberhall.EditorTools
{
    /// <summary>Keep SDK manifest app IDs and runtime ad units in one explicit mode.</summary>
    public sealed class AdMobBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) { Configure(); }
        [MenuItem("Emberhall/Configure AdMob")]
        public static void Configure()
        {
            const string path = "Assets/Resources/AdMobConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<AdMobConfig>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<AdMobConfig>(); AssetDatabase.CreateAsset(config, path); }
            const string sdkPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
            var sdk = AssetDatabase.LoadMainAssetAtPath(sdkPath);
            if (sdk == null) throw new BuildFailedException("Google Mobile Ads settings asset is missing.");
            string android = config.useTestIds && string.IsNullOrWhiteSpace(config.androidAppId) ? "ca-app-pub-3940256099942544~3347511713" : config.androidAppId;
            string ios = config.useTestIds && string.IsNullOrWhiteSpace(config.iosAppId) ? "ca-app-pub-3940256099942544~1458002511" : config.iosAppId;
            if (config.enableAds && !config.useTestIds)
            {
                bool iosBuild = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
                var appId = iosBuild ? ios : android;
                var units = iosBuild ? new[] { config.iosBanner, config.iosInterstitial, config.iosRewarded } : new[] { config.androidBanner, config.androidInterstitial, config.androidRewarded };
                if (string.IsNullOrWhiteSpace(appId) || !appId.Contains("~") || appId.Contains("3940256099942544"))
                    throw new BuildFailedException("Set your publisher AdMob app ID before disabling test mode.");
                foreach (var unit in units)
                    if (string.IsNullOrWhiteSpace(unit) || !unit.Contains("/") || unit.Contains("3940256099942544"))
                        throw new BuildFailedException("Set all three publisher ad unit IDs before disabling test mode.");
            }
            // Keep the other platform valid during Android-only setup, without requesting any live ads there.
            if (string.IsNullOrWhiteSpace(android)) android = "ca-app-pub-3940256099942544~3347511713";
            if (string.IsNullOrWhiteSpace(ios)) ios = "ca-app-pub-3940256099942544~1458002511";
            var so = new SerializedObject(sdk);
            so.FindProperty("adMobAndroidAppId").stringValue = android;
            so.FindProperty("adMobIOSAppId").stringValue = ios;
            so.FindProperty("enableGradleBuildPreProcessor").boolValue = true;
            so.FindProperty("userLanguage").stringValue = "en";
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(sdk); AssetDatabase.SaveAssets();
            Debug.Log("[GM] AdMob configured: " + (config.useTestIds ? "Google test IDs" : "publisher IDs"));
        }
    }
}
