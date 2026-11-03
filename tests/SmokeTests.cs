using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Text;
namespace CodexThemeModifier {
    internal sealed class FakeDesktop:IDesktop {
        public readonly Dictionary<string,string> Targets=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        public bool Registered;public int Launched;
        public PackageInfo Package;
        public PackageInfo Probe(){return Package;}
        public void Shortcut(string path,string target,string args,string icon){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,"LINK "+target);Targets[path]=target;}
        public string ShortcutTarget(string path){string value;return Targets.TryGetValue(path,out value)?value:null;}
        public void OfficialShortcut(string path,string appId){File.WriteAllText(path,"APP "+appId);Targets[path]="APP "+appId;}
        public void Register(AppPaths paths){Registered=true;}
        public void Unregister(AppPaths paths){Registered=false;}
        public void Launch(string file,string args){Launched++;}
        public void StopHelper(AppPaths paths){}
    }
    internal static class TestProgram {
        static int checks;
        static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS "+name);}
        static void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,name);}
        [STAThread]static int Main(string[] args){
            string root=Path.GetFullPath(args[0]);string release=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
            try{
                var paths=new AppPaths(Path.Combine(root,"installation"),Path.Combine(root,"programs"),"unused-test-registry");
                string original=Path.Combine(root,"official","app","ChatGPT.exe");Directory.CreateDirectory(Path.GetDirectoryName(original));File.WriteAllText(original,"unchanged official fixture");string originalHash=JsonFiles.Hash(original);
                var p=new PackageInfo{Found=true,Root=Path.Combine(root,"official"),Executable=original,AppId="OpenAI.Codex_TEST!App",PackageFamily="OpenAI.Codex_TEST",Version="99.0.0",DisplayName="Codex"};
                var desktop=new FakeDesktop{Package=p};var engine=new ManagerEngine(paths,desktop);
                var state=engine.Install(p,true);
                Check(state.ProductId==BuildInfo.ProductId&&state.Enabled&&desktop.Registered,"install-register-enable");
                Check(File.Exists(paths.Enabled)&&File.Exists(paths.Manager)&&File.Exists(paths.FilePath("ThemeHost.cs")),"embedded-runtime-deployed");
                Check(desktop.Targets.Count==2,"own-start-menu-shortcuts-created");
                Check(JsonFiles.Hash(original)==originalHash&&desktop.Launched==0,"official-fixture-and-processes-untouched");
                string image=Path.Combine(root,"test.png");using(var b=new Bitmap(2,2)){b.SetPixel(0,0,Color.Red);b.Save(image,System.Drawing.Imaging.ImageFormat.Png);}
                var settings=new ThemeConfig{dim=999,blur=-1};engine.SaveTheme(settings,image);
                var saved=JsonFiles.Read<ThemeConfig>(paths.Config);Check(saved.dim==90&&saved.blur==0&&saved.imageAction=="replace","settings-normalized-and-image-copied");
                engine.SaveTheme(new ThemeConfig{dim=30},null);saved=JsonFiles.Read<ThemeConfig>(paths.Config);Check(saved.imageAction=="replace"&&saved.imageFile=="preferences/background.bin","pending-image-survives-settings-edit");
                File.WriteAllText(Path.Combine(paths.Preferences,"applied-revision.txt"),saved.revision);
                engine.SaveTheme(new ThemeConfig{dim=50},null);saved=JsonFiles.Read<ThemeConfig>(paths.Config);Check(saved.imageAction=="keep","applied-image-not-forced-over-native-selection");
                engine.Disable(p,false);Check(!engine.State().Enabled&&!File.Exists(paths.Enabled)&&File.Exists(paths.Config),"disable-preserves-preferences");
                string external=Path.Combine(root,"other.exe");string managerLink=Path.Combine(paths.Programs,"Codex 主题修改器.lnk");desktop.Shortcut(managerLink,external,"","");
                engine.PrepareUninstall(p,false);Check(desktop.ShortcutTarget(managerLink)==external&&File.Exists(managerLink),"user-changed-shortcut-preserved");
                var plan=new MaintenancePlan{ProductId=BuildInfo.ProductId,Root=paths.Root,InstanceId=state.InstanceId,Operation="uninstall",DeletePreferences=false};
                Reject(()=>MaintenanceCore.Validate(new MaintenancePlan{ProductId=BuildInfo.ProductId,Root=root,InstanceId=state.InstanceId,Operation="uninstall"},paths),"outside-installation-root-rejected");
                Reject(()=>MaintenanceCore.Validate(new MaintenancePlan{ProductId=BuildInfo.ProductId,Root=paths.Root,InstanceId="wrong",Operation="uninstall"},paths),"wrong-installation-identity-rejected");
                MaintenanceCore.Apply(plan,paths);Check(!File.Exists(paths.Manager)&&!File.Exists(paths.State)&&File.Exists(paths.Config),"uninstall-removes-program-and-retains-preferences");
                Check(JsonFiles.Hash(original)==originalHash&&!desktop.Registered,"uninstall-does-not-touch-official-installation");
                state=engine.Install(p,true);Check(state.Enabled&&File.Exists(paths.Config),"reinstall-after-retaining-preferences");
                var update=new MaintenancePlan{ProductId=BuildInfo.ProductId,Root=paths.Root,InstanceId=state.InstanceId,Operation="update",Source=release,Sha256=new string('0',64)};
                string old=JsonFiles.Hash(paths.Manager);Reject(()=>MaintenanceCore.Apply(update,paths),"tampered-update-rejected");Check(JsonFiles.Hash(paths.Manager)==old,"failed-update-keeps-previous-version");
                update.Sha256=JsonFiles.Hash(release);MaintenanceCore.Apply(update,paths);Check(JsonFiles.Hash(paths.Manager)==update.Sha256,"verified-update-replaces-only-modifier");
                var meta=new Dictionary<string,object>{{"draft",false},{"prerelease",false},{"tag_name","v99.0.0"},{"body","release notes"},{"assets",new object[]{new Dictionary<string,object>{{"name",BuildInfo.AssetName},{"browser_download_url",BuildInfo.Website+"/releases/download/v99.0.0/"+BuildInfo.AssetName},{"digest","sha256:"+update.Sha256},{"size",new FileInfo(release).Length}}}}};
                var u=UpdateService.ParseRelease(JsonFiles.Serializer().Serialize(meta));Check(u.Newer&&u.Digest==update.Sha256,"github-release-version-and-digest-parsed");
                Reject(()=>UpdateService.ValidateAssetUrl("https://github.com/attacker/repo/releases/download/v1/CodexThemeModifier.exe"),"foreign-update-repository-rejected");
                Reject(()=>UpdateService.ValidateAssetUrl("http://github.com/XiGuaOhh/codex-theme-modifier/releases/download/v1/CodexThemeModifier.exe"),"insecure-update-url-rejected");
                Reject(()=>UpdateService.ParseVersion("2.0.0-rc1"),"prerelease-does-not-enter-stable-updater");
                Reject(()=>paths.FilePath("..\\outside.txt"),"managed-filename-traversal-rejected");
                var client=typeof(CodexThemeMonitor).GetNestedType("LocalClient",BindingFlags.NonPublic);using(var c=(System.Net.WebClient)Activator.CreateInstance(client,true))Check(c.Encoding.WebName=="utf-8","cdp-json-forces-utf8-for-chinese-titles");
                var target=new Dictionary<string,object>{{"type","page"},{"url","app://-/index.html?%69nitialRoute=%2Favatar-overlay"},{"webSocketDebuggerUrl","ws://127.0.0.1:1/devtools/page/test"}};Check(!CodexThemeMonitor.IsMainTarget(target),"encoded-avatar-route-excluded");target["url"]="app://-/detached-window.html";Check(CodexThemeMonitor.IsMainTarget(target),"detached-chat-route-supported");target["type"]="webview";Check(!CodexThemeMonitor.IsMainTarget(target),"browser-and-plugin-webviews-excluded");
                engine.PrepareUninstall(p,true);plan.InstanceId=state.InstanceId;plan.DeletePreferences=true;MaintenanceCore.Apply(plan,paths);Check(!File.Exists(paths.Config)&&!File.Exists(Path.Combine(paths.Preferences,"background.bin")),"explicit-preference-removal-is-scoped");
                engine.Install(p,true);var reset=JsonFiles.Read<ThemeConfig>(paths.Config);Check(reset.imageAction=="remove"&&!reset.enabled,"reinstall-does-not-revive-deleted-browser-background");
                var barePaths=new AppPaths(Path.Combine(root,"native-only"),Path.Combine(root,"native-only-programs"),"unused-test-registry");var bareEngine=new ManagerEngine(barePaths,new FakeDesktop{Package=p});var bare=bareEngine.Install(p,true);bareEngine.PrepareUninstall(p,true);MaintenanceCore.Apply(new MaintenancePlan{ProductId=BuildInfo.ProductId,Root=barePaths.Root,InstanceId=bare.InstanceId,Operation="uninstall",DeletePreferences=true},barePaths);Check(JsonFiles.Read<InstallState>(Path.Combine(barePaths.Preferences,"retained-state.json")).ResetBrowserPreferences,"native-only-settings-also-get-reset-marker");
                var legacyPaths=new AppPaths(Path.Combine(root,"legacy"),Path.Combine(root,"legacy-programs"),"unused-test-registry");Directory.CreateDirectory(legacyPaths.Root);Directory.CreateDirectory(legacyPaths.Programs);JsonFiles.Write(legacyPaths.FilePath("install.json"),new{Version="1.3.1",InstallPath=legacyPaths.Root});
                var legacyDesktop=new FakeDesktop{Package=p};string link=Path.Combine(legacyPaths.Programs,"ChatGPT.lnk");legacyDesktop.Shortcut(link,legacyPaths.FilePath("ChatGPTTheme.exe"),"","");var legacyEngine=new ManagerEngine(legacyPaths,legacyDesktop);var legacyState=legacyEngine.Install(p,true);legacyEngine.PrepareUninstall(p,false);Check(legacyDesktop.ShortcutTarget(link)=="APP "+p.AppId,"legacy-pinned-entry-restores-registered-official-app");
                Check(JsonFiles.Hash(original)==originalHash,"final-original-file-integrity");
                Console.WriteLine("SUCCESS "+checks+" checks");return 0;
            }catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}
        }
    }
}
