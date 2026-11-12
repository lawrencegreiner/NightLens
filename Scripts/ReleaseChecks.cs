using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using CosineKitty;
namespace NightLens {
internal static class ReleaseChecks {
    static int count;
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); count++; Console.WriteLine("PASS: "+name); }
    [STAThread] public static int Main(string[] args) {
        System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
        if(args.Length==1&&args[0]=="--verify-live-images")return SiteImageChecks.LiveImages();
        if(args.Length==1&&args[0]=="--verify-live-update")return UpdaterChecks.VerifyLive();
        string original=Settings.Folder;
        try {
            Settings.Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Source",".build","release-check-settings");
            var s=new Settings {WindowsLocation=false};
            Check(s.SkySites[0].Source.Contains("\u00b7"),"UTF-8 source text survives release version stamping");
            string notice;var targets=CatalogLibrary.Load(s,out notice);
            Check(targets.Count==13365,"Bundled catalogs load and deduplicate to 13,365 targets");
            Check(targets.All(t=>t.Ra>=0&&t.Ra<24&&Math.Abs(t.Dec)<=90),"Catalog coordinates are valid");
            Check(EquipmentLibrary.Error==""&&EquipmentLibrary.All.Count(p=>!p.Smart)==91,"91 camera profiles load");
            Check(EquipmentLibrary.Subs(s.Rigs[0]).SequenceEqual(new double[]{10,20,30}),"S50 uses supported app exposures");
            Check(EquipmentLibrary.Find(s.Rigs[1]).GainStart==200,"585 camera has model-specific gain");
            Check(EquipmentLibrary.Gain(s.Rigs[0]).Contains("App-controlled"),"Smart telescope gain is app controlled");
            Check(Math.Abs(s.Rigs[0].Width-1.276)<.01&&Math.Abs(s.Rigs[1].Width-1.564)<.01,"Default equipment framing");
            DateTime at=new DateTime(2026,10,24,18,0,0,DateTimeKind.Utc);
            var n=new Night(s,new Forecast(),at);Check(n.Picks.Count==3,"Offline full-night planning returns three targets");
            Check(n.Picks.All(p=>p.Alt>=30&&p.Best>=n.Dark.First()&&p.Best<=n.Dark.Last()),"Targets are above 30 degrees during darkness");
            Check(n.Picks.Select(p=>p.Target.Canonical??p.Target.Name).Distinct().Count()==3,"Recommendations are unique");
            Check(n.Picks.Where(p=>p.Target.Body==null).All(p=>p.Guide!=null),"Deep-sky capture guidance is available");
            s.Active=1;var custom=new Night(s,new Forecast(),at);Check(custom.Picks.Count==3,"Custom equipment also produces plans");
            Check(n.Picks.First().Target.Name!=custom.Picks.First().Target.Name,"Equipment changes target ranking");
            var future=new Night(s,new Forecast(),at,new DateTime(2027,1,15));Check(future.Picks.Count==3,"Future planning works without forecasts");
            var polar=new Night(new Settings{WindowsLocation=false,Lat=78,Lon=15,Zone="W. Europe Standard Time"},new Forecast(),new DateTime(2026,6,21,12,0,0,DateTimeKind.Utc));
            Check(polar.Dark.Count==0,"Polar summer has no invented astronomical darkness");
            Check(StarCatalog.Data.Stars.Count>5000&&StarCatalog.Data.Lines.Count>100&&StarCatalog.Error=="","Offline sky-map data loads");
            foreach(string body in new[]{"Moon","Mars","Jupiter","Saturn","Venus"})using(var image=Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Objects",body+".jpg")))Check(image.Width>=400&&image.Height>=400,body+" reference image decodes");
            var clear=new Hour{Cloud=0,Rain=0,Wind=5,Gust=8,Temp=10,Dew=0,Humidity=40};
            Check(ObservingWeather.Evaluate(new[]{clear}).Word=="GO","Clear weather verdict");
            clear.Cloud=54;Check(ObservingWeather.Evaluate(new[]{clear}).Word=="MARGINAL","Marginal cloud verdict");
            clear.Cloud=95;Check(ObservingWeather.Evaluate(new[]{clear}).Word=="NO-GO","Heavy cloud verdict");
            Check(ObservingWeather.Evaluate(new[]{new Hour{}}).Word=="LIMITED DATA","Missing weather remains explicit");
            Check(Conditions.Dew(10,0)=="Low"&&Conditions.Dew(10,7)=="Medium"&&Conditions.Dew(10,9)=="High","Dew risk bands");
            Check(new Forecast().SeeingAt(at)==null,"Missing seeing is never invented");
            var w=new ObservingWeather(s,new Forecast(),custom);
            Check(w.DarkStart.HasValue&&w.DarkEnd>w.DarkStart,"Darkness boundaries are calculated");
            Check(Math.Abs(custom.Altitude(Body.Sun,w.DarkStart.Value)+18)<.01,"Dark sky starts with geometric Sun below -18 degrees");
            var dst=new Night(s,new Forecast(),at,new DateTime(2026,10,31));Check((dst.End-dst.Start).TotalHours==25,"DST night retains its extra hour");
            Check(LocationForm.MapZone("America/Denver")=="Mountain Standard Time","Windows time-zone mapping loads");
            count+=UpdaterChecks.Run();count+=SiteImageChecks.Run();Console.WriteLine(count+" offline release checks passed.");return 0;
        } catch(Exception error) {Console.Error.WriteLine("FAIL: "+error);return 1;}
        finally {Settings.Folder=original;}
    }
}
}
