using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace Qingling {
public sealed class GlobalMouse:IDisposable {
 delegate IntPtr Hook(int code,IntPtr wp,IntPtr lp);readonly Hook callback;IntPtr handle;
 [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,Hook proc,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h,int n,IntPtr w,IntPtr l);
 [DllImport("kernel32.dll",CharSet=CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
 public bool Installed {get{return handle!=IntPtr.Zero;}}
 public GlobalMouse(Action<int> pressed){callback=(n,w,l)=>{if(n>=0&&(w.ToInt32()==0x201||w.ToInt32()==0x204)){try{pressed(Marshal.ReadInt32(l,16));}catch{}}return CallNextHookEx(handle,n,w,l);};handle=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);}
 public void Dispose(){if(handle!=IntPtr.Zero){UnhookWindowsHookEx(handle);handle=IntPtr.Zero;}}
}
public sealed class ScreenArea {
 public Rect Bounds,Work;public string Name;
 public static ScreenArea Read(Window w,string preferred){
  var screen=Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==preferred);
  if(screen==null){Native.POINT p;Native.GetCursorPos(out p);screen=Forms.Screen.FromPoint(new System.Drawing.Point(p.X,p.Y));}
  var source=PresentationSource.FromVisual(w);var matrix=source==null?Matrix.Identity:source.CompositionTarget.TransformFromDevice;
  var b=screen.Bounds;var a=screen.WorkingArea;
  return new ScreenArea{Name=screen.DeviceName,Bounds=new Rect(matrix.Transform(new Point(b.Left,b.Top)),matrix.Transform(new Point(b.Right,b.Bottom))),Work=new Rect(matrix.Transform(new Point(a.Left,a.Top)),matrix.Transform(new Point(a.Right,a.Bottom)))};
 }
}
public static class Docking {
 public static int Detect(Rect pet,ScreenArea screen){
  double[] d={Math.Max(0,pet.Left-screen.Bounds.Left),Math.Max(0,screen.Bounds.Right-pet.Right),Math.Max(0,pet.Top-screen.Bounds.Top),Math.Max(0,screen.Work.Bottom-pet.Bottom)};
  int best=Array.IndexOf(d,d.Min());return d[best]<=22?best+13:-1;
 }
 public static Point Target(int row,Rect pet,ScreenArea s){
  double x=Math.Max(s.Bounds.Left,Math.Min(pet.Left,s.Bounds.Right-pet.Width));double y=Math.Max(s.Bounds.Top,Math.Min(pet.Top,s.Work.Bottom-pet.Height));
  if(row==13)x=s.Bounds.Left-pet.Width*76/192;
  if(row==14)x=s.Bounds.Right-pet.Width*116/192;
  if(row==15)y=s.Bounds.Top-pet.Height*66/208;
  if(row==16){double bar=s.Bounds.Bottom-s.Work.Bottom;double seat=bar>=16?s.Work.Bottom:s.Bounds.Bottom-Math.Min(38,pet.Height*.22);y=seat-pet.Height*.74;}
  return new Point(x,y);
 }
 public static Rect Clip(int row,double width,double height){if(row==13)return new Rect(width*76/192,0,width*116/192,height);if(row==14)return new Rect(0,0,width*116/192,height);if(row==15)return new Rect(0,height*66/208,width,height*142/208);return new Rect(0,0,width,height);}
 public static double Ease(double t){t=Math.Max(0,Math.Min(1,t));return 1-Math.Pow(1-t,3);}
}
public sealed class GreetingClock {
 public double Next=1800;public double Interval=1800;
 public bool Due(double now,bool busy){if(busy||now<Next)return false;Next=now+Interval;return true;}
 public void Postpone(double now){Next=now+Interval;}
}
public static class CompanionPolicy {
 public static double GreetingInterval(int level){return level==2?1200:1800;}
 public static double RandomInterval(double configured,int level){return level==2?Math.Max(5,configured*.6):configured;}
 public static bool Proactive(int level,bool fullscreen){return level>0&&!fullscreen;}
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out WinRect rect);
 [StructLayout(LayoutKind.Sequential)] struct WinRect {public int Left,Top,Right,Bottom;}
 public static bool Fullscreen(){IntPtr hwnd=GetForegroundWindow();WinRect r;if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out r))return false;var bounds=Forms.Screen.FromHandle(hwnd).Bounds;return r.Left<=bounds.Left+2&&r.Top<=bounds.Top+2&&r.Right>=bounds.Right-2&&r.Bottom>=bounds.Bottom-2;}
}
public static class BubblePlacement {
 public static bool NextBeside(bool beside,double petTop,double screenTop,double bubbleHeight){
  bool enough=petTop-screenTop>=bubbleHeight+24;
  if(beside)return !(enough&&petTop-screenTop>=bubbleHeight+52);
  return !enough;
 }
}
public sealed class GreetingService {
 readonly DesktopApp app;readonly Stopwatch clock=Stopwatch.StartNew();readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
 readonly GreetingClock schedule=new GreetingClock();CancellationTokenSource current;int count;public string Last="";public bool Running{get{return current!=null;}}
 public GreetingService(DesktopApp a){app=a;var previous=a.Memory.Turns.LastOrDefault(t=>t.Status=="greeting");if(previous!=null)Last=previous.Answer;timer.Tick+=(s,e)=>Tick(clock.Elapsed.TotalSeconds);}
 public void Start(){timer.Start();}public void Stop(){timer.Stop();Cancel();}public void Cancel(){if(current!=null)current.Cancel();}
 public void Tick(double now){if(app.Quitting||!app.Pet.IsVisible)return;schedule.Interval=CompanionPolicy.GreetingInterval(app.Config.CompanionLevel);if(!CompanionPolicy.Proactive(app.Config.CompanionLevel,app.Config.PauseInFullscreen&&CompanionPolicy.Fullscreen())){schedule.Postpone(now);return;}if(schedule.Due(now,app.Request!=null||app.Bubbles.Busy||Running))Greet();}
 public static string Default(int number){string[] jokes={"叮铃～来看看你。小蛇为什么不穿鞋？因为它想把买鞋的钱，攒起来请你喝奶茶呀。 (｡•ᴗ•｡)","半小时的小铃铛准时响起～今天的冷笑话：云朵去上班为什么迟到？因为它一路都在摸鱼，摸成了鱼鳞云。", "我探出脑袋来打个招呼。铃铛为什么爱听你说话？因为每一句，它都想认真回应一声：叮。 (｡･ω･)ﾉ"};return jokes[number%jokes.Length];}
 public async void Greet(){
  if(app.Quitting||app.Request!=null||Running)return;app.Memories.Cancel();current=new CancellationTokenSource();var request=current;string text=Default(count++);
  try{
   bool personalized=app.Memory.Turns.Any(t=>t.Status=="complete")||app.Memory.State.Pinned.Length>0||app.Config.Persona!=new Settings().Persona||app.Config.Personality!=new Settings().Personality||app.Config.Speech!=new Settings().Speech||app.Config.Knowledge!=new Settings().Knowledge;
   if(personalized&&!string.IsNullOrWhiteSpace(app.Config.Model)){
    var config=app.Config.Copy();config.Stream=false;config.TimeoutSeconds=Math.Min(60,config.TimeoutSeconds);config.SystemOverride=Prompts.Compose(config,config.MemoryEnabled?app.Memory.Context():"");
    text=await app.Api.Complete(config,SettingsStore.Unprotect(config.EncryptedKey),new List<Message>{new Message("user","这是桌宠主动问候事件，不是用户发来的消息。按霖铃当前人设与已提供的真实记忆，写一句自然问候，再接一个温柔可爱的小笑话，合计不超过100字。不假装看到用户正在做什么，不催促回复，不编造共同经历。避免重复上次问候。上次内容（仅供参考）："+Prompts.Cut(Last,300))},null,request.Token);
   }
   request.Token.ThrowIfCancellationRequested();if(app.Request!=null||app.Quitting||app.Config.CompanionLevel==0)return;
   Show(text);
  }catch(OperationCanceledException){}
  catch{if(!request.IsCancellationRequested&&app.Request==null&&!app.Quitting&&app.Config.CompanionLevel>0)Show(text);}
  finally{current=null;request.Dispose();}
 }
 void Show(string text){Last=Prompts.Cut(text,500);app.Bubbles.Greet(Last);app.Chat.ShowGreeting(Last);try{app.Memory.Add("",Last,"greeting");}catch{}if(app.Pet.DockRow<0)app.Pet.Play(3);}
}
}
