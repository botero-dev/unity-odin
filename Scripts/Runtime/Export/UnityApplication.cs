using UnityEngine;

namespace OdinInterop
{
    [OdinExport]
    internal static partial class UnityApplication
    {
        private static String8 GetBuildGUID(Allocator allocator) => new String8(Application.buildGUID, allocator);
        private static String8 GetCompanyName(Allocator allocator) => new String8(Application.companyName, allocator);
        private static String8 GetDataPath(Allocator allocator) => new String8(Application.dataPath, allocator);
        private static String8 GetIdentifier(Allocator allocator) => new String8(Application.identifier, allocator);
        private static String8 GetInstallerName(Allocator allocator) => new String8(Application.installerName, allocator);
        private static String8 GetPersistentDataPath(Allocator allocator) => new String8(Application.persistentDataPath, allocator);
        private static String8 GetProductName(Allocator allocator) => new String8(Application.productName, allocator);
        private static String8 GetStreamingAssetsPath(Allocator allocator) => new String8(Application.streamingAssetsPath, allocator);
        private static String8 GetTemporaryCachePath(Allocator allocator) => new String8(Application.temporaryCachePath, allocator);
        private static String8 GetUnityVersion(Allocator allocator) => new String8(Application.unityVersion, allocator);
        private static String8 GetVersion(Allocator allocator) => new String8(Application.version, allocator);
        private static String8 GetConsoleLogPath(Allocator allocator) => new String8(Application.consoleLogPath, allocator);
        private static String8 GetAbsoluteURL(Allocator allocator) => new String8(Application.absoluteURL, allocator);
        private static int GetTargetFrameRate() => Application.targetFrameRate;
        private static void SetTargetFrameRate(int targetFrameRate) => Application.targetFrameRate = targetFrameRate;
        private static bool IsBatchMode() => Application.isBatchMode;
        private static bool IsEditor() => Application.isEditor;
        private static bool IsFocused() => Application.isFocused;
        private static bool IsPlaying() => Application.isPlaying;
        private static bool IsMobilePlatform() => Application.isMobilePlatform;
        private static bool IsConsolePlatform() => Application.isConsolePlatform;
        private static bool IsGenuine() => Application.genuine;
        private static bool IsGenuineCheckAvailable() => Application.genuineCheckAvailable;
        private static bool GetRunInBackground() => Application.runInBackground;
        private static void SetRunInBackground(bool runInBackground) => Application.runInBackground = runInBackground;
        private static bool CanStreamedLevelBeLoaded(int levelIndex) => Application.CanStreamedLevelBeLoaded(levelIndex);
        private static bool CanStreamedLevelBeLoadedByName(String8 levelName) => Application.CanStreamedLevelBeLoaded(levelName.ToString());
        private static void OpenURL(String8 url) => Application.OpenURL(url.ToString());
        private static void Quit(int exitCode = default) => Application.Quit(exitCode);
        private static void Unload() => Application.Unload();
        private static RuntimePlatform GetPlatform() => Application.platform;
        private static SystemLanguage GetSystemLanguage() => Application.systemLanguage;
        private static ApplicationInstallMode GetInstallMode() => Application.installMode;
        private static ApplicationSandboxType GetSandboxType() => Application.sandboxType;
        private static NetworkReachability GetInternetReachability() => Application.internetReachability;
        private static ThreadPriority GetBackgroundLoadingPriority() => Application.backgroundLoadingPriority;
        private static void SetBackgroundLoadingPriority(ThreadPriority priority) => Application.backgroundLoadingPriority = priority;
        private static bool RequestUserAuthorization(UserAuthorization mode) => Application.RequestUserAuthorization(mode).isDone;
        private static bool HasUserAuthorization(UserAuthorization mode) => Application.HasUserAuthorization(mode);
    }
}