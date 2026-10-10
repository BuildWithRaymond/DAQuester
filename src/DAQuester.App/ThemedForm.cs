using System.Drawing.Drawing2D;

namespace DAQuester.App;

public class ThemedForm:Form {
 const int CaptionHeight=38;
 bool chromeReady;
 DarkCaption? caption;
 internal void InitializeChrome(){
  if(chromeReady)return;chromeReady=true;
  bool resize=FormBorderStyle==FormBorderStyle.Sizable||FormBorderStyle==FormBorderStyle.SizableToolWindow;
  var intendedClient=ClientSize;
  FormBorderStyle=FormBorderStyle.None;
  ClientSize=new(intendedClient.Width,intendedClient.Height+CaptionHeight);
  Padding=new(Padding.Left,Padding.Top+CaptionHeight,Padding.Right,Padding.Bottom);
  caption=new DarkCaption(this,resize){Bounds=new(0,0,ClientSize.Width,CaptionHeight),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
  Controls.Add(caption);caption.BringToFront();
  TextChanged+=(_,_)=>caption.Invalidate();
 }
 protected override void WndProc(ref Message m){
  const int hitTest=0x84;
  if(m.Msg==hitTest&&chromeReady&&caption?.Resizable==true&&WindowState==FormWindowState.Normal){
   var screen=new Point(unchecked((short)(long)m.LParam),unchecked((short)((long)m.LParam>>16)));var p=PointToClient(screen);const int edge=5;
   bool left=p.X<edge,right=p.X>=ClientSize.Width-edge,top=p.Y<edge,bottom=p.Y>=ClientSize.Height-edge;
   int hit=top&&left?13:top&&right?14:bottom&&left?16:bottom&&right?17:left?10:right?11:top?12:bottom?15:0;
   if(hit!=0){m.Result=(IntPtr)hit;return;}
  }
  base.WndProc(ref m);
 }
 internal sealed class DarkCaption:Control {
  readonly ThemedForm form;public bool Resizable {get;}
  int hover=-1;Point dragStart;Point origin;bool dragging;
  public DarkCaption(ThemedForm form,bool resizable){this.form=form;Resizable=resizable;DoubleBuffered=true;BackColor=Theme.Ink;TabStop=false;AccessibleName="Window title bar";}
  int ActionAt(int x)=>x>=Width-45?2:x>=Width-90&&form.MaximizeBox?1:x>=Width-135&&form.MinimizeBox?0:-1;
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging&&form.WindowState==FormWindowState.Normal){var current=MousePosition;form.Location=new(origin.X+current.X-dragStart.X,origin.Y+current.Y-dragStart.Y);}int target=ActionAt(e.X);if(target!=hover){hover=target;Invalidate();}}
  protected override void OnMouseLeave(EventArgs e){hover=-1;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&ActionAt(e.X)<0){dragStart=MousePosition;origin=form.Location;dragging=true;Capture=true;}}
  protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);bool moved=dragging;dragging=false;Capture=false;if(e.Button!=MouseButtons.Left||moved)return;switch(ActionAt(e.X)){case 0:form.WindowState=FormWindowState.Minimized;break;case 1:ToggleMaximize();break;case 2:form.Close();break;}}
  protected override void OnDoubleClick(EventArgs e){base.OnDoubleClick(e);if(form.MaximizeBox)ToggleMaximize();}
  void ToggleMaximize(){form.MaximizedBounds=Screen.FromControl(form).WorkingArea;form.WindowState=form.WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using var rule=new Pen(Theme.Line);g.DrawLine(rule,0,Height-1,Width,Height-1);
   using var gold=new Pen(Theme.Gold);Point[] diamond=[new(18,11),new(24,18),new(18,25),new(12,18)];g.DrawPolygon(gold,diamond);
   using var font=Theme.Body(9);TextRenderer.DrawText(g,form.Text,font,new Rectangle(34,0,Math.Max(20,Width-185),Height),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   for(int i=0;i<3;i++){if(i==0&&!form.MinimizeBox||i==1&&!form.MaximizeBox)continue;int x=Width-135+i*45;if(i==2)x=Width-45;
    if(hover==i){using var fill=new SolidBrush(i==2?Color.FromArgb(82,40,49):Theme.Raised);g.FillRectangle(fill,x,0,45,Height-1);}
    using var pen=new Pen(i==2&&hover==i?Theme.Rose:Theme.Muted,1.2f);int cx=x+22,cy=Height/2;
    if(i==0)g.DrawLine(pen,cx-5,cy+3,cx+5,cy+3);else if(i==1)g.DrawRectangle(pen,cx-5,cy-5,10,10);else{g.DrawLine(pen,cx-4,cy-4,cx+4,cy+4);g.DrawLine(pen,cx+4,cy-4,cx-4,cy+4);}
   }
  }
 }
}

internal sealed class InputField:Panel {
 readonly Control input;
 public InputField(Control input){this.input=input;DoubleBuffered=true;BackColor=Theme.Raised;Dock=DockStyle.Fill;Size=new(160,36);Margin=new(0,0,0,5);input.Dock=DockStyle.None;Controls.Add(input);input.GotFocus+=(_,_)=>Invalidate();input.LostFocus+=(_,_)=>Invalidate();}
 public override Size GetPreferredSize(Size proposedSize)=>new(160,36);
 protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(input is null)return;bool multiline=input is TextBox t&&t.Multiline;int height=multiline?Math.Max(1,Height-20):input is NumericUpDown?26:input.PreferredSize.Height;input.SetBounds(12,multiline?10:Math.Max(2,(Height-height)/2),Math.Max(1,Width-24),height);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using var pen=new Pen(input.Focused?Theme.Gold:Theme.Line);e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);}
}

internal sealed class JournalTextBox:TextBox {
 protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg is not (0xF or 0x317 or 0x318))return;if(TextLength!=0||PlaceholderText.Length==0)return;if(m.Msg==0xF){using var g=Graphics.FromHwnd(Handle);DrawHint(g);}else if(m.WParam!=IntPtr.Zero){using var g=Graphics.FromHdc(m.WParam);DrawHint(g);}}
 void DrawHint(Graphics g)=>TextRenderer.DrawText(g,PlaceholderText,Font,ClientRectangle,Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
}

internal sealed class JournalComboBox:ComboBox {
 Form? popup;
 public JournalComboBox(){DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=27;DropDownStyle=ComboBoxStyle.DropDownList;FlatStyle=FlatStyle.Flat;BackColor=Theme.Raised;ForeColor=Theme.Text;}
 protected override void WndProc(ref Message m){
  if(m.Msg==0x201){ShowPopup();m.Result=IntPtr.Zero;return;}
  if(m.Msg==0x202){m.Result=IntPtr.Zero;return;}
  if((m.Msg==0x100&&(int)m.WParam==115)||(m.Msg==0x104&&(int)m.WParam==40)){ShowPopup();m.Result=IntPtr.Zero;return;}
  base.WndProc(ref m);
  if(m.Msg==0xF){using var g=Graphics.FromHwnd(Handle);PaintSelected(g);}
  else if((m.Msg==0x318||m.Msg==0x317)&&m.WParam!=IntPtr.Zero){using var g=Graphics.FromHdc(m.WParam);PaintSelected(g);}
 }
 internal Form? Popup=>popup;
 internal void ShowPopup(){
  if(!Enabled||Items.Count==0)return;if(popup is {IsDisposed:false}){popup.Close();return;}
  int itemHeight=36;var screen=Screen.FromControl(this).WorkingArea;int popupHeight=Math.Min(Items.Count,8)*itemHeight+4;var point=PointToScreen(new(0,Height+3));if(point.Y+popupHeight>screen.Bottom)point.Y=PointToScreen(Point.Empty).Y-popupHeight-3;
  popup=new Form{FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=point,ClientSize=new(Width,popupHeight),BackColor=Theme.Line,Padding=new(2),KeyPreview=true};
  var scroller=new DarkScrollPanel{Dock=DockStyle.Fill,BackColor=Theme.Surface};var options=new TableLayoutPanel{AutoSize=true,ColumnCount=1,RowCount=Items.Count,BackColor=Theme.Surface,Margin=Padding.Empty};options.ColumnStyles.Add(new(SizeType.Percent,100));
  for(int i=0;i<Items.Count;i++){int index=i;options.RowStyles.Add(new(SizeType.Absolute,itemHeight));var button=Theme.Button(GetItemText(Items[i])??"");button.Quiet=true;button.Active=i==SelectedIndex;button.Dock=DockStyle.Fill;button.Margin=Padding.Empty;button.Click+=(_,_)=>{SelectedIndex=index;popup?.Close();Focus();Invalidate();};options.Controls.Add(button,0,i);}
  scroller.Controls.Add(options);popup.Controls.Add(scroller);popup.Deactivate+=(_,_)=>popup?.Close();popup.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){popup?.Close();Focus();e.Handled=true;}};popup.Show(FindForm());
  if(SelectedIndex>=0)options.Controls[SelectedIndex].Focus();
 }
 protected override void Dispose(bool disposing){if(disposing)popup?.Dispose();base.Dispose(disposing);}
 void PaintSelected(Graphics g){
  using var fill=new SolidBrush(Theme.Raised);g.FillRectangle(fill,ClientRectangle);using var line=new Pen(Focused?Theme.Gold:Theme.Line);g.DrawRectangle(line,0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));
  string value=SelectedItem is null?Text:GetItemText(SelectedItem)??"";
  TextRenderer.DrawText(g,value,Font,new Rectangle(11,0,Math.Max(0,Width-44),Height),Enabled?Theme.Text:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  g.SmoothingMode=SmoothingMode.AntiAlias;using var pen=new Pen(Theme.Muted,1.5f);int cx=Width-17,cy=Height/2;g.DrawLine(pen,cx-4,cy-2,cx,cy+2);g.DrawLine(pen,cx,cy+2,cx+4,cy-2);
 }
}

internal sealed class JournalCheckBox:CheckBox {
 public JournalCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;ForeColor=Theme.Muted;Cursor=Cursors.Hand;}
 public override Size GetPreferredSize(Size proposedSize){var text=TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.NoPadding);return new(text.Width+30,Math.Max(23,text.Height+6));}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(Parent?.BackColor??Theme.Ink);int y=(Height-15)/2;using var fill=new SolidBrush(Checked?Theme.Gold:Theme.Raised);using var pen=new Pen(Enabled?Theme.Line:Theme.Raised);e.Graphics.FillRectangle(fill,0,y,14,14);e.Graphics.DrawRectangle(pen,0,y,14,14);
  if(Checked){using var tick=new Pen(Theme.Ink,2);e.Graphics.DrawLines(tick,[new(3,y+7),new(6,y+10),new(11,y+4)]);}
  TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(23,0,Math.Max(0,Width-23),Height),Enabled?ForeColor:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  if(Focused){using var focus=new Pen(Theme.Gold);e.Graphics.DrawRectangle(focus,0,0,Width-1,Height-1);}
 }
}
