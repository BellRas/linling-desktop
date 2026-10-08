using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Qingling {
public sealed class Settings {
 public string BaseUrl = "https://api.openai.com/v1";
 public string Model = "";
 public string EncryptedKey = "";
 public string Persona = "你是霖铃，一位白发、浅绿色蛇尾、佩戴小铃铛的桌面伙伴。用简洁自然的中文与用户交流，温柔、略带俏皮和神秘感。认真回答问题，不假装拥有未被提供的屏幕、文件或系统访问能力。";
 public string Personality="温柔、呆萌，偶尔俏皮，有一点神秘感；尊重用户的情绪与边界。";
 public string Speech="自然简洁的中文，像朋友聊天。可偶尔使用颜文字，不滥用口癖；解释问题时保持清楚准确。";
 public string Knowledge="霖铃是白发、浅绿蛇尾、佩戴小铃铛的桌面伙伴。没有被提供的现实经历或能力不要编造。";
 public bool MemoryEnabled=true, Effects=true;
 public bool InputCollapsed=false,InputPinned=false;public double InputX=double.NaN,InputY=double.NaN;public string InputScreen="",DockScreen="";public int DockSide=-1;
 public int MemoryEvery=4;
 public bool InputHidden=false;
 public double BubbleSeconds=20,BlinkSeconds=5;
 [ScriptIgnore] public string SystemOverride;
 public bool Stream = true, Topmost = true, FollowMouse = true, RandomActions = true;
 public int CompanionLevel=1; public bool PauseInFullscreen=true;
 public double Size = 176, ActionSeconds = 3, Speed = 1, RandomMin = 15, RandomMax = 35;
 public double X = double.NaN, Y = double.NaN;
 public int TimeoutSeconds = 180;
 public Settings Copy() { return Json.Read<Settings>(Json.Write(this)); }
 public void Validate() {
  Endpoint(BaseUrl);
  if(double.IsNaN(BubbleSeconds)||double.IsInfinity(BubbleSeconds)||BubbleSeconds<3||BubbleSeconds>300||double.IsNaN(BlinkSeconds)||double.IsInfinity(BlinkSeconds)||BlinkSeconds<2||BlinkSeconds>20)throw new ArgumentException("气泡停留时间为 3–300 秒，眨眼间隔为 2–20 秒。");
  if(MemoryEvery<1||MemoryEvery>6)throw new ArgumentException("记忆间隔应为 1–6 轮。");
  if(CompanionLevel<0||CompanionLevel>2)throw new ArgumentException("陪伴强度应选择安静、标准或活泼。");
  if (Size < 80 || Size > 384 || ActionSeconds < 1 || ActionSeconds > 30 || Speed < .25 || Speed > 3 || RandomMin < 5 || RandomMax < RandomMin || RandomMax > 300 || TimeoutSeconds < 10 || TimeoutSeconds > 600) throw new ArgumentException("请检查大小、动画时长、随机间隔和超时设置的范围。");
 }
 public static Uri Endpoint(string value) {
  Uri u;
  if (!Uri.TryCreate((value ?? "").Trim(), UriKind.Absolute, out u) || !(u.Scheme == "https" || (u.Scheme == "http" && u.IsLoopback)) || !string.IsNullOrEmpty(u.UserInfo) || !string.IsNullOrEmpty(u.Query) || !string.IsNullOrEmpty(u.Fragment))
   throw new ArgumentException("API 地址须为 HTTPS；本机服务也可以使用 http://localhost。请勿在地址中填写密钥、查询参数或账号密码。");
  string s = u.AbsoluteUri.TrimEnd('/');
  return new Uri(s.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ? s : s + "/chat/completions");
 }
}
public static class Json {
 public static string Write(object value) { return new JavaScriptSerializer { MaxJsonLength = 8388608 }.Serialize(value); }
 public static T Read<T>(string value) { return new JavaScriptSerializer { MaxJsonLength = 8388608 }.Deserialize<T>(value); }
}
public sealed class SettingsStore {
 public readonly string DirectoryPath;
 public string Warning;
 static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Qingling.Desktop.v1");
 public SettingsStore(string root) { DirectoryPath = root; }
 public Settings Load() {
  string path = Path.Combine(DirectoryPath, "settings.json");
  if (!File.Exists(path)) return new Settings();
  try { var s = Json.Read<Settings>(File.ReadAllText(path, Encoding.UTF8)); s.Validate(); return s; }
  catch { Warning = "保存的设置无法读取，已使用默认值；原文件尚未改动。"; return new Settings(); }
 }
 public static string Protect(string key) { return string.IsNullOrEmpty(key) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key), Entropy, DataProtectionScope.CurrentUser)); }
 public static string Unprotect(string key) { return string.IsNullOrEmpty(key) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(key), Entropy, DataProtectionScope.CurrentUser)); }
 public void Save(Settings s) {
  s.Validate(); Directory.CreateDirectory(DirectoryPath);
  string path = Path.Combine(DirectoryPath, "settings.json"), temp = path + ".tmp";
  File.WriteAllText(temp, Json.Write(s), new UTF8Encoding(false));
  if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
 }
}
public static class Startup {
 const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
 const string Name = "LinlingDesktop";
 public static bool Enabled { get { using (var k=Registry.CurrentUser.OpenSubKey(Key)) return k != null && (k.GetValue(Name) != null || k.GetValue("QinglingDesktop") != null); } }
 public static void Set(bool enabled) {
  using (var k=Registry.CurrentUser.CreateSubKey(Key)) {
   k.DeleteValue("QinglingDesktop", false);
   if (enabled) k.SetValue(Name, "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --quiet");
   else k.DeleteValue(Name, false);
  }
 }
}
public sealed class Message { public string role; public string content; public Message() {} public Message(string r,string c) { role=r; content=c; } }
public sealed class ChatApi : IDisposable {
 readonly HttpClient client;
 public ChatApi() : this(new HttpClientHandler { AllowAutoRedirect=false, AutomaticDecompression=DecompressionMethods.GZip | DecompressionMethods.Deflate }) {}
 public ChatApi(HttpMessageHandler handler) { client=new HttpClient(handler); client.Timeout=System.Threading.Timeout.InfiniteTimeSpan; }
 public async Task<string> Complete(Settings s, string key, IList<Message> history, Action<string> onDelta, CancellationToken cancel) {
  s.Validate(); if (string.IsNullOrWhiteSpace(s.Model)) throw new ArgumentException("请先在设置中填写模型名称。");
  var messages=new List<Message>();
  messages.Add(new Message("system",s.SystemOverride??Prompts.Compose(s,"")));
  // Keep complete recent turns. Caller history ends in the new user message.
  int skip=Math.Max(0,history.Count-21); if (skip%2 != 0) skip++;
  messages.AddRange(history.Skip(skip));
  var payload=new { model=s.Model.Trim(), messages=messages, stream=s.Stream };
  using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel)) {
   timeout.CancelAfter(TimeSpan.FromSeconds(s.TimeoutSeconds));
   try {
    using(var request=new HttpRequestMessage(HttpMethod.Post,Settings.Endpoint(s.BaseUrl))) {
     if (!string.IsNullOrWhiteSpace(key)) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key.Trim());
     request.Content=new StringContent(Json.Write(payload),Encoding.UTF8,"application/json");
     using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false)) {
      if (!response.IsSuccessStatusCode) {
       int status=(int)response.StatusCode;
       string hint=status==401||status==403?"请检查 API Key 和模型权限。":status==429?"请求过多或额度不足，请稍后重试。":status==404?"请检查 API 地址和模型名称。":status>=300&&status<400?"接口返回重定向，请填写最终 API 地址。":"请检查服务状态和接口设置。";
       throw new InvalidOperationException("接口返回 HTTP " + status + "。" + hint);
      }
      string media=response.Content.Headers.ContentType==null?"":response.Content.Headers.ContentType.MediaType;
      using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
      using(var registration=timeout.Token.Register(()=>stream.Dispose()))
      using(var reader=new StreamReader(stream,Encoding.UTF8)) {
       if (media.IndexOf("event-stream",StringComparison.OrdinalIgnoreCase)<0) {
        string raw=await reader.ReadToEndAsync().ConfigureAwait(false);timeout.Token.ThrowIfCancellationRequested();
        var obj=Parse(raw);string value=Content(obj,false);
        if (string.IsNullOrEmpty(value)) throw new InvalidOperationException("接口未返回可显示的文字。请检查模型，或切换流式回复选项。");
        if(onDelta!=null)onDelta(value); return value;
       }
       var result=new StringBuilder();var data=new StringBuilder();bool ended=false,finished=false;
       while(!ended) {
        timeout.Token.ThrowIfCancellationRequested();
        string line=await reader.ReadLineAsync().ConfigureAwait(false);
        if(line==null){if(data.Length>0)Consume(data.ToString(),result,onDelta,ref ended,ref finished);break;}
        if(line.Length==0){if(data.Length>0){Consume(data.ToString(),result,onDelta,ref ended,ref finished);data.Clear();}continue;}
        if(line.StartsWith("data:",StringComparison.Ordinal)){if(data.Length>0)data.Append('\n');data.Append(line.Substring(5).TrimStart());}
        if(data.Length>8388608||result.Length>2000000)throw new InvalidOperationException("回复过长，已停止接收。请缩小问题范围。");
       }
       timeout.Token.ThrowIfCancellationRequested();
       if(!ended&&!finished)throw new InvalidOperationException("回复连接提前中断，已保留收到的内容。请重新发送。");
       if(result.Length==0)throw new InvalidOperationException("模型没有返回文字回复，请检查模型配置。");
       return result.ToString();
      }
     }
    }
   } catch(Exception ex) {
    if(cancel.IsCancellationRequested)throw new OperationCanceledException(cancel);
    if(timeout.IsCancellationRequested)throw new TimeoutException("等待回复超时。可以在设置中增加超时时间，或稍后重试。");
    if(ex is HttpRequestException)throw new InvalidOperationException("无法连接模型服务，请检查网络和 API 地址。",ex);
    throw;
   }
  }
 }
 static Dictionary<string,object> Parse(string text) {
  Dictionary<string,object> obj;
  try {obj=Json.Read<Dictionary<string,object>>(text);}catch{throw new InvalidOperationException("接口回复不是有效的 OpenAI 兼容 JSON。");}
  if(obj==null||obj.ContainsKey("error"))throw new InvalidOperationException("模型服务返回错误，请检查模型权限、额度或接口设置。");return obj;
 }
 static void Consume(string data,StringBuilder result,Action<string> callback,ref bool ended,ref bool finished) {
  if(data.Trim()=="[DONE]"){ended=true;return;}
  var obj=Parse(data); object v;
  if(obj.TryGetValue("choices",out v))foreach(var item in (System.Collections.IList)v){var c=item as Dictionary<string,object>;if(c!=null&&c.ContainsKey("finish_reason")&&c["finish_reason"]!=null)finished=true;}
  string delta=Content(obj,true);if(delta.Length>0){result.Append(delta);if(callback!=null)callback(delta);}
 }
 static string Content(Dictionary<string,object> obj,bool delta) {
  object v; if(!obj.TryGetValue("choices",out v)||!(v is System.Collections.IList)||((System.Collections.IList)v).Count==0)return "";
  var c=((System.Collections.IList)v)[0] as Dictionary<string,object>;if(c==null)return "";
  if(!c.TryGetValue(delta?"delta":"message",out v))return "";
  var m=v as Dictionary<string,object>;if(m==null)return "";
  if(m.TryGetValue("refusal",out v)&&v is string&&!string.IsNullOrEmpty((string)v))return (string)v;
  if(!m.TryGetValue("content",out v)||v==null)return "";
  if(v is string)return (string)v;
  if(v is System.Collections.IList){var b=new StringBuilder();foreach(var p in (System.Collections.IList)v){var d=p as Dictionary<string,object>;object t;if(d!=null&&d.TryGetValue("text",out t)&&t is string)b.Append((string)t);}return b.ToString();}
  return "";
 }
 public void Dispose(){client.Dispose();}
}
public sealed class AnimationEngine {
 public Func<int,int,int> ChooseStart,ChooseDragStart;public int PreviewRow=-1,DockRow=-1;double phase,lastTime=-1;int previousIndex=-1;
 public bool Busy; public int DragRow=-1; int transient=-1; double until; int last=-1;
 public void ClearTransient(){transient=-1;until=0;}
 public static int Drag(double dx,double dy){return Math.Abs(dy)>Math.Abs(dx)*1.2?(dy<0?11:12):(dx>=0?1:2);}
 public void Play(int row,double now,double seconds){transient=row;until=now+seconds;}
 public int CurrentRow(double now){if(DragRow>=0)return DragRow;if(PreviewRow>=0)return PreviewRow;if(DockRow>=0)return DockRow;if(now<until&&transient>=0)return transient;return Busy?7:0;}
 public static int FrameCount(int row){return MotionData.Counts[row];}
 static readonly Dictionary<int,int[]> timing=new Dictionary<int,int[]>();
 public static int[] Timing(int row){int[] values;if(timing.TryGetValue(row,out values))return values;int count=FrameCount(row);values=new int[count];for(int i=0;i<count;i++)values[i]=(int)Math.Round((i+1)*1000.0/24)-(int)Math.Round(i*1000.0/24);timing[row]=values;return values;}
 public double PhaseFraction(int row){var ds=Timing(row);return ds.Length==0?0:(phase%ds.Sum())/ds.Sum();}
 public int Frame(double now,double speed){int row=CurrentRow(now);var ds=Timing(row);double delta=lastTime<0?0:Math.Max(0,now-lastTime);lastTime=now;
  if(row==16||row==17)speed=Math.Min(speed,1); // Seated and sleeping loops stay calm at high action speeds.
  if(row!=last){bool reverse=(row==1&&last==2)||(row==2&&last==1);if(reverse)phase+=delta*1000*speed;else{bool drag=(row==1||row==2)&&DragRow==row;int start=drag?(ChooseDragStart==null?0:ChooseDragStart(row,previousIndex)):(ChooseStart==null||row==3||row==4||row>=13?0:ChooseStart(row,previousIndex));start=Math.Max(0,Math.Min(ds.Length-1,start));phase=0;for(int i=0;i<start;i++)phase+=ds[i];}last=row;}else phase+=delta*1000*speed;
  double ms=phase%ds.Sum();int col=0;while(col<ds.Length-1&&ms>=ds[col])ms-=ds[col++];previousIndex=MotionData.Index(row,col);return previousIndex;
 }
 public static class JumpArc {
  public static double Offset(double fraction,double height){if(fraction<=.18||fraction>=.82)return 0;double t=(fraction-.18)/.64;return height*.23*Math.Sin(Math.PI*t);}
 }
 public static int Look(double dx,double dy){if(dx*dx+dy*dy<144)return -1;double degrees=(Math.Atan2(dx,-dy)*180/Math.PI+360)%360;int index=(int)Math.Floor(degrees/22.5+.5)%16;return 9*8+index;}
}
public sealed class GazeTracker {
 const double SectorSeconds=1.0/24,NeutralSeconds=.18;
 const int NeutralSector=0;
 int sector=-1,target=-1;double lastStep=-1,neutralSince=-1;
 bool withinRange,centerHold;
 public int Update(double dx,double dy,double now,bool enabled){
  double radius=dx*dx+dy*dy;
  withinRange=enabled&&radius<(withinRange?1040.0*1040:1000.0*1000);
  if(withinRange){
   if(centerHold){if(radius>=18.0*18)centerHold=false;}
   else if(radius<=12.0*12)centerHold=true;
   if(centerHold){target=sector;}
   else{
    double angle=(Math.Atan2(dx,-dy)*180/Math.PI+360)%360;
    double difference=(angle-target*22.5+540)%360-180;
    if(target<0||Math.Abs(difference)>=14.5)target=(int)Math.Floor(angle/22.5+.5)%16;
   }
   neutralSince=-1;
  }else{centerHold=false;target=NeutralSector;}
  if(sector<0){
   if(!withinRange||centerHold)return -1;
   sector=NeutralSector;lastStep=now;
  }else if(target>=0&&target!=sector&&now-lastStep>=SectorSeconds){
   int clockwise=(target-sector+16)%16;
   sector=(sector+(clockwise<=8?1:15))%16;
   lastStep+=SectorSeconds;
   if(now-lastStep>SectorSeconds)lastStep=now;
  }
  if(!withinRange&&sector==NeutralSector){
   if(neutralSince<0)neutralSince=now;
   if(now-neutralSince>=NeutralSeconds){sector=-1;lastStep=now;return -1;}
  }else neutralSince=-1;
  return 72+sector;
 }
 public int Relax(double now){return Update(0,0,now,false);}
}
}

