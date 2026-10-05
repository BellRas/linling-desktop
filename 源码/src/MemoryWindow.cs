using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Qingling {
public sealed class MemoryWindow:Window {
 readonly DesktopApp app;readonly TextBox pinned,shortTerm,longTerm,growth,diary,record;
 readonly TextBlock status;readonly ListBox list,facts;readonly TextBox factSource;bool dirty;
 public MemoryWindow(DesktopApp a){app=a;Theme.Window(this);Title="霖铃 · 记忆手册";Width=700;Height=760;MinWidth=540;MinHeight=520;
  var root=new DockPanel{Margin=new Thickness(20)};Content=root;
  var header=new StackPanel();header.Children.Add(Theme.Text("一起记住的小事",24,Theme.Ink));status=Theme.Text("",11,Theme.Muted);status.Margin=new Thickness(0,8,0,12);header.Children.Add(status);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  var actions=new WrapPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
  var save=Theme.Button("保存修改",true);save.Click+=(s,e)=>{try{app.Memories.Cancel();app.Memory.Edit(pinned.Text,shortTerm.Text,longTerm.Text,growth.Text);dirty=false;Refresh();}catch(Exception ex){status.Text=ex.Message;}};actions.Children.Add(save);
  var run=Theme.Button("立即整理",false);run.Click+=(s,e)=>{if(dirty){status.Text="请先保存或重新打开，避免整理覆盖未保存修改。";return;}app.Memories.Run(true);};actions.Children.Add(run);
  var forget=Theme.Button("清空记忆",false);forget.Click+=(s,e)=>Forget(false);actions.Children.Add(forget);
  var erase=Theme.Button("清空对话与记忆",false);erase.Click+=(s,e)=>Forget(true);actions.Children.Add(erase);
  var tabs=new TabControl();root.Children.Add(tabs);var fields=new StackPanel{Margin=new Thickness(14)};
  tabs.Items.Add(new TabItem{Header="记忆与成长",Padding=new Thickness(15,8,15,8),Content=new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});
  pinned=Field(fields,"固定记忆","你确认的重要信息，永不自动覆盖；可删除文字后保存。",110);
  shortTerm=Field(fields,"短期记忆","近期对话摘要，包含对话编号。",130);
  longTerm=Field(fields,"长期记忆","稳定偏好与共同经历；旧信息会随纠正更新。",150);
  growth=Field(fields,"成长补充","与固定人设共同使用，不能改写原始人设。",110);
  diary=Theme.Box("",true);diary.IsReadOnly=true;
  tabs.Items.Add(new TabItem{Header="日记归档",Padding=new Thickness(15,8,15,8),Content=diary});
  var history=new Grid{Margin=new Thickness(12)};history.RowDefinitions.Add(new RowDefinition{Height=new GridLength(180)});history.RowDefinitions.Add(new RowDefinition());
  list=new ListBox{BorderBrush=Theme.Line};history.Children.Add(list);record=Theme.Box("",true);record.IsReadOnly=true;Grid.SetRow(record,1);history.Children.Add(record);
  list.SelectionChanged+=(s,e)=>{var t=list.SelectedItem as TurnItem;if(t!=null)record.Text=t.Record.Time+"  ["+t.Record.Status+"]"+(t.Record.Status=="greeting"?"\n\n霖铃主动问候：\n"+t.Record.Answer:"\n\n你：\n"+t.Record.User+"\n\n霖铃：\n"+t.Record.Answer);};
  tabs.Items.Add(new TabItem{Header="完整对话",Padding=new Thickness(15,8,15,8),Content=history});
  var factPage=new Grid{Margin=new Thickness(12)};factPage.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});factPage.RowDefinitions.Add(new RowDefinition{Height=new GridLength(2,GridUnitType.Star)});factPage.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  facts=new ListBox{BorderBrush=Theme.Line};factPage.Children.Add(facts);factSource=Theme.Box("选择一条记忆查看原对话。",true);factSource.IsReadOnly=true;Grid.SetRow(factSource,1);factPage.Children.Add(factSource);
  facts.SelectionChanged+=(s,e)=>ShowFactSource();var review=new WrapPanel();foreach(var choice in new[]{new[]{"确认", "confirmed"},new[]{"驳回", "rejected"},new[]{"重新待核查", "pending"}}){string value=choice[1];var button=Theme.Button(choice[0],value=="confirmed");button.Click+=(s,e)=>Review(value);review.Children.Add(button);}Grid.SetRow(review,2);factPage.Children.Add(review);
  tabs.Items.Add(new TabItem{Header="可核查记忆",Padding=new Thickness(15,8,15,8),Content=factPage});
  Refresh();foreach(var t in new[]{pinned,shortTerm,longTerm,growth})t.TextChanged+=(s,e)=>dirty=true;
  app.Memory.Changed+=Refresh;Closed+=(s,e)=>app.Memory.Changed-=Refresh;
 }
 TextBox Field(Panel p,string label,string hint,double height){Theme.Label(p,label,hint);var t=Theme.Box("",true);t.Height=height;p.Children.Add(t);return t;}
 void Refresh(){status.Text=app.Memory.Status+" · 本地记录 "+app.Memory.Turns.Count+" 条 · 已整理至 #"+app.Memory.State.Through;
  if(!dirty){pinned.Text=app.Memory.State.Pinned;shortTerm.Text=app.Memory.State.Short;longTerm.Text=app.Memory.State.Long;growth.Text=app.Memory.State.Growth;dirty=false;}
  diary.Text="近期日记（最近三篇参与回答）\n\n"+string.Join("\n\n",app.Memory.State.Days.Select(x=>x.Date+"\n"+x.Text))+"\n\n归档日记（只供查阅，不随每次聊天发送）\n\n"+string.Join("\n\n",app.Memory.State.Archive.Select(x=>x.Date+"\n"+x.Text));
  list.ItemsSource=app.Memory.Turns.AsEnumerable().Reverse().Select(x=>new TurnItem{Record=x}).ToList();
  int selected=facts.SelectedItem is FactItem?((FactItem)facts.SelectedItem).Index:-1;facts.ItemsSource=app.Memory.State.Facts.Select((x,i)=>new FactItem{Fact=x,Index=i}).Reverse().ToList();if(selected>=0)facts.SelectedItem=facts.Items.Cast<FactItem>().FirstOrDefault(x=>x.Index==selected);
 }
 void ShowFactSource(){var item=facts.SelectedItem as FactItem;if(item==null){factSource.Text="选择一条记忆查看原对话。";return;}factSource.Text="状态："+item.Status+"\n记忆："+item.Fact.Text+"\n\n"+string.Join("\n\n",item.Fact.SourceIds.Select(id=>{var t=app.Memory.Source(id);return t==null?"#"+id+" 原对话已删除或不可用":"#"+id+" · "+t.Time+"\n你："+t.User+"\n霖铃："+t.Answer;}));}
 void Review(string value){var item=facts.SelectedItem as FactItem;if(item==null){status.Text="请先选择一条记忆。";return;}try{app.Memories.Cancel();app.Memory.ReviewFact(item.Index,value);Refresh();}catch(Exception ex){status.Text=ex.Message;}}
 void Forget(bool all){if(app.Request!=null){status.Text="请先停止当前回复，再清空记录。";return;}if(MessageBox.Show(all?"删除本机所有对话与生成的记忆？此操作不能撤销。":"清空固定、短期、长期记忆、日记和成长补充？旧对话不会再被自动学习。","霖铃",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
  try{app.Memories.Cancel();app.Memory.Forget(all);if(all){app.History.Clear();app.Chat.ClearDisplayed();app.Bubbles.SetText("我们从这里重新开始。 ");}dirty=false;Refresh();}catch(Exception ex){status.Text=ex.Message;}
 }
 sealed class TurnItem {public TurnRecord Record;public override string ToString(){return "#"+Record.Id+"  "+Record.Time.Substring(0,16).Replace('T',' ')+"  "+(Record.Status=="greeting"?"霖铃主动问候":Prompts.Cut(Record.User,38))+(Record.Status=="complete"||Record.Status=="greeting"?"":"  [未完成]");}}
 sealed class FactItem {public MemoryFact Fact;public int Index;public string Status{get{return Fact.Review=="confirmed"?"已确认":Fact.Review=="rejected"?"已驳回":"待核查";}}public override string ToString(){return Status+" · "+Prompts.Cut(Fact.Text,65)+"  [#"+string.Join(",#",Fact.SourceIds)+"]";}}
}
}
