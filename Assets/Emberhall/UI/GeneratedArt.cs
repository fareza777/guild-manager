using System.Collections.Generic;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    /// <summary>Painted illustrations alongside the original animated pixel characters.</summary>
    public static class GeneratedArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var t = Resources.Load<Texture2D>("Art/Generated/" + name);
            if (!t) return null;
            t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100);
            cache[name] = s; return s;
        }

        public static Image Cover(RectTransform parent, string name, bool moving = false)
        {
            var crop = U.Rt(parent, "painted_scene").Fill();
            crop.gameObject.AddComponent<RectMask2D>();
            var s = Get(name);
            var img = U.Img(crop, s, Color.white, name); img.Fill();
            if (s)
            {
                var fit = img.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = s.rect.width / s.rect.height;
                if (moving)
                    Tw.To(14f, k => { if (img) img.transform.localScale = Vector3.one * Mathf.Lerp(1.02f, 1.085f, k); }, Ease.InOutSine, 0, null, img, true, true);
            }
            return img;
        }

        public static Sprite Item(int index)
        {
            string key = "item_" + index;
            if (cache.TryGetValue(key, out var s)) return s;
            var atlas = Get(index >= 12 ? "item_misc" : index >= 6 ? "item_arms" : "item_relics"); if (!atlas) return null;
            int cell = index % 6;
            var t = atlas.texture; int w = t.width / 3, h = t.height / 2;
            int col = cell % 3, row = cell / 3;
            s = Sprite.Create(t, new Rect(col * w, t.height - (row + 1) * h, w, h), new Vector2(.5f, .5f), 100);
            cache[key] = s; return s;
        }

        public static Sprite HallBuilding(int index)
        {
            if (index < 0 || index > 8) return null;
            string key = "hall_building_" + index;
            if (cache.TryGetValue(key, out var s)) return s;
            var atlas = Get("hall_buildings"); if (!atlas) return null;
            var t = atlas.texture;
            float w = t.width / 3f, h = t.height / 3f;
            int col = index % 3, row = index / 3;
            var rect = new Rect(col * w, t.height - (row + 1) * h, w, h);
            // The library spire reaches above its nominal cell. Split at the clear
            // alpha gap so it stays complete and never appears under the infirmary.
            float spirePadding = t.height * (26f / 1254f);
            if (index == 3) { rect.y += spirePadding; rect.height -= spirePadding; }
            else if (index == 6) rect.height += spirePadding;
            s = Sprite.Create(t, rect, new Vector2(.5f, 0f), 100);
            cache[key] = s; return s;
        }
    }
}
