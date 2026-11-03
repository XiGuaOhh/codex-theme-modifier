using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;
namespace CodexThemeModifier {
    internal static class Resources {
        public static byte[] Bytes(string name){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(s==null)throw new FileNotFoundException("Missing bundled resource: "+name);using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();}}}
        public static string Text(string name){return Encoding.UTF8.GetString(Bytes(name)).TrimStart('\uFEFF');}
        public static void Extract(string name,string destination){
            if(File.Exists(destination)&&(File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Managed files cannot be symbolic links.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));string temp=destination+".new-"+Guid.NewGuid().ToString("N");
            try{File.WriteAllBytes(temp,Bytes(name));if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
    internal interface IDesktop {
        PackageInfo Probe();
        void Shortcut(string path,string target,string arguments,string icon);
        string ShortcutTarget(string path);
        void OfficialShortcut(string path,string appId);
        void Register(AppPaths paths);
        void Unregister(AppPaths paths);
        void Launch(string file,string arguments);
        void StopHelper(AppPaths paths);
    }
    internal sealed class WindowsDesktop:IDesktop {
        public static string PowerShell {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");}}
        public static Process RunHidden(string file,string args){return Process.Start(new ProcessStartInfo {FileName=file,Arguments=args,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});}
        public PackageInfo Probe(){
            string encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(Resources.Text("payload.PackageInfo.ps1")));
            var p=Process.Start(new ProcessStartInfo{FileName=PowerShell,Arguments="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "+encoded,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8});
            using(p){var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(15000)){try{p.Kill();}catch{}throw new IOException("检测 Codex 超时，请重试。");}string result=output.GetAwaiter().GetResult();error.GetAwaiter().GetResult();if(p.ExitCode!=0)throw new IOException("无法读取官方 Codex 的安装信息。");return JsonFiles.Serializer().Deserialize<PackageInfo>(result);}
        }
        public void Shortcut(string path,string target,string arguments,string icon){ShellLinks.Create(path,target,arguments,icon);}
        public string ShortcutTarget(string path){return ShellLinks.Target(path);}
        public void OfficialShortcut(string path,string appId){ShellLinks.CreateApp(path,appId);}
        public void Register(AppPaths paths){using(var key=Registry.CurrentUser.CreateSubKey(paths.UninstallKey)){key.SetValue("DisplayName","Codex Theme Modifier / Codex 主题修改器");key.SetValue("DisplayVersion",BuildInfo.Version);key.SetValue("Publisher","XiGuaOhh (independent project)");key.SetValue("InstallLocation",paths.Root);key.SetValue("DisplayIcon",paths.Manager+",0");key.SetValue("UninstallString","\""+paths.Manager+"\" --uninstall");key.DeleteValue("QuietUninstallString",false);key.SetValue("URLInfoAbout",BuildInfo.Website);key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);}}
        public void Unregister(AppPaths paths){Registry.CurrentUser.DeleteSubKeyTree(paths.UninstallKey,false);}
        public void Launch(string file,string args){Process.Start(new ProcessStartInfo {FileName=file,Arguments=args,UseShellExecute=true});}
        public void StopHelper(AppPaths paths){
            string status=paths.FilePath("theme-host-state.json");if(!File.Exists(status))return;
            try{
                var data=JsonFiles.Serializer().DeserializeObject(File.ReadAllText(status,Encoding.UTF8)) as System.Collections.Generic.Dictionary<string,object>;if(data==null||!data.ContainsKey("hostPid"))return;
                int pid=Convert.ToInt32(data["hostPid"]);using(var p=Process.GetProcessById(pid)){
                    if(!String.Equals(p.ProcessName,"powershell",StringComparison.OrdinalIgnoreCase)||!JsonFiles.SamePath(p.MainModule.FileName,PowerShell))return;
                    string filter="ProcessId = "+pid;
                    using(var search=new System.Management.ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE "+filter))foreach(System.Management.ManagementObject item in search.Get()){
                        string cmd=Convert.ToString(item["CommandLine"]);if(cmd.IndexOf(Path.Combine(paths.Root,"ThemeHost.ps1"),StringComparison.OrdinalIgnoreCase)>=0&&cmd.IndexOf("-BrowserProfile",StringComparison.OrdinalIgnoreCase)<0){
                            // Let the helper unregister its document-start script before exiting.
                            File.WriteAllText(paths.FilePath("disabled.flag"),"disabled",Encoding.UTF8);
                            if(!p.WaitForExit(4500)){p.Kill();p.WaitForExit(3000);}
                        }
                    }
                }
            }catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
        }
    }
    internal static class ShellLinks {
        [ComImport,Guid("00021401-0000-0000-C000-000000000046")]class LinkObject{}
        [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ILink {
            void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int size,IntPtr data,uint flags);
            void GetIDList(out IntPtr pidl);void SetIDList(IntPtr pidl);
            void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int size);void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string value);
            void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int size);void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string value);
            void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int size);void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string value);
            void GetHotkey(out short key);void SetHotkey(short key);void GetShowCmd(out int cmd);void SetShowCmd(int cmd);
            void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int size,out int index);void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string value,int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string value,uint flags);void Resolve(IntPtr hwnd,uint flags);void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
        }
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHParseDisplayName(string name,IntPtr bind,out IntPtr pidl,uint attributes,out uint result);
        public static void Create(string file,string target,string args,string icon){
            Directory.CreateDirectory(Path.GetDirectoryName(file));object o=new LinkObject();try{var l=(ILink)o;l.SetPath(target);l.SetArguments(args??"");l.SetWorkingDirectory(Path.GetDirectoryName(target));l.SetDescription("Codex Theme Modifier");if(!String.IsNullOrEmpty(icon))l.SetIconLocation(icon,0);((IPersistFile)o).Save(file,true);}finally{Marshal.FinalReleaseComObject(o);}
        }
        public static string Target(string file){if(!File.Exists(file))return null;object o=new LinkObject();try{((IPersistFile)o).Load(file,0);var result=new StringBuilder(32768);((ILink)o).GetPath(result,result.Capacity,IntPtr.Zero,4);return result.ToString();}finally{Marshal.FinalReleaseComObject(o);}}
        public static void CreateApp(string file,string appId){
            Directory.CreateDirectory(Path.GetDirectoryName(file));IntPtr pidl;uint ignored;Marshal.ThrowExceptionForHR(SHParseDisplayName("shell:AppsFolder\\"+appId,IntPtr.Zero,out pidl,0,out ignored));
            object o=new LinkObject();try{((ILink)o).SetIDList(pidl);((IPersistFile)o).Save(file,true);}finally{Marshal.FreeCoTaskMem(pidl);Marshal.FinalReleaseComObject(o);}
        }
    }
}
