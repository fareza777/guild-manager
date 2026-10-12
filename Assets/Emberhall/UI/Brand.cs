using System.Collections.Generic;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    /// <summary>Procedural brand art (guild emblem, logo) so no extra image files are needed.</summary>
    public static class Brand
    {
        public const string Studio = "Pixel Game Master";
        public const string Title = "EMBERHALL";
        public const string Subtitle = "GUILD LEGENDS";
        static readonly Dictionary<string, Sprite> shields = new Dictionary<string, Sprite>();

        /// <summary>24x28 pixel shield with a flame emblem in the given heraldic colour.</summary>
        public static Sprite Shield(Color primary)
        {
            string key = ColorUtility.ToHtmlStringRGB(primary);
            if (shields.TryGetValue(key, out var s)) return s;
            var pc = ShieldCanvas(primary);
            s = pc.ToSprite(24f); s.name = "shield_" + key; shields[key] = s; return s;
        }

        public static PixelCanvas ShieldCanvas(Color primary)
        {
            var pc = new PixelCanvas(24, 28);
            Color32 main = primary, dark = C32.Darken(main, 0.45f), light = C32.Lighten(main, 0.35f), gold = new Color32(255, 211, 78, 255), gold2 = new Color32(217, 154, 30, 255), outl = new Color32(24, 14, 30, 255);
            pc.Poly(gold2, 1.5f, 26.5f, 22.5f, 26.5f, 22.5f, 12.5f, 12, 1.5f, 1.5f, 12.5f);
            pc.Poly(dark, 3, 25, 21, 25, 21, 13, 12, 3.5f, 3, 13);
            pc.Poly(main, 3, 25, 21, 25, 21, 15, 12, 4.5f, 3, 15);
            pc.Poly(light, 3, 25, 12, 25, 12, 19, 3, 19);
            pc.Rect(3, 22, 18, 1, gold);
            // flame
            pc.Poly(new Color32(255, 240, 200, 255), 12, 22, 15.5f, 16.5f, 16.5f, 12, 14, 8.5f, 10, 8.5f, 7.5f, 12, 8.5f, 16.5f);
            pc.Poly(new Color32(255, 160, 60, 255), 12, 19.5f, 14.3f, 15.5f, 15, 12, 13.5f, 9.5f, 10.5f, 9.5f, 9, 12, 9.8f, 15.5f);
            pc.Poly(gold, 12, 15.5f, 13.2f, 13, 12.8f, 10.6f, 11.2f, 10.6f, 10.8f, 13);
            pc.Outline(outl);
            return pc;
        }

        public static Image ShieldImg(Transform parent, Color primary, float height)
        {
            var i = U.Img(parent, Shield(primary), Color.white, "shield"); i.preserveAspect = true; i.Sz(height * 26f / 30f, height); return i;
        }

        /// <summary>Contained wordmark with a phoenix crest and daylight-readable lettering.</summary>
        public static RectTransform Logo(Transform parent, float scale = 1f)
        {
            var root = U.Rt(parent, "logo"); root.Sz(900 * scale, 310 * scale);
            var crest = U.Icon(root, GeneratedArt.Item(10) ?? Shield(Theme.Blue), 132 * scale);
            crest.At(.5f, 1, 0, -4, 132 * scale, 132 * scale, .5f, 1);
            var front = U.Txt(root, Title, (int)(126 * scale), Theme.Hex("#fff1bb"), TextAnchor.MiddleCenter, true);
            front.StretchX(0, 70 * scale, 146 * scale, 12, 12);
            var outline = front.GetComponent<Outline>(); if (outline) { outline.effectColor = Theme.Hex("#74532c"); outline.effectDistance = new Vector2(2, -2); }
            var sub = U.Txt(root, Subtitle, (int)(38 * scale), Theme.Hex("#ffffff"), TextAnchor.MiddleCenter, true);
            sub.StretchX(0, 20 * scale, 48 * scale, 36, 36);
            return root;
        }

        public static string ShareText => L.T("I'm building my guild in {0}! Recruit heroes, fight monsters and rise from Rank F to S. Join me: {1}", Title + ": " + "Guild Legends", Native.StoreUrl);
    }

    /// <summary>Places the pixel landscape so its horizon sits on a chosen screen line. Buildings below that line stand on the ground.</summary>
    public static class Land
    {
        public const float ImageHorizon = 0.50f;

        public static Image Backdrop(Transform parent, string name, float width, float height, float horizonY)
        {
            float imgH = Mathf.Max(height, horizonY / ImageHorizon);
            float imgW = imgH * (320f / 240f);
            if (imgW < width) { float k = width / imgW; imgW *= k; imgH *= k; }
            var bg = U.Img(parent, Art.Bg(name), Color.white, "land");
            var r = bg.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.sizeDelta = new Vector2(imgW, imgH);
            r.anchoredPosition = Vector2.zero;
            return bg;
        }

        public static Image Contact(Transform parent, float width)
        {
            var sh = U.Img(parent, Boxes.Glow, new Color(0f, 0f, 0f, 0.42f), "contact");
            sh.At(0.5f, 0f, 0f, 2f, width, Mathf.Max(22f, width * 0.16f), 0.5f, 0.5f);
            return sh;
        }
    }

    public static class Ambient
    {
        /// <summary>Drifting ember sparks that rise and fade. Cheap: N images with looping tweens.</summary>
        public static void Embers(RectTransform parent, int count, float width, float height, float size = 8)
        {
            for (int i = 0; i < count; i++)
            {
                var spark = LuxArt.Ember;
                var img = spark != null ? U.Img(parent, spark, Color.white, "ember") : U.Solid(parent, Color.white, "ember"); var r = img.R();
                float s = size * (spark != null ? 3.5f : 1f) * Random.Range(0.65f, 1.35f); r.sizeDelta = new Vector2(s, s);
                float x0 = Random.Range(-width / 2, width / 2), amp = Random.Range(20, 60), ph = Random.Range(0f, 6.28f), spd = Random.Range(7f, 14f), life = Random.Range(5f, 10f);
                var c1 = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.9f, 0.4f), Random.value);
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0);
                float delay = Random.Range(0f, life);
                Tw.To(life, k => { if (!r) return; float y = k * height; r.anchoredPosition = new Vector2(x0 + Mathf.Sin(k * spd + ph) * amp, y); float a = Mathf.Sin(k * Mathf.PI); var tint = spark != null ? Color.Lerp(Color.white, c1, 0.25f) : c1; img.color = new Color(tint.r, tint.g, tint.b, a * 0.92f); r.localRotation = Quaternion.Euler(0, 0, k * 80); }, Ease.Linear, delay, null, r, true);
            }
        }

        public static Image Flame(Transform parent, string strip, int w, int h, float scale, float fps = 7)
        {
            var img = U.Img(parent, null, Color.white, "fire");
            var frames = new[] { Art.Strip(strip, 0, w, h), Art.Strip(strip, 1, w, h), Art.Strip(strip, 2, w, h) };
            SpriteAnim.On(img, frames, fps, true, scale);
            return img;
        }
    }
}
