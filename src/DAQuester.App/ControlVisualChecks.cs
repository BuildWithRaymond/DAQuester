using System.Reflection;

namespace DAQuester.App;

// Synthetic controls only. This runs in the isolated --smoke process.
internal static class ControlVisualChecks {
 static readonly MethodInfo background=typeof(Control).GetMethod("OnPaintBackground",BindingFlags.Instance|BindingFlags.NonPublic)!;
 static readonly MethodInfo style=typeof(Control).GetMethod("GetStyle",BindingFlags.Instance|BindingFlags.NonPublic)!;

 internal static void Run(){
  var failures=new List<string>();
  foreach(var check in new Action[]{CheckCorners,CheckPointerStates,CheckKeyboardStates,CheckPartialPaint,CheckSmallSizes,CheckPropertyRepaint,CheckCardCorners,CheckCheckboxClip,CheckDialogKeyboard,CheckRepaintCycles,CheckPlannerStateColors,CheckSearchPlaceholder})
   try{check();}catch(Exception error){failures.Add(check.Method.Name+": "+error.GetBaseException().Message);}
  if(failures.Count>0)throw new InvalidOperationException(string.Join(Environment.NewLine,failures));
  foreach(float scale in new[]{1f,1.25f,1.5f,2f})SaveStateSheet(scale);
 }
 static void CheckPlannerStateColors(){
  string[] states=["Ready","Choices","Resume ready","Not checked","Completed","Blocked","Unavailable"];
  using var sheet=new Bitmap(720,78*states.Length);using var composite=Graphics.FromImage(sheet);
  int readyTitle=0;
  for(int i=0;i<states.Length;i++){
   string state=states[i];using var row=new PlannerQuestRow(new("test","Quest title","Legend reward","","1+","","","mundane-birth"),new(state,"Known reason",state is not ("Completed" or "Blocked" or "Unavailable"))){Width=720};
   using var bitmap=new Bitmap(row.Width,row.Height);using(var graphics=Graphics.FromImage(bitmap))Paint(row,graphics,row.ClientRectangle);
   var surface=bitmap.GetPixel(100,71);
   if(state is "Ready" or "Choices" or "Resume ready"&&(surface.G<=surface.B||surface.G<=surface.R))throw new InvalidOperationException(state+" quest rows need a green surface tint.");
   int maxTitle=0;for(int y=18;y<43;y++)for(int x=95;x<300;x++)maxTitle=Math.Max(maxTitle,bitmap.GetPixel(x,y).R);
   if(state=="Ready")readyTitle=maxTitle;
   if(state is "Blocked" or "Unavailable"&&maxTitle>=readyTitle)throw new InvalidOperationException("Blocked and unavailable quest titles must be muted compared with Ready.");
   composite.DrawImageUnscaled(bitmap,0,i*78);
  }
  Directory.CreateDirectory("artifacts");sheet.Save("artifacts/quest-row-states.png");
 }
 static void CheckSearchPlaceholder(){
  using var host=new Form{ClientSize=new(440,100),BackColor=Theme.Ink,ShowInTaskbar=false};
  using var field=new JournalTextBox{PlaceholderText="Search quests, rewards, or requirements",BorderStyle=BorderStyle.None,BackColor=Theme.Raised,ForeColor=Theme.Text,Font=Theme.Body(10),Bounds=new(12,12,400,24)};
  if(((TextBox)field).PlaceholderText.Length!=0)throw new InvalidOperationException("Native cue must remain empty when a custom dark placeholder is painted.");
  using var other=Theme.Button("Other");other.SetBounds(12,50,100,35);host.Controls.AddRange([field,other]);host.Show();other.Focus();Application.DoEvents();
  using var unfocused=new Bitmap(field.Width,field.Height);field.DrawToBitmap(unfocused,field.ClientRectangle);
  field.Focus();Application.DoEvents();using var focused=new Bitmap(field.Width,field.Height);field.DrawToBitmap(focused,field.ClientRectangle);
  for(int y=0;y<field.Height;y++)for(int x=8;x<field.Width-8;x++)if(unfocused.GetPixel(x,y)!=focused.GetPixel(x,y))throw new InvalidOperationException("Empty search placeholder changes text pixels on focus; duplicate native/custom hint painting must be removed.");
  field.Text="Muisir";Application.DoEvents();using var typed=new Bitmap(field.Width,field.Height);field.DrawToBitmap(typed,field.ClientRectangle);
  field.Text="";other.Focus();Application.DoEvents();using var cleared=new Bitmap(field.Width,field.Height);field.DrawToBitmap(cleared,field.ClientRectangle);
  for(int y=0;y<field.Height;y++)for(int x=8;x<field.Width-8;x++)if(unfocused.GetPixel(x,y)!=cleared.GetPixel(x,y))throw new InvalidOperationException("Clearing a search must restore one clean placeholder.");
  Directory.CreateDirectory("artifacts");unfocused.Save("artifacts/search-placeholder.png");
 }

 static void CheckCorners(){
  using var host=new Form{BackColor=Theme.Surface};
  using var button=Theme.Button("Continue");host.Controls.Add(button);
  using var bitmap=new Bitmap(button.Width,button.Height);
  using(var graphics=Graphics.FromImage(bitmap)){
   graphics.Clear(Color.Magenta);
   Paint(button,graphics,button.ClientRectangle);
  }
  foreach(var point in new[]{Point.Empty,new Point(button.Width-1,0),new Point(0,button.Height-1),new Point(button.Width-1,button.Height-1)})
   if(bitmap.GetPixel(point.X,point.Y).ToArgb()!=host.BackColor.ToArgb())
    throw new InvalidOperationException($"Button leaves stale pixels at {point}; opaque={(bool)style.Invoke(button,[ControlStyles.Opaque])!}. Rounded corners must repaint the parent surface.");
 }

 static void CheckPointerStates(){
  foreach(bool primary in new[]{false,true}){
   using var host=new Form{BackColor=Theme.Ink};using var button=Theme.Button("Continue",primary);host.Controls.Add(button);
   int normal=Face(button);
   Invoke(button,"OnMouseEnter",EventArgs.Empty);int hover=Face(button);
   Invoke(button,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,20,20,0));int pressed=Face(button);
   if(normal==hover||hover==pressed||normal==pressed)throw new InvalidOperationException($"{(primary?"Primary":"Secondary")} button needs distinct rest, hover and press feedback.");
   Invoke(button,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,20,20,0));
   if(Face(button)!=hover)throw new InvalidOperationException("Releasing a button must clear pressed feedback.");
   button.Enabled=false;int disabled=Face(button);Invoke(button,"OnMouseEnter",EventArgs.Empty);
   if(Face(button)!=disabled)throw new InvalidOperationException("Disabled controls must ignore hover.");
   button.Enabled=true;Invoke(button,"OnMouseEnter",EventArgs.Empty);Invoke(button,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,20,20,0));
   Invoke(button,"OnMouseLeave",EventArgs.Empty);
   if(Face(button)!=normal)throw new InvalidOperationException("Dragging outside must release the pressed appearance.");
   Invoke(button,"OnMouseCaptureChanged",EventArgs.Empty);Invoke(button,"OnMouseEnter",EventArgs.Empty);
   if(Face(button)!=hover)throw new InvalidOperationException("Lost capture must clear press state.");
  }
 }

 static void CheckKeyboardStates(){
  using var host=new Form{BackColor=Theme.Ink};using var button=Theme.Button("&Continue",true);host.Controls.Add(button);
  int clicks=0;button.Click+=(_,_)=>clicks++;
  int normal=Face(button);Invoke(button,"OnKeyDown",new KeyEventArgs(Keys.Space));
  if(Face(button)==normal)throw new InvalidOperationException("Space must show keyboard press feedback.");
  Invoke(button,"OnKeyUp",new KeyEventArgs(Keys.Space));
  if(Face(button)!=normal||clicks!=1)throw new InvalidOperationException("Space must restore the surface and click exactly once.");
  Invoke(button,"OnKeyDown",new KeyEventArgs(Keys.Space));Invoke(button,"OnLostFocus",EventArgs.Empty);
  if(Face(button)!=normal)throw new InvalidOperationException("Lost focus must clear keyboard press feedback.");
  Invoke(button,"OnKeyUp",new KeyEventArgs(Keys.Space));
  if(clicks!=1)throw new InvalidOperationException("Lost focus must cancel keyboard activation.");
  Invoke(button,"OnKeyDown",new KeyEventArgs(Keys.Control|Keys.Space));
  if(Face(button)!=normal)throw new InvalidOperationException("Modified Space must retain native button behavior.");
  Invoke(button,"OnKeyUp",new KeyEventArgs(Keys.Space));
  Invoke(button,"OnKeyDown",new KeyEventArgs(Keys.Space));Invoke(button,"OnKeyUp",new KeyEventArgs(Keys.ShiftKey));
  if(Face(button)!=normal||clicks!=1)throw new InvalidOperationException("Any key release must clear the native pressed state without an extra click.");
 }

 static void CheckPartialPaint(){
  using var host=new Form{BackColor=Theme.Ink};using var button=Theme.Button("A long button label");host.Controls.Add(button);
  using var bitmap=new Bitmap(button.Width,button.Height);
  var clip=new Rectangle(0,0,12,button.Height);
  using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Magenta);Paint(button,graphics,clip);}
  for(int y=0;y<bitmap.Height;y++)for(int x=clip.Right;x<bitmap.Width;x++)
   if(bitmap.GetPixel(x,y).ToArgb()!=Color.Magenta.ToArgb())throw new InvalidOperationException("A partial repaint drew outside its invalidated region.");
  if(bitmap.GetPixel(0,0).ToArgb()!=host.BackColor.ToArgb())throw new InvalidOperationException("A partial repaint retained a stale corner.");
 }

 static void CheckSmallSizes(){
  using var host=new Form{BackColor=Theme.Ink};using var button=Theme.Button("Continue");host.Controls.Add(button);
  foreach(int size in new[]{1,2,3,8,16}){button.Size=new(size,size);using var bitmap=new Bitmap(size,size);using var graphics=Graphics.FromImage(bitmap);Paint(button,graphics,button.ClientRectangle);}
 }

 static void CheckPropertyRepaint(){
  using var host=new Form();using var button=Theme.Button("Continue");host.Controls.Add(button);_=button.Handle;
  foreach(string property in new[]{"Primary","Quiet","Active","Danger"}){
   int invalidations=0;InvalidateEventHandler handler=(_,_)=>invalidations++;button.Invalidated+=handler;
   typeof(JournalButton).GetProperty(property)!.SetValue(button,true);button.Invalidated-=handler;
   if(invalidations==0)throw new InvalidOperationException(property+" changed without scheduling a repaint.");
  }
 }

 static void CheckCardCorners(){
  using var host=new Form{BackColor=Theme.Ink};using var card=new JournalCard{Size=new(200,100)};host.Controls.Add(card);
  using var bitmap=new Bitmap(card.Width,card.Height);using var graphics=Graphics.FromImage(bitmap);Paint(card,graphics,card.ClientRectangle);
  if(bitmap.GetPixel(0,0).ToArgb()!=host.BackColor.ToArgb())throw new InvalidOperationException("Rounded card corners must match their surrounding surface.");
 }

 static void CheckCheckboxClip(){
  using var host=new Form{BackColor=Theme.Surface};using var checkbox=new JournalCheckBox{Text="Fast mode · slows near players",Size=new(280,30)};host.Controls.Add(checkbox);
  using var bitmap=new Bitmap(checkbox.Width,checkbox.Height);using var graphics=Graphics.FromImage(bitmap);graphics.Clear(Color.Magenta);
  var clip=new Rectangle(0,0,18,checkbox.Height);Paint(checkbox,graphics,clip);
  for(int y=0;y<bitmap.Height;y++)for(int x=clip.Right;x<bitmap.Width;x++)
   if(bitmap.GetPixel(x,y).ToArgb()!=Color.Magenta.ToArgb())throw new InvalidOperationException("Checkbox partial painting drew outside its invalidated region.");
 }

 static void CheckDialogKeyboard(){
  using var host=new Form{BackColor=Theme.Ink,ShowInTaskbar=false,Opacity=0};using var accept=Theme.Button("&Continue",true);using var cancel=Theme.Button("Cancel");using var input=new TextBox();
  accept.Location=new(0,40);cancel.Location=new(150,40);host.Controls.AddRange([accept,cancel,input]);host.AcceptButton=accept;host.CancelButton=cancel;
  int accepted=0,cancelled=0;accept.Click+=(_,_)=>accepted++;cancel.Click+=(_,_)=>cancelled++;
  host.Show();accept.Focus();
  if(!accept.Focused)throw new InvalidOperationException("Keyboard focus must reach native buttons.");
  using var focused=new Bitmap(accept.Width,accept.Height);using(var graphics=Graphics.FromImage(focused))Paint(accept,graphics,accept.ClientRectangle);
  input.Focus();using var unfocused=new Bitmap(accept.Width,accept.Height);using(var graphics=Graphics.FromImage(unfocused))Paint(accept,graphics,accept.ClientRectangle);
  bool focusVisible=false;for(int y=0;y<4;y++)for(int x=0;x<accept.Width;x++)if(focused.GetPixel(x,y)!=unfocused.GetPixel(x,y))focusVisible=true;
  if(!focusVisible)throw new InvalidOperationException("Keyboard focus needs a visible indicator.");
  Invoke(host,"ProcessDialogKey",Keys.Enter);Invoke(host,"ProcessDialogKey",Keys.Escape);
  if(accepted!=1||cancelled!=1)throw new InvalidOperationException("Enter and Escape must activate the native dialog actions exactly once.");
  Invoke(accept,"ProcessMnemonic",'C');if(accepted!=2)throw new InvalidOperationException("Native button mnemonics must still activate the action.");
  host.Close();
 }

 static void CheckRepaintCycles(){
  using var host=new Form{BackColor=Theme.Surface};using var transparent=new Panel{BackColor=Color.Transparent};host.Controls.Add(transparent);
  using var button=Theme.Button("A long action label that must fit");transparent.Controls.Add(button);
  foreach(float scale in new[]{1f,1.25f,1.5f,2f}){
   button.Size=new((int)(148*scale),(int)(40*scale));using var baseline=new Bitmap(button.Width,button.Height);using(var graphics=Graphics.FromImage(baseline))Paint(button,graphics,button.ClientRectangle);
   for(int cycle=0;cycle<12;cycle++){
    Invoke(button,"OnMouseEnter",EventArgs.Empty);Invoke(button,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,20,20,0));Invoke(button,"OnMouseCaptureChanged",EventArgs.Empty);Invoke(button,"OnMouseLeave",EventArgs.Empty);
    button.Enabled=false;button.Enabled=true;
    using var bitmap=new Bitmap(button.Width,button.Height);using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Magenta);Paint(button,graphics,button.ClientRectangle);}
    for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)if(bitmap.GetPixel(x,y)!=baseline.GetPixel(x,y))throw new InvalidOperationException("Repeated state changes retained stale pixels at scale "+scale);
   }
   using var native=new Bitmap(button.Width,button.Height);button.DrawToBitmap(native,button.ClientRectangle);
   if(native.GetPixel(0,0).ToArgb()!=host.BackColor.ToArgb())throw new InvalidOperationException("Native window printing retained an unpainted corner.");
  }
 }

 static void SaveStateSheet(float scale){
  // Scale geometry and fonts; this is not a substitute for a physical mixed-DPI monitor test.
  int S(float value)=>(int)Math.Round(value*scale);
  using var host=new Form{BackColor=Theme.Ink};using var sheet=new Bitmap(S(800),S(390));using var graphics=Graphics.FromImage(sheet);
  graphics.Clear(Theme.Ink);using var heading=Theme.Heading(18*scale);using var label=Theme.Body(9*scale);
  TextRenderer.DrawText(graphics,"Native control states",heading,new Rectangle(S(24),S(16),S(750),S(38)),Theme.Text,Theme.ControlTextFlags);
  string[] states=["Rest","Hover","Pressed","Disabled","Selected","Quiet","Danger"];
  for(int row=0;row<states.Length;row++){
   int y=68+row*43;TextRenderer.DrawText(graphics,states[row],label,new Rectangle(S(24),S(y),S(112),S(40)),Theme.Muted,Theme.ControlTextFlags);
   for(int column=0;column<2;column++){
    using var button=Theme.Button(column==0?"Continue quest":"View journal",column==0);host.Controls.Add(button);using var font=Theme.Heading(10*scale);button.Font=font;button.Size=new(S(272),S(40));
    if(row==1)Invoke(button,"OnMouseEnter",EventArgs.Empty);
    if(row==2){Invoke(button,"OnMouseEnter",EventArgs.Empty);Invoke(button,"OnKeyDown",new KeyEventArgs(Keys.Space));}
    if(row==3)button.Enabled=false;if(row==4){button.Primary=false;button.Quiet=true;button.Active=true;}if(row==5){button.Primary=false;button.Quiet=true;}if(row==6){button.Primary=false;button.Danger=true;}
    // Composite local control bitmaps so GDI clip/viewport coordinates match native painting.
    using var rendered=new Bitmap(button.Width,button.Height);using(var local=Graphics.FromImage(rendered))Paint(button,local,button.ClientRectangle);
    button.Text="";using var empty=new Bitmap(button.Width,button.Height);using(var local=Graphics.FromImage(empty))Paint(button,local,button.ClientRectangle);
    bool textVisible=false;for(int py=4;py<button.Height-4&&!textVisible;py++)for(int px=16;px<button.Width-16;px++)if(rendered.GetPixel(px,py)!=empty.GetPixel(px,py)){textVisible=true;break;}
    if(!textVisible)throw new InvalidOperationException($"Button text disappeared in {states[row]} at scale {scale}.");
    graphics.DrawImageUnscaled(rendered,S(144+column*304),S(y));
   }
  }
  Directory.CreateDirectory("artifacts");sheet.Save($"artifacts/control-states-{(int)(scale*100)}.png");
 }

 static int Face(JournalButton button){using var bitmap=new Bitmap(button.Width,button.Height);using var graphics=Graphics.FromImage(bitmap);Paint(button,graphics,button.ClientRectangle);return bitmap.GetPixel(button.Width/2,button.Height-8).ToArgb();}
 static void Invoke(Control control,string name,object args)=>control.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(control,[args]);

 static void Paint(Control button,Graphics graphics,Rectangle clip){
  var state=graphics.Save();graphics.SetClip(clip);
  using var args=new PaintEventArgs(graphics,clip);
  if(!(bool)style.Invoke(button,[ControlStyles.Opaque])!)background.Invoke(button,[args]);
  button.GetType().GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(button,[args]);graphics.Restore(state);
 }
}
