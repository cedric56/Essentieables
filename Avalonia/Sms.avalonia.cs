// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Windows.ApplicationModel.Chat;
using Windows.Foundation.Metadata;

namespace Essentieables;

partial class OriginalSmsImplementation : ISms
{
    public Task ComposeAsync() =>
        ComposeAsync(null);

    public Task ComposeAsync(SmsMessage? message)
    {
        if (!IsComposeSupported)
            throw new FeatureNotSupportedException();

        message ??= new SmsMessage();

        message.Recipients ??= new List<string>();

        return PlatformComposeAsync(message);
    }
}

[SupportedOSPlatform("windows10.0.10240")]
partial class OriginalSmsImplementation : ISms
{
    public bool IsComposeSupported
        => ApiInformation.IsTypePresent("Windows.ApplicationModel.Chat.ChatMessageManager");

    Task PlatformComposeAsync(SmsMessage message)
    {
        var chat = new ChatMessage();
        if (!string.IsNullOrWhiteSpace(message?.Body))
            chat.Body = message.Body;

        foreach (var recipient in message?.Recipients ?? [])
            chat.Recipients.Add(recipient);

        return ChatMessageManager.ShowComposeSmsMessageAsync(chat).AsTask();
    }
}
