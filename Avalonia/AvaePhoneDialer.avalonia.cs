using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Essentieables;

partial class AvaePhoneDialer : IPhoneDialer
{
    void ValidateOpen(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new ArgumentNullException(nameof(number));

        if (!IsSupported)
            throw new FeatureNotSupportedException();
    }
}

partial class AvaePhoneDialer : IPhoneDialer
{
    public bool IsSupported =>
        true;

    public void Open(string number)
    {
        try
        {
            _ = OpenAsync(number);
        }
        catch (Exception ex)
        {
            // Handles synchronous failures only.
            Debug.WriteLine($"Phone dialer failed: {ex}");
        }
    }

    public Task OpenAsync(string number)
    {
        ValidateOpen(number);
        return Launcher.OpenAsync($"tel:{number}");
    }
}
