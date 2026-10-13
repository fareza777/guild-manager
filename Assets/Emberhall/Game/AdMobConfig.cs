using UnityEngine;

namespace Emberhall
{
    [CreateAssetMenu(menuName = "Emberhall/AdMob configuration")]
    public sealed class AdMobConfig : ScriptableObject
    {
        public bool enableAds = true;
        public bool useTestIds = true;
        public string androidAppId = "", androidBanner = "", androidInterstitial = "", androidRewarded = "";
        public string iosAppId = "", iosBanner = "", iosInterstitial = "", iosRewarded = "";
        public string privacyPolicyUrl = "";
        public string BannerId => useTestIds ? (IOS ? "ca-app-pub-3940256099942544/2435281174" : "ca-app-pub-3940256099942544/9214589741") : (IOS ? iosBanner : androidBanner);
        public string InterstitialId => useTestIds ? (IOS ? "ca-app-pub-3940256099942544/4411468910" : "ca-app-pub-3940256099942544/1033173712") : (IOS ? iosInterstitial : androidInterstitial);
        public string RewardedId => useTestIds ? (IOS ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-3940256099942544/5224354917") : (IOS ? iosRewarded : androidRewarded);
        public bool HasUnitIds => !string.IsNullOrWhiteSpace(BannerId) && !string.IsNullOrWhiteSpace(InterstitialId) && !string.IsNullOrWhiteSpace(RewardedId);
        public static bool IOS => Application.platform == RuntimePlatform.IPhonePlayer;
        public static AdMobConfig Load() => Resources.Load<AdMobConfig>("AdMobConfig") ?? CreateInstance<AdMobConfig>();
    }
}
