using System.Windows;

namespace PhotoColorEditor;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args is ["--register-open-with"])
        {
            Shutdown(OpenWithRegistration.TryRegister(Preferences.Load().Language) ? 0 : 1);
            return;
        }

        bool skipRegistration = e.Args is ["--no-open-with-registration"];
        if (!skipRegistration) OpenWithRegistration.EnsureRegisteredOnStartup();
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        if (!skipRegistration && e.Args is [var photo]) await window.OpenStartupPhotoAsync(photo);
    }
}
