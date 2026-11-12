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
// Original vector artwork: an abstract optical lens, rendered at window resolution.
class StartupSplash:Form {
    Timer animate;int phase,tick;string detail="Opening NightLens…";bool night;
    static readonly string[] steps={"Catalogs","Equipment","Sky positions","Forecast","Targets"};
    public StartupSplash(bool nightMode=false){night=nightMode;Text="NightLens · Starting";Style.Form(this);FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.CenterScreen;Style.FitScreen(this,960,500);DoubleBuffered=true;KeyPreview=true;KeyDown+=(a,b)=>{if(b.KeyCode==Keys.Escape)Close();};
        var close=new Button{Text="×",FlatStyle=FlatStyle.Flat,ForeColor=night?Color.FromArgb(220,45,45):Color.FromArgb(167,191,204),BackColor=Color.FromArgb(12,18,27),UseVisualStyleBackColor=false,Font=new Font("Segoe UI",12),Size=new Size(34,32),Location=new Point(Width-48,12),Anchor=AnchorStyles.Right|AnchorStyles.Top,AccessibleName="Close NightLens"};close.Click+=(a,b)=>Close();close.FlatAppearance.BorderSize=0;Controls.Add(close);animate=new Timer{Interval=80};animate.Tick+=(a,b)=>{tick++;Invalidate();};animate.Start();FormClosed+=(a,b)=>{animate.Stop();animate.Dispose();};}
    public void Stage(int index,string text){phase=Math.Max(0,Math.Min(5,index));detail=text;AccessibleDescription=text;Invalidate();Update();}
    void TextAt(Graphics g,string value,float size,Color color,RectangleF bounds,FontStyle weight=FontStyle.Regular){using(var font=new Font("Segoe UI",size,weight))using(var brush=new SolidBrush(color))g.DrawString(value,font,brush,bounds);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float sx=Width/960f,sy=Height/500f;g.ScaleTransform(sx,sy);
        Color accent=night?Color.FromArgb(220,42,42):Color.FromArgb(99,217,208),text=night?Color.FromArgb(225,57,57):Color.FromArgb(230,240,247),muted=night?Color.FromArgb(160,35,35):Color.FromArgb(146,167,184),bg=night?Color.FromArgb(12,2,3):Color.FromArgb(9,16,25);
        g.Clear(bg);using(var panel=new SolidBrush(night?Color.FromArgb(20,3,4):Color.FromArgb(13,25,36)))g.FillRectangle(panel,0,0,350,500);
        // Offset optical rings and a six-blade aperture make the identity independent of a sky photograph.
        g.TranslateTransform(175,218);for(int i=0;i<7;i++){float r=147-i*14;using(var pen=new Pen(Color.FromArgb(30+i*12,accent),i==0?1:2)){g.DrawEllipse(pen,-r,-r,r*2,r*2);}}
        using(var aperture=new GraphicsPath()){for(int i=0;i<6;i++){double a=(i*60-30)*Math.PI/180,b=((i+1)*60-30)*Math.PI/180;var p1=new PointF((float)Math.Cos(a)*89,(float)Math.Sin(a)*89);var p2=new PointF((float)Math.Cos(a+.45)*35,(float)Math.Sin(a+.45)*35);var p3=new PointF((float)Math.Cos(b)*89,(float)Math.Sin(b)*89);aperture.StartFigure();aperture.AddLines(new[]{p1,p2,p3});aperture.CloseFigure();}using(var fill=new SolidBrush(Color.FromArgb(32,accent)))g.FillPath(fill,aperture);using(var pen=new Pen(Color.FromArgb(135,accent),1))g.DrawPath(pen,aperture);}
        double angle=(tick%180)*Math.PI/90;float px=(float)Math.Cos(angle)*147,py=(float)Math.Sin(angle)*147;using(var b=new SolidBrush(accent))g.FillEllipse(b,px-3,py-3,6,6);using(var p=new Pen(Color.FromArgb(100,accent),1)){g.DrawLine(p,-11,0,11,0);g.DrawLine(p,0,-11,0,11);}g.ResetTransform();g.ScaleTransform(sx,sy);
        TextAt(g,"A clearer plan for the night",11,muted,new RectangleF(49,402,285,30));TextAt(g,"NightLens",43,text,new RectangleF(397,103,520,85),FontStyle.Bold);TextAt(g,"Find your next deep-sky subject.",15,muted,new RectangleF(402,190,500,40));
        TextAt(g,detail,12,text,new RectangleF(402,266,500,54));for(int i=0;i<5;i++){float x=403+i*98;using(var b=new SolidBrush(i<phase?accent:i==phase?Color.FromArgb(150,accent):Color.FromArgb(35,accent)))g.FillRectangle(b,x,338,85,4);TextAt(g,steps[i],9,i<=phase?accent:muted,new RectangleF(x,355,96,30));}
        TextAt(g,"WINDOWS  ·  "+AppUpdates.VersionText,9,muted,new RectangleF(402,447,480,25));
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
