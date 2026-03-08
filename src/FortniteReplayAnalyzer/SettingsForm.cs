using System.Drawing;

namespace FortniteReplayAnalyzer;

internal sealed class SettingsForm : Form
{
    private readonly AnalyzerSettings _workingCopy;
    private readonly ListBox _categoryList;
    private readonly Panel _contentPanel;
    private readonly Panel _generalPanel;
    private readonly Panel _customizationPanel;
    private readonly TextBox _txtReplayFolder;
    private readonly CheckBox _chkDebugOutput;
    private readonly Panel _accentPreview;
    private readonly Panel _surfacePreview;
    private readonly Panel _backgroundPreview;

    public SettingsForm(AnalyzerSettings settings)
    {
        _workingCopy = settings.Clone();
        Text = "Settings";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 460);
        MinimumSize = new Size(700, 420);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        Controls.Add(layout);

        _categoryList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            IntegralHeight = false
        };
        _categoryList.Items.AddRange(["General", "Customization"]);
        _categoryList.SelectedIndexChanged += (_, _) => UpdateCategoryView();
        layout.Controls.Add(_categoryList, 0, 0);

        _contentPanel = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(_contentPanel, 1, 0);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        layout.SetColumnSpan(buttonPanel, 2);
        layout.Controls.Add(buttonPanel, 0, 1);

        var btnOk = new Button { Text = "OK", Width = 100, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
        btnOk.Click += (_, _) => ApplyValues();
        buttonPanel.Controls.Add(btnOk);
        buttonPanel.Controls.Add(btnCancel);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        _generalPanel = BuildGeneralPanel();
        _customizationPanel = BuildCustomizationPanel();
        _contentPanel.Controls.Add(_generalPanel);
        _contentPanel.Controls.Add(_customizationPanel);

        _txtReplayFolder = (TextBox)_generalPanel.Controls[1];
        _chkDebugOutput = (CheckBox)_generalPanel.Controls[3];
        _accentPreview = (Panel)_customizationPanel.Controls[1];
        _surfacePreview = (Panel)_customizationPanel.Controls[4];
        _backgroundPreview = (Panel)_customizationPanel.Controls[7];

        LoadValues();
        _categoryList.SelectedIndex = 0;
    }

    public AnalyzerSettings Settings => _workingCopy.Clone();

    private Panel BuildGeneralPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var lblFolder = new Label { AutoSize = true, Location = new Point(0, 10), Text = "Default Replays Folder" };
        var txtFolder = new TextBox { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Location = new Point(0, 34), Width = 420 };
        var btnBrowse = new Button { Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(430, 32), Size = new Size(90, 30), Text = "Browse" };
        btnBrowse.Click += (_, _) => BrowseForFolder(txtFolder);
        var chkDebug = new CheckBox { AutoSize = true, Location = new Point(0, 86), Text = "Enable debug output" };

        panel.Controls.Add(lblFolder);
        panel.Controls.Add(txtFolder);
        panel.Controls.Add(btnBrowse);
        panel.Controls.Add(chkDebug);
        panel.Resize += (_, _) =>
        {
            txtFolder.Width = Math.Max(260, panel.ClientSize.Width - 110);
            btnBrowse.Left = txtFolder.Right + 10;
        };
        return panel;
    }

    private Panel BuildCustomizationPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        var accentLabel = new Label { AutoSize = true, Location = new Point(0, 10), Text = "Accent Color" };
        var accentPreview = new Panel { BorderStyle = BorderStyle.FixedSingle, Location = new Point(0, 34), Size = new Size(64, 28) };
        var accentButton = new Button { Location = new Point(78, 32), Size = new Size(110, 30), Text = "Choose..." };
        accentButton.Click += (_, _) => PickColor(accentPreview);

        var surfaceLabel = new Label { AutoSize = true, Location = new Point(0, 86), Text = "Surface Color" };
        var surfacePreview = new Panel { BorderStyle = BorderStyle.FixedSingle, Location = new Point(0, 110), Size = new Size(64, 28) };
        var surfaceButton = new Button { Location = new Point(78, 108), Size = new Size(110, 30), Text = "Choose..." };
        surfaceButton.Click += (_, _) => PickColor(surfacePreview);

        var backgroundLabel = new Label { AutoSize = true, Location = new Point(0, 162), Text = "Background Color" };
        var backgroundPreview = new Panel { BorderStyle = BorderStyle.FixedSingle, Location = new Point(0, 186), Size = new Size(64, 28) };
        var backgroundButton = new Button { Location = new Point(78, 184), Size = new Size(110, 30), Text = "Choose..." };
        backgroundButton.Click += (_, _) => PickColor(backgroundPreview);

        panel.Controls.Add(accentLabel);
        panel.Controls.Add(accentPreview);
        panel.Controls.Add(accentButton);
        panel.Controls.Add(surfaceLabel);
        panel.Controls.Add(surfacePreview);
        panel.Controls.Add(surfaceButton);
        panel.Controls.Add(backgroundLabel);
        panel.Controls.Add(backgroundPreview);
        panel.Controls.Add(backgroundButton);

        return panel;
    }

    private void LoadValues()
    {
        _txtReplayFolder.Text = _workingCopy.DefaultReplaysFolder;
        _chkDebugOutput.Checked = _workingCopy.DebugOutputEnabled;
        _accentPreview.BackColor = _workingCopy.GetAccentColor();
        _surfacePreview.BackColor = _workingCopy.GetSurfaceColor();
        _backgroundPreview.BackColor = _workingCopy.GetBackgroundColor();
    }

    private void ApplyValues()
    {
        _workingCopy.DefaultReplaysFolder = _txtReplayFolder.Text.Trim();
        _workingCopy.DebugOutputEnabled = _chkDebugOutput.Checked;
        _workingCopy.AccentColor = AnalyzerSettings.ToColorText(_accentPreview.BackColor);
        _workingCopy.SurfaceColor = AnalyzerSettings.ToColorText(_surfacePreview.BackColor);
        _workingCopy.BackgroundColor = AnalyzerSettings.ToColorText(_backgroundPreview.BackColor);
    }

    private void UpdateCategoryView()
    {
        _generalPanel.Visible = _categoryList.SelectedIndex == 0;
        _customizationPanel.Visible = _categoryList.SelectedIndex == 1;
    }

    private void BrowseForFolder(TextBox textBox)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = textBox.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            textBox.Text = dialog.SelectedPath;
        }
    }

    private void PickColor(Panel previewPanel)
    {
        using var dialog = new ColorDialog { Color = previewPanel.BackColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            previewPanel.BackColor = dialog.Color;
        }
    }
}
