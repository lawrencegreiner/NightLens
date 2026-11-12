using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Device.Location;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml.Linq;
using CosineKitty;
[assembly:System.Reflection.AssemblyTitle("NightLens")]
[assembly:System.Reflection.AssemblyProduct("NightLens")]
[assembly:System.Reflection.AssemblyDescription("Astrophotography night planning dashboard")]
[assembly:System.Reflection.AssemblyVersion("8.2.1.0")]
[assembly:System.Reflection.AssemblyFileVersion("8.2.1.0")]

namespace NightLens {
public class Rig {
    public string Name {get;set;} public string Camera {get;set;} public double Focal {get;set;}
    public double Aperture {get;set;} public double SensorW {get;set;} public double SensorH {get;set;}
    public string Mount {get;set;} public bool Filter {get;set;} public bool Mosaic {get;set;}
    public string CaptureModel {get;set;} = "Auto"; public string AllowedSubs {get;set;} = "";
    public string DeviceProfileId {get;set;} public bool SmartDevice {get;set;} public bool CustomDevice {get;set;} public string SmartModel {get;set;} public string SmartLens {get;set;} public double? SmartWideSensorW {get;set;} public double? SmartWideSensorH {get;set;} public double? GainOverride {get;set;}
    [ScriptIgnore] public double Width {get {return 2*Math.Atan(SensorW/(2*Focal))*180/Math.PI;}}
    [ScriptIgnore] public double Height {get {return 2*Math.Atan(SensorH/(2*Focal))*180/Math.PI;}}
    public override string ToString(){return Name;}
}
public class Settings {
    public string Place {get;set;} = "Centennial, Colorado";
    public double Lat {get;set;} = 39.5807; public double Lon {get;set;} = -104.8772;
    public string Zone {get;set;} = "Mountain Standard Time";
    public string SavedPlace {get;set;} = "Centennial, Colorado";
    public double SavedLat {get;set;} = 39.5807; public double SavedLon {get;set;} = -104.8772;
    public string SavedZone {get;set;} = "Mountain Standard Time";
    public bool WindowsLocation {get;set;} = true; public bool Fahrenheit {get;set;} = true;
    public List<string> EnabledCatalogs {get;set;} = new List<string> {"Messier.csv","NGC.csv","IC.csv","Supplement.csv"};
    public List<ObservingSite> Sites {get;set;} = new List<ObservingSite>(); public string ActiveSiteId {get;set;} public HorizonProfile Horizon {get;set;} = new HorizonProfile();
    public bool IncludeSolar {get;set;} = true;
    public string Theme {get;set;} = "Dark";
    public List<SkySite> SkySites {get;set;} = new List<SkySite>{new SkySite{Lat=39.5807,Lon=-104.8772,Bortle=8.9,Sqm=17.88,RadiusKm=5,Source="LightPollutionMap.app · Oct 2026 model · Centennial center",AreaEstimate=true}};
    public bool Pinned {get;set;} public bool Compact {get;set;} public int Active {get;set;}
    public int Left {get;set;} = -1; public int Top {get;set;} = -1;
    public List<Rig> Rigs {get;set;} = new List<Rig> {
        new Rig {Name="Seestar S50",Camera="Built-in IMX462",Focal=250,Aperture=50,SensorW=5.568,SensorH=3.132,Mount="Alt-az / smart telescope",Filter=true},
        new Rig {Name="Askar 60F + ASI585MC Pro",Camera="ZWO ASI585MC Pro",Focal=408,Aperture=60,SensorW=11.136,SensorH=6.264,Mount="Not specified"}
    };
    public static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NightLens");
    public static Settings Load(){try {return Json.Read<Settings>(File.ReadAllText(Path.Combine(Folder,"settings.json")));} catch{return new Settings();}}
    public Settings SavedCopy(){var copy=Json.Read<Settings>(Json.Write(this));copy.RestoreSaved();return copy;}
    public void RestoreSaved(){Place=SavedPlace;Lat=SavedLat;Lon=SavedLon;Zone=SavedZone;}
    public void Save(){Directory.CreateDirectory(Folder); string p=Path.Combine(Folder,"settings.json"); File.WriteAllText(p+".tmp",Json.Write(SavedCopy())); if(File.Exists(p)) File.Replace(p+".tmp",p,p+".bak"); else File.Move(p+".tmp",p);}
    [ScriptIgnore] public TimeZoneInfo TZ {get {try{return TimeZoneInfo.FindSystemTimeZoneById(Zone);}catch{return TimeZoneInfo.Local;}}}
    public DateTime Local(DateTime utc){return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),TZ);}
    public DateTime Utc(DateTime local){return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),TZ);}
}
static class Json {
    public static string Write(object x){return new JavaScriptSerializer{MaxJsonLength=16*1024*1024}.Serialize(x);}
    public static T Read<T>(string s){return new JavaScriptSerializer{MaxJsonLength=16*1024*1024}.Deserialize<T>(s);}
    public static Dictionary<string,object> Obj(string s){return Read<Dictionary<string,object>>(s);}
    public static double Num(object o){return Convert.ToDouble(o,CultureInfo.InvariantCulture);}
    public static object[] Arr(object o){return ((IEnumerable)o).Cast<object>().ToArray();}
}
class TimedClient:WebClient { protected override WebRequest GetWebRequest(Uri u){var r=base.GetWebRequest(u);r.Timeout=16000;return r;} }
static class Net {
    public static string Get(string url){using(var c=new TimedClient()){c.Headers[HttpRequestHeader.UserAgent]="NightLens/1.0 (personal astronomy planner)";return c.DownloadString(url);}}
    public static string Coord(double n){return n.ToString("0.######",CultureInfo.InvariantCulture);}
}
public class Hour {
    public DateTime Utc; public double? Temp,Cloud,Wind,Humidity,Dew,Rain,LowCloud,MidCloud,HighCloud,Gust,WindDir,FeelsLike,Pressure,Visibility,Precip;
}
public class Sky {
    public DateTime Utc; public int Seeing;public int? Transparency;
}
public class Forecast {
    public List<Hour> Hours=new List<Hour>(); public List<Sky> Seeing=new List<Sky>();
    public DateTime? WeatherFetched,SeeingRun; public string WeatherError="",SeeingError="",IanaZone="";
    public double? CurrentTemp,CurrentCloud; public DateTime? CurrentTime;
    public static DateTime Epoch(double n){return new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(n);}
    public void ParseWeather(string text){
        var d=Json.Obj(text);if(d.ContainsKey("timezone"))IanaZone=Convert.ToString(d["timezone"]);var h=(Dictionary<string,object>)d["hourly"];var times=Json.Arr(h["time"]);
        Hours.Clear();
        for(int i=0;i<times.Length;i++) Hours.Add(new Hour {Utc=Epoch(Json.Num(times[i])),Temp=At(h,"temperature_2m",i),Cloud=At(h,"cloud_cover",i),Wind=At(h,"wind_speed_10m",i),Humidity=At(h,"relative_humidity_2m",i),Dew=At(h,"dew_point_2m",i),Rain=At(h,"precipitation_probability",i),LowCloud=At(h,"cloud_cover_low",i),MidCloud=At(h,"cloud_cover_mid",i),HighCloud=At(h,"cloud_cover_high",i),Gust=At(h,"wind_gusts_10m",i),WindDir=At(h,"wind_direction_10m",i),FeelsLike=At(h,"apparent_temperature",i),Pressure=At(h,"pressure_msl",i),Visibility=At(h,"visibility",i),Precip=At(h,"precipitation",i)});
        if(d.ContainsKey("current")){var c=(Dictionary<string,object>)d["current"];CurrentTime=Epoch(Json.Num(c["time"]));CurrentTemp=Val(c,"temperature_2m");CurrentCloud=Val(c,"cloud_cover");}
        WeatherFetched=DateTime.UtcNow;
    }
    static double? Val(Dictionary<string,object> d,string k){return d.ContainsKey(k)&&d[k]!=null?(double?)Json.Num(d[k]):null;}
    static double? At(Dictionary<string,object> d,string k,int i){if(!d.ContainsKey(k)||d[k]==null)return null;var a=Json.Arr(d[k]);if(i>=a.Length||a[i]==null)return null;double value=Json.Num(a[i]);return double.IsNaN(value)||double.IsInfinity(value)||value== -9999?null:(double?)value;}
    public void ParseSeeing(string text){var d=Json.Obj(text);var init=DateTime.SpecifyKind(DateTime.ParseExact((string)d["init"],"yyyyMMddHH",CultureInfo.InvariantCulture),DateTimeKind.Utc);SeeingRun=init;Seeing.Clear();
        if(init>DateTime.UtcNow.AddHours(3)||DateTime.UtcNow-init>TimeSpan.FromHours(36)){SeeingError="7Timer forecast is out of date";return;}
        foreach(var o in Json.Arr(d["dataseries"])) {var row=(Dictionary<string,object>)o;int v=Convert.ToInt32(row["seeing"]);if(v>=1&&v<=8)Seeing.Add(new Sky{Utc=init.AddHours(Json.Num(row["timepoint"])),Seeing=v,Transparency=row.ContainsKey("transparency")&&row["transparency"]!=null&&Convert.ToInt32(row["transparency"])>=1&&Convert.ToInt32(row["transparency"])<=8?(int?)Convert.ToInt32(row["transparency"]):null});}
    }
    public Hour At(DateTime t){var h=Hours.OrderBy(x=>Math.Abs((x.Utc-t).TotalMinutes)).FirstOrDefault();return h!=null&&Math.Abs((h.Utc-t).TotalMinutes)<=35?h:null;}
    public int? SeeingAt(DateTime t){var x=Seeing.OrderBy(h=>Math.Abs((h.Utc-t).TotalMinutes)).FirstOrDefault();return x!=null&&Math.Abs((x.Utc-t).TotalMinutes)<=90?(int?)x.Seeing:null;}
    public int? TransparencyAt(DateTime t){var x=Seeing.OrderBy(h=>Math.Abs((h.Utc-t).TotalMinutes)).FirstOrDefault();return x!=null&&Math.Abs((x.Utc-t).TotalMinutes)<=90?x.Transparency:null;}
    public string WeatherNote(DateTime t){if(WeatherError!="")return "Weather service offline";if(Hours.Count==0)return "No weather data returned";if(t<Hours.Min(h=>h.Utc).AddMinutes(-35)||t>Hours.Max(h=>h.Utc).AddMinutes(35))return "Outside weather forecast range";return "Not reported by provider";}
    public string SeeingLabel(DateTime t,bool compact=false){int? v=SeeingAt(t);if(v.HasValue)return compact?(v<=3?"Good seeing":v<=5?"Fair seeing":"Poor seeing"):SeeingText(v);if(SeeingError!="")return SeeingError.Contains("out of date")?"Forecast out of date":"Seeing service offline";if(Seeing.Count==0)return "No seeing forecast returned";if(t<Seeing.Min(h=>h.Utc).AddMinutes(-90))return "No archived seeing forecast";if(t>Seeing.Max(h=>h.Utc).AddMinutes(90))return "Beyond seeing forecast range";return "Seeing not reported";}
    public static Forecast Fetch(Settings s){var f=new Forecast();string query="latitude="+Net.Coord(s.Lat)+"&longitude="+Net.Coord(s.Lon);
        var a=Task.Run(()=>{try {f.ParseWeather(Net.Get("https://api.open-meteo.com/v1/forecast?"+query+"&hourly=temperature_2m,cloud_cover,wind_speed_10m,relative_humidity_2m,dew_point_2m,precipitation_probability,cloud_cover_low,cloud_cover_mid,cloud_cover_high,wind_gusts_10m,wind_direction_10m,apparent_temperature,pressure_msl,visibility,precipitation&current=temperature_2m,cloud_cover&forecast_days=16&past_days=1&timeformat=unixtime&timezone=auto"));}catch(Exception e){f.WeatherError=e is WebException?"Weather service could not be reached":"Weather response could not be read";}});
        var b=Task.Run(()=>{try{f.ParseSeeing(Net.Get("https://www.7timer.info/bin/api.pl?lon="+Net.Coord(s.Lon)+"&lat="+Net.Coord(s.Lat)+"&product=astro&output=json"));}catch{f.SeeingError="Seeing forecast service could not be reached";}});
        Task.WaitAll(a,b);return f;
    }
    public static string SeeingText(int? n){if(!n.HasValue)return "Unavailable — no fresh seeing forecast";string[] a={"","Excellent · under 0.5 arcsec","Very good · 0.5–0.75 arcsec","Good · 0.75–1 arcsec","Fair · 1–1.25 arcsec","Fair · 1.25–1.5 arcsec","Poor · 1.5–2 arcsec","Poor · 2–2.5 arcsec","Very poor · over 2.5 arcsec"};return a[n.Value];}
}
public class Target {
    public string Name,Type,Canonical,CatalogName; public List<string> Aliases=new List<string>(); public double? Magnitude,Vmag,Bmag,Jmag,Hmag,Kmag,SurfaceBrightness; public string MagnitudeBand,Constellation,Morphology,CatalogNotes; public double Ra,Dec,Major,Minor; public Body? Body;
    public Target(string name,string type,double ra,double dec,double major,double minor){Name=name;Type=type;Ra=ra;Dec=dec;Major=major;Minor=minor;}
}
public class Pick {public Target Target;public DateTime Best,Start,End;public double Alt,Score,Separation;public string Fit,Advice;public CaptureGuide Guide;}
public class Night {
    public Settings S;public Observer Obs;public DateTime Start,End,Now;public List<DateTime> Dark=new List<DateTime>();public List<Pick> Picks=new List<Pick>();
    public double Illum,Phase; public DateTime? Rise,Set;public DateTime PhaseAt;
    public int CatalogCount; public string CatalogNotice; public HorizonProfile Horizon; public int PlanningStepMinutes;
    public Night(Settings s,Forecast forecast,DateTime now,DateTime? planningDate=null){S=s;Now=now;Obs=new Observer(s.Lat,s.Lon,0);Horizon=SiteLibrary.HorizonAt(s);PlanningStepMinutes=Horizon.HasMask||Horizon.MinimumAltitude!=30?1:10;var date=planningDate.HasValue?planningDate.Value.Date:UpcomingDate(s,now);Start=s.Utc(date.AddHours(12));End=s.Utc(date.AddDays(1).AddHours(12));
        for(DateTime t=Start;t<End;t=t.AddMinutes(PlanningStepMinutes))if(Altitude(Body.Sun,t)<-18)Dark.Add(t);
        PhaseAt=Dark.Count>0?Dark[Dark.Count/2]:Start.AddHours(12);var at=new AstroTime(PhaseAt);Illum=Astronomy.Illumination(Body.Moon,at).phase_fraction;Phase=Astronomy.MoonPhase(at);
        var rise=Astronomy.SearchRiseSet(Body.Moon,Obs,Direction.Rise,new AstroTime(Start),1);var set=Astronomy.SearchRiseSet(Body.Moon,Obs,Direction.Set,new AstroTime(Start),1);Rise=rise==null?(DateTime?)null:rise.ToUtcDateTime();Set=set==null?(DateTime?)null:set.ToUtcDateTime();
        if(s.Rigs.Count>0)Recommend(forecast,s.Rigs[Math.Max(0,Math.Min(s.Active,s.Rigs.Count-1))]);
    }
    public static DateTime UpcomingDate(Settings s,DateTime now){var local=s.Local(now);if(local.Hour>=12)return local.Date;var obs=new Observer(s.Lat,s.Lon,0);var dawn=Astronomy.SearchAltitude(Body.Sun,obs,Direction.Rise,new AstroTime(s.Utc(local.Date)),.5,-18);if(dawn!=null)return now<dawn.ToUtcDateTime()?local.Date.AddDays(-1):local.Date;var at=new AstroTime(now);var eq=Astronomy.Equator(Body.Sun,at,obs,EquatorEpoch.OfDate,Aberration.Corrected);return Astronomy.Horizon(at,obs,eq.ra,eq.dec,Refraction.None).altitude< -18?local.Date.AddDays(-1):local.Date;}
    public double Altitude(Body body,DateTime t){var at=new AstroTime(t);var e=Astronomy.Equator(body,at,Obs,EquatorEpoch.OfDate,Aberration.Corrected);return Astronomy.Horizon(at,Obs,e.ra,e.dec,body==Body.Sun?Refraction.None:Refraction.Normal).altitude;}
    public Equatorial Coords(Target x,DateTime t){var at=new AstroTime(t);if(x.Body.HasValue)return Astronomy.Equator(x.Body.Value,at,Obs,EquatorEpoch.OfDate,Aberration.Corrected);var v=Astronomy.VectorFromSphere(new Spherical(x.Dec,x.Ra*15,1),at);return Astronomy.EquatorFromVector(Astronomy.RotateVector(Astronomy.Rotation_EQJ_EQD(at),v));}
    public bool Visible(Target x,DateTime t){var eq=Coords(x,t);var horizontal=Astronomy.Horizon(new AstroTime(t),Obs,eq.ra,eq.dec,Refraction.Normal);return horizontal.altitude>=Horizon.Limit(horizontal.azimuth);}
    public double Altitude(Target x,DateTime t){var e=Coords(x,t);return Astronomy.Horizon(new AstroTime(t),Obs,e.ra,e.dec,Refraction.Normal).altitude;}
    public string PhaseName(){if(Phase<10||Phase>=350)return "New Moon";if(Phase<80)return "Waxing crescent";if(Phase<100)return "First quarter";if(Phase<170)return "Waxing gibbous";if(Phase<190)return "Full Moon";if(Phase<260)return "Waning gibbous";if(Phase<280)return "Last quarter";return "Waning crescent";}
    public string Time(DateTime t){return S.Local(t).ToString("ddd h:mm tt",CultureInfo.InvariantCulture);}
    public static double Separation(double ra,double dec,double ra2,double dec2){double r=Math.PI/180;return Math.Acos(Math.Max(-1,Math.Min(1,Math.Sin(dec*r)*Math.Sin(dec2*r)+Math.Cos(dec*r)*Math.Cos(dec2*r)*Math.Cos((ra-ra2)*15*r))))/r;}
    void Recommend(Forecast f,Rig rig){
        string notice;var targets=CatalogLibrary.Load(S,out notice);CatalogCount=targets.Count;CatalogNotice=notice; if(S.IncludeSolar)foreach(Body b in new[]{Body.Moon,Body.Jupiter,Body.Saturn,Body.Mars,Body.Venus}) targets.Add(new Target(b.ToString(),b==Body.Moon?"Moon":"Planet",0,0,b==Body.Moon?30:1,b==Body.Moon?30:1){Body=b});
        var slots=new Dictionary<DateTime,double[]>();var moons=new Dictionary<DateTime,Equatorial>();double rad=Math.PI/180,sinlat=Math.Sin(S.Lat*rad),coslat=Math.Cos(S.Lat*rad);
        for(DateTime t=Start;t<End;t=t.AddMinutes(PlanningStepMinutes)){double sun=Altitude(Body.Sun,t);if(sun> -18)continue;slots[t]=new[]{sun,(Astronomy.SiderealTime(new AstroTime(t))+S.Lon/15)*Math.PI/12,0.0,Altitude(Body.Moon,t)};moons[t]=Astronomy.Equator(Body.Moon,new AstroTime(t),Obs,EquatorEpoch.OfDate,Aberration.Corrected);}
        foreach(var x in targets){if(x.Type=="Star"||x.Type=="Double star"||x.Type=="Nova")continue;bool planetary=x.Body.HasValue;var samples=new List<Tuple<DateTime,double>>();var fixedEq=planetary?default(Equatorial):Coords(x,PhaseAt);
            foreach(var slot in slots){var t=slot.Key;if(slot.Value[0]>(planetary?-6:-18))continue;double alt=planetary?Altitude(x,t):Math.Asin(Math.Max(-1,Math.Min(1,sinlat*Math.Sin(fixedEq.dec*rad)+coslat*Math.Cos(fixedEq.dec*rad)*Math.Cos(slot.Value[1]-fixedEq.ra*Math.PI/12))))/rad;double az=0;if(Horizon.HasMask){if(planetary){var horizonEq=Coords(x,t);az=Astronomy.Horizon(new AstroTime(t),Obs,horizonEq.ra,horizonEq.dec,Refraction.Normal).azimuth;}else{double ha=slot.Value[1]-fixedEq.ra*Math.PI/12;az=Math.Atan2(-Math.Cos(fixedEq.dec*rad)*Math.Sin(ha),Math.Sin(fixedEq.dec*rad)*coslat-Math.Cos(fixedEq.dec*rad)*sinlat*Math.Cos(ha))/rad;if(az<0)az+=360;}}if(alt>=Horizon.Limit(az))samples.Add(Tuple.Create(t,alt));}
            var usable=new List<Tuple<DateTime,double>>();var segment=new List<Tuple<DateTime,double>>();foreach(var sample in samples){if(segment.Count>0&&(sample.Item1-segment.Last().Item1).TotalMinutes>PlanningStepMinutes+.1){if((segment.Last().Item1-segment.First().Item1).TotalMinutes>=30)usable.AddRange(segment);segment.Clear();}segment.Add(sample);}if(segment.Count>0&&(segment.Last().Item1-segment.First().Item1).TotalMinutes>=30)usable.AddRange(segment);samples=usable;if(samples.Count<2)continue;
            Func<Tuple<DateTime,double>,double> score=h=>h.Item2-slots[h.Item1][2];
            var best=samples.OrderByDescending(score).First();double fw=Math.Max(rig.Width,rig.Height)*60,fh=Math.Min(rig.Width,rig.Height)*60;
            bool knownSize=x.Major>0&&x.Minor>0;bool fits=knownSize&&x.Major<=fw*.9&&x.Minor<=fh*.9;bool small=knownSize&&x.Major<fw*.08;string fit=!knownSize?"Size unavailable — verify framing":fits?"Fits one frame":rig.Mosaic?"Mosaic needed":"Close-up only; full object will not fit";
            string advice="Stack several short exposures; check a test frame.";
            if(rig.Mount=="Not specified") advice="Set your mount type to get exposure guidance.";
            else if(rig.Mount.Contains("Alt-az")) advice="Use short subframes to limit field rotation; stack for total integration.";
            else if(rig.Mount=="Untracked tripod") advice="Use very short test exposures; tracking is recommended for faint targets.";
            else advice="Tracked setup: start with short test exposures and check stars before increasing exposure.";
            if(planetary){advice=x.Body==Body.Moon?"Use short exposures or video; protect bright lunar detail.":"Small at this focal length; video and cropping are more useful than long exposures.";fit=x.Body==Body.Moon?fit:"Small planetary disc";}
            var eq=Coords(x,best.Item1);var moon=moons[best.Item1];double sep=Separation(eq.ra,eq.dec,moon.ra,moon.dec);bool moonUp=slots[best.Item1][3]>0;double moonPenalty=0;
            if(!planetary&&moonUp){moonPenalty=Illum*22+(sep<40?25:sep<70?10:0);if(x.Type=="Emission nebula"&&rig.Filter){moonPenalty*=.45;advice+=" Use your emission-nebula filter.";}else if(Illum>.5)advice+=" Bright Moon reduces faint detail.";if(sep<40)advice+=" Close to the Moon; expect extra glare.";}
            if(small&&!planetary)advice+=" Small in this frame; expect to crop.";
            double brightnessPenalty=planetary?0:x.Magnitude.HasValue?Math.Max(0,x.Magnitude.Value-(8+5*Math.Log10(rig.Aperture/50)))*6:15;var site=SkySite.At(S);double lightPenalty=site!=null&&site.Bortle>5&&(x.Type=="Dark nebula"||x.Type=="Reflection nebula"||x.Type=="Galaxy")?(site.Bortle-5)*(x.Type=="Dark nebula"?8:4):0;double total=score(best)-lightPenalty+(fits?18:!knownSize?-8:rig.Mosaic?2:-20)-moonPenalty-brightnessPenalty-(small?12:0)-(planetary&&x.Body!=Body.Moon?30:0);
            // Keep only the uninterrupted visibility interval containing the best sample.
            int index=samples.IndexOf(best),a=index,z=index;while(a>0&&(samples[a].Item1-samples[a-1].Item1).TotalMinutes<PlanningStepMinutes+.1)a--;while(z<samples.Count-1&&(samples[z+1].Item1-samples[z].Item1).TotalMinutes<PlanningStepMinutes+.1)z++;
            Picks.Add(new Pick{Target=x,Best=best.Item1,Start=samples[a].Item1,End=samples[z].Item1,Alt=best.Item2,Score=total,Fit=fit,Advice=advice,Separation=sep});
        }Picks=ImagingGroups.Unique(Picks,rig).Take(3).ToList();foreach(var pick in Picks){DateTime best=pick.Best;double high=Altitude(pick.Target,best);for(DateTime time=pick.Start;time<=pick.End;time=time.AddMinutes(5)){double alt=Altitude(pick.Target,time);if(alt>high&&Visible(pick.Target,time)){high=alt;best=time;}}DateTime refineStart=best.AddMinutes(-5)<pick.Start?pick.Start:best.AddMinutes(-5),refineEnd=best.AddMinutes(5)>pick.End?pick.End:best.AddMinutes(5);for(DateTime time=refineStart;time<=refineEnd;time=time.AddMinutes(1)){double alt=Altitude(pick.Target,time);if(alt>high&&Visible(pick.Target,time)){high=alt;best=time;}}pick.Best=best;pick.Alt=high;pick.Guide=CapturePlanning.For(pick,rig,S,this);}
    }
    static DateTime nowFloor(DateTime t){return t.AddMinutes(-10);}
}

static class Program {
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool SetProcessDPIAware();
    [System.Runtime.InteropServices.DllImport("shell32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [STAThread]static int Main(string[] args){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;SetProcessDPIAware();SetCurrentProcessExplicitAppUserModelID("NightLens.Desktop");Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length==3&&args[0]=="--apply-update")return UpdateWorker.Run(args[1],args[2]);if(args.Length>0&&args[0]=="--fetch-images")return ObjectTests.FetchImages(args);if(args.Length>0&&args[0]=="--test")return Tests.Run(args);
        Application.ThreadException+=(a,b)=>MessageBox.Show("NightLens encountered a problem: "+b.Exception.Message,"NightLens");Application.Run(new StartupContext());return 0;
    }
}
static class Tests {
    public static int Run(string[] args){var log=new List<string>();try{ObjectImages.CacheFolder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[4])),"object-image-cache");
        Action<bool,string> check=(ok,text)=>{if(!ok)throw new Exception(text);log.Add("PASS: "+text);};var s=new Settings{WindowsLocation=false};var f=new Forecast();f.ParseWeather(File.ReadAllText(args[1]));f.ParseSeeing(File.ReadAllText(args[2]));
        check(f.Hours.Count>=72,"Live weather response parses with at least 72 hours");check(f.CurrentTime.HasValue,"Current conditions timestamp parses");check(f.Seeing.Count>0,"Fresh 7Timer response parses");
        DateTime now=new DateTime(2026,10,24,18,0,0,DateTimeKind.Utc);var n=new Night(s,f,now);check(n.Dark.Count>40,"Centennial has astronomical darkness");check(n.Illum>=0&&n.Illum<=1,"Moon illumination is bounded");check(n.Rise.HasValue&&n.Set.HasValue,"Moon rise and set calculated");check(n.Rise.Value>=n.Start&&n.Rise.Value<n.End,"Moonrise belongs to observing night");check(n.Picks.Count>0,"Targets ranked for Seestar");check(n.Picks.All(p=>p.Alt>=30),"Suggested targets above minimum altitude");check(n.Picks.All(p=>p.Best>=n.Dark.First()),"Suggested times begin in the dark-sky window");
        check(Math.Abs(s.Rigs[0].Width-1.276)<.01,"Seestar field of view matches sensor and focal length");check(Math.Abs(s.Rigs[1].Width-1.564)<.01,"Askar 60F field of view matches ASI585 sensor");s.Active=1;var n2=new Night(s,f,now);check(n2.Picks.Count>0,"Askar profile produces targets");
        check(LocationForm.MapZone("America/Denver")=="Mountain Standard Time","Location time zone maps correctly");check(s.Local(new DateTime(2026,11,1,7,30,0,DateTimeKind.Utc)).Hour==1&&s.Local(new DateTime(2026,11,1,8,30,0,DateTimeKind.Utc)).Hour==1,"DST repeated hour handled by time zone rules");
        var south=new Settings{Lat=-33.87,Lon=151.21,Zone="AUS Eastern Standard Time",WindowsLocation=false};var sn=new Night(south,new Forecast(),now);check(sn.Dark.Count>0,"Southern hemisphere darkness works offline");var polar=new Settings{Lat=78,Lon=15,Zone="W. Europe Standard Time",WindowsLocation=false};var pn=new Night(polar,new Forecast(),new DateTime(2026,6,21,12,0,0,DateTimeKind.Utc));check(pn.Dark.Count==0,"Polar summer correctly has no astronomical darkness");
        check(new Forecast().SeeingAt(now)==null,"Missing seeing never becomes an invented rating");check(f.At(now.AddYears(1))==null,"Out of range forecast is unavailable");var afterMidnight=new Night(s,f,s.Utc(new DateTime(2026,10,25,2,0,0)));check(afterMidnight.Start==n.Start,"After midnight still uses the same observing night");
        var stale=new Forecast();stale.ParseSeeing("{\"init\":\"2020010100\",\"dataseries\":[]}");check(stale.Seeing.Count==0&&stale.SeeingError!="","Stale seeing is rejected");
        var roundtrip=Json.Read<Settings>(Json.Write(s));check(roundtrip.Rigs.Count==2&&roundtrip.Rigs[1].Focal==408,"Multiple equipment profiles survive serialization");
        string originalFolder=Settings.Folder;try{Settings.Folder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[4])),"test-settings");roundtrip.Place="Detected location";roundtrip.Lat=10;roundtrip.Save();var loaded=Settings.Load();check(loaded.Place=="Centennial, Colorado"&&Math.Abs(loaded.Lat-39.5807)<.0001,"Windows detection does not overwrite the saved fallback");loaded.Rigs.Add(new Rig{Name="Third test setup",Focal=135,Aperture=30,SensorW=23.5,SensorH=15.6,Mount="Equatorial / tracker",Camera="Test"});loaded.Save();check(Settings.Load().Rigs.Count==3,"Additional equipment setup persists to disk");}finally{Settings.Folder=originalFolder;}
        using(var editor=new EquipmentForm(s)){editor.Show();Application.DoEvents();using(var bmp=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(Path.Combine(Path.GetDirectoryName(args[3]),"Equipment preview.png"),System.Drawing.Imaging.ImageFormat.Png);}editor.Close();}log.Add("PASS: Equipment editor rendered with both profiles");
        using(var form=new MainForm(true)){form.LoadTest(f,now);form.Show();Application.DoEvents();using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(args[3],System.Drawing.Imaging.ImageFormat.Png);}form.Close();}log.Add("PASS: Main window rendered successfully");
        V2Tests.Run(f,now,args,log);V4Tests.Run(f,now,args,log);ThemeTests.Run(f,now,args,log);EquipmentTests.Run(f,now,args,log);ObjectTests.Run(f,now,args,log);ImageServiceTests.Run(args,log);WeatherTests.Run(f,now,args,log);File.WriteAllLines(args[4],log);return 0;
    }catch(Exception e){log.Add("FAIL: "+e);File.WriteAllLines(args[4],log);return 1;}}
}
}












