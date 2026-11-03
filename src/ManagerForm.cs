using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexThemeModifier {
    internal sealed class ManagerForm:Form {
        readonly ManagerEngine engine;
        readonly IDesktop desktop;
        readonly bool preview;
        readonly Dictionary<Control,RectangleF> bounds=new Dictionary<Control,RectangleF>();
        readonly Dictionary<string,UiButton> navigation=new Dictionary<string,UiButton>();
        readonly List<Control[]> sliderRows=new List<Control[]>();
        readonly ToolTip tips=new ToolTip{InitialDelay=400,AutoPopDelay=10000};
        UiPreferences preferences;UiPalette palette;PackageInfo package;UpdateInfo update;
        float unit=1;
        bool busy,loading,postUpdate,uninstallRequested,removeImage;
        string selectedImage,currentPage="appearance";
        UiPanel rail,appearance,management,settings;
        UiCard backgroundCard,tuningCard,packageCard,launchCard,removeCard,lookCard,updateCard,aboutCard;
        UiLabel heading,subtitle,packageLabel,statusLabel,toast,updateLabel,imageName,footerVersion,appearanceHint;
        UiButton startButton,installButton,disableButton,applyButton,updateButton,uninstallButton,themeButton,chooseButton,removeButton,appSettingsButton,originalButton;
        UiSwitch enabled,autoCheck,softBackground;
        ThemeSlider dim,blur,panel,bubble;
        ThemePicker fit,position,themeMode;
        ThemePreview picture;
        ProgressBar progress;
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);

        public ManagerForm(ManagerEngine engine,IDesktop desktop,bool preview){
            this.engine=engine;this.desktop=desktop;this.preview=preview;
            preferences=new UiPreferences();if(!preview)try{preferences=JsonFiles.Read<UiPreferences>(UiPreferenceFile)??new UiPreferences();}catch{}
            preferences.Normalize();palette=new UiPalette(IsDark());
            AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);
            using(var g=CreateGraphics())unit=g.DpiX/96f;
            Build();if(!preview)Shown+=async(s,e)=>await Initialize();
        }
        string UiPreferenceFile{get{return Path.Combine(engine.Paths.Preferences,"modifier-ui.json");}}
        float LogicalWidth{get{return ClientSize.Width/unit;}}
        float LogicalHeight{get{return ClientSize.Height/unit;}}
        bool IsDark(){if(preferences.Theme=="dark")return true;if(preferences.Theme=="light")return false;try{using(var k=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))return k!=null&&Convert.ToInt32(k.GetValue("AppsUseLightTheme",1))==0;}catch{return false;}}
        void Place(Control c,float x,float y,float w,float h){bounds[c]=new RectangleF(x,y,w,h);c.SetBounds((int)Math.Round(x*unit),(int)Math.Round(y*unit),(int)Math.Round(w*unit),(int)Math.Round(h*unit));}
        T Add<T>(T c,Control parent,float x,float y,float w,float h,float size=14)where T:Control{parent.Controls.Add(c);c.Font=UiFonts.Get(size*unit);Place(c,x,y,w,h);var themed=c as IUiControl;if(themed!=null)themed.Apply(palette,unit);return c;}
        UiLabel LabelAt(Control parent,string text,float x,float y,float w,float h,float size=14,bool muted=false){return Add(new UiLabel{Text=text,Muted=muted},parent,x,y,w,h,size);}
        UiButton ButtonAt(Control parent,string text,float x,float y,float w,float h,Action action=null,bool primary=false,string glyph=null,bool quiet=false){var b=Add(new UiButton{Text=text,Primary=primary,Glyph=glyph,Quiet=quiet},parent,x,y,w,h);if(action!=null)b.Click+=(s,e)=>action();return b;}
        UiCard CardAt(Control parent){return Add(new UiCard(),parent,0,0,100,100);}
        UiSwitch SwitchAt(Control parent,string name,bool check,float x,float y){return Add(new UiSwitch{Text=name,AccessibleName=name,Checked=check},parent,x,y,44,28);}
        void Build(){
            Text="Codex Theme · 主题修改器";StartPosition=FormStartPosition.CenterScreen;ClientSize=new Size((int)(1060*unit),(int)(740*unit));MinimumSize=new Size((int)(960*unit),(int)(680*unit));
            Font=UiFonts.Get(14*unit);try{Icon=new Icon(new MemoryStream(Resources.Bytes("payload.modifier.ico")));}catch{}
            rail=Add(new UiPanel(),this,0,0,72,740);
            var brand=ButtonAt(rail,"",16,22,40,40,null,false,"palette");brand.TabStop=false;brand.Selected=true;tips.SetToolTip(brand,"Codex Theme");
            int i=0;foreach(var item in new[]{new[]{"appearance","外观","image"},new[]{"manage","管理","manage"},new[]{"settings","设置","settings"}}){string id=item[0];var button=ButtonAt(rail,"",15,100+i*58,42,42,()=>SetView(id),false,item[2],true);button.AccessibleName=item[1];tips.SetToolTip(button,item[1]);navigation[id]=button;i++;}
            var github=ButtonAt(rail,"",16,636,40,36,()=>desktop.Launch(BuildInfo.Website,""),false,"open",true);github.AccessibleName="GitHub 项目";tips.SetToolTip(github,"GitHub 项目");github.Tag="github";
            var about=ButtonAt(rail,"",16,682,40,36,About,false,"info",true);about.AccessibleName="关于";tips.SetToolTip(about,"关于与工作原理");about.Tag="about";
            heading=LabelAt(this,"外观",96,20,420,40,25);
            subtitle=LabelAt(this,"给你的 Codex，换一种心情。",96,61,560,22,13,true);
            themeButton=ButtonAt(this,"",826,25,40,36,()=>{preferences.Theme=palette.Dark?"light":"dark";SyncAppearance();SaveAppearance();},false,"moon",true);themeButton.AccessibleName="切换浅色和深色";
            startButton=ButtonAt(this,"打开 Codex",878,25,158,36,()=>Run(async()=>{engine.Start(package);await Task.Delay(300);Notice("已打开 Codex；如果当前窗口由原版入口启动，正常退出后从主题入口重新打开即可。",false);}),true,"open");
            appearance=Add(new UiPanel(),this,96,100,940,560);management=Add(new UiPanel(),this,96,100,940,560);settings=Add(new UiPanel(),this,96,100,940,560);
            BuildAppearance();BuildManagement();BuildSettings();
            toast=LabelAt(this,"外观设置仅保存在这台电脑。",96,708,740,22,12,true);
            footerVersion=LabelAt(this,"v"+BuildInfo.Version,860,708,176,22,12,true);footerVersion.TextAlign=ContentAlignment.MiddleRight;
            progress=Add(new ProgressBar{Visible=false},this,96,736,940,3);
            Resize+=(s,e)=>LayoutSurface();
            LayoutSurface();SyncAppearance();SetView("appearance");RefreshState();
            if(preview){package=new PackageInfo{Found=true,Version="26.1027.6604.0"};RefreshState();updateLabel.Text="已是最新版本 · v"+BuildInfo.Version;}
        }
        void BuildAppearance(){
            backgroundCard=CardAt(appearance);tuningCard=CardAt(appearance);
            LabelAt(backgroundCard,"背景",22,16,180,32,17);
            var enabledLabel=LabelAt(backgroundCard,"启用背景",396,20,80,26,12,true);enabledLabel.Tag="enabled-label";
            enabled=SwitchAt(backgroundCard,"启用图片背景",true,488,20);
            picture=Add(new ThemePreview(),backgroundCard,20,64,514,288);
            imageName=LabelAt(backgroundCard,"沿用当前背景",22,382,260,30,12,true);
            chooseButton=ButtonAt(backgroundCard,"选择背景",292,382,146,36,ChooseImage,false,"folder");
            removeButton=ButtonAt(backgroundCard,"移除",448,382,86,36,()=>{selectedImage=null;removeImage=true;enabled.Checked=false;picture.SetPhoto(null);imageName.Text="保存后移除背景";PreviewChanged();},false,null,true);
            fit=Add(new ThemePicker(new[]{"铺满窗口","完整显示"}){AccessibleName="图片显示方式"},backgroundCard,20,438,249,34);
            position=Add(new ThemePicker(new[]{"居中","靠上","靠下","靠左","靠右"}){AccessibleName="图片位置"},backgroundCard,281,438,253,34);
            tips.SetToolTip(fit,"图片显示方式");tips.SetToolTip(position,"图片位置");
            LabelAt(tuningCard,"透明度与质感",22,16,290,32,17);
            LabelAt(tuningCard,"调整数值，在左侧查看效果",22,52,300,24,12,true);
            dim=Slider(tuningCard,"背景遮罩","让文字更容易阅读",98,90,42);
            blur=Slider(tuningCard,"模糊程度","柔化背景中的细节",190,24,0);
            panel=Slider(tuningCard,"面板不透明度","输入框与界面底板",282,100,70);
            bubble=Slider(tuningCard,"气泡不透明度","自己发送的消息背景",374,100,18);
            appearanceHint=LabelAt(appearance,"先预览，再应用到 Codex。",0,512,650,40,13,true);
            applyButton=ButtonAt(appearance,"保存并应用",780,512,160,40,()=>Run(SaveSettings),true,"check");
            enabled.CheckedChanged+=(s,e)=>PreviewChanged();fit.SelectedIndexChanged+=(s,e)=>PreviewChanged();position.SelectedIndexChanged+=(s,e)=>PreviewChanged();
        }
        ThemeSlider Slider(Control parent,string name,string hint,int y,int maximum,int value){
            var title=LabelAt(parent,name,22,y,220,25,14);
            var amount=LabelAt(parent,value+(maximum==24?" px":"%"),278,y,66,25,14);amount.Tag="amount";amount.TextAlign=ContentAlignment.MiddleRight;
            var detail=LabelAt(parent,hint,22,y+27,290,20,12,true);
            var slider=Add(new ThemeSlider{Minimum=0,Maximum=maximum,Value=value,AccessibleName=name},parent,16,y+49,334,25);
            slider.Tag=amount;sliderRows.Add(new Control[]{title,amount,detail,slider});slider.ValueChanged+=(s,e)=>{amount.Text=slider.Value+(maximum==24?" px":"%");PreviewChanged();};return slider;
        }
        void BuildManagement(){
            packageCard=CardAt(management);launchCard=CardAt(management);removeCard=CardAt(management);
            var icon=ButtonAt(packageCard,"",22,32,48,48,null,false,"codex");icon.Selected=true;icon.TabStop=false;
            LabelAt(packageCard,"官方桌面版",88,20,360,25,12,true);
            packageLabel=LabelAt(packageCard,"正在识别 Codex…",88,48,570,34,21);
            statusLabel=LabelAt(packageCard,"正在检查主题状态",24,112,680,26,13,true);
            installButton=ButtonAt(packageCard,"安装并启用",744,32,172,38,()=>Run(async()=>{await Task.Run(()=>engine.Install(package,true));engine.UpdatePreference(autoCheck.Checked);SaveAppearance();RefreshState();if(RuntimeClient.Connected(package))engine.Attach();Notice("主题已启用，开始菜单入口已准备好。当前工作保留，正常重开 Codex 后全部生效。",false);}),true);
            disableButton=ButtonAt(packageCard,"关闭主题",744,87,172,34,()=>Run(async()=>{await Task.Run(()=>engine.Disable(package,false));RefreshState();Notice("主题已关闭，背景和设置保留。完整退出并重开 Codex 后恢复全部原版状态。",false);}),false,null,true);
            LabelAt(launchCard,"启动与设置",22,16,500,32,17);
            LabelAt(launchCard,"从开始菜单打开，也可以在 Codex 内直接调整背景。",22,58,800,24,13,true);
            appSettingsButton=ButtonAt(launchCard,"Codex 内设置",22,104,180,36,()=>Run(async()=>{bool shown=await Task.Run(()=>RuntimeClient.OpenPanel(package));Notice(shown?"已在 Codex 标题栏打开背景设置。":"请先启用主题，并从主题入口打开 Codex。",!shown);}),false,"settings");
            originalButton=ButtonAt(launchCard,"打开原版",214,104,160,36,()=>Run(async()=>{if(package==null||!package.Found)throw new IOException("尚未检测到官方 Codex。");desktop.Launch("explorer.exe","shell:AppsFolder\\"+package.AppId);await Task.CompletedTask;Notice("已打开官方入口；如需恢复完整原版状态，请先关闭主题并正常重开 Codex。",false);}),false,"open");
            LabelAt(removeCard,"移除主题扩展",22,20,520,28,16);
            LabelAt(removeCard,"卸载时，可以选择保留背景图片和外观设置。",22,59,700,24,13,true).Tag="remove-description";
            uninstallButton=ButtonAt(removeCard,"卸载",794,38,122,36,Uninstall);uninstallButton.Destructive=true;
        }
        void BuildSettings(){
            lookCard=CardAt(settings);updateCard=CardAt(settings);aboutCard=CardAt(settings);
            LabelAt(lookCard,"修改器外观",22,20,450,30,17);
            themeMode=Add(new ThemePicker(new[]{"跟随系统","浅色","深色"}){AccessibleName="修改器主题"},lookCard,684,19,232,36);
            LabelAt(lookCard,"柔和背景",22,95,500,27,14);
            LabelAt(lookCard,"为窗口增加一点柔和的色彩层次",22,122,700,22,12,true);
            softBackground=SwitchAt(lookCard,"柔和背景",preferences.SoftBackground,872,100);
            themeMode.SelectedIndexChanged+=(s,e)=>{if(loading)return;preferences.Theme=new[]{"system","light","dark"}[themeMode.SelectedIndex];SyncAppearance();SaveAppearance();};
            softBackground.CheckedChanged+=(s,e)=>{if(loading)return;preferences.SoftBackground=softBackground.Checked;SaveAppearance();Invalidate(true);};
            LabelAt(updateCard,"版本更新",22,16,450,32,17);
            updateLabel=LabelAt(updateCard,"当前版本 v"+BuildInfo.Version,22,55,650,26,13,true);
            LabelAt(updateCard,"自动检查更新",22,106,220,26,14);
            autoCheck=SwitchAt(updateCard,"自动检查更新",true,195,106);autoCheck.CheckedChanged+=(s,e)=>{if(!preview&&!loading)try{engine.UpdatePreference(autoCheck.Checked);}catch(Exception error){Notice(error.Message,true);}};
            updateButton=ButtonAt(updateCard,"检查更新",744,48,172,36,()=>Run(CheckOrInstallUpdate),false,"refresh");
            LabelAt(aboutCard,"关于",22,16,400,32,17);
            LabelAt(aboutCard,"Codex Theme Modifier · v"+BuildInfo.Version,22,57,640,25,14);
            LabelAt(aboutCard,"独立开源主题工具",22,91,570,22,12,true).Tag="about-description";
            var source=ButtonAt(aboutCard,"GitHub 项目",744,21,172,36,()=>desktop.Launch(BuildInfo.Website,""),false,"open");source.Tag="source";
            var safety=ButtonAt(aboutCard,"工作原理",744,80,172,34,About,false,"info",true);safety.Tag="safety";
            var license=ButtonAt(aboutCard,"字体授权",594,80,138,34,ShowLicenses,false,null,true);license.Tag="license";
        }
        void LayoutSurface(){
            if(appearance==null)return;
            float w=LogicalWidth,h=LogicalHeight,cw=w-120,ch=h-174,cardHeight=ch-64,left=(cw-16)*.60f,right=cw-left-16;
            Place(rail,0,0,72,h);foreach(Control c in rail.Controls){if(Convert.ToString(c.Tag)=="github")Place(c,16,h-104,40,36);if(Convert.ToString(c.Tag)=="about")Place(c,16,h-58,40,36);}
            Place(themeButton,w-234,25,40,36);Place(startButton,w-182,25,158,36);
            foreach(var page in new[]{appearance,management,settings})Place(page,96,100,cw,ch);
            Place(backgroundCard,0,0,left,cardHeight);Place(tuningCard,left+16,0,right,cardHeight);
            foreach(Control c in backgroundCard.Controls)if(Convert.ToString(c.Tag)=="enabled-label")Place(c,left-152,20,82,26);
            Place(enabled,left-66,20,44,28);Place(picture,20,64,left-40,cardHeight-204);
            Place(imageName,22,cardHeight-113,left-288,36);Place(chooseButton,left-262,cardHeight-113,146,36);Place(removeButton,left-106,cardHeight-113,86,36);
            Place(fit,20,cardHeight-53,(left-52)/2,34);Place(position,left/2+6,cardHeight-53,(left-52)/2,34);
            for(int row=0;row<sliderRows.Count;row++){var controls=sliderRows[row];float y=92+row*Math.Min(92,(cardHeight-98)/4);Place(controls[0],22,y,right-112,25);Place(controls[1],right-89,y,67,25);Place(controls[2],22,y+27,right-44,20);Place(controls[3],16,y+49,right-32,25);}
            Place(appearanceHint,0,ch-45,cw-180,40);Place(applyButton,cw-160,ch-45,160,40);
            Place(packageCard,0,0,cw,160);Place(launchCard,0,176,cw,164);Place(removeCard,0,356,cw,114);
            Place(packageLabel,88,48,cw-310,34);Place(statusLabel,24,112,cw-240,26);Place(installButton,cw-196,32,172,38);Place(disableButton,cw-196,87,172,34);Place(uninstallButton,cw-146,38,122,36);
            foreach(Control c in removeCard.Controls)if(Convert.ToString(c.Tag)=="remove-description")Place(c,22,59,cw-200,24);
            float gap=ch<506?12:16,lookHeight=ch<506?158:174,updateHeight=ch<506?148:154,aboutTop=lookHeight+updateHeight+gap*2;
            Place(lookCard,0,0,cw,lookHeight);Place(updateCard,0,lookHeight+gap,cw,updateHeight);Place(aboutCard,0,aboutTop,cw,Math.Min(146,ch-aboutTop));
            Place(themeMode,cw-256,19,232,36);Place(softBackground,cw-68,100,44,28);Place(autoCheck,cw-68,106,44,28);Place(updateButton,cw-196,48,172,36);Place(updateLabel,22,55,cw-240,26);
            foreach(Control c in aboutCard.Controls){string tag=Convert.ToString(c.Tag);if(tag=="source")Place(c,cw-196,21,172,36);if(tag=="safety")Place(c,cw-196,80,172,34);if(tag=="license")Place(c,cw-346,80,138,34);if(tag=="about-description")Place(c,22,91,cw-390,22);}
            Place(toast,96,h-30,cw-110,22);Place(footerVersion,w-122,h-30,98,22);Place(progress,96,h-4,cw,3);Invalidate();
        }
        void SetView(string page){currentPage=page=="manage"||page=="settings"?page:"appearance";appearance.Visible=currentPage=="appearance";management.Visible=currentPage=="manage";settings.Visible=currentPage=="settings";
            heading.Text=currentPage=="appearance"?"外观":currentPage=="manage"?"管理":"设置";
            subtitle.Text=currentPage=="appearance"?"给你的 Codex，换一种心情。":currentPage=="manage"?"主题的启用、启动与恢复，都在这里。":"按你的习惯，调整修改器。";
            foreach(var item in navigation){item.Value.Selected=item.Key==currentPage;item.Value.Invalidate();}
        }
        void SyncAppearance(){loading=true;try{palette=new UiPalette(IsDark());BackColor=palette.Background;themeButton.Glyph=palette.Dark?"sun":"moon";tips.SetToolTip(themeButton,palette.Dark?"切换为浅色":"切换为深色");themeMode.SelectedIndex=preferences.Theme=="light"?1:preferences.Theme=="dark"?2:0;softBackground.Checked=preferences.SoftBackground;ApplyColors(this);ApplyWindowFrame();}finally{loading=false;}Invalidate(true);}
        void ApplyColors(Control parent){foreach(Control c in parent.Controls){var themed=c as IUiControl;if(themed!=null)themed.Apply(palette,unit);var label=c as UiLabel;if(label!=null)label.ForeColor=Convert.ToString(c.Tag)=="amount"?palette.Accent:label.Muted?palette.Muted:palette.Text;ApplyColors(c);}}
        void SaveAppearance(){if(preview)return;try{if(engine.State()!=null){engine.Paths.CheckRoot();JsonFiles.Write(UiPreferenceFile,preferences);}}catch(Exception error){Notice(error.Message,true);}}
        void ApplyWindowFrame(){if(!IsHandleCreated)return;try{int dark=palette.Dark?1:0,corner=2,bg=palette.Background.R|(palette.Background.G<<8)|(palette.Background.B<<16),ink=palette.Text.R|(palette.Text.G<<8)|(palette.Text.B<<16);DwmSetWindowAttribute(Handle,20,ref dark,4);DwmSetWindowAttribute(Handle,33,ref corner,4);DwmSetWindowAttribute(Handle,35,ref bg,4);DwmSetWindowAttribute(Handle,36,ref ink,4);}catch{}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);if(palette!=null)ApplyWindowFrame();}
        protected override void OnPaintBackground(PaintEventArgs e){
            if(palette==null){base.OnPaintBackground(e);return;}e.Graphics.Clear(palette.Background);
            if(preferences.SoftBackground&&Width>0&&Height>0){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Glow(e.Graphics,new RectangleF(Width*.12f,-Height*.30f,Width*.95f,Height*1.18f),palette.Dark?Color.FromArgb(42,91,124,164):Color.FromArgb(80,194,211,238));Glow(e.Graphics,new RectangleF(Width*.52f,Height*.21f,Width*.83f,Height),palette.Dark?Color.FromArgb(25,142,110,151):Color.FromArgb(57,216,207,236));}
            using(var pen=new Pen(Color.FromArgb(palette.Dark?140:190,palette.Line)))e.Graphics.DrawLine(pen,72*unit,20*unit,72*unit,ClientSize.Height-20*unit);
        }
        static void Glow(Graphics g,RectangleF r,Color color){using(var path=new GraphicsPath()){path.AddEllipse(r);using(var brush=new PathGradientBrush(path){CenterColor=color,SurroundColors=new[]{Color.FromArgb(0,color)}})g.FillPath(brush,path);}}
        void PreviewChanged(){if(picture==null||dim==null||bubble==null)return;picture.BackgroundEnabled=enabled.Checked;picture.Dim=dim.Value;picture.Blur=blur.Value;picture.Panel=panel.Value;picture.Bubble=bubble.Value;picture.Fit=fit.SelectedIndex==1?"contain":"cover";picture.Position=new[]{"center","top","bottom","left","right"}[position.SelectedIndex];picture.RefreshOptions();}
        async Task Initialize(){
            SetBusy(true);try{RefreshState();package=await Task.Run(()=>desktop.Probe());if(postUpdate){var old=engine.State();if(old!=null){await Task.Run(()=>engine.Install(package,old.Enabled));if(old.Enabled&&RuntimeClient.Connected(package))engine.Attach();}}
                ThemeConfig config=null;try{config=await Task.Run(()=>RuntimeClient.ReadSettings(package));}catch{}if(config==null)config=JsonFiles.Read<ThemeConfig>(engine.Paths.Config);LoadSettings(config);
                loading=true;try{var state=engine.State();autoCheck.Checked=state==null||state.CheckUpdates;}finally{loading=false;}
                try{byte[] image=await Task.Run(()=>RuntimeClient.ReadBackgroundPreview(package));if(image!=null){using(var stream=new MemoryStream(image))using(var bitmap=Image.FromStream(stream))picture.SetPhoto(new Bitmap(bitmap));picture.RefreshOptions();}}catch{}
            }catch(Exception error){if(!IsDisposed)Notice(error.Message,true);}finally{if(!IsDisposed){SetBusy(false);RefreshState();}}
            if(IsDisposed)return;if(uninstallRequested){Uninstall();return;}if(autoCheck.Checked)await CheckUpdateOnly();
        }
        void LoadSettings(ThemeConfig config){config=(config??new ThemeConfig());config.Normalize();enabled.Checked=config.enabled;dim.Value=config.dim;blur.Value=config.blur;panel.Value=config.panel;bubble.Value=config.bubble;fit.SelectedIndex=config.fit=="contain"?1:0;position.SelectedIndex=Array.IndexOf(new[]{"center","top","bottom","left","right"},config.position);PreviewChanged();}
        public void RefreshState(){var state=preview?new InstallState{Enabled=true}:engine.State();bool installed=state!=null,active=installed&&state.Enabled,found=package!=null&&package.Found;packageLabel.Text=found?"Codex  "+package.Version:"未检测到官方 Codex";statusLabel.Text=active?"主题已启用 · 官方版本自动识别":installed?"主题已关闭 · 图片和设置已保留":engine.IsLegacy()?"发现旧版加载器，可以直接升级":"尚未安装主题扩展";statusLabel.ForeColor=active?palette.Success:palette.Muted;installButton.Text=installed?"重新应用 / 修复":engine.IsLegacy()?"升级并接管":"安装并启用";installButton.Enabled=!busy&&found;disableButton.Enabled=!busy&&active;uninstallButton.Enabled=!busy&&installed;applyButton.Text=active?"保存并应用":installed?"启用并应用":"安装并应用";applyButton.Enabled=!busy&&found;startButton.Enabled=!busy&&found;appSettingsButton.Enabled=!busy&&active;originalButton.Enabled=!busy&&found;}
        void ChooseImage(){if(busy)return;using(var dialog=new OpenFileDialog{Title="选择 Codex 背景",Filter="图片|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.avif",CheckFileExists=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;var file=new FileInfo(dialog.FileName);if(file.Length>25*1024*1024){Notice("请选择不超过 25 MB 的图片。",true);return;}selectedImage=file.FullName;removeImage=false;imageName.Text=file.Name;tips.SetToolTip(imageName,file.Name);enabled.Checked=true;try{using(var image=Image.FromFile(file.FullName)){if((long)image.Width*image.Height>60000000)throw new IOException("图片尺寸超过 6000 万像素。");float scale=Math.Min(1,Math.Min(900f/image.Width,600f/image.Height));var thumbnail=new Bitmap(Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale)));using(var g=Graphics.FromImage(thumbnail)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,0,0,thumbnail.Width,thumbnail.Height);}picture.SetPhoto(thumbnail);PreviewChanged();}}catch{picture.SetPhoto(null);Notice("图片已选择。此格式会在保存后由 Codex 显示预览。",false);}}}
        async Task SaveSettings(){var config=new ThemeConfig{enabled=enabled.Checked,dim=dim.Value,blur=blur.Value,panel=panel.Value,bubble=bubble.Value,fit=fit.SelectedIndex==1?"contain":"cover",position=new[]{"center","top","bottom","left","right"}[position.SelectedIndex],imageAction=removeImage?"remove":"keep"};string image=selectedImage;var state=engine.State();if(state==null||!state.Enabled){await Task.Run(()=>engine.Install(package,true));engine.UpdatePreference(autoCheck.Checked);SaveAppearance();if(RuntimeClient.Connected(package))engine.Attach();}bool active=true;bool applied=await Task.Run(()=>{engine.SaveTheme(config,image);return active&&RuntimeClient.ApplySettings(package,engine.Paths.Root,config);});selectedImage=null;removeImage=false;if(applied)try{byte[] bytes=await Task.Run(()=>RuntimeClient.ReadBackgroundPreview(package));if(bytes!=null){using(var stream=new MemoryStream(bytes))using(var bitmap=Image.FromStream(stream))picture.SetPhoto(new Bitmap(bitmap));PreviewChanged();}}catch{}Notice(applied?"背景与透明度已保存并应用。":"设置已保存，下次从主题入口打开 Codex 时应用。",false);}
        async Task CheckUpdateOnly(){updateLabel.Text="正在检查 GitHub 新版本…";try{update=await Task.Run(()=>UpdateService.Check());updateLabel.Text=update==null?"暂时没有公开发行版本":update.Newer?"发现新版本 v"+update.Version:"已是最新版本 · v"+BuildInfo.Version;updateButton.Text=update!=null&&update.Newer?"更新到 "+update.Version:"检查更新";}catch{updateLabel.Text="暂时无法连接 GitHub，稍后可重试";}}
        async Task CheckOrInstallUpdate(){
            if(update==null||!update.Newer){await CheckUpdateOnly();return;}
            if(MessageBox.Show(this,"更新修改器到 v"+update.Version+"？\n当前 Codex 工作会保留。","发现新版本",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
            if(engine.State()==null){bool legacy=engine.IsLegacy()&&package!=null&&package.Found;await Task.Run(()=>engine.Install(package,legacy));if(legacy&&RuntimeClient.Connected(package))engine.Attach();}progress.Visible=true;progress.Value=0;Notice("正在下载并验证新版本…",false);
            string file=await Task.Run(()=>UpdateService.Download(update,value=>{if(!IsDisposed)BeginInvoke((Action)(()=>{if(!IsDisposed)progress.Value=value;}));}));engine.RunMaintenance(new MaintenancePlan{Operation="update",Source=file,Sha256=update.Digest});Application.Exit();
        }
        void SetBusy(bool value){busy=value;appearance.Enabled=!value;autoCheck.Enabled=!value;updateButton.Enabled=!value;}
        async void Run(Func<Task> action){if(busy)return;SetBusy(true);try{RefreshState();await action();}catch(Exception error){if(!IsDisposed)Notice(error.Message,true);}finally{if(!IsDisposed){SetBusy(false);progress.Visible=false;try{RefreshState();}catch(Exception error){Notice(error.Message,true);}}}}
        void Notice(string text,bool error){toast.Text=text;toast.ForeColor=error?palette.Danger:palette.Muted;tips.SetToolTip(toast,text);}
        void Uninstall(){if(busy)return;using(var dialog=new Form{Text="移除主题扩展",ClientSize=new Size((int)(440*unit),(int)(220*unit)),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false,Font=UiFonts.Get(14*unit),BackColor=palette.Card,ForeColor=palette.Text}){
            var text=new UiLabel{Text="卸载主题修改器，保留官方 Codex、\n聊天记录与账号资料。",Font=dialog.Font,ForeColor=palette.Text};text.SetBounds((int)(24*unit),(int)(22*unit),(int)(390*unit),(int)(62*unit));dialog.Controls.Add(text);
            var keep=new CheckBox{Text="保留背景图片和外观设置",Checked=true,Font=dialog.Font,ForeColor=palette.Text,BackColor=palette.Card};keep.SetBounds((int)(24*unit),(int)(100*unit),(int)(390*unit),(int)(28*unit));dialog.Controls.Add(keep);
            var ok=new UiButton{Text="卸载",Destructive=true,DialogResult=DialogResult.OK,Font=dialog.Font};ok.Apply(palette,unit);ok.SetBounds((int)(225*unit),(int)(164*unit),(int)(86*unit),(int)(36*unit));
            var cancel=new UiButton{Text="取消",DialogResult=DialogResult.Cancel,Font=dialog.Font};cancel.Apply(palette,unit);cancel.SetBounds((int)(322*unit),(int)(164*unit),(int)(94*unit),(int)(36*unit));dialog.Controls.Add(ok);dialog.Controls.Add(cancel);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;bool delete=!keep.Checked;Run(async()=>{await Task.Run(()=>engine.PrepareUninstall(package,delete));engine.RunMaintenance(new MaintenancePlan{Operation="uninstall",DeletePreferences=delete});Application.Exit();});}}
        void About(){MessageBox.Show(this,"独立开源主题工具，与 OpenAI 无隶属关系。\n\n保留官方安装包、账号、功能和更新方式。主题通过本机调试连接加载，同机其他程序可能连接该端口。未来官方界面变化可能需要适配。\n\n可以随时关闭或卸载主题。",Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}
        void ShowLicenses(){using(var dialog=new Form{Text="字体授权",ClientSize=new Size((int)(650*unit),(int)(500*unit)),StartPosition=FormStartPosition.CenterParent,Font=UiFonts.Get(14*unit),BackColor=palette.Card,ForeColor=palette.Text,Padding=new Padding((int)(22*unit))}){
            var text=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Font=dialog.Font,BackColor=palette.Card,ForeColor=palette.Text,Text=(Resources.Text("ui.font-notice")+"\n\n"+Resources.Text("ui.font-license")).Replace("\r\n","\n").Replace("\n",Environment.NewLine)};dialog.Controls.Add(text);dialog.ShowDialog(this);}}
        public void RequestUninstall(){uninstallRequested=true;}
        public void MarkPostUpdate(){postUpdate=true;}
        public void ConfigurePreview(bool dark,string page,bool compact=false){preferences.Theme=dark?"dark":"light";if(compact)ClientSize=new Size((int)(960*unit),(int)(650*unit));SyncAppearance();SetView(page);}
        public void SavePreview(string file){CreateControl();Show();Application.DoEvents();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(file,System.Drawing.Imaging.ImageFormat.Png);}Close();}
        protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
    }
}
