using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Qingling {
public static class FeatureTests {
 static readonly List<string> results=new List<string>();
 static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);results.Add(name);}
 public static void Run(){
  bool beside=false;for(int i=0;i<30;i++){beside=BubblePlacement.NextBeside(beside,120,0,145);Check(beside,"Upper edge bubble chooses side consistently");}for(int i=0;i<30;i++){beside=BubblePlacement.NextBeside(beside,180,0,145);Check(beside,"Hysteresis holds side placement near threshold");}beside=BubblePlacement.NextBeside(beside,250,0,145);Check(!beside,"Bubble returns above only with safe margin");
  var clock=new GreetingClock();Check(!clock.Due(1799,false),"No greeting before 30 minutes");Check(!clock.Due(1800,true),"Busy chat defers due greeting");Check(clock.Due(1801,false)&&!clock.Due(1801,false),"Exactly one greeting after deferred deadline");Check(clock.Due(18000,false)&&!clock.Due(18000,false)&&clock.Next==19800,"Resume after sleep does not replay missed greetings");
  var a=new ScreenArea{Bounds=new Rect(-1920,0,1920,1080),Work=new Rect(-1920,0,1920,1040)};
  Check(Docking.Detect(new Rect(-1910,300,176,190),a)==13,"Left edge detected on negative-coordinate monitor");Check(Docking.Detect(new Rect(-180,300,176,190),a)==14,"Right edge detected");Check(Docking.Detect(new Rect(-900,5,176,190),a)==15,"Top edge detected");Check(Docking.Detect(new Rect(-900,845,176,190),a)==16,"Taskbar sitting edge detected");Check(Docking.Detect(new Rect(-900,300,176,190),a)==-1,"Interior placement does not dock");
  var target=Docking.Target(16,new Rect(-900,845,176,190),a);Check(Math.Abs(target.Y+190*.74-1040)<.01,"Seated hip anchor aligns with taskbar top");Check(Math.Abs(Docking.Clip(13,176,190).Width-176.0*116/192)<.001&&Math.Abs(Docking.Clip(14,176,190).Width-176.0*116/192)<.001,"Anatomical side clipping prevents leaks to adjacent monitor");
  var origin=new Rect(-900,300,176,190);foreach(int row in new[]{13,14,15}){var t=Docking.Target(row,origin,a);var clip=Docking.Clip(row,176,190);Check(row==13?Math.Abs(t.X+clip.Left-a.Bounds.Left)<.001:row==14?Math.Abs(t.X+clip.Right-a.Bounds.Right)<.001:Math.Abs(t.Y+clip.Top-a.Bounds.Top)<.001,"Occlusion plane matches actual screen boundary "+row);}
  var anim=new AnimationEngine();anim.PreviewRow=4;Check(anim.CurrentRow(100000)==4,"Menu preview remains until a click");anim.PreviewRow=-1;anim.ClearTransient();Check(anim.CurrentRow(100000)==0,"Dismiss preview returns to default");anim.DockRow=16;Check(anim.CurrentRow(200000)==16,"Dock animation loops independently of action duration");
  anim=new AnimationEngine();anim.Frame(0,1);anim.Frame(.27,1);Check(anim.Frame(.314,.25)==MotionData.Index(0,6),"Speed adjustment integrates elapsed phase without jumping backward");
  Check(GreetingService.Default(0)!=GreetingService.Default(1),"Default greetings rotate jokes");
 }
 static void Capture(Window w,string path){w.UpdateLayout();var visual=w.Content as FrameworkElement;var bmp=new RenderTargetBitmap((int)Math.Ceiling(w.Width),(int)Math.Ceiling(w.Height),96,96,PixelFormats.Pbgra32);bmp.Render(visual);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))enc.Save(f);}
 static void Click(PetWindow p){p.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});p.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});}
 public static void Ui(DesktopApp app,string path){Directory.CreateDirectory(path);Run();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};int step=0;double pinnedX=0,pinnedY=0;app.Config.RandomActions=false;app.Config.PauseInFullscreen=false;
  timer.Tick+=(s,e)=>{try{
   if(step==0){Check(app.Pet.GlobalHookReady,"Windows low-level mouse hook installed");app.Bubbles.Draft="暂存的输入，不要丢失";app.Bubbles.ToggleInput();Check(app.Config.InputCollapsed&&app.Bubbles.Input.Height==42,"Input collapses independently");Check(app.Bubbles.Reply.IsVisible&&app.Bubbles.Draft.Contains("不要丢失"),"Collapse preserves reply and draft");Capture(app.Bubbles.Input,Path.Combine(path,"input-collapsed.png"));
    var area=ScreenArea.Read(app.Pet,app.Pet.CurrentScreenName);app.Bubbles.PinInput(area.Work.Left+60,area.Work.Top+70,area.Name);pinnedX=app.Bubbles.Input.Left;pinnedY=app.Bubbles.Input.Top;app.Pet.Left+=60;app.Bubbles.Follow();Check(app.Bubbles.Input.Left==pinnedX&&app.Bubbles.Input.Top==pinnedY,"Detached input stays put when pet moves");app.Bubbles.ToggleInput();Check(!app.Config.InputCollapsed&&app.Bubbles.Draft.Contains("暂存"),"Expand restores editable draft");Capture(app.Bubbles.Input,Path.Combine(path,"input-expanded.png"));
    app.Pet.Preview(7);Check(app.Pet.Engine.PreviewRow==7,"Manual animation enters preview state");app.Pet.CancelPreviewClick();Check(app.Pet.Engine.PreviewRow<0,"Preview cancellation resets state");step++;
   }else if(step==1){Click(app.Pet);step++;}
   else if(step==2){Check(app.Pet.ActiveRow==3,"First click after preview starts a fresh single-click gesture");Click(app.Pet);Click(app.Pet);Check(app.Pet.ActiveRow==4,"Subsequent double click triggers jump");app.Pet.Engine.ClearTransient();app.Bubbles.ToggleInput();app.Greetings.Tick(1799);Check(app.Memory.Turns.Count==0,"No startup greeting before deadline");app.Greetings.Tick(1800);Check(app.Memory.Turns.Count==1&&app.Memory.Turns[0].Status=="greeting","30-minute default greeting recorded as pet event");Check(app.Config.InputCollapsed&&app.Bubbles.Draft.Contains("暂存"),"Greeting does not expand input or erase draft");Check(app.Memory.Recent().Count==0&&app.Memory.Pending().Count==0,"Pet greeting is not fabricated user history or learned as user fact");Capture(app.Bubbles.Reply,Path.Combine(path,"greeting-default.png"));
    app.Config.Model="mock-preview";app.Memory.State.Pinned="用户喜欢一起读书";app.Request=new CancellationTokenSource();app.Greetings.Tick(3600);Check(!app.Greetings.Running&&app.Memory.Turns.Count==1,"Greeting does not interrupt active chat");app.Request.Dispose();app.Request=null;app.Greetings.Tick(3601);step++;
   }else if(step==3){if(app.Greetings.Running)return;Check(app.Memory.Turns.Count==2&&app.Greetings.Last.Contains("书页"),"Personalized greeting uses configured model and role/memory prompt");Capture(app.Bubbles.Reply,Path.Combine(path,"greeting-personalized.png"));app.Pet.Left=400;app.Pet.Top=300;app.Pet.Dock(13,false);step++;}
   else if(step>=4&&step<=7){int row=step+9;Check(app.Pet.DockRow==row,"Edge pose active "+row);Capture(app.Pet,Path.Combine(path,"edge-"+row+".png"));if(step<7){app.Pet.Left=400;app.Pet.Top=300;app.Pet.Dock(row+1,false);}step++;}
   else{File.WriteAllText(Path.Combine(path,"feature-tests.json"),Json.Write(new{ok=true,tests=results}),Encoding.UTF8);timer.Stop();app.Quit();}
  }catch(Exception ex){File.WriteAllText(Path.Combine(path,"feature-tests.json"),Json.Write(new{ok=false,error=ex.ToString(),tests=results}),Encoding.UTF8);timer.Stop();app.Quit();}};timer.Start();
 }
}
}
