using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace Qingling {
public static class Native {
 [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X,Y;}
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
}
public sealed class SpriteAtlas {
 readonly BitmapSource[] cells=new BitmapSource[704];
 public SpriteAtlas(){LoadDrag();LoadEdges();LoadMotion();LoadSleep();
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("qingling.png")){
   var sheet=new BitmapImage();sheet.BeginInit();sheet.CacheOption=BitmapCacheOption.OnLoad;sheet.StreamSource=stream;sheet.EndInit();sheet.Freeze();
   if(sheet.PixelWidth!=1536||sheet.PixelHeight!=2288)throw new InvalidDataException("宠物图集尺寸不正确。");
   for(int i=0;i<88;i++){var c=new CroppedBitmap(sheet,new Int32Rect((i%8)*192,(i/8)*208,192,208));c.Freeze();cells[i]=c;}
  }
 }
 void LoadDrag(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("drag.png")){var sheet=new BitmapImage();sheet.BeginInit();sheet.CacheOption=BitmapCacheOption.OnLoad;sheet.StreamSource=stream;sheet.EndInit();sheet.Freeze();for(int r=0;r<2;r++)for(int c=0;c<4;c++){var cell=new CroppedBitmap(sheet,new Int32Rect(c*192,r*208,192,208));cell.Freeze();cells[(11+r)*8+c]=cell;}}}
 void LoadEdges(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("edges.png")){var sheet=new BitmapImage();sheet.BeginInit();sheet.CacheOption=BitmapCacheOption.OnLoad;sheet.StreamSource=stream;sheet.EndInit();sheet.Freeze();for(int r=0;r<4;r++)for(int c=0;c<4;c++){var cell=new CroppedBitmap(sheet,new Int32Rect(c*192,r*208,192,208));cell.Freeze();cells[(13+r)*8+c]=cell;}}}
 readonly Dictionary<int,byte[]> pixels=new Dictionary<int,byte[]>();
 void LoadMotion(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("motion.png")){var sheet=new BitmapImage();sheet.BeginInit();sheet.CacheOption=BitmapCacheOption.OnLoad;sheet.StreamSource=stream;sheet.EndInit();sheet.Freeze();int ordinal=0;for(int r=0;r<17;r++)for(int c=0;c<MotionData.Counts[r];c++){var cell=new CroppedBitmap(sheet,new Int32Rect(ordinal%8*192,ordinal/8*208,192,208));ordinal++;cell.Freeze();cells[MotionData.Index(r,c)]=cell;}}}
 void LoadSleep(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("sleep.png")){if(stream==null)throw new InvalidDataException("睡觉图集缺失。");var sheet=new BitmapImage();sheet.BeginInit();sheet.CacheOption=BitmapCacheOption.OnLoad;sheet.StreamSource=stream;sheet.EndInit();sheet.Freeze();if(sheet.PixelWidth!=1536||sheet.PixelHeight!=624)throw new InvalidDataException("睡觉图集尺寸不正确。");for(int c=0;c<24;c++){var cell=new CroppedBitmap(sheet,new Int32Rect(c%8*192,c/8*208,192,208));cell.Freeze();cells[MotionData.Index(17,c)]=cell;}}}
 byte[] Pixels(int i){byte[] data;if(!pixels.TryGetValue(i,out data)){var small=new TransformedBitmap(cells[i],new ScaleTransform(.25,.25));var format=new FormatConvertedBitmap(small,PixelFormats.Bgra32,null,0);data=new byte[48*52*4];format.CopyPixels(data,48*4,0);pixels[i]=data;}return data;}
 public int Closest(int row,int previous){if(previous<0||cells[previous]==null)return 0;var before=Pixels(previous);long best=long.MaxValue;int selected=0;for(int c=0;c<AnimationEngine.FrameCount(row);c++){var after=Pixels(MotionData.Index(row,c));long cost=0;for(int n=0;n<before.Length;n+=4){cost+=Math.Abs(before[n+3]-after[n+3])*3;if(before[n+3]>128&&after[n+3]>128)cost+=Math.Abs(before[n]-after[n])+Math.Abs(before[n+1]-after[n+1])+Math.Abs(before[n+2]-after[n+2]);}if(cost<best){best=cost;selected=c;}}return selected;}
 public int ClosestDrag(int row,int previous){if(previous<0||cells[previous]==null)return 0;var before=Pixels(previous);long best=long.MaxValue;int selected=0,start=row==1?8:16;for(int c=0;c<8;c++){var after=Pixels(start+c);long cost=0;for(int n=0;n<before.Length;n+=4){cost+=Math.Abs(before[n+3]-after[n+3])*3;if(before[n+3]>128&&after[n+3]>128)cost+=Math.Abs(before[n]-after[n])+Math.Abs(before[n+1]-after[n+1])+Math.Abs(before[n+2]-after[n+2]);}if(cost<best){best=cost;selected=c;}}return selected;}
 public BitmapSource this[int index]{get{return cells[index];}}
}
public sealed class DesktopApp : Application {
 public Settings Config;public SettingsStore Store;public SpriteAtlas Atlas;public PetWindow Pet;public ChatWindow Chat;
 public MemoryBook Memory;public MemoryWorker Memories;public CompanionBubbles Bubbles;public GreetingService Greetings;
 public readonly ChatApi Api;public readonly List<Message> History=new List<Message>();
 public CancellationTokenSource Request;public bool Quitting;public bool Smoke;
 Forms.NotifyIcon tray;SettingsWindow settingsWindow;
 public bool TrayReady {get{return tray!=null&&tray.Visible;}}
 public DesktopApp(string dataRoot,bool smoke){Smoke=smoke;Api=smoke?UpgradeTests.MockApi():new ChatApi();Store=new SettingsStore(dataRoot);Config=Store.Load();Config.Persona=Config.Persona.Replace("青铃","霖铃");Memory=new MemoryBook(dataRoot);Memories=new MemoryWorker(this);History.AddRange(Memory.Recent());Atlas=new SpriteAtlas();ShutdownMode=ShutdownMode.OnExplicitShutdown;}
 public void Start(bool quiet){
  Pet=new PetWindow(this);MainWindow=Pet;Pet.Show();Chat=new ChatWindow(this);Bubbles=new CompanionBubbles(this);Greetings=new GreetingService(this);Greetings.Start();
  {tray=new Forms.NotifyIcon();using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("qingling.ico"))tray.Icon=new System.Drawing.Icon(s);tray.Text="霖铃 · 双击聊天 / 右键菜单";
   var menu=new Forms.ContextMenuStrip();
   menu.Items.Add("和霖铃聊天",null,(s,e)=>Dispatcher.Invoke((Action)OpenChat));
   menu.Items.Add("对话记录",null,(s,e)=>Dispatcher.Invoke((Action)OpenHistory));
   menu.Items.Add("设置",null,(s,e)=>Dispatcher.Invoke((Action)OpenSettings));
   menu.Items.Add("显示 / 隐藏霖铃",null,(s,e)=>Dispatcher.Invoke((Action)(()=>{if(Pet.IsVisible)Pet.Hide();else{Pet.Show();Pet.EnsureVisible();}})));
   menu.Items.Add("移回屏幕",null,(s,e)=>Dispatcher.Invoke((Action)(()=>{Pet.Show();Pet.ResetPosition();})));
   menu.Items.Add(new Forms.ToolStripSeparator());
   menu.Items.Add("退出",null,(s,e)=>Dispatcher.Invoke((Action)Quit));
   tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>Dispatcher.Invoke((Action)OpenChat);tray.Visible=true;
  }
  if(!quiet||!string.IsNullOrWhiteSpace(Config.Model))Bubbles.Show(false);
 }
 public void OpenChat(){if(Bubbles!=null)Bubbles.Show();}
 public void OpenHistory(){if(Chat==null)Chat=new ChatWindow(this);Chat.Show();if(Chat.WindowState==WindowState.Minimized)Chat.WindowState=WindowState.Normal;Chat.Activate();}
 public void OpenSettings(){if(settingsWindow==null){settingsWindow=new SettingsWindow(this);settingsWindow.Closed+=(s,e)=>settingsWindow=null;}settingsWindow.Show();settingsWindow.Activate();}
 public void SaveConfig(Settings candidate,bool startup){
  candidate.Validate();string old=null;
  if(!Smoke){old=Qingling.Startup.Enabled?"yes":"no";Qingling.Startup.Set(startup);}
  try{Store.Save(candidate);}catch{if(old!=null)Qingling.Startup.Set(old=="yes");throw;}
  Config=candidate;if(!Config.MemoryEnabled)Memories.Cancel();if(Config.CompanionLevel==0)Greetings.Cancel();Pet.ApplySettings();if(Chat!=null)Chat.RefreshStatus();if(Bubbles!=null)Bubbles.Show(false);
 }
 public void Quit(){if(Quitting)return;Quitting=true;Greetings.Stop();Memories.Cancel();if(Bubbles!=null)Bubbles.Close();if(Request!=null)Request.Cancel();Pet.Stop();if(tray!=null){tray.Visible=false;tray.Dispose();}Api.Dispose();Shutdown();}
 protected override void OnExit(ExitEventArgs e){if(tray!=null)tray.Dispose();base.OnExit(e);}
}
public sealed class PetWindow : Window {
 readonly DesktopApp app;readonly Image sprite=new Image();readonly DispatcherTimer tick=new DispatcherTimer(),click=new DispatcherTimer();
 readonly Stopwatch clock=Stopwatch.StartNew();readonly Random random=new Random();public readonly AnimationEngine Engine=new AnimationEngine();
 readonly GazeTracker gaze=new GazeTracker();readonly TranslateTransform motion=new TranslateTransform();GlobalMouse global;
 readonly BlinkClock blink=new BlinkClock();double nextRaise;IntPtr lastForeground;
 Point down,origin,lastDrag,dockFrom,dockTarget;bool pressed,dragged,docking;int frame=-1;Native.POINT lastCursor;int dismissStamp=-1;bool suppressContext;double lastMove=-100,nextRandom=20,dockBegan,lastTick,smoothX,smoothY;HwndSource source;
 public int DockRow {get{return Engine.DockRow;}}
 public int ActiveRow {get{return Engine.CurrentRow(clock.Elapsed.TotalSeconds);}}
 public string CurrentScreenName {get{return app.Config.DockScreen;}}
 public bool GlobalHookReady {get{return global!=null&&global.Installed;}}
 public PetWindow(DesktopApp a){app=a;Title="霖铃桌宠";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;
  sprite.Stretch=Stretch.Fill;sprite.RenderTransform=motion;RenderOptions.SetBitmapScalingMode(sprite,BitmapScalingMode.NearestNeighbor);Content=sprite;Engine.ChooseStart=app.Atlas.Closest;Engine.ChooseDragStart=app.Atlas.ClosestDrag;
  ContextMenu=BuildMenu();PreviewMouseDown+=(s,e)=>{if(Engine.PreviewRow>=0)CancelPreviewClick(e.Timestamp);suppressContext=false;if(e.Timestamp==dismissStamp){suppressContext=e.ChangedButton==MouseButton.Right;e.Handled=true;return;}if(e.ChangedButton==MouseButton.Right)click.Stop();};ContextMenuOpening+=(s,e)=>{if(suppressContext){e.Handled=true;suppressContext=false;}};MouseLeftButtonDown+=Down;MouseMove+=Move;MouseLeftButtonUp+=Up;
  LostMouseCapture+=(s,e)=>{pressed=false;Engine.DragRow=-1;};
  click.Interval=TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime);click.Tick+=(s,e)=>{click.Stop();Play(3);};
  tick.Interval=TimeSpan.FromMilliseconds(16);tick.Tick+=(s,e)=>Update();
  SourceInitialized+=(s,e)=>{source=(HwndSource)PresentationSource.FromVisual(this);source.AddHook(HitTest);global=new GlobalMouse(stamp=>{if(Engine.PreviewRow>=0)CancelPreviewClick(stamp);nextRaise=0;});};
  Loaded+=(s,e)=>{ApplySettings();if(double.IsNaN(a.Config.X)||double.IsNaN(a.Config.Y))ResetPosition();else{Left=a.Config.X;Top=a.Config.Y;if(a.Config.DockSide>=13&&a.Config.DockSide<=16)Dock(a.Config.DockSide,false);else EnsureVisible();}tick.Start();if(DockRow<0)Play(3);};
  Closing+=(s,e)=>{if(!app.Quitting){e.Cancel=true;Hide();}};
 }
 ContextMenu BuildMenu(){var m=new ContextMenu();Add(m,"和霖铃聊天",app.OpenChat);Add(m,"显示输入框",()=>app.Bubbles.ShowInput());Add(m,"隐藏输入框",()=>app.Bubbles.HideInput());Add(m,"对话记录",app.OpenHistory);Add(m,"设置",app.OpenSettings);m.Items.Add(new Separator());Add(m,"预览 · 挥手",()=>Preview(3));Add(m,"预览 · 跳跃",()=>Preview(4));Add(m,"预览 · 思考",()=>Preview(7));Add(m,"预览 · 观察",()=>Preview(8));Add(m,"预览 · 睡觉（点击唤醒）",()=>Preview(17));Add(m,"预览 · 攀爬",()=>Preview(11));Add(m,"预览 · 坠落",()=>Preview(12));m.Items.Add(new Separator());Add(m,"移回屏幕",ResetPosition);Add(m,"隐藏到托盘",()=>Hide());Add(m,"退出",app.Quit);return m;}
 static void Add(ContextMenu m,string title,Action action){var i=new MenuItem{Header=title};i.Click+=(s,e)=>action();m.Items.Add(i);}
 public void ApplySettings(){Width=app.Config.Size;Height=Width*208/192;Topmost=app.Config.Topmost;nextRandom=clock.Elapsed.TotalSeconds+CompanionPolicy.RandomInterval(app.Config.RandomMin,app.Config.CompanionLevel);if(IsLoaded){if(DockRow>=13)Dock(DockRow,false);else EnsureVisible();}}
 public void ResetPosition(){Engine.DockRow=-1;app.Config.DockSide=-1;docking=false;sprite.Clip=null;Width=app.Config.Size;Height=Width*208/192;var area=ScreenArea.Read(this,"");Rect r=area.Work;Left=r.Right-Width-32;Top=r.Bottom-Height-32;app.Config.DockScreen=area.Name;SavePosition();}
 public void EnsureVisible(){if(DockRow>=13){Dock(DockRow,false);return;}bool visible=false;Rect pet=new Rect(Left,Top,Width,Height);foreach(var screen in Forms.Screen.AllScreens){var area=ScreenArea.Read(this,screen.DeviceName);var inter=Rect.Intersect(pet,area.Work);if(!inter.IsEmpty&&inter.Width>=Math.Min(Width,40)&&inter.Height>=Math.Min(Height,40)){visible=true;app.Config.DockScreen=area.Name;break;}}if(!visible)ResetPosition();}
 void SavePosition(){app.Config.X=Left;app.Config.Y=Top;app.Config.DockSide=DockRow;if(app.Smoke)return;try{app.Store.Save(app.Config);}catch{}}
 public void Preview(int row){click.Stop();Engine.ClearTransient();Engine.PreviewRow=row;}
 public void CancelPreviewClick(){CancelPreviewClick(Environment.TickCount);} public void CancelPreviewClick(int stamp){Engine.PreviewRow=-1;Engine.ClearTransient();click.Stop();pressed=false;Engine.DragRow=-1;dismissStamp=stamp;if(IsMouseCaptured)ReleaseMouseCapture();}
 public void Play(int row){if(Engine.PreviewRow<0)Engine.Play(row,clock.Elapsed.TotalSeconds,app.Config.ActionSeconds);}
 public void Busy(bool busy){Engine.Busy=busy;if(busy)Engine.ClearTransient();}
 public void Stop(){tick.Stop();click.Stop();if(global!=null)global.Dispose();if(source!=null)source.RemoveHook(HitTest);}
 public void Dock(int row,bool animate){var area=ScreenArea.Read(this,app.Config.DockScreen);Engine.DockRow=row;Engine.ClearTransient();app.Config.DockSide=row;app.Config.DockScreen=area.Name;
  Width=app.Config.Size;Height=Width*208/192;if(row==16){double bar=area.Bounds.Bottom-area.Work.Bottom;if(bar>=16&&Height*.235>bar){Height=bar/.235;Width=Height*192/208;}}
  sprite.Clip=new RectangleGeometry(Docking.Clip(row,Width,Height));dockFrom=new Point(Left,Top);dockTarget=Docking.Target(row,new Rect(Left,Top,Width,Height),area);dockBegan=clock.Elapsed.TotalSeconds;docking=animate;
  if(!animate){Left=dockTarget.X;Top=dockTarget.Y;SavePosition();}
 }
 void Undock(){docking=false;Engine.DockRow=-1;app.Config.DockSide=-1;sprite.Clip=null;Width=app.Config.Size;Height=Width*208/192;}
 void Down(object sender,MouseButtonEventArgs e){if(e.ChangedButton!=MouseButton.Left)return;if(e.Timestamp==dismissStamp){e.Handled=true;return;}bool twice=click.IsEnabled;click.Stop();if(twice){Play(4);e.Handled=true;return;}
  Native.POINT p;Native.GetCursorPos(out p);down=new Point(p.X,p.Y);lastDrag=down;origin=new Point(Left,Top);pressed=true;dragged=false;CaptureMouse();e.Handled=true;}
 void Move(object sender,MouseEventArgs e){if(!pressed)return;Native.POINT p;Native.GetCursorPos(out p);Vector d=new Point(p.X,p.Y)-down;var matrix=PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice;d=matrix.Transform(d);
  if(!dragged&&Math.Abs(d.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(d.Y)<SystemParameters.MinimumVerticalDragDistance)return;
  if(!dragged)Undock();dragged=true;click.Stop();Vector step=new Point(p.X,p.Y)-lastDrag;if(step.Length>2){Engine.DragRow=AnimationEngine.Drag(step.X,step.Y);lastDrag=new Point(p.X,p.Y);}Left=origin.X+d.X;Top=origin.Y+d.Y;app.Config.DockScreen=ScreenArea.Read(this,"").Name;}
 void Up(object sender,MouseButtonEventArgs e){if(!pressed)return;bool wasDrag=dragged;pressed=false;Engine.DragRow=-1;ReleaseMouseCapture();if(wasDrag){var area=ScreenArea.Read(this,app.Config.DockScreen);int row=Docking.Detect(new Rect(Left,Top,Width,Height),area);if(row>=13)Dock(row,true);else{EnsureVisible();SavePosition();}}else click.Start();e.Handled=true;}
 void Update(){if(app.Bubbles!=null)app.Bubbles.Follow();if(!IsVisible)return;double now=clock.Elapsed.TotalSeconds,dt=Math.Min(.1,Math.Max(0,now-lastTick));lastTick=now;
  IntPtr foreground=PetZOrder.GetForegroundWindow();if(now>=nextRaise||foreground!=lastForeground){nextRaise=now+.25;lastForeground=foreground;if(ContextMenu==null||!ContextMenu.IsOpen)PetZOrder.Raise(this);}
  if(docking&&!pressed){double t=(now-dockBegan)/.22,f=Docking.Ease(t);Left=dockFrom.X+(dockTarget.X-dockFrom.X)*f;Top=dockFrom.Y+(dockTarget.Y-dockFrom.Y)*f;if(t>=1){docking=false;SavePosition();}}
  Native.POINT p;Native.GetCursorPos(out p);if(Math.Abs(p.X-lastCursor.X)+Math.Abs(p.Y-lastCursor.Y)>2){lastMove=now;lastCursor=p;}
  if(now>=nextRandom){nextRandom=now+CompanionPolicy.RandomInterval(app.Config.RandomMin+random.NextDouble()*(app.Config.RandomMax-app.Config.RandomMin),app.Config.CompanionLevel);if(app.Config.RandomActions&&CompanionPolicy.Proactive(app.Config.CompanionLevel,app.Config.PauseInFullscreen&&CompanionPolicy.Fullscreen())&&!Engine.Busy&&!pressed&&Engine.CurrentRow(now)==0&&now-lastMove>2){int[] rows={0,0,7,8,3,17};int selected=rows[random.Next(rows.Length)];Engine.Play(selected,now,selected==17?Math.Max(8,app.Config.ActionSeconds*2):app.Config.ActionSeconds);}}
  int index=Engine.Frame(now,app.Config.Speed),row=Engine.CurrentRow(now);
  if(app.Config.FollowMouse&&!pressed&&row==0&&now-lastMove<1.8){Point local=PointFromScreen(new Point(p.X,p.Y));double dx=local.X-Width/2,dy=local.Y-Height*.38;if(dx*dx+dy*dy<1000000){int look=gaze.Update(dx,dy);if(look>=0)index=look;}}
  int blinkFrame=blink.Frame(now,app.Config.BlinkSeconds,(row==0||row>=13&&row<=15)&&!(index>=72&&index<88));if(blinkFrame>=0)index=row>=13?row*8+(blinkFrame==2?2:1):blinkFrame;
  double phase=now*app.Config.Speed,x=(row==12?2:row==11?1:0)*Math.Sin(phase*10),y=row>=13?0:row==11?2*Math.Sin(phase*12):row==12?2*Math.Sin(phase*8):Math.Sin(phase*2),ease=1-Math.Exp(-dt/.065);smoothX+=(x-smoothX)*ease;smoothY+=(y-smoothY)*ease;motion.X=Math.Round(smoothX);motion.Y=Math.Round(smoothY);
  if(app.Bubbles!=null)app.Bubbles.Animate(row,now);if(index!=frame){frame=index;sprite.Source=app.Atlas[index];}
 }
 IntPtr HitTest(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg!=0x84||pressed||frame<0)return IntPtr.Zero;long n=lp.ToInt64();Point local=PointFromScreen(new Point((short)(n&65535),(short)((n>>16)&65535)));
  if(DockRow>=13&&!Docking.Clip(DockRow,Width,Height).Contains(local)){handled=true;return new IntPtr(-1);}int x=(int)((local.X-motion.X)/ActualWidth*192),y=(int)((local.Y-motion.Y)/ActualHeight*208);if(x<0||x>=192||y<0||y>=208)return IntPtr.Zero;var bitmap=app.Atlas[frame];var px=new byte[4];bitmap.CopyPixels(new Int32Rect(x,y,1,1),px,4,0);if(px[3]<20){handled=true;return new IntPtr(-1);}return IntPtr.Zero;}
}
public static class Entry {
 static Mutex singleton;
 [STAThread] public static int Main(string[] args){
  System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
  if(args.Length>0&&args[0]=="--self-test")return Tests.Run(args.Length>1?args[1]:"test-results.json");
  bool smoke=args.Length>0&&(args[0]=="--ui-smoke"||args[0]=="--feature-smoke"||args[0]=="--comfort-smoke"||args[0]=="--perf-smoke");
  string root=smoke?Path.Combine(Path.GetTempPath(),"QinglingSmoke-"+Guid.NewGuid().ToString("N")):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LinlingDesktop");
  try{
   if(!smoke){bool created;singleton=new Mutex(true,"Local\\LinlingDesktop-v2",out created);if(!created){MessageBox.Show("霖铃已经在运行。请双击系统托盘中的霖铃图标打开聊天。","霖铃");return 0;}}
   if(!smoke&&!File.Exists(Path.Combine(root,"settings.json"))){string old=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QinglingDesktop","settings.json");if(File.Exists(old)){Directory.CreateDirectory(root);File.Copy(old,Path.Combine(root,"settings.json"));}}
   var app=new DesktopApp(root,smoke);app.DispatcherUnhandledException+=(s,e)=>{e.Handled=true;MessageBox.Show("操作未能完成："+e.Exception.Message,"霖铃",MessageBoxButton.OK,MessageBoxImage.Warning);};
   app.Start(Array.IndexOf(args,"--quiet")>=0);
   if(smoke){if(args[0]=="--perf-smoke")ComfortTests.Performance(app,args.Length>1?args[1]:".");else if(args[0]=="--comfort-smoke")ComfortTests.Ui(app,args.Length>1?args[1]:".");else if(args[0]=="--feature-smoke")FeatureTests.Ui(app,args.Length>1?args[1]:".");else UpgradeTests.Ui(app,args.Length>1?args[1]:".");}
   app.Run();return 0;
  }catch(Exception e){MessageBox.Show("霖铃无法启动："+e.Message,"霖铃",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
  finally{if(singleton!=null)singleton.Dispose();}
 }
}
}

