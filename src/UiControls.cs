using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CodexThemeModifier {
    internal sealed class UiPalette {
        public bool Dark;
        public Color Background,Card,Text,Muted,Line,Accent,AccentInk,Control,Selected,Success,Danger;
        public UiPalette(bool dark){
            Dark=dark;Background=C(dark?"#1B202A":"#ECF0F7");Card=C(dark?"#252C38":"#FAFCFF");
            Text=C(dark?"#E9EEF8":"#2C374C");Muted=C(dark?"#9CA8BC":"#7B879C");Line=C(dark?"#3A4354":"#E1E6F0");
            Accent=C(dark?"#A6B9F6":"#687FD1");AccentInk=C(dark?"#222C42":"#FFFFFF");
            Control=C(dark?"#313B4D":"#EDF1F9");Selected=C(dark?"#38486B":"#DFE8FC");
            Success=C(dark?"#94CAB6":"#4F8E7B");Danger=C(dark?"#DF9CA9":"#B66D7E");
        }
        public static Color C(string value){return ColorTranslator.FromHtml(value);}
    }
    internal static class UiFonts {
        static readonly PrivateFontCollection collection=new PrivateFontCollection();
        static readonly Dictionary<string,Font> cache=new Dictionary<string,Font>();
        static FontFamily family;
        static IntPtr memory,registration;
        [DllImport("gdi32.dll")]static extern IntPtr AddFontMemResourceEx(IntPtr data,uint size,IntPtr reserved,ref uint count);
        static UiFonts(){
            try{byte[] bytes=Resources.Bytes("ui.rounded.ttf");memory=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,memory,bytes.Length);collection.AddMemoryFont(memory,bytes.Length);uint count=0;registration=AddFontMemResourceEx(memory,(uint)bytes.Length,IntPtr.Zero,ref count);family=collection.Families[0];}
            catch{family=new FontFamily("Microsoft YaHei UI");}
        }
        public static Font Get(float pixels){string key=pixels.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);Font value;if(!cache.TryGetValue(key,out value)){value=new Font(family,pixels,FontStyle.Regular,GraphicsUnit.Pixel);cache[key]=value;}return value;}
    }
    internal interface IUiControl {void Apply(UiPalette palette,float unit);}
    internal static class UiDraw {
        public static void ParentBackground(Control child,PaintEventArgs e,Color fallback,Action<Control,PaintEventArgs> background,Action<Control,PaintEventArgs> foreground){
            e.Graphics.Clear(fallback);if(child.Parent==null)return;
            var state=e.Graphics.Save();try{e.Graphics.TranslateTransform(-child.Left,-child.Top);var clip=e.ClipRectangle;clip.Offset(child.Left,child.Top);var args=new PaintEventArgs(e.Graphics,clip);background(child.Parent,args);foreground(child.Parent,args);}finally{e.Graphics.Restore(state);}
        }
        public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));if(d<1){p.AddRectangle(r);return p;}p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        public static void Box(Graphics g,RectangleF r,Color color,float radius,Color? stroke=null){using(var p=Round(r,radius)){using(var b=new SolidBrush(color))g.FillPath(b,p);if(stroke.HasValue)using(var pen=new Pen(stroke.Value))g.DrawPath(pen,p);}}
        public static void Text(Graphics g,string text,Font font,RectangleF r,Color color,StringAlignment align=StringAlignment.Near){g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;using(var b=new SolidBrush(color))using(var f=new StringFormat{Alignment=align,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(text??"",font,b,r,f);}
        public static void Icon(Graphics g,string name,RectangleF r,Color color){var state=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(r.Width/24,r.Height/24);using(var pen=new Pen(color,1.55f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){
            switch(name){
                case "image":g.DrawRectangle(pen,3,4,18,16);g.DrawEllipse(pen,7,7,3,3);g.DrawLines(pen,new[]{new PointF(4,17),new PointF(10,12),new PointF(14,16),new PointF(18,11),new PointF(21,14)});break;
                case "manage":g.DrawRectangle(pen,4,4,6,6);g.DrawRectangle(pen,14,4,6,6);g.DrawRectangle(pen,4,14,6,6);g.DrawRectangle(pen,14,14,6,6);break;
                case "settings":g.DrawEllipse(pen,5,5,14,14);g.DrawEllipse(pen,9,9,6,6);for(int i=0;i<8;i++){double a=Math.PI*i/4;g.DrawLine(pen,12+(float)Math.Cos(a)*8,12+(float)Math.Sin(a)*8,12+(float)Math.Cos(a)*10,12+(float)Math.Sin(a)*10);}break;
                case "sun":g.DrawEllipse(pen,7,7,10,10);for(int i=0;i<8;i++){double a=Math.PI*i/4;g.DrawLine(pen,12+(float)Math.Cos(a)*8,12+(float)Math.Sin(a)*8,12+(float)Math.Cos(a)*10,12+(float)Math.Sin(a)*10);}break;
                case "moon":using(var p=new GraphicsPath()){p.AddArc(3,3,18,18,58,277);p.AddArc(9,1,14,16,190,-155);p.CloseFigure();g.DrawPath(pen,p);}break;
                case "open":g.DrawLines(pen,new[]{new PointF(14,4),new PointF(20,4),new PointF(20,10)});g.DrawLine(pen,20,4,10,14);g.DrawLines(pen,new[]{new PointF(7,5),new PointF(4,5),new PointF(4,20),new PointF(19,20),new PointF(19,17)});break;
                case "refresh":g.DrawArc(pen,4,4,16,16,45,285);g.DrawLines(pen,new[]{new PointF(20,4),new PointF(20,10),new PointF(14,10)});break;
                case "folder":g.DrawPolygon(pen,new[]{new PointF(3,6),new PointF(10,6),new PointF(12,9),new PointF(21,9),new PointF(21,20),new PointF(3,20)});break;
                case "check":g.DrawLines(pen,new[]{new PointF(5,12),new PointF(10,17),new PointF(19,7)});break;
                case "palette":g.DrawArc(pen,3,3,18,18,75,285);g.DrawLines(pen,new[]{new PointF(14,21),new PointF(12,17),new PointF(16,14),new PointF(21,12)});g.DrawEllipse(pen,7,7,1.6f,1.6f);g.DrawEllipse(pen,12,5,1.6f,1.6f);g.DrawEllipse(pen,6,12,1.6f,1.6f);break;
                case "codex":g.DrawLines(pen,new[]{new PointF(9,6),new PointF(3,12),new PointF(9,18)});g.DrawLines(pen,new[]{new PointF(15,6),new PointF(21,12),new PointF(15,18)});break;
                default:g.DrawEllipse(pen,3,3,18,18);g.DrawLine(pen,12,11,12,17);g.DrawEllipse(pen,11.6f,6.4f,0.8f,0.8f);break;
            }
        }g.Restore(state);}
    }
    internal sealed class UiPanel:Panel {public UiPanel(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Color.Transparent;}}
    internal sealed class UiCard:Panel,IUiControl {
        UiPalette palette=new UiPalette(false);float unit=1;
        public UiCard(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Color.Transparent;}
        public void Apply(UiPalette p,float scale){palette=p;unit=scale;Invalidate(true);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;UiDraw.Box(e.Graphics,new RectangleF(0,0,Width-1,Height-1),palette.Card,16*unit,Color.FromArgb(palette.Dark?145:120,palette.Line));}
    }
    internal sealed class UiLabel:Label {
        public bool Muted;
        public UiLabel(){UseCompatibleTextRendering=true;AutoEllipsis=true;BackColor=Color.Transparent;TextAlign=ContentAlignment.MiddleLeft;}
    }
    internal sealed class UiButton:Control,IButtonControl,IUiControl {
        public bool Primary,Quiet,Selected,Destructive;
        public string Glyph;
        UiPalette palette=new UiPalette(false);float unit=1;bool hover,pressed;
        public DialogResult DialogResult{get;set;}
        public UiButton(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable|ControlStyles.StandardClick,true);BackColor=Color.Transparent;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.PushButton;}
        public void NotifyDefault(bool value){}
        public void PerformClick(){if(Enabled&&Visible)OnClick(EventArgs.Empty);}
        protected override void OnClick(EventArgs e){base.OnClick(e);if(DialogResult!=DialogResult.None&&FindForm()!=null)FindForm().DialogResult=DialogResult;}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){PerformClick();e.Handled=true;}}
        protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Invalidate();}
        protected override AccessibleObject CreateAccessibilityInstance(){return new ButtonAccessibility(this);}
        sealed class ButtonAccessibility:ControlAccessibleObject{readonly UiButton owner;public ButtonAccessibility(UiButton value):base(value){owner=value;}public override string DefaultAction{get{return "按下";}}public override void DoDefaultAction(){owner.PerformClick();}}
        protected override void OnPaintBackground(PaintEventArgs e){UiDraw.ParentBackground(this,e,palette.Background,(p,a)=>InvokePaintBackground(p,a),(p,a)=>InvokePaint(p,a));}
        public void Apply(UiPalette p,float scale){palette=p;unit=scale;Invalidate();}
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;pressed=false;Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left)Focus();pressed=true;Invalidate();}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);pressed=false;Invalidate();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color fill=Selected?palette.Selected:Primary?palette.Accent:Quiet?Color.Transparent:palette.Control;
            if(hover&&Enabled)fill=Color.FromArgb(pressed?195:225,Selected?palette.Selected:Primary?palette.Accent:palette.Control);
            Color ink=!Enabled?Color.FromArgb(115,palette.Muted):Destructive?palette.Danger:Primary?palette.AccentInk:Selected?palette.Accent:palette.Text;
            if(!Enabled)fill=Color.FromArgb(100,palette.Control);
            UiDraw.Box(e.Graphics,new RectangleF(0,0,Width-1,Height-1),fill,10*unit);
            if(!String.IsNullOrEmpty(Glyph)){float size=20*unit,x=String.IsNullOrEmpty(Text)?(Width-size)/2:15*unit;UiDraw.Icon(e.Graphics,Glyph,new RectangleF(x,(Height-size)/2,size,size),ink);}
            var textRect=ClientRectangle;if(!String.IsNullOrEmpty(Glyph)&&!String.IsNullOrEmpty(Text)){textRect.X=(int)(34*unit);textRect.Width-=(int)(40*unit);}
            UiDraw.Text(e.Graphics,Text,Font,textRect,ink,StringAlignment.Center);
            if(Focused&&ShowFocusCues&&Enabled)using(var path=UiDraw.Round(new RectangleF(2,2,Width-5,Height-5),8*unit))using(var pen=new Pen(Color.FromArgb(120,palette.Accent))){pen.DashStyle=DashStyle.Dot;e.Graphics.DrawPath(pen,path);}
        }
    }
    internal sealed class UiSwitch:Control,IUiControl {
        UiPalette palette=new UiPalette(false);float unit=1;
        bool check;
        public event EventHandler CheckedChanged;
        public bool Checked{get{return check;}set{if(check==value)return;check=value;Invalidate();AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);}}
        public UiSwitch(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.StandardClick,true);BackColor=Color.Transparent;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.CheckButton;}
        protected override void OnPaintBackground(PaintEventArgs e){UiDraw.ParentBackground(this,e,palette.Background,(p,a)=>InvokePaintBackground(p,a),(p,a)=>InvokePaint(p,a));}
        public void Apply(UiPalette p,float scale){palette=p;unit=scale;Invalidate();}
        protected override void OnClick(EventArgs e){Checked=!Checked;base.OnClick(e);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left)Focus();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space){Checked=!Checked;e.Handled=true;}}
        protected override AccessibleObject CreateAccessibilityInstance(){return new SwitchAccessibility(this);}
        sealed class SwitchAccessibility:ControlAccessibleObject{readonly UiSwitch owner;public SwitchAccessibility(UiSwitch value):base(value){owner=value;}public override AccessibleStates State{get{return base.State|(owner.Checked?AccessibleStates.Checked:AccessibleStates.None);}}public override string DefaultAction{get{return "切换";}}public override void DoDefaultAction(){owner.Checked=!owner.Checked;}}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float w=40*unit,h=22*unit,x=(Width-w)/2,y=(Height-h)/2;UiDraw.Box(e.Graphics,new RectangleF(x,y,w,h),Enabled?(Checked?palette.Accent:palette.Line):palette.Control,h/2);using(var b=new SolidBrush(Checked?palette.AccentInk:palette.Card))e.Graphics.FillEllipse(b,x+(Checked?20:3)*unit,y+3*unit,16*unit,16*unit);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);}
    }
    internal sealed class ThemeSlider:Control,IUiControl {
        int value;UiPalette palette=new UiPalette(false);float unit=1;
        public int Minimum,Maximum=100;
        public event EventHandler ValueChanged;
        public int Value{get{return value;}set{int next=Math.Max(Minimum,Math.Min(Maximum,value));if(this.value==next)return;this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
        public ThemeSlider(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.Selectable,true);BackColor=Color.Transparent;TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.Slider;}
        public void Apply(UiPalette p,float scale){palette=p;unit=scale;Invalidate();}
        void MoveTo(int x){Value=Minimum+(int)Math.Round(Math.Max(0,Math.Min(1,(x-8*unit)/Math.Max(1,Width-16*unit)))*(Maximum-Minimum));}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&Enabled){Focus();Capture=true;MoveTo(e.X);}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture)MoveTo(e.X);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);Capture=false;}
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||key==Keys.Home||key==Keys.End||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down)Value--;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up)Value++;else if(e.KeyCode==Keys.Home)Value=Minimum;else if(e.KeyCode==Keys.End)Value=Maximum;else return;e.Handled=true;}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float x=8*unit+(Width-16*unit)*(Value-Minimum)/(float)Math.Max(1,Maximum-Minimum),y=Height/2f;using(var track=new Pen(palette.Line,4*unit))using(var active=new Pen(Enabled?palette.Accent:palette.Muted,4*unit))using(var thumb=new SolidBrush(Enabled?palette.Accent:palette.Muted)){track.StartCap=track.EndCap=active.StartCap=active.EndCap=LineCap.Round;e.Graphics.DrawLine(track,8*unit,y,Width-8*unit,y);if(x>8*unit)e.Graphics.DrawLine(active,8*unit,y,x,y);e.Graphics.FillEllipse(thumb,x-6*unit,y-6*unit,12*unit,12*unit);}if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);}
    }
    internal sealed class ThemePicker:Control,IUiControl {
        public readonly string[] Items;int selectedIndex;UiPalette palette=new UiPalette(false);float unit=1;
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex{get{return selectedIndex;}set{int next=Math.Max(0,Math.Min(Items.Length-1,value));if(next==selectedIndex)return;selectedIndex=next;Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
        public ThemePicker(string[] items){Items=items;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.Selectable,true);BackColor=Color.Transparent;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.ComboBox;}
        public void Apply(UiPalette p,float scale){palette=p;unit=scale;Invalidate();}
        protected override void OnClick(EventArgs e){base.OnClick(e);Focus();var menu=new ContextMenuStrip{BackColor=palette.Card,ForeColor=palette.Text,ShowImageMargin=false,Font=Font};for(int i=0;i<Items.Length;i++){int index=i;var item=new ToolStripMenuItem(Items[i]){Checked=i==SelectedIndex};item.Click+=(s,a)=>SelectedIndex=index;menu.Items.Add(item);}menu.Closed+=(s,a)=>menu.Dispose();menu.Show(this,0,Height);}
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Up)SelectedIndex--;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Down)SelectedIndex++;else if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter)OnClick(EventArgs.Empty);else return;e.Handled=true;}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;UiDraw.Box(e.Graphics,new RectangleF(0,0,Width-1,Height-1),palette.Control,8*unit);UiDraw.Text(e.Graphics,Items[SelectedIndex],Font,new RectangleF(12*unit,0,Width-36*unit,Height),Enabled?palette.Text:palette.Muted);using(var pen=new Pen(palette.Muted,1.3f*unit)){e.Graphics.DrawLine(pen,Width-21*unit,Height/2-2*unit,Width-17*unit,Height/2+2*unit);e.Graphics.DrawLine(pen,Width-17*unit,Height/2+2*unit,Width-13*unit,Height/2-2*unit);}if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);}
    }
    internal sealed class ThemePreview:Control,IUiControl {
        UiPalette palette=new UiPalette(false);float unit=1;Image photo;Bitmap blurred;int cachedBlur=-1;
        public int Dim=42,Blur,Panel=70,Bubble=18;public bool BackgroundEnabled=true;public string Fit="cover",Position="center";
        public ThemePreview(){SetStyle(ControlStyles.SupportsTransparentBackColor,true);DoubleBuffered=true;BackColor=Color.Transparent;}
        public void Apply(UiPalette p,float scale){palette=p;unit=scale;Invalidate();}
        public void SetPhoto(Image value){if(photo!=null)photo.Dispose();photo=value;if(blurred!=null){blurred.Dispose();blurred=null;}cachedBlur=-1;Invalidate();}
        public void RefreshOptions(){if(cachedBlur!=Blur){if(blurred!=null){blurred.Dispose();blurred=null;}cachedBlur=Blur;if(photo!=null&&Blur>0){float scale=Math.Min(1,180f/(Blur+1)/Math.Max(photo.Width,photo.Height));blurred=new Bitmap(Math.Max(1,(int)(photo.Width*scale)),Math.Max(1,(int)(photo.Height*scale)));using(var g=Graphics.FromImage(blurred)){g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(photo,new Rectangle(0,0,blurred.Width,blurred.Height));}}}Invalidate();}
        protected override void OnPaint(PaintEventArgs e){
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(0,0,Width-1,Height-1);using(var clip=UiDraw.Round(r,12*unit)){g.SetClip(clip);using(var b=new LinearGradientBrush(r,palette.Dark?UiPalette.C("#4C617B"):UiPalette.C("#C1D2ED"),palette.Dark?UiPalette.C("#7E718D"):UiPalette.C("#DCD4E9"),35f))g.FillRectangle(b,r);
                if(BackgroundEnabled&&photo!=null){Image image=blurred??photo;float ratio=Fit=="contain"?Math.Min(r.Width/image.Width,r.Height/image.Height):Math.Max(r.Width/image.Width,r.Height/image.Height);float w=image.Width*ratio,h=image.Height*ratio;float x=(r.Width-w)/2,y=(r.Height-h)/2;if(Position=="top")y=0;if(Position=="bottom")y=r.Height-h;if(Position=="left")x=0;if(Position=="right")x=r.Width-w;g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(image,x,y,w,h);}
                else {using(var b=new SolidBrush(Color.FromArgb(45,255,255,255)))g.FillEllipse(b,r.Width*.42f,-r.Height*.18f,r.Width*.85f,r.Height*1.2f);using(var b=new SolidBrush(Color.FromArgb(35,94,145,160)))g.FillEllipse(b,-r.Width*.2f,r.Height*.48f,r.Width*1.3f,r.Height);}
                Color ink=palette.Dark?Color.FromArgb(236,240,249):Color.FromArgb(40,51,70);using(var shade=new SolidBrush(Color.FromArgb(BackgroundEnabled?Dim*255/100:255,palette.Dark?UiPalette.C("#18181B"):UiPalette.C("#F8F9FC"))))g.FillRectangle(shade,r);
                Color surface=palette.Dark?Color.FromArgb(28,33,44):Color.FromArgb(250,252,255);using(var b=new SolidBrush(Color.FromArgb(Math.Max(30,Panel*255/100),surface))){g.FillRectangle(b,0,0,r.Width,27*unit);g.FillRectangle(b,0,27*unit,35*unit,r.Height);}
                UiDraw.Text(g,"Codex · 预览",UiFonts.Get(11*unit),new RectangleF(13*unit,0,110*unit,27*unit),ink);
                for(int i=0;i<3;i++)using(var b=new SolidBrush(Color.FromArgb(100,ink)))g.FillEllipse(b,r.Width-(43-i*11)*unit,11*unit,4*unit,4*unit);
                for(int i=0;i<3;i++)UiDraw.Icon(g,i==0?"manage":i==1?"image":"settings",new RectangleF(10*unit,(44+i*31)*unit,14*unit,14*unit),Color.FromArgb(150,ink));
                float cx=52*unit,cw=r.Width-cx-18*unit;UiDraw.Text(g,"你的工作空间",UiFonts.Get(19*unit),new RectangleF(cx,r.Height*.14f,cw,32*unit),ink);
                var bubble=new RectangleF(Math.Max(cx,r.Width-228*unit),r.Height*.38f,210*unit,34*unit);UiDraw.Box(g,bubble,Color.FromArgb(Math.Max(18,Bubble*255/100),palette.Accent),10*unit);UiDraw.Text(g,"调成自己喜欢的样子",UiFonts.Get(12*unit),bubble,ink,StringAlignment.Center);
                if(r.Height>260*unit)UiDraw.Text(g,"背景与透明度，随你调整。",UiFonts.Get(12*unit),new RectangleF(cx,r.Height*.56f,cw,30*unit),ink);
                var composer=new RectangleF(cx,r.Height-72*unit,cw,53*unit);UiDraw.Box(g,composer,Color.FromArgb(Math.Max(25,Panel*255/100),surface),12*unit);UiDraw.Text(g,"今天，想做些什么？",UiFonts.Get(12*unit),new RectangleF(cx+12*unit,composer.Y,composer.Width-40*unit,composer.Height),Color.FromArgb(160,ink));UiDraw.Icon(g,"open",new RectangleF(composer.Right-29*unit,composer.Y+17*unit,16*unit,16*unit),ink);
                g.ResetClip();
            }UiDraw.Box(g,r,Color.Transparent,12*unit,Color.FromArgb(70,palette.Line));
        }
        protected override void Dispose(bool disposing){if(disposing){if(photo!=null)photo.Dispose();if(blurred!=null)blurred.Dispose();}base.Dispose(disposing);}
    }
}
