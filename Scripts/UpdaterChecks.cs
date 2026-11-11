using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace NightLens {
internal static class UpdaterChecks {
    static int count;
    static void Check(bool value,string text){if(!value)throw new Exception(text);count++;Console.WriteLine("PASS: "+text);}
    static void Fails(Action operation,string text){bool failed=false;try{operation();}catch{failed=true;}Check(failed,text);}
    static void Copy(string from,string to){Directory.CreateDirectory(Path.GetDirectoryName(to));File.Copy(from,to,false);}
    static string Fixture(string root,string version){string stage=Path.Combine(root,"fixture");Directory.CreateDirectory(stage);var paths=new[]{"NightLens.exe","Assets/NightLens.ico","Catalogs/NGC.csv","Catalogs/IC.csv","Catalogs/Messier.csv","Catalogs/Supplement.csv","EquipmentLibrary/zwo-profiles.json","StarCatalog/stars.json","Licenses/ASTRONOMY-LICENSE.txt","Licenses/UNICODE-LICENSE.txt","Read me.txt"};var files=new List<UpdateFile>();foreach(string path in paths){string source=path.StartsWith("Licenses/")?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Source",Path.GetFileName(path)):Path.Combine(AppDomain.CurrentDomain.BaseDirectory,path.Replace('/',Path.DirectorySeparatorChar));string to=AppUpdates.Inside(stage,path);Copy(source,to);files.Add(new UpdateFile{path=path,sha256=AppUpdates.Hash(to)});}AppUpdates.WriteJson(Path.Combine(stage,"update-manifest.json"),new UpdateManifest{schema=1,version=version,files=files});return stage;}
    static string Archive(string fixture,string root,string extra=null,bool symlink=false){string file=Path.Combine(root,Guid.NewGuid().ToString("N")+".zip");using(var z=ZipFile.Open(file,ZipArchiveMode.Create)){foreach(string item in Directory.GetFiles(fixture,"*",SearchOption.AllDirectories))ZipFileExtensions.CreateEntryFromFile(z,item,"NightLens/"+item.Substring(fixture.Length+1).Replace('\\','/'),CompressionLevel.Optimal);if(extra!=null){var e=z.CreateEntry(extra);if(symlink)e.ExternalAttributes=(0xA1FF<<16);using(var w=new StreamWriter(e.Open()))w.Write("bad");}}return file;}
    static IEnumerable<Control> Walk(Control root){foreach(Control child in root.Controls){yield return child;foreach(var next in Walk(child))yield return next;}}
    public static int VerifyLive(){try{string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Source",".build","live-update-check",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var update=AppUpdates.FindUpdate(new Version(0,0,0,0),CancellationToken.None);if(update==null)throw new Exception("No stable release found.");string zip=Path.Combine(root,"download.zip");AppUpdates.DownloadArchive(update,zip,CancellationToken.None,null);string stage=Path.Combine(root,"stage");AppUpdates.StageArchive(zip,stage,update,CancellationToken.None);Console.WriteLine("Public update download and staged manifest verified: "+update.Version);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    public static int Run(){
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Source",".build","updater-checks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string version=AppUpdates.VersionText;var installed=AppUpdates.InstalledVersion;string next=(installed.Major+1)+".0.0";
        Check(AppUpdates.ParseVersion("v"+version)==installed,"Updater compares three-part versions consistently");
        Fails(()=>AppUpdates.ParseVersion("v8.1.0-preview"),"Prerelease version text is not accepted as a stable update");
        Fails(()=>AppUpdates.ParseVersion("v8.65535.0"),"Out-of-range release version is rejected");
        var release=new UpdateRelease{tag_name="v"+next,assets=new List<UpdateAsset>{new UpdateAsset{name="NightLens-Windows11-v"+next+".zip",browser_download_url="https://github.com/"+AppUpdates.Repository+"/releases/download/v"+next+"/NightLens-Windows11-v"+next+".zip",digest="sha256:"+new string('a',64),size=100}}};
        Check(AppUpdates.ReadRelease(Json.Write(release),installed).Version==next,"A newer stable portable release is selected");
        release.draft=true;Check(AppUpdates.ReadRelease(Json.Write(release),installed)==null,"Draft releases are ignored");release.draft=false;release.prerelease=true;Check(AppUpdates.ReadRelease(Json.Write(release),installed)==null,"Preview releases are ignored");release.prerelease=false;
        Check(AppUpdates.ReadRelease(Json.Write(release),AppUpdates.ParseVersion(next))==null,"The same version does not trigger an update");
        string saved=release.assets[0].browser_download_url;release.assets[0].browser_download_url="https://example.com/app.zip";Fails(()=>AppUpdates.ReadRelease(Json.Write(release),installed),"Foreign download host is rejected");release.assets[0].browser_download_url=saved;
        release.assets[0].digest=null;Fails(()=>AppUpdates.ReadRelease(Json.Write(release),installed),"Missing GitHub download digest is rejected");release.assets[0].digest="sha256:"+new string('a',64);
        release.assets.Add(release.assets[0]);Fails(()=>AppUpdates.ReadRelease(Json.Write(release),installed),"Ambiguous portable release assets are rejected");release.assets.RemoveAt(1);
        foreach(string path in new[]{"../x.txt","/x.txt","C:/x.txt","Assets/../../x.txt","Assets\\x.txt","Assets/a:stream","Catalogs/CON.csv","Assets/a. ","Assets//a.jpg"})Check(!AppUpdates.ManagedPath(path),"Unsafe update path rejected: "+path);
        Check(!AppUpdates.ManagedPath("settings.json")&&!AppUpdates.ManagedPath(".git/config")&&!AppUpdates.ManagedPath("Source/Build.ps1"),"Updater cannot manage settings, Git history or source scripts");
        Check(AppUpdates.AllowedHost(new Uri("https://release-assets.githubusercontent.com/file"))&&!AppUpdates.AllowedHost(new Uri("http://github.com/file"))&&!AppUpdates.AllowedHost(new Uri("https://github.com.evil.example/file")),"Redirect policy requires trusted HTTPS hosts");
        string fixture=Fixture(root,version),zip=Archive(fixture,root),stage=Path.Combine(root,"staged");var update=new UpdateInfo{Version=version,Sha256=AppUpdates.Hash(zip),Size=new FileInfo(zip).Length};AppUpdates.StageArchive(zip,stage,update,CancellationToken.None);Check(AppUpdates.VerifyStage(stage,version).files.Count==11,"Valid update archive is staged and fully verified");
        var wrong=new UpdateInfo{Version=version,Sha256=new string('0',64)};string wrongStage=Path.Combine(root,"wrong-hash");Fails(()=>AppUpdates.StageArchive(zip,wrongStage,wrong,CancellationToken.None),"Mismatched archive checksum is rejected");Check(!Directory.Exists(wrongStage),"Checksum failure creates no staged app files");
        foreach(string bad in new[]{"NightLens/../escape.txt","NightLens/NightLens.exe","NightLens/assets/NightLens.ico","NightLens/settings.json"}){string malicious=Archive(fixture,root,bad);var info=new UpdateInfo{Version=version,Sha256=AppUpdates.Hash(malicious)};Fails(()=>AppUpdates.StageArchive(malicious,Path.Combine(root,Guid.NewGuid().ToString("N")),info,CancellationToken.None),"Malformed archive rejected: "+bad);}
        string linked=Archive(fixture,root,"NightLens/Assets/link.jpg",true);Fails(()=>AppUpdates.StageArchive(linked,Path.Combine(root,"symlink"),new UpdateInfo{Version=version,Sha256=AppUpdates.Hash(linked)},CancellationToken.None),"Archive symlinks are rejected");
        Check(!File.Exists(Path.Combine(root,"escape.txt")),"Archive traversal did not escape staging");
        string textFile=Path.Combine(stage,"Read me.txt"),text=File.ReadAllText(textFile);File.WriteAllText(textFile,"tampered");Fails(()=>AppUpdates.VerifyStage(stage,version),"Staged file tampering is detected before installation");File.Copy(Path.Combine(fixture,"Read me.txt"),textFile,true);
        var manifest=Json.Read<UpdateManifest>(File.ReadAllText(Path.Combine(stage,"update-manifest.json")));manifest.files.Add(manifest.files[0]);AppUpdates.WriteJson(Path.Combine(stage,"update-manifest.json"),manifest);Fails(()=>AppUpdates.VerifyStage(stage,version),"Duplicate manifest paths are rejected");File.Copy(Path.Combine(fixture,"update-manifest.json"),Path.Combine(stage,"update-manifest.json"),true);
        using(var c=new CancellationTokenSource()){c.Cancel();Fails(()=>AppUpdates.StageArchive(zip,Path.Combine(root,"cancelled"),update,c.Token),"Cancelled download staging stops before installation");}
        string destination=Path.Combine(root,"destination");Directory.CreateDirectory(destination);File.WriteAllText(Path.Combine(destination,"NightLens.exe"),"original app");File.WriteAllText(Path.Combine(destination,"Read me.txt"),"original readme");File.WriteAllText(Path.Combine(destination,"settings.json"),"preserve settings");Directory.CreateDirectory(Path.Combine(destination,"Catalogs"));File.WriteAllText(Path.Combine(destination,"Catalogs","custom.csv"),"preserve custom catalog");
        Fails(()=>UpdateWorker.ApplyTransaction(stage,destination,Path.Combine(root,"rollback"),version,(a,b)=>{if(Path.GetFileName(b)=="Read me.txt")throw new IOException("Simulated locked file");Directory.CreateDirectory(Path.GetDirectoryName(b));File.Copy(a,b,true);}),"Simulated interrupted update rolls back");
        Check(File.ReadAllText(Path.Combine(destination,"NightLens.exe"))=="original app"&&File.ReadAllText(Path.Combine(destination,"Read me.txt"))=="original readme","Rollback restores the original app and managed documents");
        Check(!File.Exists(Path.Combine(destination,"Assets","NightLens.ico")),"Rollback removes files introduced by the failed update");
        UpdateWorker.ApplyTransaction(stage,destination,Path.Combine(root,"backup"),version);Check(AppUpdates.Hash(Path.Combine(destination,"NightLens.exe"))==AppUpdates.Hash(Path.Combine(stage,"NightLens.exe")),"Successful transaction installs the verified executable");
        Check(File.ReadAllText(Path.Combine(destination,"settings.json"))=="preserve settings"&&File.ReadAllText(Path.Combine(destination,"Catalogs","custom.csv"))=="preserve custom catalog","Installation preserves local settings and additional catalogs");
        Check(Json.Read<UpdateManifest>(File.ReadAllText(Path.Combine(destination,"update-manifest.json"))).version==version,"Installed package manifest is updated with the app");Check(File.ReadAllText(Path.Combine(root,"backup","NightLens.exe"))=="original app","Recovery folder retains the previous executable");
        Check(AppUpdates.InstallBlockReason(AppDomain.CurrentDomain.BaseDirectory).Contains("source checkout"),"Source checkouts cannot be overwritten by the updater");
        string outside=Path.Combine(root,"untrusted");Directory.CreateDirectory(outside);Check(UpdateWorker.Run(Path.Combine(outside,"plan.json"),"bad")==1&&!File.Exists(Path.Combine(outside,"worker-error.txt")),"Untrusted helper plans cannot write outside update storage");
        foreach(string theme in new[]{"Light","Dark","Night"}){
            Style.SetTheme(theme);using(var owner=new Form())using(var controller=new AppUpdateController(owner,null,true)){
                controller.Checker=token=>new UpdateInfo{Version=next};using(var form=new UpdateForm(owner,controller)){form.Show();for(int i=0;i<30;i++){Application.DoEvents();Thread.Sleep(10);}using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"Updates-"+theme+".png"));}
                    Check(Walk(form).OfType<Button>().Any(b=>b.Text=="Download and restart"&&b.Enabled),theme+" update action is available for a newer version");
                    foreach(var control in Walk(form).Where(c=>c.Visible&&(c is Button||c is CheckBox))){var rect=form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));Check(form.ClientRectangle.Contains(rect),theme+" update control fits inside the window");}form.Close();}
            }
        }
        Style.SetTheme("Dark");Console.WriteLine(count+" updater checks passed.");return count;
    }
}
}
