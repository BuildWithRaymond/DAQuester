using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DAQuester.App;

internal static class Theme {
 public static readonly Color Ink=Color.FromArgb(13,19,30),Surface=Color.FromArgb(21,30,43),Raised=Color.FromArgb(28,40,56),Line=Color.FromArgb(45,59,77),Text=Color.FromArgb(237,240,243),Muted=Color.FromArgb(160,174,192),Gold=Color.FromArgb(217,184,121),Mint=Color.FromArgb(139,207,179),Rose=Color.FromArgb(229,151,154);
 public static Font Body(float size=10,FontStyle style=FontStyle.Regular)=>new("Segoe UI",size,style);
 public static Font Display(float size=26)=>new("Georgia",size);
 public static Label Label(string text,float size=10,Color? color=null,bool bold=false)=>new(){Text=text,ForeColor=color??Text,Font=Body(size,bold?FontStyle.Bold:FontStyle.Regular),AutoSize=false,BackColor=Color.Transparent};
 public static JournalButton Button(string text, bool primary=false)=>new(){Text=text,Primary=primary,Height=40,Width=132,Margin=new(0,0,8,0)};
 public static void Apply(Form form) {
  form.BackColor=Ink;form.ForeColor=Text;form.Font=Body();
  if(form is ThemedForm dark)dark.InitializeChrome();
  ApplyControls(form);form.HandleCreated+=(_,_)=>DarkChrome(form);
  if(form.IsHandleCreated)DarkChrome(form);
 }
 public static void ApplyControls(Control root) {
  foreach(Control c in root.Controls) {
   if(c is JournalButton or JournalArtwork or StageTrack)continue;
   if(c is DataGridView table)StyleGrid(table);
   else if(c is TextBoxBase text){text.BackColor=Raised;text.ForeColor=Text;text.BorderStyle=BorderStyle.None;if(text is TextBox box)box.ScrollBars=ScrollBars.None;}
   else if(c is ListBox list){list.BackColor=Surface;list.ForeColor=Muted;list.BorderStyle=BorderStyle.None;}
   else if(c is ComboBox combo){combo.BackColor=Raised;combo.ForeColor=Text;combo.FlatStyle=FlatStyle.Flat;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=27;combo.DrawItem+=DrawCombo;}
   else if(c is NumericUpDown number){number.BackColor=Raised;number.ForeColor=Text;number.BorderStyle=BorderStyle.None;}
   else if(c is Button button){button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=Line;button.FlatAppearance.MouseOverBackColor=Raised;button.BackColor=Raised;button.ForeColor=Text;button.Height=Math.Max(36,button.Height);}
   else if(c is CheckBox check){check.ForeColor=Muted;check.FlatStyle=FlatStyle.Flat;check.BackColor=Color.Transparent;}
   else if(c is SplitContainer split){split.BackColor=Line;split.Panel1.BackColor=Surface;split.Panel2.BackColor=Surface;}
   else if(c is Label label){if(label.ForeColor==SystemColors.ControlText)label.ForeColor=Text;label.BackColor=Color.Transparent;}
   else if(c.BackColor==SystemColors.Control||c.BackColor==Color.White)c.BackColor=Ink;
   ApplyControls(c);
  }
 }
 static void DrawCombo(object? sender,DrawItemEventArgs e) {
  var combo=(ComboBox)sender!;bool selected=(e.State&DrawItemState.Selected)!=0;
  using var brush=new SolidBrush(selected?Line:Raised);e.Graphics.FillRectangle(brush,e.Bounds);
  string value=e.Index>=0?combo.GetItemText(combo.Items[e.Index])??"":combo.Text;
  TextRenderer.DrawText(e.Graphics,value,combo.Font,Rectangle.Inflate(e.Bounds,-10,0),Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
 }
 public static void StyleGrid(DataGridView grid) {
  grid.BackgroundColor=Surface;grid.BorderStyle=BorderStyle.None;grid.GridColor=Line;grid.EnableHeadersVisualStyles=false;
  grid.RowHeadersVisible=false;grid.AllowUserToResizeRows=false;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
  grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;grid.ColumnHeadersHeight=36;
  grid.ColumnHeadersDefaultCellStyle=new(){BackColor=Surface,ForeColor=Muted,Font=Body(9),Padding=new(10,0,0,0),SelectionBackColor=Surface};
  grid.DefaultCellStyle=new(){BackColor=Surface,ForeColor=Text,SelectionBackColor=Raised,SelectionForeColor=Gold,Font=Body(10),Padding=new(10,0,0,0)};
  grid.AlternatingRowsDefaultCellStyle.BackColor=Surface;grid.RowTemplate.Height=45;grid.RowTemplate.MinimumHeight=42;foreach(DataGridViewRow row in grid.Rows)row.Height=45;
 }
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 static void DarkChrome(Form form){try{int dark=1;DwmSetWindowAttribute(form.Handle,20,ref dark,sizeof(int));}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
 public static void Error(IWin32Window? owner,string message,string title="DAQuester") {
  using var form=new ThemedForm{Text=title,ClientSize=new(500,300),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false,Padding=new(24)};
  var body=new TextBox{Text=message,Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};
  var close=Button("Got it",true);close.Dock=DockStyle.Bottom;close.DialogResult=DialogResult.OK;
  form.Controls.Add(body);form.Controls.Add(close);form.AcceptButton=close;form.CancelButton=close;Apply(form);form.ShowDialog(owner);
 }
}

internal sealed class JournalButton:Button {
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Primary {get;set;}
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Quiet {get;set;}
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Danger {get;set;}
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Active {get;set;}
 bool hovered;
 public JournalButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=Theme.Body(10,FontStyle.Bold);}
 protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  var fill=!Enabled?Theme.Raised:Primary?Theme.Gold:Active||hovered?Theme.Raised:Quiet?Parent?.BackColor??Theme.Ink:Theme.Surface;
  var ink=!Enabled?Theme.Muted:Primary?Theme.Ink:Danger?Theme.Rose:Active?Theme.Gold:Theme.Text;
  using var path=Rounded(new(1,1,Width-3,Height-3),7);using var brush=new SolidBrush(fill);e.Graphics.FillPath(brush,path);
  if(!Quiet||Focused){using var pen=new Pen(Focused?Theme.Gold:Primary&&Enabled?Theme.Gold:Theme.Line);e.Graphics.DrawPath(pen,path);}
  var rect=new Rectangle(Quiet?14:6,0,Width-(Quiet?20:12),Height);
  TextRenderer.DrawText(e.Graphics,Text,Font,rect,ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|(Quiet?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));
  if(Active){using var pen=new Pen(Theme.Gold,2);e.Graphics.DrawLine(pen,3,11,3,Height-11);}
 }
 internal static GraphicsPath Rounded(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}

internal sealed class JournalCard:Panel {
 public JournalCard(){DoubleBuffered=true;BackColor=Theme.Surface;Padding=new(22);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var path=JournalButton.Rounded(new(0,0,Width-1,Height-1),10);using var pen=new Pen(Theme.Line);e.Graphics.DrawPath(pen,path);}
}

internal sealed class JournalArtwork:Control {
 public JournalArtwork(){DoubleBuffered=true;BackColor=Theme.Surface;TabStop=false;AccessibleName="Celestial quest compass";}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float cx=Width/2f,cy=Height/2f,r=Math.Min(Width,Height)*.37f;
  using var faint=new Pen(Color.FromArgb(60,Theme.Gold));using var gold=new Pen(Color.FromArgb(150,Theme.Gold));using var fill=new SolidBrush(Color.FromArgb(90,Theme.Gold));
  g.DrawEllipse(faint,cx-r,cy-r,r*2,r*2);g.DrawEllipse(faint,cx-r*.8f,cy-r*.8f,r*1.6f,r*1.6f);
  g.DrawLine(faint,cx-r-16,cy,cx+r+16,cy);g.DrawLine(faint,cx,cy-r-16,cx,cy+r+16);
  for(int i=0;i<16;i++){double a=i*Math.PI/8;float inside=i%4==0?.83f:.94f;g.DrawLine(faint,cx+(float)Math.Cos(a)*r*inside,cy+(float)Math.Sin(a)*r*inside,cx+(float)Math.Cos(a)*r,cy+(float)Math.Sin(a)*r);}
  PointF[] star=[new(cx,cy-r*.68f),new(cx+9,cy-9),new(cx+r*.68f,cy),new(cx+9,cy+9),new(cx,cy+r*.68f),new(cx-9,cy+9),new(cx-r*.68f,cy),new(cx-9,cy-9)];g.FillPolygon(fill,star);g.DrawPolygon(gold,star);g.FillEllipse(fill,cx-3,cy-3,6,6);
  using var font=Theme.Body(7);TextRenderer.DrawText(g,"N",font,new Rectangle((int)cx-8,(int)(cy-r)-18,16,16),Theme.Gold,TextFormatFlags.HorizontalCenter);
 }
}

internal sealed class StageTrack:Control {
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public string[] Stages {get;set;}=[];
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public int Current {get;set;}
 [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]public bool Complete {get;set;}
 public StageTrack(){DoubleBuffered=true;BackColor=Theme.Surface;Height=74;TabStop=false;AccessibleName="Quest stages";}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);if(Stages.Length==0)return;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  float step=Width/(float)Stages.Length;
  for(int i=0;i<Stages.Length;i++){float x=step*(i+.5f);bool done=Complete||i<Current,active=i==Current&&!Complete;
   if(i<Stages.Length-1){using var line=new Pen(done?Color.FromArgb(100,Theme.Gold):Theme.Line,2);g.DrawLine(line,x+9,18,x+step-9,18);}
   using var brush=new SolidBrush(done?Theme.Gold:active?Theme.Raised:Theme.Surface);using var pen=new Pen(active||done?Theme.Gold:Theme.Line,active?2:1);g.FillEllipse(brush,x-6,12,12,12);g.DrawEllipse(pen,x-6,12,12,12);
   using var font=Theme.Body(8.5f);TextRenderer.DrawText(g,Stages[i],font,new Rectangle((int)(i*step),34,(int)step,36),active?Theme.Text:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak);
  }
 }
}
