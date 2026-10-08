using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Qingling {
public static class MotionData {
 public static readonly int[] Counts={96,18,18,72,36,29,24,96,96,0,0,20,20,29,29,72,62,86};
 public static int Index(int row,int col){return 136+row*96+col;}
 public static int Row(int index){return index>=136?(index-136)/96:index/8;}
}
// Measured using a monotonic clock; background streaming never consumes reading time.
public sealed class ReadingClock {
 double remaining,last;bool armed;
 public void Arm(double now,double seconds){remaining=seconds;last=now;armed=true;}
 public void Stop(){armed=false;}
 public bool Tick(double now,bool paused){double elapsed=Math.Max(0,now-last);last=now;if(!armed)return false;if(!paused)remaining-=elapsed;if(remaining>0)return false;armed=false;return true;}
}
public static class BubbleArrow {
 public static PointCollection Points(Size size,Point target){
  double w=size.Width,h=size.Height,dx=target.X-w/2,dy=target.Y-h/2;
  if(Math.Abs(dx)/w>Math.Abs(dy)/h){double y=Math.Max(32,Math.Min(h-32,target.Y));bool right=dx>0;double x=right?w-12:12;return new PointCollection{new Point(x,y-9),new Point(right?w-1:1,y),new Point(x,y+9)};}
  double cx=Math.Max(32,Math.Min(w-32,target.X));bool bottom=dy>0;double cy=bottom?h-12:12;return new PointCollection{new Point(cx-9,cy),new Point(cx,bottom?h-1:1),new Point(cx+9,cy)};
 }
}
public static class PetZOrder {
 [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h,uint command);
 public static bool Raise(Window w){if(!w.IsVisible||!w.Topmost)return false;IntPtr h=new WindowInteropHelper(w).Handle;if(h==IntPtr.Zero)return false;return SetWindowPos(h,new IntPtr(-1),0,0,0,0,0x0010|0x0001|0x0002|0x0200);}
}
public sealed class BlinkClock {
 readonly Random random=new Random(431);double next=5,start=-100;
 public int Frame(double now,double interval,bool permitted){
  if(now>=next){next=now+interval*(.8+random.NextDouble()*.4);if(permitted)start=now;}
  if(!permitted){start=-100;return -1;}double t=now-start;
  return t<0||t>=.28?-1:t<.07?1:t<.18?2:3;
 }
}
}
