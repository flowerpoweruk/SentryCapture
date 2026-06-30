using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using SentryCapture.Services;
using SentryCapture.ViewModels;
using SentryCapture.Views;

namespace SentryCapture;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Auto-scroll the debug log to the newest entry as it arrives.
        if (Vm != null)
            Vm.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
    }

    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }
    }

    private void AddCameraButton_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;

        var dialog = new CameraEditDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            var result = Vm.AddCamera(dialog.Result);
            if (result == null)
            {
                MessageBox.Show(this,
                    $"Cannot add more cameras (maximum of {Models.AppConfig.MaxCameras}).",
                    "Sentry Capture", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void EditCameraButton_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (sender is not Button { Tag: CameraViewModel cam }) return;

        var dialog = new CameraEditDialog(cam.Worker.Config) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            Vm.EditCamera(cam.Id, dialog.Result);
        }
    }

    private void RemoveCameraButton_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (sender is not Button { Tag: CameraViewModel cam }) return;

        var confirm = MessageBox.Show(this,
            $"Remove camera \"{cam.Name}\"?\n\nThis stops polling for this camera only. " +
            "Already-saved images are kept on disk.",
            "Remove Camera", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
            Vm.RemoveCamera(cam.Id);
    }
}
