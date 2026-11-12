using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
namespace NightLens {
static class ImageServiceTests {
 public static void Run(string[] args,List<string> log){var oldFolder=ObjectImages.CacheFolder;var previous=ObjectImages.Renderer;try{ObjectImages.CacheFolder=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[4])),"survey-regressions",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(ObjectImages.CacheFolder);int calls=0;ObjectImages.Renderer=t=>{Interlocked.Increment(ref calls);return new SurveyPicture(new Bitmap(512,512),"Test color survey");};var target=new Target("Test field","Galaxy",12,30,10,8);string file=ObjectImages.Get(target,true).Result;if(file==null||!File.Exists(file)||calls!=1)throw new Exception("Color reference cache failed");log.Add("PASS: New color renderer saves a complete cached picture");if(ObjectImages.Get(target,true).Result!=file||calls!=1)throw new Exception("Cached image was refetched");File.WriteAllText(file,"broken");if(ObjectImages.Get(target,true,true).Result==null||calls!=2)throw new Exception("Corrupt image not repaired");log.Add("PASS: Complete images are reused; corrupt cache is repaired");var other=new Target("Unavailable field","Galaxy",11,20,10,8);ObjectImages.Renderer=t=>{throw new IOException("Survey unavailable. Click to retry.");};if(ObjectImages.Get(other,true).Result!=null||!ObjectImages.ErrorFor(other).Contains("Survey unavailable"))throw new Exception("Missing image error was hidden");log.Add("PASS: Image failure is visible and retryable");}finally{ObjectImages.CacheFolder=oldFolder;ObjectImages.Renderer=previous;}}
}
}
