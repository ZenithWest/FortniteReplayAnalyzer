using FortniteReplayReader;
using FortniteReplayReader.Models;
using System.Globalization;
using Unreal.Core.Models.Enums;

namespace FortniteReplayAnalyzer;

public partial class FortniteReplayAnalyzer : Form
{
    private const int ExpandedReplayPaneWidth = 360;
    private const int CollapsedReplayPaneWidth = 230;

    private readonly string _replayFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FortniteGame",
        "Saved",
        "Demos");

    private readonly List<ReplayBrowserRow> _replayRows = [];
    private readonly List<PlayerSummaryRow> _playerRows = [];

    private string _replaySortColumn = nameof(ReplayBrowserRow.RecordedAt);
    private bool _replaySortAscending;
    private string _playerSortColumn = nameof(PlayerSummaryRow.Kills);
    private bool _playerSortAscending;
    private bool _isReplayPaneCollapsed;

    private ReplayBrowserRow? _selectedReplayRow;
    private PlayerData? _selectedPlayer;

    public FortniteReplayAnalyzer()
    {
        InitializeComponent();
        ConfigureGrids();
        WireEvents();
    }

    private void WireEvents()
    {
        Shown += async (_, _) => await RefreshReplayBrowserAsync();
        btnRefreshReplays.Click += async (_, _) => await RefreshReplayBrowserAsync();
        btnToggleReplayPane.Click += (_, _) => SetReplayPaneCollapsed(!_isReplayPaneCollapsed);

        dgvReplayBrowser.SelectionChanged += (_, _) => HandleReplaySelectionChanged();
        dgvReplayBrowser.ColumnHeaderMouseClick += (_, e) => SortReplayRows(dgvReplayBrowser.Columns[e.ColumnIndex].Name);
        dgvReplayBrowser.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                SetReplayPaneCollapsed(true);
            }
        };

        dgvKillFeed.CellContentClick += (_, e) => HandleKillFeedLinkClick(e);
        dgvPlayers.CellContentClick += (_, e) => HandlePlayerLinkClick(e);
        dgvPlayers.SelectionChanged += (_, _) => HandlePlayerSelectionChanged();
        dgvPlayers.ColumnHeaderMouseClick += (_, e) => SortPlayerRows(dgvPlayers.Columns[e.ColumnIndex].Name);
    }

    private void ConfigureGrids()
    {
        ConfigureReadOnlyGrid(dgvReplayBrowser, fullRowSelect: true);
        ConfigureReadOnlyGrid(dgvGameStats, fullRowSelect: false);
        ConfigureReadOnlyGrid(dgvKillFeed, fullRowSelect: true);
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
        dgvPlayers.AutoGenerateColumns = false;
        dgvPlayerOverview.AutoGenerateColumns = false;
        dgvPlayerCombatLog.AutoGenerateColumns = false;
        dgvPlayerVictims.AutoGenerateColumns = false;

        BuildReplayBrowserColumns();
        BuildGameStatsColumns();
        BuildKillFeedColumns(dgvKillFeed);
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
        grid.BackgroundColor = SystemColors.Window;
        grid.BorderStyle = BorderStyle.None;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = fullRowSelect ? DataGridViewSelectionMode.FullRowSelect : DataGridViewSelectionMode.CellSelect;
        grid.Dock = DockStyle.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
    }

    private void BuildReplayBrowserColumns()
    {
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(ReplayBrowserRow.FileName),
            HeaderText = "Replay",
            DataPropertyName = nameof(ReplayBrowserRow.FileName),
            FillWeight = 180
        });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(ReplayBrowserRow.RecordedAtText),
            HeaderText = "Played",
            DataPropertyName = nameof(ReplayBrowserRow.RecordedAtText),
            FillWeight = 110
        });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(ReplayBrowserRow.DurationText),
            HeaderText = "Length",
            DataPropertyName = nameof(ReplayBrowserRow.DurationText),
            FillWeight = 70
        });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(ReplayBrowserRow.PlacementText),
            HeaderText = "Place",
            DataPropertyName = nameof(ReplayBrowserRow.PlacementText),
            FillWeight = 55
        });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(ReplayBrowserRow.KillsText),
            HeaderText = "Kills",
            DataPropertyName = nameof(ReplayBrowserRow.KillsText),
            FillWeight = 55
        });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(ReplayBrowserRow.PlayerCountText),
            HeaderText = "Players",
            DataPropertyName = nameof(ReplayBrowserRow.PlayerCountText),
            FillWeight = 65
        });
        dgvReplayBrowser.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(ReplayBrowserRow.Status),
            HeaderText = "Status",
            DataPropertyName = nameof(ReplayBrowserRow.Status),
            FillWeight = 90
        });

        dgvReplayBrowser.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private void BuildGameStatsColumns() => BuildGameStatsColumns(dgvGameStats);

    private static void BuildGameStatsColumns(DataGridView grid)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(DetailRow.Label),
            DataPropertyName = nameof(DetailRow.Label),
            FillWeight = 40
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(DetailRow.Value),
            DataPropertyName = nameof(DetailRow.Value),
            FillWeight = 60
        });
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private static void BuildKillFeedColumns(DataGridView grid)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(KillFeedRow.TimeText),
            HeaderText = "Time",
            DataPropertyName = nameof(KillFeedRow.TimeText),
            FillWeight = 65
        });
        grid.Columns.Add(new DataGridViewLinkColumn
        {
            Name = nameof(KillFeedRow.ActorName),
            HeaderText = "Actor",
            DataPropertyName = nameof(KillFeedRow.ActorName),
            FillWeight = 130,
            TrackVisitedState = false,
            UseColumnTextForLinkValue = false
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(KillFeedRow.EventText),
            HeaderText = "Event",
            DataPropertyName = nameof(KillFeedRow.EventText),
            FillWeight = 85
        });
        grid.Columns.Add(new DataGridViewLinkColumn
        {
            Name = nameof(KillFeedRow.TargetName),
            HeaderText = "Target",
            DataPropertyName = nameof(KillFeedRow.TargetName),
            FillWeight = 130,
            TrackVisitedState = false,
            UseColumnTextForLinkValue = false
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(KillFeedRow.DistanceText),
            HeaderText = "Distance",
            DataPropertyName = nameof(KillFeedRow.DistanceText),
            FillWeight = 70
        });
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private void BuildPlayerColumns()
    {
        dgvPlayers.Columns.Add(new DataGridViewLinkColumn
        {
            Name = nameof(PlayerSummaryRow.DisplayName),
            HeaderText = "Player",
            DataPropertyName = nameof(PlayerSummaryRow.DisplayName),
            FillWeight = 170,
            TrackVisitedState = false,
            UseColumnTextForLinkValue = false
        });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(PlayerSummaryRow.TeamText),
            HeaderText = "Team",
            DataPropertyName = nameof(PlayerSummaryRow.TeamText),
            FillWeight = 55
        });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(PlayerSummaryRow.PlacementText),
            HeaderText = "Place",
            DataPropertyName = nameof(PlayerSummaryRow.PlacementText),
            FillWeight = 55
        });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(PlayerSummaryRow.KillsText),
            HeaderText = "Kills",
            DataPropertyName = nameof(PlayerSummaryRow.KillsText),
            FillWeight = 55
        });
        dgvPlayers.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(PlayerSummaryRow.Platform),
            HeaderText = "Platform",
            DataPropertyName = nameof(PlayerSummaryRow.Platform),
            FillWeight = 75
        });
        dgvPlayers.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = nameof(PlayerSummaryRow.IsBot),
            HeaderText = "Bot",
            DataPropertyName = nameof(PlayerSummaryRow.IsBot),
            FillWeight = 45
        });
        dgvPlayers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private void BuildPlayerVictimColumns()
    {
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(PlayerVictimRow.PlayerName),
            HeaderText = "Player",
            DataPropertyName = nameof(PlayerVictimRow.PlayerName),
            FillWeight = 170
        });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(PlayerVictimRow.EventText),
            HeaderText = "Event",
            DataPropertyName = nameof(PlayerVictimRow.EventText),
            FillWeight = 80
        });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(PlayerVictimRow.TimeText),
            HeaderText = "Time",
            DataPropertyName = nameof(PlayerVictimRow.TimeText),
            FillWeight = 70
        });
        dgvPlayerVictims.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(PlayerVictimRow.DistanceText),
            HeaderText = "Distance",
            DataPropertyName = nameof(PlayerVictimRow.DistanceText),
            FillWeight = 80
        });
        dgvPlayerVictims.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private async Task RefreshReplayBrowserAsync()
    {
        btnRefreshReplays.Enabled = false;
        lblReplayStatus.Text = Directory.Exists(_replayFolder)
            ? $"Loading replays from {_replayFolder}"
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

        var rows = await Task.Run(() =>
        {
            var list = new List<ReplayBrowserRow>();

            foreach (var replayFile in replayFiles)
            {
                try
                {
                    var replay = new ReplayReader(parseMode: ParseMode.Normal).ReadReplay(replayFile);
                    list.Add(CreateReplayBrowserRow(replayFile, replay));
                }
                catch (Exception ex)
                {
                    list.Add(ReplayBrowserRow.CreateError(replayFile, ex.Message));
                }
            }

            return list;
        });

        _replayRows.AddRange(rows);
        BindReplayRows();
        lblReplayStatus.Text = _replayRows.Count == 0
            ? "No replay files found."
            : $"Loaded {_replayRows.Count} replay file(s) from {_replayFolder}";
        btnRefreshReplays.Enabled = true;
    }

    private ReplayBrowserRow CreateReplayBrowserRow(string filePath, FortniteReplay replay)
    {
        var replayOwner = GetReplayOwner(replay);
        var timestamp = replay.Info.Timestamp != default
            ? replay.Info.Timestamp.ToLocalTime()
            : File.GetLastWriteTime(filePath);

        var placement = replayOwner?.Placement ?? (int?)replay.TeamStats?.Position;
        var kills = replayOwner?.Kills ?? replay.Stats?.Eliminations;
        var players = replay.PlayerData?.Count() ?? 0;

        return new ReplayBrowserRow
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            RecordedAt = timestamp,
            RecordedAtText = timestamp.ToString("g", CultureInfo.CurrentCulture),
            Duration = TimeSpan.FromMilliseconds(replay.Info.LengthInMs),
            DurationText = FormatDuration(TimeSpan.FromMilliseconds(replay.Info.LengthInMs)),
            Placement = placement,
            PlacementText = FormatNullable(placement),
            Kills = kills,
            KillsText = FormatNullable(kills),
            PlayerCount = players,
            PlayerCountText = players.ToString(CultureInfo.CurrentCulture),
            Replay = replay,
            Status = "Ready"
        };
    }

    private void HandleReplaySelectionChanged()
    {
        if (dgvReplayBrowser.CurrentRow?.DataBoundItem is not ReplayBrowserRow row)
        {
            return;
        }

        _selectedReplayRow = row;
        DisplayReplay(row);

        if (!_isReplayPaneCollapsed && row.Replay is not null)
        {
            SetReplayPaneCollapsed(true);
        }
    }

    private void DisplayReplay(ReplayBrowserRow row)
    {
        if (row.Replay is null)
        {
            lblReplayStatus.Text = $"Unable to parse {row.FileName}: {row.Status}";
            ClearReplayDetails();
            return;
        }

        lblReplayStatus.Text = $"{row.FileName} loaded";
        lblPlayerPanelTitle.Text = "Player Stats";

        dgvGameStats.DataSource = BuildReplayDetails(row).ToList();
        BuildKillFeed(row.Replay);
        BuildPlayerList(row.Replay);

        var defaultPlayer = GetReplayOwner(row.Replay) ?? row.Replay.PlayerData?.OrderBy(p => p.Placement ?? int.MaxValue).FirstOrDefault();
        ShowPlayerDetails(defaultPlayer);
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
        yield return new DetailRow("Revives", FormatNullable(replay.Stats?.Revives));
    }

    private void BuildKillFeed(FortniteReplay replay)
    {
        var rows = replay.KillFeed
            .Select(entry => CreateKillFeedRow(replay, entry))
            .OrderBy(entry => entry.TimeValue)
            .ToList();

        dgvKillFeed.DataSource = rows;
    }

    private KillFeedRow CreateKillFeedRow(FortniteReplay replay, KillFeedEntry entry)
    {
        var actorPlayer = FindPlayer(replay, entry.FinisherOrDowner, entry.FinisherOrDownerName);
        var targetPlayer = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
        var timeValue = entry.ReplicatedWorldTimeSecondsDouble ?? entry.ReplicatedWorldTimeSeconds ?? 0;

        return new KillFeedRow
        {
            Entry = entry,
            TimeValue = timeValue,
            TimeText = FormatMatchClock(timeValue),
            ActorName = ResolvePlayerName(actorPlayer, entry.FinisherOrDownerName),
            ActorId = actorPlayer?.Id ?? entry.FinisherOrDowner,
            ActorLookupKey = actorPlayer?.PlayerId ?? entry.FinisherOrDownerName,
            TargetName = ResolvePlayerName(targetPlayer, entry.PlayerName),
            TargetId = targetPlayer?.Id ?? entry.PlayerId,
            TargetLookupKey = targetPlayer?.PlayerId ?? entry.PlayerName,
            EventText = GetKillFeedEventText(entry),
            DistanceText = entry.Distance.HasValue ? $"{entry.Distance.Value:0} m" : "-"
        };
    }

    private void BuildPlayerList(FortniteReplay replay)
    {
        _playerRows.Clear();
        _playerRows.AddRange(replay.PlayerData
            .Select(player => new PlayerSummaryRow
            {
                Player = player,
                DisplayName = ResolvePlayerName(player),
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

    private void ShowPlayerDetails(PlayerData? player)
    {
        _selectedPlayer = player;

        if (_selectedReplayRow?.Replay is null || player is null)
        {
            lblSelectedPlayer.Text = "Select a player to inspect their stats.";
            dgvPlayerOverview.DataSource = new List<DetailRow>();
            dgvPlayerCombatLog.DataSource = new List<KillFeedRow>();
            dgvPlayerVictims.DataSource = new List<PlayerVictimRow>();
            return;
        }

        lblSelectedPlayer.Text = ResolvePlayerName(player);
        lblPlayerPanelTitle.Text = $"Player Stats - {ResolvePlayerName(player)}";
        dgvPlayerOverview.DataSource = BuildPlayerOverview(_selectedReplayRow.Replay, player).ToList();
        dgvPlayerCombatLog.DataSource = BuildPlayerCombatLog(_selectedReplayRow.Replay, player).ToList();
        dgvPlayerVictims.DataSource = BuildPlayerVictimRows(_selectedReplayRow.Replay, player).ToList();
    }

    private IEnumerable<DetailRow> BuildPlayerOverview(FortniteReplay replay, PlayerData player)
    {
        var deathEvent = replay.KillFeed
            .Where(entry => MatchesPlayer(player, entry.PlayerId, entry.PlayerName) && !entry.IsRevived)
            .OrderByDescending(entry => entry.ReplicatedWorldTimeSecondsDouble ?? entry.ReplicatedWorldTimeSeconds ?? 0)
            .FirstOrDefault();

        yield return new DetailRow("Name", ResolvePlayerName(player));
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
        yield return new DetailRow("Death Time", deathEvent is null ? "-" : FormatMatchClock(deathEvent.ReplicatedWorldTimeSecondsDouble ?? deathEvent.ReplicatedWorldTimeSeconds ?? 0));
        yield return new DetailRow("Eliminated By", deathEvent is null ? "-" : ResolvePlayerName(FindPlayer(replay, deathEvent.FinisherOrDowner, deathEvent.FinisherOrDownerName), deathEvent.FinisherOrDownerName));
        yield return new DetailRow("Damage", "Per-player damage is not exposed by the parser yet");
    }

    private IEnumerable<KillFeedRow> BuildPlayerCombatLog(FortniteReplay replay, PlayerData player)
    {
        return replay.KillFeed
            .Where(entry => MatchesPlayer(player, entry.PlayerId, entry.PlayerName) || MatchesPlayer(player, entry.FinisherOrDowner, entry.FinisherOrDownerName))
            .Select(entry => CreateKillFeedRow(replay, entry))
            .OrderBy(entry => entry.TimeValue);
    }

    private IEnumerable<PlayerVictimRow> BuildPlayerVictimRows(FortniteReplay replay, PlayerData player)
    {
        return replay.KillFeed
            .Where(entry => MatchesPlayer(player, entry.FinisherOrDowner, entry.FinisherOrDownerName))
            .OrderBy(entry => entry.ReplicatedWorldTimeSecondsDouble ?? entry.ReplicatedWorldTimeSeconds ?? 0)
            .Select(entry =>
            {
                var victim = FindPlayer(replay, entry.PlayerId, entry.PlayerName);
                return new PlayerVictimRow
                {
                    PlayerName = ResolvePlayerName(victim, entry.PlayerName),
                    EventText = GetKillFeedEventText(entry),
                    TimeText = FormatMatchClock(entry.ReplicatedWorldTimeSecondsDouble ?? entry.ReplicatedWorldTimeSeconds ?? 0),
                    DistanceText = entry.Distance.HasValue ? $"{entry.Distance.Value:0} m" : "-"
                };
            });
    }

    private void ClearReplayDetails()
    {
        _selectedReplayRow = null;
        _selectedPlayer = null;
        dgvGameStats.DataSource = new List<DetailRow>();
        dgvKillFeed.DataSource = new List<KillFeedRow>();
        dgvPlayers.DataSource = new List<PlayerSummaryRow>();
        dgvPlayerOverview.DataSource = new List<DetailRow>();
        dgvPlayerCombatLog.DataSource = new List<KillFeedRow>();
        dgvPlayerVictims.DataSource = new List<PlayerVictimRow>();
        lblSelectedPlayer.Text = "Select a player to inspect their stats.";
        lblPlayerPanelTitle.Text = "Player Stats";
    }

    private void BindReplayRows()
    {
        var currentPath = _selectedReplayRow?.FilePath;
        var ordered = OrderReplayRows().ToList();
        dgvReplayBrowser.DataSource = ordered;

        if (currentPath is null)
        {
            return;
        }

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

        return _replaySortAscending
            ? _replayRows.OrderBy(selector).ThenBy(row => row.FileName)
            : _replayRows.OrderByDescending(selector).ThenBy(row => row.FileName);
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

        var ordered = (_playerSortAscending
            ? _playerRows.OrderBy(selector).ThenBy(row => row.DisplayName)
            : _playerRows.OrderByDescending(selector).ThenBy(row => row.DisplayName))
            .ToList();

        dgvPlayers.DataSource = ordered;

        if (selectedKey is null)
        {
            return;
        }

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

    private void SetReplayPaneCollapsed(bool collapsed)
    {
        _isReplayPaneCollapsed = collapsed;
        splitMain.SplitterDistance = collapsed ? CollapsedReplayPaneWidth : ExpandedReplayPaneWidth;
        btnToggleReplayPane.Text = collapsed ? "Expand" : "Collapse";
    }

    private static PlayerData? GetReplayOwner(FortniteReplay replay) =>
        replay.PlayerData?.FirstOrDefault(player => player.IsReplayOwner);

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

        return !string.IsNullOrWhiteSpace(playerLookupKey)
            && string.Equals(player.PlayerId, playerLookupKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePlayerName(PlayerData? player, string? fallback = null)
    {
        if (player is null)
        {
            return ShortenIdentifier(fallback);
        }

        var preferred = player.PlayerName
            ?? player.PlayerNameCustomOverride
            ?? player.StreamerModeName
            ?? player.PlayerId;

        return ShortenIdentifier(preferred);
    }

    private static string ShortenIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        return value.Length > 18 && !value.Contains(' ', StringComparison.Ordinal)
            ? value[..8] + "..." + value[^4..]
            : value;
    }

    private static string GetKillFeedEventText(KillFeedEntry entry)
    {
        if (entry.IsRevived)
        {
            return "Revived";
        }

        return entry.IsDowned ? "Downed" : "Eliminated";
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

    private static string FormatBool(bool? value) => value switch
    {
        true => "Yes",
        false => "No",
        _ => "-"
    };

    private static string FormatMatchClock(double seconds)
    {
        if (seconds <= 0)
        {
            return "-";
        }

        return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "-";
        }

        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}
