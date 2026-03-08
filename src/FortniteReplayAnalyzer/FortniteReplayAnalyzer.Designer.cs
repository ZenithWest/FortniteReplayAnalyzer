namespace FortniteReplayAnalyzer;

partial class FortniteReplayAnalyzer
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        splitMain = new SplitContainer();
        replayBrowserLayout = new TableLayoutPanel();
        replayBrowserHeader = new Panel();
        lblReplayHeader = new Label();
        btnToggleReplayPane = new Button();
        btnRefreshReplays = new Button();
        lblReplayStatus = new Label();
        dgvReplayBrowser = new DataGridView();
        splitContent = new SplitContainer();
        centerLayout = new TableLayoutPanel();
        grpGameStats = new GroupBox();
        dgvGameStats = new DataGridView();
        grpKillFeed = new GroupBox();
        dgvKillFeed = new DataGridView();
        grpPlayers = new GroupBox();
        dgvPlayers = new DataGridView();
        playerPanelLayout = new TableLayoutPanel();
        playerHeaderPanel = new Panel();
        lblPlayerPanelTitle = new Label();
        lblSelectedPlayer = new Label();
        playerContentLayout = new TableLayoutPanel();
        grpPlayerOverview = new GroupBox();
        dgvPlayerOverview = new DataGridView();
        grpPlayerCombatLog = new GroupBox();
        dgvPlayerCombatLog = new DataGridView();
        grpPlayerVictims = new GroupBox();
        dgvPlayerVictims = new DataGridView();
        ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
        splitMain.Panel1.SuspendLayout();
        splitMain.Panel2.SuspendLayout();
        splitMain.SuspendLayout();
        replayBrowserLayout.SuspendLayout();
        replayBrowserHeader.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvReplayBrowser).BeginInit();
        ((System.ComponentModel.ISupportInitialize)splitContent).BeginInit();
        splitContent.Panel1.SuspendLayout();
        splitContent.Panel2.SuspendLayout();
        splitContent.SuspendLayout();
        centerLayout.SuspendLayout();
        grpGameStats.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvGameStats).BeginInit();
        grpKillFeed.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvKillFeed).BeginInit();
        grpPlayers.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvPlayers).BeginInit();
        playerPanelLayout.SuspendLayout();
        playerHeaderPanel.SuspendLayout();
        playerContentLayout.SuspendLayout();
        grpPlayerOverview.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvPlayerOverview).BeginInit();
        grpPlayerCombatLog.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvPlayerCombatLog).BeginInit();
        grpPlayerVictims.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvPlayerVictims).BeginInit();
        SuspendLayout();
        // 
        // splitMain
        // 
        splitMain.Dock = DockStyle.Fill;
        splitMain.FixedPanel = FixedPanel.Panel1;
        splitMain.Location = new Point(0, 0);
        splitMain.Name = "splitMain";
        // 
        // splitMain.Panel1
        // 
        splitMain.Panel1.Controls.Add(replayBrowserLayout);
        splitMain.Panel1MinSize = 230;
        // 
        // splitMain.Panel2
        // 
        splitMain.Panel2.Controls.Add(splitContent);
        splitMain.Size = new Size(1680, 980);
        splitMain.SplitterDistance = 360;
        splitMain.TabIndex = 0;
        // 
        // replayBrowserLayout
        // 
        replayBrowserLayout.ColumnCount = 1;
        replayBrowserLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        replayBrowserLayout.Controls.Add(replayBrowserHeader, 0, 0);
        replayBrowserLayout.Controls.Add(dgvReplayBrowser, 0, 1);
        replayBrowserLayout.Dock = DockStyle.Fill;
        replayBrowserLayout.Location = new Point(0, 0);
        replayBrowserLayout.Name = "replayBrowserLayout";
        replayBrowserLayout.RowCount = 2;
        replayBrowserLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        replayBrowserLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        replayBrowserLayout.Size = new Size(360, 980);
        replayBrowserLayout.TabIndex = 0;
        // 
        // replayBrowserHeader
        // 
        replayBrowserHeader.Controls.Add(lblReplayHeader);
        replayBrowserHeader.Controls.Add(btnToggleReplayPane);
        replayBrowserHeader.Controls.Add(btnRefreshReplays);
        replayBrowserHeader.Controls.Add(lblReplayStatus);
        replayBrowserHeader.Dock = DockStyle.Fill;
        replayBrowserHeader.Location = new Point(3, 3);
        replayBrowserHeader.Name = "replayBrowserHeader";
        replayBrowserHeader.Padding = new Padding(12, 10, 12, 6);
        replayBrowserHeader.Size = new Size(354, 72);
        replayBrowserHeader.TabIndex = 0;
        // 
        // lblReplayHeader
        // 
        lblReplayHeader.AutoSize = true;
        lblReplayHeader.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        lblReplayHeader.Location = new Point(12, 10);
        lblReplayHeader.Name = "lblReplayHeader";
        lblReplayHeader.Size = new Size(143, 25);
        lblReplayHeader.TabIndex = 0;
        lblReplayHeader.Text = "Replay Browser";
        // 
        // btnToggleReplayPane
        // 
        btnToggleReplayPane.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnToggleReplayPane.Location = new Point(167, 8);
        btnToggleReplayPane.Name = "btnToggleReplayPane";
        btnToggleReplayPane.Size = new Size(82, 30);
        btnToggleReplayPane.TabIndex = 1;
        btnToggleReplayPane.Text = "Collapse";
        btnToggleReplayPane.UseVisualStyleBackColor = true;
        // 
        // btnRefreshReplays
        // 
        btnRefreshReplays.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefreshReplays.Location = new Point(255, 8);
        btnRefreshReplays.Name = "btnRefreshReplays";
        btnRefreshReplays.Size = new Size(87, 30);
        btnRefreshReplays.TabIndex = 2;
        btnRefreshReplays.Text = "Refresh";
        btnRefreshReplays.UseVisualStyleBackColor = true;
        // 
        // lblReplayStatus
        // 
        lblReplayStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblReplayStatus.Location = new Point(12, 42);
        lblReplayStatus.Name = "lblReplayStatus";
        lblReplayStatus.Size = new Size(330, 22);
        lblReplayStatus.TabIndex = 3;
        lblReplayStatus.Text = "Loading replay folder...";
        // 
        // dgvReplayBrowser
        // 
        dgvReplayBrowser.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvReplayBrowser.Dock = DockStyle.Fill;
        dgvReplayBrowser.Location = new Point(3, 81);
        dgvReplayBrowser.Name = "dgvReplayBrowser";
        dgvReplayBrowser.Size = new Size(354, 896);
        dgvReplayBrowser.TabIndex = 1;
        // 
        // splitContent
        // 
        splitContent.Dock = DockStyle.Fill;
        splitContent.Location = new Point(0, 0);
        splitContent.Name = "splitContent";
        // 
        // splitContent.Panel1
        // 
        splitContent.Panel1.Controls.Add(centerLayout);
        splitContent.Panel1MinSize = 420;
        // 
        // splitContent.Panel2
        // 
        splitContent.Panel2.Controls.Add(playerPanelLayout);
        splitContent.Panel2MinSize = 540;
        splitContent.Size = new Size(1316, 980);
        splitContent.SplitterDistance = 460;
        splitContent.TabIndex = 0;
        // 
        // centerLayout
        // 
        centerLayout.ColumnCount = 1;
        centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        centerLayout.Controls.Add(grpGameStats, 0, 0);
        centerLayout.Controls.Add(grpKillFeed, 0, 1);
        centerLayout.Controls.Add(grpPlayers, 0, 2);
        centerLayout.Dock = DockStyle.Fill;
        centerLayout.Location = new Point(0, 0);
        centerLayout.Name = "centerLayout";
        centerLayout.Padding = new Padding(10);
        centerLayout.RowCount = 3;
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 28F));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));
        centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));
        centerLayout.Size = new Size(460, 980);
        centerLayout.TabIndex = 0;
        // 
        // grpGameStats
        // 
        grpGameStats.Controls.Add(dgvGameStats);
        grpGameStats.Dock = DockStyle.Fill;
        grpGameStats.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        grpGameStats.Location = new Point(13, 13);
        grpGameStats.Name = "grpGameStats";
        grpGameStats.Padding = new Padding(10);
        grpGameStats.Size = new Size(434, 258);
        grpGameStats.TabIndex = 0;
        grpGameStats.TabStop = false;
        grpGameStats.Text = "Game Stats";
        // 
        // dgvGameStats
        // 
        dgvGameStats.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvGameStats.Dock = DockStyle.Fill;
        dgvGameStats.Location = new Point(10, 33);
        dgvGameStats.Name = "dgvGameStats";
        dgvGameStats.Size = new Size(414, 215);
        dgvGameStats.TabIndex = 0;
        // 
        // grpKillFeed
        // 
        grpKillFeed.Controls.Add(dgvKillFeed);
        grpKillFeed.Dock = DockStyle.Fill;
        grpKillFeed.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        grpKillFeed.Location = new Point(13, 277);
        grpKillFeed.Name = "grpKillFeed";
        grpKillFeed.Padding = new Padding(10);
        grpKillFeed.Size = new Size(434, 331);
        grpKillFeed.TabIndex = 1;
        grpKillFeed.TabStop = false;
        grpKillFeed.Text = "Kill Feed";
        // 
        // dgvKillFeed
        // 
        dgvKillFeed.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvKillFeed.Dock = DockStyle.Fill;
        dgvKillFeed.Location = new Point(10, 33);
        dgvKillFeed.Name = "dgvKillFeed";
        dgvKillFeed.Size = new Size(414, 288);
        dgvKillFeed.TabIndex = 0;
        // 
        // grpPlayers
        // 
        grpPlayers.Controls.Add(dgvPlayers);
        grpPlayers.Dock = DockStyle.Fill;
        grpPlayers.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        grpPlayers.Location = new Point(13, 614);
        grpPlayers.Name = "grpPlayers";
        grpPlayers.Padding = new Padding(10);
        grpPlayers.Size = new Size(434, 353);
        grpPlayers.TabIndex = 2;
        grpPlayers.TabStop = false;
        grpPlayers.Text = "Player List";
        // 
        // dgvPlayers
        // 
        dgvPlayers.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvPlayers.Dock = DockStyle.Fill;
        dgvPlayers.Location = new Point(10, 33);
        dgvPlayers.Name = "dgvPlayers";
        dgvPlayers.Size = new Size(414, 310);
        dgvPlayers.TabIndex = 0;
        // 
        // playerPanelLayout
        // 
        playerPanelLayout.ColumnCount = 1;
        playerPanelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        playerPanelLayout.Controls.Add(playerHeaderPanel, 0, 0);
        playerPanelLayout.Controls.Add(playerContentLayout, 0, 1);
        playerPanelLayout.Dock = DockStyle.Fill;
        playerPanelLayout.Location = new Point(0, 0);
        playerPanelLayout.Name = "playerPanelLayout";
        playerPanelLayout.Padding = new Padding(10);
        playerPanelLayout.RowCount = 2;
        playerPanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        playerPanelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        playerPanelLayout.Size = new Size(852, 980);
        playerPanelLayout.TabIndex = 0;
        // 
        // playerHeaderPanel
        // 
        playerHeaderPanel.Controls.Add(lblPlayerPanelTitle);
        playerHeaderPanel.Controls.Add(lblSelectedPlayer);
        playerHeaderPanel.Dock = DockStyle.Fill;
        playerHeaderPanel.Location = new Point(13, 13);
        playerHeaderPanel.Name = "playerHeaderPanel";
        playerHeaderPanel.Padding = new Padding(8);
        playerHeaderPanel.Size = new Size(826, 76);
        playerHeaderPanel.TabIndex = 0;
        // 
        // lblPlayerPanelTitle
        // 
        lblPlayerPanelTitle.AutoSize = true;
        lblPlayerPanelTitle.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        lblPlayerPanelTitle.Location = new Point(8, 8);
        lblPlayerPanelTitle.Name = "lblPlayerPanelTitle";
        lblPlayerPanelTitle.Size = new Size(116, 28);
        lblPlayerPanelTitle.TabIndex = 0;
        lblPlayerPanelTitle.Text = "Player Stats";
        // 
        // lblSelectedPlayer
        // 
        lblSelectedPlayer.AutoSize = true;
        lblSelectedPlayer.Location = new Point(10, 43);
        lblSelectedPlayer.Name = "lblSelectedPlayer";
        lblSelectedPlayer.Size = new Size(247, 20);
        lblSelectedPlayer.TabIndex = 1;
        lblSelectedPlayer.Text = "Select a player to inspect their stats.";
        // 
        // playerContentLayout
        // 
        playerContentLayout.ColumnCount = 2;
        playerContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
        playerContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
        playerContentLayout.Controls.Add(grpPlayerOverview, 0, 0);
        playerContentLayout.Controls.Add(grpPlayerCombatLog, 1, 0);
        playerContentLayout.Controls.Add(grpPlayerVictims, 0, 1);
        playerContentLayout.Dock = DockStyle.Fill;
        playerContentLayout.Location = new Point(13, 95);
        playerContentLayout.Name = "playerContentLayout";
        playerContentLayout.RowCount = 2;
        playerContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
        playerContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
        playerContentLayout.Size = new Size(826, 872);
        playerContentLayout.TabIndex = 1;
        // 
        // grpPlayerOverview
        // 
        grpPlayerOverview.Controls.Add(dgvPlayerOverview);
        grpPlayerOverview.Dock = DockStyle.Fill;
        grpPlayerOverview.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        grpPlayerOverview.Location = new Point(3, 3);
        grpPlayerOverview.Name = "grpPlayerOverview";
        grpPlayerOverview.Padding = new Padding(10);
        grpPlayerOverview.Size = new Size(374, 412);
        grpPlayerOverview.TabIndex = 0;
        grpPlayerOverview.TabStop = false;
        grpPlayerOverview.Text = "Overview";
        // 
        // dgvPlayerOverview
        // 
        dgvPlayerOverview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvPlayerOverview.Dock = DockStyle.Fill;
        dgvPlayerOverview.Location = new Point(10, 33);
        dgvPlayerOverview.Name = "dgvPlayerOverview";
        dgvPlayerOverview.Size = new Size(354, 369);
        dgvPlayerOverview.TabIndex = 0;
        // 
        // grpPlayerCombatLog
        // 
        grpPlayerCombatLog.Controls.Add(dgvPlayerCombatLog);
        grpPlayerCombatLog.Dock = DockStyle.Fill;
        grpPlayerCombatLog.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        grpPlayerCombatLog.Location = new Point(383, 3);
        grpPlayerCombatLog.Name = "grpPlayerCombatLog";
        grpPlayerCombatLog.Padding = new Padding(10);
        grpPlayerCombatLog.Size = new Size(440, 412);
        grpPlayerCombatLog.TabIndex = 1;
        grpPlayerCombatLog.TabStop = false;
        grpPlayerCombatLog.Text = "Combat Log";
        // 
        // dgvPlayerCombatLog
        // 
        dgvPlayerCombatLog.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvPlayerCombatLog.Dock = DockStyle.Fill;
        dgvPlayerCombatLog.Location = new Point(10, 33);
        dgvPlayerCombatLog.Name = "dgvPlayerCombatLog";
        dgvPlayerCombatLog.Size = new Size(420, 369);
        dgvPlayerCombatLog.TabIndex = 0;
        // 
        // grpPlayerVictims
        // 
        playerContentLayout.SetColumnSpan(grpPlayerVictims, 2);
        grpPlayerVictims.Controls.Add(dgvPlayerVictims);
        grpPlayerVictims.Dock = DockStyle.Fill;
        grpPlayerVictims.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        grpPlayerVictims.Location = new Point(3, 421);
        grpPlayerVictims.Name = "grpPlayerVictims";
        grpPlayerVictims.Padding = new Padding(10);
        grpPlayerVictims.Size = new Size(820, 448);
        grpPlayerVictims.TabIndex = 2;
        grpPlayerVictims.TabStop = false;
        grpPlayerVictims.Text = "Eliminated Players";
        // 
        // dgvPlayerVictims
        // 
        dgvPlayerVictims.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvPlayerVictims.Dock = DockStyle.Fill;
        dgvPlayerVictims.Location = new Point(10, 33);
        dgvPlayerVictims.Name = "dgvPlayerVictims";
        dgvPlayerVictims.Size = new Size(800, 405);
        dgvPlayerVictims.TabIndex = 0;
        // 
        // FortniteReplayAnalyzer
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1680, 980);
        Controls.Add(splitMain);
        MinimumSize = new Size(1400, 820);
        Name = "FortniteReplayAnalyzer";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Fortnite Replay Analyzer";
        splitMain.Panel1.ResumeLayout(false);
        splitMain.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
        splitMain.ResumeLayout(false);
        replayBrowserLayout.ResumeLayout(false);
        replayBrowserHeader.ResumeLayout(false);
        replayBrowserHeader.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)dgvReplayBrowser).EndInit();
        splitContent.Panel1.ResumeLayout(false);
        splitContent.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitContent).EndInit();
        splitContent.ResumeLayout(false);
        centerLayout.ResumeLayout(false);
        grpGameStats.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvGameStats).EndInit();
        grpKillFeed.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvKillFeed).EndInit();
        grpPlayers.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvPlayers).EndInit();
        playerPanelLayout.ResumeLayout(false);
        playerHeaderPanel.ResumeLayout(false);
        playerHeaderPanel.PerformLayout();
        playerContentLayout.ResumeLayout(false);
        grpPlayerOverview.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvPlayerOverview).EndInit();
        grpPlayerCombatLog.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvPlayerCombatLog).EndInit();
        grpPlayerVictims.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvPlayerVictims).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private SplitContainer splitMain;
    private TableLayoutPanel replayBrowserLayout;
    private Panel replayBrowserHeader;
    private Label lblReplayHeader;
    private Button btnToggleReplayPane;
    private Button btnRefreshReplays;
    private Label lblReplayStatus;
    private DataGridView dgvReplayBrowser;
    private SplitContainer splitContent;
    private TableLayoutPanel centerLayout;
    private GroupBox grpGameStats;
    private DataGridView dgvGameStats;
    private GroupBox grpKillFeed;
    private DataGridView dgvKillFeed;
    private GroupBox grpPlayers;
    private DataGridView dgvPlayers;
    private TableLayoutPanel playerPanelLayout;
    private Panel playerHeaderPanel;
    private Label lblPlayerPanelTitle;
    private Label lblSelectedPlayer;
    private TableLayoutPanel playerContentLayout;
    private GroupBox grpPlayerOverview;
    private DataGridView dgvPlayerOverview;
    private GroupBox grpPlayerCombatLog;
    private DataGridView dgvPlayerCombatLog;
    private GroupBox grpPlayerVictims;
    private DataGridView dgvPlayerVictims;
}
