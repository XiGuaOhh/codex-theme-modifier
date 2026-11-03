using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
internal static class ThemeLauncher {
    [STAThread]static void Main(string[] args){
        string root=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        bool original=Array.IndexOf(args,"--original")>=0;
        try{
            var p=Process.Start(new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),Arguments="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(root,"Start.ps1")+"\" -Quiet"+(original?" -Original":""),WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
            using(p){if(!p.WaitForExit(40000)){try{p.Kill();}catch{}OpenOriginal(root);}else if(p.ExitCode!=0)OpenOriginal(root);}
        }catch{OpenOriginal(root);}
    }
    static void OpenOriginal(string root){
        string app="OpenAI.Codex_2p2nqsd0c76g0!App";
        try{string file=Path.Combine(root,"modifier-state.json");if(File.Exists(file)){var s=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(file)) as System.Collections.Generic.Dictionary<string,object>;object id;if(s!=null&&s.TryGetValue("OfficialAppId",out id)&&Convert.ToString(id).StartsWith("OpenAI.Codex_",StringComparison.Ordinal)&&Convert.ToString(id).IndexOf('!')>0)app=Convert.ToString(id);}}catch{}
        try{Process.Start(new ProcessStartInfo{FileName="explorer.exe",Arguments="shell:AppsFolder\\"+app,UseShellExecute=true});}catch{}
    }
}
