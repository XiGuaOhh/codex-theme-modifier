using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
namespace CodexThemeModifier {
    internal static class UpdateService {
        public static Version ParseVersion(string text){
            if(String.IsNullOrWhiteSpace(text))throw new InvalidDataException("Missing version.");text=text.Trim().TrimStart('v','V');if(text.IndexOf('-')>=0)throw new InvalidDataException("Only stable releases are accepted.");Version version;if(!Version.TryParse(text,out version)||version.Major<0)throw new InvalidDataException("Invalid release version.");return new Version(version.Major,version.Minor,Math.Max(0,version.Build),Math.Max(0,version.Revision));
        }
        public static UpdateInfo ParseRelease(string data){
            var d=JsonFiles.Serializer().DeserializeObject(data) as Dictionary<string,object>;if(d==null||Convert.ToBoolean(d["draft"])||Convert.ToBoolean(d["prerelease"]))throw new InvalidDataException("No stable release.");
            string version=Convert.ToString(d["tag_name"]);var parsed=ParseVersion(version);object raw;if(!d.TryGetValue("assets",out raw))throw new InvalidDataException("The release has no installer.");
            foreach(object item in (object[])raw){var a=item as Dictionary<string,object>;if(a==null||Convert.ToString(a["name"])!=BuildInfo.AssetName)continue;
                string url=Convert.ToString(a["browser_download_url"]);ValidateAssetUrl(url);object digest;if(!a.TryGetValue("digest",out digest)||!Convert.ToString(digest).StartsWith("sha256:",StringComparison.Ordinal))throw new InvalidDataException("GitHub 未提供文件校验值，请稍后重试。");
                string hash=Convert.ToString(digest).Substring(7);if(hash.Length!=64||!System.Text.RegularExpressions.Regex.IsMatch(hash,"\\A[0-9a-fA-F]{64}\\z"))throw new InvalidDataException("Invalid update checksum.");
                long size=Convert.ToInt64(a["size"]);if(size<1024||size>64L*1024*1024)throw new InvalidDataException("Invalid update size.");
                object notes;return new UpdateInfo {Version=parsed.ToString(3),Url=url,Digest=hash.ToLowerInvariant(),Size=size,Page=BuildInfo.Website+"/releases",Notes=d.TryGetValue("body",out notes)?Convert.ToString(notes):"",Newer=parsed>ParseVersion(BuildInfo.Version)};
            }throw new InvalidDataException("此发行版本还没有 Windows 安装程序。");
        }
        public static void ValidateAssetUrl(string text){Uri uri;if(!Uri.TryCreate(text,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/"+BuildInfo.Owner+"/"+BuildInfo.Repository+"/releases/download/",StringComparison.Ordinal)||!String.IsNullOrEmpty(uri.UserInfo))throw new InvalidDataException("更新来源不是本项目的 GitHub Release。");}
        static HttpWebRequest Request(Uri uri){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var r=(HttpWebRequest)WebRequest.Create(uri);r.UserAgent="CodexThemeModifier/"+BuildInfo.Version;r.Accept="application/vnd.github+json";r.Headers["X-GitHub-Api-Version"]="2022-11-28";r.Timeout=15000;r.ReadWriteTimeout=30000;r.AllowAutoRedirect=false;return r;}
        public static UpdateInfo Check(){
            try{using(var response=(HttpWebResponse)Request(new Uri(BuildInfo.LatestApi)).GetResponse())using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){
                var buffer=new byte[8192];int read;while((read=stream.Read(buffer,0,buffer.Length))>0){if(memory.Length+read>2*1024*1024)throw new InvalidDataException("Release metadata too large.");memory.Write(buffer,0,read);}return ParseRelease(new UTF8Encoding(false,true).GetString(memory.ToArray()));
            }}catch(WebException e){var response=e.Response as HttpWebResponse;if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)return null;throw new IOException("无法连接 GitHub，稍后重试即可；已安装的功能仍可使用。");}
        }
        public static string Download(UpdateInfo update,Action<int> progress){
            ValidateAssetUrl(update.Url);Uri uri=new Uri(update.Url);HttpWebResponse response=null;
            for(int i=0;i<6;i++){
                response=(HttpWebResponse)Request(uri).GetResponse();int code=(int)response.StatusCode;if(code<300||code>=400)break;
                string location=response.Headers["Location"];response.Dispose();response=null;Uri next;
                if(String.IsNullOrEmpty(location)||!Uri.TryCreate(uri,location,out next)||next.Scheme!="https"||Array.IndexOf(new[]{"github.com","release-assets.githubusercontent.com","objects.githubusercontent.com","github-releases.githubusercontent.com"},next.Host)<0||!String.IsNullOrEmpty(next.UserInfo))throw new InvalidDataException("GitHub 下载重定向无效。");uri=next;
            }
            if(response==null||(int)response.StatusCode!=200){if(response!=null)response.Dispose();throw new IOException("更新下载失败。");}
            string directory=Path.Combine(Path.GetTempPath(),"CodexThemeModifier",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string file=Path.Combine(directory,BuildInfo.AssetName);
            try{
                using(response)using(var source=response.GetResponseStream())using(var target=File.Create(file)){
                    var buffer=new byte[32768];long total=0;int read;var watch=Stopwatch.StartNew();while((read=source.Read(buffer,0,buffer.Length))>0){total+=read;if(total>update.Size||total>64L*1024*1024||watch.Elapsed.TotalMinutes>5)throw new InvalidDataException("更新大小或下载时间异常。");target.Write(buffer,0,read);if(progress!=null)progress((int)Math.Min(100,total*100/update.Size));}if(total!=update.Size)throw new InvalidDataException("更新文件没有下载完整。");
                }
                ValidateDownloaded(file,update.Digest);return file;
            }catch{try{if(File.Exists(file))File.Delete(file);}catch{}throw;}
        }
        public static void ValidateDownloaded(string file,string expected){
            if(!String.Equals(JsonFiles.Hash(file),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新校验失败，文件未被执行。");
            if(AssemblyName.GetAssemblyName(file).Name!="CodexThemeModifier")throw new InvalidDataException("更新程序的产品标识不匹配。");
        }
    }
}
