using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Windows.Forms;
public class CaptureControls : Form {
 protected override bool ShowWithoutActivation { get {return true;} }
 protected override CreateParams CreateParams {get {var p=base.CreateParams;p.ExStyle|=0x08000000;return p;}}
}
public static class CaptureEngine {
 public sealed class MotionMask {
  internal bool[,] Tiles;public double Fraction;
  public bool Contains(int x,int y){return Tiles[x/32,y/32];}
 }
 public static MotionMask DetectMotion(Bitmap a,Bitmap b){
  if(a.Size!=b.Size)throw new ArgumentException("Image sizes differ");
  byte[] p=Pixels(a),q=Pixels(b);int w=a.Width,h=a.Height,cols=(w+31)/32,rows=(h+31)/32,total=0;
  var mask=new MotionMask {Tiles=new bool[cols,rows]};
  for(int ty=0;ty<rows;ty++)for(int tx=0;tx<cols;tx++){
   int changed=0,count=0;
   for(int y=ty*32;y<Math.Min(h,(ty+1)*32);y+=3)for(int x=tx*32;x<Math.Min(w,(tx+1)*32);x+=3){int i=(y*w+x)*3,d=0;for(int c=0;c<3;c++)d+=Math.Abs(p[i+c]-q[i+c]);if(d>12)changed++;count++;}
   if(changed>count*.12){mask.Tiles[tx,ty]=true;total++;}
  }
  // Include a small border around motion: antialiasing and video edges can
  // otherwise remain in the samples and contaminate the scroll estimate.
  var expanded=(bool[,])mask.Tiles.Clone();
  for(int ty=0;ty<rows;ty++)for(int tx=0;tx<cols;tx++)if(mask.Tiles[tx,ty]){
   for(int yy=Math.Max(0,ty-1);yy<=Math.Min(rows-1,ty+1);yy++)for(int xx=Math.Max(0,tx-1);xx<=Math.Min(cols-1,tx+1);xx++)expanded[xx,yy]=true;
  }
  mask.Tiles=expanded;total=0;foreach(bool value in expanded)if(value)total++;
  mask.Fraction=(double)total/(cols*rows);return mask;
 }
 static bool Masked(MotionMask a,MotionMask b,int x,int y,int shift){return (a!=null&&a.Contains(x,y+shift))||(b!=null&&b.Contains(x,y));}
 public static volatile bool StopRequested=false;
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 public static IntPtr Target(Rectangle r) { return GetAncestor(WindowFromPoint(new Point(r.X+r.Width/2,r.Y+r.Height/2)),2); }
 public static bool Activate(IntPtr h) { return SetForegroundWindow(h); }
 public static bool IsTarget(IntPtr h,Rectangle r) { return GetForegroundWindow()==h && Target(r)==h; }
 public static bool Cancelled() { return StopRequested || (GetAsyncKeyState(27)&0x8000)!=0; }
 public static bool Wait(int ms) { for(int i=0;i<ms;i+=25){if(Cancelled())return false;System.Threading.Thread.Sleep(Math.Min(25,ms-i));}return !Cancelled(); }
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
 [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
 public static Bitmap Grab(Rectangle r) { var b=new Bitmap(r.Width,r.Height,PixelFormat.Format24bppRgb);try {using(var g=Graphics.FromImage(b))g.CopyFromScreen(r.Location,Point.Empty,r.Size);return b;}catch{b.Dispose();throw;} }
 public static void Scroll(int ticks) { mouse_event(0x0800,0,0,unchecked((uint)(-120*ticks)),UIntPtr.Zero); }
 public static double Difference(Bitmap a, Bitmap b,int shift) {
  if(a.Size!=b.Size)throw new ArgumentException("Image sizes differ");
  byte[] p=Pixels(a),q=Pixels(b);int w=a.Width;
  double sum=0; int n=0; int h=a.Height-shift;
  for(int y=8;y<h-8;y+=7) for(int x=8;x<a.Width-8;x+=13){
   int i=((y+shift)*w+x)*3,j=(y*w+x)*3;
   for(int c=0;c<3;c++)sum+=Math.Abs(p[i+c]-q[j+c]);n+=3;
  } return n==0?255:sum/n;
 }
 static byte[] Pixels(Bitmap b) {
  var d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
  try {var p=new byte[b.Width*b.Height*3];for(int y=0;y<b.Height;y++)Marshal.Copy(IntPtr.Add(d.Scan0,y*d.Stride),p,y*b.Width*3,b.Width*3);return p;}finally{b.UnlockBits(d);}
 }
 public static int Match(Bitmap a,Bitmap b,MotionMask oldMotion=null,MotionMask newMotion=null) {
  if(Cancelled())return -2;
  if(a.Size!=b.Size)throw new ArgumentException("Image sizes differ");
  byte[] p=Pixels(a),q=Pixels(b);int w=a.Width,h=a.Height;
  long equal=0;int compared=0;for(int y=0;y<h;y+=3)for(int x=0;x<w;x+=3){if(Masked(oldMotion,newMotion,x,y,0))continue;int i=(y*w+x)*3;for(int c=0;c<3;c++)equal+=Math.Abs(p[i+c]-q[i+c]);compared+=3;}
  if(compared>0 && (double)equal/compared<0.15)return 0;
  double best=255;int shift=-1;double[] scores=new double[h];
  for(int s=1;s<h*3/4;s++) {
   if(s%16==0 && Cancelled())return -2;
   long sum=0;int n=0,detail=0;
   // Match the interior: application chrome and taskbars at the edges are stationary.
   int margin=Math.Max(12,h/8);
   // Bounded sampling keeps alignment practical on high-resolution displays.
   // Every possible vertical offset is still checked at pixel resolution.
   for(int y=margin;y<h-s-margin;y+=Math.Max(3,h/64))for(int x=3;x<w-3;x+=Math.Max(5,w/96)){
    if(Masked(oldMotion,newMotion,x,y,s))continue;
    int i=((y+s)*w+x)*3,j=(y*w+x)*3,edge=0;
    for(int c=0;c<3;c++)edge+=Math.Abs(p[i+c]-p[i-3+c])+Math.Abs(q[j+c]-q[j-3+c]);
    if(edge<18)continue;
    // Exclude detailed pixels that stayed at the same viewport position.
    // Floating buttons and sticky toolbars do not establish scroll distance.
    int fixedDifference=0,stationary=(y*w+x)*3;
    for(int c=0;c<3;c++)fixedDifference+=Math.Abs(p[stationary+c]-q[j+c])+Math.Abs(p[stationary-3+c]-q[j-3+c]);
    if(fixedDifference<3)continue;
    detail++;for(int c=0;c<3;c++){sum+=Math.Abs(p[i+c]-q[j+c]);n++;}
   }
   double score=detail<30?255:(double)sum/n;scores[s]=score;
   if(score<best){best=score;shift=s;}
  }
  // Refine nearby offsets densely: sparse sampling can tie on thin text or lines.
  if(shift>0){
   int candidate=shift;best=255;
   int radius=Math.Max(3,h/64+3);
   for(int s=Math.Max(1,candidate-radius);s<=Math.Min(h*3/4-1,candidate+radius);s++){
    if(Cancelled())return -2;
    long sum=0;int n=0,detail=0,margin=Math.Max(12,h/8);
    for(int y=margin;y<h-s-margin;y+=3)for(int x=3;x<w-3;x+=5){
     if(Masked(oldMotion,newMotion,x,y,s))continue;
     int i=((y+s)*w+x)*3,j=(y*w+x)*3,edge=0,fixedDifference=0;
     for(int c=0;c<3;c++){edge+=Math.Abs(p[i+c]-p[i-3+c])+Math.Abs(q[j+c]-q[j-3+c]);fixedDifference+=Math.Abs(p[j+c]-q[j+c])+Math.Abs(p[j-3+c]-q[j-3+c]);}
     if(edge<18||fixedDifference<3)continue;
     detail++;for(int c=0;c<3;c++){sum+=Math.Abs(p[i+c]-q[j+c]);n++;}
    }
    double score=detail<30?255:(double)sum/n;scores[s]=score;if(score<best){best=score;shift=s;}
   }
  }
  if(best>8 || shift<0)return -1;
  for(int s=1;s<h*3/4;s++)if(Math.Abs(s-shift)>3 && scores[s]<=best+1){
   if(Cancelled())return -2;
   long sum=0;int n=0,detail=0,margin=Math.Max(12,h/8);
   for(int y=margin;y<h-s-margin;y+=3)for(int x=3;x<w-3;x+=5){
    if(Masked(oldMotion,newMotion,x,y,s))continue;
    int i=((y+s)*w+x)*3,j=(y*w+x)*3,edge=0,fixedDifference=0;
    for(int c=0;c<3;c++){edge+=Math.Abs(p[i+c]-p[i-3+c])+Math.Abs(q[j+c]-q[j-3+c]);fixedDifference+=Math.Abs(p[j+c]-q[j+c])+Math.Abs(p[j-3+c]-q[j-3+c]);}
    if(edge<18||fixedDifference<3)continue;
    detail++;for(int c=0;c<3;c++){sum+=Math.Abs(p[i+c]-q[j+c]);n++;}
   }
   if(detail>=30 && (double)sum/n<=best+1)return -1;
  }
  return shift;
 }
 public static int Seam(Bitmap a,Bitmap b,int shift,int minimum,MotionMask oldMotion=null,MotionMask newMotion=null) {
  if(shift<=0 || shift>=a.Height || a.Size!=b.Size)throw new ArgumentException("Invalid overlap");
  byte[] p=Pixels(a),q=Pixels(b);int w=a.Width,overlap=a.Height-shift;
  int from=Math.Max(minimum,Math.Max(2,overlap/5)),to=Math.Min(overlap-3,overlap*4/5);
  if(from>to)throw new InvalidOperationException("Insufficient overlap for a safe seam");
  int best=from;double score=double.MaxValue;
  for(int y=from;y<=to;y++) {
   if(y%16==0 && Cancelled())throw new OperationCanceledException();
   long sum=0;int n=0;
   for(int row=y-2;row<=y+2;row++)for(int x=0;x<w;x+=2){
    int i=((row+shift)*w+x)*3,j=(row*w+x)*3;
    for(int c=0;c<3;c++){sum+=Masked(oldMotion,newMotion,x,row,shift)?255:Math.Abs(p[i+c]-q[j+c]);n++;}
   }
   double value=(double)sum/n;
   if(value<score){score=value;best=y;}
  }
  return best;
 }
 public static Bitmap Crop(Bitmap image,int start,int height) {
  return image.Clone(new Rectangle(0,start,image.Width,height),PixelFormat.Format24bppRgb);
 }
 // Consensus fallback: changing video pixels are outliers, not alignment evidence.
 public static int Anchors(Bitmap a,Bitmap b,MotionMask oldMotion=null,MotionMask newMotion=null) {
  if(a.Size!=b.Size)throw new ArgumentException("Image sizes differ");
  byte[] p=Pixels(a),q=Pixels(b);int w=a.Width,h=a.Height,limit=h/2;
  var points=new System.Collections.Generic.List<Point>();
  for(int y=4;y<h-4;y+=Math.Max(8,h/70))for(int x=4;x<w-4;x+=Math.Max(8,w/90)){
   if(oldMotion!=null&&oldMotion.Contains(x,y))continue;
   int i=(y*w+x)*3,edge=0;for(int c=0;c<3;c++)edge+=Math.Abs(p[i+c]-p[i-6+c])+Math.Abs(p[i+c]-p[i-w*6+c]);
   if(edge>50 && PatchDifference(p,q,w,x,y,y)>2)points.Add(new Point(x,y));
  }
  int stride=Math.Max(1,(points.Count+239)/240);int[] votes=new int[limit];
  var voters=new System.Collections.Generic.List<Point>[limit];
  for(int k=0;k<points.Count;k+=stride){
   if(Cancelled())return -2;Point point=points[k];
   for(int s=1;s<Math.Min(limit,point.Y-3);s++){
    if(newMotion!=null&&newMotion.Contains(point.X,point.Y-s))continue;
    if(PatchDifference(p,q,w,point.X,point.Y,point.Y-s)>2)continue;
    votes[s]++;if(voters[s]==null)voters[s]=new System.Collections.Generic.List<Point>();voters[s].Add(point);
   }
  }
  int best=-1,count=0;for(int s=1;s<limit;s++)if(votes[s]>count){count=votes[s];best=s;}
  if(count<6)return -1;
  for(int s=1;s<limit;s++)if(Math.Abs(s-best)>2 && votes[s]>=count*.75)return -1;
  var rows=new System.Collections.Generic.HashSet<int>();var cols=new System.Collections.Generic.HashSet<int>();
  foreach(Point point in voters[best]){rows.Add(point.Y/16);cols.Add(point.X/32);}
  return rows.Count>=2 && cols.Count>=2?best:-1;
 }
 static double PatchDifference(byte[] p,byte[] q,int w,int x,int oldY,int newY){
  int sum=0;for(int dy=-2;dy<=2;dy+=2)for(int dx=-2;dx<=2;dx+=2){int i=((oldY+dy)*w+x+dx)*3,j=((newY+dy)*w+x+dx)*3;for(int c=0;c<3;c++)sum+=Math.Abs(p[i+c]-q[j+c]);}return sum/27.0;
 }
 public static int Consensus(Bitmap a,Bitmap b,bool returning) {
  if(a.Size!=b.Size)throw new ArgumentException("Image sizes differ");
  byte[] p=Pixels(a),q=Pixels(b);int w=a.Width,h=a.Height;
  int limit=returning?1:h/2,bestShift=-1;double best=0,second=0;
  double[] votes=new double[limit];
  for(int s=returning?0:1;s<limit;s++){
   if(s%8==0&&Cancelled())return -2;
   int good=0,total=0;var rows=new System.Collections.Generic.HashSet<int>();var cols=new System.Collections.Generic.HashSet<int>();
   for(int y=8;y<h-s-8;y+=Math.Max(3,h/80))for(int x=3;x<w-3;x+=Math.Max(5,w/120)){
    int i=((y+s)*w+x)*3,j=(y*w+x)*3,edge=0,diff=0,fixedDiff=0;
    for(int c=0;c<3;c++){edge+=Math.Abs(p[i+c]-p[i-3+c])+Math.Abs(q[j+c]-q[j-3+c]);diff+=Math.Abs(p[i+c]-q[j+c]);fixedDiff+=Math.Abs(p[j+c]-q[j+c]);}
    if(edge<24 || (!returning && fixedDiff<3))continue;
    total++;if(diff<=6){good++;rows.Add(y/16);cols.Add(x/32);}
   }
   double ratio=total<80||good<60||rows.Count<6||cols.Count<3?0:(double)good/total;
   votes[s]=ratio;if(ratio>best){best=ratio;bestShift=s;}
  }
  if(best<.40)return -1;
  for(int s=returning?0:1;s<limit;s++)if(Math.Abs(s-bestShift)>3)second=Math.Max(second,votes[s]);
  if(!returning && best-second<.06)return -1;
  return bestShift;
 }
}
