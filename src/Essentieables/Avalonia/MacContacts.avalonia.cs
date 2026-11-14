// macOS only: compiled for net10.0-macos (MACOS is defined by the SDK).
#if MACOS

// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Contacts;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contact = Microsoft.Maui.ApplicationModel.Communication.Contact;

namespace Essentieables;

/// <summary>
/// The address book of macOS (CNContactStore), as in Avae.macOS.Essentials. The app needs <c>NSContactsUsageDescription</c> in
/// its Info.plist (and the <c>com.apple.security.personal-information.addressbook</c> entitlement when sandboxed).
/// <see cref="PickContactAsync"/> is not supported on macOS.
/// </summary>
internal sealed class AvaeMacContacts : IContacts
{
    static readonly NSString[] Keys =
    [
        CNContactKey.Identifier,
        CNContactKey.NamePrefix,
        CNContactKey.GivenName,
        CNContactKey.MiddleName,
        CNContactKey.FamilyName,
        CNContactKey.NameSuffix,
        CNContactKey.EmailAddresses,
        CNContactKey.PhoneNumbers,
    ];

    public Task<Contact?> PickContactAsync() =>
        throw new FeatureNotSupportedException("Picking a contact is not supported on macOS: use GetAllAsync.");

    public async Task<IEnumerable<Contact>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var store = new CNContactStore();
        await EnsureAccessAsync(store).ConfigureAwait(false);

        // Enumerating the address book is synchronous and can be slow: off the UI thread.
        return await Task.Run(() =>
        {
            var result = new List<Contact>();
            var containers = store.GetContainers(null, out _);
            if (containers is null)
                return (IEnumerable<Contact>)result;

            foreach (var container in containers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var predicate = CNContact.GetPredicateForContactsInContainer(container.Identifier);
                var contacts = store.GetUnifiedContacts(predicate, Keys, out _);
                if (contacts is null)
                    continue;

                foreach (var contact in contacts)
                    result.Add(ConvertContact(contact));
            }
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    static async Task EnsureAccessAsync(CNContactStore store)
    {
        var status = CNContactStore.GetAuthorizationStatus(CNEntityType.Contacts);
        if (status == CNAuthorizationStatus.Authorized)
            return;

        if (status == CNAuthorizationStatus.NotDetermined)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            store.RequestAccess(CNEntityType.Contacts, (granted, _) => tcs.TrySetResult(granted));
            if (await tcs.Task.ConfigureAwait(false))
                return;
        }
        else if ((long)status == 4) // CNAuthorizationStatus.Limited (macOS 15)
        {
            return;
        }

        throw new PermissionException("Access to the contacts was denied. Enable it in System Settings > Privacy & Security > Contacts.");
    }

    static Contact ConvertContact(CNContact contact)
    {
        var phones = contact.PhoneNumbers?
            .Select(item => new ContactPhone(item?.Value?.StringValue ?? string.Empty))
            .ToList() ?? [];
        var emails = contact.EmailAddresses?
            .Select(item => new ContactEmail(item?.Value?.ToString() ?? string.Empty))
            .ToList() ?? [];

        return new Contact(
            contact.Identifier,
            contact.NamePrefix,
            contact.GivenName,
            contact.MiddleName,
            contact.FamilyName,
            contact.NameSuffix,
            phones,
            emails);
    }
}
#endif
