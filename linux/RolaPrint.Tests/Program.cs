using RolaPrint.Portable;
const int w=220,h=300,shift=60;
var random=new Random(1701);
byte[] document=new byte[w*(h+shift)*3];random.NextBytes(document);
var a=new RgbFrame(w,h,document.Take(w*h*3).ToArray());
var b=new RgbFrame(w,h,document.Skip(w*shift*3).Take(w*h*3).ToArray());
void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine(message);}
Check(Alignment.Match(a,b)==shift,"Exact scroll displacement");
Check(Alignment.Match(a,a)==0,"Stationary viewport");
Check(a.Crop(40,50).Data.SequenceEqual(a.Data.Skip(w*40*3).Take(w*50*3)),"Crop preserves pixels");
byte[] animated=(byte[])a.Data.Clone();
for(int y=60;y<140;y++)for(int x=60;x<140;x++)for(int c=0;c<3;c++)animated[(y*w+x)*3+c]^=255;
var motion=Alignment.DetectMotion(a,new RgbFrame(w,h,animated));
Check(motion.Contains(90,90)&&!motion.Contains(0,299),"Motion mask isolates animation");
Alignment.CancellationRequested=()=>true;
Check(Alignment.Match(a,b)==-2,"Cancellation stops alignment");
