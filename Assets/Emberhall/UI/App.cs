using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Emberhall
{
    public static class Prefs
    {
        static string Key(string name) => Tour.Requested ? "visualcheck_" + name : name;
        public static float Sfx { get => PlayerPrefs.GetFloat(Key("sfx"), 0.9f); set => PlayerPrefs.SetFloat(Key("sfx"), value); }
        public static float Music { get => PlayerPrefs.GetFloat(Key("music"), 0.5f); set => PlayerPrefs.SetFloat(Key("music"), value); }
        public static bool Vibrate { get => PlayerPrefs.GetInt(Key("vib"), 1) == 1; set => PlayerPrefs.SetInt(Key("vib"), value ? 1 : 0); }
        public static bool Hints { get => PlayerPrefs.GetInt(Key("hints"), 1) == 1; set => PlayerPrefs.SetInt(Key("hints"), value ? 1 : 0); }
        public static bool BattleFx { get => PlayerPrefs.GetInt(Key("bfx"), 1) == 1; set => PlayerPrefs.SetInt(Key("bfx"), value ? 1 : 0); }
        public static int Fps { get => PlayerPrefs.GetInt(Key("fps"), 60); set => PlayerPrefs.SetInt(Key("fps"), value); }
        public static string Lang
        {
            get => NormalizeLanguage(PlayerPrefs.GetString(Key("lang"), "en"));
            set { PlayerPrefs.SetString(Key("lang"), NormalizeLanguage(value)); PlayerPrefs.SetInt(Key("english_default_v1"), 1); }
        }
        public static int BattleSpeed { get => PlayerPrefs.GetInt(Key("bspeed"), 1); set => PlayerPrefs.SetInt(Key("bspeed"), value); }
        public static bool Seen(string k) => PlayerPrefs.GetInt(Key("seen_" + k), 0) == 1;
        public static void MarkSeen(string k) => PlayerPrefs.SetInt(Key("seen_" + k), 1);
        public static string NormalizeLanguage(string language) => string.Equals(language?.Trim(), "id", StringComparison.OrdinalIgnoreCase) ? "id" : "en";
        public static string InitializeLanguage()
        {
            // Move both fresh and older installations to the requested English default
            // once. Subsequent explicit language choices remain saved.
            if (PlayerPrefs.GetInt(Key("english_default_v1"), 0) == 0) { Lang = "en"; PlayerPrefs.Save(); }
            return Lang;
        }
        public static void Apply()
        {
            Audio.SetVolumes(Sfx, Music); Native.Vibration = Vibrate;
            Application.targetFrameRate = Fps; QualitySettings.vSyncCount = 0;
        }
    }

    /// <summary>Game entry point. Creates the UI host, owns the GameContext and drives the whole flow.</summary>
    public class App : MonoBehaviour
    {
        public const string Version = "1.3.0";
        public static App I;
        public static GameContext Ctx => I != null ? I.ctx : null;
        public static bool Suppress;                      // tests can set before boot to take over
        public GameContext ctx; public SaveSystem save; public UiHost host; public Nav nav; public Hud hud;
        public bool InGame { get; private set; }
        public AdService Ads { get; private set; }
        double lastSave;
        public string PendingMusic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (I == null && !Suppress) new GameObject("App").AddComponent<App>(); }

        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this; DontDestroyOnLoad(gameObject);
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Application.runInBackground = Tour.Requested;
            Input.multiTouchEnabled = false;
        }

        IEnumerator Start()
        {
            host = UiHost.Create(); nav = new Nav(host);
            host.OnBackKey = HandleBack;
            Audio.Init(gameObject);
            L.Load(Prefs.InitializeLanguage());
            Prefs.Apply();
            var splash = new SplashScreen(); nav.Root(splash);
            if (Tour.Requested) StartCoroutine(Tour.Run(this));
            yield return null;
            ctx = GuildGame.Create();
            save = new SaveSystem(Tour.Requested ? "emberhall_visualcheck" : "emberhall");
            ctx.OnNote += OnNote;
            Ads = gameObject.AddComponent<AdService>(); Ads.Bind(this);
            ctx.Quests.OnReady += q => { };
            yield return null;
            Audio.PlayMusic("title");
            yield return new WaitForSecondsRealtime(0.1f);
            while (!splash.Done) yield return null;
            GoTitle();
        }

        void OnNote(Note n)
        {
            Sprite ic = !string.IsNullOrEmpty(n.icon) ? Art.Get(n.icon) : null;
            switch (n.kind)
            {
                case NoteKind.Good: Audio.Play("hire"); break;
                case NoteKind.Warn: Audio.Play("deny"); break;
                case NoteKind.Level: Audio.Play("levelup"); break;
                case NoteKind.Quest: Audio.Play("quest"); break;
                case NoteKind.Rank: Audio.Play("rank"); break;
                case NoteKind.Build: Audio.Play("build"); break;
                case NoteKind.Reward: Audio.Play("coin"); break;
                default: Audio.Play("pop"); break;
            }
            var col = n.kind == NoteKind.Warn ? Theme.RedDark : n.kind == NoteKind.Quest ? Theme.PurpleDark : n.kind == NoteKind.Level ? Theme.GreenDark : n.kind == NoteKind.Rank ? Theme.AccentDark : Theme.Panel;
            if (n.kind == NoteKind.Quest) ic = Icons.Get("quest");
            if (n.kind == NoteKind.Level) ic = Icons.Get("star");
            Toasts.Show(L.T(n.text), ic, col);
        }

        // ============================ flow ============================
        public bool HasSave => save != null && save.Exists;

        public void GoTitle()
        {
            Ads?.HideBanner();
            InGame = false; Popups.CloseAll();
            if (hud != null) hud.SetVisible(false);
            nav.Root(new TitleScreen());
            Audio.PlayMusic("title");
        }

        public void ContinueGame()
        {
            if (!save.Load(ctx)) { Toasts.Show(L.T("No save found")); return; }
            EnterGame(true);
        }

        public void StartOnboarding() { nav.Root(new OnboardingScreen()); }

        public void BeginNewGame(string name, int emblem, string cls)
        {
            BoardState.Tab = 0; BoardState.Region = "r1"; BoardState.LastParty.Clear();
            Bag.OpenTab = 0; Bag.Filter = 0; Bag.Sort = 0;
            save.Delete();
            GuildGame.StartNew(ctx, name, emblem, cls);
            Prefs.MarkSeen("tutorial_started");
            save.Save(ctx);
            EnterGame(false);
        }

        void EnterGame(bool fromSave)
        {
            InGame = true; lastSave = Util.Now;
            if (ctx.Ranks.Index <= 0)
            {
                double gold = ctx.Wallet.Get("gold");
                if (gold < 400) ctx.Wallet.Add("gold", 600 - gold);
                int ore = ctx.Inventory.Count("iron_ore");
                if (ore < 8) ctx.Give(new Res("iron_ore", 12 - ore));
            }
            if (hud == null) hud = new Hud(this);
            hud.SetVisible(true);
            hud.GoTab(Hud.Tab.Hall, true);
            Audio.PlayMusic("town");
            ctx.LastTick = 0; ctx.Tick();
            if (fromSave && ctx.LastOffline != null && ctx.LastOffline.Meaningful) Popups.Show(new OfflinePopup(ctx.LastOffline));
            Tw.After(0.6f, () => { if (InGame) hud.CheckPopups(); });
        }

        // ============================ loop ============================
        void Update()
        {
            if (ctx == null || nav == null) return;
            if (Ads != null && Ads.Busy) return;
            nav.Update();
            Popups.Update();
            if (InGame)
            {
                ctx.Tick();
                hud?.Tick();
                if (Util.Now - lastSave > 20) { save.Save(ctx); lastSave = Util.Now; }
            }
        }

        void HandleBack()
        {
            if (Ads != null && Ads.Busy) return;
            if (Popups.Back()) return;
            if (InGame && hud != null && hud.Back()) return;
            if (nav.Back()) return;
            if (InGame) { Popups.Confirm(L.T("Back to menu?"), L.T("Your progress is saved automatically."), L.T("Menu"), () => { SaveNow(); GoTitle(); }, L.T("Stay")); }
            else if (nav.Current is TitleScreen) Popups.Confirm(L.T("Quit game?"), "", L.T("Quit"), Application.Quit, L.T("Stay"), true);
            else GoTitle();
        }

        public void SaveNow() { if (InGame && ctx != null) { save.Save(ctx); lastSave = Util.Now; } }
        void OnApplicationPause(bool paused) { if (paused) SaveNow(); else if (InGame && ctx != null) { ctx.LastTick = 0; } }
        void OnApplicationQuit() { SaveNow(); }

        public void ResetProgress()
        {
            save.Delete(); ctx.NewGame(); InGame = false; GoTitle();
        }
    }
}
