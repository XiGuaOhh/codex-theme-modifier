using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace CodexThemeModifier {
    internal static class RuntimeClient {
        static int Port(PackageInfo package){if(package==null||!package.Found)return 0;CodexThemeNative.ConfigureExecutable(package.Executable.Substring(package.Root.TrimEnd('\\').Length+1));return CodexThemeMonitor.ActivePort(CodexThemeNative.DefaultBrowserProfile(package.PackageFamily),package.PackageFamily);}
        public static ThemeConfig ReadSettings(PackageInfo package){
            int port=Port(package);if(port==0)return null;
            foreach(object raw in CodexThemeMonitor.Targets(port)){
                var target=raw as Dictionary<string,object>;if(target==null||!CodexThemeMonitor.IsMainTarget(target))continue;
                using(var c=new CodexThemeSocket(Convert.ToString(target["webSocketDebuggerUrl"]),port)){
                    var result=c.Send("Runtime.evaluate",new {expression="localStorage.getItem('codex.custom-background.settings.v1')",returnByValue=true,silent=true});
                    var value=result["result"] as Dictionary<string,object>;if(value==null||!value.ContainsKey("value")||value["value"]==null)return null;
                    return JsonFiles.Serializer().Deserialize<ThemeConfig>(Convert.ToString(value["value"]));
                }
            }return null;
        }
        public static bool OpenPanel(PackageInfo package){
            int port=Port(package);if(port==0)return false;
            foreach(object raw in CodexThemeMonitor.Targets(port)){
                var target=raw as Dictionary<string,object>;if(target==null||!CodexThemeMonitor.IsMainTarget(target))continue;
                using(var c=new CodexThemeSocket(Convert.ToString(target["webSocketDebuggerUrl"]),port)){
                    var result=c.Send("Runtime.evaluate",new {expression="(()=>{const b=document.getElementById('codex-background-controls')?.shadowRoot?.getElementById('toggle');if(!b)return false;if(b.getAttribute('aria-expanded')!=='true')b.click();return true;})()",returnByValue=true,silent=true});
                    var value=result["result"] as Dictionary<string,object>;return value!=null&&value.ContainsKey("value")&&Object.Equals(value["value"],true);
                }
            }return false;
        }
        public static bool Remove(PackageInfo package,bool clear){
            int port=Port(package);if(port==0)return false;bool removed=false;
            foreach(object raw in CodexThemeMonitor.Targets(port)){
                var target=raw as Dictionary<string,object>;if(target==null||!CodexThemeMonitor.IsMainTarget(target))continue;
                using(var c=new CodexThemeSocket(Convert.ToString(target["webSocketDebuggerUrl"]),port)){c.RemoveAddon(clear);removed=true;}
            }return removed;
        }
        public static bool Connected(PackageInfo package){try{return Port(package)>0;}catch{return false;}}
        public static bool ApplySettings(PackageInfo package,string root,ThemeConfig config){
            int port=Port(package);if(port==0)return false;bool first=true;
            foreach(object raw in CodexThemeMonitor.Targets(port)){
                var target=raw as Dictionary<string,object>;if(target==null||!CodexThemeMonitor.IsMainTarget(target))continue;
                using(var c=new CodexThemeSocket(Convert.ToString(target["webSocketDebuggerUrl"]),port)){
                    if(first){var data=JsonFiles.Serializer().DeserializeObject(JsonFiles.Serializer().Serialize(config)) as Dictionary<string,object>;CodexThemeMonitor.ApplyPreferences(c,root,data);first=false;}
                    else c.Send("Runtime.evaluate",new{expression="window.__codexBackgroundReload?.()",awaitPromise=true,returnByValue=true,silent=true},20000);
                }
            }return !first;
        }
    }
}
