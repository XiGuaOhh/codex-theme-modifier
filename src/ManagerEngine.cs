using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
namespace CodexThemeModifier {
    internal sealed class ManagerEngine {
        public readonly AppPaths Paths;
        readonly IDesktop desktop;
        public static readonly string[] RuntimeFiles={"Start.ps1","ThemeHost.ps1","ThemeHost.cs","custom-background.js","PackageInfo.ps1","ChatGPTTheme.exe","modifier.ico"};
        public static readonly string[] DeletableFiles=BuildInfo.OwnedFiles;
        public ManagerEngine(AppPaths paths,IDesktop platform){Paths=paths;desktop=platform;}
        public InstallState State(){
            if(!File.Exists(Paths.State))return null;
            var state=JsonFiles.Read<InstallState>(Paths.State);if(state.ProductId!=BuildInfo.ProductId)throw new IOException("安装目录内有不属于此修改器的状态文件。");return state;
        }
        public bool IsLegacy(){
            string file=Paths.FilePath("install.json");if(!File.Exists(file))return false;
            try{var data=JsonFiles.Serializer().DeserializeObject(File.ReadAllText(file,Encoding.UTF8)) as Dictionary<string,object>;object value,version;return data!=null&&data.TryGetValue("InstallPath",out value)&&JsonFiles.SamePath(Convert.ToString(value),Paths.Root)&&data.TryGetValue("Version",out version)&&Convert.ToString(version).StartsWith("1.",StringComparison.Ordinal);}catch{return false;}
        }
        bool Retained(){try{return JsonFiles.Read<InstallState>(Path.Combine(Paths.Preferences,"retained-state.json")).ProductId==BuildInfo.ProductId;}catch{return false;}}
        public InstallState Install(PackageInfo package,bool enable){
            if(enable&&(package==null||!package.Found))throw new IOException("请先安装 Windows 官方桌面版 Codex。");
            Paths.CheckRoot();var old=State();bool legacy=IsLegacy();
            if(old==null&&!legacy&&!Retained()&&Directory.Exists(Paths.Root)&&Directory.EnumerateFileSystemEntries(Paths.Root).Any())throw new IOException("同名安装目录包含其他文件，未覆盖。请先检查 "+Paths.Root);
            Directory.CreateDirectory(Paths.Root);
            var state=old??new InstallState {ProductId=BuildInfo.ProductId,InstanceId=Guid.NewGuid().ToString("N")};
            // Preserve an enabled app during a modifier update; only this helper is stopped.
            desktop.StopHelper(Paths);
            foreach(string file in RuntimeFiles)Resources.Extract("payload."+file,Paths.FilePath(file));
            JsonFiles.CopyAtomic(Assembly.GetExecutingAssembly().Location,Paths.Manager);
            if(old==null&&Retained()){
                var retained=JsonFiles.Read<InstallState>(Path.Combine(Paths.Preferences,"retained-state.json"));
                if(retained.ResetBrowserPreferences)JsonFiles.Write(Paths.Config,new ThemeConfig{revision=Guid.NewGuid().ToString("N"),enabled=false,imageAction="remove"});
            }
            state.Version=BuildInfo.Version;state.Enabled=enable;
            if(package!=null&&package.Found)state.OfficialAppId=package.AppId;
            if(String.IsNullOrEmpty(state.OfficialAppId))state.OfficialAppId="OpenAI.Codex_2p2nqsd0c76g0!App";
            state.OwnedFiles=new List<string>(DeletableFiles);
            string icon=Paths.FilePath("modifier.ico");
            if(package!=null&&package.Found){try{using(var image=Icon.ExtractAssociatedIcon(package.Executable))using(var stream=File.Create(Paths.FilePath("Codex.ico")))image.Save(stream);icon=Paths.FilePath("Codex.ico");}catch{}}
            SaveShortcut(state,"Codex 主题修改器.lnk",Paths.Manager,"",Paths.FilePath("modifier.ico"),false);
            SaveShortcut(state,"Codex（自定义背景）.lnk",Paths.FilePath("ChatGPTTheme.exe"),"",icon,false);
            if(legacy)foreach(string name in new[]{"ChatGPT.lnk","Codex.lnk"}){
                string path=Path.Combine(Paths.Programs,name);string target=desktop.ShortcutTarget(path);
                if(JsonFiles.SamePath(target,Paths.FilePath("ChatGPTTheme.exe")))SaveShortcut(state,name,Paths.FilePath("ChatGPTTheme.exe"),"",icon,true);
            }
            JsonFiles.Write(Paths.State,state);desktop.Register(Paths);
            if(enable){if(File.Exists(Paths.FilePath("disabled.flag")))File.Delete(Paths.FilePath("disabled.flag"));File.WriteAllText(Paths.Enabled,"enabled",Encoding.UTF8);}else{if(File.Exists(Paths.Enabled))File.Delete(Paths.Enabled);File.WriteAllText(Paths.FilePath("disabled.flag"),"disabled",Encoding.UTF8);}
            return state;
        }
        void SaveShortcut(InstallState state,string name,string target,string args,string icon,bool restore){
            string path=Path.Combine(Paths.Programs,name);
            var existing=state.Shortcuts.FirstOrDefault(s=>s.Name==name);
            if(File.Exists(path)&&existing==null&&!restore){string current=desktop.ShortcutTarget(path);if(!JsonFiles.SamePath(current,target))return;}
            desktop.Shortcut(path,target,args,icon);
            if(existing==null)state.Shortcuts.Add(new ShortcutRecord {Name=name,Target=target,RestoreOfficial=restore});
        }
        public void Start(PackageInfo package){
            if(package==null||!package.Found)throw new IOException("请先安装 Windows 官方桌面版 Codex。");
            var state=State();if(state!=null&&state.Enabled)desktop.Launch(Paths.FilePath("ChatGPTTheme.exe"),"");else desktop.Launch("explorer.exe","shell:AppsFolder\\"+package.AppId);
        }
        public void Attach(){
            var state=State();if(state==null||!state.Enabled)return;
            WindowsDesktop.RunHidden(WindowsDesktop.PowerShell,"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Paths.FilePath("ThemeHost.ps1")+"\" -AttachOnly");
        }
        public bool Disable(PackageInfo package,bool clearPreferences){
            var state=State();if(state==null)return false;
            File.WriteAllText(Paths.FilePath("disabled.flag"),"disabled",Encoding.UTF8);if(File.Exists(Paths.Enabled))File.Delete(Paths.Enabled);
            desktop.StopHelper(Paths);bool live=false;try{live=RuntimeClient.Remove(package,clearPreferences);}catch{}
            state.Enabled=false;JsonFiles.Write(Paths.State,state);return live;
        }
        public void SaveTheme(ThemeConfig config,string selectedFile){
            var state=State();if(state==null)throw new IOException("请先安装修改器。");Paths.CheckRoot();config.Normalize();
            if(String.IsNullOrEmpty(selectedFile)&&config.imageAction=="keep"&&File.Exists(Paths.Config)){
                var pending=JsonFiles.Read<ThemeConfig>(Paths.Config);string ack=Path.Combine(Paths.Preferences,"applied-revision.txt");string applied=File.Exists(ack)?File.ReadAllText(ack,Encoding.UTF8).Trim():"";
                if(!String.IsNullOrEmpty(pending.revision)&&pending.revision!=applied){config.imageAction=pending.imageAction;config.imageFile=pending.imageFile;config.imageMime=pending.imageMime;config.imageName=pending.imageName;}
            }
            if(!String.IsNullOrEmpty(selectedFile)){
                var file=new FileInfo(selectedFile);if(!file.Exists||file.Length>25*1024*1024)throw new IOException("图片不存在或超过 25 MB。");
                string ext=file.Extension.ToLowerInvariant();var types=new Dictionary<string,string>{{".png","image/png"},{".jpg","image/jpeg"},{".jpeg","image/jpeg"},{".webp","image/webp"},{".gif","image/gif"},{".avif","image/avif"}};
                if(!types.ContainsKey(ext))throw new IOException("请选择 PNG、JPG、WebP、GIF 或 AVIF 图片。");
                Directory.CreateDirectory(Paths.Preferences);JsonFiles.CopyAtomic(file.FullName,Path.Combine(Paths.Preferences,"background.bin"));
                config.imageAction="replace";config.imageFile="preferences/background.bin";config.imageName=file.Name;config.imageMime=types[ext];
            }
            config.revision=Guid.NewGuid().ToString("N");JsonFiles.Write(Paths.Config,config);
        }
        public void RestoreShortcuts(InstallState state,PackageInfo package){
            foreach(var item in state.Shortcuts){
                if(Array.IndexOf(new[]{"Codex 主题修改器.lnk","Codex（自定义背景）.lnk","ChatGPT.lnk","Codex.lnk"},item.Name)<0)continue;
                string path=Path.Combine(Paths.Programs,item.Name);string current=desktop.ShortcutTarget(path);
                if(!JsonFiles.SamePath(current,Paths.Manager)&&!JsonFiles.SamePath(current,Paths.FilePath("ChatGPTTheme.exe")))continue;
                if(item.RestoreOfficial&&package!=null&&package.Found)desktop.OfficialShortcut(path,package.AppId);
                else if(File.Exists(path))File.Delete(path);
            }
        }
        public void PrepareUninstall(PackageInfo package,bool deletePreferences){
            var state=State();if(state==null)throw new IOException("此修改器尚未安装。");
            Disable(package,deletePreferences);RestoreShortcuts(state,package);desktop.Unregister(Paths);
        }
        public string Worker(){
            string dir=Path.Combine(Path.GetTempPath(),"CodexThemeModifier",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string exe=Path.Combine(dir,"MaintenanceWorker.exe");Resources.Extract("payload.MaintenanceWorker.exe",exe);return exe;
        }
        public void RunMaintenance(MaintenancePlan plan){
            var state=State();if(state==null)throw new IOException("安装状态不存在。");
            plan.ProductId=BuildInfo.ProductId;plan.InstanceId=state.InstanceId;plan.Root=Paths.Root;plan.ParentId=Process.GetCurrentProcess().Id;plan.ParentPath=Assembly.GetExecutingAssembly().Location;
            string worker=Worker();string file=Path.Combine(Path.GetDirectoryName(worker),"plan.json");JsonFiles.Write(file,plan);WindowsDesktop.RunHidden(worker,"\""+file+"\"");
        }
        public void UpdatePreference(bool automatic){var state=State();if(state==null)return;state.CheckUpdates=automatic;JsonFiles.Write(Paths.State,state);}
    }
}
