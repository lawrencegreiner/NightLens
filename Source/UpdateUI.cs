using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace NightLens {
public sealed class AppUpdateController:IDisposable {
    readonly Form owner;readonly Action changed;readonly System.Windows.Forms.Timer timer;readonly CancellationTokenSource cancel=new CancellationTokenSource();bool checking,disposed;readonly bool test;internal Func<CancellationToken,UpdateInfo> Checker=AppUpdates.Check;
    public UpdatePreferences Preferences {get;private set;} public UpdateInfo Available {get;private set;} public string Status {get;private set;}="Ready to check for updates.";
    public AppUpdateController(Form owner,Action changed,bool test=false){this.owner=owner;this.changed=changed;this.test=test;Preferences=test?new UpdatePreferences():AppUpdates.LoadPreferences();timer=new System.Windows.Forms.Timer{Interval=30*60*1000};timer.Tick+=async(a,b)=>await Check(false);owner.Shown+=async(a,b)=>{ShowResult();timer.Start();await Check(false);};}
    void ShowResult(){string file=Path.Combine(AppUpdates.DataRoot,"update-result.json");try{if(File.Exists(file)){var result=Json.Read<UpdateResult>(File.ReadAllText(file));File.Delete(file);if(result!=null){using(var dialog=new Form()){Style.Form(dialog);dialog.Text="NightLens update";Style.FitScreen(dialog,600,230);var grid=Style.Grid(1,-100,40);grid.Padding=new Padding(16);grid.Controls.Add(Style.Label(result.Message+(result.Success?" Your equipment and observing settings are retained.":""),11),0,0);grid.Controls.Add(Style.Button("Close",(a,b)=>dialog.Close()),0,1);dialog.Controls.Add(grid);dialog.ShowDialog(owner);}}}}catch{}}
    public void SetAutomatic(bool enabled){Preferences.CheckAutomatically=enabled;if(!test)AppUpdates.SavePreferences(Preferences);}
    public async Task Check(bool force){
        if(disposed)return;if(checking){if(force)while(checking&&!disposed)await Task.Delay(50);return;}if(!force&&(!Preferences.CheckAutomatically||DateTime.UtcNow-Preferences.LastAttemptUtc<TimeSpan.FromHours(6)))return;
        checking=true;Status="Checking GitHub for updates…";
        try{Preferences.LastAttemptUtc=DateTime.UtcNow;if(!test)AppUpdates.SavePreferences(Preferences);Available=await Task.Run(()=>Checker(cancel.Token));if(disposed)return;Preferences.LastCheckedUtc=DateTime.UtcNow;if(!test)AppUpdates.SavePreferences(Preferences);Status=Available==null?"You have the latest stable version.":"NightLens "+Available.Version+" is available.";changed?.Invoke();}
        catch(OperationCanceledException){}catch(Exception error){Status=error is ArgumentException?"GitHub's update information could not be read. Try again later.":error.Message;}
        finally{checking=false;}
    }
    public void Dispose(){disposed=true;timer.Stop();timer.Dispose();cancel.Cancel();cancel.Dispose();}
}
public sealed class UpdateForm:Form {
    readonly Form owner;readonly AppUpdateController controller;readonly Label latest,status;readonly Button check,install,close;readonly CheckBox automatic;readonly CancellationTokenSource cancel=new CancellationTokenSource();bool downloading;
    public UpdateForm(Form owner,AppUpdateController controller){
        this.owner=owner;this.controller=controller;Style.Form(this);Text="NightLens updates";Style.FitScreen(this,700,330);
        var grid=Style.Grid(1,34,32,40,-100,34,40);grid.Padding=new Padding(16);Controls.Add(grid);
        grid.Controls.Add(Style.Label("NightLens updates",18,true),0,0);grid.Controls.Add(Style.Label("Installed version: "+AppUpdates.VersionText,11),0,1);
        latest=Style.Label("Checking for a newer version…",12,true);grid.Controls.Add(latest,0,2);
        status=Style.Label("Updates come from the public NightLens GitHub releases. No sign-in is needed.",10);status.TextAlign=ContentAlignment.TopLeft;grid.Controls.Add(status,0,3);
        automatic=new CheckBox{Text="Check automatically at startup and every six hours",Checked=controller.Preferences.CheckAutomatically,Dock=DockStyle.Fill};automatic.CheckedChanged+=(a,b)=>{try{controller.SetAutomatic(automatic.Checked);}catch{SetStatus("The automatic-check preference could not be saved.");}};grid.Controls.Add(automatic,0,4);
        var buttons=Style.Grid(3,-100);check=Style.Button("Check now",async(a,b)=>await CheckNow());install=Style.Button("Download and restart",async(a,b)=>await Install());close=Style.Button("Close",(a,b)=>Close());buttons.Controls.Add(check,0,0);buttons.Controls.Add(install,1,0);buttons.Controls.Add(close,2,0);grid.Controls.Add(buttons,0,5);
        Shown+=async(a,b)=>await CheckNow();FormClosing+=(a,b)=>{cancel.Cancel();};FormClosed+=(a,b)=>cancel.Dispose();RefreshState();Style.Apply(this);
    }
    void SetStatus(string text){if(!IsDisposed)status.Text=text;}
    void RefreshState(){var info=controller.Available;latest.Text=info==null?"Installed: "+AppUpdates.VersionText:"Available: "+info.Version;install.Enabled=info!=null&&!downloading;check.Enabled=!downloading;automatic.Enabled=!downloading;if(!downloading)SetStatus(controller.Status);}
    async Task CheckNow(){check.Enabled=false;install.Enabled=false;SetStatus("Checking GitHub for updates…");await controller.Check(true);if(!IsDisposed)RefreshState();}
    async Task Install(){
        if(downloading||controller.Available==null)return;var info=controller.Available;downloading=true;RefreshState();SetStatus("Downloading the update. NightLens will restart after the package is verified.");
        try{var progress=new Progress<string>(s=>SetStatus(s));string run=await Task.Run(()=>AppUpdates.DownloadAndStage(info,cancel.Token,s=>((IProgress<string>)progress).Report(s)));if(IsDisposed||cancel.IsCancellationRequested)return;SetStatus("Update verified. Preparing to restart…");close.Enabled=false;await Task.Run(()=>AppUpdates.StartWorker(run,info.Version));DialogResult=DialogResult.OK;Close();owner.BeginInvoke(new Action(()=>owner.Close()));}
        catch(OperationCanceledException){}catch(Exception error){downloading=false;if(!IsDisposed){close.Enabled=true;RefreshState();SetStatus(error.Message);}}
    }
}
}
