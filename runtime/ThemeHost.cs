// Loads only local appearance code into the registered, unmodified Windows app.
// Does not pause execution or intercept networking or native application APIs.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

public static class CodexThemeNative {
    public const string ThemeArguments="--remote-debugging-port=0 --remote-debugging-address=127.0.0.1";
    static string relativeExecutable=Path.Combine("app","ChatGPT.exe");
    public static string ProcessName {get{return Path.GetFileNameWithoutExtension(relativeExecutable);}}
    public static void ConfigureExecutable(string relative) {
        if(String.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||Array.IndexOf(relative.Replace('/','\\').Split('\\'),"..")>=0||!relative.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid application executable.");
        relativeExecutable=relative.Replace('/','\\');
    }
    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IActivationManager {
        [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string args, uint flags, out uint pid);
        [PreserveSig] int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint pid);
        [PreserveSig] int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items, out uint pid);
    }
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern int GetPackageFullName(IntPtr process, ref uint length, StringBuilder name);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern int GetPackageFamilyName(IntPtr process, ref uint length, StringBuilder name);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern int GetPackagePathByFullName(string name, ref uint length, StringBuilder path);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint length);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("iphlpapi.dll", SetLastError=true)] static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
    [StructLayout(LayoutKind.Sequential)] struct TcpRow { public uint State, LocalAddress, LocalPort, RemoteAddress, RemotePort, Pid; }

    public static uint Launch(string aumid, string args) {
        object instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));
        try { uint pid; int result=((IActivationManager)instance).ActivateApplication(aumid,args,2,out pid); Marshal.ThrowExceptionForHR(result); return pid; }
        finally { Marshal.FinalReleaseComObject(instance); }
    }
    public static string PackageName(uint pid) { return ProcessValue(pid, false); }
    public static string PackageFamily(uint pid) { return ProcessValue(pid, true); }
    public static string DefaultBrowserProfile(string family) {
        string packaged=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Packages",family,"LocalCache","Roaming","Codex","web","Codex");
        string ordinary=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Codex","web","Codex");
        // Start menu launchers run outside the app's merged AppData view.
        // Use its physical profile so both launch contexts reach the same data.
        if(Directory.Exists(packaged) || !Directory.Exists(ordinary)) return packaged;
        return ordinary;
    }
    static string ProcessValue(uint pid, bool family) {
        IntPtr handle=OpenProcess(0x1000,false,pid);
        if(handle==IntPtr.Zero) return null;
        try {
            uint length=0;
            int error=family ? GetPackageFamilyName(handle,ref length,null) : GetPackageFullName(handle,ref length,null);
            if(error!=122) return null;
            var value=new StringBuilder((int)length);
            error=family ? GetPackageFamilyName(handle,ref length,value) : GetPackageFullName(handle,ref length,value);
            return error==0 ? value.ToString() : null;
        } finally { CloseHandle(handle); }
    }
    public static bool IsOfficialProcess(uint pid, string family) {
        if(PackageFamily(pid)!=family) return false;
        string package=PackageName(pid);
        if(package==null) return false;
        uint length=0;
        if(GetPackagePathByFullName(package,ref length,null)!=122) return false;
        var root=new StringBuilder((int)length);
        if(GetPackagePathByFullName(package,ref length,root)!=0) return false;
        IntPtr handle=OpenProcess(0x1000,false,pid);
        if(handle==IntPtr.Zero) return false;
        try {
            var path=new StringBuilder(32768); length=32768;
            return QueryFullProcessImageName(handle,0,path,ref length) && String.Equals(Path.Combine(root.ToString(),relativeExecutable),path.ToString(),StringComparison.OrdinalIgnoreCase);
        } finally { CloseHandle(handle); }
    }
    public static uint ListenerPid(int port) {
        int size=0;
        if(GetExtendedTcpTable(IntPtr.Zero,ref size,false,2,3,0)!=122) return 0;
        IntPtr table=Marshal.AllocHGlobal(size);
        try {
            if(GetExtendedTcpTable(table,ref size,false,2,3,0)!=0) return 0;
            int count=Marshal.ReadInt32(table), rowSize=Marshal.SizeOf(typeof(TcpRow));
            for(int i=0;i<count;i++) {
                var row=(TcpRow)Marshal.PtrToStructure(IntPtr.Add(table,4+i*rowSize),typeof(TcpRow));
                int rowPort=(int)(((row.LocalPort&255)<<8)|((row.LocalPort>>8)&255));
                if(rowPort==port && new IPAddress(row.LocalAddress).Equals(IPAddress.Loopback)) return row.Pid;
            }
            return 0;
        } finally { Marshal.FreeHGlobal(table); }
    }
    public static bool HasOfficialProcess(string family) {
        foreach(var process in Process.GetProcessesByName(ProcessName)) {
            using(process) { try { if(IsOfficialProcess((uint)process.Id,family)) return true; } catch {} }
        }
        return false;
    }
}

public sealed class CodexThemeSocket : IDisposable {
    readonly ClientWebSocket socket=new ClientWebSocket();
    readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=2*1024*1024};
    int nextId;
    string installedIdentifier;
    public CodexThemeSocket(string address, int port) {
        var uri=new Uri(address);
        if(uri.Scheme!="ws" || uri.Host!="127.0.0.1" || uri.Port!=port) throw new InvalidDataException("Unexpected local endpoint.");
        socket.Options.Proxy=null;
        try {
            using(var timeout=new CancellationTokenSource(5000)) socket.ConnectAsync(uri,timeout.Token).GetAwaiter().GetResult();
        } catch { socket.Dispose(); throw; }
    }
    public Dictionary<string,object> Send(string method, object parameters, int timeoutMs=5000) {
        int id=++nextId;
        byte[] message=Encoding.UTF8.GetBytes(json.Serialize(new {id=id,method=method,@params=parameters}));
        using(var timeout=new CancellationTokenSource(timeoutMs)) {
            socket.SendAsync(new ArraySegment<byte>(message),WebSocketMessageType.Text,true,timeout.Token).GetAwaiter().GetResult();
            while(true) {
                using(var body=new MemoryStream()) {
                    var buffer=new byte[8192]; WebSocketReceiveResult received;
                    do {
                        received=socket.ReceiveAsync(new ArraySegment<byte>(buffer),timeout.Token).GetAwaiter().GetResult();
                        if(received.MessageType==WebSocketMessageType.Close) throw new IOException("Application window closed.");
                        body.Write(buffer,0,received.Count);
                        if(body.Length>2*1024*1024) throw new IOException("Unexpected message size.");
                    } while(!received.EndOfMessage);
                    var result=json.DeserializeObject(Encoding.UTF8.GetString(body.ToArray())) as Dictionary<string,object>;
                    if(result==null || !result.ContainsKey("id") || Convert.ToInt32(result["id"])!=id) continue;
                    if(result.ContainsKey("error")) throw new IOException("Appearance command unavailable.");
                    return (Dictionary<string,object>)result["result"];
                }
            }
        }
    }
    public bool Install(string script) {
        var installed=Send("Page.addScriptToEvaluateOnNewDocument",new {source=script});
        object identifier;if(installed.TryGetValue("identifier",out identifier))installedIdentifier=Convert.ToString(identifier);
        return Refresh(script);
    }
    public bool Refresh(string script) {
        if(IsReady()) {
            var canUpdate=Send("Runtime.evaluate",new {expression="typeof window.__codexBackgroundDispose === 'function'",returnByValue=true,silent=true});
            var value=canUpdate["result"] as Dictionary<string,object>;
            bool disposable=value!=null&&value.ContainsKey("value")&&Object.Equals(value["value"],true);
            if(!disposable)return true;
            var current=Send("Runtime.evaluate",new {expression="window.__codexBackgroundVersion",returnByValue=true,silent=true});
            var version=current["result"] as Dictionary<string,object>;
            string marker="const VERSION = \""+(version!=null&&version.ContainsKey("value")?Convert.ToString(version["value"]):"")+"\"";
            if(script.IndexOf(marker,StringComparison.Ordinal)>=0)return true;
        }
        var result=Send("Runtime.evaluate",new {expression=script,returnByValue=true,silent=true});
        if(result.ContainsKey("exceptionDetails")) throw new IOException("Appearance code failed; official application remains running.");
        return IsReady();
    }
    bool IsReady() {
        var result=Send("Runtime.evaluate",new {expression="Boolean(window.__codexBackgroundVersion && document.getElementById('codex-background-controls') && window.electronBridge)",returnByValue=true,silent=true});
        if(result.ContainsKey("exceptionDetails")) return false;
        var value=result["result"] as Dictionary<string,object>;
        return value!=null && value.ContainsKey("value") && Object.Equals(value["value"],true);
    }
    public void Dispose() { socket.Abort(); socket.Dispose(); }
    public void RemoveAddon(bool clearPreferences) {
        if(!String.IsNullOrEmpty(installedIdentifier))try{Send("Page.removeScriptToEvaluateOnNewDocument",new {identifier=installedIdentifier});}catch{}
        string remove=@"(async()=>{
            if(typeof window.__codexBackgroundDispose==='function')window.__codexBackgroundDispose();
            else{document.getElementById('codex-background-controls')?.remove();document.getElementById('codex-custom-background-styles')?.remove();document.querySelectorAll('[data-cbg-titlebar-anchor]').forEach(e=>e.removeAttribute('data-cbg-titlebar-anchor'));document.documentElement.removeAttribute('data-cbg-enabled');document.documentElement.removeAttribute('data-cbg-titlebar-ready');for(const p of Array.from(document.documentElement.style))if(p.startsWith('--cbg-'))document.documentElement.style.removeProperty(p);}
            if(CLEAR){for(const k of ['codex.custom-background.settings.v1','codex.custom-background.image-revision.v1','codex.custom-background.manager-revision.v1'])localStorage.removeItem(k);indexedDB.deleteDatabase('codex-custom-background-v1');}
            return true;
        })()";
        Send("Runtime.evaluate",new {expression=remove.Replace("CLEAR",clearPreferences?"true":"false"),awaitPromise=true,returnByValue=true,silent=true},10000);
    }
}

public static class CodexThemeMonitor {
    sealed class LocalClient : WebClient {
        public LocalClient() { Proxy=null; Encoding=new UTF8Encoding(false,true); }
        protected override WebRequest GetWebRequest(Uri address) { var request=base.GetWebRequest(address); request.Timeout=1500; return request; }
    }
    public static int ActivePort(string profile, string family) {
        try {
            string path=Path.Combine(profile,"DevToolsActivePort");
            if(!File.Exists(path) || new FileInfo(path).Length>2048) return 0;
            string[] lines=File.ReadAllLines(path); int port;
            if(lines.Length<2 || !Int32.TryParse(lines[0],out port) || port<1 || port>65535 || !lines[1].StartsWith("/devtools/browser/",StringComparison.Ordinal)) return 0;
            uint pid=CodexThemeNative.ListenerPid(port);
            if(pid==0 || !CodexThemeNative.IsOfficialProcess(pid,family)) return 0;
            using(var client=new LocalClient()) {
                var json=new JavaScriptSerializer {MaxJsonLength=65536};
                var version=json.DeserializeObject(client.DownloadString("http://127.0.0.1:"+port+"/json/version")) as Dictionary<string,object>;
                object address; Uri endpoint;
                if(version==null || !version.TryGetValue("webSocketDebuggerUrl",out address) || !Uri.TryCreate(Convert.ToString(address),UriKind.Absolute,out endpoint)) return 0;
                return endpoint.Scheme=="ws" && endpoint.Host=="127.0.0.1" && endpoint.Port==port && endpoint.AbsolutePath==lines[1] ? port : 0;
            }
        } catch { return 0; }
    }
    public static object[] Targets(int port) {
        using(var client=new LocalClient()) {
            var json=new JavaScriptSerializer {MaxJsonLength=1024*1024};
            return json.DeserializeObject(client.DownloadString("http://127.0.0.1:"+port+"/json/list")) as object[] ?? new object[0];
        }
    }
    public static bool IsMainTarget(Dictionary<string,object> target) {
        if(!target.ContainsKey("type") || !target.ContainsKey("url") || !target.ContainsKey("webSocketDebuggerUrl") || !Object.Equals(target["type"],"page")) return false;
        Uri url;
        if(!Uri.TryCreate(Convert.ToString(target["url"]),UriKind.Absolute,out url) || url.Scheme!="app" || url.Host!="-" || (url.AbsolutePath!="/index.html" && url.AbsolutePath!="/detached-window.html")) return false;
        foreach(string pair in url.Query.TrimStart('?').Split('&')) {
            int separator=pair.IndexOf('=');
            if(separator>0 && Uri.UnescapeDataString(pair.Substring(0,separator).Replace("+"," "))=="initialRoute" && Uri.UnescapeDataString(pair.Substring(separator+1).Replace("+"," ")).StartsWith("/avatar-overlay",StringComparison.Ordinal)) return false;
        }
        return true;
    }
    static readonly Dictionary<string,string> previousStatuses=new Dictionary<string,string>();
    static void Status(string file, string state, string package, string detail=null) {
        string signature=state+"|"+package+"|"+detail, previous;
        if(previousStatuses.TryGetValue(file,out previous) && previous==signature) return;
        var json=new JavaScriptSerializer();
        string temp=file+".tmp-"+Process.GetCurrentProcess().Id;
        try {
            File.WriteAllText(temp,json.Serialize(new {status=state,package=package,detail=detail,updatedAt=DateTime.UtcNow.ToString("o"),hostPid=Process.GetCurrentProcess().Id}),new UTF8Encoding(false));
            if(File.Exists(file)) File.Replace(temp,file,null);
            else File.Move(temp,file);
            previousStatuses[file]=signature;
        } catch(IOException) {} catch(UnauthorizedAccessException) {}
        finally { try { if(File.Exists(temp)) File.Delete(temp); } catch {} }
    }
    // Returns after the application exits. Never quits, restarts or installs the app.
    public static void Run(string profile, string family, string source, string statusFile) {
        string script=File.ReadAllText(source,Encoding.UTF8);
        var sessions=new Dictionary<string,CodexThemeSocket>();
        DateTime started=DateTime.UtcNow, lastProcess=started, lastEndpoint=started;
        int previousPort=0;
        bool installed=false;
        string lastConfigRevision=null;
        string failedConfigRevision=null;
        DateTime retryConfigAt=DateTime.MinValue;
        string root=Path.GetDirectoryName(source);
        Status(statusFile,"starting",null);
        try {
            while(true) {
                if(File.Exists(Path.Combine(root,"disabled.flag"))) {
                    foreach(var s in sessions.Values)try{s.RemoveAddon(false);}catch{}
                    Status(statusFile,"disabled",null);return;
                }
                if(CodexThemeNative.HasOfficialProcess(family)) lastProcess=DateTime.UtcNow;
                else if((DateTime.UtcNow-lastProcess).TotalSeconds>15) return;
                int port=ActivePort(profile,family);
                if(port!=previousPort) { foreach(var s in sessions.Values) s.Dispose(); sessions.Clear(); previousPort=port; }
                int readyWindows=0;
                string issue=null;
                if(port!=0) {
                    lastEndpoint=DateTime.UtcNow;
                    try {
                        var seen=new HashSet<string>();
                        foreach(object raw in Targets(port)) {
                            var target=raw as Dictionary<string,object>;
                            if(target==null || !IsMainTarget(target)) continue;
                            string key=Convert.ToString(target["id"]); seen.Add(key);
                            try {
                                CodexThemeSocket connection;
                                bool ready;
                                if(!sessions.TryGetValue(key,out connection)) {
                                    connection=new CodexThemeSocket(Convert.ToString(target["webSocketDebuggerUrl"]),port);
                                    sessions.Add(key,connection);
                                    ready=connection.Install(script);
                                } else ready=connection.Refresh(script);
                                if(ready) {
                                    installed=true;
                                    readyWindows++;
                                }
                            } catch(Exception error) {
                                CodexThemeSocket failed;
                                if(sessions.TryGetValue(key,out failed)) { failed.Dispose(); sessions.Remove(key); }
                                issue="window-attach-"+error.GetType().Name;
                            }
                        }
                        var removed=new List<string>();
                        foreach(string key in sessions.Keys) if(!seen.Contains(key)) removed.Add(key);
                        foreach(string key in removed) { sessions[key].Dispose(); sessions.Remove(key); }
                        if(readyWindows>0) {
                            try {
                                string config=Path.Combine(root,"preferences","theme.json");
                                if(File.Exists(config)) {
                                    var settings=new JavaScriptSerializer {MaxJsonLength=65536}.DeserializeObject(File.ReadAllText(config,Encoding.UTF8)) as Dictionary<string,object>;
                                    object revision;
                                    if(settings!=null&&settings.TryGetValue("revision",out revision)&&!String.IsNullOrEmpty(Convert.ToString(revision))&&Convert.ToString(revision)!=lastConfigRevision&&(Convert.ToString(revision)!=failedConfigRevision||DateTime.UtcNow>=retryConfigAt)) {
                                        failedConfigRevision=Convert.ToString(revision);retryConfigAt=DateTime.UtcNow.AddSeconds(60);
                                        CodexThemeSocket first=null;foreach(var s in sessions.Values){first=s;break;}
                                        if(first!=null) {
                                            var marker=first.Send("Runtime.evaluate",new {expression="localStorage.getItem('codex.custom-background.manager-revision.v1')",returnByValue=true,silent=true});
                                            var markerValue=marker["result"] as Dictionary<string,object>;
                                            if(markerValue==null||!markerValue.ContainsKey("value")||Convert.ToString(markerValue["value"])!=Convert.ToString(revision)) {
                                                ApplyPreferences(first,root,settings);
                                                foreach(var s in sessions.Values)s.Send("Runtime.evaluate",new {expression="window.__codexBackgroundReload?.()",awaitPromise=true,returnByValue=true,silent=true},20000);
                                            }
                                            lastConfigRevision=Convert.ToString(revision);
                                            failedConfigRevision=null;
                                            try{File.WriteAllText(Path.Combine(root,"preferences","applied-revision.txt"),lastConfigRevision,new UTF8Encoding(false));}catch(IOException){}catch(UnauthorizedAccessException){}
                                        }
                                    }
                                }
                            }catch(Exception error){issue="preferences-"+error.GetType().Name;}
                        }
                    } catch(Exception error) { issue="window-list-"+error.GetType().Name; }
                }
                if(readyWindows>0) Status(statusFile,"ready",CodexThemeNative.PackageName(CodexThemeNative.ListenerPid(port)),"ready-windows:"+readyWindows);
                else Status(statusFile,port==0 ? "waiting-for-endpoint" : "waiting-for-window",null,issue);
                if((DateTime.UtcNow-lastEndpoint).TotalSeconds>180) {
                    Status(statusFile,installed ? "official-restarted" : "theme-unavailable",null,"endpoint-timeout");
                    return;
                }
                if(!installed && (DateTime.UtcNow-started).TotalSeconds>180) { Status(statusFile,"theme-unavailable",null,issue??"window-readiness-timeout"); return; }
                Thread.Sleep(1500);
            }
        } finally {
            foreach(var session in sessions.Values) session.Dispose();
        }
    }
    public static void ApplyPreferences(CodexThemeSocket connection,string root,Dictionary<string,object> settings) {
        var json=new JavaScriptSerializer {MaxJsonLength=2*1024*1024};
        string action=settings.ContainsKey("imageAction")?Convert.ToString(settings["imageAction"]):"keep";
        if(action=="replace") {
            string relative=Convert.ToString(settings["imageFile"]);
            string prefs=Path.GetFullPath(Path.Combine(root,"preferences"))+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(Path.Combine(root,relative??""));
            if(!path.StartsWith(prefs,StringComparison.OrdinalIgnoreCase)||(File.GetAttributes(Path.GetDirectoryName(path))&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Invalid managed background path.");
            var file=new FileInfo(path);if(file.Length>25*1024*1024)throw new InvalidDataException("Background image too large.");
            string data=Convert.ToBase64String(File.ReadAllBytes(path));
            connection.Send("Runtime.evaluate",new {expression="window.__ctmImageParts=[];true",returnByValue=true,silent=true});
            for(int offset=0;offset<data.Length;offset+=262144)connection.Send("Runtime.evaluate",new {expression="window.__ctmImageParts.push("+json.Serialize(data.Substring(offset,Math.Min(262144,data.Length-offset)))+");true",returnByValue=true,silent=true});
        }
        string expression=@"(async()=>{
            const c=CONFIG;const allowed=new Set(['image/png','image/jpeg','image/webp','image/gif','image/avif']);
            try{
                if(c.imageAction==='replace'||c.imageAction==='remove'){
                    let photo=null;
                    if(c.imageAction==='replace'){
                        if(!allowed.has(c.imageMime))throw Error('Unsupported image type');
                        const data=atob((window.__ctmImageParts||[]).join(''));const bytes=Uint8Array.from(data,x=>x.charCodeAt(0));const blob=new Blob([bytes],{type:c.imageMime});const url=URL.createObjectURL(blob);
                        try{const img=new Image();img.src=url;await img.decode();if(!img.naturalWidth||img.naturalWidth*img.naturalHeight>60000000)throw Error('Image dimensions too large');}finally{URL.revokeObjectURL(url);}
                        photo={blob,name:c.imageName||'background'};
                    }
                    const db=await new Promise((resolve,reject)=>{const r=indexedDB.open('codex-custom-background-v1',1);r.onupgradeneeded=()=>r.result.createObjectStore('images');r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(Error('Theme image storage unavailable'));});
                    try{await new Promise((resolve,reject)=>{const tx=db.transaction('images','readwrite');const s=tx.objectStore('images');photo?s.put(photo,'background'):s.delete('background');tx.oncomplete=resolve;tx.onerror=()=>reject(Error('Theme image save failed'));});}finally{db.close();}
                    localStorage.setItem('codex.custom-background.image-revision.v1',String(c.revision));
                }
                localStorage.setItem('codex.custom-background.settings.v1',JSON.stringify({enabled:!!c.enabled,dim:c.dim,blur:c.blur,panel:c.panel,bubble:c.bubble,fit:c.fit,position:c.position}));
                if(typeof window.__codexBackgroundReload==='function')await window.__codexBackgroundReload();
                localStorage.setItem('codex.custom-background.manager-revision.v1',String(c.revision));return true;
            }finally{delete window.__ctmImageParts;}
        })()";
        var result=connection.Send("Runtime.evaluate",new {expression=expression.Replace("CONFIG",json.Serialize(settings)),awaitPromise=true,returnByValue=true,silent=true},20000);
        if(result.ContainsKey("exceptionDetails"))throw new IOException("The background settings could not be applied.");
    }
}
