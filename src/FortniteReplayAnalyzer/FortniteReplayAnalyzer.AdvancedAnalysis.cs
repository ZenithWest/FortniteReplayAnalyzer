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
    private CancellationTokenSource? _weaponStatsLoadCts;
    private ComboBox? _cmbOverallRange;
    private DateTimePicker? _dtOverallFrom;
    private DateTimePicker? _dtOverallTo;
    private Label? _lblOverallStatsStatus;
    private DataGridView? _dgvOverallStats;
    private Button? _btnOverallStatsStop;
    private CancellationTokenSource? _overallStatsLoadCts;
    private Panel? _damageTimelinePanel;
    private CheckBox? _chkTimelinePlayers;
    private CheckBox? _chkTimelineBots;
    private CheckBox? _chkTimelineTeam;
    private CheckBox? _chkTimelineCumulative;
    private List<(string Name, List<DamageTimelinePoint> Points)> _timelineSeries = [];

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
            RowCount = 3
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var filters = CreateFilterFlowPanel();
        filters.AutoScroll = false;

        _cmbWeaponRange = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        _cmbWeaponRange.Items.AddRange(["All-time", "Last 7 days", "Last 30 days", "Custom"]);
        _cmbWeaponRange.SelectedIndex = 0;
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

        _lblWeaponStatsStatus = new Label
        {
            AutoSize = true,
            Text = "Weapon stats load on demand for the selected date range."
        };

        _dgvWeaponStats = new DataGridView { Name = "dgvWeaponStats" };
        ConfigureReadOnlyGrid(_dgvWeaponStats, fullRowSelect: true);
        _dgvWeaponStats.AutoGenerateColumns = false;
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.WeaponType), HeaderText = "Weapon Type", DataPropertyName = nameof(WeaponStatsRow.WeaponType), FillWeight = 110 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.WeaponName), HeaderText = "Weapon", DataPropertyName = nameof(WeaponStatsRow.WeaponName), FillWeight = 180 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.Hits), HeaderText = "Hits", DataPropertyName = nameof(WeaponStatsRow.Hits), FillWeight = 65 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.CriticalHits), HeaderText = "Crit Hits", DataPropertyName = nameof(WeaponStatsRow.CriticalHits), FillWeight = 72 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.FatalHits), HeaderText = "Fatal", DataPropertyName = nameof(WeaponStatsRow.FatalHits), FillWeight = 62 });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.TotalDamage), HeaderText = "Damage", DataPropertyName = nameof(WeaponStatsRow.TotalDamage), FillWeight = 84, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.AvgDamage), HeaderText = "Avg Hit", DataPropertyName = nameof(WeaponStatsRow.AvgDamage), FillWeight = 84, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#" } });
        _dgvWeaponStats.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(WeaponStatsRow.CriticalRate), HeaderText = "Crit %", DataPropertyName = nameof(WeaponStatsRow.CriticalRate), FillWeight = 70, DefaultCellStyle = new DataGridViewCellStyle { Format = "0.#'%'"} });
        _dgvWeaponStats.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        page.Controls.Add(filters, 0, 0);
        page.Controls.Add(_lblWeaponStatsStatus, 0, 1);
        page.Controls.Add(_dgvWeaponStats, 0, 2);
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

        _lblOverallStatsStatus = new Label
        {
            AutoSize = true,
            Text = "Overall stats are generated from replay-owner data in the selected range."
        };

        _dgvOverallStats = new DataGridView { Name = "dgvOverallStats" };
        ConfigureReadOnlyGrid(_dgvOverallStats, fullRowSelect: false);
        _dgvOverallStats.AutoGenerateColumns = false;
        BuildGameStatsColumns(_dgvOverallStats);

        page.Controls.Add(filters, 0, 0);
        page.Controls.Add(_lblOverallStatsStatus, 0, 1);
        page.Controls.Add(_dgvOverallStats, 0, 2);
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
        if (_dgvWeaponStats is null || _lblWeaponStatsStatus is null)
        {
            return;
        }

        ToggleAggregateDatePickers(_cmbWeaponRange, _dtWeaponFrom, _dtWeaponTo);
        _weaponStatsLoadCts?.Cancel();
        _weaponStatsLoadCts = new CancellationTokenSource();
        var cancellationToken = _weaponStatsLoadCts.Token;
        _lblWeaponStatsStatus.Text = "Building weapon stats from already-loaded replays...";
        if (_btnWeaponStatsStop is not null) _btnWeaponStatsStop.Enabled = true;

        try
        {
            var rows = await GetAggregateReplayRowsAsync(_cmbWeaponRange, _dtWeaponFrom, _dtWeaponTo, cancellationToken);
            var weaponRows = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return rows
                    .SelectMany(BuildWeaponStatsRowsForReplay)
                    .GroupBy(row => new { row.WeaponType, row.WeaponName })
                    .Select(group =>
                    {
                        var hits = group.Sum(x => x.Hits);
                        var crits = group.Sum(x => x.CriticalHits);
                        var damage = group.Sum(x => x.TotalDamage);
                        return new WeaponStatsRow
                        {
                            WeaponType = group.Key.WeaponType,
                            WeaponName = group.Key.WeaponName,
                            Hits = hits,
                            CriticalHits = crits,
                            FatalHits = group.Sum(x => x.FatalHits),
                            TotalDamage = damage,
                            AvgDamage = hits == 0 ? 0F : damage / hits,
                            CriticalRate = hits == 0 ? 0F : (float)crits / hits * 100F
                        };
                    })
                    .OrderByDescending(row => row.TotalDamage)
                    .ThenBy(row => row.WeaponType)
                    .ToList();
            }, cancellationToken);

            _dgvWeaponStats.DataSource = weaponRows;
            _lblWeaponStatsStatus.Text = rows.Count == 0
                ? "No loaded replays in range."
                : $"Weapon stats built from {rows.Count} loaded replay(s).";
        }
        catch (OperationCanceledException)
        {
            _lblWeaponStatsStatus.Text = "Weapon stats refresh stopped.";
        }
        finally
        {
            if (_btnWeaponStatsStop is not null) _btnWeaponStatsStop.Enabled = false;
        }
    }

    private async Task RefreshOverallStatsPageAsync()
    {
        if (_dgvOverallStats is null || _lblOverallStatsStatus is null)
        {
            return;
        }

        ToggleAggregateDatePickers(_cmbOverallRange, _dtOverallFrom, _dtOverallTo);
        _overallStatsLoadCts?.Cancel();
        _overallStatsLoadCts = new CancellationTokenSource();
        var cancellationToken = _overallStatsLoadCts.Token;
        _lblOverallStatsStatus.Text = "Building overall statistics from already-loaded replays...";
        if (_btnOverallStatsStop is not null) _btnOverallStatsStop.Enabled = true;

        try
        {
            var rows = await GetAggregateReplayRowsAsync(_cmbOverallRange, _dtOverallFrom, _dtOverallTo, cancellationToken);
            var detailRows = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return BuildOverallStatisticsRows(rows).ToList();
            }, cancellationToken);

            _dgvOverallStats.DataSource = detailRows;
            _lblOverallStatsStatus.Text = rows.Count == 0
                ? "No loaded replays in range."
                : $"Overall statistics built from {rows.Count} loaded replay(s).";
        }
        catch (OperationCanceledException)
        {
            _lblOverallStatsStatus.Text = "Overall statistics refresh stopped.";
        }
        finally
        {
            if (_btnOverallStatsStop is not null) _btnOverallStatsStop.Enabled = false;
        }
    }

    private Task<List<ReplayBrowserRow>> GetAggregateReplayRowsAsync(ComboBox? rangeCombo, DateTimePicker? fromPicker, DateTimePicker? toPicker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filtered = _replayRows
            .Where(row => IsReplayInAggregateRange(row, GetSelectedRange(rangeCombo), fromPicker, toPicker))
            .Where(row => row.Replay is not null)
            .OrderByDescending(row => row.RecordedAt)
            .ToList();
        return Task.FromResult(filtered);
    }

    private static AggregateRangeOption GetSelectedRange(ComboBox? comboBox) => comboBox?.SelectedIndex switch
    {
        1 => AggregateRangeOption.Last7Days,
        2 => AggregateRangeOption.Last30Days,
        3 => AggregateRangeOption.Custom,
        _ => AggregateRangeOption.AllTime
    };

    private static void ToggleAggregateDatePickers(ComboBox? comboBox, DateTimePicker? fromPicker, DateTimePicker? toPicker)
    {
        var isCustom = GetSelectedRange(comboBox) == AggregateRangeOption.Custom;
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

    private IEnumerable<WeaponStatsRow> BuildWeaponStatsRowsForReplay(ReplayBrowserRow row)
    {
        var replay = row.Replay;
        var owner = replay is null ? null : GetReplayOwner(replay);
        if (replay is null || owner is null)
        {
            yield break;
        }

        foreach (var group in replay.DamageEvents
                     .Where(evt => IsDamageByPlayer(owner, evt))
                     .GroupBy(evt => new
                     {
                         WeaponType = GetWeaponStatsTypeLabel(evt),
                         WeaponName = GetWeaponStatsNameLabel(evt)
                     }))
        {
            var hits = group.Count();
            var crits = group.Count(evt => evt.IsCritical == true);
            var damage = group.Sum(evt => evt.Magnitude ?? 0F);
            yield return new WeaponStatsRow
            {
                WeaponType = group.Key.WeaponType,
                WeaponName = group.Key.WeaponName,
                Hits = hits,
                CriticalHits = crits,
                FatalHits = group.Count(evt => evt.IsFatal == true),
                TotalDamage = damage,
                AvgDamage = hits == 0 ? 0F : damage / hits,
                CriticalRate = hits == 0 ? 0F : (float)crits / hits * 100F
            };
        }
    }

    private static string GetWeaponStatsTypeLabel(DamageEvent evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.WeaponType) && !string.Equals(evt.WeaponType, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return evt.WeaponType!;
        }

        if (!string.IsNullOrWhiteSpace(evt.WeaponClassName))
        {
            return evt.WeaponClassName!;
        }

        if (!string.IsNullOrWhiteSpace(evt.WeaponAssetName))
        {
            return evt.WeaponAssetName!;
        }

        return "Unknown";
    }

    private static string GetWeaponStatsNameLabel(DamageEvent evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.WeaponName) && !string.Equals(evt.WeaponName, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return evt.WeaponName!;
        }

        if (!string.IsNullOrWhiteSpace(evt.WeaponAssetName))
        {
            return evt.WeaponAssetName!;
        }

        if (!string.IsNullOrWhiteSpace(evt.WeaponClassName))
        {
            return evt.WeaponClassName!;
        }

        return "Unknown";
    }

    private IEnumerable<DetailRow> BuildOverallStatisticsRows(IEnumerable<ReplayBrowserRow> rows)
    {
        var loadedRows = rows.Where(row => row.Replay is not null).ToList();
        var owners = loadedRows
            .Select(row => new { Row = row, Replay = row.Replay!, Owner = GetReplayOwner(row.Replay!) })
            .Where(x => x.Owner is not null)
            .ToList();

        if (owners.Count == 0)
        {
            yield return new DetailRow("Status", "No loaded replay-owner data available.");
            yield break;
        }

        var placements = owners.Select(x => x.Owner!.Placement ?? (int?)x.Replay.TeamStats?.Position).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var kills = owners.Select(x => (int)(x.Owner!.Kills ?? x.Replay.Stats?.Eliminations ?? 0)).ToList();
        var damages = owners.Select(x => (double)(x.Replay.Stats?.DamageToPlayers ?? 0)).ToList();
        var damageTaken = owners.Select(x => (double)(x.Replay.Stats?.DamageTaken ?? 0)).ToList();
        var accuracies = owners.Select(x => (double)(x.Replay.Stats?.Accuracy ?? 0)).ToList();
        var damageEvents = owners
            .SelectMany(x => x.Replay.DamageEvents
                .Where(evt => IsDamageByPlayer(x.Owner!, evt))
                .Select(evt => new
                {
                    Replay = x.Replay,
                    Event = evt
                }))
            .ToList();
        var critCount = damageEvents.Count(x => x.Event.IsCritical == true);
        var playerDamageTotal = damageEvents
            .Where(x => ClassifyDamageParticipant(x.Replay, x.Event.TargetId, x.Event.TargetName, x.Event.TargetIsBot) == DamageParticipantCategory.Player)
            .Sum(x => x.Event.Magnitude ?? 0F);
        var botDamageTotal = damageEvents
            .Where(x => ClassifyDamageParticipant(x.Replay, x.Event.TargetId, x.Event.TargetName, x.Event.TargetIsBot) == DamageParticipantCategory.Bot)
            .Sum(x => x.Event.Magnitude ?? 0F);
        var structureDamageTotal = damageEvents
            .Where(x => ClassifyDamageParticipant(x.Replay, x.Event.TargetId, x.Event.TargetName, x.Event.TargetIsBot) == DamageParticipantCategory.Structure)
            .Sum(x => x.Event.Magnitude ?? 0F);
        var npcDamageTotal = damageEvents
            .Where(x => ClassifyDamageParticipant(x.Replay, x.Event.TargetId, x.Event.TargetName, x.Event.TargetIsBot) == DamageParticipantCategory.Npc)
            .Sum(x => x.Event.Magnitude ?? 0F);

        yield return new DetailRow("Matches", owners.Count.ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Wins", placements.Count(place => place == 1).ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Win Rate", $"{owners.Count(place => place.Owner!.Placement == 1) / (double)owners.Count * 100:0.0}%");
        yield return new DetailRow("Avg Placement", placements.Count == 0 ? "-" : $"{placements.Average():0.0}");
        yield return new DetailRow("Median Placement", placements.Count == 0 ? "-" : $"{placements.OrderBy(x => x).ElementAt(placements.Count / 2)}");
        yield return new DetailRow("Top 3 Rate", $"{placements.Count(place => place <= 3) / (double)Math.Max(1, placements.Count) * 100:0.0}%");
        yield return new DetailRow("Top 10 Rate", $"{placements.Count(place => place <= 10) / (double)Math.Max(1, placements.Count) * 100:0.0}%");
        yield return new DetailRow("Total Eliminations", kills.Sum().ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Avg Eliminations", $"{kills.Average():0.0}");
        yield return new DetailRow("Best Elim Game", kills.Max().ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Avg Damage To Players", $"{damages.Average():0.0}");
        yield return new DetailRow("Avg Damage Taken", $"{damageTaken.Average():0.0}");
        yield return new DetailRow("Avg Accuracy", $"{accuracies.Average():0.0}%");
        yield return new DetailRow("Critical Hit Rate", damageEvents.Count == 0 ? "-" : $"{critCount / (double)damageEvents.Count * 100:0.0}%");
        yield return new DetailRow("Damage Events Logged", damageEvents.Count.ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Avg Hit Damage", damageEvents.Count == 0 ? "-" : $"{damageEvents.Average(x => x.Event.Magnitude ?? 0F):0.0}");
        yield return new DetailRow("Damage To Bots", $"{botDamageTotal:0.0}");
        yield return new DetailRow("Damage To Players", $"{playerDamageTotal:0.0}");
        yield return new DetailRow("Damage To Structures", $"{structureDamageTotal:0.0}");
        yield return new DetailRow("Damage To NPCs", $"{npcDamageTotal:0.0}");
        yield return new DetailRow("Revives", owners.Sum(x => x.Replay.Stats?.Revives ?? 0).ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Assists", owners.Sum(x => x.Replay.Stats?.Assists ?? 0).ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Materials Gathered", owners.Sum(x => x.Replay.Stats?.MaterialsGathered ?? 0).ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Materials Used", owners.Sum(x => x.Replay.Stats?.MaterialsUsed ?? 0).ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Distance Travelled", owners.Sum(x => x.Replay.Stats?.TotalTraveled ?? 0).ToString(CultureInfo.CurrentCulture));
        yield return new DetailRow("Early Fight Damage (0-5m)", $"{damageEvents.Where(x => GetDamageTime(x.Event) <= 300).Sum(x => x.Event.Magnitude ?? 0F):0.0}");
        yield return new DetailRow("Mid Fight Damage (5-12m)", $"{damageEvents.Where(x => GetDamageTime(x.Event) > 300 && GetDamageTime(x.Event) <= 720).Sum(x => x.Event.Magnitude ?? 0F):0.0}");
        yield return new DetailRow("Late Fight Damage (12m+)", $"{damageEvents.Where(x => GetDamageTime(x.Event) > 720).Sum(x => x.Event.Magnitude ?? 0F):0.0}");
        yield return new DetailRow(
            "Longest Elimination Distance",
            $"{owners.SelectMany(ownerRow => ownerRow.Replay.KillFeed
                    .Where(entry => ResolveKillFeedActorReference(ownerRow.Replay, entry).Player?.PlayerId == ownerRow.Owner!.PlayerId && entry.Distance.HasValue)
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
            .Where(player => player.TeamIndex == owner.TeamIndex && player.PlayerId != owner.PlayerId)
            .OrderBy(player => ResolvePlayerName(player, player.Id, player.PlayerId))
            .ToList() ?? [];

        var selectedPlayers = new List<PlayerData> { owner };
        if (_chkTimelineTeam?.Checked == true)
        {
            selectedPlayers.AddRange(teamMates);
        }

        foreach (var player in selectedPlayers.DistinctBy(player => player.PlayerId))
        {
            var events = replay.DamageEvents
                .Where(evt => IsDamageByPlayer(player, evt))
                .Where(evt => ShouldIncludeTimelineDamageEvent(replay, evt))
                .OrderBy(evt => GetDamageTime(evt))
                .ToList();

            if (events.Count == 0)
            {
                continue;
            }

            var running = 0F;
            var points = new List<DamageTimelinePoint>();

            foreach (var evt in events)
            {
                running += evt.Magnitude ?? 0F;
                points.Add(new DamageTimelinePoint
                {
                    TimeValue = GetDamageTime(evt),
                    Damage = _chkTimelineCumulative?.Checked == true ? running : evt.Magnitude ?? 0F
                });
            }

            _timelineSeries.Add((ResolvePlayerName(player, player.Id, player.PlayerId), points));
        }

        _damageTimelinePanel.Invalidate();
    }

    private bool ShouldIncludeTimelineDamageEvent(FortniteReplay replay, DamageEvent evt)
    {
        var category = ClassifyDamageParticipant(replay, evt.TargetId, evt.TargetName, evt.TargetIsBot);

        return category switch
        {
            DamageParticipantCategory.Player => _chkTimelinePlayers?.Checked ?? true,
            DamageParticipantCategory.Bot => _chkTimelineBots?.Checked ?? true,
            _ => false
        };
    }

    private static bool IsDamageByPlayer(PlayerData player, DamageEvent evt)
    {
        return (!string.IsNullOrWhiteSpace(player.PlayerId) && string.Equals(player.PlayerId, evt.InstigatorName, StringComparison.OrdinalIgnoreCase))
               || (player.Id.HasValue && evt.InstigatorId == player.Id);
    }

    private void PaintDamageTimeline(Graphics graphics, Rectangle bounds)
    {
        graphics.Clear(Color.White);
        using var axisPen = new Pen(Color.Silver, 1F);
        using var textBrush = new SolidBrush(Color.FromArgb(48, 56, 66));
        using var gridPen = new Pen(Color.Gainsboro, 1F);
        using var font = new Font("Segoe UI", 8.5F);
        using var smallFont = new Font("Segoe UI", 8F);

        var chartBounds = Rectangle.FromLTRB(bounds.Left + 48, bounds.Top + 16, bounds.Right - 12, bounds.Bottom - 58);
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
            var points = _timelineSeries[i].Points
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

            graphics.DrawString(_timelineSeries[i].Name, smallFont, new SolidBrush(color), chartBounds.Left + 8 + (i * 120), bounds.Bottom - 42);
        }

        var axisLabel = "Match Time";
        var axisLabelSize = graphics.MeasureString(axisLabel, font);
        graphics.DrawString(axisLabel, font, textBrush, chartBounds.Left + (chartBounds.Width - axisLabelSize.Width) / 2F, bounds.Bottom - 20);
    }
}
