using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Forms=System.Windows.Forms;

namespace Qingling {
public sealed class CompanionBubbles {
 readonly DesktopApp app;
 public readonly Window Reply,Input,Effects;
 readonly TextBox answer,entry;readonly TextBlock caption,face,spark;readonly Button send;readonly ScrollViewer scroll;
 bool visible,inputVisible=true,inputDragging,replyBeside;Panel inputButtons;Button fold;public bool Busy;double nextMemory;int lastRow=-1;double actionStart;
 readonly ReadingClock reading=new ReadingClock();readonly System.Diagnostics.Stopwatch time=System.Diagnostics.Stopwatch.StartNew();readonly Polygon tail;
 Point lastArrowTarget=new Point(double.NaN,double.NaN);Size lastArrowSize;
 public void AdvanceReading(double now,bool hover){if(reading.Tick(now,Busy||hover)){visible=false;Reply.Hide();}}
 void ArmReading(){reading.Arm(time.Elapsed.TotalSeconds,app.Config.BubbleSeconds);}
 public void HideInput(){inputVisible=false;app.Config.InputHidden=true;Input.Hide();SaveInput();}
 public void ShowInput(){inputVisible=true;app.Config.InputHidden=false;ApplyInput();Input.Show();SaveInput();Follow();}
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int n);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h,int n,int value);
 static Window Floating(DesktopApp app,double width,double height,string title){var w=new Window{Title=title,Width=width,Height=height,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Topmost=app.Config.Topmost,Owner=app.Pet};w.FontFamily=new FontFamily("Microsoft YaHei UI");w.FontSize=12;w.Foreground=Theme.Ink;return w;}
 public CompanionBubbles(DesktopApp a){app=a;Reply=Floating(a,330,208,"霖铃 · 回复气泡");Input=Floating(a,320,112,"和霖铃说话");Effects=Floating(a,340,260,"霖铃 · 心情");
  var root=new Grid();Reply.Content=root;
  var inner=new Grid();inner.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});inner.RowDefinitions.Add(new RowDefinition());
  var bar=new DockPanel();var close=Small("收起");close.Click+=(s,e)=>{visible=false;Reply.Hide();};DockPanel.SetDock(close,Dock.Right);bar.Children.Add(close);caption=Theme.Text("霖铃  ·  铃声轻响",12,Theme.Deep);caption.VerticalAlignment=VerticalAlignment.Center;bar.Children.Add(caption);inner.Children.Add(bar);
  answer=new TextBox{Text="我在这里。点下方的小框，就可以和我说话。",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=Theme.Ink,FontSize=14,AcceptsReturn=true,Padding=new Thickness(2,8,2,2)};
  scroll=new ScrollViewer{Content=answer,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);inner.Children.Add(scroll);
  root.Children.Add(new Border{Margin=new Thickness(12),Child=inner,Background=Theme.Paper,BorderBrush=Theme.Deep,BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(12),Padding=new Thickness(12,6,12,10)});
  tail=new Polygon{Fill=Theme.Paper,Stroke=Theme.Deep,StrokeThickness=2,IsHitTestVisible=false};root.Children.Add(tail);
  var box=new Grid();box.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});box.RowDefinitions.Add(new RowDefinition());box.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Input.Content=new Border{Margin=new Thickness(3),Child=box,Background=Theme.Paper,BorderBrush=Theme.Deep,BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(10),Padding=new Thickness(8,3,8,5)};
  var inputHeader=new DockPanel();fold=Small("收起");fold.Click+=(s,e)=>ToggleInput();DockPanel.SetDock(fold,Dock.Right);inputHeader.Children.Add(fold);var grip=Theme.Text("⋮⋮  对霖铃说",11,Theme.Muted);grip.Cursor=Cursors.SizeAll;grip.Background=Brushes.Transparent;grip.VerticalAlignment=VerticalAlignment.Center;grip.MouseLeftButtonDown+=(s,e)=>{e.Handled=true;inputDragging=true;try{Input.DragMove();app.Config.InputPinned=true;app.Config.InputX=Input.Left;app.Config.InputY=Input.Top;app.Config.InputScreen=ScreenArea.Read(Input,"").Name;SaveInput();}catch(InvalidOperationException){}finally{inputDragging=false;}};inputHeader.Children.Add(grip);box.Children.Add(inputHeader);
  var hide=Small("隐藏");hide.ToolTip="完全隐藏输入框；右键霖铃可重新显示";hide.Click+=(s,e)=>HideInput();DockPanel.SetDock(hide,Dock.Right);inputHeader.Children.Insert(0,hide);
  entry=Theme.Box("",true);entry.Margin=new Thickness(0);entry.Padding=new Thickness(5);entry.ToolTip="Enter 发送 · Shift+Enter 换行";entry.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Shift)==0){e.Handled=true;Submit();}};Grid.SetRow(entry,1);box.Children.Add(entry);
  var buttons=new DockPanel();inputButtons=buttons;Grid.SetRow(buttons,2);box.Children.Add(buttons);send=Small("发送 ↗");send.Click+=(s,e)=>Submit();DockPanel.SetDock(send,Dock.Right);buttons.Children.Add(send);var settings=Small("设置");settings.Click+=(s,e)=>app.OpenSettings();buttons.Children.Add(settings);var history=Small("记录 / 记忆");history.Click+=(s,e)=>app.OpenHistory();buttons.Children.Add(history);
  var cm=new ContextMenu();var attach=new MenuItem{Header="输入框重新跟随霖铃"};attach.Click+=(s,e)=>{app.Config.InputPinned=false;SaveInput();Follow();};cm.Items.Add(attach);Input.ContextMenu=cm;ApplyInput();
  var canvas=new Canvas{IsHitTestVisible=false};Effects.Content=canvas;face=Theme.Text("",18,Theme.Deep);face.FontWeight=FontWeights.Bold;face.Background=Brushes.Transparent;face.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Colors.Ivory,BlurRadius=0,ShadowDepth=1,Opacity=1};canvas.Children.Add(face);spark=Theme.Text("✧",20,Theme.Deep);canvas.Children.Add(spark);
  Effects.SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(Effects).Handle;SetWindowLong(h,-20,GetWindowLong(h,-20)|0x20|0x08000000);};
  Reply.Closing+=(s,e)=>{if(!app.Quitting){e.Cancel=true;visible=false;Reply.Hide();}};Input.Closing+=(s,e)=>{if(!app.Quitting){e.Cancel=true;HideInput();}};
  if(a.History.Count>0)answer.Text=a.History[a.History.Count-1].content;SetText(answer.Text);
 }
 static Button Small(string text){var b=Theme.Button(text,false);b.MinHeight=22;b.FontSize=11;b.Padding=new Thickness(6,2,6,2);b.Margin=new Thickness(2);b.BorderThickness=new Thickness(0);b.Background=Brushes.Transparent;return b;}
 void Submit(){if(Busy){if(app.Request!=null)app.Request.Cancel();return;}string q=entry.Text.Trim();if(q.Length==0)return;app.Chat.Submit(q);}
 public void Begin(){reading.Stop();visible=true;Reply.Show();Busy=true;entry.Clear();entry.IsEnabled=false;send.Content="停止";caption.Text="霖铃  ·  正在想一想…";SetText("让我想一想…");}
 public void SetText(string text){answer.Text=text;answer.Measure(new Size(260,double.PositiveInfinity));Reply.Height=Math.Min(280,Math.Max(145,answer.DesiredSize.Height+82));scroll.ScrollToEnd();}
 public void End(string status){Busy=false;entry.IsEnabled=true;send.Content="发送 ↗";caption.Text="霖铃  ·  "+status;ArmReading();}
 public void SaveInput(){if(!app.Smoke)try{app.Store.Save(app.Config);}catch{}}
 public string Draft {get{return entry.Text;}set{entry.Text=value;}}
 public void PinInput(double x,double y,string screen){app.Config.InputPinned=true;app.Config.InputX=x;app.Config.InputY=y;app.Config.InputScreen=screen;SaveInput();Follow();}
 public void ToggleInput(){app.Config.InputCollapsed=!app.Config.InputCollapsed;ApplyInput();SaveInput();Follow();}
 public void ApplyInput(){bool collapsed=app.Config.InputCollapsed;entry.Visibility=inputButtons.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;Input.Width=collapsed?210:320;Input.Height=collapsed?42:140;fold.Content=collapsed?"展开":"收起";}
 public void Greet(string text){visible=true;SetText(text);caption.Text="霖铃 · 半小时的小问候";Reply.Show();ArmReading();Follow();}
 public void Show(){Show(true);}
 public void Show(bool restoreInput){inputVisible=restoreInput||!app.Config.InputHidden;if(restoreInput){app.Config.InputHidden=false;SaveInput();}visible=true;ApplyInput();if(!app.Pet.IsVisible)app.Pet.Show();Reply.Show();if(inputVisible)Input.Show();else Input.Hide();ArmReading();Follow();}
 public void Hide(){visible=false;inputVisible=false;Reply.Hide();Input.Hide();}
 public void Close(){Reply.Close();Input.Close();Effects.Close();}
 public void Follow(){
  AdvanceReading(time.Elapsed.TotalSeconds,Reply.IsMouseOver||answer.IsMouseCaptureWithin||!app.Pet.IsVisible);
  if(!app.Pet.IsVisible){Reply.Hide();Input.Hide();Effects.Hide();return;}
  Reply.Topmost=Input.Topmost=Effects.Topmost=app.Config.Topmost;
  var area=ScreenArea.Read(app.Pet,app.Pet.CurrentScreenName);
  double left=area.Work.Left,top=area.Work.Top,right=area.Work.Right,bottom=area.Work.Bottom;
  if(inputVisible&&!Input.IsVisible)Input.Show();
  if(!inputDragging){
   if(app.Config.InputPinned&&!double.IsNaN(app.Config.InputX)&&!double.IsNaN(app.Config.InputY)){var inputArea=ScreenArea.Read(Input,app.Config.InputScreen);Input.Left=Clamp(app.Config.InputX,inputArea.Work.Left,inputArea.Work.Right-Input.Width);Input.Top=Clamp(app.Config.InputY,inputArea.Work.Top,inputArea.Work.Bottom-Input.Height);}
   else{Input.Left=Clamp(app.Pet.Left-Input.Width-8,left,right-Input.Width);if(app.Pet.Left-Input.Width-8<left)Input.Left=Clamp(app.Pet.Left+app.Pet.Width+8,left,right-Input.Width);Input.Top=Clamp(app.Pet.Top+app.Pet.Height*.6,top,bottom-Input.Height);}
  }
  if(visible){if(!Reply.IsVisible)Reply.Show();
   // Choose from available space before clamping. Hysteresis prevents the
   // upper-edge case from alternating between above and beside each tick.
   replyBeside=BubblePlacement.NextBeside(replyBeside,app.Pet.Top,top,Reply.Height);
   if(!replyBeside){Reply.Left=Clamp(app.Pet.Left+app.Pet.Width/2-Reply.Width/2,left,right-Reply.Width);Reply.Top=Clamp(app.Pet.Top-Reply.Height+12,top,bottom-Reply.Height);}
   else {double beside=app.Pet.Left+app.Pet.Width+8;if(beside+Reply.Width>right)beside=app.Pet.Left-Reply.Width-8;Reply.Left=Clamp(beside,left,right-Reply.Width);Reply.Top=Clamp(app.Pet.Top+24,top,bottom-Reply.Height);}
   var clip=app.Pet.DockRow>=13?Docking.Clip(app.Pet.DockRow,app.Pet.Width,app.Pet.Height):new Rect(0,0,app.Pet.Width,app.Pet.Height);
   var arrowSize=new Size(Reply.Width,Reply.Height);var arrowTarget=new Point(app.Pet.Left+clip.Left+clip.Width/2-Reply.Left,app.Pet.Top+clip.Top+clip.Height/2-Reply.Top);
   if(double.IsNaN(lastArrowTarget.X)||lastArrowSize!=arrowSize||(arrowTarget-lastArrowTarget).LengthSquared>.25){tail.Points=BubbleArrow.Points(arrowSize,arrowTarget);lastArrowTarget=arrowTarget;lastArrowSize=arrowSize;}
   }
  Effects.Width=app.Pet.Width+160;Effects.Height=app.Pet.Height+50;Effects.Left=app.Pet.Left-80;Effects.Top=app.Pet.Top-25;
  if(app.Config.Effects&&app.Pet.DockRow<13){if(!Effects.IsVisible)Effects.Show();}else Effects.Hide();
 }
 static double Clamp(double v,double min,double max){return Math.Max(min,Math.Min(v,Math.Max(min,max)));}
 public void Animate(int row,double now){
  if(row!=lastRow){lastRow=row;actionStart=now;}
  string[] faces={"(｡•ᴗ•｡)","♪ →","← ♪","(｡･ω･)ﾉ","ヽ(•ω•)ﾉ","(；ω；)","(｡•́‿•̀｡)","(・・?)","(￢‿￢)","","","(ง •̀_•́)ง","(>﹏<)","(・ω|","|ω・)","(｡･ω･｡)","(˘︶˘)♪","(－_－) zZ"};
  face.Text=row<faces.Length?faces[row]:"✧";double phase=now-actionStart;face.Opacity=row==0?.30+.30*Math.Sin(now*1.5):.8+.2*Math.Sin(now*3);
  Canvas.SetLeft(face,4+Math.Round(3*Math.Sin(phase*2)));Canvas.SetTop(face,40+Math.Round(5*Math.Sin(phase*2.5)));
  spark.Text=row==17?"":row==7?"···":row==12?"!":row==11?"↑":"✧";Canvas.SetLeft(spark,Effects.Width-45);Canvas.SetTop(spark,65+Math.Round(7*Math.Sin(phase*3)));spark.Opacity=row==17?0:.5+.4*Math.Sin(now*2);
  if(now>nextMemory){nextMemory=now+30;app.Memories.Run(false);}
 }
}
}
