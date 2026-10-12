using UnityEngine;

namespace ManagerKit.UI
{
    /// <summary>Thin bridge to platform features (share sheet, store rating, haptics). Safe no-ops in the editor.</summary>
    public static class Native
    {
        public static bool Vibration = true;

        public static void Share(string subject, string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.SUBJECT", subject);
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                    using (var cls = new AndroidJavaClass("android.content.Intent"))
                    using (var chooser = cls.CallStatic<AndroidJavaObject>("createChooser", intent, subject))
                    using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                        activity.Call("startActivity", chooser);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("Share failed: " + e.Message); GUIUtility.systemCopyBuffer = text; }
#else
            GUIUtility.systemCopyBuffer = text;
            Debug.Log("[Share] " + subject + "\n" + text);
#endif
        }

        public static string StoreUrl => "https://play.google.com/store/apps/details?id=" + Application.identifier;

        public static void Rate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { Application.OpenURL("market://details?id=" + Application.identifier); }
            catch { Application.OpenURL(StoreUrl); }
#else
            Application.OpenURL(StoreUrl);
#endif
        }

        public static void OpenUrl(string url) { if (!string.IsNullOrEmpty(url)) Application.OpenURL(url); }

        public static void Haptic()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Vibration) Handheld.Vibrate();
#endif
        }
    }
}
