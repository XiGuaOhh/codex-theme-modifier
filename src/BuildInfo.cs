using System;
using System.Reflection;
[assembly: AssemblyTitle("Codex Theme Modifier")]
[assembly: AssemblyDescription("Independent appearance manager for the official Codex desktop app")]
[assembly: AssemblyCompany("XiGuaOhh")]
[assembly: AssemblyProduct("Codex Theme Modifier")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
namespace CodexThemeModifier {
    internal static class BuildInfo {
        public const string Version="2.0.0";
        public const string ProductId="CodexThemeModifier.v2";
        public const string Owner="XiGuaOhh";
        public const string Repository="codex-theme-modifier";
        public const string AssetName="CodexThemeModifier.exe";
        public const string Website="https://github.com/XiGuaOhh/codex-theme-modifier";
        public static readonly string[] OwnedFiles={"Start.ps1","ThemeHost.ps1","ThemeHost.cs","custom-background.js","PackageInfo.ps1","ChatGPTTheme.exe","modifier.ico","Codex.ico","CodexThemeModifier.exe","enabled.flag","disabled.flag","theme-host-state.json","启动日志.txt","ChatGPT.lnk","启动官方原版.cmd","modifier-state.json"};
        public static string LatestApi { get { return "https://api.github.com/repos/"+Owner+"/"+Repository+"/releases/latest"; } }
    }
}
