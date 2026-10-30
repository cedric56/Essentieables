// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

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

/// <summary>Contacts are not accessible from the browser (the Contact Picker API is not broadly available).</summary>
internal class BlazorContacts : IContacts
{
    public async Task<IEnumerable<Contact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        { 
            var result = await BlazorEssentials.Module.InvokeAsync<string>("getAllContactsAsync", true);
            return Parse(result);   
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return Parse(string.Empty);
        }
#else
        catch
        {
            return Parse(string.Empty);
        }
#endif
    }

    public async Task<Contact?> PickContactAsync()
    {
        try
        { 
            var result = await BlazorEssentials.Module.InvokeAsync<string>("getAllContactsAsync", false);
            return Parse(result).FirstOrDefault();
        }
#if DEBUG
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return Parse(string.Empty).FirstOrDefault();
        }
#else
        catch
        {
            return Parse(string.Empty).FirstOrDefault();
        }
#endif
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