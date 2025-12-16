using System.Net.Http.Json;
using System.Text.Json;
using ZkFingerBridge.Models;

namespace ZkFingerBridge.UI;

/// <summary>
/// Setup wizard for selecting company and branch during initial installation
/// </summary>
public class SetupWizard : Form
{
    private readonly ComboBox _companyCombo;
    private readonly ComboBox _branchCombo;
    private readonly Button _saveButton;
    private readonly Label _statusLabel;
    private readonly string _apiBaseUrl;
    private readonly string _companiesEndpoint;
    private readonly string? _apiKey;
    
    private List<Company>? _companies;
    
    public int SelectedCompanyId { get; private set; }
    public string? SelectedCompanyName { get; private set; }
    public int SelectedBranchId { get; private set; }
    public string? SelectedBranchName { get; private set; }
    public bool ConfigurationSaved { get; private set; }

    public SetupWizard(string apiBaseUrl, string companiesEndpoint, string? apiKey = null)
    {
        _apiBaseUrl = apiBaseUrl;
        _companiesEndpoint = companiesEndpoint;
        _apiKey = apiKey;
        
        // Form setup
        Text = "إعداد ZkFingerBridge";
        Size = new Size(450, 300);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 10);

        // Company label
        var companyLabel = new Label
        {
            Text = "الشركة:",
            Location = new Point(330, 30),
            AutoSize = true
        };
        Controls.Add(companyLabel);

        // Company dropdown
        _companyCombo = new ComboBox
        {
            Location = new Point(30, 55),
            Size = new Size(380, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        _companyCombo.SelectedIndexChanged += CompanyCombo_SelectedIndexChanged;
        Controls.Add(_companyCombo);

        // Branch label
        var branchLabel = new Label
        {
            Text = "الفرع:",
            Location = new Point(330, 100),
            AutoSize = true
        };
        Controls.Add(branchLabel);

        // Branch dropdown
        _branchCombo = new ComboBox
        {
            Location = new Point(30, 125),
            Size = new Size(380, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false
        };
        Controls.Add(_branchCombo);

        // Status label
        _statusLabel = new Label
        {
            Text = "جاري تحميل الشركات...",
            Location = new Point(30, 170),
            Size = new Size(380, 25),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray
        };
        Controls.Add(_statusLabel);

        // Save button
        _saveButton = new Button
        {
            Text = "حفظ وبدء الخدمة",
            Location = new Point(130, 210),
            Size = new Size(180, 40),
            Enabled = false
        };
        _saveButton.Click += SaveButton_Click;
        Controls.Add(_saveButton);

        // Load companies on form load
        Load += async (_, _) => await LoadCompaniesAsync();
    }

    private async Task LoadCompaniesAsync()
    {
        try
        {
            // Configure HttpClient with proper SSL and proxy handling
            var handler = new HttpClientHandler
            {
                // Allow self-signed certs for development
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                // Use system proxy settings
                UseProxy = true,
                Proxy = System.Net.WebRequest.GetSystemWebProxy()
            };
            
            using var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri(_apiBaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Add common headers
            httpClient.DefaultRequestHeaders.Add("User-Agent", "ZkFingerBridge/1.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            }

            _statusLabel.Text = $"جاري الاتصال بـ {_apiBaseUrl}...";
            
            var fullUrl = new Uri(new Uri(_apiBaseUrl), _companiesEndpoint);
            var response = await httpClient.GetFromJsonAsync<CompaniesResponse>(fullUrl);
            
            if (response?.Success == true && response.Data?.Count > 0)
            {
                _companies = response.Data;
                
                _companyCombo.Items.Clear();
                foreach (var company in _companies)
                {
                    _companyCombo.Items.Add(new ComboBoxItem(company.CompanyName, company.CompanyId));
                }
                
                _companyCombo.Enabled = true;
                _statusLabel.Text = $"تم تحميل {_companies.Count} شركة - اختر الشركة والفرع";
                _statusLabel.ForeColor = Color.Green;
            }
            else
            {
                _statusLabel.Text = "لا توجد شركات متاحة";
                _statusLabel.ForeColor = Color.Red;
            }
        }
        catch (HttpRequestException ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            _statusLabel.Text = $"خطأ في الاتصال: {innerMsg}";
            _statusLabel.ForeColor = Color.Red;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"خطأ: {ex.Message}";
            _statusLabel.ForeColor = Color.Red;
        }
    }

    private void CompanyCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _branchCombo.Items.Clear();
        _branchCombo.Enabled = false;
        _saveButton.Enabled = false;

        if (_companyCombo.SelectedItem is not ComboBoxItem selectedCompany || _companies == null)
            return;

        var company = _companies.FirstOrDefault(c => c.CompanyId == selectedCompany.Value);
        if (company?.Branches == null || company.Branches.Count == 0)
        {
            _statusLabel.Text = "لا توجد فروع لهذه الشركة";
            _statusLabel.ForeColor = Color.Orange;
            return;
        }

        foreach (var branch in company.Branches)
        {
            _branchCombo.Items.Add(new ComboBoxItem(branch.BranchName, branch.BranchId));
        }

        _branchCombo.Enabled = true;
        _branchCombo.SelectedIndexChanged += (_, _) =>
        {
            _saveButton.Enabled = _branchCombo.SelectedItem != null;
        };
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (_companyCombo.SelectedItem is ComboBoxItem company && 
            _branchCombo.SelectedItem is ComboBoxItem branch)
        {
            SelectedCompanyId = company.Value;
            SelectedCompanyName = company.Text;
            SelectedBranchId = branch.Value;
            SelectedBranchName = branch.Text;
            ConfigurationSaved = true;
            
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>
    /// Helper class for ComboBox items with value
    /// </summary>
    private class ComboBoxItem
    {
        public string Text { get; }
        public int Value { get; }

        public ComboBoxItem(string text, int value)
        {
            Text = text;
            Value = value;
        }

        public override string ToString() => Text;
    }
}
