using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Email compose via a mailto: link handled by the user's configured mail client.</summary>
public class BlazorEmail : IEmail
{
    public bool IsComposeSupported => true;

    public async Task ComposeAsync(EmailMessage? message)
    {
        if (message?.Attachments is { Count: > 0 })
            throw new FeatureNotSupportedException("mailto: links cannot carry attachments.");
        if (message?.BodyFormat is EmailBodyFormat.Html)
            throw new FeatureNotSupportedException("mailto: links only support plain text bodies.");

        var url = new StringBuilder("mailto:");
        if (message?.To is { Count: > 0 })
            url.Append(string.Join(",", message.To.Select(Uri.EscapeDataString)));

        var query = new List<string>();
        if (message?.Cc is { Count: > 0 })
            query.Add("cc=" + string.Join(",", message.Cc.Select(Uri.EscapeDataString)));
        if (message?.Bcc is { Count: > 0 })
            query.Add("bcc=" + string.Join(",", message.Bcc.Select(Uri.EscapeDataString)));
        if (!string.IsNullOrEmpty(message?.Subject))
            query.Add("subject=" + Uri.EscapeDataString(message.Subject));
        if (!string.IsNullOrEmpty(message?.Body))
            query.Add("body=" + Uri.EscapeDataString(message.Body));
        if (query.Count > 0)
            url.Append('?').Append(string.Join("&", query));

        await BlazorLauncher.NavigateToAsync(url.ToString());
    }
}
