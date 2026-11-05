using System.IO;
using System.Windows;
using CalendarGenerator.App.Services;
using CalendarGenerator.Core.Settings;
using Microsoft.Win32;

namespace CalendarGenerator.App.Windows;

public partial class AppSettingsWindow : Window
{
    private readonly AppSettingsStore _store = new();
    private readonly GoogleDriveOrderSyncService _drive = new();
    private AppSettings _settings;

    public AppSettingsWindow()
    {
        InitializeComponent();
        _settings = _store.Load();
        SourceFolderBox.Text = _settings.OrdersSourceFolder;
        OutputFolderBox.Text = _settings.PdfOutputFolder;
        ClientIdBox.Text = _settings.GoogleClientId;
        ClientSecretBox.Text = _settings.GoogleClientSecret;
        FolderIdBox.Text = _settings.GoogleDriveRootFolderId;
        UpdateDriveStatus();
    }

    public AppSettings Settings => _settings;

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFolder(SourceFolderBox.Text);
        if (path is not null) SourceFolderBox.Text = path;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFolder(OutputFolderBox.Text);
        if (path is not null) OutputFolderBox.Text = path;
    }

    private static string? PickFolder(string current)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Mappa kiválasztása",
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
            dialog.InitialDirectory = current;
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        ApplyFormToSettings();
        try
        {
            DriveStatusText.Text = "Böngészőben engedélyezze a Google Drive hozzáférést…";
            await _drive.ConnectAsync(_settings);
            _store.Save(_settings);
            UpdateDriveStatus();
            MessageBox.Show("Google Drive kapcsolat sikeres.", "Drive", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Drive kapcsolat hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateDriveStatus();
        }
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        ApplyFormToSettings();
        _drive.Disconnect(_settings);
        _store.Save(_settings);
        UpdateDriveStatus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ApplyFormToSettings();
        Directory.CreateDirectory(_settings.OrdersSourceFolder);
        Directory.CreateDirectory(_settings.PdfOutputFolder);
        _store.Save(_settings);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ApplyFormToSettings()
    {
        _settings.OrdersSourceFolder = SourceFolderBox.Text.Trim();
        _settings.PdfOutputFolder = OutputFolderBox.Text.Trim();
        _settings.GoogleClientId = ClientIdBox.Text.Trim();
        _settings.GoogleClientSecret = ClientSecretBox.Text.Trim();
        _settings.GoogleDriveRootFolderId = FolderIdBox.Text.Trim();
    }

    private void UpdateDriveStatus()
    {
        DriveStatusText.Text = _settings.GoogleDriveConnected
            ? "Állapot: kapcsolódva (token mentve)."
            : "Állapot: nincs kapcsolat.";
    }
}
