using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Essentieables;


/// <summary>SMS compose via an sms: link (effective on mobile browsers).</summary>
internal class BlazorSms : ISms
{
    public bool IsComposeSupported => true;

    public async Task ComposeAsync(SmsMessage? message)
    {
        var url = new StringBuilder("sms:");
        if (message?.Recipients is { Count: > 0 })
            url.Append(string.Join(",", message.Recipients.Select(Uri.EscapeDataString)));
        if (!string.IsNullOrEmpty(message?.Body))
            url.Append("?body=").Append(Uri.EscapeDataString(message.Body));
        await BlazorLauncher.NavigateToAsync(url.ToString());
    }
}
