using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace CodexThemeModifier {
    internal sealed class Card:Panel {
        public Color Fill=Color.FromArgb(24,31,48),Border=Color.FromArgb(43,53,74);
        public Card(){DoubleBuffered=true;BackColor=Color.Transparent;}
        protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.FromArgb(15,21,34):Parent.BackColor);using(var p=Shape(new Rectangle(0,0,Width-1,Height-1),14))using(var b=new SolidBrush(Fill))using(var pen=new Pen(Border)){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}}
        public static GraphicsPath Shape(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    }
    internal sealed class AccentButton:Button {
        public AccentButton(string text,bool primary){Text=text;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=primary?Color.FromArgb(137,156,255):Color.FromArgb(41,52,75);ForeColor=primary?Color.FromArgb(13,20,37):Color.FromArgb(225,232,248);Cursor=Cursors.Hand;Font=new Font("Microsoft YaHei UI",10,FontStyle.Bold);Height=40;TabStop=true;}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Card.Shape(new Rectangle(0,0,Width-1,Height-1),8))using(var b=new SolidBrush(Enabled?BackColor:Color.FromArgb(37,43,57))){e.Graphics.FillPath(b,p);}TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(105,114,133),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
    }
    internal sealed class ThemeSlider:Control {
        int value;
        public int Minimum,Maximum=100;
        public event EventHandler ValueChanged;
        public int Value {get{return value;}set{int next=Math.Max(Minimum,Math.Min(Maximum,value));if(this.value==next)return;this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
        public ThemeSlider(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.Selectable,true);BackColor=Color.Transparent;TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.Slider;}
        void SetFromMouse(int x){Value=Minimum+(int)Math.Round(Math.Max(0,Math.Min(1,(x-9.0)/Math.Max(1,Width-18)))*(Maximum-Minimum));}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&Enabled){Focus();Capture=true;SetFromMouse(e.X);}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(Capture)SetFromMouse(e.X);}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);Capture=false;}
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||key==Keys.Home||key==Keys.End||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down)Value--;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up)Value++;else if(e.KeyCode==Keys.Home)Value=Minimum;else if(e.KeyCode==Keys.End)Value=Maximum;else return;e.Handled=true;}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float x=9+(Width-18)*(Value-Minimum)/(float)Math.Max(1,Maximum-Minimum),y=Height/2f;using(var track=new Pen(Color.FromArgb(54,66,91),5))using(var active=new Pen(Enabled?Color.FromArgb(137,156,255):Color.FromArgb(73,85,115),5))using(var thumb=new SolidBrush(Enabled?Color.FromArgb(172,186,255):Color.FromArgb(88,99,127))){track.StartCap=track.EndCap=active.StartCap=active.EndCap=LineCap.Round;e.Graphics.DrawLine(track,9,y,Width-9,y);if(x>9)e.Graphics.DrawLine(active,9,y,x,y);e.Graphics.FillEllipse(thumb,x-7,y-7,14,14);}if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(0,0,Width-1,Height-1));}
    }
    internal sealed class ThemePicker:Control {
        public readonly string[] Items;
        int selectedIndex;
        public int SelectedIndex{get{return selectedIndex;}set{selectedIndex=Math.Max(0,Math.Min(Items.Length-1,value));Invalidate();}}
        public ThemePicker(string[] items){Items=items;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.Selectable,true);BackColor=Color.Transparent;Cursor=Cursors.Hand;TabStop=true;AccessibleRole=AccessibleRole.ComboBox;}
        protected override void OnClick(EventArgs e){base.OnClick(e);Focus();var menu=new ContextMenuStrip{BackColor=Color.FromArgb(41,52,75),ForeColor=Color.FromArgb(231,236,249),ShowImageMargin=false,Font=Font};for(int i=0;i<Items.Length;i++){int index=i;var item=new ToolStripMenuItem(Items[i]){Checked=i==SelectedIndex};item.Click+=(s,a)=>SelectedIndex=index;menu.Items.Add(item);}menu.Closed+=(s,a)=>menu.Dispose();menu.Show(this,0,Height);}
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Up)SelectedIndex--;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Down)SelectedIndex++;else if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter)OnClick(EventArgs.Empty);else return;e.Handled=true;}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Card.Shape(new Rectangle(0,0,Width-1,Height-1),6))using(var fill=new SolidBrush(Color.FromArgb(41,52,75))){e.Graphics.FillPath(fill,path);}TextRenderer.DrawText(e.Graphics,Items[SelectedIndex],Font,new Rectangle(8,0,Width-26,Height),Enabled?Color.FromArgb(231,236,249):Color.Gray,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);using(var pen=new Pen(Color.FromArgb(167,181,212),1.5f)){e.Graphics.DrawLine(pen,Width-19,Height/2-2,Width-15,Height/2+2);e.Graphics.DrawLine(pen,Width-15,Height/2+2,Width-11,Height/2-2);}if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(2,2,Width-4,Height-4));}
    }
    internal sealed class ManagerForm:Form {
        readonly ManagerEngine engine;
        readonly IDesktop desktop;
        readonly bool preview;
        PackageInfo package;
        UpdateInfo update;
        string selectedImage;
        bool removeImage,busy,postUpdate,uninstallRequested;
        Label packageLabel,statusLabel,toast,updateLabel,imageName;
        PictureBox picture;
        CheckBox enabled,autoCheck;
        ThemeSlider dim,blur,panel,bubble;
        Card editor;
        Label dimValue,blurValue,panelValue,bubbleValue;
        ThemePicker fit,position;
        AccentButton installButton,disableButton,startButton,applyButton,updateButton,uninstallButton;
        ProgressBar progress;
        readonly Color foreground=Color.FromArgb(231,236,249),muted=Color.FromArgb(146,160,185);
        public ManagerForm(ManagerEngine engine,IDesktop desktop,bool preview){this.engine=engine;this.desktop=desktop;this.preview=preview;Build();if(!preview)Shown+=async(s,e)=>await Initialize();}
        Label LabelAt(Control parent,string text,int x,int y,int width,int height,float size,Color color,bool bold){var label=new Label{Text=text,Location=new Point(x,y),Size=new Size(width,height),ForeColor=color,BackColor=Color.Transparent,Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular)};parent.Controls.Add(label);return label;}
        AccentButton ButtonAt(Control parent,string text,int x,int y,int width,bool primary,Action action){var b=new AccentButton(text,primary){Location=new Point(x,y),Width=width};if(action!=null)b.Click+=(s,e)=>action();parent.Controls.Add(b);return b;}
        void Build(){
            Text="Codex Theme Modifier · 主题修改器";ClientSize=new Size(1060,760);MinimumSize=new Size(1076,799);MaximumSize=new Size(1076,799);MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(13,19,32);ForeColor=foreground;Font=new Font("Microsoft YaHei UI",9);AutoScaleMode=AutoScaleMode.None;
            try{Icon=new Icon(new MemoryStream(Resources.Bytes("payload.modifier.ico")));}catch{}
            var side=new Panel{Location=new Point(0,0),Size=new Size(204,760),BackColor=Color.FromArgb(17,25,41)};Controls.Add(side);
            LabelAt(side,"CT",24,27,120,47,26,Color.FromArgb(155,174,255),true);
            LabelAt(side,"CODEX THEME",24,83,165,26,12,foreground,true);
            LabelAt(side,"主题修改器",24,113,160,25,10,muted,false);
            var nav=new Card{Location=new Point(14,174),Size=new Size(176,46),Fill=Color.FromArgb(39,52,81),Border=Color.FromArgb(39,52,81)};side.Controls.Add(nav);LabelAt(nav,"◉   外观控制台",18,12,150,25,10,Color.FromArgb(177,192,255),true);
            ButtonAt(side,"GitHub 开源仓库",16,590,172,false,()=>OpenWebsite());
            ButtonAt(side,"关于 / 安全说明",16,640,172,false,()=>MessageBox.Show(this,"第三方开源外观工具，不代表 OpenAI 官方。\n\n保留官方安装包、账号、功能与更新方式。主题通过本机调试连接加载，会增加一处本地访问面；同机程序可能连接该端口。未来官方界面变化也可能需要适配。\n\n可随时关闭主题或卸载修改功能。",Text,MessageBoxButtons.OK,MessageBoxIcon.Information));
            LabelAt(side,"v"+BuildInfo.Version+"  /  Windows",24,710,175,25,8.5f,muted,false);
            LabelAt(this,"把 Codex 调成你的样子",232,24,700,41,21,foreground,true);
            LabelAt(this,"原版继续更新，外观由你决定。",234,72,700,28,10,muted,false);
            var status=new Card{Location=new Point(232,112),Size=new Size(800,108)};Controls.Add(status);
            LabelAt(status,"官方桌面版",20,15,200,23,10,muted,false);
            packageLabel=LabelAt(status,"正在检测 Codex…",20,43,460,30,14,foreground,true);
            statusLabel=LabelAt(status,"检查安装与主题状态",20,79,520,21,9,muted,false);
            startButton=ButtonAt(status,"启动 Codex",624,35,154,false,()=>Run(async()=>{engine.Start(package);await Task.Delay(500);Notice("已打开 Codex。当前窗口若由原版入口启动，请正常退出后从主题入口重开一次。",false);}));
            var control=new Card{Location=new Point(232,236),Size=new Size(800,91)};Controls.Add(control);
            installButton=ButtonAt(control,"安装并启用",18,24,210,true,()=>Run(async()=>{await Task.Run(()=>engine.Install(package,true));engine.UpdatePreference(autoCheck.Checked);RefreshState();if(RuntimeClient.Connected(package))engine.Attach();Notice("已启用。开始菜单新增“Codex（自定义背景）”；当前工作保留，正常重开后全部生效。",false);}));
            disableButton=ButtonAt(control,"关闭主题",244,24,150,false,()=>Run(async()=>{await Task.Run(()=>engine.Disable(package,false));RefreshState();Notice("主题已关闭。当前 Codex 继续运行，完整退出重开后恢复全部原版状态。",false);}));
            ButtonAt(control,"应用内设置",410,24,160,false,()=>Run(async()=>{bool shown=await Task.Run(()=>RuntimeClient.OpenPanel(package));Notice(shown?"背景设置已在 Codex 标题栏打开。":"请先启用并从主题入口启动 Codex。",!shown);}));
            ButtonAt(control,"启动原版",586,24,194,false,()=>Run(async()=>{if(package==null||!package.Found)throw new IOException("尚未检测到官方 Codex。");desktop.Launch("explorer.exe","shell:AppsFolder\\"+package.AppId);await Task.CompletedTask;Notice("已打开官方入口。若要完全不加载主题，请先正常退出当前 Codex。",false);}));
            editor=new Card{Location=new Point(232,343),Size=new Size(800,257)};Controls.Add(editor);
            LabelAt(editor,"背景与透明度",18,15,300,26,12,foreground,true);
            picture=new PictureBox{Location=new Point(18,52),Size=new Size(228,121),BackColor=Color.FromArgb(31,43,68),SizeMode=PictureBoxSizeMode.Zoom};editor.Controls.Add(picture);
            imageName=LabelAt(editor,"沿用 Codex 中的背景",18,182,235,22,8,muted,false);
            ButtonAt(editor,"选择图片",18,211,110,false,ChooseImage);
            ButtonAt(editor,"移除图片",138,211,108,false,()=>{selectedImage=null;removeImage=true;enabled.Checked=false;imageName.Text="保存后移除背景图片";if(picture.Image!=null){picture.Image.Dispose();picture.Image=null;}});
            dim=Slider(editor,"背景遮罩",278,50,0,90,42,out dimValue);
            blur=Slider(editor,"模糊程度",538,50,0,24,0,out blurValue);
            panel=Slider(editor,"面板不透明度",278,117,0,100,70,out panelValue);
            bubble=Slider(editor,"消息气泡不透明度",538,117,0,100,18,out bubbleValue);
            enabled=new CheckBox{Text="启用图片背景",Checked=true,Location=new Point(280,209),Size=new Size(150,28),ForeColor=foreground,BackColor=Color.Transparent};editor.Controls.Add(enabled);
            fit=Dropdown(editor,new[]{"铺满窗口","完整显示"},440,211,108);
            position=Dropdown(editor,new[]{"居中","靠上","靠下","靠左","靠右"},558,211,88);
            applyButton=ButtonAt(editor,"保存应用",661,204,119,true,()=>Run(SaveSettings));
            var updates=new Card{Location=new Point(232,616),Size=new Size(800,81)};Controls.Add(updates);
            LabelAt(updates,"修改器更新",18,12,180,25,10,foreground,true);
            updateLabel=LabelAt(updates,"GitHub Releases · 独立于官方 Codex 更新",18,43,425,22,8.5f,muted,false);
            autoCheck=new CheckBox{Text="启动时检查",Checked=true,Location=new Point(478,25),Size=new Size(126,28),ForeColor=muted,BackColor=Color.Transparent};autoCheck.CheckedChanged+=(s,e)=>{if(!preview)engine.UpdatePreference(autoCheck.Checked);};updates.Controls.Add(autoCheck);
            updateButton=ButtonAt(updates,"检查更新",624,21,156,false,()=>Run(CheckOrInstallUpdate));
            toast=LabelAt(this,"PNG / JPG / WebP / GIF / AVIF · 图片仅保存到本机",234,711,580,36,8.5f,muted,false);
            uninstallButton=ButtonAt(this,"卸载修改功能",848,705,184,false,Uninstall);uninstallButton.ForeColor=Color.FromArgb(255,165,173);
            progress=new ProgressBar{Location=new Point(232,752),Size=new Size(800,4),Visible=false};Controls.Add(progress);
            if(preview){packageLabel.Text="Codex  26.1027.3384.0";statusLabel.Text="●  官方安装已识别  ·  主题已启用";statusLabel.ForeColor=Color.FromArgb(97,211,164);installButton.Text="重新应用 / 修复";updateLabel.Text="已是最新版 v"+BuildInfo.Version;}
        }
        ThemePicker Dropdown(Control parent,string[] items,int x,int y,int width){var box=new ThemePicker(items){Location=new Point(x,y),Size=new Size(width,28),AccessibleName=items[0]};parent.Controls.Add(box);return box;}
        ThemeSlider Slider(Control parent,string name,int x,int y,int min,int max,int value,out Label output){
            LabelAt(parent,name,x,y,175,22,9,muted,false);var local=LabelAt(parent,value+(max==24?" px":"%"),x+176,y,65,22,9,foreground,true);output=local;var bar=new ThemeSlider{Location=new Point(x-6,y+23),Size=new Size(242,30),Minimum=min,Maximum=max,Value=value,AccessibleName=name};bar.ValueChanged+=(s,e)=>local.Text=bar.Value+(max==24?" px":"%");parent.Controls.Add(bar);return bar;
        }
        async Task Initialize(){
            busy=true;
            try{RefreshState();package=await Task.Run(()=>desktop.Probe());if(postUpdate){var previous=engine.State();if(previous!=null){await Task.Run(()=>engine.Install(package,previous.Enabled));if(previous.Enabled&&RuntimeClient.Connected(package))engine.Attach();}}ThemeConfig config=null;try{config=await Task.Run(()=>RuntimeClient.ReadSettings(package));}catch{}if(config==null)config=JsonFiles.Read<ThemeConfig>(engine.Paths.Config);LoadSettings(config);var state=engine.State();autoCheck.Checked=state==null||state.CheckUpdates;}catch(Exception e){Notice(e.Message,true);packageLabel.Text="检测未完成，可稍后重试";}finally{busy=false;RefreshState();}
            if(uninstallRequested){Uninstall();return;}
            if(autoCheck.Checked)await CheckUpdateOnly();
        }
        public void RefreshState(){var state=engine.State();bool installed=state!=null;bool active=installed&&state.Enabled;packageLabel.Text=package!=null&&package.Found?"Codex  "+package.Version:"未检测到 Windows 官方 Codex";statusLabel.Text=active?"●  主题已启用  ·  官方版本自动识别":installed?"●  已安装修改器  ·  主题已关闭":engine.IsLegacy()?"检测到旧版背景加载器，可以升级接管":"尚未安装修改功能";statusLabel.ForeColor=active?Color.FromArgb(97,211,164):muted;installButton.Text=installed?"重新应用 / 修复":engine.IsLegacy()?"升级并接管":"安装并启用";installButton.Enabled=!busy&&package!=null&&package.Found;disableButton.Enabled=!busy&&active;applyButton.Enabled=!busy&&installed;uninstallButton.Enabled=!busy&&installed;startButton.Enabled=!busy&&package!=null&&package.Found;}
        void LoadSettings(ThemeConfig c){c.Normalize();enabled.Checked=c.enabled;dim.Value=c.dim;blur.Value=c.blur;panel.Value=c.panel;bubble.Value=c.bubble;fit.SelectedIndex=c.fit=="contain"?1:0;position.SelectedIndex=Array.IndexOf(new[]{"center","top","bottom","left","right"},c.position);}
        void ChooseImage(){if(busy)return;using(var dialog=new OpenFileDialog{Title="选择 Codex 背景图片",Filter="图片|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.avif",CheckFileExists=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;var file=new FileInfo(dialog.FileName);if(file.Length>25*1024*1024){Notice("请选择不超过 25 MB 的图片。",true);return;}selectedImage=file.FullName;removeImage=false;imageName.Text=file.Name;enabled.Checked=true;if(picture.Image!=null){picture.Image.Dispose();picture.Image=null;}try{using(var image=Image.FromFile(file.FullName)){if((long)image.Width*image.Height>60000000)throw new IOException();picture.Image=new Bitmap(image);}}catch{Notice("该格式会由 Codex 显示，修改器暂不显示缩略图。",false);}}}
        async Task SaveSettings(){var config=new ThemeConfig{enabled=enabled.Checked,dim=dim.Value,blur=blur.Value,panel=panel.Value,bubble=bubble.Value,fit=fit.SelectedIndex==1?"contain":"cover",position=new[]{"center","top","bottom","left","right"}[Math.Max(0,position.SelectedIndex)],imageAction=removeImage?"remove":"keep"};string image=selectedImage;bool active=engine.State().Enabled;bool applied=await Task.Run(()=>{engine.SaveTheme(config,image);return active&&RuntimeClient.ApplySettings(package,engine.Paths.Root,config);});selectedImage=null;removeImage=false;Notice(applied?"背景和透明度已应用到 Codex。":"设置已保存，将在下次主题启动后应用。",false);}
        async Task CheckUpdateOnly(){updateLabel.Text="正在连接 GitHub…";try{update=await Task.Run(()=>UpdateService.Check());updateLabel.Text=update==null?"尚无公开发行版本":update.Newer?"发现新版 v"+update.Version:"已是最新版 v"+BuildInfo.Version;updateButton.Text=update!=null&&update.Newer?"更新到 "+update.Version:"检查更新";}catch{updateLabel.Text="暂时连不上 GitHub，可稍后重试";}}
        async Task CheckOrInstallUpdate(){
            if(update==null||!update.Newer){await CheckUpdateOnly();return;}
            if(MessageBox.Show(this,"下载并安装修改器 v"+update.Version+"？\n\nCodex 当前工作会保留，更新后自动重新打开修改器。","发现新版本",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
            if(engine.State()==null){bool keepLegacy=engine.IsLegacy()&&package!=null&&package.Found;await Task.Run(()=>engine.Install(package,keepLegacy));if(keepLegacy&&RuntimeClient.Connected(package))engine.Attach();}progress.Visible=true;progress.Value=0;Notice("正在下载并验证 GitHub 发行包…",false);
            string file=await Task.Run(()=>UpdateService.Download(update,value=>BeginInvoke((Action)(()=>progress.Value=value))));
            engine.RunMaintenance(new MaintenancePlan{Operation="update",Source=file,Sha256=update.Digest});Application.Exit();
        }
        void Uninstall(){if(busy)return;using(var dialog=new Form{Text="卸载修改功能",ClientSize=new Size(430,210),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false,Font=Font}){
            var text=new Label{Text="移除修改器和主题加载，保留官方 Codex、\n聊天记录与账号资料。",Location=new Point(24,24),Size=new Size(380,54)};dialog.Controls.Add(text);var keep=new CheckBox{Text="保留我的背景和外观设置",Checked=true,Location=new Point(24,94),Size=new Size(370,28)};dialog.Controls.Add(keep);var ok=new Button{Text="卸载",DialogResult=DialogResult.OK,Location=new Point(218,155),Size=new Size(82,32)};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,Location=new Point(312,155),Size=new Size(90,32)};dialog.Controls.Add(ok);dialog.Controls.Add(cancel);dialog.AcceptButton=ok;dialog.CancelButton=cancel;if(dialog.ShowDialog(this)!=DialogResult.OK)return;bool delete=!keep.Checked;Run(async()=>{await Task.Run(()=>engine.PrepareUninstall(package,delete));engine.RunMaintenance(new MaintenancePlan{Operation="uninstall",DeletePreferences=delete});Application.Exit();});}}
        public void RequestUninstall(){uninstallRequested=true;}
        public void MarkPostUpdate(){postUpdate=true;}
        async void Run(Func<Task> action){if(busy)return;busy=true;editor.Enabled=false;autoCheck.Enabled=false;updateButton.Enabled=false;try{RefreshState();await action();}catch(Exception e){Notice(e is System.Net.WebException?"连接失败，请稍后重试。":e.Message,true);}finally{busy=false;if(!IsDisposed){editor.Enabled=true;autoCheck.Enabled=true;updateButton.Enabled=true;progress.Visible=false;try{RefreshState();}catch(Exception e){Notice(e.Message,true);}}}}
        void Notice(string text,bool error){toast.Text=text;toast.ForeColor=error?Color.FromArgb(255,156,165):Color.FromArgb(159,185,220);}
        void OpenWebsite(){desktop.Launch(BuildInfo.Website,"");}
        public void SavePreview(string file){CreateControl();Show();Application.DoEvents();using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(file,System.Drawing.Imaging.ImageFormat.Png);}Close();}
    }
}
