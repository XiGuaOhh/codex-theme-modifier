using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace CodexThemeModifier {
    internal static class MaintenanceCore {
        [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int id);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder path,ref int length);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
        public static void WaitForParent(MaintenancePlan plan){
            if(plan.ParentId<=0||plan.ParentId==Process.GetCurrentProcess().Id)throw new InvalidDataException("Invalid parent.");
            Process parent;try{parent=Process.GetProcessById(plan.ParentId);}catch(ArgumentException){return;}
            using(parent)WaitForParentProcess(parent,plan.ParentPath,60000);
        }
        public static void WaitForParentProcess(Process parent,string expectedPath,int timeout){
            if(parent.HasExited)return;
            string path;
            try{
                // Query the executable path without reading modules in an exiting process.
                IntPtr handle=OpenProcess(0x1000,false,parent.Id);
                if(handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                try{var name=new StringBuilder(32768);int length=name.Capacity;if(!QueryFullProcessImageName(handle,0,name,ref length))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());path=name.ToString();}finally{CloseHandle(handle);}
            }catch(System.ComponentModel.Win32Exception){if(parent.HasExited)return;throw;}
            if(!JsonFiles.SamePath(path,expectedPath))throw new InvalidDataException("Unexpected parent process.");
            if(!parent.WaitForExit(timeout))throw new IOException("修改器窗口仍在运行，请关闭后重试。");
        }
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
            string stage="read-plan";
            try{
                if(args.Length!=1)throw new InvalidDataException("Missing maintenance plan.");
                var plan=JsonFiles.Read<MaintenancePlan>(Path.GetFullPath(args[0]));var paths=AppPaths.Current();stage="validate-target";MaintenanceCore.Validate(plan,paths);
                stage="wait-for-parent";
                MaintenanceCore.WaitForParent(plan);
                stage="apply-maintenance";
                Exception last=null;for(int i=0;i<10;i++){try{MaintenanceCore.Apply(plan,paths);last=null;break;}catch(IOException e){last=e;Thread.Sleep(500);}}
                if(last!=null)throw last;
                stage="reopen-manager";
                if(plan.Operation=="update")Process.Start(new ProcessStartInfo{FileName=paths.Manager,Arguments="--post-update",UseShellExecute=true});
                else MessageBox.Show("修改功能已卸载，Codex 和聊天资料保留。\n\n当前 Codex 可以继续使用；完整退出重开后恢复全部原版状态。","Codex Theme Modifier",MessageBoxButtons.OK,MessageBoxIcon.Information);
                try{File.Delete(args[0]);}catch{}
            }catch(Exception error){
                try{JsonFiles.Write(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"maintenance-error.json"),new{Stage=stage,Error=error.GetType().Name,Message=error.Message});}catch{}
                MessageBox.Show("维护操作未完成。原版 Codex 未被删除。\n请重新打开修改器进行修复，或从 GitHub 下载最新版本。","Codex Theme Modifier",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }
    }
}
