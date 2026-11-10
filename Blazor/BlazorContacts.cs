
using Microsoft.Maui.ApplicationModel;

using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Essentieables;

/// <summary>Contact Picker API: Chrome on Android only; elsewhere the calls throw FeatureNotSupportedException.</summary>
internal class BlazorContacts : IContacts
{
    public async Task<IEnumerable<Contact>> GetAllAsync(CancellationToken cancellationToken = default)
        => Parse(await SelectAsync(multiple: true, cancellationToken).ConfigureAwait(false));

    public async Task<Contact?> PickContactAsync()
        => Parse(await SelectAsync(multiple: false, CancellationToken.None).ConfigureAwait(false)).FirstOrDefault();

    /// <summary>Contact Picker API (Chrome on Android). The user cancelling returns no contact.</summary>
    static async Task<string> SelectAsync(bool multiple, CancellationToken cancellationToken)
    {
        try
        {
            return await BlazorEssentials.Module.InvokeAsync<string>("getAllContactsAsync", cancellationToken, multiple).ConfigureAwait(false);
        }
        catch (JSException ex) when (ex.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
        {
            throw new FeatureNotSupportedException("The Contact Picker API is not available in this browser.");
        }
        catch (JSException ex) when (ex.Message.Contains("SecurityError", StringComparison.OrdinalIgnoreCase)
                                     || ex.Message.Contains("NotAllowedError", StringComparison.OrdinalIgnoreCase))
        {
            throw new PermissionException("The contact picker was refused (it needs a user gesture and a secure context).");
        }
    }

    private static List<Contact> Parse(string result)
    {
        if (!string.IsNullOrWhiteSpace(result))
        {
            var contacts = new List<Contact>();

            var results = JsonSerializer.Deserialize(result, AvaeJsonSerializerContext.Default.ListContactsResponseInterop);

            if (results is null)
            {
                return contacts;
            }

            foreach (var item in results)
            {
                var contact = new Contact()
                {
                    GivenName = item.Name?.FirstOrDefault() ?? string.Empty,
                    Emails = item.Email?.Select(e => new ContactEmail { EmailAddress = e }).ToList() ?? [],
                    Phones = item.Tel?.Select(e => new ContactPhone { PhoneNumber = e }).ToList() ?? [],
                };
                contacts.Add(contact);
            }
            return contacts;
        }
        return [];
    }
}