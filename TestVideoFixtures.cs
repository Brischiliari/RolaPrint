using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class VideoFixtures {
 public static void Main(){Run();Console.WriteLine("PASS: sparse captions, textured changing video, fixed sidebar, unrelated-frame rejection.");}
 public static void Run(){
  using(var doc=new Bitmap(1200,1600)){
   using(var g=Graphics.FromImage(doc))using(var font=new Font("Arial",12)){
    g.Clear(Color.White);
    g.DrawString("Perfil: exemplo de postagem 937",font,Brushes.Black,420,100);
    g.DrawString("Curtido por usuario e outras pessoas",font,Brushes.Black,420,620);
    g.DrawString("Legenda exclusiva: um dia no parque",font,Brushes.Black,420,650);
    g.DrawString("Ver comentarios da publicacao",font,Brushes.Black,420,680);
   }
   using(var a=CaptureEngine.Crop(doc,0,900))using(var b=CaptureEngine.Crop(doc,100,900)){
    Noise(a,1,180);Noise(b,2,80);
    Console.WriteLine("Sparse fixture: old matcher="+CaptureEngine.Match(a,b)+", consensus="+CaptureEngine.Consensus(a,b,false));
    var watch=System.Diagnostics.Stopwatch.StartNew();
    int actual=CaptureEngine.Anchors(a,b);
    watch.Stop();Console.WriteLine("Local anchors: "+actual+" px in "+watch.ElapsedMilliseconds+" ms");
    if(actual!=100)throw new Exception("Sparse text + textured video: expected 100, got "+actual);
    using(var unrelated=new Bitmap(1200,900)){Noise(unrelated,3,0);if(CaptureEngine.Anchors(a,unrelated)!=-1)throw new Exception("Unrelated video accepted");}
   }
  }
 }
 static void Noise(Bitmap image,int seed,int y){
  var random=new Random(seed);using(var g=Graphics.FromImage(image)){
   for(int yy=y;yy<Math.Min(image.Height,y+400);yy+=4)for(int x=420;x<820;x+=4)using(var brush=new SolidBrush(Color.FromArgb(random.Next(256),random.Next(256),random.Next(256))))g.FillRectangle(brush,x,yy,4,4);
   g.FillRectangle(Brushes.LightGray,0,0,180,image.Height);
   using(var font=new Font("Arial",14))g.DrawString("Inicio\n\nPesquisar\n\nExplorar\n\nReels\n\nMensagens",font,Brushes.Black,15,80);
  }
 }
}
