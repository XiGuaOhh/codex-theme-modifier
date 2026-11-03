using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
namespace CodexThemeModifier {
    internal static class Program {
        [STAThread]static int Main(string[] args){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try{
                var desktop=new WindowsDesktop();var paths=AppPaths.Current();var engine=new ManagerEngine(paths,desktop);
                if(Array.IndexOf(args,"--inspect")>=0){var p=desktop.Probe();Console.WriteLine(JsonFiles.Serializer().Serialize(new{version=BuildInfo.Version,package=p,installed=engine.State()!=null,github=BuildInfo.Website}));return 0;}
                int preview=Array.IndexOf(args,"--preview");if(preview>=0){if(preview+1>=args.Length)return 2;int page=Array.IndexOf(args,"--page");using(var f=new ManagerForm(engine,desktop,true)){f.ConfigurePreview(Array.IndexOf(args,"--dark")>=0,page>=0&&page+1<args.Length?args[page+1]:"appearance",Array.IndexOf(args,"--compact")>=0);f.SavePreview(Path.GetFullPath(args[preview+1]));}return 0;}
                if(Array.IndexOf(args,"--check-update")>=0){var u=UpdateService.Check();Console.WriteLine(JsonFiles.Serializer().Serialize(new{version=BuildInfo.Version,latest=u==null?null:u.Version,newer=u!=null&&u.Newer,digest=u==null?null:u.Digest}));return 0;}
                bool created;using(var mutex=new Mutex(true,"Local\\CodexThemeModifier.Manager",out created)){
                    if(!created){MessageBox.Show("修改器窗口已在运行。","Codex Theme Modifier",MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;}
                    using(var form=new ManagerForm(engine,desktop,false)){
                        if(Array.IndexOf(args,"--uninstall")>=0)form.RequestUninstall();
                        if(Array.IndexOf(args,"--post-update")>=0)form.MarkPostUpdate();
                        Application.Run(form);
                    }
                    mutex.ReleaseMutex();
                }return 0;
            }catch(Exception e){if(args.Length>0){Console.Error.WriteLine(e.GetType().Name);return 1;}MessageBox.Show("修改器未能启动，请从 GitHub 下载最新版本。\n官方 Codex 可以继续正常使用。","Codex Theme Modifier",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
        }
    }
}
