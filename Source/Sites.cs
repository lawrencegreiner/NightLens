using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace NightLens {
public class HorizonPoint { public double Azimuth {get;set;} public double Altitude {get;set;} public override string ToString(){return Azimuth.ToString("0.##",CultureInfo.InvariantCulture)+"°   "+Altitude.ToString("0.##",CultureInfo.InvariantCulture)+"°";} }
public class HorizonProfile {
    public bool Enabled {get;set;}=true; public double MinimumAltitude {get;set;}=30;public double? AnchorLat {get;set;}public double? AnchorLon {get;set;}public double RadiusKm {get;set;}=1;
    public List<HorizonPoint> Points {get;set;}=new List<HorizonPoint>();
    [System.Web.Script.Serialization.ScriptIgnore]public bool HasMask {get{return Enabled&&Points!=null&&Points.Count>0;}}
    public HorizonProfile Copy(){return Json.Read<HorizonProfile>(Json.Write(this));}
    public void Normalize(){if(double.IsNaN(MinimumAltitude)||double.IsInfinity(MinimumAltitude)||MinimumAltitude<0||MinimumAltitude>89)throw new FormatException("Minimum target altitude must be 0–89 degrees.");if(Points==null)Points=new List<HorizonPoint>();if(Points.Count>3600)throw new FormatException("Use at most 3,600 horizon points.");var sorted=new SortedDictionary<double,double>();foreach(var p in Points){if(p==null||double.IsNaN(p.Azimuth)||double.IsInfinity(p.Azimuth)||double.IsNaN(p.Altitude)||double.IsInfinity(p.Altitude)||p.Azimuth<0||p.Azimuth>360||p.Altitude< -30||p.Altitude>90)throw new FormatException("Horizon points need azimuth 0–360° and altitude −30–90°.");double az=p.Azimuth==360?0:p.Azimuth;if(sorted.ContainsKey(az)&&Math.Abs(sorted[az]-p.Altitude)>.001)throw new FormatException("Conflicting horizon heights at azimuth "+az.ToString("0.##")+"°. North at 0° and 360° must agree.");sorted[az]=p.Altitude;}Points=sorted.Select(p=>new HorizonPoint{Azimuth=p.Key,Altitude=p.Value}).ToList();if(RadiusKm<=0||RadiusKm>10)RadiusKm=1;}
    public double Height(double az){if(!HasMask)return 0;if(Points.Count==1)return Points[0].Altitude;az=(az%360+360)%360;int low=0,high=Points.Count;while(low<high){int mid=(low+high)/2;if(Points[mid].Azimuth<=az)low=mid+1;else high=mid;}int upper=low==Points.Count?-1:low;HorizonPoint a,b;double x=az,lo,hi;if(upper==0){a=Points.Last();b=Points.First();lo=a.Azimuth-360;hi=b.Azimuth;}else if(upper<0){a=Points.Last();b=Points.First();lo=a.Azimuth;hi=b.Azimuth+360;}else{a=Points[upper-1];b=Points[upper];lo=a.Azimuth;hi=b.Azimuth;}return a.Altitude+(b.Altitude-a.Altitude)*(x-lo)/(hi-lo);}
    public double Limit(double az){return Math.Max(MinimumAltitude,HasMask?Height(az):0);}
    public static HorizonProfile Import(string text,string mode="azDeg_altDeg",double rotation=0){
        if(text==null||text.Length>256*1024)throw new FormatException("Horizon file is empty or exceeds 256 KB.");if(!new[]{"azDeg_altDeg","azDeg_zdDeg","azRad_altRad","azRad_zdRad","azGrad_altGrad","azGrad_zdGrad"}.Contains(mode))throw new FormatException("Unsupported Stellarium horizon format.");
        double factor=mode.Contains("Rad")?180/Math.PI:mode.Contains("Grad")?.9:1;bool zenith=mode.Contains("_zd");var result=new HorizonProfile();int line=0;
        foreach(string raw in text.Split('\n')){line++;string row=raw.Trim().TrimStart('\ufeff');int comment=row.IndexOf('#');if(comment>=0)row=row.Substring(0,comment).Trim();if(row=="")continue;var fields=Regex.Split(row,@"[\s,;]+");double az,alt;if(fields.Length!=2||!double.TryParse(fields[0],NumberStyles.Float,CultureInfo.InvariantCulture,out az)||!double.TryParse(fields[1],NumberStyles.Float,CultureInfo.InvariantCulture,out alt)||double.IsNaN(az)||double.IsInfinity(az)||double.IsNaN(alt)||double.IsInfinity(alt))throw new FormatException("Line "+line+": expected two numbers (azimuth and altitude), with optional # comment.");az*=factor;alt*=factor;if(az<0||az>360.000001)throw new FormatException("Line "+line+": azimuth is outside one turn for the selected units.");if(zenith)alt=90-alt;az=((az+rotation)%360+360)%360;result.Points.Add(new HorizonPoint{Azimuth=az,Altitude=alt});}
        if(!result.Points.Any())throw new FormatException("No horizon points found.");result.Normalize();return result;
    }
}
public class SiteChoice {public string Id;public string Label;public override string ToString(){return Label;}}
public class ObservingSite {
    public string Id {get;set;}=Guid.NewGuid().ToString("N");public string Name {get;set;}public double Lat {get;set;}public double Lon {get;set;}public string Zone {get;set;}
    public HorizonProfile Horizon {get;set;}=new HorizonProfile();public SkySite Sky {get;set;}
    public override string ToString(){return Name;}
}
public static class SiteLibrary {
    public static void Normalize(Settings s){if(s.Sites==null)s.Sites=new List<ObservingSite>();s.Sites=s.Sites.Where(x=>x!=null).ToList();if(s.Horizon==null)s.Horizon=new HorizonProfile();if(!s.Horizon.AnchorLat.HasValue)s.Horizon.AnchorLat=s.SavedLat;if(!s.Horizon.AnchorLon.HasValue)s.Horizon.AnchorLon=s.SavedLon;try{s.Horizon.Normalize();}catch{s.Horizon=new HorizonProfile();}foreach(var site in s.Sites){if(string.IsNullOrWhiteSpace(site.Id))site.Id=Guid.NewGuid().ToString("N");if(site.Horizon==null)site.Horizon=new HorizonProfile();try{site.Horizon.Normalize();}catch{site.Horizon=new HorizonProfile();}}}
    public static HorizonProfile HorizonAt(Settings s){var h=s.Horizon??new HorizonProfile();double lat=h.AnchorLat??s.SavedLat,lon=h.AnchorLon??s.SavedLon;if(SkySite.Distance(s.Lat,s.Lon,lat,lon)>h.RadiusKm)return new HorizonProfile{MinimumAltitude=h.MinimumAltitude,AnchorLat=s.Lat,AnchorLon=s.Lon};return h;}
    public static bool HorizonMoved(Settings s){return s.Horizon!=null&&s.Horizon.HasMask&&!ReferenceEquals(HorizonAt(s),s.Horizon);}
    public static ObservingSite Capture(Settings s){var site=new ObservingSite{Name=s.Place,Lat=s.Lat,Lon=s.Lon,Zone=s.Zone,Horizon=HorizonAt(s).Copy(),Sky=SkySite.At(s)==null?null:Json.Read<SkySite>(Json.Write(SkySite.At(s)))};site.Horizon.AnchorLat=s.Lat;site.Horizon.AnchorLon=s.Lon;return site;}
    public static void Validate(ObservingSite site){if(site==null||string.IsNullOrWhiteSpace(site.Name)||site.Name.Length>100||double.IsNaN(site.Lat)||double.IsInfinity(site.Lat)||Math.Abs(site.Lat)>90||double.IsNaN(site.Lon)||double.IsInfinity(site.Lon)||Math.Abs(site.Lon)>180)throw new FormatException("Enter a site name and valid coordinates.");TimeZoneInfo.FindSystemTimeZoneById(site.Zone);site.Horizon=site.Horizon??new HorizonProfile();if(!site.Horizon.AnchorLat.HasValue)site.Horizon.AnchorLat=site.Lat;if(!site.Horizon.AnchorLon.HasValue)site.Horizon.AnchorLon=site.Lon;site.Horizon.Normalize();if(site.Sky!=null&&(site.Sky.Bortle<1||site.Sky.Bortle>9||double.IsNaN(site.Sky.Bortle)))throw new FormatException("Bortle must be 1–9.");}
    public static void Use(Settings s,ObservingSite site){Validate(site);s.ActiveSiteId=site.Id;s.WindowsLocation=false;s.SavedPlace=site.Name;s.SavedLat=site.Lat;s.SavedLon=site.Lon;s.SavedZone=site.Zone;s.RestoreSaved();s.Horizon=site.Horizon.Copy();if(site.Sky!=null){var sky=Json.Read<SkySite>(Json.Write(site.Sky));s.SkySites=(s.SkySites??new List<SkySite>()).Where(x=>SkySite.Distance(sky.Lat,sky.Lon,x.Lat,x.Lon)>.01).ToList();s.SkySites.Add(sky);}}
    public static void RememberActive(Settings s){var active=(s.Sites??new List<ObservingSite>()).FirstOrDefault(x=>x.Id==s.ActiveSiteId);if(active==null||SkySite.Distance(s.Lat,s.Lon,active.Lat,active.Lon)>1)return;active.Horizon=HorizonAt(s).Copy();active.Sky=SkySite.At(s)==null?null:Json.Read<SkySite>(Json.Write(SkySite.At(s)));}
}
}
