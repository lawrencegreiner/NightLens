using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace NightLens {
enum ConditionLevel {Unknown,Good,Medium,Poor}
static class Conditions {
    public static ConditionLevel Seeing(int? value){return !value.HasValue||value<1||value>8?ConditionLevel.Unknown:value<=3?ConditionLevel.Good:value<=5?ConditionLevel.Medium:ConditionLevel.Poor;}
    public static ConditionLevel Clouds(double? value){return !value.HasValue||double.IsNaN(value.Value)||value<0||value>100?ConditionLevel.Unknown:value<=20?ConditionLevel.Good:value<=50?ConditionLevel.Medium:ConditionLevel.Poor;}
    public static string CloudText(double? value,string missing="Not forecast"){var level=Clouds(value);return level==ConditionLevel.Unknown?missing:value.Value.ToString("0")+"% · "+(level==ConditionLevel.Good?"Low":level==ConditionLevel.Medium?"Medium":"High");}
    public static string Dew(double? temp,double? dew){if(!temp.HasValue||!dew.HasValue||double.IsNaN(temp.Value)||double.IsNaN(dew.Value))return "Not forecast";double gap=temp.Value-dew.Value;return gap<=2?"High":gap<=5?"Medium":"Low";}
    public static Color ColorFor(ConditionLevel level){if(level==ConditionLevel.Unknown)return Style.Text;if(Style.Theme=="Night")return level==ConditionLevel.Good?Color.FromArgb(225,54,54):level==ConditionLevel.Medium?Color.FromArgb(255,72,72):Color.FromArgb(255,100,100);if(Style.Theme=="Light")return level==ConditionLevel.Good?Color.FromArgb(22,107,58):level==ConditionLevel.Medium?Color.FromArgb(126,82,0):Color.FromArgb(174,35,44);return level==ConditionLevel.Good?Color.FromArgb(128,225,161):level==ConditionLevel.Medium?Color.FromArgb(246,203,99):Color.FromArgb(255,143,147);}
    public static StatusRun Text(string text){return new StatusRun{Text=text};}
    public static StatusRun Value(string text,ConditionLevel level){return new StatusRun{Text=text,Level=level};}
    public static StatusRow Row(params StatusRun[] runs){return new StatusRow{Runs=runs};}
    public static Control Card(string heading,params StatusRow[] rows){var box=Style.Grid(1,27,-100);box.BackColor=Style.Card;box.Padding=new Padding(9,4,9,5);box.Margin=new Padding(3);box.Controls.Add(Style.Label(heading,10.5f,true),0,0);box.Controls.Add(new StatusText(rows,10){Dock=DockStyle.Fill},0,1);return box;}
}
class StatusRun {public string Text;public ConditionLevel Level=ConditionLevel.Unknown;}
class StatusRow {public StatusRun[] Runs;}
class StatusText:Control {
    public StatusRow[] Rows {get;private set;}
    public StatusText(StatusRow[] rows,float size){Rows=rows;Font=new Font("Segoe UI",size);AccessibleName=string.Join("; ",rows.Select(row=>string.Concat(row.Runs.Select(run=>run.Text))));SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Style.Card);int y=Padding.Top,line=(int)Math.Ceiling(Font.GetHeight(e.Graphics))+2;using(var bold=new Font(Font,FontStyle.Bold)){foreach(var row in Rows){int x=Padding.Left;foreach(var run in row.Runs){var font=run.Level==ConditionLevel.Unknown?Font:bold;var flags=TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine;int width=TextRenderer.MeasureText(e.Graphics,run.Text,font,new Size(int.MaxValue,line),flags).Width;TextRenderer.DrawText(e.Graphics,run.Text,font,new Rectangle(x,y,width,line),Conditions.ColorFor(run.Level),flags);x+=width;}y+=line;}}}
}
}

