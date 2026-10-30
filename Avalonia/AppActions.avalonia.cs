using Microsoft.Maui.ApplicationModel;
#if WINDOWS
using Microsoft.UI.Xaml;
#endif
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.StartScreen;

namespace Essentieables;

[SupportedOSPlatform("windows10.0.10586")]
class OriginalAppActionsImplementation : IAppActions
#if WINDOWS
    , IPlatformAppActions
#endif
{
    public bool IsSupported => true;

    public async Task<IEnumerable<AppAction>> GetAsync()
    {
        // Load existing items
        var jumpList = await JumpList.LoadCurrentAsync();

        var actions = new List<AppAction>();
        foreach (var item in jumpList.Items)
            actions.Add(item.ToAction());

        return actions;
    }

    public async Task SetAsync(IEnumerable<AppAction> actions)
    {
        // Load existing items
        var jumpList = await JumpList.LoadCurrentAsync();

        // Set as custom, not system or frequent
        jumpList.SystemGroupKind = JumpListSystemGroupKind.None;

        // Clear the existing items
        jumpList.Items.Clear();

        // Add each action
        foreach (var a in actions)
            jumpList.Items.Add(a.ToJumpListItem());

        // Save the changes
        await jumpList.SaveAsync();
    }

    public event EventHandler<AppActionEventArgs>? AppActionActivated;

    public Task OnLaunched(AppAction a)
    {
        AppActionActivated?.Invoke(null, new AppActionEventArgs(a));
        return Task.CompletedTask;
    }
#if WINDOWS
    public Task OnLaunched(LaunchActivatedEventArgs e)
    {
        return Task.CompletedTask;
    }
#endif
}
[SupportedOSPlatform("windows10.0.10586")]
static partial class OriginalAppActionsExtensions
{
    internal const string AppActionPrefix = "XE_APP_ACTIONS-";

    internal const string iconDirectory = "";
    internal const string iconExtension = ".png";

    internal static string ArgumentsToId(this string arguments)
    {
        if (arguments?.StartsWith(AppActionPrefix) ?? false)
            return Encoding.Default.GetString(Convert.FromBase64String(arguments.Substring(AppActionPrefix.Length)));

        return string.Empty;
    }

    internal static AppAction ToAction(this JumpListItem item)
        => new AppAction(ArgumentsToId(item.Arguments), item.DisplayName, item.Description);

    internal static JumpListItem ToJumpListItem(this AppAction action)
    {
        var id = AppActionPrefix + Convert.ToBase64String(Encoding.Default.GetBytes(action.Id));
        var item = JumpListItem.CreateWithArguments(id, action.Title);

        if (!string.IsNullOrEmpty(action.Subtitle))
            item.Description = action.Subtitle;

        //if (!string.IsNullOrEmpty(action.Icon))
        //{


        //             try
        //	{
        //		//ApplicationDataManager.CreateForPackageFamily("local");


        //                 var cleanPath = action.Icon.TrimStart('/', '\\');
        //                 var sourcePath = Path.Combine(AppContext.BaseDirectory, cleanPath);
        //		var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);// ApplicationData.Current.LocalFolder;
        //		var fileName = Path.GetFileName(cleanPath);
        //		var destPath = Path.Combine(appDataDir, fileName);
        //		if (!File.Exists(destPath) || File.GetLastWriteTime(sourcePath) > File.GetLastWriteTime(destPath))
        //		{
        //			File.Copy(sourcePath, destPath, true);
        //		}

        //                 item.Logo = new Uri($"file:///{destPath}");// new Uri($"ms-appx:///{action.Icon.TrimStart('/', '\\')}");
        //             }
        //	catch
        //	{

        //             }
        //}

        return item;
    }
}
