using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace CodexThemeModifier {
    internal static class MaintenanceCore {
        public static void Validate(MaintenancePlan plan,AppPaths allowed){
            allowed.CheckRoot();if(plan.ProductId!=BuildInfo.ProductId||!JsonFiles.SamePath(plan.Root,allowed.Root))throw new InvalidDataException("Invalid maintenance target.");
            var state=JsonFiles.Read<InstallState>(allowed.State);if(state.ProductId!=BuildInfo.ProductId||String.IsNullOrEmpty(plan.InstanceId)||state.InstanceId!=plan.InstanceId)throw new InvalidDataException("Installation identity changed.");
            if(plan.Operation!="update"&&plan.Operation!="uninstall")throw new InvalidDataException("Invalid maintenance operation.");
        }
        public static void Apply(MaintenancePlan plan,AppPaths allowed){
            Validate(plan,allowed);
            if(plan.Operation=="update"){
                UpdateService.ValidateDownloaded(plan.Source,plan.Sha256);
                string backup=allowed.Manager+".previous";
                if(File.Exists(allowed.Manager))File.Copy(allowed.Manager,backup,true);
                try{JsonFiles.CopyAtomic(plan.Source,allowed.Manager);}catch{if(File.Exists(backup))JsonFiles.CopyAtomic(backup,allowed.Manager);throw;}
                if(File.Exists(backup))File.Delete(backup);
            }else{
                if(!plan.DeletePreferences)JsonFiles.Write(Path.Combine(allowed.Preferences,"retained-state.json"),new InstallState{ProductId=BuildInfo.ProductId,InstanceId=plan.InstanceId,Version=BuildInfo.Version});
                foreach(string name in BuildInfo.OwnedFiles){if(name=="modifier-state.json")continue;string file=allowed.FilePath(name);if(File.Exists(file))File.Delete(file);}
                if(plan.DeletePreferences){
                    foreach(string name in new[]{"theme.json","background.bin","applied-revision.txt","retained-state.json","modifier-ui.json"}){string file=Path.Combine(allowed.Preferences,name);if(File.Exists(file))File.Delete(file);}
                    // A tiny reset marker prevents old in-app preferences from returning on reinstall.
                    JsonFiles.Write(Path.Combine(allowed.Preferences,"retained-state.json"),new InstallState{ProductId=BuildInfo.ProductId,InstanceId=plan.InstanceId,Version=BuildInfo.Version,ResetBrowserPreferences=true});
                }
                if(File.Exists(allowed.State))File.Delete(allowed.State);
                if(Directory.Exists(allowed.Root)&&Directory.GetFileSystemEntries(allowed.Root).Length==0)Directory.Delete(allowed.Root);
            }
        }
    }
    internal static class WorkerProgram {
        [STAThread]static void Main(string[] args){
            try{
                if(args.Length!=1)throw new InvalidDataException("Missing maintenance plan.");
                var plan=JsonFiles.Read<MaintenancePlan>(Path.GetFullPath(args[0]));var paths=AppPaths.Current();MaintenanceCore.Validate(plan,paths);
                if(plan.ParentId==Process.GetCurrentProcess().Id)throw new InvalidDataException("Invalid parent.");
                try{using(var parent=Process.GetProcessById(plan.ParentId)){if(!JsonFiles.SamePath(parent.MainModule.FileName,plan.ParentPath))throw new InvalidDataException("Unexpected parent process.");if(!parent.WaitForExit(60000))throw new IOException("修改器窗口仍在运行，请关闭后重试。");}}catch(ArgumentException){}
                Exception last=null;for(int i=0;i<10;i++){try{MaintenanceCore.Apply(plan,paths);last=null;break;}catch(IOException e){last=e;Thread.Sleep(500);}}
                if(last!=null)throw last;
                if(plan.Operation=="update")Process.Start(new ProcessStartInfo{FileName=paths.Manager,Arguments="--post-update",UseShellExecute=true});
                else MessageBox.Show("修改功能已卸载，Codex 和聊天资料保留。\n\n当前 Codex 可以继续使用；完整退出重开后恢复全部原版状态。","Codex Theme Modifier",MessageBoxButtons.OK,MessageBoxIcon.Information);
                try{File.Delete(args[0]);}catch{}
            }catch(Exception){MessageBox.Show("维护操作未完成。原版 Codex 未被删除。\n请重新打开修改器进行修复，或从 GitHub 下载最新版本。","Codex Theme Modifier",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
    }
}
