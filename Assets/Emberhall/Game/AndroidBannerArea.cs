using System;
using UnityEngine;

namespace Emberhall
{
    public static class AndroidBannerArea
    {
        static double next;
        static int[] metrics;
        static bool unavailable;
        public static bool Ready => Application.platform != RuntimePlatform.Android || metrics != null && metrics.Length == 7 && metrics[0] == 1;
        public static float ExtraPixels => metrics == null ? 0 : Mathf.Max(0, metrics[1] - Screen.safeArea.y);
        public static int NavigationPixels => metrics == null ? 0 : metrics[2];
        public static bool NavigationVisible => metrics != null && metrics[3] == 1;
        public static int BannerTop => metrics == null ? -1 : metrics[4];
        public static int BannerBottom => metrics == null ? -1 : metrics[5];
        public static int WindowBottom => metrics == null ? 0 : metrics[6];
        public static void Refresh(bool immediate = false)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (unavailable || !immediate && Time.realtimeSinceStartupAsDouble < next) return;
            next = Time.realtimeSinceStartupAsDouble + .25;
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var area = new AndroidJavaClass("com.pixelgamemaster.emberhall.EmberhallNavigation"))
                {
                    area.CallStatic("refresh", activity);
                    metrics = area.CallStatic<int[]>("getMetrics");
                }
            }
            catch (Exception error)
            {
                unavailable = true; // Leave banners hidden if native placement cannot be measured safely.
                Debug.LogWarning("[Ads] Navigation area unavailable: " + error.Message);
            }
#endif
        }
    }
}
