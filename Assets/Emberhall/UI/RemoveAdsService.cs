using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Emberhall
{
    /// <summary>Google Play non-consumable entitlement that disables every ad placement.</summary>
    public sealed class RemoveAdsService : MonoBehaviour
    {
        public const string ProductId = "remove_ads";
        const string OwnedKey = "emberhall_remove_ads_owned_v1";
        StoreController controller;
        Product product;
        bool fetching, connected;
        App app;
        public bool Owned { get; private set; }
        public bool Supported => Application.platform == RuntimePlatform.Android && !Tour.Requested && !NativeAdCheck.Requested;
        public bool CanBuy => Supported && connected && !fetching && product != null && !Owned;
        public bool CanShowPurchase => Supported && !Owned;
        public string DisplayPrice => product?.metadata?.localizedPriceString ?? "$4.99 one-time";
        public event Action Changed;

        public void Bind(App owner)
        {
            app = owner;
            Owned = PlayerPrefs.GetInt(OwnedKey, 0) == 1;
            if (Supported) Initialize();
        }

        async void Initialize()
        {
            try
            {
                controller = UnityIAPServices.StoreController();
                controller.OnPurchasePending += OnPurchasePending;
                controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
                controller.OnStoreConnected += OnStoreConnected;
                controller.OnStoreDisconnected += OnStoreDisconnected;
                controller.OnProductsFetched += OnProductsFetched;
                controller.OnProductsFetchFailed += OnProductsFetchFailed;
                await controller.Connect();
                connected = true;
                FetchProducts();
            }
            catch (Exception e) { Debug.LogWarning("[IAP] Google Play connection unavailable: " + e.GetType().Name); }
        }

        void OnStoreConnected() { connected = true; FetchProducts(); }
        void OnStoreDisconnected(StoreConnectionFailureDescription details) { connected = false; Debug.LogWarning("[IAP] Store disconnected: " + details.message); Changed?.Invoke(); }
        void FetchProducts()
        {
            if (controller == null || fetching) return;
            fetching = true;
            controller.FetchProducts(new List<ProductDefinition> { new(ProductId, ProductType.NonConsumable) });
        }
        void OnProductsFetched(List<Product> products)
        {
            fetching = false;
            product = products?.FirstOrDefault(p => p.definition.id == ProductId);
            if (product != null && product.hasReceipt) SetOwned();
            Changed?.Invoke();
        }
        void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            fetching = false;
            Debug.LogWarning("[IAP] Product fetch failed: " + failure.FailureReason);
            Changed?.Invoke();
        }
        void OnPurchasePending(PendingOrder order)
        {
            var bought = order.CartOrdered.Items().FirstOrDefault()?.Product;
            if (bought == null || bought.definition.id != ProductId) return;
            SetOwned();
            controller.ConfirmPurchase(order);
        }
        void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed)
                Debug.LogWarning("[IAP] Purchase confirmation failed: " + failed.Details);
            Changed?.Invoke();
        }
        void SetOwned()
        {
            if (Owned) return;
            Owned = true; PlayerPrefs.SetInt(OwnedKey, 1); PlayerPrefs.Save();
            Changed?.Invoke();
            app?.SaveNow();
        }
        public void Buy()
        {
            if (!CanBuy) { Toasts.Show(StoryScript.Pick("Google Play is still connecting.", "Google Play masih menghubungkan.")); return; }
            controller.PurchaseProduct(product);
        }
        public void Restore()
        {
            if (controller == null || !connected) { Toasts.Show(StoryScript.Pick("Google Play is still connecting.", "Google Play masih menghubungkan.")); return; }
            controller.RestoreTransactions((ok, error) =>
            {
                if (!ok) Debug.LogWarning("[IAP] Restore failed: " + error);
                Changed?.Invoke();
                Toasts.Show(StoryScript.Pick(ok ? "Purchase restore requested." : "No purchase could be restored yet.", ok ? "Pemulihan pembelian diminta." : "Belum ada pembelian yang dapat dipulihkan."));
            });
        }
        void OnDestroy()
        {
            if (controller == null) return;
            controller.OnPurchasePending -= OnPurchasePending;
            controller.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            controller.OnStoreConnected -= OnStoreConnected;
            controller.OnStoreDisconnected -= OnStoreDisconnected;
            controller.OnProductsFetched -= OnProductsFetched;
            controller.OnProductsFetchFailed -= OnProductsFetchFailed;
        }
    }
}
