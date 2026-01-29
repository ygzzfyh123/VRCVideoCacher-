using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace VRCVideoCacher;

public class ConfigForm : Form
{
    private readonly TextBox tbYtdlWebServerURL = new() { Width = 400 };
    private readonly TextBox tbYtdlPath = new() { Width = 320 };
    private readonly Button btnBrowseYtdl = new() { Text = "浏览..." };
    private readonly CheckBox cbUseCookies = new() { Text = "使用浏览器 cookies" };
    private readonly CheckBox cbAutoUpdate = new() { Text = "自动更新 yt-dlp/ffmpeg/deno" };
    private readonly TextBox tbAdditionalArgs = new() { Width = 400 };
    private readonly TextBox tbDubLanguage = new() { Width = 200 };
    private readonly NumericUpDown numDelay = new() { Minimum = 0, Maximum = 600, Width = 80 };
    private readonly TextBox tbCachedAssetPath = new() { Width = 320 };
    private readonly Button btnBrowseCache = new() { Text = "浏览..." };
    private readonly TextBox tbBlockedUrls = new() { Multiline = true, Width = 400, Height = 80, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox tbBlockRedirect = new() { Width = 400 };
    private readonly CheckBox cbCacheYouTube = new() { Text = "缓存 YouTube 视频" };
    private readonly NumericUpDown numCacheMaxRes = new() { Minimum = 0, Maximum = 4320, Width = 80 };
    private readonly NumericUpDown numCacheMaxLength = new() { Minimum = 0, Maximum = 10000, Width = 80 };
    private readonly NumericUpDown numCacheMaxSizeGb = new() { DecimalPlaces = 1, Minimum = 0, Maximum = 10000, Width = 80 };
    private readonly CheckBox cbCachePyPyDance = new() { Text = "缓存 PyPyDance 视频" };
    private readonly CheckBox cbCacheVRDancing = new() { Text = "缓存 VRDancing 视频" };
    private readonly CheckBox cbPatchResonite = new() { Text = "启用 Resonite 补丁" };
    private readonly TextBox tbResonitePath = new() { Width = 320 };
    private readonly Button btnBrowseResonite = new() { Text = "浏览..." };
    private readonly CheckBox cbPatchVRC = new() { Text = "启用 VRChat 补丁" };
    private readonly CheckBox cbAutoUpdateApp = new() { Text = "程序自动更新" };
    private readonly TextBox tbPreCacheUrls = new() { Multiline = true, Width = 400, Height = 80, ScrollBars = ScrollBars.Vertical };

    private readonly Button btnSave = new() { Text = "保存", Width = 100 };
    private readonly Button btnCancel = new() { Text = "取消", Width = 100 };

    public ConfigForm()
    {
        Text = "VRCVideoCacher - 配置";
        Width = 820;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        void AddLabeled(Control control, string label)
        {
            var lbl = new Label { Text = label, AutoSize = true, Width = 760 };
            panel.Controls.Add(lbl);
            panel.Controls.Add(control);
        }

        AddLabeled(tbYtdlWebServerURL, "ytdlWebServerURL:");
        var ytdlPathPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 760, Height = tbYtdlPath.Height };
        ytdlPathPanel.Controls.Add(tbYtdlPath);
        ytdlPathPanel.Controls.Add(btnBrowseYtdl);
        panel.Controls.Add(new Label { Text = "ytdlPath:", AutoSize = true, Width = 760 });
        panel.Controls.Add(ytdlPathPanel);

        panel.Controls.Add(cbUseCookies);
        panel.Controls.Add(cbAutoUpdate);
        AddLabeled(tbAdditionalArgs, "ytdlAdditionalArgs:");
        AddLabeled(tbDubLanguage, "ytdlDubLanguage:");
        var delayPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 760, Height = numDelay.Height };
        delayPanel.Controls.Add(new Label { Text = "ytdlDelay (秒):", AutoSize = true });
        delayPanel.Controls.Add(numDelay);
        panel.Controls.Add(delayPanel);

        var cachePathPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 760, Height = tbCachedAssetPath.Height };
        cachePathPanel.Controls.Add(tbCachedAssetPath);
        cachePathPanel.Controls.Add(btnBrowseCache);
        panel.Controls.Add(new Label { Text = "CachedAssetPath:", AutoSize = true, Width = 760 });
        panel.Controls.Add(cachePathPanel);

        AddLabeled(tbBlockedUrls, "BlockedUrls (每行一个):");
        AddLabeled(tbBlockRedirect, "BlockRedirect:");

        panel.Controls.Add(cbCacheYouTube);
        var cacheResPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 760, Height = numCacheMaxRes.Height };
        cacheResPanel.Controls.Add(new Label { Text = "CacheYouTubeMaxResolution:", AutoSize = true });
        cacheResPanel.Controls.Add(numCacheMaxRes);
        cacheResPanel.Controls.Add(new Label { Text = "CacheYouTubeMaxLength (分钟):", AutoSize = true });
        cacheResPanel.Controls.Add(numCacheMaxLength);
        cacheResPanel.Controls.Add(new Label { Text = "CacheMaxSizeInGb:", AutoSize = true });
        cacheResPanel.Controls.Add(numCacheMaxSizeGb);
        panel.Controls.Add(cacheResPanel);

        panel.Controls.Add(cbCachePyPyDance);
        panel.Controls.Add(cbCacheVRDancing);
        panel.Controls.Add(cbPatchResonite);
        var resonitePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 760, Height = tbResonitePath.Height };
        resonitePanel.Controls.Add(tbResonitePath);
        resonitePanel.Controls.Add(btnBrowseResonite);
        panel.Controls.Add(new Label { Text = "ResonitePath:", AutoSize = true, Width = 760 });
        panel.Controls.Add(resonitePanel);

        panel.Controls.Add(cbPatchVRC);
        panel.Controls.Add(cbAutoUpdateApp);
        AddLabeled(tbPreCacheUrls, "PreCacheUrls (JSON 列表):");

        var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Width = 760, Height = 40 };
        btnPanel.Controls.Add(btnSave);
        btnPanel.Controls.Add(btnCancel);
        panel.Controls.Add(btnPanel);

        Controls.Add(panel);

        Load += ConfigForm_Load;
        btnBrowseYtdl.Click += BtnBrowseYtdl_Click;
        btnBrowseCache.Click += BtnBrowseCache_Click;
        btnBrowseResonite.Click += BtnBrowseResonite_Click;
        btnSave.Click += BtnSave_Click;
        btnCancel.Click += (_, _) => Close();
    }

    private void ConfigForm_Load(object? sender, EventArgs e)
    {
        var cfg = ConfigManager.Config;
        tbYtdlWebServerURL.Text = cfg.ytdlWebServerURL;
        tbYtdlPath.Text = cfg.ytdlPath;
        cbUseCookies.Checked = cfg.ytdlUseCookies;
        cbAutoUpdate.Checked = cfg.ytdlAutoUpdate;
        tbAdditionalArgs.Text = cfg.ytdlAdditionalArgs;
        tbDubLanguage.Text = cfg.ytdlDubLanguage;
        numDelay.Value = cfg.ytdlDelay;
        tbCachedAssetPath.Text = cfg.CachedAssetPath;
        tbBlockedUrls.Text = string.Join(Environment.NewLine, cfg.BlockedUrls ?? Array.Empty<string>());
        tbBlockRedirect.Text = cfg.BlockRedirect;
        cbCacheYouTube.Checked = cfg.CacheYouTube;
        numCacheMaxRes.Value = cfg.CacheYouTubeMaxResolution;
        numCacheMaxLength.Value = cfg.CacheYouTubeMaxLength;
        numCacheMaxSizeGb.Value = (decimal)cfg.CacheMaxSizeInGb;
        cbCachePyPyDance.Checked = cfg.CachePyPyDance;
        cbCacheVRDancing.Checked = cfg.CacheVRDancing;
        cbPatchResonite.Checked = cfg.PatchResonite;
        tbResonitePath.Text = cfg.ResonitePath;
        cbPatchVRC.Checked = cfg.PatchVRC;
        cbAutoUpdateApp.Checked = cfg.AutoUpdate;
        tbPreCacheUrls.Text = cfg.PreCacheUrls != null ? string.Join(Environment.NewLine, cfg.PreCacheUrls) : string.Empty;
    }

    private void BtnBrowseYtdl_Click(object? sender, EventArgs e)
    {
        using var of = new OpenFileDialog { Filter = "Executable|*.exe|All files|*.*", FileName = tbYtdlPath.Text };
        if (of.ShowDialog() == DialogResult.OK)
            tbYtdlPath.Text = of.FileName;
    }

    private void BtnBrowseCache_Click(object? sender, EventArgs e)
    {
        using var fd = new FolderBrowserDialog { SelectedPath = string.IsNullOrEmpty(tbCachedAssetPath.Text) ? Environment.CurrentDirectory : tbCachedAssetPath.Text };
        if (fd.ShowDialog() == DialogResult.OK)
            tbCachedAssetPath.Text = fd.SelectedPath;
    }

    private void BtnBrowseResonite_Click(object? sender, EventArgs e)
    {
        using var of = new OpenFileDialog { Filter = "All files|*.*", FileName = tbResonitePath.Text };
        if (of.ShowDialog() == DialogResult.OK)
            tbResonitePath.Text = of.FileName;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var cfg = ConfigManager.Config;
        cfg.ytdlWebServerURL = tbYtdlWebServerURL.Text.Trim();
        cfg.ytdlPath = tbYtdlPath.Text.Trim();
        cfg.ytdlUseCookies = cbUseCookies.Checked;
        cfg.ytdlAutoUpdate = cbAutoUpdate.Checked;
        cfg.ytdlAdditionalArgs = tbAdditionalArgs.Text;
        cfg.ytdlDubLanguage = tbDubLanguage.Text;
        cfg.ytdlDelay = (int)numDelay.Value;
        cfg.CachedAssetPath = tbCachedAssetPath.Text.Trim();
        cfg.BlockedUrls = tbBlockedUrls.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        cfg.BlockRedirect = tbBlockRedirect.Text.Trim();
        cfg.CacheYouTube = cbCacheYouTube.Checked;
        cfg.CacheYouTubeMaxResolution = (int)numCacheMaxRes.Value;
        cfg.CacheYouTubeMaxLength = (int)numCacheMaxLength.Value;
        cfg.CacheMaxSizeInGb = (float)numCacheMaxSizeGb.Value;
        cfg.CachePyPyDance = cbCachePyPyDance.Checked;
        cfg.CacheVRDancing = cbCacheVRDancing.Checked;
        cfg.PatchResonite = cbPatchResonite.Checked;
        cfg.ResonitePath = tbResonitePath.Text.Trim();
        cfg.PatchVRC = cbPatchVRC.Checked;
        cfg.AutoUpdate = cbAutoUpdateApp.Checked;
        cfg.PreCacheUrls = tbPreCacheUrls.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        ConfigManager.TrySaveConfig();
        MessageBox.Show("配置已保存。", "保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }

    public static void RunForm()
    {
        var t = new Thread(() =>
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new ConfigForm());
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
    }
}

