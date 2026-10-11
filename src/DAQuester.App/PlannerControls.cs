using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace DAQuester.App;

internal sealed class PlannerStack:Panel {
 bool arranging;
 public PlannerStack(){DoubleBuffered=true;BackColor=Theme.Surface;}
 public override Size GetPreferredSize(Size proposedSize)=>new(proposedSize.Width>0?proposedSize.Width:Width,Padding.Vertical+Controls.Cast<Control>().Sum(c=>c.Height+c.Margin.Vertical));
 protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(arranging)return;arranging=true;try{int y=Padding.Top;foreach(Control c in Controls){if(!c.Visible)continue;y+=c.Margin.Top;c.SetBounds(Padding.Left+c.Margin.Left,y,Math.Max(1,ClientSize.Width-Padding.Horizontal-c.Margin.Horizontal),c.Height);y+=c.Height+c.Margin.Bottom;}}finally{arranging=false;}}
}

internal sealed class CharacterSeal:Control {
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public string Character {get;set;}="";
 public CharacterSeal(){DoubleBuffered=true;Size=new(38,42);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var shape=JournalButton.Rounded(new(1,1,Width-3,Height-3),6);using var fill=new SolidBrush(Color.FromArgb(34,37,32));using var pen=new Pen(Color.FromArgb(98,81,48));e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(pen,shape);using var font=Theme.Display(20);TextRenderer.DrawText(e.Graphics,Character.Length>0?Character[..1].ToUpperInvariant():"◇",font,ClientRectangle,Theme.Gold,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
}

internal sealed class PlannerBrand:Control {
 public PlannerBrand(){DoubleBuffered=true;Height=98;}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var pen=new Pen(Theme.Gold,1.4f);e.Graphics.DrawEllipse(pen,3,7,21,21);e.Graphics.DrawLines(pen,[new(8,23),new(13,11),new(20,14),new(8,23)]);using var font=Theme.Heading(17);TextRenderer.DrawText(e.Graphics,"DAQuester",font,new Rectangle(32,3,150,32),Theme.Text,TextFormatFlags.VerticalCenter);using var note=Theme.Body(7.5f);TextRenderer.DrawText(e.Graphics,"DARK AGES COMPANION",note,new Rectangle(4,42,185,28),Theme.Muted,TextFormatFlags.SingleLine);}
}

internal static class PlannerIcons {
 public static void Draw(Graphics g,string kind,Rectangle bounds,Color color){
  g.SmoothingMode=SmoothingMode.AntiAlias;using var p=new Pen(color,1.35f){StartCap=LineCap.Round,EndCap=LineCap.Round};float x=bounds.X,y=bounds.Y,w=bounds.Width,h=bounds.Height;
  if(kind.Contains("hairstyle")){g.DrawEllipse(p,x,y+h*.58f,w*.32f,h*.32f);g.DrawEllipse(p,x+w*.68f,y+h*.58f,w*.32f,h*.32f);g.DrawLine(p,x+w*.24f,y+h*.64f,x+w*.9f,y);g.DrawLine(p,x+w*.76f,y+h*.64f,x+w*.1f,y);}
  else if(kind.Contains("birth")){g.DrawRectangle(p,x,y,w,h);g.DrawLine(p,x+w/2,y,x+w/2,y+h);g.DrawLine(p,x+w*.18f,y+h*.3f,x+w*.38f,y+h*.3f);g.DrawLine(p,x+w*.63f,y+h*.3f,x+w*.84f,y+h*.3f);}
  else if(kind.Contains("faerie")){g.DrawLines(p,new PointF[]{new(x+w*.5f,y),new(x+w*.61f,y+h*.38f),new(x+w,y+h*.5f),new(x+w*.61f,y+h*.61f),new(x+w*.5f,y+h),new(x+w*.38f,y+h*.61f),new(x,y+h*.5f),new(x+w*.38f,y+h*.38f),new(x+w*.5f,y)});}
  else if(kind.Contains("stick")){g.DrawLine(p,x+w*.2f,y+h,x+w*.65f,y);g.DrawLine(p,x+w*.43f,y+h*.52f,x+w,y+h*.28f);g.DrawLine(p,x+w*.47f,y+h*.44f,x+w*.18f,y+h*.19f);}
  else if(kind.Contains("emerald")){g.DrawPolygon(p,new PointF[]{new(x+w*.25f,y),new(x+w*.75f,y),new(x+w,y+h*.35f),new(x+w*.5f,y+h),new(x,y+h*.35f)});g.DrawLine(p,x,y+h*.35f,x+w,y+h*.35f);g.DrawLine(p,x+w*.25f,y,x+w*.5f,y+h);g.DrawLine(p,x+w*.75f,y,x+w*.5f,y+h);}
  else{g.DrawRectangle(p,x+2,y,w-4,h);g.DrawLine(p,x+w*.25f,y+h*.3f,x+w*.75f,y+h*.3f);g.DrawLine(p,x+w*.25f,y+h*.55f,x+w*.75f,y+h*.55f);}
 }
}

internal sealed record PlannerEntry(string Id,string Name,string Reward,string Requirements,string Level,string Description,string Source,string? RecipeId);
internal sealed record PlannerAvailability(string State,string Reason,bool CanQueue){public bool Ready=>State is "Ready" or "Choices" or "Resume ready";}

internal sealed class PlannerQuestRow:Control {
 public PlannerEntry Entry {get;}
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public PlannerAvailability Availability {get;set;}
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public int QueuePosition {get;set;}
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Locked {get;set;}
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool SelectedQuest {get;set;}
 public event Action? Toggle;
 public event Action? Inspect;
 bool hovered;
 public PlannerQuestRow(PlannerEntry entry,PlannerAvailability availability){Entry=entry;Availability=availability;Height=78;Margin=Padding.Empty;TabStop=true;AccessibleRole=AccessibleRole.CheckButton;AccessibleName=entry.Name+", "+availability.State;AccessibleDescription=availability.Reason;SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;}
 protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(e.X<42){if(!Locked&&Availability.CanQueue)Toggle?.Invoke();}else Inspect?.Invoke();}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space){if(!Locked&&Availability.CanQueue)Toggle?.Invoke();e.Handled=true;}else if(e.KeyCode==Keys.Enter){Inspect?.Invoke();e.Handled=true;}else if(e.KeyCode is Keys.Down or Keys.Up){Parent?.SelectNextControl(this,e.KeyCode==Keys.Down,true,false,false);e.Handled=true;}}
 public void ToggleSelection(){if(!Locked&&Availability.CanQueue)Toggle?.Invoke();}
 protected override void OnPaint(PaintEventArgs e){
  bool disabled=Availability.State is "Unavailable" or "Blocked" or "Cooldown",completed=Availability.State=="Completed",queued=QueuePosition>0&&Availability.CanQueue;
  var surface=disabled?hovered?Theme.QuestUnavailableHover:Theme.QuestUnavailableSurface:Availability.Ready?hovered?Theme.QuestReadyHover:Theme.QuestReadySurface:hovered?Theme.Raised:queued?Color.FromArgb(28,36,38):Theme.Surface;
  var g=e.Graphics;g.Clear(surface);using var line=new Pen(Color.FromArgb(33,46,62));g.DrawLine(line,0,Height-1,Width,Height-1);
  if(SelectedQuest){using var marker=new SolidBrush(Theme.Gold);g.FillRectangle(marker,0,0,2,Height-1);}
  if(Availability.CanQueue){var box=new Rectangle(17,31,14,14);using var pen=new Pen(queued?Theme.Gold:Availability.Ready?Theme.Mint:Theme.Muted);g.DrawRectangle(pen,box);if(queued){using var fill=new SolidBrush(Theme.Gold);g.FillRectangle(fill,box);using var check=new Pen(Theme.Ink,1.5f);g.DrawLines(check,[new(20,38),new(23,41),new(28,34)]);}}
  else{using var pen=new Pen(completed?Theme.Mint:Theme.DisabledText);g.DrawEllipse(pen,18,32,13,13);if(completed)g.DrawLines(pen,[new(21,38),new(24,41),new(28,35)]);else g.DrawLine(pen,24,35,24,40);}
  g.SmoothingMode=SmoothingMode.AntiAlias;var icon=new Rectangle(45,20,36,37);using var shape=JournalButton.Rounded(icon,6);using var bg=new SolidBrush(disabled?Theme.QuestUnavailableSurface:queued?Color.FromArgb(38,41,31):Availability.Ready?Color.FromArgb(25,47,39):Theme.Raised);using var border=new Pen(disabled?Color.FromArgb(32,43,58):queued?Color.FromArgb(96,79,44):Availability.Ready?Color.FromArgb(47,77,64):Color.FromArgb(37,52,70));g.FillPath(bg,shape);g.DrawPath(border,shape);PlannerIcons.Draw(g,Entry.RecipeId??Entry.Id,new(54,29,18,19),disabled?Theme.DisabledText:queued?Theme.Gold:Availability.Ready?Theme.Mint:Theme.Muted);
  int stateWidth=116,textWidth=Math.Max(30,Width-98-stateWidth-16);using var title=Theme.Body(10.5f,FontStyle.Bold);using var small=Theme.Body(8.5f);
  TextRenderer.DrawText(g,Entry.Name,title,new Rectangle(95,18,textWidth,25),disabled?Theme.DisabledText:completed?Theme.Muted:Theme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
  string detail=!Availability.CanQueue&&Entry.RecipeId is not null?Availability.Reason:queued?"Saved progress travels with this character":Entry.Reward;
  TextRenderer.DrawText(g,detail,small,new Rectangle(95,45,textWidth,21),disabled?Theme.DisabledText:Theme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
  string state=QueuePosition>0&&Availability.CanQueue?"Queued  "+QueuePosition.ToString("00"):Availability.State;
  TextRenderer.DrawText(g,state,small,new Rectangle(Width-stateWidth-15,25,stateWidth,28),disabled?Theme.DisabledText:queued?Theme.Gold:Availability.Ready||completed?Theme.Mint:Theme.Muted,TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
  if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8),Theme.Gold,BackColor);
 }
}

internal sealed class PlannerQueueRow:Panel {
 public string Quest {get;}
 public int Index {get;}
 readonly string character,title,detail;
 readonly bool active;
 int insertion;
 Point down;
 public event Action<int>? MoveRequested;
 public event Action? Remove;
 public event Action<string,int>? Dropped;
 public PlannerQueueRow(string character,string quest,string title,string detail,int index,int total,bool locked,bool active){
  this.character=character;Quest=quest;this.title=title;this.detail=detail;Index=index;this.active=active;SetStyle(ControlStyles.Selectable,true);DoubleBuffered=true;Height=82;Margin=new(0,0,0,8);BackColor=Theme.Ink;AllowDrop=!locked;TabStop=true;AccessibleName=title+", queue position "+(index+1);AccessibleRole=AccessibleRole.ListItem;
  JournalButton Button(string text,string name,int x){var b=Theme.Button(text);b.Name=name;b.Compact=true;b.Font=Theme.Body(9);b.Size=new(32,30);b.Location=new(x,43);b.Anchor=AnchorStyles.Right|AnchorStyles.Top;b.AccessibleName=name+" "+title;b.Enabled=!locked;Controls.Add(b);return b;}
  var up=Button("↑","Move up",225);var bottom=Button("↓","Move down",254);var remove=Button("×","Remove",254);remove.Top=8;up.Enabled&=index>0;bottom.Enabled&=index<total-1;
  Resize+=(_,_)=>{up.Left=Width-77;bottom.Left=remove.Left=Width-39;};up.Click+=(_,_)=>MoveRequested?.Invoke(index-1);bottom.Click+=(_,_)=>MoveRequested?.Invoke(index+1);remove.Click+=(_,_)=>Remove?.Invoke();
  MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Left){Focus();down=e.Location;}};
  MouseMove+=(_,e)=>{if(locked||e.Button!=MouseButtons.Left||e.X>40&&down.X>40)return;if(Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)>6)DoDragDrop(new DataObject("DAQuester.Queue",character+"\n"+quest),DragDropEffects.Move);};
  DragEnter+=(_,e)=>e.Effect=Valid(e.Data)?DragDropEffects.Move:DragDropEffects.None;
  DragOver+=(_,e)=>{e.Effect=Valid(e.Data)?DragDropEffects.Move:DragDropEffects.None;insertion=PointToClient(new(e.X,e.Y)).Y<Height/2?-1:1;Invalidate();};
  DragLeave+=(_,_)=>{insertion=0;Invalidate();};
  DragDrop+=(_,e)=>{if(Valid(e.Data)){string id=((string)e.Data!.GetData("DAQuester.Queue")!).Split('\n')[1];Dropped?.Invoke(id,index+(insertion>0?1:0));}insertion=0;Invalidate();};
 }
 bool Valid(IDataObject? data)=>data?.GetData("DAQuester.Queue") is string text&&text.StartsWith(character+"\n",StringComparison.OrdinalIgnoreCase)&&text.Split('\n').Length==2;
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(!AllowDrop)return;if(e.Control&&e.KeyCode is Keys.Up or Keys.Down){MoveRequested?.Invoke(Index+(e.KeyCode==Keys.Up?-1:1));e.Handled=true;}else if(e.KeyCode==Keys.Delete){Remove?.Invoke();e.Handled=true;}}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var path=JournalButton.Rounded(new(1,1,Width-3,Height-3),7);using var fill=new SolidBrush(active?Color.FromArgb(36,40,31):Theme.Ink);using var pen=new Pen(active?Theme.Gold:Theme.Line);e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);using var dots=new SolidBrush(Theme.Muted);for(int x=14;x<21;x+=5)for(int y=21;y<36;y+=5)e.Graphics.FillEllipse(dots,x,y,1.7f,1.7f);using var small=Theme.Body(8);using var bold=Theme.Body(9,FontStyle.Bold);TextRenderer.DrawText(e.Graphics,(Index+1).ToString("00"),small,new Rectangle(30,14,23,24),Theme.Muted);TextRenderer.DrawText(e.Graphics,title,bold,new Rectangle(57,13,Math.Max(1,Width-95),28),Theme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);TextRenderer.DrawText(e.Graphics,detail,small,new Rectangle(31,48,Math.Max(1,Width-107),24),Theme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);if(insertion!=0){using var marker=new Pen(Theme.Gold,2);e.Graphics.DrawLine(marker,3,insertion<0?2:Height-3,Width-3,insertion<0?2:Height-3);}if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new(4,4,Width-8,Height-8),Theme.Gold,BackColor);}
}
