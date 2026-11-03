using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace CodexThemeModifier {
    // Optional integration test. Its browser profile and helper are isolated from normal Codex.
    internal static class NativeSmoke {
        static int checks;
        static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
        static bool Wait(Func<bool> predicate,int seconds){var watch=Stopwatch.StartNew();do{try{if(predicate())return true;}catch{}Thread.Sleep(350);}while(watch.Elapsed.TotalSeconds<seconds);return false;}
        static object Evaluate(CodexThemeSocket connection,string expression){var response=connection.Send("Runtime.evaluate",new{expression=expression,returnByValue=true,awaitPromise=true,silent=true},10000);if(response.ContainsKey("exceptionDetails"))throw new Exception("Evaluation failed");var result=response["result"] as Dictionary<string,object>;return result!=null&&result.ContainsKey("value")?result["value"]:null;}
        static Dictionary<string,object> MainTarget(int port){foreach(object raw in CodexThemeMonitor.Targets(port)){var t=raw as Dictionary<string,object>;if(t!=null&&CodexThemeMonitor.IsMainTarget(t))return t;}return null;}
        [STAThread]static int Main(string[] args){
            string root=Path.GetFullPath(args[0]),runtime=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
            PackageInfo package=null;Process helper=null;uint browser=0;CodexThemeSocket socket=null;
            var before=new HashSet<int>(Process.GetProcessesByName("ChatGPT").Select(p=>p.Id));
            try{
                package=new WindowsDesktop().Probe();Check(package.Found,"registered-official-package-found");
                CodexThemeNative.ConfigureExecutable(package.Executable.Substring(package.Root.TrimEnd('\\').Length+1));
                foreach(var p in Process.GetProcessesByName(CodexThemeNative.ProcessName))before.Add(p.Id);
                string executableHash=JsonFiles.Hash(package.Executable),asar=Path.Combine(Path.GetDirectoryName(package.Executable),"resources","app.asar");string asarHash=File.Exists(asar)?JsonFiles.Hash(asar):null;
                foreach(string name in new[]{"ThemeHost.ps1","ThemeHost.cs","custom-background.js"})File.Copy(Path.Combine(runtime,name),Path.Combine(root,name));
                string profile=Path.Combine(root,"browser-profile"),status=Path.Combine(root,"status.json");
                browser=CodexThemeNative.Launch(package.AppId,CodexThemeNative.ThemeArguments+" --user-data-dir=\""+profile+"\"");
                Check(!before.Contains((int)browser)&&CodexThemeNative.IsOfficialProcess(browser,package.PackageFamily),"test-browser-is-separate-and-retains-package-identity");
                int port=0;Check(Wait(()=>{port=CodexThemeMonitor.ActivePort(profile,package.PackageFamily);return port>0;},45),"loopback-endpoint-owner-and-session-verified");
                Check(CodexThemeNative.ListenerPid(port)==browser,"endpoint-belongs-to-test-browser");
                Dictionary<string,object> target=null;Check(Wait(()=>{target=MainTarget(port);return target!=null;},30),"eligible-main-window-found");
                socket=new CodexThemeSocket(Convert.ToString(target["webSocketDebuggerUrl"]),port);
                Check(Wait(()=>Object.Equals(Evaluate(socket,"Boolean(window.electronBridge && document.getElementById('root')?.childElementCount)"),true),30),"official-window-ready");
                Thread.Sleep(3000);
                Evaluate(socket,"window.__ctmBaseline={fetch:window.fetch,bridge:window.electronBridge,storage:Storage.prototype.setItem,ws:WebSocket.prototype.send};true");
                Directory.CreateDirectory(Path.Combine(root,"preferences"));string image=Path.Combine(root,"preferences","background.bin");using(var b=new Bitmap(3,3)){using(var g=Graphics.FromImage(b))g.Clear(Color.CornflowerBlue);b.Save(image,System.Drawing.Imaging.ImageFormat.Png);}
                var config=new ThemeConfig{revision=Guid.NewGuid().ToString("N"),dim=23,panel=39,bubble=27,imageAction="replace",imageFile="preferences/background.bin",imageMime="image/png",imageName="integration.png"};JsonFiles.Write(Path.Combine(root,"preferences","theme.json"),config);
                helper=WindowsDesktop.RunHidden(WindowsDesktop.PowerShell,"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(root,"ThemeHost.ps1")+"\" -AttachOnly -BrowserProfile \""+profile+"\" -StatusFile \""+status+"\"");
                Check(Wait(()=>Object.Equals(Evaluate(socket,"window.__codexBackgroundVersion === '"+BuildInfo.Version+"' && document.querySelectorAll('#codex-background-controls').length === 1"),true),45),"background-controls-loaded-once");
                Check(Wait(()=>Object.Equals(Evaluate(socket,"document.documentElement.hasAttribute('data-cbg-enabled') && JSON.parse(localStorage.getItem('codex.custom-background.settings.v1')||'{}').dim === 23"),true),30),"image-and-transparency-applied");
                var unchanged=Evaluate(socket,"[window.fetch===__ctmBaseline.fetch,window.electronBridge===__ctmBaseline.bridge,Storage.prototype.setItem===__ctmBaseline.storage,WebSocket.prototype.send===__ctmBaseline.ws]") as object[];
                if(unchanged!=null&&!unchanged.All(x=>Object.Equals(x,true)))Console.WriteLine("API stability flags "+JsonFiles.Serializer().Serialize(unchanged));
                Check(unchanged!=null&&unchanged.All(x=>Object.Equals(x,true)),"official-bridge-and-browser-apis-unchanged");
                Evaluate(socket,"document.title='中文测试：透明主题 ✅';true");
                Check(CodexThemeMonitor.Targets(port).OfType<Dictionary<string,object>>().Any(t=>t.ContainsKey("title")&&Convert.ToString(t["title"])=="中文测试：透明主题 ✅"),"chinese-target-json-round-trips-as-utf8");
                Check(Wait(()=>File.Exists(Path.Combine(root,"preferences","applied-revision.txt")),15),"background-config-acknowledged");
                File.WriteAllText(Path.Combine(root,"disabled.flag"),"disabled");
                Check(helper.WaitForExit(15000)&&helper.ExitCode==0,"helper-exits-gracefully-on-disable");
                Check(Object.Equals(Evaluate(socket,"!document.getElementById('codex-background-controls') && !document.getElementById('codex-custom-background-styles') && !window.__codexBackgroundVersion && !document.documentElement.hasAttribute('data-cbg-enabled')"),true),"disable-removes-only-theme-ui");
                Check(Object.Equals(Evaluate(socket,"window.fetch===__ctmBaseline.fetch && window.electronBridge===__ctmBaseline.bridge"),true),"disable-preserves-official-runtime");
                socket.Send("Page.reload",new{ignoreCache=false});
                Check(Wait(()=>Object.Equals(Evaluate(socket,"Boolean(window.electronBridge && document.body) && !window.__codexBackgroundVersion && !document.getElementById('codex-background-controls')"),true),30),"reload-does-not-reinstall-disabled-theme");
                string shortcut=Path.Combine(root,"official-restored.lnk");ShellLinks.CreateApp(shortcut,package.AppId);Check(File.Exists(shortcut)&&new FileInfo(shortcut).Length>76,"official-shell-shortcut-pidl-can-be-restored");
                Check(JsonFiles.Hash(package.Executable)==executableHash&&(asarHash==null||JsonFiles.Hash(asar)==asarHash),"official-executable-and-resources-stay-identical");
                Check(before.All(id=>{try{return !Process.GetProcessById(id).HasExited;}catch{return false;}}),"preexisting-codex-processes-preserved");
                Console.WriteLine("SUCCESS "+checks+" native checks; official "+package.Version);return 0;
            }catch(Exception e){Console.Error.WriteLine("FAIL "+e.GetType().Name+": "+e.Message);return 1;}
            finally{
                if(helper!=null){try{File.WriteAllText(Path.Combine(root,"disabled.flag"),"disabled");if(!helper.WaitForExit(16000))helper.Kill();}catch{}helper.Dispose();}
                if(socket!=null)socket.Dispose();
                // Never stop a preexisting process, and never terminate a process tree.
                if(browser!=0&&!before.Contains((int)browser)&&package!=null&&CodexThemeNative.IsOfficialProcess(browser,package.PackageFamily))try{using(var owned=Process.GetProcessById((int)browser))owned.Kill();}catch{}
            }
        }
    }
}
