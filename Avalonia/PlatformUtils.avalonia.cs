// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.
// Portions Copyright (c) .NET Foundation and Contributors (.NET MAUI, MIT License). See THIRD-PARTY-NOTICES.txt.

using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Essentieables
{
	[SupportedOSPlatform("windows10.0.10240")]
	static class PlatformUtils
	{
		internal const string AppManifestFilename = "AppxManifest.xml";
		internal const string AppManifestXmlns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
		internal const string AppManifestUapXmlns = "http://schemas.microsoft.com/appx/manifest/uap/windows10";


		internal static void WatchForError<T>(this IAsyncOperation<T> self) =>
			self.AsTask().WatchForError();

		internal static void WatchForError(this Task self)
		{
			var context = SynchronizationContext.Current;
			if (context == null)
				return;

			self.ContinueWith(
				t =>
				{
					var exception = t.Exception?.InnerExceptions.Count > 1 ? t.Exception : t.Exception?.InnerException;
					context.Post(e => { if (e is Exception ex) throw ex; }, exception);

				}, CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted,
				TaskScheduler.Default);
		}
	}
}
