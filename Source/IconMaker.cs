using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
class IconMaker {
 static void Main(string[] args){Directory.CreateDirectory(args[0]);int[] sizes={16,24,32,48,64,128,256};var images=new byte[sizes.Length][];for(int i=0;i<sizes.Length;i++){int size=sizes[i];using(var b=new Bitmap(size,size)){using(var g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/256f,size/256f);g.Clear(Color.Transparent);using(var bg=new SolidBrush(Color.FromArgb(27,51,81)))g.FillEllipse(bg,8,8,240,240);using(var pen=new Pen(Color.FromArgb(135,174,208),11))g.DrawEllipse(pen,33,33,190,190);using(var moon=new SolidBrush(Color.FromArgb(250,200,63)))g.FillEllipse(moon,67,55,130,139);using(var bg=new SolidBrush(Color.FromArgb(27,51,81)))g.FillEllipse(bg,101,43,119,129);using(var star=new SolidBrush(Color.FromArgb(239,246,252))){g.FillPolygon(star,new[]{new PointF(161,159),new PointF(169,179),new PointF(190,187),new PointF(169,195),new PointF(161,216),new PointF(153,195),new PointF(132,187),new PointF(153,179)});g.FillEllipse(star,58,145,12,12);g.FillEllipse(star,188,90,10,10);}}using(var stream=new MemoryStream()){b.Save(stream,ImageFormat.Png);images[i]=stream.ToArray();}if(size==256)b.Save(Path.Combine(args[0],"NightLens.png"),ImageFormat.Png);}}
 using(var file=File.Create(Path.Combine(args[0],"NightLens.ico")))using(var writer=new BinaryWriter(file)){writer.Write((short)0);writer.Write((short)1);writer.Write((short)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((short)1);writer.Write((short)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}foreach(var data in images)writer.Write(data);}
 }
}

