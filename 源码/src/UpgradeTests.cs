using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Qingling {
public static class UpgradeTests {
 public static readonly List<string> Results=new List<string>();
 static void Check(bool value,string name){if(!value)throw new Exception(name);Results.Add(name);}
 public static void Run(string root){
  var settings=new Settings{Persona="角色甲",Personality="性格乙",Speech="语气丙",Knowledge="知识丁"};
  string prompt=Prompts.Compose(settings,"记忆戊");foreach(var field in new[]{"角色甲","性格乙","语气丙","知识丁","记忆戊"})Check(prompt.Contains(field),"Prompt includes "+field);
  string directory=Path.Combine(root,"memory-test-"+Guid.NewGuid().ToString("N"));var book=new MemoryBook(directory);
  book.Add("喜欢喝茶","我记住了","complete");book.Add("未完成的问题","片段","cancelled");book.Add("今天下雨","一起听雨","complete");
  Check(new MemoryBook(directory).Recent().Count==4,"Only complete turns are restored to context");Check(book.Pending().Count==2,"Cancelled turn excluded from memory learning");
  var result=MemoryBook.ParseResult("{\"shortTerm\":\"#1 用户喜欢喝茶\",\"longTerm\":\"用户喜欢喝茶\",\"growth\":\"聊天时适当提起茶\"}");
  MemoryBook.Apply(book.State,result,book.Pending());book.Edit("固定记忆",book.State.Short,book.State.Long,book.State.Growth);
  var restored=new MemoryBook(directory);Check(restored.State.Through==3&&restored.State.Pinned=="固定记忆"&&restored.Context().Contains("喝茶"),"Memory and checkpoint survive restart");
  var facts=MemoryBook.ParseResult("{\"shortTerm\":\"用户喜欢喝茶\",\"longTerm\":\"用户喜欢喝茶\",\"growth\":\"用茶来问候\",\"facts\":[{\"Text\":\"用户喜欢喝茶\",\"SourceIds\":[1]},{\"Text\":\"没有来源的编造\",\"SourceIds\":[999]}]}");
  MemoryBook.Apply(book.State,facts,book.Turns.Where(x=>x.Status=="complete").ToList());book.Save();Check(book.State.Facts.Count==1&&book.State.Facts[0].SourceIds[0]==1&&book.Source(1).User=="喜欢喝茶","Fact keeps a valid source and discards invented source IDs");
  book.ReviewFact(0,"rejected");restored=new MemoryBook(directory);Check(restored.State.Facts[0].Review=="rejected"&&!restored.Context().Contains("用户喜欢喝茶")&&!restored.Context().Contains("用茶来问候"),"Rejected fact survives restart and cannot leak back through summaries");
  book.ReviewFact(0,"confirmed");Check(book.Context().Contains("已确认 #1 用户喜欢喝茶"),"Confirmed source-backed fact is available to the next answer");
  bool rejected=false;try{MemoryBook.ParseResult("{\"shortTerm\":\"partial\"}");}catch{rejected=true;}Check(rejected,"Incomplete summary rejected without advancing checkpoint");
  book.Forget(false);Check(book.Pending().Count==0&&book.State.Pinned==""&&book.Turns.Count==3,"Forget does not relearn old conversations");
  book.Add("新的经历","新的回答","complete");Check(book.Pending().Count==1,"New conversations remain learnable after forget");book.Forget(true);
  restored=new MemoryBook(directory);Check(restored.Turns.Count==0&&restored.State.Long==""&&!File.Exists(Path.Combine(directory,"memories.json.bak")),"Full erase removes persisted transcript and memory backup");
  Check(AnimationEngine.Drag(2,-20)==11&&AnimationEngine.Drag(2,20)==12,"Vertical drag selects climbing and falling");Check(AnimationEngine.Drag(20,2)==1&&AnimationEngine.Drag(-20,2)==2,"Horizontal drag retains direction-aware gait");var large=new AnimationEngine{DragRow=2};int largeCell=large.Frame(0,1);Check(largeCell>=16&&largeCell<24,"Every left drag selects original full-stride source cells");
  var selectedStride=new AnimationEngine{DragRow=1,ChooseDragStart=(row,previous)=>5};Check(selectedStride.Frame(0,1)==13,"Large-stride entry uses a matching original source pose");
  Check(CompanionPolicy.Proactive(0,false)==false&&CompanionPolicy.Proactive(1,false)&&!CompanionPolicy.Proactive(2,true)&&CompanionPolicy.GreetingInterval(2)==1200&&CompanionPolicy.RandomInterval(20,2)<20,"Companion levels adjust only proactive activity");
  var engine=new AnimationEngine{DragRow=11};Check(engine.Frame(0,1)==MotionData.Index(11,0)&&engine.Frame(.15,1)==MotionData.Index(11,3),"Climbing advances through new frames");engine.DragRow=12;Check(engine.Frame(1,1)==MotionData.Index(12,0)&&engine.Frame(1.3,1)==MotionData.Index(12,6),"Falling advances through new frames");
  Directory.Delete(directory,true);
 }
 sealed class MockHandler:HttpMessageHandler {
  int chats;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken token){
   string body=await req.Content.ReadAsStringAsync();await Task.Delay(80,token);
   string answer=body.Contains("记忆整理器")?"{\"shortTerm\":\"#1 今天我们约好一起读书。\",\"longTerm\":\"用户喜欢一起读书。\",\"growth\":\"以轻松语气陪伴读书。\",\"facts\":[{\"Text\":\"用户提议一起读书\",\"SourceIds\":["+(body.Contains("\\\"Id\\\":2")?"2":"1")+"]}]}":"好呀，书页翻动的时候，我就安静地陪在你身旁。累了也可以叫我，听一声小铃铛。 (｡•ᴗ•｡)";
   if(!body.Contains("记忆整理器")){Check(body.Contains("温柔、呆萌")&&body.Contains("自然简洁的中文")&&body.Contains("没有被提供的现实经历"),"All role fields reach real chat request pipeline");if(++chats>1)Check(body.Contains("用户喜欢一起读书。")&&body.Contains("以轻松语气陪伴读书。"),"Generated memory and growth feed the next reply request");}
   return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Json.Write(new{choices=new[]{new{message=new{content=answer}}}}),Encoding.UTF8,"application/json")};
  }
 }
 public static ChatApi MockApi(){return new ChatApi(new MockHandler());}
 static void DrawWindow(System.Windows.Media.DrawingContext dc,Window w,Rect rect){var visual=w.Content as FrameworkElement;w.UpdateLayout();dc.DrawRectangle(w.Background,null,rect);if(visual is Canvas){var bmp=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(visual);dc.DrawImage(bmp,rect);}else dc.DrawRectangle(new VisualBrush(visual){Stretch=Stretch.Fill},null,rect);}
 static void Capture(Window w,string path){w.UpdateLayout();var visual=w.Content as FrameworkElement;var bmp=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())DrawWindow(dc,w,new Rect(0,0,bmp.PixelWidth,bmp.PixelHeight));bmp.Render(drawing);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))enc.Save(f);}
 static void Composite(DesktopApp a,string path){var windows=new[]{a.Pet,a.Bubbles.Effects,a.Bubbles.Reply,a.Bubbles.Input};double x=windows.Min(w=>w.Left)-25,y=windows.Min(w=>w.Top)-25;int width=(int)(windows.Max(w=>w.Left+w.Width)-x+25),height=(int)(windows.Max(w=>w.Top+w.Height)-y+25);var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){dc.DrawRectangle(Theme.Brush("#E9EDE6"),null,new Rect(0,0,width,height));foreach(var w in windows)DrawWindow(dc,w,new Rect(w.Left-x,w.Top-y,w.Width,w.Height));}bmp.Render(drawing);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))enc.Save(f);}
 public static void Ui(DesktopApp app,string path){
  Directory.CreateDirectory(path);app.Config.Model="mock-preview";app.Config.MemoryEvery=1;
  app.Pet.Left=650;app.Pet.Top=400;app.Bubbles.Show();app.OpenHistory();
  var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(600)};int step=0,waits=0;bool second=false;SettingsWindow settings=null;
  timer.Tick+=(s,e)=>{try{
   if(step==0){app.Chat.Submit("今天一起读书吧。");step++;}
   else if(step==1){if(app.Request!=null||app.Memories.Running){if(++waits>30)throw new Exception("UI request timeout");return;}
    if(!second){second=true;app.Chat.Submit("还记得我们约好的事情吗？");return;}
    Check(app.TrayReady&&app.Pet.AllowsTransparency&&app.Pet.Topmost,"Native tray and transparent topmost pet initialized");
    Check(app.Memory.Turns.Count==2&&app.Memory.State.Through==2,"Bubble chat persists and automatically summarizes through mock API");
    Check(new MemoryBook(app.Store.DirectoryPath).State.Long.Contains("读书"),"Generated memory survives disk reload");
    Check(app.Bubbles.Reply.IsVisible&&app.Bubbles.Input.IsVisible,"Reply and compact input are independent visible windows");
    Composite(app,Path.Combine(path,"desktop-preview.png"));Capture(app.Bubbles.Reply,Path.Combine(path,"reply-bubble.png"));Capture(app.Bubbles.Input,Path.Combine(path,"input-bubble.png"));Capture(app.Chat,Path.Combine(path,"history.png"));
    settings=new SettingsWindow(app);settings.Show();step++;
   }else if(step==2){var tabs=((Grid)settings.Content).Children.OfType<TabControl>().First();for(int i=0;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;settings.UpdateLayout();Capture(settings,Path.Combine(path,"settings-"+i+".png"));}settings.Close();
    var memory=new MemoryWindow(app);memory.Show();Capture(memory,Path.Combine(path,"memory.png"));var mtabs=((DockPanel)memory.Content).Children.OfType<TabControl>().First();mtabs.SelectedIndex=3;var factGrid=(Grid)((TabItem)mtabs.Items[3]).Content;factGrid.Children.OfType<ListBox>().First().SelectedIndex=0;memory.UpdateLayout();Capture(memory,Path.Combine(path,"memory-facts.png"));memory.Close();
    app.Pet.Engine.DragRow=11;step++;
   }else if(step==3){Capture(app.Pet,Path.Combine(path,"climbing.png"));app.Pet.Engine.DragRow=12;step++;}
   else {Capture(app.Pet,Path.Combine(path,"falling.png"));app.Pet.Engine.DragRow=-1;
    app.Pet.Left=0;app.Pet.Top=0;app.Bubbles.Follow();Check(app.Bubbles.Reply.Left>=0&&app.Bubbles.Reply.Top>=0&&app.Bubbles.Input.Top>=0,"Bubbles clamp at upper screen edge");
    File.WriteAllText(Path.Combine(path,"ui-test-results.json"),Json.Write(new{ok=true,tests=Results}),Encoding.UTF8);timer.Stop();app.Quit();}
  }catch(Exception ex){File.WriteAllText(Path.Combine(path,"ui-test-results.json"),Json.Write(new{ok=false,error=ex.ToString(),tests=Results}),Encoding.UTF8);timer.Stop();app.Quit();}};timer.Start();
 }
}
}
