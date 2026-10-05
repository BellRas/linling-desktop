using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Qingling {
public static class Theme {
 public static readonly Brush Ink=Brush("#26372E"),Muted=Brush("#6C7970"),Sage=Brush("#DCE7D6"),Paper=Brush("#F6F7F1"),Deep=Brush("#395744"),Line=Brush("#E1E7DC");
 public static Brush Brush(string c){return (Brush)new BrushConverter().ConvertFromString(c);}
 public static TextBlock Text(string value,double size,Brush color){return new TextBlock{Text=value,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap};}
 public static Button Button(string text,bool primary){return new Button{Content=text,Padding=new Thickness(15,9,15,9),Margin=new Thickness(4),Background=primary?Deep:Brushes.White,Foreground=primary?Brushes.White:Ink,BorderBrush=primary?Deep:Line,BorderThickness=new Thickness(1),Cursor=Cursors.Hand,FontSize=13,MinHeight=36};}
 public static void Window(Window w){w.FontFamily=new FontFamily("Microsoft YaHei UI");w.FontSize=13;w.Foreground=Ink;w.Background=Paper;w.WindowStartupLocation=WindowStartupLocation.CenterScreen;}
 public static Border Card(UIElement child,Thickness pad){return new Border{Background=Brushes.White,CornerRadius=new CornerRadius(16),BorderBrush=Line,BorderThickness=new Thickness(1),Padding=pad,Child=child};}
 public static TextBox Box(string value,bool multi){return new TextBox{Text=value??"",Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,5,0,12),BorderBrush=Line,Background=Brushes.White,Foreground=Ink,BorderThickness=new Thickness(1),AcceptsReturn=multi,TextWrapping=multi?TextWrapping.Wrap:TextWrapping.NoWrap,VerticalScrollBarVisibility=multi?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden};}
 public static void Label(Panel p,string title,string hint){p.Children.Add(Text(title,13,Ink));if(hint!=null){var t=Text(hint,11,Muted);t.Margin=new Thickness(0,4,0,2);p.Children.Add(t);}}
}
public sealed class ChatWindow : Window {
 readonly DesktopApp app;readonly StackPanel messages=new StackPanel();readonly ScrollViewer scroll;readonly TextBox input;readonly TextBlock status,connection;
 readonly Button send,clear,settings;bool busy;bool dismissing;
 public ChatWindow(DesktopApp a){app=a;Theme.Window(this);Title="霖铃 · 对话记录";Width=470;Height=660;MinWidth=370;MinHeight=440;
  var root=new Grid{Margin=new Thickness(20)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=root;
  var header=new Grid{Margin=new Thickness(0,0,0,16)};header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(72)});header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var portrait=new Image{Source=a.Atlas[0],Width=64,Height=70};RenderOptions.SetBitmapScalingMode(portrait,BitmapScalingMode.NearestNeighbor);header.Children.Add(portrait);
  var titles=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,0,0)};titles.Children.Add(Theme.Text("霖铃",26,Theme.Ink));connection=Theme.Text("桌面上的一位小伙伴",11,Theme.Muted);titles.Children.Add(connection);Grid.SetColumn(titles,1);header.Children.Add(titles);
  settings=Theme.Button("设置",false);settings.VerticalAlignment=VerticalAlignment.Center;settings.Click+=(s,e)=>app.OpenSettings();var headerButtons=new StackPanel();headerButtons.Children.Add(settings);var memories=Theme.Button("记忆",false);memories.Click+=(s,e)=>new MemoryWindow(app).Show();headerButtons.Children.Add(memories);Grid.SetColumn(headerButtons,2);header.Children.Add(headerButtons);root.Children.Add(header);
  scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=messages,Padding=new Thickness(0,0,5,0)};Grid.SetRow(scroll,1);root.Children.Add(scroll);
  var bottom=new StackPanel{Margin=new Thickness(0,14,0,0)};status=Theme.Text("在这里，慢慢聊。",11,Theme.Muted);status.Margin=new Thickness(2,0,0,7);bottom.Children.Add(status);
  input=Theme.Box("",true);input.Height=76;input.Margin=new Thickness(0,0,0,8);input.ToolTip="Enter 发送 · Shift+Enter 换行";input.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Shift)==0){e.Handled=true;if(!busy)Send();}};bottom.Children.Add(input);
  var actions=new Grid();actions.ColumnDefinitions.Add(new ColumnDefinition());actions.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});clear=Theme.Button("记忆与记录",false);clear.HorizontalAlignment=HorizontalAlignment.Left;clear.Click+=(s,e)=>{new MemoryWindow(app).Show();};actions.Children.Add(clear);
  send=Theme.Button("发送  ↗",true);send.MinWidth=100;send.Click+=(s,e)=>{if(busy){if(app.Request!=null)app.Request.Cancel();}else Send();};Grid.SetColumn(send,1);actions.Children.Add(send);bottom.Children.Add(actions);Grid.SetRow(bottom,2);root.Children.Add(bottom);bottom.Visibility=Visibility.Collapsed;
  Welcome();foreach(var m in app.History)Bubble(m.content,m.role=="user");RefreshStatus();
  Closing+=(s,e)=>{if(!app.Quitting){e.Cancel=true;Hide();if(!dismissing){dismissing=true;}}};
 }
 void Welcome(){var box=new StackPanel();box.Children.Add(Theme.Text("铃声轻响，我在这里。",19,Theme.Ink));var t=Theme.Text("我是霖铃。可以聊聊今天的小事，也可以一起想办法。\n\n先到「设置」连接你的模型服务，然后开始聊天。离线时也可以和桌面上的我互动。",13,Theme.Muted);t.Margin=new Thickness(0,12,0,0);box.Children.Add(t);var card=Theme.Card(box,new Thickness(20));card.Margin=new Thickness(0,6,0,15);messages.Children.Add(card);}
 public void RefreshStatus(){connection.Text=string.IsNullOrWhiteSpace(app.Config.Model)?"尚未连接模型 · 桌宠互动可用":app.Config.Model;if(!busy)status.Text=string.IsNullOrWhiteSpace(app.Config.Model)?"在设置中填写 API 地址、模型名称与密钥。":"已配置模型 · Enter 发送，Shift+Enter 换行";}
 TextBox Bubble(string text,bool user){var stack=new StackPanel();var label=Theme.Text(user?"你":"霖铃",11,Theme.Muted);label.Margin=new Thickness(0,0,0,5);stack.Children.Add(label);var body=new TextBox{Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=Theme.Ink,Padding=new Thickness(0),FontSize=14,AcceptsReturn=true,IsReadOnlyCaretVisible=false,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden};stack.Children.Add(body);var border=new Border{Child=stack,Padding=new Thickness(14),CornerRadius=new CornerRadius(13),Background=user?Theme.Sage:Brushes.White,BorderBrush=Theme.Line,BorderThickness=new Thickness(1),Margin=user?new Thickness(35,0,0,12):new Thickness(0,0,25,12)};messages.Children.Add(border);scroll.ScrollToEnd();return body;}
 public void Demo(){Bubble("今天也一起加油吧。",true);Bubble("好呀。我会在桌面上陪着你。累了就停一停，听一声小铃铛。",false);status.Text="预览 · 消息可选择、复制";}
 public void ClearDisplayed(){messages.Children.Clear();Welcome();}
 public void ShowGreeting(string text){Bubble("半小时的小问候 · "+text,false);}
 public void Submit(string question){if(busy)return;input.Text=question;Send();}
 async void Send(){
  string question=input.Text.Trim();if(busy||question.Length==0)return;
  if(question.Length>20000){status.Text="这条消息太长了，请控制在 20,000 字符以内。";return;}
  Settings config=app.Config.Copy();string key;
  try{config.Validate();if(string.IsNullOrWhiteSpace(config.Model)){app.OpenSettings();status.Text="请先填写模型名称。";return;}key=SettingsStore.Unprotect(config.EncryptedKey);}
  catch(Exception ex){status.Text="设置不可用："+ex.Message;app.OpenSettings();return;}
  app.Greetings.Cancel();app.Memories.Cancel();config.SystemOverride=Prompts.Compose(config,config.MemoryEnabled?app.Memory.Context():"")+"\n【最近主动问候，供理解用户回复，不是用户事实】\n"+app.Greetings.Last;app.Bubbles.Begin();
  input.Clear();Bubble(question,true);var response=Bubble("正在想一想…",false);var pending=new List<Message>(app.History);pending.Add(new Message("user",question));
  busy=true;input.IsEnabled=false;clear.IsEnabled=false;send.Content="停止回复";status.Text="霖铃正在思考 / 回复…";app.Pet.Busy(true);app.Request=new CancellationTokenSource();var request=app.Request;
  var text=new StringBuilder();var progress=new Progress<string>(delta=>{if(app.Request!=request)return;text.Append(delta);response.Text=text.ToString();app.Bubbles.SetText(response.Text);scroll.ScrollToEnd();});
  try{
   string answer=await app.Api.Complete(config,key,pending,delta=>((IProgress<string>)progress).Report(delta),request.Token);
   response.Text=answer;app.Bubbles.SetText(answer);app.History.Add(new Message("user",question));app.History.Add(new Message("assistant",answer));while(app.History.Count>40)app.History.RemoveRange(0,2);
   status.Text="回复完成 · 对话已保存到本机";try{app.Memory.Add(question,answer,"complete");}catch{status.Text="回复完成，但本地记录写入失败。请检查磁盘空间。";}app.Pet.Play(3);
  }catch(OperationCanceledException){response.Text=text.Length==0?"已停止。你可以重新问我。":text.ToString()+"\n\n［已停止，未加入后续对话上下文］";status.Text="已停止回复";try{app.Memory.Add(question,text.ToString(),"cancelled");}catch{}}
  catch(Exception ex){response.Text=(text.Length>0?text.ToString()+"\n\n":"")+"这次没能完成回复。\n"+ex.Message;status.Text="连接或回复失败 · 可在设置中检查连接";app.Pet.Play(5);input.Text=question;try{app.Memory.Add(question,text.ToString(),"failed");}catch{}}
  finally{app.Bubbles.SetText(response.Text);app.Bubbles.End(status.Text);request.Dispose();if(app.Request==request)app.Request=null;busy=false;app.Pet.Busy(false);input.IsEnabled=true;clear.IsEnabled=true;send.Content="发送  ↗";if(IsVisible)input.Focus();scroll.ScrollToEnd();app.Memories.Run(false);}
 }
}
public sealed class SettingsWindow : Window {
 readonly DesktopApp app;TextBox url,model,persona,personality,speech,knowledge,every,size,duration,speed,min,max,timeout,bubbleSeconds,blinkSeconds;PasswordBox key;
 CheckBox stream,top,follow,random,startup,memory,effects,fullscreen;ComboBox companion;TextBlock message;Button save,test;bool testing;CancellationTokenSource testCancel;
 public SettingsWindow(DesktopApp a){app=a;Theme.Window(this);Title="霖铃 · 设置";Width=570;Height=750;MinWidth=480;MinHeight=520;
  var root=new Grid{Margin=new Thickness(24)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=root;
  var head=new StackPanel{Margin=new Thickness(0,0,0,16)};head.Children.Add(Theme.Text("让霖铃更懂你",24,Theme.Ink));var sub=Theme.Text("模型连接与桌面陪伴，都在这里。",12,Theme.Muted);sub.Margin=new Thickness(0,6,0,0);head.Children.Add(sub);root.Children.Add(head);
  var tabs=new TabControl{Background=Brushes.Transparent,BorderBrush=Theme.Line};Grid.SetRow(tabs,1);root.Children.Add(tabs);
  var api=new StackPanel{Margin=new Thickness(16)};var pet=new StackPanel{Margin=new Thickness(16)};
  tabs.Items.Add(new TabItem{Header="模型连接",Padding=new Thickness(15,8,15,8),Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=api}});
  tabs.Items.Add(new TabItem{Header="陪伴与动画",Padding=new Thickness(15,8,15,8),Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=pet}});
  Theme.Label(api,"API 地址","支持 OpenAI 兼容 Chat Completions。填基础地址（通常含 /v1），或完整 /chat/completions 地址。");url=Theme.Box(a.Config.BaseUrl,false);api.Children.Add(url);
  Theme.Label(api,"模型名称","填写服务商提供的完整模型 ID。");model=Theme.Box(a.Config.Model,false);api.Children.Add(model);
  Theme.Label(api,"API Key","使用 Windows 当前账户加密保存；不会写入日志。无密钥的本机服务可留空。");key=new PasswordBox{Padding=new Thickness(10),Margin=new Thickness(0,5,0,12),BorderBrush=Theme.Line};try{key.Password=SettingsStore.Unprotect(a.Config.EncryptedKey);}catch{key.ToolTip="原密钥无法解密，请重新填写。";}api.Children.Add(key);
  stream=Check(api,"逐字显示回复（流式）",a.Config.Stream);
  Theme.Label(api,"等待回复超时（秒）","10–600 秒。深度思考模型可适当增加。");timeout=Theme.Box(a.Config.TimeoutSeconds.ToString(),false);api.Children.Add(timeout);
  var role=new StackPanel{Margin=new Thickness(16)};tabs.Items.Add(new TabItem{Header="人设提示词",Padding=new Thickness(12,8,12,8),Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=role}});
  Theme.Label(role,"人设 / 身份","四栏会一同作为每次回答的系统设定，固定人设优先于成长补充。");persona=Theme.Box(a.Config.Persona,true);persona.Height=110;role.Children.Add(persona);
  Theme.Label(role,"性格","情绪、价值观与相处方式。");personality=Theme.Box(a.Config.Personality,true);personality.Height=90;role.Children.Add(personality);
  Theme.Label(role,"说话方式","语气、长度、口癖与表达偏好。");speech=Theme.Box(a.Config.Speech,true);speech.Height=90;role.Children.Add(speech);
  Theme.Label(role,"通用知识 / 世界观","你希望霖铃始终知道的背景知识。");knowledge=Theme.Box(a.Config.Knowledge,true);knowledge.Height=120;role.Children.Add(knowledge);
  var preview=Theme.Button("预览实际角色提示词",false);preview.Click+=(s,e)=>{try{var w=new Window{Title="霖铃 · 提示词预览",Width=600,Height=620};Theme.Window(w);var t=Theme.Box(PreviewPrompt(),true);t.IsReadOnly=true;w.Content=t;w.Show();}catch(Exception ex){message.Text=ex.Message;}};role.Children.Add(preview);
  var mem=new StackPanel{Margin=new Thickness(16)};tabs.Items.Add(new TabItem{Header="记忆",Padding=new Thickness(12,8,12,8),Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=mem}});
  memory=Check(mem,"启用记忆学习与回忆",a.Config.MemoryEnabled);Theme.Label(mem,"每隔几轮完整对话整理一次（1–6）","使用当前 API 和模型额外调用一次；新消息优先。关闭后仍保留本地记录，但不会整理或发送记忆。");every=Theme.Box(a.Config.MemoryEvery.ToString(),false);mem.Children.Add(every);
   mem.Children.Add(Theme.Text("原始对话 → 短期摘要 → 近期日记 / 长期记忆；新提炼事实可查看来源并确认或驳回。摘要只代表模型的整理，可能有误。",12,Theme.Muted));
  var manage=Theme.Button("查看 / 编辑记忆与对话记录",false);manage.Click+=(s,e)=>new MemoryWindow(app).Show();mem.Children.Add(manage);
  mem.Children.Add(Theme.Text("保存位置："+app.Store.DirectoryPath+"\n记录以明文留在当前 Windows 用户目录。聊天会发送近期对话与启用的记忆；整理记忆也会发送相关记录到你配置的服务。API Key 独立使用 Windows 加密。",11,Theme.Muted));
  test=Theme.Button("测试连接 · 发送一次短消息",false);test.HorizontalAlignment=HorizontalAlignment.Left;test.Click+=(s,e)=>Test();api.Children.Add(test);
  var note=Theme.Text("测试和聊天会向所填 API 服务发送内容，并可能产生该服务的调用费用。设置修改后点击「保存」。",11,Theme.Muted);note.Margin=new Thickness(0,6,0,0);api.Children.Add(note);
   effects=Check(pet,"显示透明颜文字与状态动效",a.Config.Effects);top=Check(pet,"始终置顶",a.Config.Topmost);follow=Check(pet,"跟随鼠标转头",a.Config.FollowMouse);random=Check(pet,"随机眨眼、思考、观察、挥手和睡觉",a.Config.RandomActions);startup=Check(pet,"登录 Windows 时启动",!a.Smoke&&Startup.Enabled);
   Theme.Label(pet,"主动陪伴强度","安静：不主动问候或随机表演；标准：约每 30 分钟问候；活泼：约每 20 分钟问候、随机动作更频繁。点击、拖动和聊天始终可用。");companion=new ComboBox{Margin=new Thickness(0,5,0,12),Height=34};companion.Items.Add("安静");companion.Items.Add("标准");companion.Items.Add("活泼");companion.SelectedIndex=a.Config.CompanionLevel;pet.Children.Add(companion);fullscreen=Check(pet,"全屏应用运行时暂停主动陪伴",a.Config.PauseInFullscreen);
  Theme.Label(pet,"宠物宽度（80–384）",null);size=Theme.Box(a.Config.Size.ToString(CultureInfo.InvariantCulture),false);pet.Children.Add(size);
  Theme.Label(pet,"互动 / 随机动作持续时间（1–30 秒）","聊天思考会持续到回复结束或取消，不受此时长限制。");duration=Theme.Box(a.Config.ActionSeconds.ToString(CultureInfo.InvariantCulture),false);pet.Children.Add(duration);
  Theme.Label(pet,"动画速度（0.25–3 倍）","底部坐姿最多按 1 倍播放，保持轻缓；眨眼独立计时。");speed=Theme.Box(a.Config.Speed.ToString(CultureInfo.InvariantCulture),false);pet.Children.Add(speed);
  Theme.Label(pet,"回复气泡停留时间（3–300 秒）","回复完成后计时；鼠标悬停阅读时暂停。隐藏后仍可从记录查看。");bubbleSeconds=Theme.Box(a.Config.BubbleSeconds.ToString(CultureInfo.InvariantCulture),false);pet.Children.Add(bubbleSeconds);
  Theme.Label(pet,"眨眼平均间隔（2–20 秒）","实际间隔随机浮动 20%，不受动画速度影响。");blinkSeconds=Theme.Box(a.Config.BlinkSeconds.ToString(CultureInfo.InvariantCulture),false);pet.Children.Add(blinkSeconds);
  Theme.Label(pet,"随机动作间隔：最短 / 最长（5–300 秒）",null);var interval=new Grid();interval.ColumnDefinitions.Add(new ColumnDefinition());interval.ColumnDefinitions.Add(new ColumnDefinition());min=Theme.Box(a.Config.RandomMin.ToString(CultureInfo.InvariantCulture),false);min.Margin=new Thickness(0,5,5,12);max=Theme.Box(a.Config.RandomMax.ToString(CultureInfo.InvariantCulture),false);Grid.SetColumn(max,1);interval.Children.Add(min);interval.Children.Add(max);pet.Children.Add(interval);
   var guide=Theme.Text("单击挥手 · 双击跳跃 · 按住拖动\n右键菜单可以聊天、设置、隐藏和退出\n16 向平滑注视；上拖攀爬、下拖坠落。贴边松手进入探头 / 坐姿。\n右键动作预览时，任意处点击退出预览。\n鼠标停下后恢复眨眼；主动问候可能额外调用模型。",12,Theme.Muted);guide.Margin=new Thickness(0,10,0,0);pet.Children.Add(guide);
  var footer=new StackPanel{Margin=new Thickness(0,14,0,0)};message=Theme.Text(a.Store.Warning??"设置、对话与记忆保存在本机。",11,Theme.Muted);footer.Children.Add(message);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,0,0)};var cancel=Theme.Button("取消",false);cancel.Click+=(s,e)=>Close();save=Theme.Button("保存",true);save.Click+=(s,e)=>Save();buttons.Children.Add(cancel);buttons.Children.Add(save);footer.Children.Add(buttons);Grid.SetRow(footer,2);root.Children.Add(footer);
  Closed+=(s,e)=>{if(testCancel!=null)testCancel.Cancel();};
 }
 static CheckBox Check(Panel p,string title,bool value){var c=new CheckBox{Content=title,IsChecked=value,Margin=new Thickness(0,5,0,12),Foreground=Theme.Ink};p.Children.Add(c);return c;}
 static double Number(TextBox t,string name){double value;if(!double.TryParse(t.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||double.IsNaN(value)||double.IsInfinity(value))throw new ArgumentException(name+"需要填写有效数字。");return value;}
 string PreviewPrompt(){var c=Read();return Prompts.Compose(c,c.MemoryEnabled?app.Memory.Context():"");}
  Settings Read(){var c=app.Config.Copy();c.BaseUrl=url.Text.Trim();c.Model=model.Text.Trim();c.EncryptedKey=SettingsStore.Protect(key.Password.Trim());c.Persona=persona.Text;c.Personality=personality.Text;c.Speech=speech.Text;c.Knowledge=knowledge.Text;c.MemoryEnabled=memory.IsChecked==true;c.Effects=effects.IsChecked==true;c.MemoryEvery=(int)Number(every,"记忆间隔");c.Stream=stream.IsChecked==true;c.TimeoutSeconds=(int)Number(timeout,"超时时间");c.Topmost=top.IsChecked==true;c.FollowMouse=follow.IsChecked==true;c.RandomActions=random.IsChecked==true;c.CompanionLevel=companion.SelectedIndex;c.PauseInFullscreen=fullscreen.IsChecked==true;c.Size=Number(size,"大小");c.ActionSeconds=Number(duration,"时长");c.Speed=Number(speed,"速度");c.BubbleSeconds=Number(bubbleSeconds,"气泡停留时间");c.BlinkSeconds=Number(blinkSeconds,"眨眼间隔");c.RandomMin=Number(min,"最短间隔");c.RandomMax=Number(max,"最长间隔");c.Validate();return c;}
 void Save(){try{app.SaveConfig(Read(),startup.IsChecked==true);Close();}catch(Exception ex){message.Foreground=Theme.Brush("#A44538");message.Text=ex.Message;}}
 async void Test(){if(testing)return;Settings c;try{c=Read();if(string.IsNullOrWhiteSpace(c.Model))throw new ArgumentException("请填写模型名称。");}catch(Exception ex){message.Text=ex.Message;return;}
  testing=true;test.IsEnabled=false;save.IsEnabled=false;message.Text="正在连接…";testCancel=new CancellationTokenSource();
  try{string answer=await app.Api.Complete(c,key.Password.Trim(),new List<Message>{new Message("user","请只回复：连接成功")},null,testCancel.Token);message.Text="连接成功："+(answer.Length>90?answer.Substring(0,90)+"…":answer);message.Foreground=Theme.Deep;}
  catch(OperationCanceledException){message.Text="测试已取消。";}catch(Exception ex){message.Text=ex.Message;message.Foreground=Theme.Brush("#A44538");}
  finally{testing=false;test.IsEnabled=true;save.IsEnabled=true;testCancel.Dispose();testCancel=null;}
 }
}
}

