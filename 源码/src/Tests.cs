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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Qingling {
public static class Tests {
 static readonly List<string> passed=new List<string>();
 static void Assert(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);passed.Add(name);}
 sealed class Handler:HttpMessageHandler {
  readonly Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> fn;
  public Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> f){fn=f;}
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken t){return fn(req,t);}
 }
 static HttpResponseMessage Response(string body,string type){return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,type)};}
 public static int Run(string path){try{RunAsync(Path.GetDirectoryName(Path.GetFullPath(path))).GetAwaiter().GetResult();File.WriteAllText(path,Json.Write(new{ok=true,tests=passed}),Encoding.UTF8);return 0;}catch(Exception e){File.WriteAllText(path,Json.Write(new{ok=false,tests=passed,error=e.ToString()}),Encoding.UTF8);return 1;}}
 static async Task RunAsync(string root){
  ComfortTests.Run();FeatureTests.Run();UpgradeTests.Run(root);passed.AddRange(UpgradeTests.Results);var anim=new AnimationEngine();anim.Busy=true;
  for(int i=0;i<120;i++)Assert(MotionData.Row(anim.Frame(i*.5,1))==7,"Working remains animated at "+(i*.5)+" s");
  anim.Play(4,60,2);Assert(MotionData.Row(anim.Frame(60.1,1))==4,"Jump can override working temporarily");Assert(MotionData.Row(anim.Frame(62.1,1))==7,"Working resumes after interaction");anim.Busy=false;Assert(MotionData.Row(anim.Frame(63,1))==0,"Idle after completion");
  for(int i=0;i<16;i++){double angle=i*22.5*Math.PI/180;Assert(AnimationEngine.Look(Math.Sin(angle)*200,-Math.Cos(angle)*200)==72+i,"Gaze direction "+i);}
  Assert(AnimationEngine.Look(2,2)==-1,"Gaze dead zone");
  var gaze=new GazeTracker();
  Assert(gaze.Update(0,-200,0,true)==72,"Gaze enters through neutral pose");
  Assert(gaze.Update(200,0,.016,true)==72&&gaze.Update(200,0,.043,true)==73,"Gaze advances at most one sector per 24 fps step");
  Assert(gaze.Update(200,0,.060,true)==73&&gaze.Update(200,0,.086,true)==74,"Gaze does not accelerate at dispatcher tick rate");
  var cadence=new GazeTracker();cadence.Update(0,-200,0,true);int cadenceIndex=72;
  for(int tick=1;tick<=22;tick++){int next=cadence.Update(0,200,tick*.016,true);Assert(next==cadenceIndex||next==cadenceIndex+1,"Gaze advances at most one sector per actual dispatcher tick "+tick);cadenceIndex=next;}
  Assert(cadenceIndex==80,"Gaze reaches eight sectors at 24 fps on 16 ms dispatcher ticks");
  Assert(gaze.Update(200,0,.129,true)==75&&gaze.Update(200,0,.172,true)==76,"Large cursor jump walks through all intervening directions");
  Assert(gaze.Update(200,0,10,true)==76,"Stationary cursor keeps its look indefinitely");
  var wrap=new GazeTracker();wrap.Update(0,-200,0,true);
  Assert(wrap.Update(-100,-200,.05,true)==87&&wrap.Update(0,-200,.10,true)==72,"Gaze takes shortest path across sectors 15 and 0");
  Assert(wrap.Update(-100,-200,.15,true)==87&&wrap.Update(0,0,.20,true)==87&&wrap.Update(0,0,2,true)==87,"Center zone holds the last look without idle flicker");
  Assert(wrap.Update(15,0,2.05,true)==87&&wrap.Update(0,-200,2.10,true)==72,"Center zone has radial hysteresis");
  Assert(gaze.Update(2000,0,10.05,true)==75&&gaze.Update(2000,0,10.10,true)==74&&gaze.Update(2000,0,10.15,true)==73&&gaze.Update(2000,0,10.20,true)==72,"Leaving range relaxes one sector at a time");
  Assert(gaze.Update(2000,0,10.30,true)==72&&gaze.Update(2000,0,10.40,true)==-1,"Gaze returns to idle only after a neutral hold");
  Assert(gaze.Update(200,0,10.50,true)==72&&gaze.Update(200,0,10.55,true)==73&&gaze.Update(200,0,10.60,false)==72,"Disabling follow mouse relaxes instead of snapping");
  Assert(Settings.Endpoint("https://example.org/v1/").AbsoluteUri=="https://example.org/v1/chat/completions","Base URL normalized");Assert(Settings.Endpoint("http://localhost:11434/v1/chat/completions").AbsoluteUri.EndsWith("/v1/chat/completions"),"Local full endpoint accepted");
  bool denied=false;try{Settings.Endpoint("https://user:secret@example.org/v1");}catch(ArgumentException){denied=true;}Assert(denied,"Credentials in URL rejected");
  string secret="test-key-local-only";string encrypted=SettingsStore.Protect(secret);Assert(!encrypted.Contains(secret)&&SettingsStore.Unprotect(encrypted)==secret,"Windows account key encryption round trip");
  string dir=Path.Combine(root,"settings-test-"+Guid.NewGuid().ToString("N"));var store=new SettingsStore(dir);var config=new Settings{Model="test-model",EncryptedKey=encrypted};store.Save(config);var loaded=store.Load();Assert(loaded.Model==config.Model&&SettingsStore.Unprotect(loaded.EncryptedKey)==secret,"Settings persisted with encrypted key");Assert(!File.ReadAllText(Path.Combine(dir,"settings.json")).Contains(secret),"No plaintext API key in settings");Directory.Delete(dir,true);
  var history=new List<Message>{new Message("user","你好")};
  using(var api=new ChatApi(new Handler(async(req,ct)=>{Assert(req.Headers.Authorization.Parameter==secret,"Bearer auth reaches configured endpoint");Assert(req.RequestUri.AbsoluteUri=="https://api.openai.com/v1/chat/completions","Chat endpoint path");string body=await req.Content.ReadAsStringAsync();Assert(body.Contains("你好")&&body.Contains("test-model"),"Model and Chinese content serialized");return Response("data: {\"choices\":[{\"delta\":{\"content\":\"你\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"好，霖铃\"}}]}\n\ndata: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n","text/event-stream");}))){var deltas=new List<string>();string answer=await api.Complete(config,secret,history,deltas.Add,CancellationToken.None);Assert(answer=="你好，霖铃"&&deltas.Count==2,"SSE combines incremental UTF8 response");}
  using(var api=new ChatApi(new Handler((req,ct)=>Task.FromResult(Response("{\"choices\":[{\"message\":{\"content\":\"普通回复\"}}]}","application/json"))))){Assert(await api.Complete(config,"",history,null,CancellationToken.None)=="普通回复","Nonstream fallback accepted");}
  using(var api=new ChatApi(new Handler((req,ct)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized){Content=new StringContent(secret)})))){bool failed=false;try{await api.Complete(config,secret,history,null,CancellationToken.None);}catch(InvalidOperationException e){failed=e.Message.Contains("401")&&!e.Message.Contains(secret);}Assert(failed,"HTTP errors redact raw server body");}
  using(var api=new ChatApi(new Handler(async(req,ct)=>{await Task.Delay(10000,ct);return Response("{}","application/json");})))using(var cts=new CancellationTokenSource(40)){bool cancelled=false;try{await api.Complete(config,"",history,null,cts.Token);}catch(OperationCanceledException){cancelled=true;}Assert(cancelled,"Cancellation interrupts pending request");}
  using(var api=new ChatApi(new Handler((req,ct)=>Task.FromResult(Response("data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n","text/event-stream"))))){bool failed=false;try{await api.Complete(config,"",history,null,CancellationToken.None);}catch(InvalidOperationException){failed=true;}Assert(failed,"Truncated stream is not silently marked complete");}
  using(var api=new ChatApi(new Handler((req,ct)=>Task.FromResult(Response("data: {\"error\":{\"message\":\"bad\"}}\n\n","text/event-stream"))))){bool failed=false;try{await api.Complete(config,"",history,null,CancellationToken.None);}catch(InvalidOperationException){failed=true;}Assert(failed,"Stream error events reported");}
 }
 static void Capture(Window w,string path){w.UpdateLayout();var visual=w.Content as FrameworkElement;int width=(int)Math.Ceiling(visual.ActualWidth),height=(int)Math.Ceiling(visual.ActualHeight);var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){dc.DrawRectangle(w.Background,null,new Rect(0,0,width,height));var brush=new VisualBrush(visual){Stretch=Stretch.Fill};dc.DrawRectangle(brush,null,new Rect(0,0,width,height));}bmp.Render(drawing);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))png.Save(f);}
 public static void UiSmoke(DesktopApp app,string path){Directory.CreateDirectory(path);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};int step=0;SettingsWindow settings=null;timer.Tick+=(s,e)=>{try{if(step==0){Assert(app.TrayReady,"System tray icon initialized");Assert(app.Pet.AllowsTransparency&&app.Pet.Topmost&&!app.Pet.ShowInTaskbar,"Transparent topmost pet window");app.Chat.Demo();Capture(app.Chat,Path.Combine(path,"chat-preview.png"));Capture(app.Pet,Path.Combine(path,"pet-preview.png"));settings=new SettingsWindow(app);settings.Show();step++;}else{Capture(settings,Path.Combine(path,"settings-preview.png"));var tabs=((System.Windows.Controls.Grid)settings.Content).Children.OfType<System.Windows.Controls.TabControl>().First();tabs.SelectedIndex=1;settings.UpdateLayout();Capture(settings,Path.Combine(path,"animation-settings-preview.png"));Assert(settings.ActualWidth>=480&&app.Chat.ActualWidth>=370,"Chat and settings windows render");File.WriteAllText(Path.Combine(path,"ui-test-results.json"),Json.Write(new{ok=true,tests=passed}),Encoding.UTF8);timer.Stop();app.Quit();}}catch(Exception ex){File.WriteAllText(Path.Combine(path,"ui-test-results.json"),Json.Write(new{ok=false,error=ex.ToString()}));timer.Stop();app.Quit();}};timer.Start();}
}
}



