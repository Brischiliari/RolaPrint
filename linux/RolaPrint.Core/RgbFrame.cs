using System;

namespace RolaPrint.Portable {
 public sealed class RgbFrame {
  public int Width { get; private set; }
  public int Height { get; private set; }
  public byte[] Data { get; private set; }
  public RgbFrame(int width,int height,byte[] data) {
   if(width<=0 || height<=0 || data==null || (long)width*height*3!=data.Length)throw new ArgumentException("Invalid RGB frame");
   Width=width;Height=height;Data=data;
  }
  public RgbFrame Crop(int start,int height){
   if(start<0 || height<=0 || start>Height-height)throw new ArgumentOutOfRangeException();
   var data=new byte[checked(Width*height*3)];Buffer.BlockCopy(Data,checked(start*Width*3),data,0,data.Length);return new RgbFrame(Width,height,data);
  }
 }
 internal struct PixelPoint {
  public int X,Y;
  public PixelPoint(int x,int y){X=x;Y=y;}
 }
}
