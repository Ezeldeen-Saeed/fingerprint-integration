using System.ComponentModel;
using InstallerWizard.Services;

namespace InstallerWizard.Forms;

/// <summary>
/// Main wizard form with step-by-step installation process
/// </summary>
public class MainWizardForm : Form
{
    // Wizard steps
    private enum WizardStep
    {
        Welcome = 0,
        License = 1,
        InstallPath = 2,
        Configuration = 3,
        Installing = 4,
        Complete = 5
    }

    // Current step
    private WizardStep _currentStep = WizardStep.Welcome;

    // Services
    private readonly InstallerService _installerService;
    private readonly ApiService _apiService;

    // Collected data
    private string _installationKey = "";
    private string _installPath = @"C:\Program Files (x86)\ZkFingerBridge";
    private BranchInfo? _branchInfo;

    // UI Controls
    private Panel _headerPanel = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private Panel _contentPanel = null!;
    private Panel _footerPanel = null!;
    private Button _backButton = null!;
    private Button _nextButton = null!;
    private Button _cancelButton = null!;
    private ProgressBar _progressBar = null!;
    private Label _statusLabel = null!;

    // Step-specific controls
    private Panel _welcomePanel = null!;
    private Panel _licensePanel = null!;
    private Panel _pathPanel = null!;
    private Panel _configPanel = null!;
    private Panel _installingPanel = null!;
    private Panel _completePanel = null!;

    public MainWizardForm()
    {
        _installerService = new InstallerService();
        _apiService = new ApiService();

        InitializeComponent();
        InitializeStepPanels();
        ShowStep(WizardStep.Welcome);
    }

    private void InitializeComponent()
    {
        // Form settings
        Text = "ZkFingerBridge Setup";
        Size = new Size(600, 480);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.White;

        // Header panel (gradient blue)
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.FromArgb(0, 120, 215)
        };
        Controls.Add(_headerPanel);

        _titleLabel = new Label
        {
            Text = "ZkFingerBridge Setup",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 15),
            AutoSize = true
        };
        _headerPanel.Controls.Add(_titleLabel);

        _subtitleLabel = new Label
        {
            Text = "Installation Wizard",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(200, 220, 255),
            Location = new Point(20, 48),
            AutoSize = true
        };
        _headerPanel.Controls.Add(_subtitleLabel);

        // Content panel
        _contentPanel = new Panel
        {
            Location = new Point(0, 80),
            Size = new Size(600, 310),
            BackColor = Color.White
        };
        Controls.Add(_contentPanel);

        // Footer panel
        _footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(240, 240, 240)
        };
        Controls.Add(_footerPanel);

        // Footer separator line
        var separator = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Color.FromArgb(200, 200, 200)
        };
        _footerPanel.Controls.Add(separator);

        // Buttons
        _cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(90, 35),
            Location = new Point(15, 12),
            FlatStyle = FlatStyle.Flat
        };
        _cancelButton.Click += CancelButton_Click;
        _footerPanel.Controls.Add(_cancelButton);

        _backButton = new Button
        {
            Text = "< Back",
            Size = new Size(90, 35),
            Location = new Point(380, 12),
            Enabled = false,
            FlatStyle = FlatStyle.Flat
        };
        _backButton.Click += BackButton_Click;
        _footerPanel.Controls.Add(_backButton);

        _nextButton = new Button
        {
            Text = "Next >",
            Size = new Size(90, 35),
            Location = new Point(480, 12),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _nextButton.FlatAppearance.BorderSize = 0;
        _nextButton.Click += NextButton_Click;
        _footerPanel.Controls.Add(_nextButton);
    }

    private void InitializeStepPanels()
    {
        // Welcome Panel
        _welcomePanel = CreateWelcomePanel();
        _contentPanel.Controls.Add(_welcomePanel);

        // License Key Panel
        _licensePanel = CreateLicensePanel();
        _contentPanel.Controls.Add(_licensePanel);

        // Install Path Panel
        _pathPanel = CreatePathPanel();
        _contentPanel.Controls.Add(_pathPanel);

        // Configuration Panel
        _configPanel = CreateConfigPanel();
        _contentPanel.Controls.Add(_configPanel);

        // Installing Panel
        _installingPanel = CreateInstallingPanel();
        _contentPanel.Controls.Add(_installingPanel);

        // Complete Panel
        _completePanel = CreateCompletePanel();
        _contentPanel.Controls.Add(_completePanel);
    }

    #region Step Panels

    private Panel CreateWelcomePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            Padding = new Padding(30)
        };

        var welcomeLabel = new Label
        {
            Text = "مرحباً بك في معالج تثبيت ZkFingerBridge\n\nWelcome to the ZkFingerBridge Setup Wizard",
            Font = new Font("Segoe UI", 14),
            Location = new Point(30, 30),
            Size = new Size(520, 80),
            TextAlign = ContentAlignment.MiddleCenter
        };
        panel.Controls.Add(welcomeLabel);

        var descLabel = new Label
        {
            Text = "This wizard will install ZkFingerBridge on your computer.\n\n" +
                   "ZkFingerBridge syncs attendance data from your ZKTeco fingerprint " +
                   "device to your Firstsoft HR system automatically.\n\n" +
                   "You will need:\n" +
                   "• Installation key (provided by your administrator)\n" +
                   "• ZKTeco device IP address\n" +
                   "• Internet connection for activation",
            Font = new Font("Segoe UI", 10),
            Location = new Point(30, 120),
            Size = new Size(520, 150)
        };
        panel.Controls.Add(descLabel);

        return panel;
    }

    private TextBox _keyTextBox = null!;
    private Label _keyStatusLabel = null!;
    private Label _branchInfoLabel = null!;

    private Panel CreateLicensePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            Padding = new Padding(30)
        };

        var label = new Label
        {
            Text = "Enter your installation key:",
            Font = new Font("Segoe UI", 11),
            Location = new Point(30, 30),
            AutoSize = true
        };
        panel.Controls.Add(label);

        var arabicLabel = new Label
        {
            Text = "أدخل مفتاح التثبيت:",
            Font = new Font("Segoe UI", 11),
            Location = new Point(30, 55),
            AutoSize = true,
            RightToLeft = RightToLeft.Yes
        };
        panel.Controls.Add(arabicLabel);

        _keyTextBox = new TextBox
        {
            Location = new Point(30, 90),
            Size = new Size(400, 35),
            Font = new Font("Consolas", 14),
            PlaceholderText = "XXXX-2024-XXXX"
        };
        _keyTextBox.TextChanged += KeyTextBox_TextChanged;
        panel.Controls.Add(_keyTextBox);

        var validateButton = new Button
        {
            Text = "Validate",
            Location = new Point(440, 88),
            Size = new Size(90, 35),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        validateButton.FlatAppearance.BorderSize = 0;
        validateButton.Click += ValidateButton_Click;
        panel.Controls.Add(validateButton);

        _keyStatusLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(30, 135),
            Size = new Size(500, 25)
        };
        panel.Controls.Add(_keyStatusLabel);

        _branchInfoLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.FromArgb(0, 100, 0),
            Location = new Point(30, 165),
            Size = new Size(500, 60)
        };
        panel.Controls.Add(_branchInfoLabel);

        return panel;
    }

    private TextBox _pathTextBox = null!;

    private Panel CreatePathPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            Padding = new Padding(30)
        };

        var label = new Label
        {
            Text = "Choose installation folder:",
            Font = new Font("Segoe UI", 11),
            Location = new Point(30, 30),
            AutoSize = true
        };
        panel.Controls.Add(label);

        _pathTextBox = new TextBox
        {
            Text = _installPath,
            Location = new Point(30, 60),
            Size = new Size(440, 30),
            Font = new Font("Segoe UI", 10)
        };
        panel.Controls.Add(_pathTextBox);

        var browseButton = new Button
        {
            Text = "...",
            Location = new Point(480, 58),
            Size = new Size(50, 30)
        };
        browseButton.Click += BrowseButton_Click;
        panel.Controls.Add(browseButton);

        var spaceLabel = new Label
        {
            Text = "Required space: ~80 MB",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.Gray,
            Location = new Point(30, 95),
            AutoSize = true
        };
        panel.Controls.Add(spaceLabel);

        return panel;
    }

    private ComboBox _companyCombo = null!;
    private ComboBox _branchCombo = null!;
    private TextBox _deviceIpTextBox = null!;

    private Panel CreateConfigPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            Padding = new Padding(30)
        };

        // Company
        var companyLabel = new Label
        {
            Text = "Company (الشركة):",
            Font = new Font("Segoe UI", 10),
            Location = new Point(30, 20),
            AutoSize = true
        };
        panel.Controls.Add(companyLabel);

        _companyCombo = new ComboBox
        {
            Location = new Point(30, 45),
            Size = new Size(400, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        _companyCombo.SelectedIndexChanged += CompanyCombo_SelectedIndexChanged;
        panel.Controls.Add(_companyCombo);

        // Branch
        var branchLabel = new Label
        {
            Text = "Branch (الفرع):",
            Font = new Font("Segoe UI", 10),
            Location = new Point(30, 85),
            AutoSize = true
        };
        panel.Controls.Add(branchLabel);

        _branchCombo = new ComboBox
        {
            Location = new Point(30, 110),
            Size = new Size(400, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        panel.Controls.Add(_branchCombo);

        // Device IP
        var deviceLabel = new Label
        {
            Text = "Device IP Address (عنوان IP للجهاز):",
            Font = new Font("Segoe UI", 10),
            Location = new Point(30, 150),
            AutoSize = true
        };
        panel.Controls.Add(deviceLabel);

        _deviceIpTextBox = new TextBox
        {
            Location = new Point(30, 175),
            Size = new Size(200, 30),
            Font = new Font("Consolas", 11),
            PlaceholderText = "192.168.1.100"
        };
        panel.Controls.Add(_deviceIpTextBox);

        return panel;
    }

    private Label _installStatusLabel = null!;
    private ProgressBar _installProgressBar = null!;
    private ListBox _installLogListBox = null!;

    private Panel CreateInstallingPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            Padding = new Padding(30)
        };

        _installStatusLabel = new Label
        {
            Text = "Installing...",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Location = new Point(30, 20),
            Size = new Size(500, 30)
        };
        panel.Controls.Add(_installStatusLabel);

        _installProgressBar = new ProgressBar
        {
            Location = new Point(30, 55),
            Size = new Size(520, 25),
            Style = ProgressBarStyle.Continuous
        };
        panel.Controls.Add(_installProgressBar);

        _installLogListBox = new ListBox
        {
            Location = new Point(30, 90),
            Size = new Size(520, 180),
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle
        };
        panel.Controls.Add(_installLogListBox);

        return panel;
    }

    private Panel CreateCompletePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            Padding = new Padding(30)
        };

        var checkLabel = new Label
        {
            Text = "✓",
            Font = new Font("Segoe UI", 48),
            ForeColor = Color.FromArgb(0, 180, 0),
            Location = new Point(250, 20),
            AutoSize = true
        };
        panel.Controls.Add(checkLabel);

        var completeLabel = new Label
        {
            Text = "Installation Complete!\n\nتم التثبيت بنجاح!",
            Font = new Font("Segoe UI", 16),
            Location = new Point(30, 100),
            Size = new Size(520, 80),
            TextAlign = ContentAlignment.MiddleCenter
        };
        panel.Controls.Add(completeLabel);

        var infoLabel = new Label
        {
            Text = "The ZkFingerBridge service has been installed and started.\n\n" +
                   "It will automatically sync attendance data from your device.\n" +
                   "Check the system tray for the status icon.",
            Font = new Font("Segoe UI", 10),
            Location = new Point(30, 190),
            Size = new Size(520, 80),
            TextAlign = ContentAlignment.MiddleCenter
        };
        panel.Controls.Add(infoLabel);

        return panel;
    }

    #endregion

    #region Navigation

    private void ShowStep(WizardStep step)
    {
        _currentStep = step;

        // Hide all panels
        _welcomePanel.Visible = false;
        _licensePanel.Visible = false;
        _pathPanel.Visible = false;
        _configPanel.Visible = false;
        _installingPanel.Visible = false;
        _completePanel.Visible = false;

        // Update header
        switch (step)
        {
            case WizardStep.Welcome:
                _subtitleLabel.Text = "Welcome";
                _welcomePanel.Visible = true;
                _backButton.Enabled = false;
                _nextButton.Text = "Next >";
                _nextButton.Enabled = true;
                break;

            case WizardStep.License:
                _subtitleLabel.Text = "License Activation";
                _licensePanel.Visible = true;
                _backButton.Enabled = true;
                _nextButton.Text = "Next >";
                _nextButton.Enabled = _branchInfo != null;
                break;

            case WizardStep.InstallPath:
                _subtitleLabel.Text = "Installation Path";
                _pathPanel.Visible = true;
                _backButton.Enabled = true;
                _nextButton.Text = "Next >";
                _nextButton.Enabled = true;
                break;

            case WizardStep.Configuration:
                _subtitleLabel.Text = "Configuration";
                _configPanel.Visible = true;
                _backButton.Enabled = true;
                _nextButton.Text = "Install";
                _nextButton.Enabled = true;
                LoadCompaniesAsync();
                break;

            case WizardStep.Installing:
                _subtitleLabel.Text = "Installing...";
                _installingPanel.Visible = true;
                _backButton.Enabled = false;
                _nextButton.Enabled = false;
                _cancelButton.Enabled = false;
                StartInstallationAsync();
                break;

            case WizardStep.Complete:
                _subtitleLabel.Text = "Complete";
                _completePanel.Visible = true;
                _backButton.Visible = false;
                _nextButton.Text = "Finish";
                _nextButton.Enabled = true;
                _cancelButton.Visible = false;
                break;
        }
    }

    private void NextButton_Click(object? sender, EventArgs e)
    {
        switch (_currentStep)
        {
            case WizardStep.Welcome:
                ShowStep(WizardStep.License);
                break;

            case WizardStep.License:
                if (_branchInfo == null)
                {
                    MessageBox.Show("Please validate your installation key first.", "Validation Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ShowStep(WizardStep.InstallPath);
                break;

            case WizardStep.InstallPath:
                _installPath = _pathTextBox.Text;
                ShowStep(WizardStep.Configuration);
                break;

            case WizardStep.Configuration:
                if (!ValidateConfiguration())
                    return;
                ShowStep(WizardStep.Installing);
                break;

            case WizardStep.Complete:
                Close();
                break;
        }
    }

    private void BackButton_Click(object? sender, EventArgs e)
    {
        switch (_currentStep)
        {
            case WizardStep.License:
                ShowStep(WizardStep.Welcome);
                break;

            case WizardStep.InstallPath:
                ShowStep(WizardStep.License);
                break;

            case WizardStep.Configuration:
                ShowStep(WizardStep.InstallPath);
                break;
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to cancel the installation?",
            "Cancel Setup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            Close();
        }
    }

    #endregion

    #region Event Handlers

    private void KeyTextBox_TextChanged(object? sender, EventArgs e)
    {
        _branchInfo = null;
        _keyStatusLabel.Text = "";
        _branchInfoLabel.Text = "";
        _nextButton.Enabled = false;
    }

    private async void ValidateButton_Click(object? sender, EventArgs e)
    {
        var key = _keyTextBox.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            _keyStatusLabel.Text = "Please enter a key";
            _keyStatusLabel.ForeColor = Color.Red;
            return;
        }

        _keyStatusLabel.Text = "Validating...";
        _keyStatusLabel.ForeColor = Color.Blue;
        _branchInfoLabel.Text = "";

        try
        {
            var result = await _apiService.ValidateKeyAsync(key);

            if (result.Success)
            {
                _branchInfo = result.Branch;
                _installationKey = key;
                _keyStatusLabel.Text = "✓ Key validated successfully!";
                _keyStatusLabel.ForeColor = Color.Green;
                _branchInfoLabel.Text = $"Branch: {result.Branch?.Name}\nBranch ID: {result.Branch?.Id}";
                _nextButton.Enabled = true;
            }
            else
            {
                _keyStatusLabel.Text = $"✗ {result.Error}";
                _keyStatusLabel.ForeColor = Color.Red;
                _nextButton.Enabled = false;
            }
        }
        catch (Exception ex)
        {
            _keyStatusLabel.Text = $"✗ Error: {ex.Message}";
            _keyStatusLabel.ForeColor = Color.Red;
        }
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select installation folder",
            SelectedPath = _pathTextBox.Text,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _pathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void CompanyCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _branchCombo.Items.Clear();
        _branchCombo.Enabled = false;

        if (_companyCombo.SelectedItem is ComboItem company)
        {
            // In real implementation, load branches from API
            // For now, add the validated branch
            if (_branchInfo != null)
            {
                _branchCombo.Items.Add(new ComboItem(_branchInfo.Name, _branchInfo.Id));
                _branchCombo.SelectedIndex = 0;
                _branchCombo.Enabled = true;
            }
        }
    }

    #endregion

    #region Installation

    private bool ValidateConfiguration()
    {
        if (_companyCombo.SelectedItem == null)
        {
            MessageBox.Show("Please select a company.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (_branchCombo.SelectedItem == null)
        {
            MessageBox.Show("Please select a branch.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var ip = _deviceIpTextBox.Text.Trim();
        if (string.IsNullOrEmpty(ip) || !IsValidIp(ip))
        {
            MessageBox.Show("Please enter a valid device IP address.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private bool IsValidIp(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4) return false;
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var num) || num < 0 || num > 255)
                return false;
        }
        return true;
    }

    private async void LoadCompaniesAsync()
    {
        try
        {
            // For demo, just add a default company with the validated branch
            _companyCombo.Items.Clear();
            _companyCombo.Items.Add(new ComboItem("Default Company", 1));
            _companyCombo.SelectedIndex = 0;
            _companyCombo.Enabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load companies: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void StartInstallationAsync()
    {
        var progress = new Progress<InstallProgress>(p =>
        {
            _installProgressBar.Value = p.Percentage;
            _installStatusLabel.Text = p.Status;
            _installLogListBox.Items.Add(p.Status);
            _installLogListBox.TopIndex = _installLogListBox.Items.Count - 1;
        });

        try
        {
            var config = new InstallConfig
            {
                InstallPath = _installPath,
                InstallationKey = _installationKey,
                BranchId = _branchInfo?.Id ?? "1",
                BranchName = _branchInfo?.Name ?? "Default",
                DeviceIp = _deviceIpTextBox.Text.Trim(),
                CompanyId = (_companyCombo.SelectedItem as ComboItem)?.Value.ToString() ?? "1"
            };

            var success = await _installerService.InstallAsync(config, progress);

            if (success)
            {
                ShowStep(WizardStep.Complete);
            }
            else
            {
                MessageBox.Show("Installation failed. Check the log for details.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _backButton.Enabled = true;
                _cancelButton.Enabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Installation error: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _backButton.Enabled = true;
            _cancelButton.Enabled = true;
        }
    }

    #endregion

    private class ComboItem
    {
        public string Text { get; }
        public int Value { get; }

        public ComboItem(string text, int value)
        {
            Text = text;
            Value = value;
        }

        public ComboItem(string text, string value)
        {
            Text = text;
            Value = int.TryParse(value, out var v) ? v : 0;
        }

        public override string ToString() => Text;
    }
}
