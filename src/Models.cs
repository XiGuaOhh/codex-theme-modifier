using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
namespace CodexThemeModifier {
    internal static class JsonFiles {
        public static JavaScriptSerializer Serializer(){return new JavaScriptSerializer {MaxJsonLength=4*1024*1024};}
        public static T Read<T>(string path) where T:new(){return File.Exists(path)?Serializer().Deserialize<T>(File.ReadAllText(path,Encoding.UTF8)):new T();}
        public static void Write(string path,object value){
            if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Managed files cannot be symbolic links.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".new-"+Guid.NewGuid().ToString("N");
            try{File.WriteAllText(temp,Serializer().Serialize(value),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public static string Hash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        public static bool SamePath(string a,string b){return !String.IsNullOrEmpty(a)&&!String.IsNullOrEmpty(b)&&String.Equals(Path.GetFullPath(a).TrimEnd('\\'),Path.GetFullPath(b).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase);}
        public static void CopyAtomic(string source,string destination){
            if(SamePath(source,destination))return;
            if(File.Exists(destination)&&(File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Managed files cannot be symbolic links.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temp=destination+".new-"+Guid.NewGuid().ToString("N");
            try{File.Copy(source,temp);if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
    internal sealed class AppPaths {
        public readonly string Root,Programs,UninstallKey;
        public AppPaths(string root,string programs,string uninstallKey){Root=Path.GetFullPath(root);Programs=Path.GetFullPath(programs);UninstallKey=uninstallKey;}
        public static AppPaths Current(){return new AppPaths(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"CodexCustomTheme"),Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\CodexThemeModifier");}
        public string FilePath(string name){if(name.IndexOfAny(new[]{'\\','/'})>=0||name=="..")throw new InvalidDataException("Invalid managed filename.");return Path.Combine(Root,name);}
        public string State {get{return FilePath("modifier-state.json");}}
        public string Manager {get{return FilePath(BuildInfo.AssetName);}}
        public string Preferences {get{return Path.Combine(Root,"preferences");}}
        public string Config {get{return Path.Combine(Preferences,"theme.json");}}
        public string Enabled {get{return FilePath("enabled.flag");}}
        public void CheckRoot(){
            string parent=Path.GetDirectoryName(Root);
            if(String.IsNullOrEmpty(parent)||JsonFiles.SamePath(Root,Path.GetPathRoot(Root)))throw new InvalidDataException("Unsafe installation directory.");
            if(Directory.Exists(Root)&&(File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("安装目录不能是目录链接，请先处理同名文件夹。");
            if(Directory.Exists(Preferences)&&(File.GetAttributes(Preferences)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("设置目录不能是目录链接。");
        }
    }
    internal sealed class PackageInfo {
        public bool Found {get;set;}
        public string PackageFamily {get;set;}
        public string FullName {get;set;}
        public string Root {get;set;}
        public string Executable {get;set;}
        public string AppId {get;set;}
        public string Version {get;set;}
        public string DisplayName {get;set;}
    }
    internal sealed class ShortcutRecord {
        public string Name {get;set;}
        public string Target {get;set;}
        public bool RestoreOfficial {get;set;}
    }
    internal sealed class InstallState {
        public string ProductId {get;set;}
        public string InstanceId {get;set;}
        public string Version {get;set;}
        public bool Enabled {get;set;}
        public bool CheckUpdates {get;set;}
        public string OfficialAppId {get;set;}
        public bool ResetBrowserPreferences {get;set;}
        public List<ShortcutRecord> Shortcuts {get;set;}
        public List<string> OwnedFiles {get;set;}
        public InstallState(){Shortcuts=new List<ShortcutRecord>();OwnedFiles=new List<string>();CheckUpdates=true;}
    }
    internal sealed class ThemeConfig {
        public string revision {get;set;}
        public bool enabled {get;set;}
        public int dim {get;set;}
        public int blur {get;set;}
        public int panel {get;set;}
        public int bubble {get;set;}
        public string fit {get;set;}
        public string position {get;set;}
        public string imageAction {get;set;}
        public string imageFile {get;set;}
        public string imageName {get;set;}
        public string imageMime {get;set;}
        public ThemeConfig(){enabled=true;dim=42;panel=70;bubble=18;fit="cover";position="center";imageAction="keep";}
        public void Normalize(){dim=Math.Min(90,Math.Max(0,dim));blur=Math.Min(24,Math.Max(0,blur));panel=Math.Min(100,Math.Max(0,panel));bubble=Math.Min(100,Math.Max(0,bubble));fit=fit=="contain"?"contain":"cover";if(Array.IndexOf(new[]{"center","top","bottom","left","right"},position)<0)position="center";if(Array.IndexOf(new[]{"keep","replace","remove"},imageAction)<0)imageAction="keep";}
    }
    internal sealed class UpdateInfo {
        public string Version,Url,Digest,Page,Notes;
        public long Size;
        public bool Newer;
    }
    internal sealed class MaintenancePlan {
        public string ProductId {get;set;}
        public string InstanceId {get;set;}
        public string Operation {get;set;}
        public string Root {get;set;}
        public string Source {get;set;}
        public string Sha256 {get;set;}
        public int ParentId {get;set;}
        public string ParentPath {get;set;}
        public bool DeletePreferences {get;set;}
    }
}
