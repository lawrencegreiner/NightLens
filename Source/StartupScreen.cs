using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CosineKitty;
namespace NightLens {
class StartupSplash:Form {
    Bitmap sky;Timer animate;int phase,tick;string detail="Opening NightLens…";bool night;
    static readonly string[] steps={"STAR CATALOG","EQUIPMENT","EPHEMERIS","FORECAST","TONIGHT'S TARGETS"};
    public StartupSplash(bool nightMode=false){night=nightMode;Text="NightLens · Starting";Style.Form(this);FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.CenterScreen;Style.FitScreen(this,1040,590);DoubleBuffered=true;KeyPreview=true;KeyDown+=(a,b)=>{if(b.KeyCode==Keys.Escape)Close();};try{using(var image=Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Startup-sky.png")))sky=new Bitmap(image);}catch{}
        var close=new Button{Text="×",FlatStyle=FlatStyle.Flat,ForeColor=night?Color.FromArgb(230,45,45):Color.FromArgb(239,196,94),UseVisualStyleBackColor=false,Font=new Font("Segoe UI",12)};close.Click+=(a,b)=>Close();close.AccessibleName="Close NightLens";close.Size=new Size(34,32);close.Location=new Point(Width-48,12);close.Anchor=AnchorStyles.Right|AnchorStyles.Top;close.BackColor=Color.FromArgb(20,20,20);close.FlatAppearance.BorderSize=0;Controls.Add(close);animate=new Timer{Interval=100};animate.Tick+=(a,b)=>{tick++;Invalidate();};animate.Start();FormClosed+=(a,b)=>{animate.Stop();animate.Dispose();sky?.Dispose();};}
    public void Stage(int index,string text){phase=Math.Max(0,Math.Min(5,index));detail=text;AccessibleDescription=text;Invalidate();Update();}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Black);if(sky!=null){float scale=Math.Max(Width/(float)sky.Width,Height/(float)sky.Height);var rect=new RectangleF((Width-sky.Width*scale)/2,(Height-sky.Height*scale)/2,sky.Width*scale,sky.Height*scale);using(var attributes=new ImageAttributes()){if(night)attributes.SetColorMatrix(new ColorMatrix(new[]{new float[]{.12f,0,0,0,0},new float[]{.23f,0,0,0,0},new float[]{.04f,0,0,0,0},new float[]{0,0,0,1,0},new float[]{0,0,0,0,1}}));g.DrawImage(sky,Rectangle.Round(rect),0,0,sky.Width,sky.Height,GraphicsUnit.Pixel,attributes);}}
        using(var shade=new SolidBrush(Color.FromArgb(70,0,0,0)))g.FillRectangle(shade,ClientRectangle);Color gold=night?Color.FromArgb(235,48,48):Color.FromArgb(239,196,94),soft=night?Color.FromArgb(170,30,30):Color.FromArgb(194,192,179);float scaleText=Width/1040f;using(var pen=new Pen(Color.FromArgb(130,gold),1)){foreach(Point p in new[]{new Point(22,22),new Point(Width-22,22),new Point(22,Height-22),new Point(Width-22,Height-22)}){int dx=p.X<Width/2?1:-1,dy=p.Y<Height/2?1:-1;g.DrawLine(pen,p.X,p.Y,p.X+dx*29,p.Y);g.DrawLine(pen,p.X,p.Y,p.X,p.Y+dy*29);}}
        using(var font=new Font("Consolas",10*scaleText,FontStyle.Bold))using(var brush=new SolidBrush(gold)){g.DrawString("NIGHTLENS STARTUP",font,brush,52,40);g.DrawString("VERSION "+AppUpdates.VersionText,font,brush,Width-230,40);for(int i=0;i<steps.Length;i++){string marker=i<phase?"✓":i==phase?(tick%8<4?"○":"◌"):"·";using(var line=new SolidBrush(i<=phase?gold:Color.FromArgb(120,soft)))g.DrawString(marker+"  "+steps[i],font,line,55,77+i*27);}g.DrawString(detail,font,brush,new RectangleF(52,Height-58,Width-100,28));}
        float center=Width*.59f;using(var title=new Font("Segoe UI Light",55*scaleText,FontStyle.Regular))using(var brush=new LinearGradientBrush(new PointF(0,Height*.4f),new PointF(0,Height*.65f),gold,night?gold:Color.FromArgb(188,132,35))){string text="NIGHTLENS";SizeF size=g.MeasureString(text,title);g.DrawString(text,title,brush,center-size.Width/2,Height*.39f);}
        using(var subtitle=new Font("Segoe UI",12*scaleText,FontStyle.Regular))using(var brush=new SolidBrush(night?gold:Color.FromArgb(224,224,217))){string text="YOUR SKY · TONIGHT";SizeF size=g.MeasureString(text,subtitle);g.DrawString(text,subtitle,brush,center-size.Width/2,Height*.57f);}
        using(var line=new Pen(Color.FromArgb(60,gold),1))g.DrawLine(line,center-110,Height*.7f,center+110,Height*.7f);using(var line=new Pen(gold,2)){float fraction=Math.Max(.05f,phase/5f);g.DrawLine(line,center-110,Height*.7f,center-110+220*fraction,Height*.7f);}
    }
}
class StartupContext:ApplicationContext {
    StartupSplash splash;bool handingOff,closed;
    public StartupContext(){var saved=Settings.Load();Style.SetTheme(saved.Theme);splash=new StartupSplash(saved.Theme=="Night");MainForm=splash;splash.FormClosed+=(a,b)=>{if(!handingOff){closed=true;ExitThread();}};splash.Shown+=async(a,b)=>await Start();splash.Show();}
    async Task Start(){NightLens.MainForm main=null;try{
        splash.Stage(0,"Reading selected star and target catalogs…");await Task.Run(()=>{var data=StarCatalog.Data;var catalogs=CatalogLibrary.Available().ToList();});if(closed)return;
        splash.Stage(1,"Loading your equipment profiles…");await Task.Run(()=>{var profiles=EquipmentLibrary.All;});if(closed)return;
        splash.Stage(2,"Preparing Sun, Moon and sky positions…");await Task.Run(()=>Astronomy.SiderealTime(new AstroTime(DateTime.UtcNow)));if(closed)return;
        main=new NightLens.MainForm();await main.PrepareStartup(text=>{if(!closed)splash.Stage(text.StartsWith("Scoring",StringComparison.Ordinal)?4:3,text);});if(closed){main.Dispose();return;}
        splash.Stage(5,"Ready — opening your sky tonight");handingOff=true;MainForm=main;splash.Close();main.Show();
    }catch(Exception error){if(closed)return;handingOff=true;if(main==null)main=new NightLens.MainForm();MainForm=main;splash.Close();main.Show();MessageBox.Show(main,"Startup could not finish: "+error.Message+"\nYou can check your location and refresh from the app.","NightLens");}}
}
}
