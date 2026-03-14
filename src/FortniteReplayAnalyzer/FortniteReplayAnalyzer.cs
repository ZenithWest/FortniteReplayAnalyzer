using FortniteReplayReader;
using FortniteReplayReader.Models;
using System.Globalization;
using System.Runtime.CompilerServices;
using Unreal.Core.Exceptions;
using Unreal.Core.Models.Enums;

namespace FortniteReplayAnalyzer;

public partial class FortniteReplayAnalyzer : Form
{
    private const int ExpandedReplayPaneWidth = 720;
    private const int CollapsedReplayPaneWidth = 52;
    private const int MaxParallelReplayLoads = 10;

    private static readonly string DefaultReplayFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame",
        "Saved",
        "Demos");

    private readonly List<ReplayBrowserRow> _replayRows = [];
    private readonly HashSet<string> _manuallyAddedReplayPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PlayerSummaryRow> _playerRows = [];
    private readonly Dictionary<string, int> _replayBrowserColumnWidths = new(StringComparer.Ordinal);
    private readonly Queue<ReplayBrowserRow> _pendingReplayLoads = new();
    private readonly HashSet<string> _pendingReplayLoadPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConditionalWeakTable<FortniteReplay, ReplayLookupCache> _replayLookupCaches = new();

    private string _replaySortColumn = nameof(ReplayBrowserRow.RecordedAt);
    private bool _replaySortAscending;
    private string _playerSortColumn = nameof(PlayerSummaryRow.Kills);
    private bool _playerSortAscending;
    private bool _isReplayPaneCollapsed;
    private bool _suppressReplaySelectionChanged;

    private ReplayBrowserRow? _selectedReplayRow;
    private PlayerData? _selectedPlayer;
    private AnalyzerSettings _settings;
    private MenuStrip? _menuStrip;
    private CheckBox? _chkTeamKillFeedOnly;
    private CheckBox? _chkKillFeedPlayers;
    private CheckBox? _chkKillFeedBots;
    private CheckBox? _chkTeamDamageOnly;
    private CheckBox? _chkDamagePlayers;
    private CheckBox? _chkDamageBots;
    private CheckBox? _chkDamageStructures;
    private CheckBox? _chkDamageNpcs;
    private CheckBox? _chkPlayerDamagePlayers;
    private CheckBox? _chkPlayerDamageBots;
    private CheckBox? _chkPlayerDamageStructures;
    private CheckBox? _chkPlayerDamageNpcs;
    private CheckBox? _chkPlayerKillLogPlayers;
    private CheckBox? _chkPlayerKillLogBots;
    private GroupBox? _grpPlayerDamageLog;
    private DataGridView? _dgvPlayerDamageLog;
    private TabControl? _openedReplayTabs;
    private TabControl? _centerPanelTabs;
    private TabControl? _playerPanelTabs;
    private TabControl? _playerSubjectTabs;
    private Label? _lblReplayHideHint;
    private ContextMenuStrip? _replayBrowserContextMenu;
    private ToolStripMenuItem? _loadReplayMenuItem;
    private ToolStripMenuItem? _loadAllReplayMenuItem;
    private ToolStripMenuItem? _unloadReplayMenuItem;
    private ToolStripMenuItem? _stopReplayMenuItem;
    private ReplayBrowserRow? _contextMenuReplayRow;
    private bool _suppressReplayTabSelection;
    private bool _isProcessingReplayQueue;
    private int _lastExpandedReplayPaneWidth = ExpandedReplayPaneWidth;
    private readonly HashSet<DataGridView> _iconTextConfiguredGrids = [];
    private PictureBox? _picSelectedPlayer;
    private bool _ignoreReplayBrowserCellClick;
    private bool _ignoreReplaySelectionChanged;
    private bool _isReplayDragSelecting;
    private bool _replayDragSelectionChanged;
    private int _replayDragStartIndex = -1;
    private bool _replayRowsRefreshPending;
    private bool _suppressPlayerSubjectTabSelection;

    private string ReplayFolder => string.IsNullOrWhiteSpace(_settings.DefaultReplaysFolder) ? DefaultReplayFolder : _settings.DefaultReplaysFolder;

    internal FortniteReplayAnalyzer(AnalyzerSettings settings)
    {
        _settings = settings.Clone();
        InitializeComponent();
        ConfigureGrids();
        InitializeDynamicUi();
        ApplySettingsToUi();
        WireEvents();
        Shown += (_, _) => LayoutContentBelowMenu();
        Resize += (_, _) => LayoutContentBelowMenu();
        DebugOutputWriter.LogInfo("Fortnite Replay Analyzer started.");
    }


    private void InitializeDynamicUi()
    {
        InitializeMenuStrip();
        Size = new Size(1800, 900);

        replayBrowserLayout.Padding = new Padding(0, 6, 0, 0);
        splitMain.Panel1MinSize = CollapsedReplayPaneWidth;
        splitMain.SplitterWidth = 8;
        splitMain.BackColor = Color.FromArgb(184, 194, 208);
        splitContent.SplitterWidth = 8;
        splitContent.BackColor = Color.FromArgb(184, 194, 208);
        grpCombatEvents.Text = "Damage Events";
        grpPlayerCombatLog.Text = "Kill Log";

        btnToggleReplayPane.Visible = false;
        btnRefreshReplays.Visible = false;
        replayBrowserHeader.Cursor = Cursors.Hand;
        lblReplayHeader.Cursor = Cursors.Hand;
        lblReplayStatus.Cursor = Cursors.Hand;

        _lblReplayHideHint = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(73, 82, 95),
            Text = "(Click to Hide)",
            Cursor = Cursors.Hand
        };
        replayBrowserHeader.Controls.Add(_lblReplayHideHint);
        _lblReplayHideHint.BringToFront();

        replayBrowserHeader.Click += (_, _) => SetReplayPaneCollapsed(!_isReplayPaneCollapsed);
        lblReplayHeader.Click += (_, _) => SetReplayPaneCollapsed(!_isReplayPaneCollapsed);
        lblReplayStatus.Click += (_, _) => SetReplayPaneCollapsed(!_isReplayPaneCollapsed);
        _lblReplayHideHint.Click += (_, _) => SetReplayPaneCollapsed(!_isReplayPaneCollapsed);
        InitializeReplayBrowserContextMenu();

        _picSelectedPlayer = new PictureBox
        {
            Size = new Size(40, 40),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(8, 8),
            BorderStyle = BorderStyle.FixedSingle
        };
        playerHeaderPanel.Controls.Add(_picSelectedPlayer);
        playerHeaderPanel.Controls.SetChildIndex(_picSelectedPlayer, 0);
        lblPlayerPanelTitle.Location = new Point(56, 8);
        lblSelectedPlayer.Location = new Point(56, 40);

        var killFeedFilterPanel = CreateFilterFlowPanel();
        _chkTeamKillFeedOnly = CreateFilterCheckBox("Team members only", (_, _) =>
        {
            if (_selectedReplayRow is not null)
            {
                BuildKillFeed(_selectedReplayRow);
            }
        });
        _chkKillFeedPlayers = CreateFilterCheckBox("Players", (_, _) =>
        {
            if (_selectedReplayRow is not null)
            {
                BuildKillFeed(_selectedReplayRow);
            }
        }, true);
        _chkKillFeedBots = CreateFilterCheckBox("Bots", (_, _) =>
        {
            if (_selectedReplayRow is not null)
            {
                BuildKillFeed(_selectedReplayRow);
            }
        }, true);
        killFeedFilterPanel.Controls.Add(_chkTeamKillFeedOnly);
        killFeedFilterPanel.Controls.Add(_chkKillFeedPlayers);
        killFeedFilterPanel.Controls.Add(_chkKillFeedBots);

        var killFeedLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 2
        };
        killFeedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        killFeedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        killFeedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        grpKillFeed.Controls.Remove(dgvKillFeed);
        killFeedLayout.Controls.Add(killFeedFilterPanel, 0, 0);
        killFeedLayout.Controls.Add(dgvKillFeed, 0, 1);
        grpKillFeed.Controls.Add(killFeedLayout);

        var damageFilterPanel = CreateFilterFlowPanel();

        _chkTeamDamageOnly = CreateFilterCheckBox("Team members only", (_, _) => RefreshDamageEventViews());
        _chkDamagePlayers = CreateFilterCheckBox("Players", (_, _) => RefreshDamageEventViews(), true);
        _chkDamageBots = CreateFilterCheckBox("Bots", (_, _) => RefreshDamageEventViews(), true);
        _chkDamageStructures = CreateFilterCheckBox("Structure", (_, _) => RefreshDamageEventViews(), false);
        _chkDamageNpcs = CreateFilterCheckBox("NPC", (_, _) => RefreshDamageEventViews(), true);
        _chkDamageNpcs.Visible = false;

        damageFilterPanel.Controls.Add(_chkTeamDamageOnly);
        damageFilterPanel.Controls.Add(_chkDamagePlayers);
        damageFilterPanel.Controls.Add(_chkDamageBots);
        damageFilterPanel.Controls.Add(_chkDamageStructures);
        damageFilterPanel.Controls.Add(_chkDamageNpcs);

        damageFilterPanel.Dock = DockStyle.Top;
        damageFilterPanel.BackColor = SystemColors.Control;

        grpCombatEvents.Controls.Clear();
        dgvCombatEvents.Dock = DockStyle.Fill;
        grpCombatEvents.Controls.Add(dgvCombatEvents);
        grpCombatEvents.Controls.Add(damageFilterPanel);

        var playerKillLogFilterPanel = CreateFilterFlowPanel();
        _chkPlayerKillLogPlayers = CreateFilterCheckBox("Players", (_, _) => RefreshPlayerKillLog(), true);
        _chkPlayerKillLogBots = CreateFilterCheckBox("Bots", (_, _) => RefreshPlayerKillLog(), true);
        playerKillLogFilterPanel.Controls.Add(_chkPlayerKillLogPlayers);
        playerKillLogFilterPanel.Controls.Add(_chkPlayerKillLogBots);

        grpPlayerCombatLog.Controls.Clear();
        dgvPlayerCombatLog.Dock = DockStyle.Fill;
        grpPlayerCombatLog.Controls.Add(dgvPlayerCombatLog);
        grpPlayerCombatLog.Controls.Add(playerKillLogFilterPanel);

        _grpPlayerDamageLog = new GroupBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Padding = new Padding(10),
            Text = "Damage Log"
        };

        var playerDamageFilterPanel = CreateFilterFlowPanel();
        _chkPlayerDamagePlayers = CreateFilterCheckBox("Players", (_, _) => RefreshDamageEventViews(), true);
        _chkPlayerDamageBots = CreateFilterCheckBox("Bots", (_, _) => RefreshDamageEventViews(), true);
        _chkPlayerDamageStructures = CreateFilterCheckBox("Structure", (_, _) => RefreshDamageEventViews(), false);
        _chkPlayerDamageNpcs = CreateFilterCheckBox("NPC", (_, _) => RefreshDamageEventViews(), true);
        _chkPlayerDamageNpcs.Visible = false;
        playerDamageFilterPanel.Controls.Add(_chkPlayerDamagePlayers);
        playerDamageFilterPanel.Controls.Add(_chkPlayerDamageBots);
        playerDamageFilterPanel.Controls.Add(_chkPlayerDamageStructures);
        playerDamageFilterPanel.Controls.Add(_chkPlayerDamageNpcs);

        _dgvPlayerDamageLog = new DataGridView { Name = "dgvPlayerDamageLog" };
        ConfigureReadOnlyGrid(_dgvPlayerDamageLog, fullRowSelect: true);
        _dgvPlayerDamageLog.AutoGenerateColumns = false;
        BuildCombatEventColumns(_dgvPlayerDamageLog);
        EnsureIconTextRendering(_dgvPlayerDamageLog);
        _dgvPlayerDamageLog.CellClick += (_, e) => HandleCombatEventLinkClick(_dgvPlayerDamageLog, e);

        var playerDamageLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 2
        };
        playerDamageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        playerDamageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        playerDamageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        playerDamageLayout.Controls.Add(playerDamageFilterPanel, 0, 0);
        playerDamageLayout.Controls.Add(_dgvPlayerDamageLog, 0, 1);
        _grpPlayerDamageLog.Controls.Add(playerDamageLayout);

        playerContentLayout.SetColumnSpan(grpPlayerVictims, 1);
        playerContentLayout.Controls.Add(_grpPlayerDamageLog, 1, 1);

        _centerPanelTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            HotTrack = true,
            Multiline = false,
            Padding = new Point(14, 4)
        };
        centerLayout.Parent?.Controls.Remove(centerLayout);
        splitContent.Panel1.Controls.Clear();
        splitContent.Panel1.Controls.Add(_centerPanelTabs);
        AddPanelTab(_centerPanelTabs, "Game Stats", grpGameStats);
        AddPanelTab(_centerPanelTabs, "Kill Feed", grpKillFeed);
        AddPanelTab(_centerPanelTabs, "Damage Events", grpCombatEvents);
        AddPanelTab(_centerPanelTabs, "Player List", grpPlayers);

        _playerPanelTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            HotTrack = true,
            Multiline = false,
            Padding = new Point(14, 4)
        };
        _playerSubjectTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            HotTrack = true,
            Multiline = false,
            Padding = new Point(12, 4)
        };
        _playerSubjectTabs.SelectedIndexChanged += (_, _) => HandlePlayerSubjectTabSelectionChanged();
        playerContentLayout.Parent?.Controls.Remove(playerContentLayout);
        playerPanelLayout.Controls.Remove(playerContentLayout);
        playerPanelLayout.RowCount = 3;
        playerPanelLayout.RowStyles.Clear();
        playerPanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        playerPanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        playerPanelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        playerPanelLayout.Controls.Add(_playerSubjectTabs, 0, 1);
        playerPanelLayout.Controls.Add(_playerPanelTabs, 0, 2);
        AddPanelTab(_playerPanelTabs, "Overview", grpPlayerOverview);
        AddPanelTab(_playerPanelTabs, "Kill Log", grpPlayerCombatLog);
        AddPanelTab(_playerPanelTabs, "Eliminated Players", grpPlayerVictims);
        if (_grpPlayerDamageLog is not null)
        {
            AddPanelTab(_playerPanelTabs, "Damage Log", _grpPlayerDamageLog);
        }

        _openedReplayTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            HotTrack = true,
            Multiline = false,
            Padding = new Point(14, 4)
        };
        _openedReplayTabs.SelectedIndexChanged += async (_, _) => await HandleReplayTabSelectionChangedAsync();

        splitMain.Panel2.Padding = new Padding(0, 6, 0, 0);
        splitMain.Panel2.Controls.Clear();
        splitMain.Panel2.Controls.Add(_openedReplayTabs);
        splitMain.SplitterDistance = ExpandedReplayPaneWidth;
        UpdateReplayBrowserHeaderChrome();
        InitializeAdvancedAnalysisUi();
        LayoutContentBelowMenu();
    }

    private void InitializeReplayBrowserContextMenu()
    {
        _replayBrowserContextMenu = new ContextMenuStrip();
        _loadReplayMenuItem = new ToolStripMenuItem("Load", null, (_, _) => QueueSelectedReplayLoad());
        _loadAllReplayMenuItem = new ToolStripMenuItem("Load All", null, (_, _) => QueueAllReplayLoads());
        _unloadReplayMenuItem = new ToolStripMenuItem("Unload", null, (_, _) => UnloadSelectedReplay());
        _stopReplayMenuItem = new ToolStripMenuItem("Stop", null, (_, _) => StopSelectedReplayLoad());

        _replayBrowserContextMenu.Items.AddRange(
        [
            _loadReplayMenuItem,
            _loadAllReplayMenuItem,
            _unloadReplayMenuItem,
            _stopReplayMenuItem
        ]);

        _replayBrowserContextMenu.Opening += (_, e) =>
        {
            var row = GetReplayRowForContextMenu();
            if (row is null)
            {
                e.Cancel = true;
                return;
            }

            _loadReplayMenuItem!.Tag = row;
            _loadAllReplayMenuItem!.Tag = row;
            _unloadReplayMenuItem!.Tag = row;
            _stopReplayMenuItem!.Tag = row;
            var targetRows = GetReplayRowsForContextAction(row);
            _loadReplayMenuItem!.Enabled = targetRows.Any(candidate => !IsReplayLoaded(candidate) && !candidate.IsQueued && !candidate.IsLoading);
            _loadAllReplayMenuItem!.Enabled = _replayRows.Any(candidate => !IsReplayLoaded(candidate) && !candidate.IsQueued && !candidate.IsLoading);
            _unloadReplayMenuItem!.Enabled = targetRows.Any(IsReplayLoaded);
            _stopReplayMenuItem!.Enabled = targetRows.Any(candidate => candidate.IsQueued || candidate.IsLoading);
        };
        dgvReplayBrowser.ContextMenuStrip = _replayBrowserContextMenu;
    }

    private void InitializeMenuStrip()
    {
        _menuStrip = new MenuStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Visible,
            BackColor = Color.FromArgb(244, 247, 252)
        };

        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.Add("Open File", null, async (_, _) => await OpenReplayFilesAsync());
        fileMenu.DropDownItems.Add("Close File", null, (_, _) => CloseCurrentReplayFile());
        fileMenu.DropDownItems.Add("Close All Files", null, (_, _) => CloseAllReplayFiles());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("Exit", null, (_, _) => Close());

        var preferencesMenu = new ToolStripMenuItem("Preferences");
        preferencesMenu.DropDownItems.Add("Settings", null, async (_, _) => await OpenSettingsAsync());

        _menuStrip.Items.Add(fileMenu);
        _menuStrip.Items.Add(preferencesMenu);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;
        Controls.SetChildIndex(_menuStrip, 0);
        _menuStrip.BringToFront();
        mainContentHost.Dock = DockStyle.None;
        mainContentHost.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
    }

    private void LayoutContentBelowMenu()
    {
        if (_menuStrip is null)
        {
            return;
        }

        _menuStrip.Dock = DockStyle.Top;
        var top = _menuStrip.Bottom;
        mainContentHost.Dock = DockStyle.None;
        mainContentHost.Location = new Point(0, top);
        mainContentHost.Size = new Size(ClientSize.Width, Math.Max(0, ClientSize.Height - top));
        _menuStrip.BringToFront();
    }

    private static FlowLayoutPanel CreateFilterFlowPanel()
    {
        return new FlowLayoutPanel
        {
            AutoScroll = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(4, 4, 4, 8),
            WrapContents = true
        };
    }

    private static CheckBox CreateFilterCheckBox(string text, EventHandler onChanged, bool isChecked = false)
    {
        var checkBox = new CheckBox
        {
            AutoSize = true,
            Checked = isChecked,
            Margin = new Padding(0, 2, 14, 0),
            Text = text
        };
        checkBox.CheckedChanged += onChanged;
        return checkBox;
    }
    private void WireEvents()
    {
        Shown += async (_, _) => await RefreshReplayBrowserAsync();

        dgvReplayBrowser.SelectionChanged += async (_, _) => await HandleReplaySelectionChangedAsync();
        dgvReplayBrowser.CellClick += async (_, e) => await HandleReplayBrowserCellClickAsync(e);
        dgvReplayBrowser.ColumnHeaderMouseClick += (_, e) => SortReplayRows(dgvReplayBrowser.Columns[e.ColumnIndex].Name);
        dgvReplayBrowser.CellMouseDown += HandleReplayBrowserCellMouseDown;
        dgvReplayBrowser.CellMouseEnter += HandleReplayBrowserCellMouseEnter;
        dgvReplayBrowser.CellMouseUp += HandleReplayBrowserCellMouseUp;
        dgvReplayBrowser.KeyDown += HandleReplayBrowserKeyDown;


        dgvKillFeed.CellClick += (_, e) => HandleKillFeedLinkClick(e);
        dgvPlayerCombatLog.CellClick += (_, e) => HandleKillFeedLinkClick(dgvPlayerCombatLog, e);
        dgvCombatEvents.CellClick += (_, e) => HandleCombatEventLinkClick(dgvCombatEvents, e);
        dgvPlayers.CellClick += (_, e) => HandlePlayerLinkClick(e);
        dgvPlayerVictims.CellClick += (_, e) => HandlePlayerVictimLinkClick(e);
        dgvPlayers.SelectionChanged += (_, _) => HandlePlayerSelectionChanged();
        dgvPlayers.ColumnHeaderMouseClick += (_, e) => SortPlayerRows(dgvPlayers.Columns[e.ColumnIndex].Name);
    }

    private void ConfigureGrids()
    {
        ConfigureReadOnlyGrid(dgvReplayBrowser, fullRowSelect: true);
        dgvReplayBrowser.MultiSelect = true;
        ConfigureReadOnlyGrid(dgvGameStats, fullRowSelect: false);
        ConfigureReadOnlyGrid(dgvKillFeed, fullRowSelect: true);
        ConfigureReadOnlyGrid(dgvCombatEvents, fullRowSelect: true);
        ConfigureReadOnlyGrid(dgvPlayers, fullRowSelect: true);
        ConfigureReadOnlyGrid(dgvPlayerOverview, fullRowSelect: false);
        ConfigureReadOnlyGrid(dgvPlayerCombatLog, fullRowSelect: true);
        ConfigureReadOnlyGrid(dgvPlayerVictims, fullRowSelect: false);

        dgvGameStats.RowHeadersVisible = false;
        dgvPlayerOverview.RowHeadersVisible = false;
        dgvGameStats.ColumnHeadersVisible = false;
        dgvPlayerOverview.ColumnHeadersVisible = false;

        dgvReplayBrowser.AutoGenerateColumns = false;
        dgvGameStats.AutoGenerateColumns = false;
        dgvKillFeed.AutoGenerateColumns = false;
        dgvCombatEvents.AutoGenerateColumns = false;
        dgvPlayers.AutoGenerateColumns = false;
        dgvPlayerOverview.AutoGenerateColumns = false;
        dgvPlayerCombatLog.AutoGenerateColumns = false;
        dgvPlayerVictims.AutoGenerateColumns = false;

        BuildReplayBrowserColumns();
        BuildGameStatsColumns();
        BuildKillFeedColumns(dgvKillFeed);
        BuildCombatEventColumns(dgvCombatEvents);
        BuildPlayerColumns();
        BuildGameStatsColumns(dgvPlayerOverview);
        BuildKillFeedColumns(dgvPlayerCombatLog);
        BuildPlayerVictimColumns();
        EnsureIconTextRendering(dgvKillFeed);
        EnsureIconTextRendering(dgvPlayerCombatLog);
        EnsureIconTextRendering(dgvCombatEvents);
        EnsureIconTextRendering(dgvPlayers);
        EnsureIconTextRendering(dgvPlayerVictims);
    }

    private static void ConfigureReadOnlyGrid(DataGridView grid, bool fullRowSelect)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.BackgroundColor = Color.FromArgb(247, 249, 252);
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.RowTemplate.Height = 28;
        grid.SelectionMode = fullRowSelect ? DataGridViewSelectionMode.FullRowSelect : DataGridViewSelectionMode.CellSelect;
        grid.Dock = DockStyle.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 236, 245);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(33, 42, 54);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(32, 37, 43);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(25, 118, 210);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(244, 248, 252);
        grid.GridColor = Color.FromArgb(214, 223, 235);
    }

    private void BuildReplayBrowserColumns()
    {
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(ReplayBrowserRow.FileName), HeaderText = "Replay", DataPropertyName = nameof(ReplayBrowserRow.FileName), Width = 172, MinimumWidth = 140 });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(ReplayBrowserRow.RecordedAtText), HeaderText = "Played", DataPropertyName = nameof(ReplayBrowserRow.RecordedAtText), Width = 132, MinimumWidth = 120 });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(ReplayBrowserRow.DurationText), HeaderText = "Length", DataPropertyName = nameof(ReplayBrowserRow.DurationText), Width = 68, MinimumWidth = 60 });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(ReplayBrowserRow.PlacementText), HeaderText = "Place", DataPropertyName = nameof(ReplayBrowserRow.PlacementText), Width = 58, MinimumWidth = 52 });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(ReplayBrowserRow.KillsText), HeaderText = "Kills", DataPropertyName = nameof(ReplayBrowserRow.KillsText), Width = 58, MinimumWidth = 52 });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(ReplayBrowserRow.PlayerCountText), HeaderText = "Players", DataPropertyName = nameof(ReplayBrowserRow.PlayerCountText), Width = 66, MinimumWidth = 58 });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(ReplayBrowserRow.Status), HeaderText = "Status", DataPropertyName = nameof(ReplayBrowserRow.Status), Width = 120, MinimumWidth = 90 });
        dgvReplayBrowser.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
    }

    private void BuildGameStatsColumns() => BuildGameStatsColumns(dgvGameStats);

    private static void BuildGameStatsColumns(DataGridView grid)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(DetailRow.Label), DataPropertyName = nameof(DetailRow.Label), FillWeight = 40 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(DetailRow.Value), DataPropertyName = nameof(DetailRow.Value), FillWeight = 60 });
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private static void BuildKillFeedColumns(DataGridView grid)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(KillFeedRow.TimeText), HeaderText = "Time", DataPropertyName = nameof(KillFeedRow.TimeText), FillWeight = 65 });
        grid.Columns.Add(CreateIconNameColumn(nameof(KillFeedRow.ActorName), "Actor", nameof(KillFeedRow.ActorName), 150));
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(KillFeedRow.EventText), HeaderText = "Event", DataPropertyName = nameof(KillFeedRow.EventText), FillWeight = 85 });
        grid.Columns.Add(CreateIconNameColumn(nameof(KillFeedRow.TargetName), "Target", nameof(KillFeedRow.TargetName), 150));
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(KillFeedRow.ReasonText), HeaderText = "Reason", DataPropertyName = nameof(KillFeedRow.ReasonText), FillWeight = 95 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(KillFeedRow.DistanceText), HeaderText = "Distance", DataPropertyName = nameof(KillFeedRow.DistanceText), FillWeight = 70 });
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private static void BuildCombatEventColumns(DataGridView grid)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.TimeText), HeaderText = "Time", DataPropertyName = nameof(CombatEventRow.TimeText), FillWeight = 62 });
        grid.Columns.Add(CreateIconNameColumn(nameof(CombatEventRow.AttackerName), "Attacker", nameof(CombatEventRow.AttackerName), 150));
        grid.Columns.Add(CreateIconNameColumn(nameof(CombatEventRow.TargetName), "Target", nameof(CombatEventRow.TargetName), 150));
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.DamageText), HeaderText = "Damage", DataPropertyName = nameof(CombatEventRow.DamageText), FillWeight = 72 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.WeaponTypeText), HeaderText = "Weapon", DataPropertyName = nameof(CombatEventRow.WeaponTypeText), FillWeight = 88 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.ShieldText), HeaderText = "Shield", DataPropertyName = nameof(CombatEventRow.ShieldText), FillWeight = 62 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.FatalText), HeaderText = "Fatal", DataPropertyName = nameof(CombatEventRow.FatalText), FillWeight = 58 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.CriticalText), HeaderText = "Crit", DataPropertyName = nameof(CombatEventRow.CriticalText), FillWeight = 54 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.LocationText), HeaderText = "Location", DataPropertyName = nameof(CombatEventRow.LocationText), FillWeight = 150 });
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private void BuildPlayerColumns()
    {
        dgvPlayers.Columns.Add(CreateIconNameColumn(nameof(PlayerSummaryRow.DisplayName), "Player", nameof(PlayerSummaryRow.DisplayName), 170));
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerSummaryRow.TeamText), HeaderText = "Team", DataPropertyName = nameof(PlayerSummaryRow.TeamText), FillWeight = 55 });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerSummaryRow.PlacementText), HeaderText = "Place", DataPropertyName = nameof(PlayerSummaryRow.PlacementText), FillWeight = 55 });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerSummaryRow.KillsText), HeaderText = "Kills", DataPropertyName = nameof(PlayerSummaryRow.KillsText), FillWeight = 55 });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerSummaryRow.Platform), HeaderText = "Platform", DataPropertyName = nameof(PlayerSummaryRow.Platform), FillWeight = 75 });
        dgvPlayers.Columns.Add(new DataGridViewCheckBoxColumn { Name = nameof(PlayerSummaryRow.IsBot), HeaderText = "Bot", DataPropertyName = nameof(PlayerSummaryRow.IsBot), FillWeight = 45 });
        dgvPlayers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }
    private void BuildPlayerVictimColumns()
    {
        dgvPlayerVictims.Columns.Add(CreateIconNameColumn(nameof(PlayerVictimRow.PlayerName), "Player", nameof(PlayerVictimRow.PlayerName), 150));
        dgvPlayerVictims.Columns.Add(new DataGridViewCheckBoxColumn { Name = nameof(PlayerVictimRow.IsBot), HeaderText = "Bot", DataPropertyName = nameof(PlayerVictimRow.IsBot), FillWeight = 45 });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerVictimRow.EventText), HeaderText = "Event", DataPropertyName = nameof(PlayerVictimRow.EventText), FillWeight = 80 });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerVictimRow.ReasonText), HeaderText = "Reason", DataPropertyName = nameof(PlayerVictimRow.ReasonText), FillWeight = 95 });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerVictimRow.TimeText), HeaderText = "Time", DataPropertyName = nameof(PlayerVictimRow.TimeText), FillWeight = 70 });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerVictimRow.DistanceText), HeaderText = "Distance", DataPropertyName = nameof(PlayerVictimRow.DistanceText), FillWeight = 80 });
        dgvPlayerVictims.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private Task RefreshReplayBrowserAsync()
    {
        btnRefreshReplays.Enabled = false;
        lblReplayStatus.Text = Directory.Exists(ReplayFolder)
            ? $"Scanning {ReplayFolder}"
            : $"Replay folder not found: {ReplayFolder}";

        ClearReplayDetails();
        _replayRows.Clear();
        BindReplayRows();

        var replayFiles = GetReplayFilePaths().ToArray();
        _replayRows.AddRange(replayFiles.Select(ReplayBrowserRow.CreateFromFile));
        BindReplayRows();

        if (_replayRows.Count == 0)
        {
            lblReplayStatus.Text = Directory.Exists(ReplayFolder)
                ? "No replay files found."
                : $"Replay folder not found: {ReplayFolder}";
            btnRefreshReplays.Enabled = true;
            return Task.CompletedTask;
        }

        lblReplayStatus.Text = $"Found {_replayRows.Count} replay file(s). Click a replay to load it.";
        btnRefreshReplays.Enabled = true;
        _suppressReplaySelectionChanged = true;
        dgvReplayBrowser.ClearSelection();
        dgvReplayBrowser.CurrentCell = null;
        _selectedReplayRow = null;
        _suppressReplaySelectionChanged = false;
        return Task.CompletedTask;
    }

    private async Task HandleReplaySelectionChangedAsync()
    {
        if (_ignoreReplaySelectionChanged)
        {
            return;
        }

        if (_suppressReplaySelectionChanged || dgvReplayBrowser.CurrentRow?.DataBoundItem is not ReplayBrowserRow row)
        {
            return;
        }

        _selectedReplayRow = row;

        var requiredMode = ParseMode.Full;
        if (row.IsLoading || row.IsQueued)
        {
            DisplayReplay(row);
            return;
        }

        if (row.Replay is null || row.LoadedParseMode < requiredMode)
        {
            await EnsureReplayLoadedAsync(row, requiredMode, updateSelectionView: true);
            return;
        }

        DisplayReplay(row);
    }

    private async Task HandleReplayBrowserCellClickAsync(DataGridViewCellEventArgs e)
    {
        if (_ignoreReplayBrowserCellClick)
        {
            _ignoreReplayBrowserCellClick = false;
            return;
        }

        if ((ModifierKeys & Keys.Shift) == Keys.Shift || _replayDragSelectionChanged)
        {
            return;
        }

        if (e.RowIndex < 0 || e.RowIndex >= dgvReplayBrowser.Rows.Count)
        {
            return;
        }

        if (dgvReplayBrowser.Rows[e.RowIndex].DataBoundItem is not ReplayBrowserRow row)
        {
            return;
        }

        if (!ReferenceEquals(row, _selectedReplayRow) || row.IsLoading || row.IsQueued || IsReplayLoaded(row))
        {
            return;
        }

        await EnsureReplayLoadedAsync(row, ParseMode.Full, updateSelectionView: true);
    }

    private async Task EnsureReplayLoadedAsync(ReplayBrowserRow row, ParseMode parseMode, bool updateSelectionView)
    {
        CancelQueuedReplayLoad(row, preserveStatus: false);

        while (row.IsLoading)
        {
            await Task.Delay(50);
        }

        if (row.Replay is not null && row.LoadedParseMode >= parseMode)
        {
            if (updateSelectionView && row == _selectedReplayRow)
            {
                DisplayReplay(row);
            }

            return;
        }

        await LoadReplayRowAsync(row, parseMode, updateSelectionView);
    }

    private async Task LoadReplayRowAsync(ReplayBrowserRow row, ParseMode parseMode, bool updateSelectionView)
    {
        if (row.IsLoading)
        {
            return;
        }

        row.StopRequested = false;
        row.IsQueued = false;
        _pendingReplayLoadPaths.Remove(row.FilePath);
        row.IsLoading = true;
        row.Status = parseMode == ParseMode.Full ? "Loading full replay..." : "Loading summary...";
        RequestReplayRowsRefresh();
        DebugOutputWriter.LogInfo($"Parsing replay '{row.FileName}' with mode {parseMode}.");

        try
        {
            var loadResult = await Task.Run(() => ParseReplaySafely(row.FilePath, parseMode));
            if (loadResult.Exception is InvalidReplayException invalidReplayException)
            {
                ApplyReplayFailure(row, "Skipped", invalidReplayException, parseMode, updateSelectionView);
                return;
            }

            if (loadResult.Exception is Exception ex)
            {
                ApplyReplayFailure(row, "Error", ex, parseMode, updateSelectionView);
                return;
            }

            var replay = loadResult.Replay!;
            if (row.StopRequested)
            {
                MarkReplayAsStopped(row, updateSelectionView);
                return;
            }

            if (parseMode == ParseMode.Minimal && !HasReplaySummary(replay))
            {
                loadResult = await Task.Run(() => ParseReplaySafely(row.FilePath, ParseMode.Normal));
                if (loadResult.Exception is InvalidReplayException fallbackInvalidReplayException)
                {
                    ApplyReplayFailure(row, "Skipped", fallbackInvalidReplayException, ParseMode.Normal, updateSelectionView);
                    return;
                }

                if (loadResult.Exception is Exception fallbackException)
                {
                    ApplyReplayFailure(row, "Error", fallbackException, ParseMode.Normal, updateSelectionView);
                    return;
                }

                replay = loadResult.Replay!;
                parseMode = ParseMode.Normal;
                if (row.StopRequested)
                {
                    MarkReplayAsStopped(row, updateSelectionView);
                    return;
                }
            }

            var weaponStatsSnapshots = await Task.Run(() => BuildWeaponStatsSnapshotsForReplay(replay, row.FilePath));
            if (row.StopRequested)
            {
                MarkReplayAsStopped(row, updateSelectionView: false);
                return;
            }

            ApplyReplaySummary(row, replay);
            row.Replay = replay;
            row.WeaponStatsSnapshots = weaponStatsSnapshots;
            row.Status = parseMode == ParseMode.Full ? $"Ready ({replay.DamageEvents.Count} hits)" : "Ready";
            row.SummaryLoaded = true;
            row.LoadedParseMode = parseMode;

            DebugOutputWriter.LogInfo($"Parsed replay '{row.FileName}' successfully in mode {parseMode}. KillFeed={replay.KillFeed.Count}, DamageEvents={replay.DamageEvents.Count}.");
            DebugOutputWriter.WriteReplaySnapshot(row.FilePath, new
            {
                FileName = row.FileName,
                FilePath = row.FilePath,
                ParseMode = parseMode.ToString(),
                Status = row.Status,
                ParsedAt = DateTime.Now,
                Replay = replay
            });
        }
        finally
        {
            row.IsLoading = false;
            RequestReplayRowsRefresh();

            if (updateSelectionView && row.Replay is not null && row == _selectedReplayRow)
            {
                DisplayReplay(row);
            }
        }
    }

    private static ReplayLoadResult ParseReplaySafely(string filePath, ParseMode parseMode)
    {
        var reader = new ReplayReader(parseMode: parseMode);
        return reader.TryReadReplay(filePath, out var replay, out var exception)
            ? new ReplayLoadResult(replay, null)
            : new ReplayLoadResult(null, exception);
    }
    private void ApplyReplayFailure(ReplayBrowserRow row, string status, Exception ex, ParseMode parseMode, bool updateSelectionView)
    {
        row.Replay = null;
        row.WeaponStatsSnapshots = [];
        row.IsQueued = false;
        row.StopRequested = false;
        row.Status = status;
        row.SummaryLoaded = true;
        row.Duration = TimeSpan.Zero;
        row.DurationText = "-";
        row.Placement = null;
        row.PlacementText = "-";
        row.Kills = null;
        row.KillsText = "-";
        row.PlayerCount = 0;
        row.PlayerCountText = "-";

        DebugOutputWriter.LogError($"Failed to parse replay '{row.FileName}' in mode {parseMode}. Marked as {status}.", ex);
        DebugOutputWriter.WriteReplaySnapshot(row.FilePath, new
        {
            FileName = row.FileName,
            FilePath = row.FilePath,
            ParseMode = parseMode.ToString(),
            Status = status,
            FailedAt = DateTime.Now,
            Error = ex.ToString()
        });

        if (updateSelectionView)
        {
            lblReplayStatus.Text = $"{status} {row.FileName}: {ex.Message}";
            ClearReplayDetails(keepReplaySelection: true);
        }
    }

    private static void ApplyReplaySummary(ReplayBrowserRow row, FortniteReplay replay)
    {
        var replayOwner = GetReplayOwner(replay);
        var timestamp = GetReplayTimestamp(replay.Info.Timestamp, row.FilePath, row.FileName);

        row.RecordedAt = timestamp;
        row.RecordedAtText = timestamp.ToString("g", CultureInfo.CurrentCulture);
        row.Duration = TimeSpan.FromMilliseconds(replay.Info.LengthInMs);
        row.DurationText = FormatDuration(row.Duration);
        row.Placement = replayOwner?.Placement ?? (int?)replay.TeamStats?.Position;
        row.PlacementText = FormatNullable(row.Placement);
        row.Kills = replayOwner?.Kills ?? replay.Stats?.Eliminations;
        row.KillsText = FormatNullable(row.Kills);
        row.PlayerCount = replay.PlayerData?.Count() ?? 0;
        row.PlayerCountText = row.PlayerCount.ToString(CultureInfo.CurrentCulture);
    }

    private void DisplayReplay(ReplayBrowserRow row)
    {
        if (row.Replay is null)
        {
            lblReplayStatus.Text = row.Status == "Error" ? $"Unable to parse {row.FileName}." : $"Loading {row.FileName}...";
            ClearReplayDetails(keepReplaySelection: true);
            return;
        }

        EnsureReplayTab(row);
        EnsureReplayViewCache(row);
        lblReplayStatus.Text = $"{row.FileName} loaded";
        lblPlayerPanelTitle.Text = "Player Stats";

        dgvGameStats.DataSource = row.ViewCache?.ReplayDetails ?? BuildReplayDetails(row).ToList();
        BuildKillFeed(row);
        BuildCombatEvents(row);
        BuildPlayerList(row);
        PopulatePlayerSubjectTabs(row);

        var defaultPlayer = ResolveDefaultPlayer(row.Replay);
        ShowPlayerDetails(defaultPlayer);
        UpdateDamageTimelineChart();
    }

    private void EnsureReplayViewCache(ReplayBrowserRow row)
    {
        if (row.Replay is null || row.ViewCache is not null)
        {
            return;
        }

        row.ViewCache = BuildReplayViewCache(row, row.Replay);
    }

    private void RequestReplayRowsRefresh()
    {
        if (_replayRowsRefreshPending)
        {
            return;
        }

        _replayRowsRefreshPending = true;
        if (!IsHandleCreated)
        {
            _replayRowsRefreshPending = false;
            BindReplayRows();
            return;
        }

        BeginInvoke((Action)(() =>
        {
            _replayRowsRefreshPending = false;
            if (!IsDisposed)
            {
                BindReplayRows();
            }
        }));
    }

    private ReplayLookupCache GetReplayLookupCache(FortniteReplay replay)
    {
        return _replayLookupCaches.GetValue(replay, static replayValue => ReplayLookupCache.Create(replayValue));
    }

    private List<KillFeedWeaponCue> GetKillFeedWeaponCues(FortniteReplay replay)
    {
        var lookupCache = GetReplayLookupCache(replay);
        if (lookupCache.KillFeedWeaponCues.Count > 0)
        {
            return lookupCache.KillFeedWeaponCues;
        }

        foreach (var entry in replay.KillFeed)
        {
            if (entry.IsRevived)
            {
                continue;
            }

            var reason = NormalizeKillReasonToWeaponLabel(FormatKillFeedReason(replay, entry));
            if (string.IsNullOrWhiteSpace(reason))
            {
                continue;
            }

            var actor = ResolveKillFeedActorReference(replay, entry);
            var target = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
            lookupCache.KillFeedWeaponCues.Add(new KillFeedWeaponCue
            {
                ActorId = actor.NumericId,
                ActorLookupKey = actor.Player?.PlayerId ?? actor.LookupKey,
                TargetId = target?.Id ?? entry.PlayerId,
                TargetLookupKey = target?.PlayerId ?? entry.PlayerName,
            TimeValue = GetKillFeedTime(replay, entry),
                WeaponLabel = reason
            });
        }

        return lookupCache.KillFeedWeaponCues;
    }

    private static PlayerData? ResolveDefaultPlayer(FortniteReplay replay)
    {
        return GetReplayOwner(replay)
            ?? replay.PlayerData?.OrderBy(p => p.Placement ?? int.MaxValue).ThenBy(p => p.Id ?? int.MaxValue).FirstOrDefault();
    }

    private ReplayViewCache BuildReplayViewCache(ReplayBrowserRow row, FortniteReplay replay)
    {
        var replayDetails = BuildReplayDetails(row).ToList();
        var killFeedRows = replay.KillFeed
            .Select(entry => CreateKillFeedRow(replay, entry))
            .OrderBy(entry => entry.TimeValue)
            .ToList();
        var combatRows = replay.DamageEvents
            .Select(evt => CreateCombatEventRow(replay, evt))
            .OrderBy(evt => evt.TimeValue)
            .ToList();
        var playerRows = replay.PlayerData.Select(player => new PlayerSummaryRow
        {
            Player = player,
            ProfileIcon = GetPlayerSkinIcon(player),
            DisplayName = ResolvePlayerName(player, player.Id, player.PlayerId),
            Team = player.TeamIndex,
            TeamText = FormatNullable(player.TeamIndex),
            Placement = player.Placement,
            PlacementText = FormatNullable(player.Placement),
            Kills = player.Kills,
            KillsText = FormatNullable(player.Kills),
            Platform = string.IsNullOrWhiteSpace(player.Platform) ? "-" : player.Platform,
            IsBot = player.IsBot
        }).ToList();

        var playerCaches = replay.PlayerData
            .ToDictionary(
                GetPlayerCacheKey,
                player => BuildPlayerViewCache(replay, player),
                StringComparer.OrdinalIgnoreCase);

        return new ReplayViewCache
        {
            ReplayDetails = replayDetails,
            KillFeedRows = killFeedRows,
            CombatEventRows = combatRows,
            PlayerRows = playerRows,
            PlayerCaches = playerCaches
        };
    }

    private PlayerViewCache BuildPlayerViewCache(FortniteReplay replay, PlayerData player)
    {
        var profileIcon = GetPlayerSkinIcon(player);
        var displayName = ResolvePlayerName(player, player.Id, player.PlayerId);
        var overviewRows = BuildPlayerOverview(replay, player).ToList();
        var killLogRows = replay.KillFeed
            .Where(entry => MatchesPlayer(player, entry.PlayerId, entry.PlayerName) || MatchesResolvedKillFeedActor(replay, player, entry))
            .Select(entry => CreateKillFeedRow(replay, entry))
            .OrderBy(entry => entry.TimeValue)
            .ToList();
        var victimRows = replay.KillFeed
            .Where(entry => MatchesResolvedKillFeedActor(replay, player, entry))
            .OrderBy(GetKillFeedTime)
            .Select(entry =>
            {
                var victim = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
                return new PlayerVictimRow
                {
                    PlayerIcon = GetPlayerSkinIcon(victim),
                    PlayerName = ResolvePlayerName(victim, entry.PlayerId, entry.PlayerName),
                    PlayerId = entry.PlayerId,
                    PlayerLookupKey = victim?.PlayerId ?? entry.PlayerName,
                    IsBot = victim?.IsBot ?? false,
                    EventText = GetKillFeedEventText(entry),
                    ReasonText = FormatKillFeedReason(replay, entry),
                    TimeText = FormatMatchClock(GetKillFeedTime(replay, entry)),
                    DistanceText = FormatDistance(entry.Distance)
                };
            })
            .ToList();
        var damageRows = replay.DamageEvents
            .Where(evt => MatchesPlayer(player, evt.InstigatorId, evt.InstigatorName) || MatchesPlayer(player, evt.TargetId, evt.TargetName))
            .Select(evt => CreateCombatEventRow(replay, evt))
            .OrderBy(evt => evt.TimeValue)
            .ToList();

        return new PlayerViewCache
        {
            Player = player,
            CacheKey = GetPlayerCacheKey(player),
            ProfileIcon = profileIcon,
            DisplayName = displayName,
            OverviewRows = overviewRows,
            KillLogRows = killLogRows,
            VictimRows = victimRows,
            DamageRows = damageRows
        };
    }

    private static string GetPlayerCacheKey(PlayerData player)
    {
        if (!string.IsNullOrWhiteSpace(player.PlayerId))
        {
            return player.PlayerId;
        }

        return player.Id?.ToString(CultureInfo.InvariantCulture)
            ?? player.PlayerName
            ?? string.Empty;
    }

    private IEnumerable<DetailRow> BuildReplayDetails(ReplayBrowserRow row)
    {
        var replay = row.Replay!;
        var replayOwner = GetReplayOwner(replay);
        var ownerSummary = replayOwner is null ? null : BuildReplayOwnerSummary(replay, replayOwner);
        var ownerDamageTaken = ownerSummary is null ? 0F : ownerSummary.DamageTakenFromPlayers + ownerSummary.DamageTakenFromBots;

        yield return new DetailRow("Replay", row.FileName);
        yield return new DetailRow("Folder", Path.GetDirectoryName(row.FilePath) ?? "-");
        yield return new DetailRow("Played", row.RecordedAtText);
        yield return new DetailRow("Duration", row.DurationText);
        yield return new DetailRow("Playlist", replay.GameData.CurrentPlaylist ?? "-");
        yield return new DetailRow("Map", replay.GameData.MapInfo ?? "-");
        yield return new DetailRow("Placement", FormatNullable(replayOwner?.Placement ?? (int?)replay.TeamStats?.Position));
        yield return new DetailRow("Kills", FormatNullable(replayOwner?.Kills ?? replay.Stats?.Eliminations));
        yield return new DetailRow("Players", FormatNullable(replay.PlayerData?.Count()));
        yield return new DetailRow("Bots", FormatNullable(replay.GameData.TotalBots));
        yield return new DetailRow("Teams", FormatNullable(replay.GameData.TotalTeams));
        yield return new DetailRow("Team Size", FormatNullable(replay.GameData.TeamSize));
        yield return new DetailRow("Accuracy", replay.Stats is null ? "-" : $"{replay.Stats.Accuracy:0.#}%");
        yield return new DetailRow("Damage To Players", ownerSummary is null ? "-" : FormatDamageTotal(ownerSummary.DamageToPlayers));
        yield return new DetailRow("Damage To Bots", ownerSummary is null ? "-" : FormatDamageTotal(ownerSummary.DamageToBots));
        yield return new DetailRow("Damage Taken", ownerSummary is null ? "-" : FormatDamageTotal(ownerDamageTaken));
        yield return new DetailRow("Recorded Hits", ownerSummary is null ? "-" : ownerSummary.HitsGiven.ToString(CultureInfo.CurrentCulture));
    }

    private void BuildKillFeed(ReplayBrowserRow row)
    {
        var rows = row.ViewCache?.KillFeedRows ?? [];
        dgvKillFeed.DataSource = rows
            .Where(ShouldIncludeKillFeedRow)
            .ToList();
    }

    private void BuildCombatEvents(ReplayBrowserRow row)
    {
        var rows = (row.ViewCache?.CombatEventRows ?? [])
            .Where(ShouldIncludeReplayDamageEventRow)
            .ToList();

        dgvCombatEvents.DataSource = rows;
    }

    private KillFeedRow CreateKillFeedRow(FortniteReplay replay, KillFeedEntry entry)
    {
        var actorReference = ResolveKillFeedActorReference(replay, entry);
        var targetPlayer = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
        var timeValue = GetKillFeedTime(replay, entry);
        var ownerTeamIndex = GetReplayOwner(replay)?.TeamIndex;
        var actorIsTeammate = ownerTeamIndex.HasValue && actorReference.Player?.TeamIndex == ownerTeamIndex;
        var targetIsTeammate = ownerTeamIndex.HasValue && targetPlayer?.TeamIndex == ownerTeamIndex;
        var actorIsBot = actorReference.Player?.IsBot ?? false;
        var targetIsBot = targetPlayer?.IsBot ?? entry.PlayerIsBot;

        return new KillFeedRow
        {
            Entry = entry,
            ActorIcon = GetPlayerSkinIcon(actorReference.Player),
            TimeValue = timeValue,
            TimeText = FormatMatchClock(timeValue),
            ActorName = ResolvePlayerName(actorReference.Player, actorReference.NumericId, actorReference.LookupKey),
            ActorId = actorReference.NumericId,
            ActorLookupKey = actorReference.LookupKey,
            TargetIcon = GetPlayerSkinIcon(targetPlayer),
            TargetName = ResolvePlayerName(targetPlayer, entry.PlayerId, entry.PlayerName),
            TargetId = targetPlayer?.Id ?? entry.PlayerId,
            TargetLookupKey = targetPlayer?.PlayerId ?? entry.PlayerName,
            ActorIsBot = actorIsBot,
            TargetIsBot = targetIsBot,
            ActorIsTeammate = actorIsTeammate,
            TargetIsTeammate = targetIsTeammate,
            InvolvesOwnerTeam = actorIsTeammate || targetIsTeammate,
            EventText = GetKillFeedEventText(entry),
            ReasonText = FormatKillFeedReason(replay, entry),
            DistanceText = FormatDistance(entry.Distance)
        };
    }

    private CombatEventRow CreateCombatEventRow(FortniteReplay replay, DamageEvent evt)
    {
        var attacker = FindPlayer(replay, evt.InstigatorId, evt.InstigatorName);
        var target = FindPlayer(replay, evt.TargetId, evt.TargetName);
        var timeValue = GetDamageTime(replay, evt);
        var attackerCategory = GetDamageEventInstigatorCategory(replay, evt);
        var targetCategory = GetDamageEventTargetCategory(replay, evt);
        var ownerTeamIndex = GetReplayOwner(replay)?.TeamIndex;
        var attackerIsTeammate = ownerTeamIndex.HasValue && attacker?.TeamIndex == ownerTeamIndex;
        var targetIsTeammate = ownerTeamIndex.HasValue && target?.TeamIndex == ownerTeamIndex;

        return new CombatEventRow
        {
            TimeValue = timeValue,
            TimeText = FormatMatchClock(timeValue),
            AttackerIcon = GetPlayerSkinIcon(attacker),
            AttackerName = ResolveCombatantName(attacker, evt.InstigatorId, evt.InstigatorName, evt.InstigatorIsBot),
            AttackerId = attacker?.Id ?? evt.InstigatorId,
            AttackerLookupKey = attacker?.PlayerId ?? evt.InstigatorName,
            TargetIcon = targetCategory == DamageParticipantCategory.Structure ? null : GetPlayerSkinIcon(target),
            TargetName = targetCategory == DamageParticipantCategory.Structure
                ? ResolveCombatantName(null, evt.TargetId, null, false)
                : ResolveCombatantName(target, evt.TargetId, evt.TargetName, evt.TargetIsBot),
            TargetId = target?.Id ?? evt.TargetId,
            TargetLookupKey = target?.PlayerId ?? evt.TargetName,
            AttackerCategory = attackerCategory,
            TargetCategory = targetCategory,
            InvolvesOwnerTeam = attackerIsTeammate || targetIsTeammate,
            EventText = evt.EventTag ?? evt.EventSource ?? "-",
            DamageText = evt.Magnitude.HasValue ? evt.Magnitude.Value.ToString("0.#", CultureInfo.CurrentCulture) : "-",
            WeaponTypeText = FormatCombatEventWeaponType(replay, evt),
            ShieldText = evt.IsShield switch { true => "Yes", false => "No", _ => "-" },
            FatalText = FormatBool(evt.IsFatal),
            CriticalText = FormatBool(evt.IsCritical),
            LocationText = FormatVector(evt.Location)
        };
    }

    private Image? GetPlayerSkinIcon(PlayerData? player)
    {
        if (player is null)
        {
            return null;
        }

        var cosmeticId = player?.Cosmetics?.Character;
        if (string.IsNullOrWhiteSpace(cosmeticId))
        {
            return CosmeticIconCache.GetPlaceholderImage();
        }

        var cached = CosmeticIconCache.LoadCachedImage(cosmeticId);
        if (cached is not null)
        {
            return cached;
        }

        CosmeticIconCache.QueueBackgroundDownload(cosmeticId);
        return CosmeticIconCache.GetPlaceholderImage();
    }
    private void BuildPlayerList(ReplayBrowserRow row)
    {
        _playerRows.Clear();
        _playerRows.AddRange(row.ViewCache?.PlayerRows ?? []);

        SortPlayerRows(_playerSortColumn, toggle: false);
    }

    private void HandlePlayerSelectionChanged()
    {
        if (dgvPlayers.CurrentRow?.DataBoundItem is PlayerSummaryRow row)
        {
            ShowPlayerDetails(row.Player);
        }
    }

    private void HandlePlayerLinkClick(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || dgvPlayers.Rows[e.RowIndex].DataBoundItem is not PlayerSummaryRow row)
        {
            return;
        }

        ShowPlayerDetails(row.Player);
    }

    private void HandleKillFeedLinkClick(DataGridViewCellEventArgs e)
    {
        HandleKillFeedLinkClick(dgvKillFeed, e);
    }

    private void HandleKillFeedLinkClick(DataGridView grid, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _selectedReplayRow?.Replay is null || grid.Rows[e.RowIndex].DataBoundItem is not KillFeedRow row)
        {
            return;
        }

        if (grid.Columns[e.ColumnIndex].Name == nameof(KillFeedRow.ActorName))
        {
            ShowPlayerDetails(FindPlayer(_selectedReplayRow.Replay, row.ActorId, row.ActorLookupKey));
        }

        if (grid.Columns[e.ColumnIndex].Name == nameof(KillFeedRow.TargetName))
        {
            ShowPlayerDetails(FindPlayer(_selectedReplayRow.Replay, row.TargetId, row.TargetLookupKey));
        }
    }

    private void HandlePlayerVictimLinkClick(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _selectedReplayRow?.Replay is null || dgvPlayerVictims.Rows[e.RowIndex].DataBoundItem is not PlayerVictimRow row)
        {
            return;
        }

        if (dgvPlayerVictims.Columns[e.ColumnIndex].Name == nameof(PlayerVictimRow.PlayerName))
        {
            ShowPlayerDetails(FindPlayer(_selectedReplayRow.Replay, row.PlayerId, row.PlayerLookupKey));
        }
    }

    private void HandleCombatEventLinkClick(DataGridView grid, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _selectedReplayRow?.Replay is null || grid.Rows[e.RowIndex].DataBoundItem is not CombatEventRow row)
        {
            return;
        }

        if (grid.Columns[e.ColumnIndex].Name == nameof(CombatEventRow.AttackerName))
        {
            ShowPlayerDetails(FindPlayer(_selectedReplayRow.Replay, row.AttackerId, row.AttackerLookupKey));
        }

        if (grid.Columns[e.ColumnIndex].Name == nameof(CombatEventRow.TargetName))
        {
            ShowPlayerDetails(FindPlayer(_selectedReplayRow.Replay, row.TargetId, row.TargetLookupKey));
        }
    }

    private void ShowPlayerDetails(PlayerData? player)
    {
        ShowPlayerDetails(player, true);
    }

    private void ShowPlayerDetails(PlayerData? player, bool syncPlayerTabs)
    {
        _selectedPlayer = player;

        if (_selectedReplayRow?.Replay is null || _selectedReplayRow.ViewCache is null || player is null)
        {
            lblSelectedPlayer.Text = "Select a player to inspect their stats.";
            dgvPlayerOverview.DataSource = new List<DetailRow>();
            dgvPlayerCombatLog.DataSource = new List<KillFeedRow>();
            dgvPlayerVictims.DataSource = new List<PlayerVictimRow>();
            if (_picSelectedPlayer is not null) _picSelectedPlayer.Image = CosmeticIconCache.GetPlaceholderImage();
            if (_dgvPlayerDamageLog is not null)
            {
                _dgvPlayerDamageLog.DataSource = new List<CombatEventRow>();
            }
            return;
        }

        if (syncPlayerTabs)
        {
            EnsurePlayerSubjectTab(player, true);
        }

        var playerCache = GetPlayerViewCache(_selectedReplayRow.ViewCache, player);
        var playerDisplayName = playerCache?.DisplayName ?? ResolvePlayerName(player, player.Id, player.PlayerId);

        lblSelectedPlayer.Text = playerDisplayName;
        lblPlayerPanelTitle.Text = $"Player Stats - {playerDisplayName}";
        if (_picSelectedPlayer is not null)
        {
            _picSelectedPlayer.Image = playerCache?.ProfileIcon ?? GetPlayerSkinIcon(player) ?? CosmeticIconCache.GetPlaceholderImage();
        }
        dgvPlayerOverview.DataSource = playerCache?.OverviewRows ?? BuildPlayerOverview(_selectedReplayRow.Replay, player).ToList();
        RefreshPlayerKillLog();
        dgvPlayerVictims.DataSource = playerCache?.VictimRows ?? BuildPlayerVictimRows(_selectedReplayRow.Replay, player).ToList();
        if (_dgvPlayerDamageLog is not null)
        {
            _dgvPlayerDamageLog.DataSource = playerCache is null
                ? BuildPlayerDamageRows(_selectedReplayRow.Replay, player).ToList()
                : playerCache.DamageRows.Where(ShouldIncludePlayerDamageEventRow).ToList();
        }
    }

    private IEnumerable<DetailRow> BuildPlayerOverview(FortniteReplay replay, PlayerData player)
    {
        var deathEvent = replay.KillFeed
            .Where(entry => MatchesPlayer(player, entry.PlayerId, entry.PlayerName) && !entry.IsRevived)
            .OrderByDescending(GetKillFeedTime)
            .FirstOrDefault();

        (PlayerData? Player, int? NumericId, string? LookupKey)? eliminatedBy = deathEvent is null ? null : ResolveKillFeedActorReference(replay, deathEvent);
        var hitsGiven = replay.DamageEvents.Count(evt => MatchesPlayer(player, evt.InstigatorId, evt.InstigatorName));
        var hitsTaken = replay.DamageEvents.Count(evt => MatchesPlayer(player, evt.TargetId, evt.TargetName));
        var damageDealt = SummarizeDamage(replay, replay.DamageEvents.Where(evt => MatchesPlayer(player, evt.InstigatorId, evt.InstigatorName)), targetPerspective: true);
        var damageTaken = SummarizeDamage(replay, replay.DamageEvents.Where(evt => MatchesPlayer(player, evt.TargetId, evt.TargetName)), targetPerspective: false);

        yield return new DetailRow("Name", ResolvePlayerName(player, player.Id, player.PlayerId));
        yield return new DetailRow("Player Id", player.PlayerId ?? "-");
        yield return new DetailRow("Team", FormatNullable(player.TeamIndex));
        yield return new DetailRow("Placement", FormatNullable(player.Placement));
        yield return new DetailRow("Kills", FormatNullable(player.Kills));
        yield return new DetailRow("Platform", string.IsNullOrWhiteSpace(player.Platform) ? "-" : player.Platform);
        yield return new DetailRow("Level", FormatNullable(player.Level));
        yield return new DetailRow("Bot", player.IsBot ? "Yes" : "No");
        yield return new DetailRow("Replay Owner", player.IsReplayOwner ? "Yes" : "No");
        yield return new DetailRow("Anonymous", FormatBool(player.IsUsingAnonymousMode));
        yield return new DetailRow("Streamer Mode", FormatBool(player.IsUsingStreamerMode));
        yield return new DetailRow("Party Leader", player.IsPartyLeader ? "Yes" : "No");
        yield return new DetailRow("Character", player.Cosmetics.Character ?? "-");
        yield return new DetailRow("Backpack", player.Cosmetics.Backpack ?? "-");
        yield return new DetailRow("Pickaxe", player.Cosmetics.Pickaxe ?? "-");
        yield return new DetailRow("Glider", player.Cosmetics.Glider ?? "-");
        yield return new DetailRow("Death Cause", FormatNullable(player.DeathCause));
        yield return new DetailRow("Death Time", deathEvent is null ? "-" : FormatMatchClock(GetKillFeedTime(replay, deathEvent)));
        yield return new DetailRow("Eliminated By", !eliminatedBy.HasValue ? "-" : ResolvePlayerName(eliminatedBy.Value.Player, eliminatedBy.Value.NumericId, eliminatedBy.Value.LookupKey));
        yield return new DetailRow("Damage Events Given", hitsGiven.ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Damage Events Taken", hitsTaken.ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Total Damage", FormatDamageTotal(damageDealt.Players + damageDealt.Bots));
        yield return new DetailRow("Damage To Players", FormatDamageTotal(damageDealt.Players));
        yield return new DetailRow("Damage To Bots", FormatDamageTotal(damageDealt.Bots));
        yield return new DetailRow("Damage To Structures", FormatDamageTotal(damageDealt.Structures + damageDealt.Npcs));
        yield return new DetailRow("Damage Taken From Players", FormatDamageTotal(damageTaken.Players));
        yield return new DetailRow("Damage Taken From Bots", FormatDamageTotal(damageTaken.Bots));
        yield return new DetailRow("Damage Taken From Structures", FormatDamageTotal(damageTaken.Structures + damageTaken.Npcs));
    }

    private ReplayOwnerSummary BuildReplayOwnerSummary(FortniteReplay replay, PlayerData owner)
    {
        var hitsGiven = 0;
        var hitsTaken = 0;
        var damageToPlayers = 0F;
        var damageToBots = 0F;
        var damageToStructures = 0F;
        var damageTakenFromPlayers = 0F;
        var damageTakenFromBots = 0F;
        var damageTakenFromStructures = 0F;

        foreach (var evt in replay.DamageEvents)
        {
            var amount = evt.Magnitude ?? 0F;
            if (amount <= 0F)
            {
                continue;
            }

            if (MatchesPlayer(owner, evt.InstigatorId, evt.InstigatorName))
            {
                hitsGiven++;
                switch (GetDamageEventTargetCategory(replay, evt))
                {
                    case DamageParticipantCategory.Player:
                        damageToPlayers += amount;
                        break;
                    case DamageParticipantCategory.Bot:
                        damageToBots += amount;
                        break;
                    default:
                        damageToStructures += amount;
                        break;
                }
            }

            if (MatchesPlayer(owner, evt.TargetId, evt.TargetName))
            {
                hitsTaken++;
                switch (GetDamageEventInstigatorCategory(replay, evt))
                {
                    case DamageParticipantCategory.Player:
                        damageTakenFromPlayers += amount;
                        break;
                    case DamageParticipantCategory.Bot:
                        damageTakenFromBots += amount;
                        break;
                    default:
                        damageTakenFromStructures += amount;
                        break;
                }
            }
        }

        return new ReplayOwnerSummary
        {
            Owner = owner,
            HitsGiven = hitsGiven,
            HitsTaken = hitsTaken,
            DamageToPlayers = damageToPlayers,
            DamageToBots = damageToBots,
            DamageToStructures = damageToStructures,
            DamageTakenFromPlayers = damageTakenFromPlayers,
            DamageTakenFromBots = damageTakenFromBots,
            DamageTakenFromStructures = damageTakenFromStructures
        };
    }

    private IEnumerable<KillFeedRow> BuildPlayerCombatLog(FortniteReplay replay, PlayerData player)
    {
        return replay.KillFeed
            .Where(entry => MatchesPlayer(player, entry.PlayerId, entry.PlayerName) || MatchesResolvedKillFeedActor(replay, player, entry))
            .Where(entry => ShouldIncludePlayerKillLogEntry(replay, player, entry))
            .Select(entry => CreateKillFeedRow(replay, entry))
            .OrderBy(entry => entry.TimeValue);
    }

    private void RefreshPlayerKillLog()
    {
        if (_selectedReplayRow?.Replay is null || _selectedReplayRow.ViewCache is null || _selectedPlayer is null)
        {
            dgvPlayerCombatLog.DataSource = new List<KillFeedRow>();
            return;
        }

        var playerCache = GetPlayerViewCache(_selectedReplayRow.ViewCache, _selectedPlayer);
        dgvPlayerCombatLog.DataSource = playerCache is null
            ? BuildPlayerCombatLog(_selectedReplayRow.Replay, _selectedPlayer).ToList()
            : playerCache.KillLogRows.Where(ShouldIncludePlayerKillLogRow).ToList();
    }

    private IEnumerable<PlayerVictimRow> BuildPlayerVictimRows(FortniteReplay replay, PlayerData player)
    {
        return replay.KillFeed
            .Where(entry => MatchesResolvedKillFeedActor(replay, player, entry))
            .OrderBy(GetKillFeedTime)
            .Select(entry =>
            {
                var victim = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
                return new PlayerVictimRow
                {
                    PlayerIcon = GetPlayerSkinIcon(victim),
                    PlayerName = ResolvePlayerName(victim, entry.PlayerId, entry.PlayerName),
                    PlayerId = entry.PlayerId,
                    PlayerLookupKey = victim?.PlayerId ?? entry.PlayerName,
                    IsBot = victim?.IsBot ?? false,
                    EventText = GetKillFeedEventText(entry),
                    ReasonText = FormatKillFeedReason(replay, entry),
                    TimeText = FormatMatchClock(GetKillFeedTime(replay, entry)),
                    DistanceText = FormatDistance(entry.Distance)
                };
            });
    }

    private IEnumerable<CombatEventRow> BuildPlayerDamageRows(FortniteReplay replay, PlayerData player)
    {
        return replay.DamageEvents
            .Where(evt => MatchesPlayer(player, evt.InstigatorId, evt.InstigatorName) || MatchesPlayer(player, evt.TargetId, evt.TargetName))
            .Where(evt => ShouldIncludePlayerDamageEvent(replay, player, evt))
            .Select(evt => CreateCombatEventRow(replay, evt))
            .OrderBy(row => row.TimeValue);
    }

    private void ClearReplayDetails(bool keepReplaySelection = false)
    {
        if (!keepReplaySelection)
        {
            _selectedReplayRow = null;
        }

        _selectedPlayer = null;
        dgvGameStats.DataSource = new List<DetailRow>();
        dgvKillFeed.DataSource = new List<KillFeedRow>();
        dgvCombatEvents.DataSource = new List<CombatEventRow>();
        dgvPlayers.DataSource = new List<PlayerSummaryRow>();
        dgvPlayerOverview.DataSource = new List<DetailRow>();
        dgvPlayerCombatLog.DataSource = new List<KillFeedRow>();
        dgvPlayerVictims.DataSource = new List<PlayerVictimRow>();
        if (_picSelectedPlayer is not null) _picSelectedPlayer.Image = CosmeticIconCache.GetPlaceholderImage();
        if (_dgvPlayerDamageLog is not null)
        {
            _dgvPlayerDamageLog.DataSource = new List<CombatEventRow>();
        }
        lblSelectedPlayer.Text = "Select a player to inspect their stats.";
        lblPlayerPanelTitle.Text = "Player Stats";
        _playerSubjectTabs?.TabPages.Clear();
    }

    private void BindReplayRows()
    {
        CaptureReplayBrowserColumnWidths();

        var currentPath = _selectedReplayRow?.FilePath;
        var selectedPaths = dgvReplayBrowser.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(gridRow => gridRow.DataBoundItem as ReplayBrowserRow)
            .Where(row => row is not null)
            .Select(row => row!.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentScroll = dgvReplayBrowser.FirstDisplayedScrollingRowIndex;
        var ordered = OrderReplayRows().ToList();

        _suppressReplaySelectionChanged = true;
        dgvReplayBrowser.DataSource = ordered;
        RestoreReplayBrowserColumnWidths();

        foreach (DataGridViewRow gridRow in dgvReplayBrowser.Rows)
        {
            if ((gridRow.DataBoundItem as ReplayBrowserRow)?.FilePath is string path
                && selectedPaths.Contains(path))
            {
                gridRow.Selected = true;
            }
        }

        var restoredCurrentCell = false;
        if (currentPath is not null)
        {
            foreach (DataGridViewRow gridRow in dgvReplayBrowser.Rows)
            {
                if ((gridRow.DataBoundItem as ReplayBrowserRow)?.FilePath == currentPath)
                {
                    gridRow.Selected = true;
                    if (dgvReplayBrowser.SelectedRows.Count <= 1 && gridRow.Cells.Count > 0)
                    {
                        dgvReplayBrowser.CurrentCell = gridRow.Cells[0];
                        restoredCurrentCell = true;
                    }

                    break;
                }
            }
        }

        if (!restoredCurrentCell)
        {
            dgvReplayBrowser.CurrentCell = null;
        }

        if (currentScroll >= 0 && dgvReplayBrowser.Rows.Count > currentScroll)
        {
            dgvReplayBrowser.FirstDisplayedScrollingRowIndex = currentScroll;
        }

        _suppressReplaySelectionChanged = false;
    }

    private void SortReplayRows(string columnName, bool toggle = true)
    {
        if (toggle && _replaySortColumn == columnName)
        {
            _replaySortAscending = !_replaySortAscending;
        }
        else
        {
            _replaySortColumn = columnName;
            _replaySortAscending = columnName is nameof(ReplayBrowserRow.FileName) or nameof(ReplayBrowserRow.Status);
        }

        BindReplayRows();
    }
    private IEnumerable<ReplayBrowserRow> OrderReplayRows()
    {
        Func<ReplayBrowserRow, object?> selector = _replaySortColumn switch
        {
            nameof(ReplayBrowserRow.FileName) => row => row.FileName,
            nameof(ReplayBrowserRow.RecordedAtText) or nameof(ReplayBrowserRow.RecordedAt) => row => row.RecordedAt,
            nameof(ReplayBrowserRow.DurationText) => row => row.Duration,
            nameof(ReplayBrowserRow.PlacementText) => row => row.Placement ?? int.MaxValue,
            nameof(ReplayBrowserRow.KillsText) => row => row.Kills ?? uint.MinValue,
            nameof(ReplayBrowserRow.PlayerCountText) => row => row.PlayerCount,
            nameof(ReplayBrowserRow.Status) => row => row.Status,
            _ => row => row.RecordedAt
        };

        return _replaySortAscending ? _replayRows.OrderBy(selector).ThenBy(row => row.FileName) : _replayRows.OrderByDescending(selector).ThenBy(row => row.FileName);
    }

    private void SortPlayerRows(string columnName, bool toggle = true)
    {
        if (toggle && _playerSortColumn == columnName)
        {
            _playerSortAscending = !_playerSortAscending;
        }
        else
        {
            _playerSortColumn = columnName;
            _playerSortAscending = columnName == nameof(PlayerSummaryRow.DisplayName);
        }

        var selectedKey = _selectedPlayer?.PlayerId;
        var currentScroll = dgvPlayers.FirstDisplayedScrollingRowIndex;
        Func<PlayerSummaryRow, object?> selector = _playerSortColumn switch
        {
            nameof(PlayerSummaryRow.DisplayName) => row => row.DisplayName,
            nameof(PlayerSummaryRow.TeamText) => row => row.Team ?? int.MaxValue,
            nameof(PlayerSummaryRow.PlacementText) => row => row.Placement ?? int.MaxValue,
            nameof(PlayerSummaryRow.KillsText) => row => row.Kills ?? uint.MinValue,
            nameof(PlayerSummaryRow.Platform) => row => row.Platform,
            nameof(PlayerSummaryRow.IsBot) => row => row.IsBot,
            _ => row => row.Kills ?? uint.MinValue
        };

        var ordered = (_playerSortAscending ? _playerRows.OrderBy(selector).ThenBy(row => row.DisplayName) : _playerRows.OrderByDescending(selector).ThenBy(row => row.DisplayName)).ToList();
        dgvPlayers.DataSource = ordered;

        if (selectedKey is not null)
        {
            foreach (DataGridViewRow gridRow in dgvPlayers.Rows)
            {
                if ((gridRow.DataBoundItem as PlayerSummaryRow)?.Player.PlayerId == selectedKey)
                {
                    gridRow.Selected = true;
                    dgvPlayers.CurrentCell = gridRow.Cells[0];
                    break;
                }
            }
        }

        if (currentScroll >= 0 && dgvPlayers.Rows.Count > currentScroll)
        {
            dgvPlayers.FirstDisplayedScrollingRowIndex = currentScroll;
        }
    }

    private void SetReplayPaneCollapsed(bool collapsed)
    {
        if (collapsed && !_isReplayPaneCollapsed)
        {
            _lastExpandedReplayPaneWidth = Math.Max(ExpandedReplayPaneWidth, splitMain.SplitterDistance);
        }

        _isReplayPaneCollapsed = collapsed;
        dgvReplayBrowser.Visible = !collapsed;
        lblReplayStatus.Visible = !collapsed;
        lblReplayHeader.AutoSize = !collapsed;
        lblReplayHeader.Text = collapsed ? string.Join(Environment.NewLine, "Replays".ToCharArray()) : "Replay Browser";
        lblReplayHeader.Location = collapsed ? new Point(14, 8) : new Point(12, 10);
        splitMain.SplitterDistance = collapsed ? CollapsedReplayPaneWidth : _lastExpandedReplayPaneWidth;
        UpdateReplayBrowserHeaderChrome();
    }


    private void UpdateReplayBrowserHeaderChrome()
    {
        if (_lblReplayHideHint is null)
        {
            return;
        }

        _lblReplayHideHint.Visible = !_isReplayPaneCollapsed;
        if (_isReplayPaneCollapsed)
        {
            return;
        }

        _lblReplayHideHint.Location = new Point(lblReplayHeader.Right + 8, lblReplayHeader.Top + 5);
        lblReplayStatus.Location = new Point(12, 42);
        lblReplayStatus.Size = new Size(replayBrowserHeader.ClientSize.Width - 24, 22);
    }

    private static DataGridViewTextBoxColumn CreateIconNameColumn(string name, string headerText, string dataPropertyName, float fillWeight)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = headerText,
            DataPropertyName = dataPropertyName,
            FillWeight = fillWeight,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                ForeColor = Color.RoyalBlue,
                SelectionForeColor = Color.White,
                Padding = new Padding(24, 0, 0, 0)
            }
        };
    }

    private void EnsureIconTextRendering(DataGridView grid)
    {
        if (!_iconTextConfiguredGrids.Add(grid))
        {
            return;
        }

        grid.CellPainting += HandleIconTextCellPainting;
        grid.CellMouseMove += HandleIconTextCellMouseMove;
        grid.CellMouseLeave += (_, _) => grid.Cursor = Cursors.Default;
    }

    private void HandleIconTextCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        if (!TryGetGridCellIcon(grid.Rows[e.RowIndex].DataBoundItem, grid.Columns[e.ColumnIndex].Name, out var icon) || icon is null)
        {
            return;
        }

        e.Paint(e.CellBounds, e.PaintParts);
        var iconBounds = new Rectangle(e.CellBounds.Left + 4, e.CellBounds.Top + ((e.CellBounds.Height - 16) / 2), 16, 16);
        e.Graphics.DrawImage(icon, iconBounds);
        e.Handled = true;
    }

    private void HandleIconTextCellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        grid.Cursor = IsClickableNameColumn(grid.Columns[e.ColumnIndex].Name) ? Cursors.Hand : Cursors.Default;
    }

    private static bool TryGetGridCellIcon(object? rowItem, string columnName, out Image? icon)
    {
        icon = rowItem switch
        {
            KillFeedRow killFeedRow when columnName == nameof(KillFeedRow.ActorName) => killFeedRow.ActorIcon,
            KillFeedRow killFeedRow when columnName == nameof(KillFeedRow.TargetName) => killFeedRow.TargetIcon,
            CombatEventRow combatEventRow when columnName == nameof(CombatEventRow.AttackerName) => combatEventRow.AttackerIcon,
            CombatEventRow combatEventRow when columnName == nameof(CombatEventRow.TargetName) => combatEventRow.TargetIcon,
            PlayerSummaryRow playerRow when columnName == nameof(PlayerSummaryRow.DisplayName) => playerRow.ProfileIcon,
            PlayerVictimRow victimRow when columnName == nameof(PlayerVictimRow.PlayerName) => victimRow.PlayerIcon,
            _ => null
        };

        return icon is not null;
    }

    private static bool IsClickableNameColumn(string columnName)
    {
        return columnName is nameof(KillFeedRow.ActorName)
            or nameof(KillFeedRow.TargetName)
            or nameof(CombatEventRow.AttackerName)
            or nameof(CombatEventRow.TargetName)
            or nameof(PlayerSummaryRow.DisplayName)
            or nameof(PlayerVictimRow.PlayerName);
    }
    private static PlayerData? GetReplayOwner(FortniteReplay replay) => replay.PlayerData?.FirstOrDefault(player => player.IsReplayOwner);

    private PlayerData? FindPlayer(FortniteReplay replay, int? playerId, string? playerLookupKey)
    {
        var lookupCache = GetReplayLookupCache(replay);
        if (playerId.HasValue && lookupCache.PlayersById.TryGetValue(playerId.Value, out var playerById))
        {
            return playerById;
        }

        if (!string.IsNullOrWhiteSpace(playerLookupKey) && lookupCache.PlayersByLookupKey.TryGetValue(playerLookupKey, out var playerByKey))
        {
            return playerByKey;
        }

        return null;
    }

    private static bool MatchesPlayer(PlayerData player, int? playerId, string? playerLookupKey)
    {
        if (playerId.HasValue && player.Id == playerId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(playerLookupKey) && string.Equals(player.PlayerId, playerLookupKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesKillFeedTarget(KillFeedEntry entry, int? playerId, string? playerLookupKey)
    {
        if (playerId.HasValue && entry.PlayerId == playerId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(playerLookupKey) && string.Equals(entry.PlayerName, playerLookupKey, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesResolvedKillFeedActor(FortniteReplay replay, PlayerData player, KillFeedEntry entry)
    {
        var actorReference = ResolveKillFeedActorReference(replay, entry);
        return MatchesPlayer(player, actorReference.NumericId, actorReference.LookupKey);
    }

    private (PlayerData? Player, int? NumericId, string? LookupKey) ResolveKillFeedActorReference(FortniteReplay replay, KillFeedEntry entry)
    {
        var lookupCache = GetReplayLookupCache(replay);
        if (lookupCache.ResolvedActorCache.TryGetValue(entry, out var cachedReference))
        {
            return cachedReference;
        }

        var directActor = FindPlayer(replay, entry.FinisherOrDowner, entry.FinisherOrDownerName);
        if (directActor is not null || entry.FinisherOrDowner.HasValue || !string.IsNullOrWhiteSpace(entry.FinisherOrDownerName))
        {
            var resolved = (directActor, directActor?.Id ?? entry.FinisherOrDowner, directActor?.PlayerId ?? entry.FinisherOrDownerName);
            lookupCache.ResolvedActorCache[entry] = resolved;
            return resolved;
        }

        if (entry.IsDowned || entry.IsRevived)
        {
            const string? noLookupKey = null;
            var empty = ((PlayerData?)null, (int?)null, noLookupKey);
            lookupCache.ResolvedActorCache[entry] = empty;
            return empty;
        }

        var currentTime = GetKillFeedTime(replay, entry);
        foreach (var priorEntry in lookupCache.KillFeedByDescendingTime)
        {
            if (ReferenceEquals(priorEntry, entry)
                || !MatchesKillFeedTarget(priorEntry, entry.PlayerId, entry.PlayerName)
                || GetKillFeedTime(priorEntry) > currentTime)
            {
                continue;
            }

            if (priorEntry.IsRevived)
            {
                break;
            }

            if (priorEntry.IsDowned)
            {
                var inferredActor = FindPlayer(replay, priorEntry.FinisherOrDowner, priorEntry.FinisherOrDownerName);
                var resolved = (inferredActor, inferredActor?.Id ?? priorEntry.FinisherOrDowner, inferredActor?.PlayerId ?? priorEntry.FinisherOrDownerName);
                lookupCache.ResolvedActorCache[entry] = resolved;
                return resolved;
            }

            break;
        }

        const string? unresolvedLookupKey = null;
        var unresolved = ((PlayerData?)null, (int?)null, unresolvedLookupKey);
        lookupCache.ResolvedActorCache[entry] = unresolved;
        return unresolved;
    }

    private static string ResolveCombatantName(PlayerData? player, int? numericId, string? fallback = null, bool isBot = false)
    {
        if (player is not null)
        {
            return ResolvePlayerName(player, numericId, fallback);
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return ShortenIdentifier(fallback);
        }

        if (isBot && numericId.HasValue)
        {
            return $"Bot {numericId.Value:000}";
        }

        if (numericId.HasValue)
        {
            return $"Structure {numericId.Value}";
        }

        return "Structure";
    }

    private static string FormatWeaponType(DamageEvent evt)
    {
        var displayName = NormalizeWeaponDisplayLabel(evt.WeaponName);
        if (!string.IsNullOrWhiteSpace(displayName) && !string.Equals(displayName, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return displayName;
        }

        var weaponType = NormalizeWeaponTypeLabel(evt.WeaponType);
        if (!string.IsNullOrWhiteSpace(weaponType) && !string.Equals(weaponType, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return weaponType;
        }

        if (!string.IsNullOrWhiteSpace(evt.WeaponAssetName))
        {
            var inferredFromAsset = InferWeaponLabelFromTags([evt.WeaponAssetName]);
            if (!string.IsNullOrWhiteSpace(inferredFromAsset))
            {
                return inferredFromAsset;
            }
        }

        if (!string.IsNullOrWhiteSpace(evt.WeaponClassName))
        {
            var inferredFromClass = InferWeaponLabelFromTags([evt.WeaponClassName]);
            if (!string.IsNullOrWhiteSpace(inferredFromClass))
            {
                return inferredFromClass;
            }
        }

        return GetUnknownWeaponFallback(evt) ?? "Unknown";
    }

    private string FormatCombatEventWeaponType(FortniteReplay replay, DamageEvent evt)
    {
        var label = GetMostSpecificWeaponLabel(evt) ?? FormatWeaponType(evt);
        if (!string.Equals(label, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return label;
        }

        var inferred = InferWeaponLabelFromNearbyDamageEvent(replay, evt)
            ?? InferWeaponLabelFromNearbyKillFeed(replay, evt);

        return string.IsNullOrWhiteSpace(inferred) ? label : inferred;
    }

    private static string? GetUnknownWeaponFallback(DamageEvent evt)
    {
        var eventTag = NormalizeWeaponDisplayLabel(evt.EventTag);
        if (!string.IsNullOrWhiteSpace(eventTag))
        {
            return eventTag;
        }

        var assetName = NormalizeWeaponDisplayLabel(evt.WeaponAssetName);
        if (!string.IsNullOrWhiteSpace(assetName))
        {
            return assetName;
        }

        var className = NormalizeWeaponDisplayLabel(evt.WeaponClassName);
        if (!string.IsNullOrWhiteSpace(className))
        {
            return className;
        }

        if (!string.IsNullOrWhiteSpace(evt.EventTag))
        {
            return ShortGameplayTag(evt.EventTag);
        }

        return null;
    }

    private string? InferWeaponLabelFromNearbyDamageEvent(FortniteReplay replay, DamageEvent evt)
    {
        var eventTime = GetDamageTime(replay, evt);
        return replay.DamageEvents
            .Where(candidate => !ReferenceEquals(candidate, evt))
            .Where(candidate => candidate.InstigatorId == evt.InstigatorId && candidate.TargetId == evt.TargetId)
            .Where(candidate => Math.Abs(GetDamageTime(replay, candidate) - eventTime) <= 2.5D)
            .Select(candidate => GetMostSpecificWeaponLabel(candidate) ?? FormatWeaponType(candidate))
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && !string.Equals(candidate, "Unknown", StringComparison.OrdinalIgnoreCase));
    }



    private static bool TryParseReplayTimestampFromFileName(string fileName, out DateTime timestamp)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fileName, @"(?<year>\d{4})\.(?<month>\d{2})\.(?<day>\d{2})-(?<hour>\d{2})\.(?<minute>\d{2})\.(?<second>\d{2})");
        if (match.Success
            && int.TryParse(match.Groups["year"].Value, out var year)
            && int.TryParse(match.Groups["month"].Value, out var month)
            && int.TryParse(match.Groups["day"].Value, out var day)
            && int.TryParse(match.Groups["hour"].Value, out var hour)
            && int.TryParse(match.Groups["minute"].Value, out var minute)
            && int.TryParse(match.Groups["second"].Value, out var second))
        {
            timestamp = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
            return true;
        }

        timestamp = default;
        return false;
    }

    private void EnsureReplayTab(ReplayBrowserRow row)
    {
        if (_openedReplayTabs is null)
        {
            return;
        }

        var tabPage = _openedReplayTabs.TabPages
            .Cast<TabPage>()
            .FirstOrDefault(page => string.Equals((page.Tag as ReplayBrowserRow)?.FilePath, row.FilePath, StringComparison.OrdinalIgnoreCase));

        if (tabPage is null)
        {
            tabPage = new TabPage(ShortenTabTitle(row.FileName)) { Tag = row };
            _openedReplayTabs.TabPages.Add(tabPage);
        }

        if (!ReferenceEquals(splitContent.Parent, tabPage))
        {
            splitContent.Parent?.Controls.Remove(splitContent);
            tabPage.Controls.Add(splitContent);
            splitContent.Dock = DockStyle.Fill;
        }

        _suppressReplayTabSelection = true;
        _openedReplayTabs.SelectedTab = tabPage;
        _suppressReplayTabSelection = false;
    }

    private async Task HandleReplayTabSelectionChangedAsync()
    {
        if (_suppressReplayTabSelection || _openedReplayTabs?.SelectedTab?.Tag is not ReplayBrowserRow row)
        {
            return;
        }

        SelectReplayBrowserRow(row);
        _selectedReplayRow = row;

        if (row.Replay is null || row.LoadedParseMode < ParseMode.Full)
        {
            await EnsureReplayLoadedAsync(row, ParseMode.Full, updateSelectionView: true);
            return;
        }

        DisplayReplay(row);
    }

    private void SelectReplayBrowserRow(ReplayBrowserRow row)
    {
        _suppressReplaySelectionChanged = true;
        foreach (DataGridViewRow gridRow in dgvReplayBrowser.Rows)
        {
            if ((gridRow.DataBoundItem as ReplayBrowserRow)?.FilePath == row.FilePath)
            {
                gridRow.Selected = true;
                dgvReplayBrowser.CurrentCell = gridRow.Cells[0];
                break;
            }
        }
        _suppressReplaySelectionChanged = false;
    }

    private IEnumerable<string> GetReplayFilePaths()
    {
        var replayFiles = Enumerable.Empty<string>();
        if (Directory.Exists(ReplayFolder))
        {
            replayFiles = Directory.EnumerateFiles(ReplayFolder, "*.replay", SearchOption.TopDirectoryOnly);
        }

        return replayFiles
            .Concat(_manuallyAddedReplayPaths.Where(File.Exists))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(File.GetLastWriteTimeUtc);
    }

    private async Task OpenReplayFilesAsync()
    {
        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Fortnite Replay Files (*.replay)|*.replay|All Files (*.*)|*.*",
            Multiselect = true,
            Title = "Open Replay File"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await AddReplayFilesAsync(dialog.FileNames);
    }

    private void HandleReplayBrowserCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= dgvReplayBrowser.Rows.Count)
        {
            _contextMenuReplayRow = null;
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            _isReplayDragSelecting = true;
            _replayDragSelectionChanged = false;
            _replayDragStartIndex = e.RowIndex;
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            _contextMenuReplayRow = dgvReplayBrowser.Rows[e.RowIndex].DataBoundItem as ReplayBrowserRow;
        }
    }

    private void HandleReplayBrowserCellMouseEnter(object? sender, DataGridViewCellEventArgs e)
    {
        if (!_isReplayDragSelecting || e.RowIndex < 0 || e.RowIndex >= dgvReplayBrowser.Rows.Count)
        {
            return;
        }

        if ((Control.MouseButtons & MouseButtons.Left) != MouseButtons.Left || _replayDragStartIndex < 0)
        {
            return;
        }

        if (e.RowIndex == _replayDragStartIndex && !_replayDragSelectionChanged)
        {
            return;
        }

        _ignoreReplaySelectionChanged = true;
        _replayDragSelectionChanged = true;
        SelectReplayDragRange(e.RowIndex);
    }

    private void HandleReplayBrowserCellMouseUp(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _isReplayDragSelecting = false;
        _replayDragStartIndex = -1;

        if (_replayDragSelectionChanged)
        {
            var selectedIndices = GetSelectedReplayRowIndices();
            _ignoreReplayBrowserCellClick = true;
            _ignoreReplaySelectionChanged = true;
            _suppressReplaySelectionChanged = true;
            dgvReplayBrowser.CurrentCell = null;

            BeginInvoke(new Action(() =>
            {
                ReapplyReplaySelection(selectedIndices);
                dgvReplayBrowser.CurrentCell = null;
                _suppressReplaySelectionChanged = false;
                _ignoreReplaySelectionChanged = false;
                _replayDragSelectionChanged = false;
            }));
        }
    }

    private ReplayBrowserRow? GetReplayRowForContextMenu()
    {
        if (_contextMenuReplayRow is not null)
        {
            return _contextMenuReplayRow;
        }

        if (dgvReplayBrowser.CurrentRow?.DataBoundItem is ReplayBrowserRow currentRow)
        {
            return currentRow;
        }

        return _selectedReplayRow;
    }

    private void QueueSelectedReplayLoad()
    {
        var rows = GetReplayRowsForContextAction();
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            QueueReplayLoad(row, ParseMode.Full);
        }

        ClearReplayBrowserContextTarget();
    }

    private void QueueAllReplayLoads()
    {
        foreach (var row in _replayRows)
        {
            QueueReplayLoad(row, ParseMode.Full);
        }

        ClearReplayBrowserContextTarget();
    }

    private void QueueReplayLoad(ReplayBrowserRow row, ParseMode parseMode)
    {
        if (IsReplayLoaded(row) || row.IsLoading || row.IsQueued)
        {
            return;
        }

        row.StopRequested = false;
        row.IsQueued = true;
        row.QueuedParseMode = parseMode;
        row.Status = "Queued";
        if (_pendingReplayLoadPaths.Add(row.FilePath))
        {
            _pendingReplayLoads.Enqueue(row);
        }

        RequestReplayRowsRefresh();
        lblReplayStatus.Text = $"{row.FileName} queued for loading.";
        _ = ProcessReplayLoadQueueAsync();
    }

    private async Task ProcessReplayLoadQueueAsync()
    {
        if (_isProcessingReplayQueue)
        {
            return;
        }

        _isProcessingReplayQueue = true;
        try
        {
            while (_pendingReplayLoads.Count > 0)
            {
                var batch = new List<Task>(MaxParallelReplayLoads);
                while (_pendingReplayLoads.Count > 0 && batch.Count < MaxParallelReplayLoads)
                {
                    var row = _pendingReplayLoads.Dequeue();
                    _pendingReplayLoadPaths.Remove(row.FilePath);

                    if (!row.IsQueued || row.StopRequested || IsReplayLoaded(row))
                    {
                        row.IsQueued = false;
                        if (row.StopRequested)
                        {
                            MarkReplayAsStopped(row, updateSelectionView: false);
                        }

                        continue;
                    }

                    batch.Add(LoadReplayRowAsync(row, row.QueuedParseMode, updateSelectionView: false));
                }

                if (batch.Count == 0)
                {
                    continue;
                }

                await Task.WhenAll(batch);
            }
        }
        finally
        {
            _isProcessingReplayQueue = false;
        }
    }

    private void UnloadSelectedReplay()
    {
        var rows = GetReplayRowsForContextAction();
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            UnloadReplay(row);
        }

        ClearReplayBrowserContextTarget();
    }

    private void UnloadReplay(ReplayBrowserRow row)
    {
        CancelQueuedReplayLoad(row, preserveStatus: false);
        row.StopRequested = false;
        row.Replay = null;
        row.WeaponStatsSnapshots = [];
        row.LoadedParseMode = ParseMode.EventsOnly;
        row.Status = "Not loaded";
        CloseReplayTab(row);
        RequestReplayRowsRefresh();

        if (row == _selectedReplayRow)
        {
            ClearReplayDetails(keepReplaySelection: true);
            lblReplayStatus.Text = $"{row.FileName} unloaded.";
        }
    }

    private void StopSelectedReplayLoad()
    {
        var rows = GetReplayRowsForContextAction();
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            StopReplayLoad(row);
        }

        ClearReplayBrowserContextTarget();
    }

    private void ClearReplayBrowserContextTarget()
    {
        _contextMenuReplayRow = null;
        if (_loadReplayMenuItem is not null) _loadReplayMenuItem.Tag = null;
        if (_loadAllReplayMenuItem is not null) _loadAllReplayMenuItem.Tag = null;
        if (_unloadReplayMenuItem is not null) _unloadReplayMenuItem.Tag = null;
        if (_stopReplayMenuItem is not null) _stopReplayMenuItem.Tag = null;
    }

    private List<ReplayBrowserRow> GetReplayRowsForContextAction(ReplayBrowserRow? contextRow = null)
    {
        contextRow ??= _contextMenuReplayRow ?? _loadReplayMenuItem?.Tag as ReplayBrowserRow ?? GetReplayRowForContextMenu();
        if (contextRow is null)
        {
            return [];
        }

        var selectedRows = dgvReplayBrowser.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(gridRow => gridRow.DataBoundItem as ReplayBrowserRow)
            .Where(row => row is not null)
            .Cast<ReplayBrowserRow>()
            .Distinct()
            .ToList();

        if (selectedRows.Count > 1 && selectedRows.Contains(contextRow))
        {
            return selectedRows;
        }

        return [contextRow];
    }

    private void HandleReplayBrowserKeyDown(object? sender, KeyEventArgs e)
    {
        if (!(e.Control && e.KeyCode == Keys.A))
        {
            return;
        }

        _suppressReplaySelectionChanged = true;
        try
        {
            foreach (DataGridViewRow row in dgvReplayBrowser.Rows)
            {
                row.Selected = true;
            }

            RestoreReplayBrowserCurrentRow();
        }
        finally
        {
            _suppressReplaySelectionChanged = false;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void SelectReplayDragRange(int hoveredRowIndex)
    {
        var start = Math.Min(_replayDragStartIndex, hoveredRowIndex);
        var end = Math.Max(_replayDragStartIndex, hoveredRowIndex);

        _suppressReplaySelectionChanged = true;
        try
        {
            dgvReplayBrowser.ClearSelection();
            for (var index = start; index <= end; index++)
            {
                dgvReplayBrowser.Rows[index].Selected = true;
            }
        }
        finally
        {
            _suppressReplaySelectionChanged = false;
        }
    }

    private List<int> GetSelectedReplayRowIndices()
    {
        return dgvReplayBrowser.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.Index)
            .Where(index => index >= 0 && index < dgvReplayBrowser.Rows.Count)
            .OrderBy(index => index)
            .ToList();
    }

    private void ReapplyReplaySelection(IReadOnlyList<int> indices)
    {
        _suppressReplaySelectionChanged = true;
        try
        {
            dgvReplayBrowser.ClearSelection();
            foreach (var index in indices)
            {
                if (index >= 0 && index < dgvReplayBrowser.Rows.Count)
                {
                    dgvReplayBrowser.Rows[index].Selected = true;
                }
            }
        }
        finally
        {
            _suppressReplaySelectionChanged = false;
        }
    }

    private void RestoreReplayBrowserCurrentRow()
    {
        if (dgvReplayBrowser.SelectedRows.Count > 1)
        {
            dgvReplayBrowser.CurrentCell = null;
            return;
        }

        if (_selectedReplayRow is null || dgvReplayBrowser.Columns.Count == 0)
        {
            dgvReplayBrowser.CurrentCell = null;
            return;
        }

        var selectedIndex = _replayRows.IndexOf(_selectedReplayRow);
        if (selectedIndex < 0 || selectedIndex >= dgvReplayBrowser.Rows.Count)
        {
            dgvReplayBrowser.CurrentCell = null;
            return;
        }

        var row = dgvReplayBrowser.Rows[selectedIndex];
        if (row.Cells.Count == 0)
        {
            dgvReplayBrowser.CurrentCell = null;
            return;
        }

        var firstVisibleColumn = dgvReplayBrowser.Columns
            .Cast<DataGridViewColumn>()
            .FirstOrDefault(column => column.Visible);

        if (firstVisibleColumn is null || firstVisibleColumn.Index < 0 || firstVisibleColumn.Index >= row.Cells.Count)
        {
            dgvReplayBrowser.CurrentCell = null;
            return;
        }

        try
        {
            dgvReplayBrowser.CurrentCell = row.Cells[firstVisibleColumn.Index];
        }
        catch (ArgumentOutOfRangeException)
        {
            dgvReplayBrowser.CurrentCell = null;
        }
        catch (InvalidOperationException)
        {
            dgvReplayBrowser.CurrentCell = null;
        }
    }

    private void StopReplayLoad(ReplayBrowserRow row)
    {
        if (row.IsQueued)
        {
            row.StopRequested = true;
            CancelQueuedReplayLoad(row, preserveStatus: false);
            MarkReplayAsStopped(row, updateSelectionView: row == _selectedReplayRow);
            return;
        }

        if (!row.IsLoading)
        {
            return;
        }

        row.StopRequested = true;
        row.Status = "Stopping...";
        RequestReplayRowsRefresh();
        if (row == _selectedReplayRow)
        {
            lblReplayStatus.Text = $"Stopping {row.FileName}...";
        }
    }

    private void CancelQueuedReplayLoad(ReplayBrowserRow row, bool preserveStatus)
    {
        if (!row.IsQueued)
        {
            return;
        }

        row.IsQueued = false;
        _pendingReplayLoadPaths.Remove(row.FilePath);
        if (!preserveStatus)
        {
            row.Status = IsReplayLoaded(row) ? row.Status : "Not loaded";
        }
    }

    private void MarkReplayAsStopped(ReplayBrowserRow row, bool updateSelectionView)
    {
        row.IsQueued = false;
        row.IsLoading = false;
        row.StopRequested = false;
        row.Replay = null;
        row.LoadedParseMode = ParseMode.EventsOnly;
        row.Status = "Stopped";
        RequestReplayRowsRefresh();

        if (updateSelectionView)
        {
            ClearReplayDetails(keepReplaySelection: true);
            lblReplayStatus.Text = $"{row.FileName} stopped.";
        }
    }

    private static bool IsReplayLoaded(ReplayBrowserRow row) => row.Replay is not null && row.LoadedParseMode >= ParseMode.Full;

    private Task AddReplayFilesAsync(IEnumerable<string> filePaths)
    {
        var addedCount = 0;

        foreach (var filePath in filePaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var existingRow = _replayRows.FirstOrDefault(row => string.Equals(row.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (existingRow is not null)
            {
                continue;
            }

            _manuallyAddedReplayPaths.Add(filePath);
            var newRow = ReplayBrowserRow.CreateFromFile(filePath);
            _replayRows.Add(newRow);
            addedCount++;
        }

        if (addedCount == 0)
        {
            return Task.CompletedTask;
        }

        BindReplayRows();
        lblReplayStatus.Text = addedCount == 1
            ? "Added 1 replay file. Click it to load."
            : $"Added {addedCount} replay files. Click a replay to load it.";
        return Task.CompletedTask;
    }

    private void CloseCurrentReplayFile()
    {
        if (_selectedReplayRow is null)
        {
            return;
        }

        RemoveReplayRow(_selectedReplayRow);
    }

    private void CloseAllReplayFiles()
    {
        if (_openedReplayTabs is not null && ReferenceEquals(splitContent.Parent, _openedReplayTabs.SelectedTab))
        {
            _openedReplayTabs.SelectedTab.Controls.Remove(splitContent);
        }

        _openedReplayTabs?.TabPages.Clear();
        _replayRows.Clear();
        _pendingReplayLoads.Clear();
        _pendingReplayLoadPaths.Clear();
        _manuallyAddedReplayPaths.Clear();
        ClearReplayDetails();
        BindReplayRows();
        lblReplayStatus.Text = "No replay files loaded.";
    }

    private void RemoveReplayRow(ReplayBrowserRow row)
    {
        var orderedRows = OrderReplayRows().ToList();
        var orderedIndex = orderedRows.FindIndex(candidate => string.Equals(candidate.FilePath, row.FilePath, StringComparison.OrdinalIgnoreCase));
        StopReplayLoad(row);
        CloseReplayTab(row);
        _replayRows.RemoveAll(candidate => string.Equals(candidate.FilePath, row.FilePath, StringComparison.OrdinalIgnoreCase));
        _manuallyAddedReplayPaths.Remove(row.FilePath);

        if (_replayRows.Count == 0)
        {
            ClearReplayDetails();
            BindReplayRows();
            lblReplayStatus.Text = "No replay files loaded.";
            return;
        }

        var nextOrderedRows = OrderReplayRows().ToList();
        var nextIndex = orderedIndex < 0 ? 0 : Math.Min(orderedIndex, nextOrderedRows.Count - 1);
        var nextRow = nextOrderedRows[nextIndex];
        _selectedReplayRow = nextRow;
        BindReplayRows();
        SelectReplayBrowserRow(nextRow);

        if (nextRow.Replay is not null)
        {
            DisplayReplay(nextRow);
        }
        else
        {
            ClearReplayDetails(keepReplaySelection: true);
            lblReplayStatus.Text = $"{nextRow.FileName} selected. Click again to load if needed.";
        }
    }

    private void CloseReplayTab(ReplayBrowserRow row)
    {
        if (_openedReplayTabs is null)
        {
            return;
        }

        var tabPage = _openedReplayTabs.TabPages
            .Cast<TabPage>()
            .FirstOrDefault(page => string.Equals((page.Tag as ReplayBrowserRow)?.FilePath, row.FilePath, StringComparison.OrdinalIgnoreCase));

        if (tabPage is null)
        {
            return;
        }

        if (ReferenceEquals(splitContent.Parent, tabPage))
        {
            tabPage.Controls.Remove(splitContent);
        }

        _openedReplayTabs.TabPages.Remove(tabPage);
        tabPage.Dispose();
    }

    private Task OpenSettingsAsync()
    {
        using var settingsForm = new SettingsForm(_settings);
        settingsForm.ApplyRequested += (_, _) => ApplySettings(settingsForm.Settings);
        if (settingsForm.ShowDialog(this) != DialogResult.OK)
        {
            return Task.CompletedTask;
        }

        ApplySettings(settingsForm.Settings);
        return Task.CompletedTask;
    }

    private void ApplySettings(AnalyzerSettings settings)
    {
        var previousFolder = ReplayFolder;
        _settings = settings.Clone();
        AnalyzerSettingsStore.Save(_settings);
        DebugOutputWriter.SetEnabled(_settings.DebugOutputEnabled);
        ApplySettingsToUi();
        UpdateReplayBrowserHeaderChrome();
        LayoutContentBelowMenu();

        if (!string.Equals(previousFolder, ReplayFolder, StringComparison.OrdinalIgnoreCase))
        {
            _ = RefreshReplayBrowserAsync();
        }
    }

    private void RefreshDamageEventViews()
    {
        if (_selectedReplayRow?.Replay is not null)
        {
            BuildCombatEvents(_selectedReplayRow);
            if (_selectedPlayer is not null && _dgvPlayerDamageLog is not null)
            {
                var playerCache = _selectedReplayRow.ViewCache is null ? null : GetPlayerViewCache(_selectedReplayRow.ViewCache, _selectedPlayer);
                _dgvPlayerDamageLog.DataSource = playerCache is null
                    ? BuildPlayerDamageRows(_selectedReplayRow.Replay, _selectedPlayer).ToList()
                    : playerCache.DamageRows.Where(ShouldIncludePlayerDamageEventRow).ToList();
            }
        }
    }

    private void PopulatePlayerSubjectTabs(ReplayBrowserRow row)
    {
        if (_playerSubjectTabs is null || row.Replay is null)
        {
            return;
        }

        var owner = GetReplayOwner(row.Replay);
        if (owner is null)
        {
            _playerSubjectTabs.TabPages.Clear();
            return;
        }

        var players = row.Replay.PlayerData
            .Where(player => player.TeamIndex == owner.TeamIndex || MatchesPlayer(owner, player.Id, player.PlayerId))
            .DistinctBy(GetPlayerCacheKey)
            .OrderByDescending(player => MatchesPlayer(owner, player.Id, player.PlayerId))
            .ThenBy(player => ResolvePlayerName(player, player.Id, player.PlayerId))
            .ToList();

        _suppressPlayerSubjectTabSelection = true;
        try
        {
            _playerSubjectTabs.TabPages.Clear();
            foreach (var player in players)
            {
                _playerSubjectTabs.TabPages.Add(new TabPage(ResolvePlayerName(player, player.Id, player.PlayerId))
                {
                    Tag = player
                });
            }
        }
        finally
        {
            _suppressPlayerSubjectTabSelection = false;
        }
    }

    private void EnsurePlayerSubjectTab(PlayerData player, bool selectTab)
    {
        if (_playerSubjectTabs is null)
        {
            return;
        }

        var existing = _playerSubjectTabs.TabPages
            .Cast<TabPage>()
            .FirstOrDefault(page => page.Tag is PlayerData pagePlayer
                                    && string.Equals(GetPlayerCacheKey(pagePlayer), GetPlayerCacheKey(player), StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            existing = new TabPage(ResolvePlayerName(player, player.Id, player.PlayerId))
            {
                Tag = player
            };
            _playerSubjectTabs.TabPages.Add(existing);
        }

        if (!selectTab)
        {
            return;
        }

        _suppressPlayerSubjectTabSelection = true;
        try
        {
            _playerSubjectTabs.SelectedTab = existing;
        }
        finally
        {
            _suppressPlayerSubjectTabSelection = false;
        }
    }

    private void HandlePlayerSubjectTabSelectionChanged()
    {
        if (_suppressPlayerSubjectTabSelection || _playerSubjectTabs?.SelectedTab?.Tag is not PlayerData player)
        {
            return;
        }

        ShowPlayerDetails(player, false);
    }

    private static PlayerViewCache? GetPlayerViewCache(ReplayViewCache viewCache, PlayerData player)
    {
        return viewCache.PlayerCaches.TryGetValue(GetPlayerCacheKey(player), out var playerCache)
            ? playerCache
            : null;
    }

    private bool ShouldIncludeKillFeedRow(KillFeedRow row)
    {
        if ((_chkTeamKillFeedOnly?.Checked ?? false) && !row.InvolvesOwnerTeam)
        {
            return false;
        }

        var playersEnabled = _chkKillFeedPlayers?.Checked ?? true;
        var botsEnabled = _chkKillFeedBots?.Checked ?? true;
        var teamOnly = _chkTeamKillFeedOnly?.Checked ?? false;
        var hasBot = row.ActorIsBot || row.TargetIsBot;
        var hasTeammate = row.ActorIsTeammate || row.TargetIsTeammate;

        if (teamOnly && !hasTeammate)
        {
            return false;
        }

        if (teamOnly)
        {
            if (playersEnabled && botsEnabled) return true;
            if (playersEnabled) return hasTeammate && !hasBot;
            if (botsEnabled) return hasTeammate && hasBot;
            return row.ActorIsTeammate && row.TargetIsTeammate;
        }

        if (playersEnabled && botsEnabled) return true;
        if (playersEnabled) return !hasBot;
        if (botsEnabled) return hasBot;
        return false;
    }

    private bool ShouldIncludeReplayDamageEventRow(CombatEventRow row)
    {
        if ((_chkTeamDamageOnly?.Checked ?? false) && !row.InvolvesOwnerTeam)
        {
            return false;
        }

        return IsDamageCategoryEnabled(row.TargetCategory, _chkDamagePlayers, _chkDamageBots, _chkDamageStructures, _chkDamageNpcs);
    }

    private bool ShouldIncludePlayerDamageEventRow(CombatEventRow row)
    {
        var playerKey = _selectedPlayer is null ? null : GetPlayerCacheKey(_selectedPlayer);
        var playerMatchesAttacker = !string.IsNullOrWhiteSpace(playerKey)
            && string.Equals(row.AttackerLookupKey ?? row.AttackerId?.ToString(CultureInfo.InvariantCulture), playerKey, StringComparison.OrdinalIgnoreCase);

        var category = playerMatchesAttacker ? row.TargetCategory : row.AttackerCategory;
        return IsDamageCategoryEnabled(category, _chkPlayerDamagePlayers, _chkPlayerDamageBots, _chkPlayerDamageStructures, _chkPlayerDamageNpcs);
    }

    private bool ShouldIncludePlayerKillLogRow(KillFeedRow row)
    {
        var selectedPlayerKey = _selectedPlayer is null ? null : GetPlayerCacheKey(_selectedPlayer);
        var selectedPlayerIsTarget = !string.IsNullOrWhiteSpace(selectedPlayerKey)
            && string.Equals(row.TargetLookupKey ?? row.TargetId?.ToString(CultureInfo.InvariantCulture), selectedPlayerKey, StringComparison.OrdinalIgnoreCase);

        var counterpartIsBot = selectedPlayerIsTarget ? row.ActorIsBot : row.TargetIsBot;
        return counterpartIsBot
            ? _chkPlayerKillLogBots?.Checked ?? true
            : _chkPlayerKillLogPlayers?.Checked ?? true;
    }

    private bool ShouldIncludeReplayDamageEvent(FortniteReplay replay, DamageEvent evt)
    {
        if ((_chkTeamDamageOnly?.Checked ?? false) && !InvolvesReplayOwnerTeam(replay, evt))
        {
            return false;
        }

        var category = GetDamageEventTargetCategory(replay, evt);
        return IsDamageCategoryEnabled(category, _chkDamagePlayers, _chkDamageBots, _chkDamageStructures, _chkDamageNpcs);
    }

    private bool ShouldIncludePlayerDamageEvent(FortniteReplay replay, PlayerData player, DamageEvent evt)
    {
        var category = MatchesPlayer(player, evt.InstigatorId, evt.InstigatorName)
            ? GetDamageEventTargetCategory(replay, evt)
            : GetDamageEventParticipantCategory(replay, evt, false);

        return IsDamageCategoryEnabled(category, _chkPlayerDamagePlayers, _chkPlayerDamageBots, _chkPlayerDamageStructures, _chkPlayerDamageNpcs);
    }

    private bool ShouldIncludePlayerKillLogEntry(FortniteReplay replay, PlayerData player, KillFeedEntry entry)
    {
        var counterpart = MatchesPlayer(player, entry.PlayerId, entry.PlayerName)
            ? ResolveKillFeedActorReference(replay, entry).Player
            : FindPlayer(replay, entry.PlayerId, entry.PlayerName);

        return counterpart?.IsBot == true
            ? _chkPlayerKillLogBots?.Checked ?? true
            : _chkPlayerKillLogPlayers?.Checked ?? true;
    }

    private bool ShouldIncludeKillFeedEntry(FortniteReplay replay, KillFeedEntry entry)
    {
        var actor = ResolveKillFeedActorReference(replay, entry).Player;
        var target = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
        var playersEnabled = _chkKillFeedPlayers?.Checked ?? true;
        var botsEnabled = _chkKillFeedBots?.Checked ?? true;
        var teamOnly = _chkTeamKillFeedOnly?.Checked ?? false;
        var ownerTeam = GetReplayOwner(replay)?.TeamIndex;
        var actorIsBot = actor?.IsBot ?? false;
        var targetIsBot = target?.IsBot ?? false;
        var hasBot = actorIsBot || targetIsBot;
        var actorIsTeammate = ownerTeam.HasValue && actor?.TeamIndex == ownerTeam;
        var targetIsTeammate = ownerTeam.HasValue && target?.TeamIndex == ownerTeam;
        var hasTeammate = actorIsTeammate || targetIsTeammate;

        if (teamOnly && !hasTeammate)
        {
            return false;
        }

        if (teamOnly)
        {
            if (playersEnabled && botsEnabled)
            {
                return true;
            }

            if (playersEnabled)
            {
                return !hasBot;
            }

            if (botsEnabled)
            {
                return hasBot;
            }

            return actorIsTeammate && targetIsTeammate;
        }

        if (playersEnabled && botsEnabled)
        {
            return true;
        }

        if (playersEnabled)
        {
            return !hasBot;
        }

        if (botsEnabled)
        {
            return hasBot;
        }

        return false;
    }

    private static bool IsDamageCategoryEnabled(DamageParticipantCategory category, CheckBox? players, CheckBox? bots, CheckBox? structures, CheckBox? npcs)
    {
        return category switch
        {
            DamageParticipantCategory.Player => players?.Checked ?? true,
            DamageParticipantCategory.Bot => bots?.Checked ?? true,
            DamageParticipantCategory.Npc => (structures?.Checked ?? true) || (npcs?.Checked ?? true),
            _ => structures?.Checked ?? true
        };
    }

    private static bool IsLikelyStructureDamageEvent(DamageEvent evt)
    {
        if (evt.UsesNonPlayerTarget)
        {
            return true;
        }

        if (!evt.Magnitude.HasValue)
        {
            return false;
        }

        var magnitude = evt.Magnitude.Value;
        return magnitude == 0F
            || magnitude == 25F
            || magnitude == 50F
            || magnitude == 62.5F
            || magnitude == 100F
            || magnitude == 125F;
    }

    private DamageParticipantCategory GetDamageEventTargetCategory(FortniteReplay replay, DamageEvent evt)
    {
        if (IsLikelyStructureDamageEvent(evt))
        {
            return DamageParticipantCategory.Structure;
        }

        return ClassifyDamageParticipant(replay, evt.TargetId, evt.TargetName, evt.TargetIsBot, evt.UsesNonPlayerTarget);
    }

    private DamageParticipantCategory GetDamageEventInstigatorCategory(FortniteReplay replay, DamageEvent evt)
    {
        return ClassifyDamageParticipant(replay, evt.InstigatorId, evt.InstigatorName, evt.InstigatorIsBot, false);
    }

    private DamageParticipantCategory GetDamageEventParticipantCategory(FortniteReplay replay, DamageEvent evt, bool targetPerspective)
    {
        return targetPerspective
            ? GetDamageEventTargetCategory(replay, evt)
            : GetDamageEventInstigatorCategory(replay, evt);
    }

    private void ApplySettingsToUi()
    {
        var accentColor = _settings.GetAccentColor();
        var surfaceColor = _settings.GetSurfaceColor();
        var backgroundColor = _settings.GetBackgroundColor();
        var headerColor = ControlPaint.Light(backgroundColor);
        var alternateRowColor = ControlPaint.LightLight(surfaceColor);

        BackColor = backgroundColor;
        replayBrowserLayout.BackColor = backgroundColor;
        replayBrowserHeader.BackColor = headerColor;
        playerHeaderPanel.BackColor = headerColor;
        splitMain.BackColor = ControlPaint.Light(accentColor);
        splitContent.BackColor = ControlPaint.Light(accentColor);

        if (_menuStrip is not null)
        {
            _menuStrip.BackColor = surfaceColor;
            _menuStrip.ForeColor = Color.FromArgb(32, 37, 43);
        }

        foreach (var groupBox in new Control[] { grpGameStats, grpKillFeed, grpCombatEvents, grpPlayers, grpPlayerOverview, grpPlayerCombatLog, grpPlayerVictims })
        {
            groupBox.BackColor = backgroundColor;
            groupBox.ForeColor = Color.FromArgb(32, 37, 43);
        }

        if (_grpPlayerDamageLog is not null)
        {
            _grpPlayerDamageLog.BackColor = backgroundColor;
            _grpPlayerDamageLog.ForeColor = Color.FromArgb(32, 37, 43);
        }

        foreach (var grid in GetThemedGrids())
        {
            grid.BackgroundColor = surfaceColor;
            grid.DefaultCellStyle.SelectionBackColor = accentColor;
            grid.AlternatingRowsDefaultCellStyle.BackColor = alternateRowColor;
            grid.ColumnHeadersDefaultCellStyle.BackColor = ControlPaint.Light(surfaceColor);
        }
    }

    private IEnumerable<DataGridView> GetThemedGrids()
    {
        yield return dgvReplayBrowser;
        yield return dgvGameStats;
        yield return dgvKillFeed;
        yield return dgvCombatEvents;
        yield return dgvPlayers;
        yield return dgvPlayerOverview;
        yield return dgvPlayerCombatLog;
        yield return dgvPlayerVictims;
        if (_dgvPlayerDamageLog is not null)
        {
            yield return _dgvPlayerDamageLog;
        }
    }

    private static string ShortenTabTitle(string fileName)
    {
        if (TryParseReplayTimestampFromFileName(fileName, out DateTime timestamp))
        {
            return timestamp.ToString("M/d h:mm tt", CultureInfo.CurrentCulture);
        }

        const int maxLength = 20;
        return fileName.Length <= maxLength ? fileName : fileName[..17] + "...";
    }
    private static DateTime GetReplayTimestamp(DateTime replayTimestamp, string filePath, string? fileName = null)
    {
        if (TryParseReplayTimestampFromFileName(fileName ?? Path.GetFileName(filePath), out DateTime parsedFromFileName))
        {
            return parsedFromFileName;
        }

        if (replayTimestamp == default)
        {
            return File.GetLastWriteTime(filePath);
        }

        var localOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
        var assumedUtc = replayTimestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(replayTimestamp, DateTimeKind.Utc)
            : replayTimestamp.ToUniversalTime();

        return assumedUtc + localOffset;
    }

    private static bool HasReplaySummary(FortniteReplay replay)
    {
        if (replay.Info.LengthInMs > 0)
        {
            return true;
        }

        var replayOwner = GetReplayOwner(replay);
        return replayOwner?.Kills is not null || replayOwner?.Placement is not null || replay.TeamStats?.Position is not null;
    }

    private void CaptureReplayBrowserColumnWidths()
    {
        foreach (DataGridViewColumn column in dgvReplayBrowser.Columns)
        {
            _replayBrowserColumnWidths[column.Name] = column.Width;
        }
    }

    private void RestoreReplayBrowserColumnWidths()
    {
        foreach (DataGridViewColumn column in dgvReplayBrowser.Columns)
        {
            if (_replayBrowserColumnWidths.TryGetValue(column.Name, out var width))
            {
                column.Width = width;
            }
        }
    }


    private static void AddPanelTab(TabControl tabControl, string title, Control content)
    {
        var page = new TabPage(title)
        {
            Padding = new Padding(0, 8, 0, 0)
        };
        content.Parent?.Controls.Remove(content);
        page.Controls.Add(content);
        content.Dock = DockStyle.Fill;
        tabControl.TabPages.Add(page);
    }

    private bool InvolvesReplayOwnerTeam(FortniteReplay replay, KillFeedEntry entry)
    {
        var teamIndex = GetReplayOwner(replay)?.TeamIndex;
        if (!teamIndex.HasValue)
        {
            return true;
        }

        var actorReference = ResolveKillFeedActorReference(replay, entry);
        var target = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
        return actorReference.Player?.TeamIndex == teamIndex || target?.TeamIndex == teamIndex;
    }
    private bool InvolvesReplayOwnerTeam(FortniteReplay replay, DamageEvent evt)
    {
        var teamIndex = GetReplayOwner(replay)?.TeamIndex;
        if (!teamIndex.HasValue)
        {
            return true;
        }

        var attacker = FindPlayer(replay, evt.InstigatorId, evt.InstigatorName);
        var target = FindPlayer(replay, evt.TargetId, evt.TargetName);
        return attacker?.TeamIndex == teamIndex || target?.TeamIndex == teamIndex;
    }

    private DamageTotals SummarizeDamage(FortniteReplay replay, IEnumerable<DamageEvent> events, bool targetPerspective)
    {
        var totals = new DamageTotals();
        foreach (var evt in events)
        {
            var amount = evt.Magnitude.GetValueOrDefault();
            if (amount <= 0)
            {
                continue;
            }

            var category = GetDamageEventParticipantCategory(replay, evt, targetPerspective);

            switch (category)
            {
                case DamageParticipantCategory.Player:
                    totals.Players += amount;
                    break;
                case DamageParticipantCategory.Bot:
                    totals.Bots += amount;
                    break;
                default:
                    totals.Structures += amount;
                    break;
            }
        }

        return totals;
    }

    private DamageParticipantCategory ClassifyDamageParticipant(FortniteReplay replay, int? numericId, string? lookupKey, bool isBot, bool preferNonPlayerTarget = false)
    {
        if (preferNonPlayerTarget)
        {
            return DamageParticipantCategory.Structure;
        }

        var player = FindPlayer(replay, numericId, lookupKey);
        if (player is not null)
        {
            return player.IsBot ? DamageParticipantCategory.Bot : DamageParticipantCategory.Player;
        }

        if (isBot)
        {
            return DamageParticipantCategory.Bot;
        }

        if (!string.IsNullOrWhiteSpace(lookupKey))
        {
            return DamageParticipantCategory.Player;
        }

        return DamageParticipantCategory.Structure;
    }

    private static string FormatDamageTotal(float value) => value <= 0 ? "-" : value.ToString("0.#", CultureInfo.CurrentCulture);
    private static string ResolvePlayerName(PlayerData? player, int? numericId, string? fallback = null)
    {
        if (player is not null)
        {
            var preferred = player.PlayerName ?? player.PlayerNameCustomOverride ?? player.StreamerModeName;
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                return preferred;
            }

            numericId ??= player.Id;
            fallback ??= player.PlayerId;
        }

        if (numericId.HasValue)
        {
            return numericId.Value < 1000 ? $"Anonymous {numericId.Value:000}" : $"Anonymous {numericId.Value}";
        }

        return ShortenIdentifier(fallback);
    }

    private static string ShortenIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        return value.Length > 18 && !value.Contains(' ', StringComparison.Ordinal) ? value[..8] + "..." + value[^4..] : value;
    }

    private static string GetKillFeedEventText(KillFeedEntry entry) => entry.IsRevived ? "Revived" : entry.IsDowned ? "Downed" : "Eliminated";

    private string FormatKillFeedReason(FortniteReplay replay, KillFeedEntry entry)
    {
        var tags = entry.DeathTags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        var environmentalReason = InferEnvironmentalReason(entry.DeathCause, tags);
        if (!string.IsNullOrWhiteSpace(environmentalReason))
        {
            return environmentalReason;
        }

        var weaponLabel = InferWeaponLabelFromTags(tags);
        if (string.IsNullOrWhiteSpace(weaponLabel))
        {
            weaponLabel = GetWeaponLabelFromMatchingElimination(replay, entry);
        }

        if (!string.IsNullOrWhiteSpace(weaponLabel))
        {
            return HasHeadshotTag(tags) ? $"Headshot ({weaponLabel})" : weaponLabel;
        }

        if (entry.DeathCause.HasValue)
        {
            return entry.DeathCause.Value switch
            {
                50 => "Storm Damage",
                _ => $"Cause {entry.DeathCause.Value}"
            };
        }

        return "-";
    }

    private string? GetWeaponLabelFromMatchingElimination(FortniteReplay replay, KillFeedEntry entry)
    {
        var targetPlayer = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
        var actorReference = ResolveKillFeedActorReference(replay, entry);
        var targetLookupKey = targetPlayer?.PlayerId ?? entry.PlayerName;
        var actorLookupKey = actorReference.Player?.PlayerId ?? actorReference.LookupKey;
        var targetId = targetLookupKey ?? entry.PlayerId?.ToString(CultureInfo.InvariantCulture);
        var actorId = actorLookupKey ?? actorReference.NumericId?.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return null;
        }

        var desiredKnocked = entry.IsDowned;
        var entryTime = GetKillFeedTime(replay, entry);
        var matchingElimination = replay.Eliminations
            .Where(elim => string.Equals(elim.Eliminated, targetId, StringComparison.OrdinalIgnoreCase))
            .Where(elim => string.IsNullOrWhiteSpace(actorId) || string.Equals(elim.Eliminator, actorId, StringComparison.OrdinalIgnoreCase))
            .Where(elim => desiredKnocked == elim.Knocked || !entry.IsDowned)
            .Select(elim => new { Elimination = elim, Delta = Math.Abs(ParseEventClock(elim.Time) - entryTime) })
            .OrderBy(match => match.Delta)
            .FirstOrDefault(match => match.Delta <= 4.0d);

        return matchingElimination is null ? null : MapGunType(matchingElimination.Elimination.GunType);
    }

    private static string? InferEnvironmentalReason(int? deathCause, IEnumerable<string> tags)
    {
        var tagList = tags.ToList();
        if (deathCause == 50
            || tagList.Any(tag => tag.Contains("OutsideSafeZone", StringComparison.OrdinalIgnoreCase))
            || tagList.Any(tag => tag.Contains("Storm", StringComparison.OrdinalIgnoreCase)))
        {
            return "Storm Damage";
        }

        if (tagList.Any(tag => tag.Contains("Gameplay.Damage.Fall", StringComparison.OrdinalIgnoreCase))
            || tagList.Any(tag => tag.Contains("FallDamage", StringComparison.OrdinalIgnoreCase))
            || tagList.Any(tag => tag.Contains("Gameplay.Damage.Environment.Fall", StringComparison.OrdinalIgnoreCase)))
        {
            return "Fall Damage";
        }

        return null;
    }

    private static bool HasHeadshotTag(IEnumerable<string> tags)
    {
        return tags.Any(tag => tag.Contains("headshot", StringComparison.OrdinalIgnoreCase));
    }

    private static string? InferWeaponLabelFromTags(IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Contains("weapon.ranged.smg", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Item.Weapon.Ranged.SMG", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Bacchus.Shoot.SMG", StringComparison.OrdinalIgnoreCase))
            {
                return "SMG";
            }

            if (tag.Contains("weapon.ranged.shotgun", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Item.Weapon.Ranged.Shotgun", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Bacchus.Shoot.Shotgun", StringComparison.OrdinalIgnoreCase))
            {
                return "Shotgun";
            }

            if (tag.Contains("weapon.ranged.assault", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Item.Weapon.Ranged.Assault", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Item.Weapon.Ranged.AR", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Bacchus.Shoot.AR", StringComparison.OrdinalIgnoreCase))
            {
                return "Assault Rifle";
            }

            if (tag.Contains("weapon.ranged.pistol", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Item.Weapon.Ranged.Pistol", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Bacchus.Shoot.Pistol", StringComparison.OrdinalIgnoreCase))
            {
                return "Pistol";
            }

            if (tag.Contains("weapon.ranged.sniper", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Item.Weapon.Ranged.Sniper", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("Bacchus.Shoot.Sniper", StringComparison.OrdinalIgnoreCase)
                || tag.Contains("weapon.ranged.dmr", StringComparison.OrdinalIgnoreCase))
            {
                return "Sniper";
            }
        }

        return null;
    }

    private static string NormalizeWeaponTypeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        return value.Trim() switch
        {
            "Rifle" => "Assault Rifle",
            "Marksman" => "Sniper",
            "Other" => "Unknown",
            var label => label
        };
    }

    private static string? NormalizeWeaponDisplayLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        if (cleaned.Contains('/'))
        {
            cleaned = cleaned[(cleaned.LastIndexOf('/') + 1)..];
        }

        cleaned = cleaned.Replace('_', ' ');
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "(?<=[a-z])(?=[A-Z])", " ");
        cleaned = cleaned.Replace("  ", " ").Trim();

        if (cleaned.StartsWith("Fort Weapon", StringComparison.OrdinalIgnoreCase)
            || cleaned.StartsWith("Base Weapon", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return cleaned;
    }

    private static string? MapGunType(byte gunType)
    {
        return gunType switch
        {
            2 => "Pistol",
            3 => "Shotgun",
            4 => "Assault Rifle",
            5 => "SMG",
            _ => null
        };
    }

    private static double ParseEventClock(string? value)
    {
        return TimeSpan.TryParseExact(value, @"mm\:ss", CultureInfo.InvariantCulture, out var parsed)
            ? parsed.TotalSeconds
            : 0d;
    }

    private static string ShortGameplayTag(string value)
    {
        var tag = value.Trim();
        var dotIndex = tag.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex < tag.Length - 1)
        {
            tag = tag[(dotIndex + 1)..];
        }

        tag = tag.Replace('_', ' ');
        return string.IsNullOrWhiteSpace(tag) ? "-" : tag;
    }

    private static double GetKillFeedTime(KillFeedEntry entry) => entry.ReplicatedWorldTimeSecondsDouble ?? entry.ReplicatedWorldTimeSeconds ?? 0;

    private double GetKillFeedTime(FortniteReplay replay, KillFeedEntry entry)
    {
        return NormalizeReplayEventTime(replay, GetKillFeedTime(entry));
    }

    private static double GetDamageTime(DamageEvent evt) => evt.ReplicatedWorldTimeSecondsDouble ?? evt.ReplicatedWorldTimeSeconds ?? 0;

    private double GetDamageTime(FortniteReplay replay, DamageEvent evt)
    {
        return NormalizeReplayEventTime(replay, GetDamageTime(evt));
    }

    private double NormalizeReplayEventTime(FortniteReplay replay, double rawTime)
    {
        if (rawTime <= 0)
        {
            return 0;
        }

        var offset = GetReplayLookupCache(replay).EventTimeOffset;
        return Math.Max(0, rawTime - offset);
    }

    private static string FormatNullable(object? value)
    {
        return value switch
        {
            null => "-",
            uint number => number.ToString(CultureInfo.CurrentCulture),
            int number => number.ToString(CultureInfo.CurrentCulture),
            long number => number.ToString(CultureInfo.CurrentCulture),
            _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? "-"
        };
    }

    private static string FormatBool(bool? value) => value switch { true => "Yes", false => "No", _ => "-" };

    private static string FormatMatchClock(double seconds) => seconds <= 0 ? "-" : TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "-";
        }

        return duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatDistance(float? rawDistance)
    {
        if (!rawDistance.HasValue)
        {
            return "-";
        }

        return $"{rawDistance.Value / 100f:0.00} m";
    }

    private static string FormatVector(object? vector)
    {
        if (vector is null)
        {
            return "-";
        }

        return vector.ToString() ?? "-";
    }

    private sealed class DamageTotals
    {
        public float Players { get; set; }
        public float Bots { get; set; }
        public float Npcs { get; set; }
        public float Structures { get; set; }
    }

    private sealed class ReplayLookupCache
    {
        public Dictionary<int, PlayerData> PlayersById { get; } = new();
        public Dictionary<string, PlayerData> PlayersByLookupKey { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<KillFeedEntry, (PlayerData? Player, int? NumericId, string? LookupKey)> ResolvedActorCache { get; } = new();
        public List<KillFeedEntry> KillFeedByDescendingTime { get; } = [];
        public List<KillFeedWeaponCue> KillFeedWeaponCues { get; } = [];
        public double EventTimeOffset { get; private set; }

        public static ReplayLookupCache Create(FortniteReplay replay)
        {
            var cache = new ReplayLookupCache();

            if (replay.PlayerData is not null)
            {
                foreach (var player in replay.PlayerData)
                {
                    if (player.Id.HasValue)
                    {
                        cache.PlayersById[player.Id.Value] = player;
                    }

                    if (!string.IsNullOrWhiteSpace(player.PlayerId))
                    {
                        cache.PlayersByLookupKey[player.PlayerId] = player;
                    }
                }
            }

            cache.KillFeedByDescendingTime.AddRange(replay.KillFeed.OrderByDescending(GetKillFeedTime));
            cache.EventTimeOffset = GetInitialEventTime(replay);
            return cache;
        }

        private static double GetInitialEventTime(FortniteReplay replay)
        {
            var minKillFeed = replay.KillFeed
                .Select(GetKillFeedTime)
                .Where(time => time > 0)
                .DefaultIfEmpty(double.MaxValue)
                .Min();

            var minDamage = replay.DamageEvents
                .Select(GetDamageTime)
                .Where(time => time > 0)
                .DefaultIfEmpty(double.MaxValue)
                .Min();

            var minTime = Math.Min(minKillFeed, minDamage);
            return minTime == double.MaxValue ? 0 : minTime;
        }
    }

    private sealed class KillFeedWeaponCue
    {
        public int? ActorId { get; init; }
        public string? ActorLookupKey { get; init; }
        public int? TargetId { get; init; }
        public string? TargetLookupKey { get; init; }
        public double TimeValue { get; init; }
        public string WeaponLabel { get; init; } = string.Empty;
    }

    private sealed record ReplayLoadResult(FortniteReplay? Replay, Exception? Exception);
}












