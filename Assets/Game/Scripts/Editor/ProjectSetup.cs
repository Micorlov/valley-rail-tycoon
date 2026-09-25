using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
namespace ValleyRail.Editor
{
    public static class ProjectSetup
    {
        static readonly string[] Scenes = { "Assets/Game/Scenes/Bootstrap.unity", "Assets/Game/Scenes/MainMenu.unity", "Assets/Game/Scenes/Game.unity" };
        [MenuItem("Valley Rail/Configure Project")]
        public static void Configure()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory("Assets/Game/Resources");
            Directory.CreateDirectory("Assets/Game/Scenes");
            if (!AssetDatabase.LoadAssetAtPath<GameBalance>("Assets/Game/Resources/GameBalance.asset"))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameBalance>(), "Assets/Game/Resources/GameBalance.asset");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/Resources/UIFont.asset");
            if (!font)
            {
                font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"));
                font.name = "Valley UI";
                font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?$+-/()#%·–→|");
                AssetDatabase.CreateAsset(font, "Assets/Game/Resources/UIFont.asset");
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures)
                    AssetDatabase.AddObjectToAsset(atlas, font);
            }
            if (!AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Resources/WorldMaterial.mat"))
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.enableInstancing = true;
                material.SetFloat("_Smoothness", .12f);
                AssetDatabase.CreateAsset(material, "Assets/Game/Resources/WorldMaterial.mat");
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Game/Resources/MobilePipeline.asset");
            if (!pipeline)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Game/Resources/MobileRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Mobile Pipeline";
                pipeline.msaaSampleCount = 2;
                pipeline.supportsHDR = false;
                pipeline.shadowDistance = 40;
                pipeline.mainLightShadowmapResolution = 1024;
                pipeline.renderScale = 1;
                AssetDatabase.CreateAsset(pipeline, "Assets/Game/Resources/MobilePipeline.asset");
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0;
            PlayerSettings.companyName = "Valley Rail";
            PlayerSettings.productName = "Valley Rail";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.valleyrail.tycoon");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = false;
            PlayerSettings.bundleVersion = System.Environment.GetEnvironmentVariable("VALLEY_VERSION") ?? "1.0.0";
            if (int.TryParse(System.Environment.GetEnvironmentVariable("VALLEY_VERSION_CODE"), out int versionCode))
                PlayerSettings.Android.bundleVersionCode = versionCode;
            ApplyIcons();
            // Both input handlers: the Input System drives touch and UI; the legacy manager guarantees the Android Back
            // key is seen on its first press. Serialized setting avoids dependency on editor-only package APIs.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input != null)
            {
                input.intValue = 2;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            string[] scenes = { "Bootstrap", "MainMenu", "Game" };
            var builds = new EditorBuildSettingsScene[3];
            for (int i = 0; i < scenes.Length; i++)
            {
                string path = "Assets/Game/Scenes/" + scenes[i] + ".unity";
                if (!File.Exists(path))
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    if (i == 0)
                        new GameObject("Game Bootstrap").AddComponent<GameBootstrap>();
                    EditorSceneManager.SaveScene(scene, path);
                }
                builds[i] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = builds;
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(builds[0].path);
            Debug.Log("VALLEY_RAIL_CONFIGURED");
        }
        // Android icons: legacy and round slots use the full-bleed art; adaptive icons layer the art over the meadow gradient.
        // The Android icon kinds live in the platform extension assembly, so they are resolved by name like the tool settings below.
        static void ApplyIcons()
        {
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/icon-legacy.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/icon-fg.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/icon-bg.png");
            if (!legacy || !foreground || !background)
            {
                Debug.LogWarning("Icon art is missing under Assets/Game/Art; the build keeps Unity's default icon.");
                return;
            }
            var kinds = System.Type.GetType("UnityEditor.Android.AndroidPlatformIconKind, UnityEditor.Android.Extensions", true);
            var target = UnityEditor.Build.NamedBuildTarget.Android;
            foreach (string name in new[] { "Legacy", "Round" })
            {
                var kind = (PlatformIconKind)kinds.GetField(name).GetValue(null);
                var icons = PlayerSettings.GetPlatformIcons(target, kind);
                foreach (var icon in icons)
                    icon.SetTexture(legacy);
                PlayerSettings.SetPlatformIcons(target, kind, icons);
            }
            var adaptive = (PlatformIconKind)kinds.GetField("Adaptive").GetValue(null);
            var layered = PlayerSettings.GetPlatformIcons(target, adaptive);
            foreach (var icon in layered)
            {
                icon.SetTexture(background, 0);
                icon.SetTexture(foreground, 1);
            }
            PlayerSettings.SetPlatformIcons(target, adaptive, layered);
        }
        [MenuItem("Valley Rail/Build Android APK (development)")]
        public static void BuildAndroid() => BuildAndroid(false);
        /// <summary>
        /// Release build: no development flag or debugger, signed with the keystore named by VALLEY_KEYSTORE,
        /// VALLEY_KEYSTORE_PASS, VALLEY_KEY_ALIAS and VALLEY_KEY_PASS. VALLEY_AAB=1 produces an app bundle for the store.
        /// Secrets are read from the environment only and cleared from the player settings afterward.
        /// </summary>
        [MenuItem("Valley Rail/Build Android Release")]
        public static void BuildAndroidRelease() => BuildAndroid(true);
        static void BuildAndroid(bool release)
        {
            Configure();
            Directory.CreateDirectory("Builds");
            bool bundle = release && System.Environment.GetEnvironmentVariable("VALLEY_AAB") == "1";
            EditorUserBuildSettings.buildAppBundle = bundle;
            string output = release ? (bundle ? "Builds/ValleyRail-release.aab" : "Builds/ValleyRail-release.apk") : "Builds/ValleyRail.apk";
            var type = System.Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions", true);
            string[] properties = { "sdkRootPath", "ndkRootPath", "jdkRootPath" };
            string[] variables = { "VALLEY_ANDROID_SDK", "VALLEY_ANDROID_NDK", "VALLEY_JAVA_HOME" };
            var old = new string[3];
            var changed = new bool[3];
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    string value = System.Environment.GetEnvironmentVariable(variables[i]);
                    if (string.IsNullOrEmpty(value))
                        continue;
                    var property = type.GetProperty(properties[i]);
                    old[i] = (string)property.GetValue(null);
                    property.SetValue(null, value);
                    changed[i] = true;
                }
                if (release)
                    ApplySigning();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = output, target = BuildTarget.Android, options = release ? BuildOptions.None : BuildOptions.Development });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new System.Exception("Android build failed: " + report.summary.result);
            }
            finally
            {
                for (int i = 0; i < 3; i++)
                    if (changed[i])
                        type.GetProperty(properties[i]).SetValue(null, old[i]);
                if (release)
                    ClearSigning();
            }
        }
        static void ApplySigning()
        {
            string keystore = System.Environment.GetEnvironmentVariable("VALLEY_KEYSTORE");
            string storePass = System.Environment.GetEnvironmentVariable("VALLEY_KEYSTORE_PASS");
            string alias = System.Environment.GetEnvironmentVariable("VALLEY_KEY_ALIAS");
            string keyPass = System.Environment.GetEnvironmentVariable("VALLEY_KEY_PASS");
            if (string.IsNullOrEmpty(keystore) || string.IsNullOrEmpty(storePass) || string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(keyPass))
                throw new System.InvalidOperationException("Release builds need VALLEY_KEYSTORE, VALLEY_KEYSTORE_PASS, VALLEY_KEY_ALIAS and VALLEY_KEY_PASS in the environment.");
            if (!File.Exists(keystore))
                throw new FileNotFoundException("Keystore not found: " + keystore);
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPass;
        }
        static void ClearSigning()
        {
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.useCustomKeystore = false;
        }
        [MenuItem("Valley Rail/Build Mac Preview")]
        public static void BuildMac()
        {
            Configure();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = "Builds/ValleyRail.app", target = BuildTarget.StandaloneOSX });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Mac build failed");
        }
    }
}
