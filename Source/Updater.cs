using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace NightLens {
public class UpdateAsset { public string name {get;set;} public string browser_download_url {get;set;} public string digest {get;set;} public long size {get;set;} }
public class UpdateRelease { public string tag_name {get;set;} public string html_url {get;set;} public bool draft {get;set;} public bool prerelease {get;set;} public List<UpdateAsset> assets {get;set;} }
public class UpdateInfo { public string Version {get;set;} public string DownloadUrl {get;set;} public string Sha256 {get;set;} public long Size {get;set;} }
public class UpdateFile { public string path {get;set;} public string sha256 {get;set;} }
public class UpdateManifest { public int schema {get;set;} public string version {get;set;} public List<UpdateFile> files {get;set;} }
public class UpdatePreferences { public bool CheckAutomatically {get;set;}=true; public DateTime LastAttemptUtc {get;set;} public DateTime LastCheckedUtc {get;set;} }
public class UpdatePlan { public int Schema {get;set;}=1; public string Nonce {get;set;} public string Destination {get;set;} public string OldExeHash {get;set;} public string Version {get;set;} public int ParentId {get;set;} public long ParentStartedUtcTicks {get;set;} }
public class UpdateResult { public bool Success {get;set;} public string Message {get;set;} public string RecoveryFolder {get;set;} }
public static class AppUpdates {
    public const string Repository="lawrencegreiner/NightLens";
    public const string ReleasesUrl="https://github.com/"+Repository+"/releases";
    public const long MaxDownload=64L*1024*1024,MaxExpanded=128L*1024*1024;
    public static string DataRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NightLens");}}
    public static string UpdatesRoot {get{return Path.Combine(DataRoot,"Updates");}}
    public static Version InstalledVersion {get{return Assembly.GetExecutingAssembly().GetName().Version;}}
    public static string VersionText {get{return InstalledVersion.ToString(3);}}
    public static Version ParseVersion(string text) {
        if(text==null||!Regex.IsMatch(text,@"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$"))throw new InvalidDataException("The release version is not supported.");
        var p=text.TrimStart('v').Split('.').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();
        if(p.Any(x=>x>65534))throw new InvalidDataException("The release version is not supported.");return new Version(p[0],p[1],p[2],0);
    }
    public static bool IsHash(string text){return text!=null&&Regex.IsMatch(text,@"^[a-fA-F0-9]{64}$");}
    public static string Hash(Stream stream){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    public static string Hash(string file){using(var s=File.OpenRead(file))return Hash(s);}
    public static void WriteJson(string path,object data){Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temporary,Json.Write(data),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
    public static UpdatePreferences LoadPreferences(){try{return Json.Read<UpdatePreferences>(File.ReadAllText(Path.Combine(DataRoot,"update-settings.json")))??new UpdatePreferences();}catch{return new UpdatePreferences();}}
    public static void SavePreferences(UpdatePreferences p){WriteJson(Path.Combine(DataRoot,"update-settings.json"),p);}
    internal static bool AllowedHost(Uri uri){return uri.Scheme=="https"&&uri.Port==443&&uri.UserInfo==""&&new[]{"api.github.com","github.com","release-assets.githubusercontent.com","objects.githubusercontent.com","github-releases.githubusercontent.com"}.Contains(uri.Host,StringComparer.OrdinalIgnoreCase);}
    static void ReadUrl(string address,Stream output,long limit,long? expected,CancellationToken cancel,Action<int> progress){
        Uri uri=new Uri(address);for(int redirect=0;redirect<=5;redirect++){
            cancel.ThrowIfCancellationRequested();if(!AllowedHost(uri))throw new InvalidDataException("Unexpected update download address.");
            var request=(HttpWebRequest)WebRequest.Create(uri);request.UserAgent="NightLens/"+VersionText;request.Accept=uri.Host=="api.github.com"?"application/vnd.github+json":"application/octet-stream";request.Headers["X-GitHub-Api-Version"]="2022-11-28";request.AllowAutoRedirect=false;request.Timeout=20000;request.ReadWriteTimeout=20000;
            try{using(cancel.Register(()=>request.Abort()))using(var response=(HttpWebResponse)request.GetResponse()){
                int status=(int)response.StatusCode;if(status==301||status==302||status==303||status==307||status==308){uri=new Uri(uri,response.Headers["Location"]);continue;}
                if(status!=200)throw new IOException("GitHub could not provide the update.");if(response.ContentLength>limit)throw new InvalidDataException("The update download is too large.");
                using(var source=response.GetResponseStream()){var bytes=new byte[65536];long total=0;int read;int last=-1;while((read=source.Read(bytes,0,bytes.Length))>0){cancel.ThrowIfCancellationRequested();total+=read;if(total>limit||expected.HasValue&&total>expected.Value)throw new InvalidDataException("Unexpected update download size.");output.Write(bytes,0,read);int percent=expected.HasValue?(int)(total*100/expected.Value):0;if(percent!=last){progress?.Invoke(percent);last=percent;}}if(expected.HasValue&&total!=expected.Value)throw new InvalidDataException("The update download is incomplete.");return;}
            }}catch(WebException ex){cancel.ThrowIfCancellationRequested();var response=ex.Response as HttpWebResponse;int code=response==null?0:(int)response.StatusCode;response?.Dispose();if(code==403||code==429)throw new IOException("GitHub's update limit was reached. Try again later.");if(code==404)throw new IOException("A downloadable public release is not available.");throw new IOException("Could not reach GitHub. Check your connection and try again.");}
        }throw new IOException("Too many update download redirects.");
    }
    public static UpdateInfo ReadRelease(string json,Version installed){
        var release=Json.Read<UpdateRelease>(json);if(release==null||release.draft||release.prerelease) return null;
        Version version=ParseVersion(release.tag_name);if(version<=installed)return null;
        if(release.tag_name!="v"+version.ToString(3))throw new InvalidDataException("The release tag is not supported.");
        string name="NightLens-Windows11-"+release.tag_name+".zip";var assets=(release.assets??new List<UpdateAsset>()).Where(x=>x.name==name).ToList();
        if(assets.Count!=1)throw new InvalidDataException("This release has no unique portable Windows download.");var asset=assets[0];
        if(asset.size<=0||asset.size>MaxDownload||asset.digest==null||!asset.digest.StartsWith("sha256:",StringComparison.Ordinal)||!IsHash(asset.digest.Substring(7)))throw new InvalidDataException("This release has no supported download checksum.");
        string expected="https://github.com/"+Repository+"/releases/download/"+release.tag_name+"/"+name;
        if(asset.browser_download_url!=expected)throw new InvalidDataException("Unexpected update download address.");
        return new UpdateInfo{Version=version.ToString(3),DownloadUrl=expected,Sha256=asset.digest.Substring(7).ToLowerInvariant(),Size=asset.size};
    }
    public static UpdateInfo Check(CancellationToken cancel){return FindUpdate(InstalledVersion,cancel);} public static UpdateInfo FindUpdate(Version installed,CancellationToken cancel){using(var stream=new MemoryStream()){ReadUrl("https://api.github.com/repos/"+Repository+"/releases/latest",stream,2*1024*1024,null,cancel,null);return ReadRelease(Encoding.UTF8.GetString(stream.ToArray()),installed);}}
    public static bool SafeRelative(string path){
        if(string.IsNullOrEmpty(path)||path.Length>200||path.Contains('\\')||path.Contains(':')||path.StartsWith("/")||Path.IsPathRooted(path))return false;
        foreach(string part in path.Split('/')){if(part==""||part=="."||part==".."||part.EndsWith(".")||part.EndsWith(" ")||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(Path.GetFileNameWithoutExtension(part),@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",RegexOptions.IgnoreCase))return false;}return true;
    }
    internal static bool ManagedPath(string path){
        if(!SafeRelative(path))return false;var p=path.Split('/');string ext=Path.GetExtension(path).ToLowerInvariant();
        if(p.Length==1)return path=="NightLens.exe"||ext==".txt";
        if(p[0]=="Assets")return new[]{".jpg",".png",".ico"}.Contains(ext);
        if(p[0]=="Catalogs")return ext==".csv"||ext==".txt";
        if(p[0]=="EquipmentLibrary")return ext==".json"||ext==".txt";
        if(p[0]=="StarCatalog")return ext==".json"||ext==".txt"||ext==".md";
        return p[0]=="Licenses"&&ext==".txt";
    }
    internal static string Inside(string root,string relative){if(!SafeRelative(relative))throw new InvalidDataException("Unsafe path in update package.");string target=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar))),prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||target.Length>=250)throw new InvalidDataException("The update path is unsafe or too long.");return target;}
    internal static void NoLinks(string path){for(string at=Path.GetFullPath(path);at!=null;at=Path.GetDirectoryName(at)){if((Directory.Exists(at)||File.Exists(at))&&(File.GetAttributes(at)&FileAttributes.ReparsePoint)!=0)throw new IOException("Updates cannot replace files through a linked folder. Use a regular local app folder.");}}
    public static UpdateManifest ReadManifest(string file,string version){
        var info=new FileInfo(file);if(!info.Exists||info.Length>256*1024)throw new InvalidDataException("Update manifest is missing or too large.");var m=Json.Read<UpdateManifest>(File.ReadAllText(file));
        if(m==null||m.schema!=1||m.version!=version||m.files==null||m.files.Count<10||m.files.Count>255)throw new InvalidDataException("Unsupported update manifest.");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var f in m.files)if(f==null||!ManagedPath(f.path)||!IsHash(f.sha256)||!seen.Add(f.path))throw new InvalidDataException("Unsafe or duplicate update file.");
        foreach(string required in new[]{"NightLens.exe","Assets/NightLens.ico","Catalogs/NGC.csv","Catalogs/IC.csv","Catalogs/Messier.csv","Catalogs/Supplement.csv","EquipmentLibrary/zwo-profiles.json","StarCatalog/stars.json","Licenses/ASTRONOMY-LICENSE.txt","Licenses/UNICODE-LICENSE.txt"})if(!seen.Contains(required))throw new InvalidDataException("The update is missing a required app file.");return m;
    }
    static List<string> SafeStageFiles(string stage){var files=new List<string>();var pending=new Stack<string>();pending.Push(stage);while(pending.Count>0){string folder=pending.Pop();NoLinks(folder);foreach(string file in Directory.GetFiles(folder)){NoLinks(file);files.Add(file);if(files.Count>256)throw new InvalidDataException("Too many staged files.");}foreach(string child in Directory.GetDirectories(folder))pending.Push(child);}return files;}
    public static UpdateManifest VerifyStage(string stage,string version){
        NoLinks(stage);var m=ReadManifest(Path.Combine(stage,"update-manifest.json"),version);
        var files=SafeStageFiles(stage);if(files.Count!=m.files.Count+1)throw new InvalidDataException("Unexpected files in the staged update.");
        foreach(var f in m.files){string file=Inside(stage,f.path);NoLinks(file);if(!File.Exists(file)||!Hash(file).Equals(f.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A staged update file failed its checksum.");}
        var exe=Inside(stage,"NightLens.exe");if(FileVersionInfo.GetVersionInfo(exe).FileVersion!=version+".0"||AssemblyName.GetAssemblyName(exe).Name!="NightLens")throw new InvalidDataException("The update executable has the wrong identity or version.");return m;
    }
    public static void StageArchive(string zipPath,string stage,UpdateInfo update,CancellationToken cancel){
        if(!Hash(zipPath).Equals(update.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The download checksum did not match. No app files were changed.");
        if(Directory.Exists(stage))throw new IOException("Update staging folder already exists.");Directory.CreateDirectory(stage);NoLinks(stage);
        using(var archive=ZipFile.OpenRead(zipPath)){
            if(archive.Entries.Count<11||archive.Entries.Count>256)throw new InvalidDataException("Unexpected update file count.");long total=0;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in archive.Entries){cancel.ThrowIfCancellationRequested();string name=entry.FullName;
                if(!name.StartsWith("NightLens/",StringComparison.Ordinal)||name.EndsWith("/"))throw new InvalidDataException("Unexpected update archive layout.");string relative=name.Substring(10);
                if(relative!="update-manifest.json"&&!ManagedPath(relative)||!SafeRelative(relative)||!seen.Add(relative)||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Unsafe entry in the update archive.");
                if(entry.Length<0||entry.Length>32L*1024*1024||(total+=entry.Length)>MaxExpanded)throw new InvalidDataException("The expanded update is too large.");
                string file=Inside(stage,relative);Directory.CreateDirectory(Path.GetDirectoryName(file));using(var source=entry.Open())using(var target=new FileStream(file,FileMode.CreateNew,FileAccess.Write)){var bytes=new byte[65536];long copied=0;int n;while((n=source.Read(bytes,0,bytes.Length))>0){cancel.ThrowIfCancellationRequested();copied+=n;if(copied>entry.Length)throw new InvalidDataException("Expanded update entry is too large.");target.Write(bytes,0,n);}if(copied!=entry.Length)throw new InvalidDataException("An update file is incomplete.");}
            }
        }VerifyStage(stage,update.Version);
    }
    public static string InstallBlockReason(string folder){
        try{folder=Path.GetFullPath(folder);if(Directory.Exists(Path.Combine(folder,".git"))||File.Exists(Path.Combine(folder,".git")))return "This copy is running from a source checkout. Use the portable app in a separate folder to install updates.";NoLinks(folder);if(!File.Exists(Path.Combine(folder,"NightLens.exe")))return "Run the portable app as NightLens.exe to install an update.";string probe=Path.Combine(folder,".nightlens-write-"+Guid.NewGuid().ToString("N"));using(File.Create(probe)){}File.Delete(probe);return null;}catch{return "This app folder cannot be updated. Move the portable NightLens folder to a writable local folder and try again.";}
    }
    public static void DownloadArchive(UpdateInfo update,string zip,CancellationToken cancel,Action<string> progress){
        string version=ParseVersion(update.Version).ToString(3),expected="https://github.com/"+Repository+"/releases/download/v"+version+"/NightLens-Windows11-v"+version+".zip";
        if(update.DownloadUrl!=expected||!IsHash(update.Sha256)||update.Size<=0||update.Size>MaxDownload)throw new InvalidDataException("Invalid update download metadata.");
        progress?.Invoke("Downloading update…");using(var output=new FileStream(zip,FileMode.CreateNew,FileAccess.Write)){ReadUrl(update.DownloadUrl,output,MaxDownload,update.Size,cancel,p=>progress?.Invoke("Downloading update… "+p+"%"));}
        if(!Hash(zip).Equals(update.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The download checksum did not match. No app files were changed.");
    }
    public static string DownloadAndStage(UpdateInfo update,CancellationToken cancel,Action<string> progress){
        string blocked=InstallBlockReason(AppDomain.CurrentDomain.BaseDirectory);if(blocked!=null)throw new IOException(blocked);string run=Path.Combine(UpdatesRoot,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(run);NoLinks(run);string zip=Path.Combine(run,"download.zip");
        DownloadArchive(update,zip,cancel,progress);
        progress?.Invoke("Checking the update package…");StageArchive(zip,Path.Combine(run,"stage"),update,cancel);return run;
    }
    public static void StartWorker(string run,string version){
        string executable=Assembly.GetExecutingAssembly().Location;var process=Process.GetCurrentProcess();string nonce=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");
        var plan=new UpdatePlan{Nonce=nonce,Destination=Path.GetDirectoryName(executable),OldExeHash=Hash(executable),Version=version,ParentId=process.Id,ParentStartedUtcTicks=process.StartTime.ToUniversalTime().Ticks};
        string file=Path.Combine(run,"plan.json"),helper=Path.Combine(run,"NightLens.Update.exe");WriteJson(file,plan);File.Copy(executable,helper,false);
        Process.Start(new ProcessStartInfo(helper,"--apply-update \""+file+"\" "+nonce){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=run});
        string ready=Path.Combine(run,"ready");for(int i=0;i<100;i++){if(File.Exists(ready))return;if(File.Exists(Path.Combine(run,"worker-error.txt")))throw new IOException(File.ReadAllText(Path.Combine(run,"worker-error.txt")));Thread.Sleep(100);}throw new IOException("The updater did not start. NightLens is still running; try again.");
    }
}
internal static class UpdateWorker {
    static void AtomicCopy(string source,string destination){Directory.CreateDirectory(Path.GetDirectoryName(destination));string temporary=destination+".nightlens-"+Guid.NewGuid().ToString("N")+".tmp";try{File.Copy(source,temporary,false);if(File.Exists(destination)){try{File.Replace(temporary,destination,null);}catch(Exception error){if(!(error is PlatformNotSupportedException)&&(!(error is IOException)||(error.HResult&0xFFFF)!=50))throw;string previous=destination+".nightlens-old-"+Guid.NewGuid().ToString("N")+".tmp";File.Move(destination,previous);try{File.Move(temporary,destination);}catch{if(!File.Exists(destination))File.Move(previous,destination);throw;}try{File.Delete(previous);}catch{}}}else File.Move(temporary,destination);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
    internal static void ApplyTransaction(string stage,string destination,string backup,string version,Action<string,string> copy=null){
        var m=AppUpdates.VerifyStage(stage,version);AppUpdates.NoLinks(destination);if(Directory.Exists(Path.Combine(destination,".git"))||File.Exists(Path.Combine(destination,".git")))throw new IOException("A source checkout cannot be updated in place.");
        if(Directory.Exists(backup))throw new IOException("Recovery folder already exists.");Directory.CreateDirectory(backup);var existed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var touched=new List<string>();var operations=m.files.Concat(new[]{new UpdateFile{path="update-manifest.json",sha256=AppUpdates.Hash(Path.Combine(stage,"update-manifest.json"))}}).ToList();
        foreach(var f in operations){string to=AppUpdates.Inside(destination,f.path);AppUpdates.NoLinks(to);if(Directory.Exists(to))throw new IOException("An update file conflicts with a folder.");if(File.Exists(to)){string save=AppUpdates.Inside(backup,f.path);Directory.CreateDirectory(Path.GetDirectoryName(save));File.Copy(to,save,false);existed.Add(f.path);}}
        try{foreach(var f in operations.OrderBy(f=>f.path=="NightLens.exe"?1:0)){string to=AppUpdates.Inside(destination,f.path);touched.Add(f.path);(copy??AtomicCopy)(AppUpdates.Inside(stage,f.path),to);if(!AppUpdates.Hash(to).Equals(f.sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("An installed file failed verification.");}}
        catch(Exception original){var failures=new List<string>();foreach(string relative in touched.AsEnumerable().Reverse()){try{string to=AppUpdates.Inside(destination,relative);if(existed.Contains(relative)){string saved=AppUpdates.Inside(backup,relative);if(!File.Exists(to)||AppUpdates.Hash(to)!=AppUpdates.Hash(saved))AtomicCopy(saved,to);}else if(File.Exists(to))File.Delete(to);}catch{failures.Add(relative);}}if(failures.Count>0)throw new IOException("The update stopped, but some files could not be restored. Recover them from: "+backup,original);throw new IOException("The update could not finish. The previous app files were restored.",original);}
    }
    public static int Run(string planFile,string nonce){
        string run=null,destination=null,oldHash=null;bool ready=false,trusted=false,parentExited=false;
        try{
            string full=Path.GetFullPath(planFile);run=Path.GetDirectoryName(full);string parent=Path.GetDirectoryName(run);if(parent==null||!parent.Equals(Path.GetFullPath(AppUpdates.UpdatesRoot),StringComparison.OrdinalIgnoreCase)||!Regex.IsMatch(Path.GetFileName(run),@"^[a-f0-9]{32}$")||Path.GetFileName(full)!="plan.json")throw new InvalidDataException("Unexpected update plan location.");
            AppUpdates.NoLinks(run);trusted=true;if(new FileInfo(full).Length>16384)throw new InvalidDataException("Update plan is too large.");var p=Json.Read<UpdatePlan>(File.ReadAllText(full));if(p==null||p.Schema!=1||p.Nonce!=nonce||!Regex.IsMatch(nonce??"",@"^[a-f0-9]{64}$")||!AppUpdates.IsHash(p.OldExeHash))throw new InvalidDataException("Invalid update plan.");
            AppUpdates.ParseVersion(p.Version);destination=Path.GetFullPath(p.Destination);oldHash=p.OldExeHash;string blocked=AppUpdates.InstallBlockReason(destination);if(blocked!=null)throw new IOException(blocked);
            if(AppUpdates.ParseVersion(p.Version)<=new Version(FileVersionInfo.GetVersionInfo(Path.Combine(destination,"NightLens.exe")).FileVersion))throw new IOException("This update is not newer than the installed app.");if(!AppUpdates.Hash(Path.Combine(destination,"NightLens.exe")).Equals(p.OldExeHash,StringComparison.OrdinalIgnoreCase)||!AppUpdates.Hash(Assembly.GetExecutingAssembly().Location).Equals(p.OldExeHash,StringComparison.OrdinalIgnoreCase))throw new IOException("The original app changed before the update started.");
            string stage=Path.Combine(run,"stage");AppUpdates.VerifyStage(stage,p.Version);
            File.WriteAllText(Path.Combine(run,"ready"),"ready");ready=true;
            try{using(var app=Process.GetProcessById(p.ParentId)){if(app.StartTime.ToUniversalTime().Ticks!=p.ParentStartedUtcTicks)throw new IOException("The original app process changed.");if(!app.WaitForExit(60000))throw new IOException("NightLens did not close. No app files were changed.");}}catch(ArgumentException){}parentExited=true;
            if(!AppUpdates.Hash(Path.Combine(destination,"NightLens.exe")).Equals(p.OldExeHash,StringComparison.OrdinalIgnoreCase))throw new IOException("The app changed while waiting to update.");
            string backup=Path.Combine(run,"backup");ApplyTransaction(stage,destination,backup,p.Version);
            AppUpdates.WriteJson(Path.Combine(AppUpdates.DataRoot,"update-result.json"),new UpdateResult{Success=true,Message="NightLens was updated to "+p.Version+".",RecoveryFolder=backup});
            try{Process.Start(new ProcessStartInfo(Path.Combine(destination,"NightLens.exe")){UseShellExecute=true,WorkingDirectory=destination});}catch{System.Windows.Forms.MessageBox.Show("NightLens was updated. Open NightLens.exe to start it.","NightLens update");}return 0;
        }catch(Exception error){try{if(trusted){File.WriteAllText(Path.Combine(run,"worker-error.txt"),error.Message);if(ready)AppUpdates.WriteJson(Path.Combine(AppUpdates.DataRoot,"update-result.json"),new UpdateResult{Success=false,Message=error.Message,RecoveryFolder=Path.Combine(run,"backup")});}}catch{}if(ready){bool restarted=false;try{string exe=Path.Combine(destination,"NightLens.exe");if(parentExited&&File.Exists(exe)&&AppUpdates.Hash(exe).Equals(oldHash,StringComparison.OrdinalIgnoreCase)){Process.Start(new ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=destination});restarted=true;}}catch{}if(!restarted)System.Windows.Forms.MessageBox.Show(error.Message,"NightLens update");}return 1;}
    }
}
}
