using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace PhotoColorEditor;

public partial class MainWindow
{
    private const string RepositoryUrl = "https://github.com/vfyang/PhotoColorEditor";
    private const string ContactEmail = "contact@syncport.ai";

    private void AboutClicked(object sender, RoutedEventArgs e) => CreateAboutWindow().ShowDialog();

    internal Window CreateAboutWindow()
    {
        var assembly = typeof(App).Assembly;
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()!.Copyright;
        var version = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(metadata => metadata.Key == "DisplayVersion").Value!;
        var panel = new StackPanel { Margin = new Thickness(22) };
        var dialog = new Window
        {
            Owner = this, Title = T("AboutTitle", T("Title")), Content = panel, FontFamily = FontFamily, FontSize = FontSize,
            Language = Language, Width = 460, MaxWidth = SystemParameters.WorkArea.Width,
            SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false
        };
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(new TextBlock { Text = T("Title"), FontSize = FontSize + 5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = T("Version", version), Margin = new Thickness(0, 4, 0, 12) });
        panel.Children.Add(new TextBlock { Text = company, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = copyright, Margin = new Thickness(0, 4, 0, 12) });
        AddLink(T("ProjectRepository"), "GitHub · vfyang/PhotoColorEditor", RepositoryUrl);
        AddLink(T("Contact"), ContactEmail, "mailto:" + ContactEmail);
        panel.Children.Add(feedback);
        var close = new Button { Content = T("Close"), IsDefault = true, IsCancel = true, MinHeight = 44,
            MinWidth = 88, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        panel.Children.Add(close);
        return dialog;

        void AddLink(string label, string text, string uri)
        {
            var line = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
            line.Inlines.Add(new Run(label + " · "));
            var link = new Hyperlink(new Run(text)) { NavigateUri = new Uri(uri) };
            link.RequestNavigate += (object sender, RequestNavigateEventArgs e) =>
            {
                try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
                catch (Win32Exception) { feedback.Text = T("LinkFailed"); }
                e.Handled = true;
            };
            line.Inlines.Add(link);
            panel.Children.Add(line);
        }
    }
}
