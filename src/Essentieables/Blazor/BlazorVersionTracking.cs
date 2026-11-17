

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace Essentieables;

/// <summary>Version tracking persisted in preferences (localStorage).</summary>
internal class BlazorVersionTracking : IVersionTracking
{
	const string SharedName = "versiontracking";
	const string VersionsKey = "VersionTracking.Versions";
	const string BuildsKey = "VersionTracking.Builds";

	readonly IPreferences preferences;
	readonly IAppInfo appInfo;

	List<string> versionHistory = [];
	List<string> buildHistory = [];
	bool tracked;
	bool trackPending;

	public BlazorVersionTracking(IPreferences preferences, IAppInfo appInfo)
	{
		this.preferences = preferences;
		this.appInfo = appInfo;
	}

	public bool IsFirstLaunchEver { get; private set; }

	public bool IsFirstLaunchForCurrentVersion { get; private set; }

	public bool IsFirstLaunchForCurrentBuild { get; private set; }

	public string CurrentVersion => appInfo.VersionString;

	public string CurrentBuild => appInfo.BuildString;

	public string? PreviousVersion => GetPrevious(versionHistory);

	public string? PreviousBuild => GetPrevious(buildHistory);

	public string? FirstInstalledVersion => versionHistory.FirstOrDefault();

	public string? FirstInstalledBuild => buildHistory.FirstOrDefault();

	public IReadOnlyList<string> VersionHistory => versionHistory;

	public IReadOnlyList<string> BuildHistory => buildHistory;

	public bool IsFirstLaunchForVersion(string version) =>
		CurrentVersion == version ? IsFirstLaunchForCurrentVersion : !versionHistory.Contains(version);

	public bool IsFirstLaunchForBuild(string build) =>
		CurrentBuild == build ? IsFirstLaunchForCurrentBuild : !buildHistory.Contains(build);

	/// <summary>
	/// Records the current version. Called before the preferences are loaded (Program.cs, before
	/// <c>BlazorEssentials.InitializeAsync</c>), it would read an empty history (first launch every time) and overwrite it:
	/// it then runs as soon as they are loaded.
	/// </summary>
	public void Track()
	{
		if (tracked)
			return;
		if (preferences is BlazorPreferences blazorPreferences && !blazorPreferences.TryEnsureLoaded())
		{
			if (!trackPending)
			{
				trackPending = true;
				blazorPreferences.Loaded += Track;
			}
			return;
		}
		if (preferences is BlazorPreferences loaded)
			loaded.Loaded -= Track;
		tracked = true;

		versionHistory = ReadHistory(VersionsKey);
		buildHistory = ReadHistory(BuildsKey);

		IsFirstLaunchEver = versionHistory.Count == 0;
		IsFirstLaunchForCurrentVersion = !versionHistory.Contains(CurrentVersion) || CurrentVersion != versionHistory.LastOrDefault();
		IsFirstLaunchForCurrentBuild = !buildHistory.Contains(CurrentBuild) || CurrentBuild != buildHistory.LastOrDefault();

		if (IsFirstLaunchForCurrentVersion)
		{
			versionHistory.Remove(CurrentVersion);
			versionHistory.Add(CurrentVersion);
		}
		if (IsFirstLaunchForCurrentBuild)
		{
			buildHistory.Remove(CurrentBuild);
			buildHistory.Add(CurrentBuild);
		}

		WriteHistory(VersionsKey, versionHistory);
		WriteHistory(BuildsKey, buildHistory);
	}

	string? GetPrevious(List<string> history) =>
		history.Count >= 2 ? history[^2] : null;

	List<string> ReadHistory(string key)
	{
		var raw = preferences.Get<string?>(key, null, SharedName);
		return string.IsNullOrEmpty(raw) ? [] : [.. raw.Split('|', StringSplitOptions.RemoveEmptyEntries)];
	}

	void WriteHistory(string key, List<string> history) =>
		preferences.Set(key, string.Join('|', history), SharedName);
}
