// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Contacts;
using WinRT;
using Contact = Microsoft.Maui.ApplicationModel.Communication.Contact;
using ContactEmail = Microsoft.Maui.ApplicationModel.Communication.ContactEmail;
using ContactPhone = Microsoft.Maui.ApplicationModel.Communication.ContactPhone;

namespace Essentieables;

[SupportedOSPlatform("windows10.0.10240")]
class OriginalContactsImplementation(AvaeTopLevelStateManager topLevelStateManager) : IContacts
{
    [ComImport]
    [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] // WinRT interfaces are IInspectable-based
    interface IInitializeWithWindow
    {
        [PreserveSig] int Initialize(IntPtr hwnd);
    }

    public async Task<Contact?> PickContactAsync()
    {
        var contactPicker = new ContactPicker();
        var hwnd = topLevelStateManager.GetActiveWindowHandle(false);
        contactPicker.As<IInitializeWithWindow>().Initialize(hwnd);
        var contactSelected = await contactPicker.PickContactAsync();
        return ConvertContact(contactSelected);
    }

    public async Task<IEnumerable<Contact>> GetAllAsync(CancellationToken cancellationToken)
    {
        var contactStore = await ContactManager.RequestStoreAsync()
            .AsTask(cancellationToken).ConfigureAwait(false);
        if (contactStore == null)
            throw new PermissionException("Permission to access the contacts was denied.");

        var contacts = await contactStore.FindContactsAsync()
            .AsTask(cancellationToken).ConfigureAwait(false);
        if (contacts == null || contacts.Count == 0)
            return Array.Empty<Contact>();

        return GetEnumerable();

        IEnumerable<Contact> GetEnumerable()
        {
            foreach (var item in contacts)
            {
                var c = ConvertContact(item);
                if (c != null)
                    yield return c;
            }
        }
    }

    internal static Contact? ConvertContact(global::Windows.ApplicationModel.Contacts.Contact contact)
    {
        if (contact == null)
            return default;

        var phones = contact.Phones?.Select(
            item => new ContactPhone(item?.Number))?.ToList();
        var emails = contact.Emails?.Select(
            item => new ContactEmail(item?.Address))?.ToList();

        return new Contact(
            contact.Id,
            contact.HonorificNamePrefix,
            contact.FirstName,
            contact.MiddleName,
            contact.LastName,
            contact.HonorificNameSuffix,
            phones,
            emails,
            contact.DisplayName);
    }
}