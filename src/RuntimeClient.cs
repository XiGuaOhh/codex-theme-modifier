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
        public static byte[] ReadBackgroundPreview(PackageInfo package){
            int port=Port(package);if(port==0)return null;
            foreach(object raw in CodexThemeMonitor.Targets(port)){
                var target=raw as Dictionary<string,object>;if(target==null||!CodexThemeMonitor.IsMainTarget(target))continue;
                using(var c=new CodexThemeSocket(Convert.ToString(target["webSocketDebuggerUrl"]),port)){
                    string expression=@"(async()=>{
                        const name='codex-custom-background-v1';
                        if(!(await indexedDB.databases()).some(d=>d.name===name))return null;
                        const db=await new Promise((resolve,reject)=>{const r=indexedDB.open(name,1);r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(Error('Preview unavailable'));});
                        try{
                            const image=await new Promise((resolve,reject)=>{const tx=db.transaction('images','readonly');const r=tx.objectStore('images').get('background');r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(Error('Preview unavailable'));});
                            if(!image?.blob)return null;
                            const bitmap=await createImageBitmap(image.blob);
                            try{if(!bitmap.width||!bitmap.height||bitmap.width*bitmap.height>60000000)return null;const scale=Math.min(1,900/bitmap.width,600/bitmap.height);const canvas=document.createElement('canvas');canvas.width=Math.max(1,Math.round(bitmap.width*scale));canvas.height=Math.max(1,Math.round(bitmap.height*scale));const ctx=canvas.getContext('2d');ctx.fillStyle='#e9edf4';ctx.fillRect(0,0,canvas.width,canvas.height);ctx.drawImage(bitmap,0,0,canvas.width,canvas.height);return canvas.toDataURL('image/jpeg',0.84);}finally{bitmap.close();}
                        }finally{db.close();}
                    })()";
                    var response=c.Send("Runtime.evaluate",new{expression=expression,awaitPromise=true,returnByValue=true,silent=true},15000);
                    var result=response["result"] as Dictionary<string,object>;object value;if(result==null||!result.TryGetValue("value",out value)||value==null)return null;
                    string data=Convert.ToString(value),prefix="data:image/jpeg;base64,";if(!data.StartsWith(prefix,StringComparison.Ordinal)||data.Length>1500000)return null;
                    return Convert.FromBase64String(data.Substring(prefix.Length));
                }
            }return null;
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
