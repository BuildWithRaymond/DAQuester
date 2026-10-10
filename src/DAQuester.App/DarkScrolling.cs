using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DAQuester.App;

internal sealed class DarkScrollBar:Control {
 public int Maximum,Value,Viewport=1,Total=1;
 public Action<int>? Changed;
 bool dragging;int dragOffset;
 public DarkScrollBar(){DoubleBuffered=true;Width=12;BackColor=Theme.Ink;TabStop=true;AccessibleRole=AccessibleRole.ScrollBar;AccessibleName="Scroll";Cursor=Cursors.Hand;}
 int ThumbHeight=>Math.Min(Height,Math.Max(28,(int)(Height*Math.Min(1,Viewport/(double)Math.Max(1,Total)))));
 int ThumbTop=>Maximum<=0?0:(int)((Height-ThumbHeight)*Value/(double)Maximum);
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;if(Maximum<=0)return;using var fill=new SolidBrush(dragging?Theme.Gold:Theme.Line);using var path=JournalButton.Rounded(new(3,ThumbTop,Math.Max(3,Width-6),ThumbHeight),3);e.Graphics.FillPath(fill,path);}
 void SetPosition(int y){int position=(int)Math.Round(Math.Clamp(y,0,Math.Max(0,Height-ThumbHeight))*Maximum/(double)Math.Max(1,Height-ThumbHeight));Changed?.Invoke(position);Invalidate();}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();dragging=true;Capture=true;dragOffset=e.Y>=ThumbTop&&e.Y<=ThumbTop+ThumbHeight?e.Y-ThumbTop:ThumbHeight/2;SetPosition(e.Y-dragOffset);}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)SetPosition(e.Y-dragOffset);}
 protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnMouseWheel(MouseEventArgs e){Changed?.Invoke(Math.Clamp(Value-e.Delta/120*3,0,Maximum));base.OnMouseWheel(e);}
 protected override void OnKeyDown(KeyEventArgs e){int value=e.KeyCode switch{Keys.Down=>Value+1,Keys.Up=>Value-1,Keys.PageDown=>Value+Viewport,Keys.PageUp=>Value-Viewport,Keys.Home=>0,Keys.End=>Maximum,_=>Value};Changed?.Invoke(Math.Clamp(value,0,Maximum));e.Handled=true;base.OnKeyDown(e);}
}

internal sealed class DarkScrollPanel:Panel {
 readonly DarkScrollBar bar=new();Control? content;int offset;bool arranging;
 public DarkScrollPanel(){DoubleBuffered=true;AutoScroll=false;Controls.Add(bar);bar.Changed=value=>{offset=value;Arrange();};}
 protected override void OnControlAdded(ControlEventArgs e){base.OnControlAdded(e);if(e.Control is DarkScrollBar)return;content=e.Control;if(content is null)return;content.Dock=DockStyle.None;content.SizeChanged+=(_,_)=>Arrange();HookWheel(content);Arrange();}
 void HookWheel(Control control){control.MouseWheel+=(_,e)=>{if(e is HandledMouseEventArgs handled&&handled.Handled)return;ScrollBy(e.Delta);if(e is HandledMouseEventArgs h)h.Handled=true;};control.Enter+=(_,_)=>Reveal(control);foreach(Control child in control.Controls)HookWheel(child);}
 void Reveal(Control control){
  if(content is null||control==content||!control.Visible)return;
  var bounds=new Rectangle(content.PointToClient(control.PointToScreen(Point.Empty)),control.Size);
  if(bounds.Top<offset)offset=bounds.Top;
  else if(bounds.Bottom>offset+ClientSize.Height)offset=Math.Min(bounds.Top,bounds.Bottom-ClientSize.Height);
  Arrange();
 }
 protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Arrange();}
 protected override void OnMouseWheel(MouseEventArgs e){ScrollBy(e.Delta);if(e is HandledMouseEventArgs h)h.Handled=true;}
 void ScrollBy(int delta){offset=Math.Clamp(offset-delta/120*60,0,bar.Maximum);Arrange();}
 void Arrange(){if(arranging||content is null||Width<=0)return;arranging=true;try{int height=content.PreferredSize.Height;bool scroll=height>ClientSize.Height;bar.Visible=scroll;int width=Math.Max(1,ClientSize.Width-(scroll?16:0));content.Width=width;height=content.PreferredSize.Height;bar.Maximum=Math.Max(0,height-ClientSize.Height);offset=Math.Clamp(offset,0,bar.Maximum);content.SetBounds(0,-offset,width,height);bar.SetBounds(ClientSize.Width-12,0,12,ClientSize.Height);bar.Value=offset;bar.Viewport=ClientSize.Height;bar.Total=height;bar.BringToFront();bar.Invalidate();}finally{arranging=false;}}
}

internal sealed class DarkDataGridView:DataGridView {
 readonly HashSet<ScrollBar> hooked=[];
 readonly List<NativeDarkScrollBar> painters=[];
 public DarkDataGridView(){HookBars();}
 protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);HookBars();}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);HookBars();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);PaintCorner(e.Graphics);}
 protected override void WndProc(ref Message m){base.WndProc(ref m);if((m.Msg is 0x317 or 0x318)&&m.WParam!=IntPtr.Zero){using var g=Graphics.FromHdc(m.WParam);PaintCorner(g);}}
 void PaintCorner(Graphics g){var vertical=Controls.OfType<VScrollBar>().FirstOrDefault();var horizontal=Controls.OfType<HScrollBar>().FirstOrDefault();if(vertical?.Visible==true&&horizontal?.Visible==true){using var fill=new SolidBrush(Theme.Surface);g.FillRectangle(fill,vertical.Left,horizontal.Top,vertical.Width,horizontal.Height);}}
 void HookBars(){if(hooked is null)return;foreach(var bar in Controls.OfType<ScrollBar>())if(hooked.Add(bar)){bar.HandleCreated+=(_,_)=>painters.Add(new(bar));if(bar.IsHandleCreated)painters.Add(new(bar));}}
}

internal sealed class NativeDarkScrollBar:NativeWindow {
 readonly ScrollBar bar;
 public NativeDarkScrollBar(ScrollBar bar){this.bar=bar;AssignHandle(bar.Handle);}
 protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg is 0xF or 0x85){using var g=Graphics.FromHwnd(Handle);Paint(g);}else if((m.Msg is 0x317 or 0x318)&&m.WParam!=IntPtr.Zero){using var g=Graphics.FromHdc(m.WParam);Paint(g);}}
 void Paint(Graphics g){
  using var background=new SolidBrush(Theme.Surface);g.FillRectangle(background,bar.ClientRectangle);bool vertical=bar is VScrollBar;int length=vertical?bar.Height:bar.Width,thickness=vertical?bar.Width:bar.Height;
  int arrow=thickness,track=Math.Max(1,length-2*arrow),total=Math.Max(1,bar.Maximum-bar.Minimum+1),viewport=Math.Max(1,bar.LargeChange),maximum=Math.Max(1,total-viewport),thumb=Math.Min(track,Math.Max(24,track*viewport/total)),start=arrow+(track-thumb)*Math.Clamp(bar.Value-bar.Minimum,0,maximum)/maximum;
  g.SmoothingMode=SmoothingMode.AntiAlias;using var fill=new SolidBrush(Theme.Line);using var path=JournalButton.Rounded(vertical?new(4,start,Math.Max(3,thickness-8),thumb):new(start,4,thumb,Math.Max(3,thickness-8)),3);g.FillPath(fill,path);
  using var pen=new Pen(Theme.Muted);int cx=thickness/2;if(vertical){g.DrawLines(pen,[new(cx-3,10),new(cx,7),new(cx+3,10)]);g.DrawLines(pen,[new(cx-3,length-10),new(cx,length-7),new(cx+3,length-10)]);}else{g.DrawLines(pen,[new(10,cx-3),new(7,cx),new(10,cx+3)]);g.DrawLines(pen,[new(length-10,cx-3),new(length-7,cx),new(length-10,cx+3)]);}
 }
}

internal sealed class DarkListBox:ListBox {
 public DarkListBox(){BorderStyle=BorderStyle.None;BackColor=Theme.Surface;ForeColor=Theme.Muted;HorizontalScrollbar=false;}
 [DllImport("user32.dll")]static extern IntPtr GetWindowDC(IntPtr hwnd);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr hwnd,int index);
 protected override void WndProc(ref Message m){
  base.WndProc(ref m);if((GetWindowLong(Handle,-16)&0x200000)==0)return;
  if(m.Msg is 0xF or 0x85){var dc=GetWindowDC(Handle);if(dc==IntPtr.Zero)return;try{using var g=Graphics.FromHdc(dc);PaintScroll(g);}finally{ReleaseDC(Handle,dc);}}
  else if(m.Msg is 0x317 or 0x318&&m.WParam!=IntPtr.Zero){using var g=Graphics.FromHdc(m.WParam);PaintScroll(g);}
 }
 void PaintScroll(Graphics g){
  int width=SystemInformation.VerticalScrollBarWidth,x=Width-width;using var fill=new SolidBrush(Theme.Surface);g.FillRectangle(fill,x,0,width,Height);
  int visible=Math.Max(1,ClientSize.Height/Math.Max(1,ItemHeight)),track=Math.Max(1,Height-34),thumb=Math.Min(track,Math.Max(22,track*visible/Math.Max(1,Items.Count))),maximum=Math.Max(1,Items.Count-visible);
  int y=17+(track-thumb)*Math.Clamp(TopIndex,0,maximum)/maximum;using var ink=new SolidBrush(Theme.Line);g.FillRectangle(ink,x+4,y,Math.Max(3,width-8),thumb);using var arrow=new Pen(Theme.Muted);int cx=x+width/2;
  g.DrawLines(arrow,[new(cx-3,10),new(cx,7),new(cx+3,10)]);g.DrawLines(arrow,[new(cx-3,Height-10),new(cx,Height-7),new(cx+3,Height-10)]);
 }
}

internal sealed class JournalNumericUpDown:NumericUpDown {
 readonly StepButtons buttons;bool arranging;
 public JournalNumericUpDown(){BorderStyle=BorderStyle.None;buttons=new(this);Controls.Add(buttons);Arrange();}
 protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Arrange();}
 void Arrange(){if(arranging||buttons is null)return;arranging=true;try{foreach(Control c in Controls){if(c==buttons)continue;if(c is TextBox){c.SetBounds(8,Math.Max(1,(Height-c.Height)/2),Math.Max(1,Width-38),c.Height);}else c.Visible=false;}buttons.SetBounds(Math.Max(0,Width-26),0,26,Height);buttons.BringToFront();}finally{arranging=false;}}
 sealed class StepButtons(JournalNumericUpDown owner):Control {
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;if(e.Y<Height/2)owner.UpButton();else owner.DownButton();}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Theme.Raised);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var pen=new Pen(Theme.Muted,1.5f);int x=Width/2;int top=Height/4,bottom=Height*3/4;e.Graphics.DrawLines(pen,[new(x-3,top+2),new(x,top-1),new(x+3,top+2)]);e.Graphics.DrawLines(pen,[new(x-3,bottom-2),new(x,bottom+1),new(x+3,bottom-2)]);}
 }
}
