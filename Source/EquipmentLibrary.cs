using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
namespace NightLens {
public class DeviceProfile {
    public string Id {get;set;} public string Name {get;set;} public string Kind {get;set;}
    public double? SensorWidth {get;set;} public double? SensorHeight {get;set;} public double? PixelSize {get;set;}
    public double? Focal {get;set;} public double? Aperture {get;set;} public double? GainStart {get;set;}
    public string GainGuidance {get;set;} public string Notes {get;set;} public string Source {get;set;} public string VerifiedOn {get;set;}
    public bool Legacy {get;set;} public List<double> AllowedSubs {get;set;} public double? MaxSub {get;set;}
    public bool Smart {get{return Kind=="Smart telescope";}}
    public bool HasSensor {get{return SensorWidth>0&&SensorHeight>0;}}
    public override string ToString(){return Name+(Legacy?" [legacy]":"")+(!HasSensor?" [specs needed]":"");}
}
public static class EquipmentLibrary {
    static List<DeviceProfile> profiles;public static string Error="";
    public static List<DeviceProfile> All {get{if(profiles==null){try{profiles=Json.Read<List<DeviceProfile>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"EquipmentLibrary","zwo-profiles.json")));if(profiles.GroupBy(p=>p.Id).Any(g=>g.Count()>1))throw new Exception("Duplicate device IDs");}catch(Exception e){Error="Device library unavailable: "+e.Message;profiles=new List<DeviceProfile>();}}return profiles;}}
    static string Normalize(string name){return System.Text.RegularExpressions.Regex.Replace((name??"").ToUpperInvariant().Replace("ZWO", ""),"[^A-Z0-9]","");}
    public static DeviceProfile Find(Rig rig){if(rig.CustomDevice)return null;if(!string.IsNullOrWhiteSpace(rig.DeviceProfileId))return All.FirstOrDefault(p=>p.Id==rig.DeviceProfileId);var camera=All.FirstOrDefault(p=>Normalize(p.Id)==Normalize(rig.Camera));return camera??All.FirstOrDefault(p=>p.Smart&&Normalize(p.Id)==Normalize(rig.Name));}
    public static void Apply(Rig rig,DeviceProfile profile){rig.CustomDevice=false;rig.DeviceProfileId=profile.Id;rig.SmartDevice=profile.Smart;rig.Camera=profile.Name;rig.CaptureModel="Auto";rig.GainOverride=null;rig.AllowedSubs="";rig.SensorW=profile.SensorWidth??0;rig.SensorH=profile.SensorHeight??0;if(profile.Smart){rig.Name=profile.Name;rig.Focal=profile.Focal??0;rig.Aperture=profile.Aperture??0;rig.Mount="Alt-az / smart telescope";rig.Filter=!profile.Name.Contains("Wide");}}
    public static string SmartFamily(Rig rig){if(rig.SmartModel=="Other"&&rig.CustomDevice)return "Other";string id=Find(rig)?.Id??rig.SmartModel??"";foreach(string family in new[]{"Seestar S30 Pro","Seestar S50 Pro","Seestar S30","Seestar S50"})if(id.StartsWith(family,StringComparison.OrdinalIgnoreCase))return family;return "Other";}
    public static DeviceProfile SmartProfile(string family,string lens){return All.FirstOrDefault(p=>p.Id==(family=="Seestar S50"?family:family+" — "+lens));}
    public static bool HasLenses(Rig rig){string family=SmartFamily(rig);return family!="Other"&&family!="Seestar S50";}
    public static void NormalizeSmartRig(Rig rig){var p=Find(rig);if(p==null||!p.Smart)return;string family=SmartFamily(rig);if(rig.Name==p.Name||rig.Name==p.Id)rig.Name=family;rig.SmartModel=family;rig.SmartDevice=true;rig.SmartLens=p.Name.Contains("Wide")?"Wide":"Tele";var canonical=SmartProfile(family,rig.SmartLens);if(canonical!=null&&canonical.Id!=p.Id)rig.DeviceProfileId=canonical.Id;}
    public static void SelectLens(Rig rig,string lens){string family=SmartFamily(rig);var profile=SmartProfile(family,lens);if(profile==null)throw new ArgumentException("Lens not available for this telescope.");string name=rig.Name,mount=rig.Mount;if(rig.SmartLens=="Wide"&&rig.SensorW>0&&rig.SensorH>0){rig.SmartWideSensorW=rig.SensorW;rig.SmartWideSensorH=rig.SensorH;}bool mosaic=rig.Mosaic;Apply(rig,profile);if(lens=="Wide"&&!profile.HasSensor){rig.SensorW=rig.SmartWideSensorW??0;rig.SensorH=rig.SmartWideSensorH??0;}rig.Name=name;rig.Mount=mount;rig.Mosaic=mosaic;rig.SmartModel=family;rig.SmartLens=lens;}
    public static bool Smart(Rig rig){var p=Find(rig);return p!=null?p.Smart:rig.SmartDevice||CapturePlanning.Model(rig)=="Other smart telescope"||CapturePlanning.Model(rig)=="Seestar S50";}
    public static string Gain(Rig rig,bool compact=false){var p=Find(rig);if(Smart(rig))return compact?"app-auto":"App-controlled (not adjustable)";if(rig.GainOverride.HasValue)return rig.GainOverride.Value.ToString("0.#",CultureInfo.InvariantCulture)+(compact?" override":" user override; verify the driver supports this value");if(p!=null){if(p.GainStart.HasValue)return compact?p.GainStart.Value.ToString("0.#",CultureInfo.InvariantCulture):p.GainGuidance;return compact?"unverified":p.GainGuidance;}return compact?"camera preset":string.IsNullOrEmpty(rig.DeviceProfileId)?"Use your camera's calibrated preset":"Saved device profile is missing; numeric gain unverified";}
    public static List<double> Subs(Rig rig){var p=Find(rig);var values=new List<double>();foreach(var value in (rig.AllowedSubs??"").Split(',')){double number;if(double.TryParse(value.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&number>0&&number<=3600&&(p==null||!p.MaxSub.HasValue||number<=p.MaxSub.Value)&&(p==null||p.Id!="Seestar S50"||p.AllowedSubs.Contains(number)))values.Add(number);}if(values.Count==0&&p!=null&&p.Smart)values.AddRange(p.AllowedSubs??new List<double>());return values.Distinct().OrderBy(v=>v).ToList();}
    public static string ProfileNote(Rig rig){var p=Find(rig);return p==null?(string.IsNullOrEmpty(rig.DeviceProfileId)?"":Error!=""?Error:"Saved device profile not found."):p.Name+" · "+(p.HasSensor?"sensor specifications loaded":"manual sensor dimensions required")+" · reviewed "+p.VerifiedOn+"\n"+p.GainGuidance;}
}
}

