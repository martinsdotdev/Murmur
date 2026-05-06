using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Murmur.Services;

/// <summary>WinRT StartupTask wrapper. TaskId matches the <c>desktop:StartupTask</c> entry
/// in <c>Package.appxmanifest</c>; without that manifest declaration <c>StartupTask.GetAsync</c>
/// throws. Unpackaged builds also fail, both treated as "not enabled" and logged.</summary>
public sealed class StartupService
{
    private const string TaskId = "MurmurStartupTask";

    public async Task<bool> IsEnabledAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Log($"StartupService.IsEnabledAsync error (likely unpackaged): {ex.Message}");
            return false;
        }
    }

    /// <summary>Returns the actual final state, group policy and "user disabled in Task
    /// Manager" can override the request, so always reflect the returned state in the UI
    /// rather than assuming success.</summary>
    public async Task<StartupTaskState> SetEnabledAsync(bool enabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (enabled)
            {
                return await task.RequestEnableAsync();
            }
            task.Disable();
            return StartupTaskState.Disabled;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Log($"StartupService.SetEnabledAsync error: {ex.Message}");
            return enabled ? StartupTaskState.DisabledByUser : StartupTaskState.Disabled;
        }
    }
}
