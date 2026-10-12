using System;
using System.IO;
using System.Linq;
using ManagerKit;
using ManagerKit.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emberhall.EditorTools
{
    /// <summary>Project configuration + one-click builds. Used from the menu (Emberhall/...) or batch mode -executeMethod.</summary>
    public static class BuildTools
    {
        const string Scene = "Assets/Scenes/Main.unity";
        const string Bundle = "com.pixelgamemaster.emberhall";
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [MenuItem("Emberhall/Configure Project")]
        public static void Configure()
        {
            // --- scene ---
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(Scene))
            {
                var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(s, Scene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };

            // --- identity ---
            PlayerSettings.companyName = "Pixel Game Master";
            PlayerSettings.productName = "Emberhall: Guild Legends";
            PlayerSettings.bundleVersion = App.Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, Bundle);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, Bundle);
            PlayerSettings.Android.bundleVersionCode = 6;

            // --- display ---
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.SplashScreen.show = false; PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.gpuSkinning = false; PlayerSettings.useAnimatedAutorotation = false;
            PlayerSettings.runInBackground = false;

            // --- android ---
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed;
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            QualitySettings.vSyncCount = 0;

            GenerateIcons();
            AssetDatabase.SaveAssets();
            Debug.Log("[GM] Project configured");
        }

        // =================================== icons ===================================
        static Texture2D Blank(int s, Color c) { var t = new Texture2D(s, s, TextureFormat.RGBA32, false); var px = new Color[s * s]; for (int i = 0; i < px.Length; i++) px[i] = c; t.SetPixels(px); return t; }

        static Texture2D IconBackground(int s)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
                {
                    float v = y / (float)s; var c = Color.Lerp(new Color(0.07f, 0.04f, 0.16f), new Color(0.38f, 0.12f, 0.2f), Mathf.Pow(1 - v, 1.6f) * 0.9f);
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s * 0.5f, s * 0.42f)) / (s * 0.55f); c += new Color(1f, 0.5f, 0.1f) * Mathf.Clamp01(1 - d) * 0.35f;
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, 1));
                }
            t.Apply(); return t;
        }

        /// <summary>Shield-and-flame crest (nearest-neighbour upscaled sprite), centred, occupying <paramref name="fill"/> of the canvas.</summary>
        static Texture2D IconForeground(int s, float fill, bool withBackground)
        {
            var bg = withBackground ? IconBackground(s) : Blank(s, new Color(0, 0, 0, 0));
            var pcs = Brand.ShieldCanvas(new Color(0.91f, 0.31f, 0.23f));
            int sw = pcs.W, sh = pcs.H; float scale = Mathf.Floor(s * fill / sh); scale = Mathf.Max(1, scale);
            int dw = (int)(sw * scale), dh = (int)(sh * scale), ox = (s - dw) / 2, oy = (s - dh) / 2 - (int)(s * 0.01f);
            var src = pcs.P; var px = bg.GetPixels32();
            // soft glow behind the crest
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.42f); float a = Mathf.Clamp01(1 - d); a *= a * 0.5f;
                    var p = px[y * s + x]; if (p.a == 0 && withBackground == false) { px[y * s + x] = new Color32(255, 140, 40, (byte)(a * 90)); }
                }
            for (int y = 0; y < dh; y++) for (int x = 0; x < dw; x++)
                {
                    var c = src[(int)(y / scale) * sw + (int)(x / scale)]; if (c.a < 8) continue;
                    int px_ = ox + x, py_ = oy + y; if (px_ < 0 || py_ < 0 || px_ >= s || py_ >= s) continue;
                    px[py_ * s + px_] = c;
                }
            bg.SetPixels32(px); bg.Apply(); return bg;
        }

        static void Save(Texture2D t, string path) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, t.EncodeToPNG()); AssetDatabase.ImportAsset(path); }

        static Texture2D Import(string path)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path); ti.textureType = TextureImporterType.Default; ti.isReadable = true; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.alphaIsTransparency = true; ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        [MenuItem("Emberhall/Generate App Icon")]
        public static void GenerateIcons()
        {
            const string dir = "Assets/Resources/Brand";
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Art/Generated/guild_icon.png");
            var full = source ? ResizeIcon(source, 512) : IconForeground(512, 0.8f, true); Save(full, dir + "/icon_512.png");
            var fg = IconForeground(432, 0.56f, false); Save(fg, dir + "/icon_fg.png");
            var bg = IconBackground(432); Save(bg, dir + "/icon_bg.png");
            File.WriteAllBytes(Path.Combine(Root, "Store/icon_512.png"), Directory.CreateDirectory(Path.Combine(Root, "Store")) != null ? full.EncodeToPNG() : null);
            var tFull = Import(dir + "/icon_512.png"); var tFg = Import(dir + "/icon_fg.png"); var tBg = Import(dir + "/icon_bg.png");
            PlayerSettings.SetIcons(NamedBuildTarget.Android, new[] { tFull }, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tFull }, IconKind.Any);
            Debug.Log("[GM] Icons generated");
        }

        static Texture2D ResizeIcon(Texture2D source, int size)
        {
            var icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                pixels[y * size + x] = source.GetPixelBilinear((x + .5f) / size, (y + .5f) / size);
            icon.SetPixels(pixels); icon.Apply(); return icon;
        }

        // =================================== keystore ===================================
        static void SetupKeystore()
        {
            string dir = Path.Combine(Root, "Keystore"); Directory.CreateDirectory(dir);
            string ks = Path.Combine(dir, "emberhall.keystore"), pw = Path.Combine(dir, "PASSWORDS.txt");
            if (!File.Exists(ks))
            {
                string pass = Guid.NewGuid().ToString("N").Substring(0, 16);
                string keytool = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe");
                var psi = new System.Diagnostics.ProcessStartInfo(keytool, $"-genkeypair -v -keystore \"{ks}\" -alias emberhall -keyalg RSA -keysize 2048 -validity 10000 -storepass {pass} -keypass {pass} -dname \"CN=Pixel Game Master, OU=Games, O=Pixel Game Master, C=ID\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var p = System.Diagnostics.Process.Start(psi)) { p.WaitForExit(); Debug.Log("[GM] keytool exit " + p.ExitCode); }
                File.WriteAllText(pw, $"Keystore: emberhall.keystore\nAlias: emberhall\nStore/Key password: {pass}\n\nBACK THIS UP. Losing it means you cannot update the game on Google Play.\n");
            }
            string pass2 = File.ReadAllLines(pw).First(l => l.StartsWith("Store/Key password:")).Split(':')[1].Trim();
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks; PlayerSettings.Android.keystorePass = pass2;
            PlayerSettings.Android.keyaliasName = "emberhall"; PlayerSettings.Android.keyaliasPass = pass2;
        }

        // =================================== builds ===================================
        static void Build(BuildTarget target, BuildTargetGroup group, string path, BuildOptions opt = BuildOptions.None)
        {
            Configure();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Scene }, target = target, targetGroup = group, locationPathName = path, options = opt });
            Debug.Log($"[GM] Build {target}: {report.summary.result} size={(report.summary.totalSize / 1048576.0):0.0}MB errors={report.summary.totalErrors} time={report.summary.totalTime}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        [MenuItem("Emberhall/Build Windows (test)")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Builds/Win"));
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Path.Combine(Root, "Builds/Win/Emberhall.exe"));
        }

        [MenuItem("Emberhall/Build Android APK")]
        public static void BuildApk()
        {
            SetupKeystore(); EditorUserBuildSettings.buildAppBundle = false; EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            Directory.CreateDirectory(Path.Combine(Root, "Builds"));
            Build(BuildTarget.Android, BuildTargetGroup.Android, Path.Combine(Root, "Builds/Emberhall-" + App.Version + ".apk"));
        }

        [MenuItem("Emberhall/Build Android AAB (Play Store)")]
        public static void BuildAab()
        {
            SetupKeystore(); EditorUserBuildSettings.buildAppBundle = true; EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            
            Directory.CreateDirectory(Path.Combine(Root, "Builds"));
            Build(BuildTarget.Android, BuildTargetGroup.Android, Path.Combine(Root, "Builds/Emberhall-" + App.Version + ".aab"));
        }

        [MenuItem("Emberhall/Validate Content")]
        public static void ValidateContent()
        {
            var db = ContentDb.FromResources(GuildGame.ContentFolder);
            var errs = db.Validate();
            Debug.Log(errs.Count == 0 ? $"[GM] Content OK: {db.Missions.Count} missions, {db.Quests.Count} quests, {db.Items.Count} items" : "[GM] Content errors:\n" + string.Join("\n", errs));
        }
    }
}
