using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace PhotoColorEditor;

internal static class OpenWithRegistration
{
    private const string ClassesPath = @"Software\Classes";
    private const string Application = @"Applications\PhotoColorEditor.exe";
    private const string ProgId = "PhotoColorEditor.Image.1";
    private const string OpenPath = @"shell\open\command";
    private const string ContextVerb = @"\shell\PhotoColorEditor.Open";
    private const string MenuLanguage = "PhotoColorEditor.MenuLanguage";
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png"];

    internal static string OpenCommand(string executable) => $"\"{executable}\" \"%1\"";
    internal static string ContextMenuLabel(string language) => Strings.Get("OpenWithCommandLabel", language, Strings.Get("Title", language));

    public static bool TryRegister(string? preference = null)
    {
        if (CurrentExecutable() is not { } executable) return false;
        try
        {
            using var classes = Registry.CurrentUser.CreateSubKey(ClassesPath);
            using var app = classes.CreateSubKey(Application);
            using var handler = classes.CreateSubKey(ProgId);
            string mode = AppLanguage.Normalize(preference);
            string language = AppLanguage.Resolve(mode);
            WriteNames(app, handler, Strings.Get("Title", language));
            WriteLaunch(app, executable);
            WriteLaunch(handler, executable);
            using var types = app.CreateSubKey("SupportedTypes");
            foreach (string extension in Extensions)
            {
                types.SetValue(extension, "", RegistryValueKind.String);
                using var candidates = classes.CreateSubKey(extension + @"\OpenWithProgIds");
                candidates.SetValue(ProgId, "", RegistryValueKind.String);
                using var verb = classes.CreateSubKey(ContextVerbPath(extension));
                WriteContextName(verb, language);
                verb.SetValue("Icon", IconPath(executable), RegistryValueKind.String);
                verb.SetValue("MultiSelectModel", "Single", RegistryValueKind.String);
                using var launch = verb.CreateSubKey("command");
                launch.SetValue("", OpenCommand(executable), RegistryValueKind.String);
            }
            app.SetValue(MenuLanguage, mode, RegistryValueKind.String);
            NotifyShell();
            return true;
        }
        catch (Exception error) when (RegistryError(error)) { return false; }
    }

    public static void EnsureRegisteredOnStartup()
    {
        if (CurrentExecutable() is not { } executable) return;
        try
        {
            using var classes = Registry.CurrentUser.OpenSubKey(ClassesPath);
            using var app = classes?.OpenSubKey(Application);
            using var command = app?.OpenSubKey(OpenPath);
            string? mode = app?.GetValue(MenuLanguage) as string;
            string? registered = RegisteredExecutable(command?.GetValue("") as string);
            if (app is null || registered is null || !File.Exists(registered))
            {
                TryRegister(mode);
                return;
            }
            if (!string.Equals(registered, executable, StringComparison.OrdinalIgnoreCase)) return;

            string language = AppLanguage.Resolve(mode);
            string name = Strings.Get("Title", language);
            if (mode is null || !string.Equals(app.GetValue("FriendlyAppName") as string, name, StringComparison.Ordinal))
            {
                TryRegister(mode);
                return;
            }
            foreach (string extension in Extensions)
            {
                using var verb = classes?.OpenSubKey(ContextVerbPath(extension));
                using var launch = verb?.OpenSubKey("command");
                if (!string.Equals(launch?.GetValue("") as string, OpenCommand(executable), StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(verb?.GetValue("MUIVerb") as string, ContextMenuLabel(language), StringComparison.Ordinal))
                {
                    TryRegister(mode);
                    return;
                }
            }
        }
        catch (Exception error) when (RegistryError(error)) { }
    }

    private static string? CurrentExecutable() =>
        Environment.ProcessPath is { } path && string.Equals(Path.GetFileName(path), "PhotoColorEditor.exe", StringComparison.OrdinalIgnoreCase)
            ? path : null;

    internal static string? RegisteredExecutable(string? command)
    {
        const string arguments = "\" \"%1\"";
        return command is { Length: > 7 } && command.StartsWith('"') && command.EndsWith(arguments, StringComparison.Ordinal)
            ? command[1..^arguments.Length] : null;
    }

    private static string ContextVerbPath(string extension) => @"SystemFileAssociations\" + extension + ContextVerb;

    private static void WriteContextName(RegistryKey verb, string language)
    {
        string label = ContextMenuLabel(language);
        verb.SetValue("", label, RegistryValueKind.String);
        verb.SetValue("MUIVerb", label, RegistryValueKind.String);
    }

    private static string IconPath(string executable) => $"\"{executable}\",0";

    private static void WriteNames(RegistryKey app, RegistryKey handler, string name)
    {
        app.SetValue("FriendlyAppName", name, RegistryValueKind.String);
        handler.SetValue("", name, RegistryValueKind.String);
        handler.SetValue("FriendlyAppName", name, RegistryValueKind.String);
    }

    private static void WriteLaunch(RegistryKey entry, string executable)
    {
        using var command = entry.CreateSubKey(OpenPath);
        command.SetValue("", OpenCommand(executable), RegistryValueKind.String);
        using var icon = entry.CreateSubKey("DefaultIcon");
        icon.SetValue("", IconPath(executable), RegistryValueKind.String);
    }

    private static bool RegistryError(Exception error) => error is UnauthorizedAccessException or SecurityException or IOException;

    private static void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
