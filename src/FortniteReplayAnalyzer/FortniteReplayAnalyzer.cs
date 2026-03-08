using FortniteReplayReader;
using FortniteReplayReader.Models;
using System.Globalization;
using Unreal.Core.Exceptions;
using Unreal.Core.Models.Enums;

namespace FortniteReplayAnalyzer;

public partial class FortniteReplayAnalyzer : Form
{
    private const int ExpandedReplayPaneWidth = 360;
    private const int CollapsedReplayPaneWidth = 52;

    private readonly string _replayFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame",
        "Saved",
        "Demos");

    private readonly List<ReplayBrowserRow> _replayRows = [];
    private readonly List<PlayerSummaryRow> _playerRows = [];
    private readonly Dictionary<string, int> _replayBrowserColumnWidths = new(StringComparer.Ordinal);

    private string _replaySortColumn = nameof(ReplayBrowserRow.RecordedAt);
    private bool _replaySortAscending;
    private string _playerSortColumn = nameof(PlayerSummaryRow.Kills);
    private bool _playerSortAscending;
    private bool _isReplayPaneCollapsed;
    private bool _suppressReplaySelectionChanged;

    private ReplayBrowserRow? _selectedReplayRow;
    private PlayerData? _selectedPlayer;
    private CheckBox? _chkTeamDamageOnly;
    private GroupBox? _grpPlayerDamageLog;
    private DataGridView? _dgvPlayerDamageLog;
    private TabControl? _openedReplayTabs;
    private bool _suppressReplayTabSelection;

    public FortniteReplayAnalyzer()
    {
        InitializeComponent();
        ConfigureGrids();
        InitializeDynamicUi();
        WireEvents();
        DebugOutputWriter.LogInfo("Fortnite Replay Analyzer started.");
    }


    private void InitializeDynamicUi()
    {
        Size = new Size(1800, 900);
        splitMain.Panel1MinSize = CollapsedReplayPaneWidth;
        grpCombatEvents.Text = "Damage Events";
        grpPlayerCombatLog.Text = "Kill Log";

        btnToggleReplayPane.Visible = false;
        btnRefreshReplays.Visible = false;
        lblReplayHeader.Cursor = Cursors.Hand;
        lblReplayHeader.Click += (_, _) => SetReplayPaneCollapsed(!_isReplayPaneCollapsed);

        var damageFilterPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 28,
            Padding = new Padding(4, 0, 0, 0)
        };

        _chkTeamDamageOnly = new CheckBox
        {
            AutoSize = true,
            Text = "Team members only",
            Location = new Point(0, 1)
        };
        _chkTeamDamageOnly.CheckedChanged += (_, _) =>
        {
            if (_selectedReplayRow?.Replay is not null)
            {
                BuildCombatEvents(_selectedReplayRow.Replay);
            }
        };
        damageFilterPanel.Controls.Add(_chkTeamDamageOnly);

        var damageEventsLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 2
        };
        damageEventsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        damageEventsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        damageEventsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        grpCombatEvents.Controls.Remove(dgvCombatEvents);
        damageEventsLayout.Controls.Add(damageFilterPanel, 0, 0);
        damageEventsLayout.Controls.Add(dgvCombatEvents, 0, 1);
        grpCombatEvents.Controls.Add(damageEventsLayout);

        _grpPlayerDamageLog = new GroupBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Padding = new Padding(10),
            Text = "Damage Log"
        };

        _dgvPlayerDamageLog = new DataGridView { Name = "dgvPlayerDamageLog" };
        ConfigureReadOnlyGrid(_dgvPlayerDamageLog, fullRowSelect: true);
        _dgvPlayerDamageLog.AutoGenerateColumns = false;
        BuildCombatEventColumns(_dgvPlayerDamageLog);
        _dgvPlayerDamageLog.CellContentClick += (_, e) => HandleCombatEventLinkClick(_dgvPlayerDamageLog, e);
        _grpPlayerDamageLog.Controls.Add(_dgvPlayerDamageLog);

        playerContentLayout.SetColumnSpan(grpPlayerVictims, 1);
        playerContentLayout.Controls.Add(_grpPlayerDamageLog, 1, 1);

        _openedReplayTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            HotTrack = true,
            Multiline = false,
            Padding = new Point(14, 4)
        };
        _openedReplayTabs.SelectedIndexChanged += async (_, _) => await HandleReplayTabSelectionChangedAsync();

        splitMain.Panel2.Controls.Clear();
        splitMain.Panel2.Controls.Add(_openedReplayTabs);
    }
    private void WireEvents()
    {
        Shown += async (_, _) => await RefreshReplayBrowserAsync();

        dgvReplayBrowser.SelectionChanged += async (_, _) => await HandleReplaySelectionChangedAsync();
        dgvReplayBrowser.ColumnHeaderMouseClick += (_, e) => SortReplayRows(dgvReplayBrowser.Columns[e.ColumnIndex].Name);


        dgvKillFeed.CellContentClick += (_, e) => HandleKillFeedLinkClick(e);
        dgvCombatEvents.CellContentClick += (_, e) => HandleCombatEventLinkClick(dgvCombatEvents, e);
        dgvPlayers.CellContentClick += (_, e) => HandlePlayerLinkClick(e);
        dgvPlayers.SelectionChanged += (_, _) => HandlePlayerSelectionChanged();
        dgvPlayers.ColumnHeaderMouseClick += (_, e) => SortPlayerRows(dgvPlayers.Columns[e.ColumnIndex].Name);
    }

    private void ConfigureGrids()
    {
        ConfigureReadOnlyGrid(dgvReplayBrowser, fullRowSelect: true);
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
        grid.Columns.Add(new DataGridViewLinkColumn { Name = nameof(KillFeedRow.ActorName), HeaderText = "Actor", DataPropertyName = nameof(KillFeedRow.ActorName), FillWeight = 130, TrackVisitedState = false, UseColumnTextForLinkValue = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(KillFeedRow.EventText), HeaderText = "Event", DataPropertyName = nameof(KillFeedRow.EventText), FillWeight = 85 });
        grid.Columns.Add(new DataGridViewLinkColumn { Name = nameof(KillFeedRow.TargetName), HeaderText = "Target", DataPropertyName = nameof(KillFeedRow.TargetName), FillWeight = 130, TrackVisitedState = false, UseColumnTextForLinkValue = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(KillFeedRow.DistanceText), HeaderText = "Distance", DataPropertyName = nameof(KillFeedRow.DistanceText), FillWeight = 70 });
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private static void BuildCombatEventColumns(DataGridView grid)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.TimeText), HeaderText = "Time", DataPropertyName = nameof(CombatEventRow.TimeText), FillWeight = 62 });
        grid.Columns.Add(new DataGridViewLinkColumn { Name = nameof(CombatEventRow.AttackerName), HeaderText = "Attacker", DataPropertyName = nameof(CombatEventRow.AttackerName), FillWeight = 130, TrackVisitedState = false, UseColumnTextForLinkValue = false });
        grid.Columns.Add(new DataGridViewLinkColumn { Name = nameof(CombatEventRow.TargetName), HeaderText = "Target", DataPropertyName = nameof(CombatEventRow.TargetName), FillWeight = 130, TrackVisitedState = false, UseColumnTextForLinkValue = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.DamageText), HeaderText = "Damage", DataPropertyName = nameof(CombatEventRow.DamageText), FillWeight = 72 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.ShieldText), HeaderText = "Shield", DataPropertyName = nameof(CombatEventRow.ShieldText), FillWeight = 62 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.FatalText), HeaderText = "Fatal", DataPropertyName = nameof(CombatEventRow.FatalText), FillWeight = 58 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.CriticalText), HeaderText = "Crit", DataPropertyName = nameof(CombatEventRow.CriticalText), FillWeight = 54 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CombatEventRow.LocationText), HeaderText = "Location", DataPropertyName = nameof(CombatEventRow.LocationText), FillWeight = 150 });
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private void BuildPlayerColumns()
    {
        dgvPlayers.Columns.Add(new DataGridViewLinkColumn { Name = nameof(PlayerSummaryRow.DisplayName), HeaderText = "Player", DataPropertyName = nameof(PlayerSummaryRow.DisplayName), FillWeight = 170, TrackVisitedState = false, UseColumnTextForLinkValue = false });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerSummaryRow.TeamText), HeaderText = "Team", DataPropertyName = nameof(PlayerSummaryRow.TeamText), FillWeight = 55 });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerSummaryRow.PlacementText), HeaderText = "Place", DataPropertyName = nameof(PlayerSummaryRow.PlacementText), FillWeight = 55 });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerSummaryRow.KillsText), HeaderText = "Kills", DataPropertyName = nameof(PlayerSummaryRow.KillsText), FillWeight = 55 });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerSummaryRow.Platform), HeaderText = "Platform", DataPropertyName = nameof(PlayerSummaryRow.Platform), FillWeight = 75 });
        dgvPlayers.Columns.Add(new DataGridViewCheckBoxColumn { Name = nameof(PlayerSummaryRow.IsBot), HeaderText = "Bot", DataPropertyName = nameof(PlayerSummaryRow.IsBot), FillWeight = 45 });
        dgvPlayers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }
    private void BuildPlayerVictimColumns()
    {
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerVictimRow.PlayerName), HeaderText = "Player", DataPropertyName = nameof(PlayerVictimRow.PlayerName), FillWeight = 170 });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerVictimRow.EventText), HeaderText = "Event", DataPropertyName = nameof(PlayerVictimRow.EventText), FillWeight = 80 });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerVictimRow.TimeText), HeaderText = "Time", DataPropertyName = nameof(PlayerVictimRow.TimeText), FillWeight = 70 });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(PlayerVictimRow.DistanceText), HeaderText = "Distance", DataPropertyName = nameof(PlayerVictimRow.DistanceText), FillWeight = 80 });
        dgvPlayerVictims.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private async Task RefreshReplayBrowserAsync()
    {
        btnRefreshReplays.Enabled = false;
        lblReplayStatus.Text = Directory.Exists(_replayFolder)
            ? $"Scanning {_replayFolder}"
            : $"Replay folder not found: {_replayFolder}";

        ClearReplayDetails();
        _replayRows.Clear();
        BindReplayRows();

        if (!Directory.Exists(_replayFolder))
        {
            btnRefreshReplays.Enabled = true;
            return;
        }

        var replayFiles = Directory.EnumerateFiles(_replayFolder, "*.replay", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        _replayRows.AddRange(replayFiles.Select(ReplayBrowserRow.CreateFromFile));
        BindReplayRows();

        lblReplayStatus.Text = _replayRows.Count == 0
            ? "No replay files found."
            : $"Found {_replayRows.Count} replay file(s). Loading selected replay...";

        btnRefreshReplays.Enabled = true;

        if (_replayRows.Count > 0 && dgvReplayBrowser.Rows.Count > 0)
        {
            _suppressReplaySelectionChanged = true;
            dgvReplayBrowser.ClearSelection();
            dgvReplayBrowser.Rows[0].Selected = true;
            dgvReplayBrowser.CurrentCell = dgvReplayBrowser.Rows[0].Cells[0];
            _selectedReplayRow = dgvReplayBrowser.Rows[0].DataBoundItem as ReplayBrowserRow;
            _suppressReplaySelectionChanged = false;
        }

        if (_selectedReplayRow is not null)
        {
            await EnsureReplayLoadedAsync(_selectedReplayRow, ParseMode.Full, updateSelectionView: true);
            _ = LoadReplaySummariesAsync(_selectedReplayRow);
            return;
        }

        lblReplayStatus.Text = "No replay files found.";
    }

    private async Task LoadReplaySummariesAsync(ReplayBrowserRow? skipRow = null)
    {
        try
        {
            foreach (var row in _replayRows.Where(r => !ReferenceEquals(r, skipRow) && !r.SummaryLoaded && !r.IsLoading).ToList())
            {
                await LoadReplayRowAsync(row, ParseMode.Minimal, updateSelectionView: false);
            }

            lblReplayStatus.Text = _replayRows.Count == 0
                ? "No replay files found."
                : $"Loaded {_replayRows.Count} replay file(s) from {_replayFolder}";
        }
        catch (Exception ex)
        {
            DebugOutputWriter.LogError("Background replay summary loading failed.", ex);
        }
    }

    private async Task HandleReplaySelectionChangedAsync()
    {
        if (_suppressReplaySelectionChanged || dgvReplayBrowser.CurrentRow?.DataBoundItem is not ReplayBrowserRow row)
        {
            return;
        }

        _selectedReplayRow = row;

        var requiredMode = ParseMode.Full;
        if (row.Replay is null || row.LoadedParseMode < requiredMode)
        {
            await EnsureReplayLoadedAsync(row, requiredMode, updateSelectionView: true);
            return;
        }

        DisplayReplay(row);
    }

    private async Task EnsureReplayLoadedAsync(ReplayBrowserRow row, ParseMode parseMode, bool updateSelectionView)
    {
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

        row.IsLoading = true;
        row.Status = parseMode == ParseMode.Full ? "Loading full replay..." : "Loading summary...";
        BindReplayRows();
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
            }

            ApplyReplaySummary(row, replay);
            row.Replay = replay;
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
            BindReplayRows();

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
        lblReplayStatus.Text = $"{row.FileName} loaded";
        lblPlayerPanelTitle.Text = "Player Stats";

        dgvGameStats.DataSource = BuildReplayDetails(row).ToList();
        BuildKillFeed(row.Replay);
        BuildCombatEvents(row.Replay);
        BuildPlayerList(row.Replay);

        var defaultPlayer = ResolveDefaultPlayer(row.Replay);
        ShowPlayerDetails(defaultPlayer);
    }

    private static PlayerData? ResolveDefaultPlayer(FortniteReplay replay)
    {
        return GetReplayOwner(replay)
            ?? replay.PlayerData?.OrderBy(p => p.Placement ?? int.MaxValue).ThenBy(p => p.Id ?? int.MaxValue).FirstOrDefault();
    }

    private IEnumerable<DetailRow> BuildReplayDetails(ReplayBrowserRow row)
    {
        var replay = row.Replay!;
        var replayOwner = GetReplayOwner(replay);

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
        yield return new DetailRow("Damage To Players", FormatNullable(replay.Stats?.DamageToPlayers));
        yield return new DetailRow("Damage Taken", FormatNullable(replay.Stats?.DamageTaken));
        yield return new DetailRow("Recorded Hits", replay.DamageEvents.Count.ToString(CultureInfo.CurrentCulture));
    }

    private void BuildKillFeed(FortniteReplay replay)
    {
        dgvKillFeed.DataSource = replay.KillFeed
            .Select(entry => CreateKillFeedRow(replay, entry))
            .OrderBy(entry => entry.TimeValue)
            .ToList();
    }

    private void BuildCombatEvents(FortniteReplay replay)
    {
        var rows = replay.DamageEvents
            .Where(evt => !(_chkTeamDamageOnly?.Checked ?? false) || InvolvesReplayOwnerTeam(replay, evt))
            .Select(evt => CreateCombatEventRow(replay, evt))
            .OrderBy(row => row.TimeValue)
            .ToList();

        dgvCombatEvents.DataSource = rows;
    }

    private KillFeedRow CreateKillFeedRow(FortniteReplay replay, KillFeedEntry entry)
    {
        var actorReference = ResolveKillFeedActorReference(replay, entry);
        var targetPlayer = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
        var timeValue = GetKillFeedTime(entry);

        return new KillFeedRow
        {
            Entry = entry,
            TimeValue = timeValue,
            TimeText = FormatMatchClock(timeValue),
            ActorName = ResolvePlayerName(actorReference.Player, actorReference.NumericId, actorReference.LookupKey),
            ActorId = actorReference.NumericId,
            ActorLookupKey = actorReference.LookupKey,
            TargetName = ResolvePlayerName(targetPlayer, entry.PlayerId, entry.PlayerName),
            TargetId = targetPlayer?.Id ?? entry.PlayerId,
            TargetLookupKey = targetPlayer?.PlayerId ?? entry.PlayerName,
            EventText = GetKillFeedEventText(entry),
            DistanceText = FormatDistance(entry.Distance)
        };
    }

    private CombatEventRow CreateCombatEventRow(FortniteReplay replay, DamageEvent evt)
    {
        var attacker = FindPlayer(replay, evt.InstigatorId, evt.InstigatorName);
        var target = FindPlayer(replay, evt.TargetId, evt.TargetName);
        var timeValue = GetDamageTime(evt);

        return new CombatEventRow
        {
            TimeValue = timeValue,
            TimeText = FormatMatchClock(timeValue),
            AttackerName = ResolveCombatantName(attacker, evt.InstigatorId, evt.InstigatorName, evt.InstigatorIsBot),
            AttackerId = attacker?.Id ?? evt.InstigatorId,
            AttackerLookupKey = attacker?.PlayerId ?? evt.InstigatorName,
            TargetName = ResolveCombatantName(target, evt.TargetId, evt.TargetName, evt.TargetIsBot),
            TargetId = target?.Id ?? evt.TargetId,
            TargetLookupKey = target?.PlayerId ?? evt.TargetName,
            EventText = evt.EventTag ?? evt.EventSource ?? "-",
            DamageText = evt.Magnitude.HasValue ? evt.Magnitude.Value.ToString("0.#", CultureInfo.CurrentCulture) : "-",
            ShieldText = evt.IsShield switch { true => "Yes", false => "No", _ => "-" },
            FatalText = FormatBool(evt.IsFatal),
            CriticalText = FormatBool(evt.IsCritical),
            LocationText = FormatVector(evt.Location)
        };
    }
    private void BuildPlayerList(FortniteReplay replay)
    {
        _playerRows.Clear();
        _playerRows.AddRange(replay.PlayerData.Select(player => new PlayerSummaryRow
        {
            Player = player,
            DisplayName = ResolvePlayerName(player, player.Id, player.PlayerId),
            Team = player.TeamIndex,
            TeamText = FormatNullable(player.TeamIndex),
            Placement = player.Placement,
            PlacementText = FormatNullable(player.Placement),
            Kills = player.Kills,
            KillsText = FormatNullable(player.Kills),
            Platform = string.IsNullOrWhiteSpace(player.Platform) ? "-" : player.Platform,
            IsBot = player.IsBot
        }));

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
        if (e.RowIndex < 0 || _selectedReplayRow?.Replay is null || dgvKillFeed.Rows[e.RowIndex].DataBoundItem is not KillFeedRow row)
        {
            return;
        }

        if (dgvKillFeed.Columns[e.ColumnIndex].Name == nameof(KillFeedRow.ActorName))
        {
            ShowPlayerDetails(FindPlayer(_selectedReplayRow.Replay, row.ActorId, row.ActorLookupKey));
        }

        if (dgvKillFeed.Columns[e.ColumnIndex].Name == nameof(KillFeedRow.TargetName))
        {
            ShowPlayerDetails(FindPlayer(_selectedReplayRow.Replay, row.TargetId, row.TargetLookupKey));
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
        _selectedPlayer = player;

        if (_selectedReplayRow?.Replay is null || player is null)
        {
            lblSelectedPlayer.Text = "Select a player to inspect their stats.";
            dgvPlayerOverview.DataSource = new List<DetailRow>();
            dgvPlayerCombatLog.DataSource = new List<KillFeedRow>();
            dgvPlayerVictims.DataSource = new List<PlayerVictimRow>();
            if (_dgvPlayerDamageLog is not null)
            {
                _dgvPlayerDamageLog.DataSource = new List<CombatEventRow>();
            }
            return;
        }

        lblSelectedPlayer.Text = ResolvePlayerName(player, player.Id, player.PlayerId);
        lblPlayerPanelTitle.Text = $"Player Stats - {ResolvePlayerName(player, player.Id, player.PlayerId)}";
        dgvPlayerOverview.DataSource = BuildPlayerOverview(_selectedReplayRow.Replay, player).ToList();
        dgvPlayerCombatLog.DataSource = BuildPlayerCombatLog(_selectedReplayRow.Replay, player).ToList();
        dgvPlayerVictims.DataSource = BuildPlayerVictimRows(_selectedReplayRow.Replay, player).ToList();
        if (_dgvPlayerDamageLog is not null)
        {
            _dgvPlayerDamageLog.DataSource = BuildPlayerDamageRows(_selectedReplayRow.Replay, player).ToList();
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
        yield return new DetailRow("Death Time", deathEvent is null ? "-" : FormatMatchClock(GetKillFeedTime(deathEvent)));
        yield return new DetailRow("Eliminated By", !eliminatedBy.HasValue ? "-" : ResolvePlayerName(eliminatedBy.Value.Player, eliminatedBy.Value.NumericId, eliminatedBy.Value.LookupKey));
        yield return new DetailRow("Damage Events Given", hitsGiven.ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Damage Events Taken", hitsTaken.ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Damage To Players", FormatDamageTotal(damageDealt.Players));
        yield return new DetailRow("Damage To Bots", FormatDamageTotal(damageDealt.Bots));
        yield return new DetailRow("Damage To NPCs", FormatDamageTotal(damageDealt.Npcs));
        yield return new DetailRow("Damage To Structures", FormatDamageTotal(damageDealt.Structures));
        yield return new DetailRow("Damage Taken From Players", FormatDamageTotal(damageTaken.Players));
        yield return new DetailRow("Damage Taken From Bots", FormatDamageTotal(damageTaken.Bots));
        yield return new DetailRow("Damage Taken From NPCs", FormatDamageTotal(damageTaken.Npcs));
        yield return new DetailRow("Damage Taken From World", FormatDamageTotal(damageTaken.Structures));
    }

    private IEnumerable<KillFeedRow> BuildPlayerCombatLog(FortniteReplay replay, PlayerData player)
    {
        return replay.KillFeed
            .Where(entry => MatchesPlayer(player, entry.PlayerId, entry.PlayerName) || MatchesResolvedKillFeedActor(replay, player, entry))
            .Select(entry => CreateKillFeedRow(replay, entry))
            .OrderBy(entry => entry.TimeValue);
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
                    PlayerName = ResolvePlayerName(victim, entry.PlayerId, entry.PlayerName),
                    EventText = GetKillFeedEventText(entry),
                    TimeText = FormatMatchClock(GetKillFeedTime(entry)),
                    DistanceText = FormatDistance(entry.Distance)
                };
            });
    }

    private IEnumerable<CombatEventRow> BuildPlayerDamageRows(FortniteReplay replay, PlayerData player)
    {
        return replay.DamageEvents
            .Where(evt => MatchesPlayer(player, evt.InstigatorId, evt.InstigatorName) || MatchesPlayer(player, evt.TargetId, evt.TargetName))
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
        if (_dgvPlayerDamageLog is not null)
        {
            _dgvPlayerDamageLog.DataSource = new List<CombatEventRow>();
        }
        lblSelectedPlayer.Text = "Select a player to inspect their stats.";
        lblPlayerPanelTitle.Text = "Player Stats";
    }

    private void BindReplayRows()
    {
        CaptureReplayBrowserColumnWidths();

        var currentPath = _selectedReplayRow?.FilePath;
        var currentScroll = dgvReplayBrowser.FirstDisplayedScrollingRowIndex;
        var ordered = OrderReplayRows().ToList();

        _suppressReplaySelectionChanged = true;
        dgvReplayBrowser.DataSource = ordered;
        RestoreReplayBrowserColumnWidths();

        if (currentPath is not null)
        {
            foreach (DataGridViewRow gridRow in dgvReplayBrowser.Rows)
            {
                if ((gridRow.DataBoundItem as ReplayBrowserRow)?.FilePath == currentPath)
                {
                    gridRow.Selected = true;
                    dgvReplayBrowser.CurrentCell = gridRow.Cells[0];
                    break;
                }
            }
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
        _isReplayPaneCollapsed = collapsed;
        dgvReplayBrowser.Visible = !collapsed;
        lblReplayStatus.Visible = !collapsed;
        lblReplayHeader.Text = collapsed ? "Replays" : "Replay Browser";
        splitMain.SplitterDistance = collapsed ? CollapsedReplayPaneWidth : ExpandedReplayPaneWidth;
    }

    private static PlayerData? GetReplayOwner(FortniteReplay replay) => replay.PlayerData?.FirstOrDefault(player => player.IsReplayOwner);

    private static PlayerData? FindPlayer(FortniteReplay replay, int? playerId, string? playerLookupKey)
    {
        return replay.PlayerData?.FirstOrDefault(player =>
            (playerId.HasValue && player.Id == playerId)
            || (!string.IsNullOrWhiteSpace(playerLookupKey) && string.Equals(player.PlayerId, playerLookupKey, StringComparison.OrdinalIgnoreCase)));
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

    private static bool MatchesResolvedKillFeedActor(FortniteReplay replay, PlayerData player, KillFeedEntry entry)
    {
        var actorReference = ResolveKillFeedActorReference(replay, entry);
        return MatchesPlayer(player, actorReference.NumericId, actorReference.LookupKey);
    }

    private static (PlayerData? Player, int? NumericId, string? LookupKey) ResolveKillFeedActorReference(FortniteReplay replay, KillFeedEntry entry)
    {
        var directActor = FindPlayer(replay, entry.FinisherOrDowner, entry.FinisherOrDownerName);
        if (directActor is not null || entry.FinisherOrDowner.HasValue || !string.IsNullOrWhiteSpace(entry.FinisherOrDownerName))
        {
            return (directActor, directActor?.Id ?? entry.FinisherOrDowner, directActor?.PlayerId ?? entry.FinisherOrDownerName);
        }

        if (entry.IsDowned || entry.IsRevived)
        {
            return (null, null, null);
        }

        var currentTime = GetKillFeedTime(entry);
        var priorEntries = replay.KillFeed
            .Where(candidate => !ReferenceEquals(candidate, entry) && MatchesKillFeedTarget(candidate, entry.PlayerId, entry.PlayerName) && GetKillFeedTime(candidate) <= currentTime)
            .OrderByDescending(GetKillFeedTime);

        foreach (var priorEntry in priorEntries)
        {
            if (priorEntry.IsRevived)
            {
                break;
            }

            if (priorEntry.IsDowned)
            {
                var inferredActor = FindPlayer(replay, priorEntry.FinisherOrDowner, priorEntry.FinisherOrDownerName);
                return (inferredActor, inferredActor?.Id ?? priorEntry.FinisherOrDowner, inferredActor?.PlayerId ?? priorEntry.FinisherOrDownerName);
            }

            break;
        }

        return (null, null, null);
    }

    private static string ResolveCombatantName(PlayerData? player, int? numericId, string? fallback = null, bool isBot = false)
    {
        if (player is not null)
        {
            return ResolvePlayerName(player, numericId, fallback);
        }

        if (isBot && numericId.HasValue)
        {
            return $"Bot {numericId.Value:000}";
        }

        if (numericId.HasValue)
        {
            return numericId.Value < 1000 ? $"NPC {numericId.Value:000}" : $"Structure {numericId.Value}";
        }

        return ShortenIdentifier(fallback);
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

    private static string ShortenTabTitle(string fileName)
    {
        const int maxLength = 34;
        return fileName.Length <= maxLength ? fileName : fileName[..31] + "...";
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

    private static bool InvolvesReplayOwnerTeam(FortniteReplay replay, DamageEvent evt)
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

    private static DamageTotals SummarizeDamage(FortniteReplay replay, IEnumerable<DamageEvent> events, bool targetPerspective)
    {
        var totals = new DamageTotals();
        foreach (var evt in events)
        {
            var amount = evt.Magnitude.GetValueOrDefault();
            if (amount <= 0)
            {
                continue;
            }

            var category = targetPerspective
                ? ClassifyDamageParticipant(replay, evt.TargetId, evt.TargetName, evt.TargetIsBot)
                : ClassifyDamageParticipant(replay, evt.InstigatorId, evt.InstigatorName, evt.InstigatorIsBot);

            switch (category)
            {
                case DamageParticipantCategory.Player:
                    totals.Players += amount;
                    break;
                case DamageParticipantCategory.Bot:
                    totals.Bots += amount;
                    break;
                case DamageParticipantCategory.Npc:
                    totals.Npcs += amount;
                    break;
                default:
                    totals.Structures += amount;
                    break;
            }
        }

        return totals;
    }

    private static DamageParticipantCategory ClassifyDamageParticipant(FortniteReplay replay, int? numericId, string? lookupKey, bool isBot)
    {
        var player = FindPlayer(replay, numericId, lookupKey);
        if (player is not null)
        {
            return player.IsBot ? DamageParticipantCategory.Bot : DamageParticipantCategory.Player;
        }

        if (isBot)
        {
            return DamageParticipantCategory.Bot;
        }

        if (numericId.HasValue && numericId.Value < 1000)
        {
            return DamageParticipantCategory.Npc;
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

    private static double GetKillFeedTime(KillFeedEntry entry) => entry.ReplicatedWorldTimeSecondsDouble ?? entry.ReplicatedWorldTimeSeconds ?? 0;

    private static double GetDamageTime(DamageEvent evt) => evt.ReplicatedWorldTimeSecondsDouble ?? evt.ReplicatedWorldTimeSeconds ?? 0;

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

    private enum DamageParticipantCategory
    {
        Player,
        Bot,
        Npc,
        Structure
    }

    private sealed record ReplayLoadResult(FortniteReplay? Replay, Exception? Exception);
}







