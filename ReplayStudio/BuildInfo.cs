using MelonLoader;

[assembly: MelonInfo(typeof(ReplayStudio.Core), ReplayStudio.BuildInfo.Name, ReplayStudio.BuildInfo.Version, ReplayStudio.BuildInfo.Author)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]

namespace ReplayStudio
{
    /// <summary> </summary>
    public static class BuildInfo
    {
        public const string Name = "ReplayStudio";
        public const string Author = "TacoSlayer36";
        public const string Version = "1.0.0";
        public const string Description = "View and record ReplayMod recordings from your desktop";
    }
}