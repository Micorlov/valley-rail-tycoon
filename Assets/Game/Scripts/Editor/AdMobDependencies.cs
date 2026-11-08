#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
namespace ValleyRail.Editor
{
    /// <summary>
    /// Google Mobile Ads + consent (versions from the plugin's GoogleMobileAdsDependencies.xml / GoogleUmpDependencies.xml), added to the
    /// generated gradle project instead of through the External Dependency Manager, so batchmode builds resolve them without another package.
    /// </summary>
    public sealed class AdMobDependencies : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!File.Exists("Assets/GoogleMobileAds/GoogleMobileAds.dll"))
                return;
            string gradle = Path.Combine(path, "build.gradle");
            if (!File.Exists(gradle))
                gradle = Path.Combine(path, "unityLibrary/build.gradle");
            const string marker = "// Valley Rail Google Mobile Ads";
            if (File.ReadAllText(gradle).Contains(marker))
                return;
            File.AppendAllText(gradle, "\n" + marker + "\ndependencies {\n" +
                "    implementation 'com.google.android.gms:play-services-ads:25.5.0'\n" +
                "    implementation 'com.google.android.ump:user-messaging-platform:4.0.0'\n" +
                "    implementation 'androidx.constraintlayout:constraintlayout:2.1.4'\n" +
                "    implementation 'androidx.lifecycle:lifecycle-process:2.6.2'\n" +
                "    implementation 'androidx.fragment:fragment:1.7.1'\n" +
                "}\n");
        }
    }
}
#endif
