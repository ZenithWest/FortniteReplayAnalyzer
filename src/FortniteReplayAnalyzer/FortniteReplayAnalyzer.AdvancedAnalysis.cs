using System.Globalization;
using FortniteReplayReader;
using FortniteReplayReader.Models;
using Unreal.Core.Models.Enums;

namespace FortniteReplayAnalyzer;

public partial class FortniteReplayAnalyzer
{
    private TabControl? _mainTabs;
    private ComboBox? _cmbWeaponRange;
    private DateTimePicker? _dtWeaponFrom;
    private DateTimePicker? _dtWeaponTo;
    private Label? _lblWeaponStatsStatus;
    private DataGridView? _dgvWeaponStats;
    private Button? _btnWeaponStatsStop;
    private ProgressBar? _weaponStatsProgressBar;
    private CheckBox? _chkWeaponIncludeBots;
    private CancellationTokenSource? _weaponStatsLoadCts;
    private ComboBox? _cmbOverallRange;
    private DateTimePicker? _dtOverallFrom;
    private DateTimePicker? _dtOverallTo;
    private Label? _lblOverallStatsStatus;
    private DataGridView? _dgvOverallStats;
    private Button? _btnOverallStatsStop;
    private CancellationTokenSource? _overallStatsLoadCts;
    private Panel? _damageTimelinePanel;
    private Panel? _overallDamageTrendPanel;
    private Panel? _overallKillsTrendPanel;
    private Panel? _weaponDamageSharePanel;
    private Panel? _weaponKillSharePanel;
    private CheckBox? _chkTimelinePlayers;
    private CheckBox? _chkTimelineBots;
    private CheckBox? _chkTimelineTeam;
    private CheckBox? _chkTimelineCumulative;
    private CheckBox? _chkOverallKillsIncludeBots;
    private List<(string Name, List<DamageTimelinePoint> Points)> _timelineSeries = [];
    private List<MatchTrendRow> _overallTrendRows = [];
    private List<(RectangleF Bounds, string Text)> _timelineHitRegions = [];
    private List<(RectangleF Bounds, string Text)> _overallDamageHitRegions = [];
    private List<(RectangleF Bounds, string Text)> _overallKillsHitRegions = [];
    private List<(RectangleF Bounds, string Text)> _weaponDamagePieHitRegions = [];
    private List<(RectangleF Bounds, string Text)> _weaponKillPieHitRegions = [];
    private List<WeaponStatsRow> _lastWeaponStatsRows = [];
    private readonly ToolTip _graphToolTip = new();
    private RectangleF _timelineChartBounds;
    private RectangleF _overallDamageChartBounds;
    private RectangleF _overallKillsChartBounds;

    private sealed class ReplayCombatSnapshot
    {
        public required FortniteReplay Replay { get; init; }
        public required PlayerData Owner { get; init; }
        public int Kills { get; set; }
        public float PlayerDamage { get; set; }
        public float BotDamage { get; set; }
        public float StructureDamage { get; set; }
        public int IncludedCombatEventCount { get; set; }
        public int IncludedCriticalCount { get; set; }
        public float IncludedCombatDamage { get; set; }
        public float EarlyFightDamage { get; set; }
        public float MidFightDamage { get; set; }
        public float LateFightDamage { get; set; }
    }

    private void InitializeAdvancedAnalysisUi()
    {
        _mainTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            HotTrack = true,
            Multiline = false,
            Padding = new Point(16, 4)
        };

        mainContentHost.Controls.Clear();
        mainContentHost.Controls.Add(_mainTabs);

        AddPanelTab(_mainTabs, "Replay Analysis", splitMain);
        AddPanelTab(_mainTabs, "Weapon Stats", BuildWeaponStatsPage());
        AddPanelTab(_mainTabs, "Overall Statistics", BuildOverallStatsPage());

        BuildGameStatsChartLayout();
    }

    private Control BuildWeaponStatsPage()
    {
        var page = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 5
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 260F));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var filters = CreateFilterFlowPanel();
        filters.AutoScroll = false;

        _cmbWeaponRange = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        _cmbWeaponRange.Items.AddRange(["Current Match", "All-time", "Last 7 days", "Last 30 days", "Custom"]);
        _cmbWeaponRange.SelectedIndex = 1;
        _cmbWeaponRange.SelectedIndexChanged += async (_, _) => await RefreshWeaponStatsPageAsync();

        _dtWeaponFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Enabled = false, Width = 120 };
        _dtWeaponTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Enabled = false, Width = 120 };
        _dtWeaponFrom.ValueChanged += async (_, _) => await RefreshWeaponStatsPageAsync();
        _dtWeaponTo.ValueChanged += async (_, _) => await RefreshWeaponStatsPageAsync();

        var btnRefresh = new Button { Text = "Refresh", AutoSize = true };
        btnRefresh.Click += async (_, _) => await RefreshWeaponStatsPageAsync();
        _btnWeaponStatsStop = new Button { Text = "Stop", AutoSize = true, Enabled = false };
        _btnWeaponStatsStop.Click += (_, _) => _weaponStatsLoadCts?.Cancel();

        filters.Controls.Add(new Label { AutoSize = true, Margin = new Padding(0, 6, 8, 0), Text = "Range" });
        filters.Controls.Add(_cmbWeaponRange);
        filters.Controls.Add(new Label { AutoSize = true, Margin = new Padding(8, 6, 8, 0), Text = "From" });
        filters.Controls.Add(_dtWeaponFrom);
        filters.Controls.Add(new Label { AutoSize = true, Margin = new Padding(8, 6, 8, 0), Text = "To" });
        filters.Controls.Add(_dtWeaponTo);
        filters.Controls.Add(btnRefresh);
        filters.Controls.Add(_btnWeaponStatsStop);
        _chkWeaponIncludeBots = CreateFilterCheckBox("Include bot damage", async (_, _) => await RefreshWeaponStatsPageAsync(), true);
        filters.Controls.Add(_chkWeaponIncludeBots);

        _lblWeaponStatsStatus = new Label
        {
            AutoSize = true,
            Text = "Weapon stats load on demand for the selected date range."
        };

        _weaponStatsProgressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 18,
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Style = ProgressBarStyle.Continuous,
            Visible = false
        };

        var pieChartsLayout = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };
        pieChartsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        pieChartsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        pieChartsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _weaponDamageSharePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _weaponKillSharePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _weaponDamageSharePanel.Paint += (_, e) => PaintWeaponSharePieChart(
            e.Graphics,
            _weaponDamageSharePanel.ClientRectangle,
            "Avg Damage Per Match Share",
            _lastWeaponStatsRows.Where(row => row.AvgDamagePerMatch > 0F)
                .Select(row => (row.WeaponName, (double)row.AvgDamagePerMatch))
                .ToList(),
            _weaponDamagePieHitRegions);
        _weaponKillSharePanel.Paint += (_, e) => PaintWeaponSharePieChart(
            e.Graphics,
            _weaponKillSharePanel.ClientRectangle,
            "Avg Kills/Downs Per Match Share",
            _lastWeaponStatsRows.Where(row => row.AvgKillOrDownsPerMatch > 0F)
                .Select(row => (row.WeaponName, (double)row.AvgKillOrDownsPerMatch))
                .ToList(),
            _weaponKillPieHitRegions);
        _weaponDamageSharePanel.MouseMove += (_, e) => UpdateGraphToolTip(_weaponDamageSharePanel, _weaponDamagePieHitRegions, e.Location);
        _weaponKillSharePanel.MouseMove += (_, e) => UpdateGraphToolTip(_weaponKillSharePanel, _weaponKillPieHitRegions, e.Location);
        _weaponDamageSharePanel.MouseClick += (_, _) => OpenWeaponShareExplorer(true);
        _weaponKillSharePanel.MouseClick += (_, _) => OpenWeaponShareExplorer(false);
        pieChartsLayout.Controls.Add(_weaponDamageSharePanel, 0, 0);
        pieChartsLayout.Controls.Add(_weaponKillSharePanel, 1, 0);

        _dgvWeaponStats = new DataGridView { Name = "dgvWeaponStats" };
        ConfigureReadOnlyGrid(_dgvWeaponStats, fullRowSelect: true);
        _dgvWeaponStats.AutoGenerateColumns = false;
        _dgvWeaponStats.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _dgvWeaponStats.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
        _dgvWeaponStats.ColumnHeadersHeight = 46;
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.WeaponType), HeaderText = "Weapon Type", DataPropertyName = nameof(WeaponStatsRow.WeaponType), FillWeight = 110 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.WeaponName), HeaderText = "Weapon", DataPropertyName = nameof(WeaponStatsRow.WeaponName), FillWeight = 180 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.MatchesUsed), HeaderText = "Matches", DataPropertyName = nameof(WeaponStatsRow.MatchesUsed), FillWeight = 70 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.KillOrDownCount), HeaderText = "Kills/\r\nDowns", DataPropertyName = nameof(WeaponStatsRow.KillOrDownCount), FillWeight = 85 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.EliminationCount), HeaderText = "Eliminations", DataPropertyName = nameof(WeaponStatsRow.EliminationCount), FillWeight = 78 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.Hits), HeaderText = "Hits", DataPropertyName = nameof(WeaponStatsRow.Hits), FillWeight = 65 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.HitsToPlayers), HeaderText = "Hits To\r\nPlayers", DataPropertyName = nameof(WeaponStatsRow.HitsToPlayers), FillWeight = 74 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.HitsToBots), HeaderText = "Hits To\r\nBots", DataPropertyName = nameof(WeaponStatsRow.HitsToBots), FillWeight = 72 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.HitsToNpcs), HeaderText = "Hits To\r\nNPCs", DataPropertyName = nameof(WeaponStatsRow.HitsToNpcs), FillWeight = 72 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.HitsToStructures), HeaderText = "Hits To\r\nStructures", DataPropertyName = nameof(WeaponStatsRow.HitsToStructures), FillWeight = 88 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.CriticalHits), HeaderText = "Critical\r\nHits", DataPropertyName = nameof(WeaponStatsRow.CriticalHits), FillWeight = 76 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.ShieldHits), HeaderText = "Shield\r\nHits", DataPropertyName = nameof(WeaponStatsRow.ShieldHits), FillWeight = 78 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.FatalHits), HeaderText = "Fatal", DataPropertyName = nameof(WeaponStatsRow.FatalHits), FillWeight = 62 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.TotalDamage), HeaderText = "Total Damage", DataPropertyName = nameof(WeaponStatsRow.TotalDamage), FillWeight = 90, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.DamageToPlayers), HeaderText = "Damage To\r\nPlayers", DataPropertyName = nameof(WeaponStatsRow.DamageToPlayers), FillWeight = 88, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.DamageToBots), HeaderText = "Damage To\r\nBots", DataPropertyName = nameof(WeaponStatsRow.DamageToBots), FillWeight = 84, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.DamageToNpcs), HeaderText = "Damage To\r\nNPCs", DataPropertyName = nameof(WeaponStatsRow.DamageToNpcs), FillWeight = 86, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.DamageToStructures), HeaderText = "Damage To\r\nStructures", DataPropertyName = nameof(WeaponStatsRow.DamageToStructures), FillWeight = 94, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.AvgDamage), HeaderText = "Average\r\nHit", DataPropertyName = nameof(WeaponStatsRow.AvgDamage), FillWeight = 84, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.AvgDamagePerMatch), HeaderText = "Average Damage\r\nPer Match", DataPropertyName = nameof(WeaponStatsRow.AvgDamagePerMatch), FillWeight = 102, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.AvgHitsPerMatch), HeaderText = "Average Hits\r\nPer Match", DataPropertyName = nameof(WeaponStatsRow.AvgHitsPerMatch), FillWeight = 102, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.AvgKillOrDownsPerMatch), HeaderText = "Average Kills/\r\nDowns Per Match", DataPropertyName = nameof(WeaponStatsRow.AvgKillOrDownsPerMatch), FillWeight = 112, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.CriticalRate), HeaderText = "Critical\r\nRate", DataPropertyName = nameof(WeaponStatsRow.CriticalRate), FillWeight = 78, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#'%'"} });
        _dgvWeaponStats.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        page.Controls.Add(filters, 0, 0);
        page.Controls.Add(_lblWeaponStatsStatus, 0, 1);
        page.Controls.Add(_weaponStatsProgressBar, 0, 2);
        page.Controls.Add(pieChartsLayout, 0, 3);
        page.Controls.Add(_dgvWeaponStats, 0, 4);
        return page;
    }

    private Control BuildOverallStatsPage()
    {
        var page = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var filters = CreateFilterFlowPanel();
        filters.AutoScroll = false;

        _cmbOverallRange = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        _cmbOverallRange.Items.AddRange(["All-time", "Last 7 days", "Last 30 days", "Custom"]);
        _cmbOverallRange.SelectedIndex = 0;
        _cmbOverallRange.SelectedIndexChanged += async (_, _) => await RefreshOverallStatsPageAsync();

        _dtOverallFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Enabled = false, Width = 120 };
        _dtOverallTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Enabled = false, Width = 120 };
        _dtOverallFrom.ValueChanged += async (_, _) => await RefreshOverallStatsPageAsync();
        _dtOverallTo.ValueChanged += async (_, _) => await RefreshOverallStatsPageAsync();

        var btnRefresh = new Button { Text = "Refresh", AutoSize = true };
        btnRefresh.Click += async (_, _) => await RefreshOverallStatsPageAsync();
        _btnOverallStatsStop = new Button { Text = "Stop", AutoSize = true, Enabled = false };
        _btnOverallStatsStop.Click += (_, _) => _overallStatsLoadCts?.Cancel();

        filters.Controls.Add(new Label { AutoSize = true, Margin = new Padding(0, 6, 8, 0), Text = "Range" });
        filters.Controls.Add(_cmbOverallRange);
        filters.Controls.Add(new Label { AutoSize = true, Margin = new Padding(8, 6, 8, 0), Text = "From" });
        filters.Controls.Add(_dtOverallFrom);
        filters.Controls.Add(new Label { AutoSize = true, Margin = new Padding(8, 6, 8, 0), Text = "To" });
        filters.Controls.Add(_dtOverallTo);
        filters.Controls.Add(btnRefresh);
        filters.Controls.Add(_btnOverallStatsStop);
        _chkOverallKillsIncludeBots = CreateFilterCheckBox("Include bot kills", async (_, _) => await RefreshOverallStatsPageAsync(), true);
        filters.Controls.Add(_chkOverallKillsIncludeBots);

        _lblOverallStatsStatus = new Label
        {
            AutoSize = true,
            Text = "Overall stats are generated from replay-owner data in the selected range."
        };

        _dgvOverallStats = new DataGridView { Name = "dgvOverallStats" };
        ConfigureReadOnlyGrid(_dgvOverallStats, fullRowSelect: false);
        _dgvOverallStats.AutoGenerateColumns = false;
        BuildGameStatsColumns(_dgvOverallStats);

        _overallDamageTrendPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };
        _overallDamageTrendPanel.Paint += (_, e) => PaintOverallTrendChart(
            e.Graphics,
            _overallDamageTrendPanel.ClientRectangle,
            "Damage Per Match",
            _overallTrendRows.Select(row => ((double)row.DamageToPlayersAndBots, row.Label)).ToList(),
            Color.FromArgb(52, 123, 220),
            _overallDamageHitRegions,
            chartBounds => _overallDamageChartBounds = chartBounds);
        _overallDamageTrendPanel.MouseMove += (_, e) => UpdateGraphToolTip(_overallDamageTrendPanel, _overallDamageHitRegions, e.Location);
        _overallDamageTrendPanel.MouseClick += (_, _) => OpenOverallTrendExplorer(true);

        _overallKillsTrendPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };
        _overallKillsTrendPanel.Paint += (_, e) => PaintOverallTrendChart(
            e.Graphics,
            _overallKillsTrendPanel.ClientRectangle,
            "Kills Per Match",
            _overallTrendRows.Select(row => ((double)row.Kills, row.Label)).ToList(),
            Color.FromArgb(244, 124, 32),
            _overallKillsHitRegions,
            chartBounds => _overallKillsChartBounds = chartBounds);
        _overallKillsTrendPanel.MouseMove += (_, e) => UpdateGraphToolTip(_overallKillsTrendPanel, _overallKillsHitRegions, e.Location);
        _overallKillsTrendPanel.MouseClick += (_, _) => OpenOverallTrendExplorer(false);

        var content = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0),
            RowCount = 1
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var graphsHost = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            RowCount = 2
        };
        graphsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        graphsHost.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        graphsHost.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        graphsHost.Controls.Add(_overallDamageTrendPanel, 0, 0);
        graphsHost.Controls.Add(_overallKillsTrendPanel, 0, 1);

        content.Controls.Add(graphsHost, 0, 0);
        content.Controls.Add(_dgvOverallStats, 1, 0);

        page.Controls.Add(filters, 0, 0);
        page.Controls.Add(_lblOverallStatsStatus, 0, 1);
        page.Controls.Add(content, 0, 2);
        return page;
    }

    private void BuildGameStatsChartLayout()
    {
        var filterPanel = CreateFilterFlowPanel();
        filterPanel.AutoScroll = false;
        _chkTimelinePlayers = CreateFilterCheckBox("Players", (_, _) => UpdateDamageTimelineChart(), true);
        _chkTimelineBots = CreateFilterCheckBox("Bots", (_, _) => UpdateDamageTimelineChart(), true);
        _chkTimelineTeam = CreateFilterCheckBox("Show team", (_, _) => UpdateDamageTimelineChart(), true);
        _chkTimelineCumulative = CreateFilterCheckBox("Cumulative", (_, _) => UpdateDamageTimelineChart(), true);
        filterPanel.Controls.Add(_chkTimelinePlayers);
        filterPanel.Controls.Add(_chkTimelineBots);
        filterPanel.Controls.Add(_chkTimelineTeam);
        filterPanel.Controls.Add(_chkTimelineCumulative);

        _damageTimelinePanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 240,
            BackColor = Color.White
        };
        _damageTimelinePanel.Paint += (_, e) => PaintDamageTimeline(e.Graphics, _damageTimelinePanel.ClientRectangle);
        _damageTimelinePanel.MouseMove += (_, e) => UpdateGraphToolTip(_damageTimelinePanel, _timelineHitRegions, e.Location);
        _damageTimelinePanel.MouseClick += (_, _) => OpenDamageTimelineExplorer();

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 240F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        grpGameStats.Controls.Remove(dgvGameStats);
        layout.Controls.Add(filterPanel, 0, 0);
        layout.Controls.Add(_damageTimelinePanel, 0, 1);
        layout.Controls.Add(dgvGameStats, 0, 2);
        grpGameStats.Controls.Add(layout);
    }

    private async Task RefreshWeaponStatsPageAsync()
    {
        if (_dgvWeaponStats is null || _lblWeaponStatsStatus is null || _weaponStatsProgressBar is null)
        {
            return;
        }

        ToggleAggregateDatePickers(GetWeaponSelectedRange(), _dtWeaponFrom, _dtWeaponTo);
        _weaponStatsLoadCts?.Cancel();
        _weaponStatsLoadCts = new CancellationTokenSource();
        var cancellationToken = _weaponStatsLoadCts.Token;
        _lblWeaponStatsStatus.Text = "Building weapon stats from already-loaded replays...";
        if (_btnWeaponStatsStop is not null) _btnWeaponStatsStop.Enabled = true;
        _weaponStatsProgressBar.Visible = true;
        _weaponStatsProgressBar.Maximum = 1;
        _weaponStatsProgressBar.Value = 0;

        try
        {
            var rows = await GetAggregateReplayRowsAsync(GetWeaponSelectedRange(), _dtWeaponFrom, _dtWeaponTo, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                _lblWeaponStatsStatus.Text = "Weapon stats refresh stopped.";
                return;
            }

            _weaponStatsProgressBar.Maximum = Math.Max(1, rows.Count);
            var progress = new Progress<int>(value =>
            {
                _weaponStatsProgressBar.Value = Math.Max(_weaponStatsProgressBar.Minimum, Math.Min(_weaponStatsProgressBar.Maximum, value));
            });

            var weaponRows = await Task.Run(() => BuildWeaponStatsSummary(rows, _chkWeaponIncludeBots?.Checked ?? true, progress, cancellationToken));
            if (cancellationToken.IsCancellationRequested)
            {
                _lblWeaponStatsStatus.Text = "Weapon stats refresh stopped.";
                return;
            }

            _dgvWeaponStats.DataSource = weaponRows;
            _lastWeaponStatsRows = weaponRows;
            _weaponDamageSharePanel?.Invalidate();
            _weaponKillSharePanel?.Invalidate();
            _lblWeaponStatsStatus.Text = rows.Count == 0
                ? "No loaded replays in range."
                : $"Weapon stats built from {rows.Count} loaded replay(s).";
        }
        catch (OperationCanceledException)
        {
            _lastWeaponStatsRows = [];
            _weaponDamageSharePanel?.Invalidate();
            _weaponKillSharePanel?.Invalidate();
            _lblWeaponStatsStatus.Text = "Weapon stats refresh stopped.";
        }
        finally
        {
            if (_btnWeaponStatsStop is not null) _btnWeaponStatsStop.Enabled = false;
            _weaponStatsProgressBar.Visible = false;
        }
    }

    private async Task RefreshOverallStatsPageAsync()
    {
        if (_dgvOverallStats is null || _lblOverallStatsStatus is null)
        {
            return;
        }

        ToggleAggregateDatePickers(GetOverallSelectedRange(), _dtOverallFrom, _dtOverallTo);
        _overallStatsLoadCts?.Cancel();
        _overallStatsLoadCts = new CancellationTokenSource();
        var cancellationToken = _overallStatsLoadCts.Token;
        _lblOverallStatsStatus.Text = "Building overall statistics from already-loaded replays...";
        if (_btnOverallStatsStop is not null) _btnOverallStatsStop.Enabled = true;

        try
        {
            var rows = await GetAggregateReplayRowsAsync(GetOverallSelectedRange(), _dtOverallFrom, _dtOverallTo, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                _lblOverallStatsStatus.Text = "Overall statistics refresh stopped.";
                _overallTrendRows = [];
                _overallDamageTrendPanel?.Invalidate();
                _overallKillsTrendPanel?.Invalidate();
                return;
            }

            var includeBotKills = _chkOverallKillsIncludeBots?.Checked ?? true;
            var detailRows = await Task.Run(() =>
            {
                return BuildOverallStatisticsRows(rows, includeBotKills).ToList();
            });
            if (cancellationToken.IsCancellationRequested)
            {
                _lblOverallStatsStatus.Text = "Overall statistics refresh stopped.";
                _overallTrendRows = [];
                _overallDamageTrendPanel?.Invalidate();
                _overallKillsTrendPanel?.Invalidate();
                return;
            }

            _dgvOverallStats.DataSource = detailRows;
            _overallTrendRows = BuildOverallTrendRows(rows, includeBotKills).ToList();
            _overallDamageTrendPanel?.Invalidate();
            _overallKillsTrendPanel?.Invalidate();
            _lblOverallStatsStatus.Text = rows.Count == 0
                ? "No loaded replays in range."
                : $"Overall statistics built from {rows.Count} loaded replay(s).";
        }
        catch (OperationCanceledException)
        {
            _lblOverallStatsStatus.Text = "Overall statistics refresh stopped.";
            _overallTrendRows = [];
            _overallDamageTrendPanel?.Invalidate();
            _overallKillsTrendPanel?.Invalidate();
        }
        finally
        {
            if (_btnOverallStatsStop is not null) _btnOverallStatsStop.Enabled = false;
        }
    }

    private Task<List<ReplayBrowserRow>> GetAggregateReplayRowsAsync(AggregateRangeOption range, DateTimePicker? fromPicker, DateTimePicker? toPicker, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new List<ReplayBrowserRow>());
        }

        if (range == AggregateRangeOption.CurrentMatch)
        {
            if (_selectedReplayRow?.Replay is not null)
            {
                return Task.FromResult(new List<ReplayBrowserRow> { _selectedReplayRow });
            }

            return Task.FromResult(new List<ReplayBrowserRow>());
        }

        var filtered = _replayRows
            .Where(row => IsReplayInAggregateRange(row, range, fromPicker, toPicker))
            .Where(row => row.Replay is not null)
            .OrderByDescending(row => row.RecordedAt)
            .ToList();
        return Task.FromResult(filtered);
    }

    private AggregateRangeOption GetWeaponSelectedRange() => _cmbWeaponRange?.SelectedIndex switch
    {
        0 => AggregateRangeOption.CurrentMatch,
        2 => AggregateRangeOption.Last7Days,
        3 => AggregateRangeOption.Last30Days,
        4 => AggregateRangeOption.Custom,
        _ => AggregateRangeOption.AllTime
    };

    private AggregateRangeOption GetOverallSelectedRange() => _cmbOverallRange?.SelectedIndex switch
    {
        1 => AggregateRangeOption.Last7Days,
        2 => AggregateRangeOption.Last30Days,
        3 => AggregateRangeOption.Custom,
        _ => AggregateRangeOption.AllTime
    };

    private static void ToggleAggregateDatePickers(AggregateRangeOption range, DateTimePicker? fromPicker, DateTimePicker? toPicker)
    {
        var isCustom = range == AggregateRangeOption.Custom;
        if (fromPicker is not null) fromPicker.Enabled = isCustom;
        if (toPicker is not null) toPicker.Enabled = isCustom;
    }

    private static bool IsReplayInAggregateRange(ReplayBrowserRow row, AggregateRangeOption option, DateTimePicker? fromPicker, DateTimePicker? toPicker)
    {
        var recordedAt = row.RecordedAt == default ? File.GetLastWriteTime(row.FilePath) : row.RecordedAt;
        var today = DateTime.Today;
        return option switch
        {
            AggregateRangeOption.Last7Days => recordedAt.Date >= today.AddDays(-6),
            AggregateRangeOption.Last30Days => recordedAt.Date >= today.AddDays(-29),
            AggregateRangeOption.Custom => recordedAt.Date >= fromPicker?.Value.Date && recordedAt.Date <= toPicker?.Value.Date,
            _ => true
        };
    }

    private List<WeaponStatsRow> BuildWeaponStatsSummary(
        IReadOnlyList<ReplayBrowserRow> rows,
        bool includeBotDamage,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var accumulators = new Dictionary<(string WeaponType, string WeaponName), WeaponStatsAccumulator>();

        for (var index = 0; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MergeWeaponSnapshots(rows[index], includeBotDamage, accumulators);
            progress?.Report(index + 1);
        }

        return accumulators.Values
            .Select(accumulator =>
            {
                var matchesUsed = Math.Max(1, accumulator.MatchKeys.Count);
                var includedHits = accumulator.HitsToPlayers + (includeBotDamage ? accumulator.HitsToBots : 0);
                var includedDamage = accumulator.DamageToPlayers + (includeBotDamage ? accumulator.DamageToBots : 0F);
                return new WeaponStatsRow
                {
                    WeaponType = accumulator.WeaponType,
                    WeaponName = accumulator.WeaponName,
                    MatchesUsed = matchesUsed,
                    KillOrDownCount = accumulator.KillOrDownCount,
                    EliminationCount = accumulator.EliminationCount,
                    Hits = includedHits,
                    HitsToPlayers = accumulator.HitsToPlayers,
                    HitsToBots = accumulator.HitsToBots,
                    HitsToNpcs = accumulator.HitsToNpcs,
                    HitsToStructures = accumulator.HitsToStructures,
                    CriticalHits = accumulator.CriticalHits,
                    ShieldHits = accumulator.ShieldHits,
                    FatalHits = accumulator.FatalHits,
                    TotalDamage = includedDamage,
                    DamageToPlayers = accumulator.DamageToPlayers,
                    DamageToBots = accumulator.DamageToBots,
                    DamageToNpcs = accumulator.DamageToNpcs,
                    DamageToStructures = accumulator.DamageToStructures,
                    AvgDamage = includedHits == 0 ? 0F : includedDamage / includedHits,
                    AvgDamagePerMatch = includedDamage / matchesUsed,
                    AvgHitsPerMatch = (float)includedHits / matchesUsed,
                    AvgKillOrDownsPerMatch = (float)accumulator.KillOrDownCount / matchesUsed,
                    CriticalRate = includedHits == 0 ? 0F : (float)accumulator.CriticalHits / includedHits * 100F
                };
            })
            .OrderByDescending(row => row.TotalDamage)
            .ThenByDescending(row => row.KillOrDownCount)
            .ThenBy(row => row.WeaponType)
            .ToList();
    }

    private List<ReplayWeaponStatsSnapshot> BuildWeaponStatsSnapshotsForReplay(FortniteReplay replay, string matchKey)
    {
        var owner = GetReplayOwner(replay);
        if (owner is null)
        {
            return [];
        }

        var accumulators = new Dictionary<(string WeaponType, string WeaponName), WeaponStatsAccumulator>();

        foreach (var evt in replay.DamageEvents.Where(evt => IsDamageByPlayer(owner, evt)))
        {
            var category = GetDamageEventTargetCategory(replay, evt);
            var weaponType = GetWeaponStatsTypeLabel(replay, evt);
            var weaponName = GetWeaponStatsNameLabel(replay, evt);
            if (string.IsNullOrWhiteSpace(weaponType) || string.Equals(weaponType, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var accumulator = GetOrCreateWeaponAccumulator(accumulators, weaponType, weaponName);
            accumulator.MatchKeys.Add(matchKey);
            var magnitude = evt.Magnitude ?? 0F;

            if (evt.IsCritical == true)
            {
                accumulator.CriticalHits++;
            }

            if (evt.IsFatal == true)
            {
                accumulator.FatalHits++;
            }

            if (evt.IsShield == true)
            {
                accumulator.ShieldHits++;
            }

            switch (category)
            {
                case DamageParticipantCategory.Player:
                    accumulator.Hits++;
                    accumulator.TotalDamage += magnitude;
                    accumulator.HitsToPlayers++;
                    accumulator.DamageToPlayers += magnitude;
                    break;
                case DamageParticipantCategory.Bot:
                    accumulator.Hits++;
                    accumulator.TotalDamage += magnitude;
                    accumulator.HitsToBots++;
                    accumulator.DamageToBots += magnitude;
                    break;
                case DamageParticipantCategory.Structure:
                default:
                    accumulator.HitsToStructures++;
                    accumulator.DamageToStructures += magnitude;
                    break;
            }
        }

        foreach (var entry in replay.KillFeed.Where(entry => MatchesResolvedKillFeedActor(replay, owner, entry) && !entry.IsRevived))
        {
            var weaponLabel = NormalizeKillReasonToWeaponLabel(FormatKillFeedReason(replay, entry));
            if (string.IsNullOrWhiteSpace(weaponLabel))
            {
                continue;
            }

            var accumulator = GetOrCreateWeaponAccumulator(accumulators, weaponLabel, weaponLabel);
            accumulator.MatchKeys.Add(matchKey);

            if (entry.IsDowned || IsDirectKillWithoutDown(replay, entry))
            {
                accumulator.KillOrDownCount++;
            }

            if (!entry.IsDowned)
            {
                accumulator.EliminationCount++;
            }
        }

        return accumulators.Values
            .Select(accumulator => new ReplayWeaponStatsSnapshot
            {
                WeaponType = accumulator.WeaponType,
                WeaponName = accumulator.WeaponName,
                KillOrDownCount = accumulator.KillOrDownCount,
                EliminationCount = accumulator.EliminationCount,
                Hits = accumulator.Hits,
                HitsToPlayers = accumulator.HitsToPlayers,
                HitsToBots = accumulator.HitsToBots,
                HitsToNpcs = accumulator.HitsToNpcs,
                HitsToStructures = accumulator.HitsToStructures,
                CriticalHits = accumulator.CriticalHits,
                ShieldHits = accumulator.ShieldHits,
                FatalHits = accumulator.FatalHits,
                TotalDamage = accumulator.TotalDamage,
                DamageToPlayers = accumulator.DamageToPlayers,
                DamageToBots = accumulator.DamageToBots,
                DamageToNpcs = accumulator.DamageToNpcs,
                DamageToStructures = accumulator.DamageToStructures
            })
            .ToList();
    }

    private void MergeWeaponSnapshots(
        ReplayBrowserRow row,
        bool includeBotDamage,
        IDictionary<(string WeaponType, string WeaponName), WeaponStatsAccumulator> accumulators)
    {
        foreach (var snapshot in row.WeaponStatsSnapshots)
        {
            var accumulator = GetOrCreateWeaponAccumulator(accumulators, snapshot.WeaponType, snapshot.WeaponName);
            accumulator.MatchKeys.Add(row.FilePath);
            accumulator.KillOrDownCount += snapshot.KillOrDownCount;
            accumulator.EliminationCount += snapshot.EliminationCount;
            accumulator.CriticalHits += snapshot.CriticalHits;
            accumulator.ShieldHits += snapshot.ShieldHits;
            accumulator.FatalHits += snapshot.FatalHits;

            accumulator.Hits += snapshot.HitsToPlayers + (includeBotDamage ? snapshot.HitsToBots : 0);
            accumulator.HitsToPlayers += snapshot.HitsToPlayers;
            accumulator.HitsToNpcs += snapshot.HitsToNpcs;
            accumulator.HitsToStructures += snapshot.HitsToStructures;
            accumulator.HitsToBots += includeBotDamage ? snapshot.HitsToBots : 0;

            accumulator.TotalDamage += snapshot.DamageToPlayers + (includeBotDamage ? snapshot.DamageToBots : 0F);
            accumulator.DamageToPlayers += snapshot.DamageToPlayers;
            accumulator.DamageToNpcs += snapshot.DamageToNpcs;
            accumulator.DamageToStructures += snapshot.DamageToStructures;
            accumulator.DamageToBots += includeBotDamage ? snapshot.DamageToBots : 0F;
        }
    }

    private static WeaponStatsAccumulator GetOrCreateWeaponAccumulator(
        IDictionary<(string WeaponType, string WeaponName), WeaponStatsAccumulator> accumulators,
        string weaponType,
        string weaponName)
    {
        weaponType = weaponType.Trim();
        weaponName = weaponName.Trim();
        var key = (weaponType.ToUpperInvariant(), weaponName.ToUpperInvariant());
        if (!accumulators.TryGetValue(key, out var accumulator))
        {
            accumulator = new WeaponStatsAccumulator
            {
                WeaponType = weaponType,
                WeaponName = weaponName
            };
            accumulators[key] = accumulator;
        }

        return accumulator;
    }

    private string GetWeaponStatsTypeLabel(FortniteReplay replay, DamageEvent evt)
    {
        var label = NormalizeWeaponTypeLabel(evt.WeaponType);
        if (!string.Equals(label, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return label;
        }

        label = NormalizeWeaponCategory(GetWeaponStatsNameLabel(replay, evt));
        if (!string.Equals(label, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return label;
        }

        var inferred = InferWeaponLabelFromNearbyKillFeed(replay, evt);
        return string.IsNullOrWhiteSpace(inferred) ? label : inferred;
    }

    private string GetWeaponStatsNameLabel(FortniteReplay replay, DamageEvent evt)
    {
        var rawSpecificName = GetMostSpecificWeaponIdentifier(evt);
        if (!string.IsNullOrWhiteSpace(rawSpecificName))
        {
            return rawSpecificName;
        }

        var specificName = GetMostSpecificWeaponLabel(evt);
        if (!string.IsNullOrWhiteSpace(specificName))
        {
            return specificName;
        }

        var inferred = InferWeaponLabelFromTags([evt.WeaponAssetName ?? string.Empty, evt.WeaponClassName ?? string.Empty]);
        if (!string.IsNullOrWhiteSpace(inferred))
        {
            return inferred;
        }

        var nearbyKillFeedWeapon = InferWeaponLabelFromNearbyKillFeed(replay, evt);
        return string.IsNullOrWhiteSpace(nearbyKillFeedWeapon) ? "Unknown" : nearbyKillFeedWeapon;
    }

    private static string? GetMostSpecificWeaponIdentifier(DamageEvent evt)
    {
        foreach (var candidate in new[]
                 {
                     GetRawWeaponIdentifier(evt.WeaponItemDefinition),
                     GetRawWeaponIdentifier(evt.WeaponAssetName),
                     GetRawWeaponIdentifier(evt.WeaponClassName),
                     GetRawWeaponIdentifier(evt.WeaponName)
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (!IsGenericWeaponLabel(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? GetRawWeaponIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var identifier = value.Trim();
        var slashIndex = identifier.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < identifier.Length - 1)
        {
            identifier = identifier[(slashIndex + 1)..];
        }

        var dotIndex = identifier.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex < identifier.Length - 1)
        {
            identifier = identifier[(dotIndex + 1)..];
        }

        return string.IsNullOrWhiteSpace(identifier) ? null : identifier;
    }

    private static string NormalizeWeaponCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var normalized = value.Trim();
        if (normalized.Contains("Shotgun", StringComparison.OrdinalIgnoreCase)) return "Shotgun";
        if (normalized.Contains("SMG", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Submachine", StringComparison.OrdinalIgnoreCase)) return "SMG";
        if (normalized.Contains("Pistol", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Revolver", StringComparison.OrdinalIgnoreCase)) return "Pistol";
        if (normalized.Contains("Sniper", StringComparison.OrdinalIgnoreCase) || normalized.Contains("DMR", StringComparison.OrdinalIgnoreCase)) return "Sniper";
        if (normalized.Contains("Rifle", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Assault", StringComparison.OrdinalIgnoreCase) || normalized.Contains("AR", StringComparison.OrdinalIgnoreCase)) return "Assault Rifle";
        if (normalized.Contains("Launcher", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Rocket", StringComparison.OrdinalIgnoreCase)) return "Explosive";
        return "Unknown";
    }

    private static string? GetMostSpecificWeaponLabel(DamageEvent evt)
    {
        foreach (var candidate in new[]
                 {
                     NormalizeWeaponDisplayLabel(evt.WeaponAssetName),
                     NormalizeWeaponDisplayLabel(evt.WeaponClassName),
                     NormalizeWeaponDisplayLabel(evt.WeaponName)
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (!IsGenericWeaponLabel(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsGenericWeaponLabel(string value)
    {
        return NormalizeWeaponCategory(value) != "Unknown"
               && value.Equals(NormalizeWeaponCategory(value), StringComparison.OrdinalIgnoreCase);
    }

    private string? InferWeaponLabelFromNearbyKillFeed(FortniteReplay replay, DamageEvent evt)
    {
        var weaponCues = GetKillFeedWeaponCues(replay);
        var eventTime = GetDamageTime(replay, evt);
        var instigatorKey = evt.InstigatorName;
        var targetKey = evt.TargetName;
        string? bestReason = null;
        var bestDistance = double.MaxValue;

        foreach (var cue in weaponCues)
        {
            var actorMatches =
                (!string.IsNullOrWhiteSpace(cue.ActorLookupKey) && string.Equals(cue.ActorLookupKey, instigatorKey, StringComparison.OrdinalIgnoreCase))
                || (evt.InstigatorId.HasValue && cue.ActorId == evt.InstigatorId);
            if (!actorMatches)
            {
                continue;
            }

            var targetMatches =
                (!string.IsNullOrWhiteSpace(cue.TargetLookupKey) && string.Equals(cue.TargetLookupKey, targetKey, StringComparison.OrdinalIgnoreCase))
                || (evt.TargetId.HasValue && cue.TargetId == evt.TargetId);
            if (!targetMatches)
            {
                continue;
            }

            var distance = Math.Abs(cue.TimeValue - eventTime);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestReason = cue.WeaponLabel;
        }

        return bestReason;
    }

    private static string? NormalizeKillReasonToWeaponLabel(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        if (reason.StartsWith("Headshot (", StringComparison.OrdinalIgnoreCase) && reason.EndsWith(")", StringComparison.Ordinal))
        {
            return reason["Headshot (".Length..^1].Trim();
        }

        if (reason.Contains("Storm", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("Fall", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return reason.Trim();
    }

    private static bool IsDirectKillWithoutDown(FortniteReplay replay, KillFeedEntry entry)
    {
        if (entry.IsDowned || entry.IsRevived)
        {
            return false;
        }

        var currentTime = GetKillFeedTime(entry);
        foreach (var priorEntry in replay.KillFeed
                     .Where(candidate => !ReferenceEquals(candidate, entry)
                                         && MatchesKillFeedTarget(candidate, entry.PlayerId, entry.PlayerName)
                                         && GetKillFeedTime(candidate) <= currentTime)
                     .OrderByDescending(GetKillFeedTime))
        {
            if (priorEntry.IsRevived)
            {
                break;
            }

            return !priorEntry.IsDowned;
        }

        return true;
    }

    private IEnumerable<DetailRow> BuildOverallStatisticsRows(IEnumerable<ReplayBrowserRow> rows, bool includeBotKills)
    {
        var loadedRows = rows.Where(row => row.Replay is not null).ToList();
        var owners = loadedRows
            .Select(row => BuildReplayCombatSnapshot(row, includeBotKills))
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .ToList();

        if (owners.Count == 0)
        {
            yield return new DetailRow("Status", "No loaded replay-owner data available.");
            yield break;
        }

        var placements = owners.Select(x => x.Owner.Placement ?? (int?)x.Replay.TeamStats?.Position).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var kills = owners.Select(x => x.Kills).ToList();
        var playerDamagePerReplay = owners.Select(x => (double)x.PlayerDamage).ToList();
        var damageTaken = owners.Select(x => (double)(x.Replay.Stats?.DamageTaken ?? 0)).ToList();
        var accuracies = owners.Select(x => (double)(x.Replay.Stats?.Accuracy ?? 0)).ToList();
        var totalCombatEventCount = owners.Sum(x => x.IncludedCombatEventCount);
        var totalCritCount = owners.Sum(x => x.IncludedCriticalCount);
        var totalIncludedDamage = owners.Sum(x => x.IncludedCombatDamage);
        var playerDamageTotal = owners.Sum(x => x.PlayerDamage);
        var botDamageTotal = owners.Sum(x => x.BotDamage);
        yield return new DetailRow("Matches", owners.Count.ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Wins", placements.Count(place => place == 1).ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Win Rate", $"{owners.Count(place => place.Owner.Placement == 1) / (double)owners.Count * 100:0.0}%");
        yield return new DetailRow("Avg Placement", placements.Count == 0 ? "-" : $"{placements.Average():0.0}");
        yield return new DetailRow("Median Placement", placements.Count == 0 ? "-" : $"{placements.OrderBy(x => x).ElementAt(placements.Count / 2)}");
        yield return new DetailRow("Top 3 Rate", $"{placements.Count(place => place <= 3) / (double)Math.Max(1, placements.Count) * 100:0.0}%");
        yield return new DetailRow("Top 10 Rate", $"{placements.Count(place => place <= 10) / (double)Math.Max(1, placements.Count) * 100:0.0}%");
        yield return new DetailRow("Total Eliminations", kills.Sum().ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Avg Eliminations", $"{kills.Average():0.0}");
        yield return new DetailRow("Best Elim Game", kills.Max().ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Avg Damage To Players", $"{playerDamagePerReplay.Average():0.0}");
        yield return new DetailRow("Avg Damage Taken", $"{damageTaken.Average():0.0}");
        yield return new DetailRow("Avg Accuracy", $"{accuracies.Average():0.0}%");
        yield return new DetailRow("Critical Hit Rate", totalCombatEventCount == 0 ? "-" : $"{totalCritCount / (double)totalCombatEventCount * 100:0.0}%");
        yield return new DetailRow("Avg Damage Events Logged", $"{totalCombatEventCount / (double)Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow("Avg Hit Damage", totalCombatEventCount == 0 ? "-" : $"{totalIncludedDamage / totalCombatEventCount:0.0}");
        yield return new DetailRow("Avg Logged Damage To Bots", $"{botDamageTotal / Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow("Avg Logged Damage To Players", $"{playerDamageTotal / Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow("Avg Revives", $"{owners.Sum(x => x.Replay.Stats?.Revives ?? 0) / (double)Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow("Avg Assists", $"{owners.Sum(x => x.Replay.Stats?.Assists ?? 0) / (double)Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow("Avg Materials Gathered", $"{owners.Sum(x => x.Replay.Stats?.MaterialsGathered ?? 0) / (double)Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow("Avg Materials Used", $"{owners.Sum(x => x.Replay.Stats?.MaterialsUsed ?? 0) / (double)Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow("Avg Distance Travelled", $"{owners.Sum(x => x.Replay.Stats?.TotalTraveled ?? 0) / 100.0 / Math.Max(1, owners.Count):0.0} m");
        yield return new DetailRow("Avg Early Fight Damage (0-5m)", $"{owners.Sum(x => x.EarlyFightDamage) / Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow("Avg Mid Fight Damage (5-12m)", $"{owners.Sum(x => x.MidFightDamage) / Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow("Avg Late Fight Damage (12m+)", $"{owners.Sum(x => x.LateFightDamage) / Math.Max(1, owners.Count):0.0}");
        yield return new DetailRow(
            "Longest Elimination Distance",
            $"{owners.SelectMany(ownerRow => ownerRow.Replay.KillFeed
                    .Where(entry => ResolveKillFeedActorReference(ownerRow.Replay, entry).Player?.PlayerId == ownerRow.Owner.PlayerId && entry.Distance.HasValue)
                    .Where(entry => includeBotKills || !(FindPlayer(ownerRow.Replay, entry.PlayerId, entry.PlayerName)?.IsBot ?? entry.PlayerIsBot))
                    .Select(entry => entry.Distance!.Value / 100.0))
                .DefaultIfEmpty(0)
                .Max():0.00} m");
    }

    private void UpdateDamageTimelineChart()
    {
        if (_damageTimelinePanel is null)
        {
            return;
        }

        _timelineSeries.Clear();

        var replay = _selectedReplayRow?.Replay;
        var owner = replay is null ? null : GetReplayOwner(replay);
        if (replay is null || owner is null)
        {
            return;
        }

        var teamMates = replay.PlayerData?
            .Where(player => player.TeamIndex == owner.TeamIndex && !MatchesPlayer(owner, player.Id, player.PlayerId))
            .OrderBy(player => ResolvePlayerName(player, player.Id, player.PlayerId))
            .ToList() ?? [];

        var selectedPlayers = new List<PlayerData> { owner };
        if (_chkTimelineTeam?.Checked == true)
        {
            selectedPlayers.AddRange(teamMates);
        }

        foreach (var player in selectedPlayers.DistinctBy(GetPlayerCacheKey))
        {
            var events = replay.DamageEvents
                .Where(evt => IsDamageByPlayer(player, evt))
                .Where(evt => ShouldIncludeTimelineDamageEvent(replay, evt))
                .OrderBy(evt => GetDamageTime(replay, evt))
                .ToList();

            var running = 0F;
            var points = new List<DamageTimelinePoint>
            {
                new()
                {
                    TimeValue = 0D,
                    Damage = 0F
                }
            };

            foreach (var evt in events)
            {
                running += evt.Magnitude ?? 0F;
                points.Add(new DamageTimelinePoint
                {
                    TimeValue = GetDamageTime(replay, evt),
                    Damage = _chkTimelineCumulative?.Checked == true ? running : evt.Magnitude ?? 0F
                });
            }

            _timelineSeries.Add((ResolvePlayerName(player, player.Id, player.PlayerId), points));
        }

        _damageTimelinePanel.Invalidate();
    }

    private IEnumerable<MatchTrendRow> BuildOverallTrendRows(IEnumerable<ReplayBrowserRow> rows, bool includeBotKills)
    {
        foreach (var row in rows.Where(x => x.Replay is not null))
        {
            var snapshot = BuildReplayCombatSnapshot(row, includeBotKills);
            if (snapshot is null)
            {
                continue;
            }

            yield return new MatchTrendRow
            {
                Label = BuildTrendLabel(row),
                DamageToPlayersAndBots = snapshot.IncludedCombatDamage,
                Kills = snapshot.Kills
            };
        }
    }

    private ReplayCombatSnapshot? BuildReplayCombatSnapshot(ReplayBrowserRow row, bool includeBotKills)
    {
        var replay = row.Replay;
        if (replay is null)
        {
            return null;
        }

        var owner = GetReplayOwner(replay);
        if (owner is null)
        {
            return null;
        }

        var snapshot = new ReplayCombatSnapshot
        {
            Replay = replay,
            Owner = owner,
            Kills = CountReplayEliminations(replay, owner, includeBotKills)
        };

        foreach (var evt in replay.DamageEvents.Where(evt => IsDamageByPlayer(owner, evt)))
        {
            var amount = evt.Magnitude ?? 0F;
            if (amount <= 0)
            {
                continue;
            }

            var category = GetDamageEventTargetCategory(replay, evt);
            switch (category)
            {
                case DamageParticipantCategory.Player:
                    snapshot.PlayerDamage += amount;
                    snapshot.IncludedCombatDamage += amount;
                    snapshot.IncludedCombatEventCount++;
                    if (evt.IsCritical == true)
                    {
                        snapshot.IncludedCriticalCount++;
                    }
                    AccumulateFightPhaseDamage(snapshot, evt, amount);
                    break;
                case DamageParticipantCategory.Bot:
                    snapshot.BotDamage += amount;
                    if (includeBotKills)
                    {
                        snapshot.IncludedCombatDamage += amount;
                        snapshot.IncludedCombatEventCount++;
                        if (evt.IsCritical == true)
                        {
                            snapshot.IncludedCriticalCount++;
                        }
                        AccumulateFightPhaseDamage(snapshot, evt, amount);
                    }
                    break;
                default:
                    snapshot.StructureDamage += amount;
                    break;
            }
        }

        return snapshot;
    }

    private void AccumulateFightPhaseDamage(ReplayCombatSnapshot snapshot, DamageEvent evt, float amount)
    {
        var time = GetDamageTime(snapshot.Replay, evt);
        if (time <= 300)
        {
            snapshot.EarlyFightDamage += amount;
        }
        else if (time <= 720)
        {
            snapshot.MidFightDamage += amount;
        }
        else
        {
            snapshot.LateFightDamage += amount;
        }
    }

    private int CountReplayEliminations(FortniteReplay replay, PlayerData owner, bool includeBotKills)
    {
        return replay.KillFeed
            .Where(entry => MatchesResolvedKillFeedActor(replay, owner, entry) && !entry.IsRevived && !entry.IsDowned)
            .Count(entry => includeBotKills || !(FindPlayer(replay, entry.PlayerId, entry.PlayerName)?.IsBot ?? entry.PlayerIsBot));
    }

    private static string BuildTrendLabel(ReplayBrowserRow row)
    {
        var recordedAt = row.RecordedAt == default ? File.GetLastWriteTime(row.FilePath) : row.RecordedAt;
        return $"{recordedAt:M/d/yyyy}\n{recordedAt:h:mm tt}";
    }

    private bool ShouldIncludeTimelineDamageEvent(FortniteReplay replay, DamageEvent evt)
    {
        var category = GetDamageEventTargetCategory(replay, evt);

        return category switch
        {
            DamageParticipantCategory.Player => _chkTimelinePlayers?.Checked ?? true,
            DamageParticipantCategory.Bot => _chkTimelineBots?.Checked ?? true,
            _ => false
        };
    }

    private static bool IsCombatDamageParticipant(DamageParticipantCategory category)
    {
        return category is DamageParticipantCategory.Player or DamageParticipantCategory.Bot;
    }

    private static bool IsDamageByPlayer(PlayerData player, DamageEvent evt)
    {
        return (!string.IsNullOrWhiteSpace(player.PlayerId) && string.Equals(player.PlayerId, evt.InstigatorName, StringComparison.OrdinalIgnoreCase))
               || (player.Id.HasValue && evt.InstigatorId == player.Id);
    }

    private void PaintDamageTimeline(Graphics graphics, Rectangle bounds)
    {
        _timelineHitRegions = [];
        graphics.Clear(Color.White);
        using var axisPen = new Pen(Color.Silver, 1F);
        using var textBrush = new SolidBrush(Color.FromArgb(48, 56, 66));
        using var gridPen = new Pen(Color.Gainsboro, 1F);
        using var font = new Font("Segoe UI", 8.5F);
        using var smallFont = new Font("Segoe UI", 8F);

        var legendRows = Math.Max(1, (int)Math.Ceiling(_timelineSeries.Count / 2D));
        var legendHeight = legendRows * 18;
        var bottomPadding = 42 + legendHeight;
        var chartBounds = Rectangle.FromLTRB(bounds.Left + 48, bounds.Top + 16, bounds.Right - 12, bounds.Bottom - bottomPadding);
        _timelineChartBounds = chartBounds;
        if (chartBounds.Width <= 10 || chartBounds.Height <= 10)
        {
            return;
        }

        graphics.DrawRectangle(axisPen, chartBounds);
        if (_timelineSeries.Count == 0)
        {
            graphics.DrawString("No damage events for the current filters.", font, textBrush, chartBounds.Left + 8, chartBounds.Top + 8);
            return;
        }

        var isCumulative = _chkTimelineCumulative?.Checked == true;
        var maxTime = Math.Max(1D, _timelineSeries.SelectMany(series => series.Points).DefaultIfEmpty(new DamageTimelinePoint()).Max(point => point.TimeValue));
        var maxDamage = Math.Max(1F, _timelineSeries.SelectMany(series => series.Points).DefaultIfEmpty(new DamageTimelinePoint()).Max(point => point.Damage));
        var palette = new[]
        {
            Color.FromArgb(25, 118, 210),
            Color.FromArgb(0, 150, 136),
            Color.FromArgb(244, 81, 30),
            Color.FromArgb(123, 31, 162),
            Color.FromArgb(255, 179, 0)
        };

        for (var i = 1; i <= 4; i++)
        {
            var y = chartBounds.Bottom - (chartBounds.Height * i / 4F);
            graphics.DrawLine(gridPen, chartBounds.Left, y, chartBounds.Right, y);
            graphics.DrawString($"{maxDamage * i / 4F:0}", font, textBrush, 4, y - 8);
        }

        for (var i = 0; i <= 4; i++)
        {
            var x = chartBounds.Left + (chartBounds.Width * i / 4F);
            graphics.DrawLine(axisPen, x, chartBounds.Bottom, x, chartBounds.Bottom + 4);
            var tickTime = maxTime * i / 4D;
            var tickText = FormatMatchClock(tickTime);
            var tickSize = graphics.MeasureString(tickText, smallFont);
            graphics.DrawString(tickText, smallFont, textBrush, x - (tickSize.Width / 2F), chartBounds.Bottom + 6);
        }

        for (var i = 0; i < _timelineSeries.Count; i++)
        {
            var color = palette[i % palette.Length];
            using var linePen = new Pen(color, 2.5F);
            var series = _timelineSeries[i];
            if (isCumulative)
            {
                var points = series.Points
                    .Select(point => new PointF(
                        chartBounds.Left + (float)(point.TimeValue / maxTime) * chartBounds.Width,
                        chartBounds.Bottom - (point.Damage / maxDamage) * chartBounds.Height))
                    .ToArray();

                if (points.Length >= 2)
                {
                    graphics.DrawLines(linePen, points);
                }
                else if (points.Length == 1)
                {
                    graphics.DrawEllipse(linePen, points[0].X - 2, points[0].Y - 2, 4, 4);
                }

                for (var j = 0; j < points.Length; j++)
                {
                    var point = series.Points[j];
                    var hitBounds = new RectangleF(points[j].X - 5F, points[j].Y - 5F, 10F, 10F);
                    _timelineHitRegions.Add((hitBounds, $"{series.Name}\n{point.Damage:0.#} damage\n{FormatMatchClock(point.TimeValue)}"));
                }
            }
            else
            {
                using var brush = new SolidBrush(Color.FromArgb(140, color));
                var slotWidth = Math.Max(3F, chartBounds.Width / Math.Max(8F, maxTime / 8F));
                for (var j = 0; j < series.Points.Count; j++)
                {
                    var point = series.Points[j];
                    var centerX = chartBounds.Left + (float)(point.TimeValue / maxTime) * chartBounds.Width;
                    var offset = (i - (_timelineSeries.Count - 1) / 2F) * slotWidth;
                    var height = (float)((point.Damage / maxDamage) * chartBounds.Height);
                    var rectX = (float)(centerX + offset - (slotWidth / 2F));
                    var rectY = chartBounds.Bottom - height;
                    var rectWidth = (float)Math.Max(2F, slotWidth - 1F);
                    var barBounds = new RectangleF(rectX, rectY, rectWidth, Math.Max(2F, height));
                    graphics.FillRectangle(brush, barBounds);
                    _timelineHitRegions.Add((barBounds, $"{series.Name}\n{point.Damage:0.#} damage\n{FormatMatchClock(point.TimeValue)}"));
                }
            }

            using var legendBrush = new SolidBrush(color);
            var legendSlotWidth = Math.Max(140F, chartBounds.Width / 2F);
            var legendColumn = i % 2;
            var legendRow = i / 2;
            var legendX = chartBounds.Left + 8 + (legendColumn * legendSlotWidth);
            var legendY = chartBounds.Bottom + 28F + (legendRow * 16F);
            graphics.DrawString(series.Name, smallFont, legendBrush, legendX, legendY);
        }

        var axisLabel = "Match Time";
        var axisLabelSize = graphics.MeasureString(axisLabel, font);
        graphics.DrawString(axisLabel, font, textBrush, chartBounds.Left + (chartBounds.Width - axisLabelSize.Width) / 2F, chartBounds.Bottom + 6);
    }

    private static void PaintOverallTrendChart(Graphics graphics, Rectangle bounds, string title, List<(double Value, string Label)> values, Color barColor, List<(RectangleF Bounds, string Text)> hitRegions, Action<RectangleF>? chartBoundsCallback = null)
    {
        hitRegions.Clear();
        graphics.Clear(Color.White);
        using var axisPen = new Pen(Color.Silver, 1F);
        using var textBrush = new SolidBrush(Color.FromArgb(48, 56, 66));
        using var gridPen = new Pen(Color.Gainsboro, 1F);
        using var font = new Font("Segoe UI", 8.5F);
        using var titleFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        using var barBrush = new SolidBrush(barColor);

        var chart = Rectangle.FromLTRB(bounds.Left + 56, bounds.Top + 24, bounds.Right - 18, bounds.Bottom - 76);
        chartBoundsCallback?.Invoke(chart);
        graphics.DrawString(title, titleFont, textBrush, bounds.Left + 12, bounds.Top + 4);
        PaintVerticalBarChart(graphics, chart, values, barBrush, axisPen, gridPen, textBrush, font, hitRegions);
    }

    private static void PaintVerticalBarChart(Graphics graphics, Rectangle bounds, List<(double Value, string Label)> values, Brush barBrush, Pen axisPen, Pen gridPen, Brush textBrush, Font font, List<(RectangleF Bounds, string Text)> hitRegions)
    {
        graphics.DrawRectangle(axisPen, bounds);
        if (values.Count == 0)
        {
            graphics.DrawString("No loaded replays in range.", font, textBrush, bounds.Left + 8, bounds.Top + 8);
            return;
        }

        var maxValue = Math.Max(1D, values.Max(x => x.Value));
        for (var i = 0; i <= 4; i++)
        {
            var y = bounds.Bottom - (bounds.Height * i / 4F);
            graphics.DrawLine(gridPen, bounds.Left, y, bounds.Right, y);
            var tickValue = maxValue * i / 4D;
            var tickText = tickValue.ToString("0.#", CultureInfo.CurrentCulture);
            var tickSize = graphics.MeasureString(tickText, font);
            graphics.DrawString(tickText, font, textBrush, Math.Max(0F, bounds.Left - tickSize.Width - 6F), y - (tickSize.Height / 2F));
        }

        var barWidth = Math.Max(12F, (bounds.Width - 16F) / Math.Max(1, values.Count) - 8F);
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            var x = bounds.Left + 8F + i * (barWidth + 8F);
            var barHeight = (float)(value.Value / maxValue) * (bounds.Height - 20F);
            var barBounds = new RectangleF(x, bounds.Bottom - barHeight, barWidth, barHeight);
            graphics.FillRectangle(barBrush, barBounds);
            hitRegions.Add((barBounds, $"{value.Label.Replace('\n', ' ')}\n{value.Value:0.#}"));

            var labelLines = value.Label.Split('\n');
            var labelY = bounds.Bottom + 4F;
            foreach (var line in labelLines)
            {
                var labelSize = graphics.MeasureString(line, font);
                graphics.DrawString(line, font, textBrush, x + Math.Max(0F, (barWidth - labelSize.Width) / 2F), labelY);
                labelY += labelSize.Height - 2F;
            }

            var valueText = value.Value.ToString("0.#", CultureInfo.CurrentCulture);
            var valueSize = graphics.MeasureString(valueText, font);
            graphics.DrawString(valueText, font, textBrush, x + Math.Max(0F, (barWidth - valueSize.Width) / 2F), Math.Max(bounds.Top, bounds.Bottom - barHeight - valueSize.Height - 2F));
        }
    }

    private void PaintWeaponSharePieChart(Graphics graphics, Rectangle bounds, string title, List<(string Label, double Value)> values, List<(RectangleF Bounds, string Text)> hitRegions)
    {
        hitRegions.Clear();
        graphics.Clear(Color.White);
        using var titleFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        using var font = new Font("Segoe UI", 8.5F);
        using var textBrush = new SolidBrush(Color.FromArgb(48, 56, 66));

        graphics.DrawString(title, titleFont, textBrush, bounds.Left + 12, bounds.Top + 6);

        if (values.Count == 0 || values.Sum(value => value.Value) <= 0)
        {
            graphics.DrawString("No weapon data available.", font, textBrush, bounds.Left + 12, bounds.Top + 30);
            return;
        }

        var palette = new[]
        {
            Color.FromArgb(52, 123, 220),
            Color.FromArgb(244, 124, 32),
            Color.FromArgb(0, 150, 136),
            Color.FromArgb(123, 31, 162),
            Color.FromArgb(198, 40, 40),
            Color.FromArgb(124, 179, 66)
        };

        var pieBounds = RectangleF.FromLTRB(bounds.Left + 12, bounds.Top + 28, bounds.Left + 220, bounds.Bottom - 12);
        var total = values.Sum(value => value.Value);
        var startAngle = -90F;
        for (var i = 0; i < values.Count; i++)
        {
            var sweepAngle = (float)(values[i].Value / total * 360D);
            using var brush = new SolidBrush(palette[i % palette.Length]);
            graphics.FillPie(brush, pieBounds, startAngle, sweepAngle);
            hitRegions.Add((pieBounds, $"{values[i].Label}\n{values[i].Value:0.#} ({values[i].Value / total:P1})"));

            var legendX = pieBounds.Right + 16F;
            var legendY = bounds.Top + 28F + (i * 22F);
            graphics.FillRectangle(brush, legendX, legendY + 3F, 12F, 12F);
            graphics.DrawString($"{values[i].Label}: {values[i].Value / total:P1}", font, textBrush, legendX + 18F, legendY);
            startAngle += sweepAngle;
        }
    }

    private void UpdateGraphToolTip(Control? control, List<(RectangleF Bounds, string Text)> hitRegions, Point location)
    {
        if (control is null)
        {
            return;
        }

        if (ReferenceEquals(control, _damageTimelinePanel) && _timelineChartBounds.Contains(location))
        {
            _graphToolTip.SetToolTip(control, BuildTimelineHoverText(location));
            return;
        }

        if (ReferenceEquals(control, _overallDamageTrendPanel) && _overallDamageChartBounds.Contains(location))
        {
            _graphToolTip.SetToolTip(control, BuildOverallTrendHoverText(location, true));
            return;
        }

        if (ReferenceEquals(control, _overallKillsTrendPanel) && _overallKillsChartBounds.Contains(location))
        {
            _graphToolTip.SetToolTip(control, BuildOverallTrendHoverText(location, false));
            return;
        }

        var hit = hitRegions.LastOrDefault(region => region.Bounds.Contains(location));
        _graphToolTip.SetToolTip(control, string.IsNullOrWhiteSpace(hit.Text) ? string.Empty : hit.Text);
    }

    private string BuildTimelineHoverText(Point location)
    {
        if (_timelineSeries.Count == 0 || _timelineChartBounds.Width <= 0)
        {
            return string.Empty;
        }

        var maxTime = Math.Max(1D, _timelineSeries.SelectMany(series => series.Points).DefaultIfEmpty(new DamageTimelinePoint()).Max(point => point.TimeValue));
        var timeValue = ((location.X - _timelineChartBounds.Left) / _timelineChartBounds.Width) * maxTime;
        timeValue = Math.Max(0D, Math.Min(maxTime, timeValue));

        var lines = new List<string> { FormatMatchClock(timeValue) };
        if (_chkTimelineCumulative?.Checked == true)
        {
            foreach (var series in _timelineSeries)
            {
                lines.Add($"{series.Name}: {GetTimelineInterpolatedValue(series.Points, timeValue):0.#}");
            }
        }
        else
        {
            foreach (var series in _timelineSeries)
            {
                lines.Add($"{series.Name}: {GetTimelineNearestValue(series.Points, timeValue):0.#}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildOverallTrendHoverText(Point location, bool isDamageChart)
    {
        var bounds = isDamageChart ? _overallDamageChartBounds : _overallKillsChartBounds;
        if (_overallTrendRows.Count == 0 || bounds.Width <= 0)
        {
            return string.Empty;
        }

        var barWidth = Math.Max(12F, (bounds.Width - 16F) / Math.Max(1, _overallTrendRows.Count) - 8F);
        var index = (int)Math.Floor((location.X - bounds.Left - 8F) / (barWidth + 8F));
        index = Math.Max(0, Math.Min(_overallTrendRows.Count - 1, index));
        var row = _overallTrendRows[index];
        var value = isDamageChart ? row.DamageToPlayersAndBots : row.Kills;
        return $"{row.Label.Replace('\n', ' ')}{Environment.NewLine}{value:0.#}";
    }

    private static float GetTimelineInterpolatedValue(List<DamageTimelinePoint> points, double timeValue)
    {
        if (points.Count == 0)
        {
            return 0F;
        }

        if (timeValue <= points[0].TimeValue)
        {
            return points[0].Damage;
        }

        for (var i = 1; i < points.Count; i++)
        {
            if (timeValue > points[i].TimeValue)
            {
                continue;
            }

            var previous = points[i - 1];
            var current = points[i];
            var delta = current.TimeValue - previous.TimeValue;
            if (delta <= 0D)
            {
                return current.Damage;
            }

            var ratio = (float)((timeValue - previous.TimeValue) / delta);
            return previous.Damage + ((current.Damage - previous.Damage) * ratio);
        }

        return points[^1].Damage;
    }

    private static float GetTimelineNearestValue(List<DamageTimelinePoint> points, double timeValue)
    {
        if (points.Count == 0)
        {
            return 0F;
        }

        return points.OrderBy(point => Math.Abs(point.TimeValue - timeValue)).First().Damage;
    }

    private void OpenDamageTimelineExplorer()
    {
        if (_selectedReplayRow is null || _timelineSeries.Count == 0)
        {
            return;
        }

        GraphExplorerForm.CreateTimeline(
            $"Damage Timeline - {_selectedReplayRow.FileName}",
            _timelineSeries,
            _chkTimelineCumulative?.Checked == true).Show(this);
    }

    private void OpenOverallTrendExplorer(bool isDamageChart)
    {
        if (_overallTrendRows.Count == 0)
        {
            return;
        }

        var title = isDamageChart ? "Overall Damage Per Match" : "Overall Kills Per Match";
        var points = _overallTrendRows
            .Select((row, index) => new GraphExplorerPoint
            {
                Label = row.Label,
                XValue = index,
                Value = isDamageChart ? row.DamageToPlayersAndBots : row.Kills
            })
            .ToList();
        GraphExplorerForm.CreateBar(title, points, isDamageChart ? Color.FromArgb(52, 123, 220) : Color.FromArgb(244, 124, 32)).Show(this);
    }

    private void OpenWeaponShareExplorer(bool isDamageChart)
    {
        if (_lastWeaponStatsRows.Count == 0)
        {
            return;
        }

        var title = isDamageChart ? "Weapon Damage Share" : "Weapon Kills/Downs Share";
        var points = _lastWeaponStatsRows
            .Where(row => isDamageChart ? row.AvgDamagePerMatch > 0F : row.AvgKillOrDownsPerMatch > 0F)
            .Select((row, index) => new GraphExplorerPoint
            {
                Label = row.WeaponName,
                XValue = index,
                Value = isDamageChart ? row.AvgDamagePerMatch : row.AvgKillOrDownsPerMatch
            })
            .ToList();

        if (points.Count == 0)
        {
            return;
        }

        GraphExplorerForm.CreateBar(title, points, isDamageChart ? Color.FromArgb(52, 123, 220) : Color.FromArgb(244, 124, 32)).Show(this);
    }
}
