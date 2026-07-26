using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Builder.Core.Logging;

namespace Builder.Data.Files.Updater;

public class IndicesUpdateService
{
	private int _current;

	private int _currentMax = 1;

	private int _progress;

	public Version AppVersion { get; }

	public event EventHandler<IndicesUpdateStatusChangedEventArgs> FileUpdated;

	public event EventHandler<IndicesUpdateStatusChangedEventArgs> StatusChanged;

	public event EventHandler<UpdateServiceProgressChangedEventArgs> ProgressChanged;

	public IndicesUpdateService(Version appVersion)
	{
		AppVersion = appVersion;
	}

	public string[] GetIndexFiles(string directory, SearchOption searchOption = SearchOption.TopDirectoryOnly)
	{
		return Directory.GetFiles(directory, "*.index", searchOption);
	}

	public string[] GetElementFiles(string directory, SearchOption searchOption = SearchOption.TopDirectoryOnly)
	{
		return Directory.GetFiles(directory, "*.xml", searchOption);
	}

	[Obsolete]
	public async Task<bool> RunVerification(string directory)
	{
		UpdateServiceProgressChangedEventArgs e = new UpdateServiceProgressChangedEventArgs("getting index files...", 0, null);
		OnProgressChanged(e);
		string[] indexFiles = GetIndexFiles(directory, SearchOption.AllDirectories);
		e.StatusMessage = "getting element files...";
		OnProgressChanged(e);
		string[] elementFiles = GetElementFiles(directory, SearchOption.AllDirectories);
		_ = indexFiles.Length;
		_ = elementFiles.Length;
		return false;
	}

	public async Task<bool> UpdateIndexFiles(string directory, string onlyMe = null)
	{
		_progress = 0;
		IndicesUpdateStatusChangedEventArgs args = new IndicesUpdateStatusChangedEventArgs("updating indices...", _progress, null);
		bool xx = false;
		OnStatusChanged(args);
		string[] files = Directory.GetFiles(directory, "*.index", SearchOption.AllDirectories);
		if (files.Length == 0)
		{
			args.StatusMessage = "no indices found in " + directory;
			args.ProgressPercentage = 100;
			OnStatusChanged(args);
			return false;
		}
		_current = 0;
		_currentMax = files.Length;
		int progress;
		foreach (FileInfo fileInfo in files.Select((string x) => new FileInfo(x)))
		{
			_current++;
			IndicesUpdateService indicesUpdateService = this;
			progress = (args.ProgressPercentage = GetPercentage(_current, _currentMax));
			indicesUpdateService._progress = progress;
			OnStatusChanged(args);
			if (!fileInfo.Exists)
			{
				Logger.Info(fileInfo.FullName + " was removed before it could be checked for updates, possibly marked as obsolete");
			}
			else
			{
				if (!string.IsNullOrWhiteSpace(onlyMe) && fileInfo.FullName != onlyMe)
				{
					continue;
				}
				args.StatusMessage = "checking " + fileInfo.Name + " for updates...";
				OnStatusChanged(args);
				await Task.Delay(5);
				IndexUpdateResult indexUpdateResult = await UpdateIndexFile(fileInfo);
				if (indexUpdateResult.Success)
				{
					args.StatusMessage = $"{indexUpdateResult.File.FileInfo.Name} updated to version {indexUpdateResult.File.Info.Version}";
					OnStatusChanged(args);
					OnFileUpdated(args);
					xx = true;
				}
				else
				{
					Logger.Warning(fileInfo.Name + " up to date");
					if (indexUpdateResult.RequiredAppVersion != null)
					{
						Logger.Warning($"you need app version {indexUpdateResult.RequiredAppVersion} to update {indexUpdateResult.File.Info.DisplayName}");
					}
				}
				if (await UpdateFileEntries(indexUpdateResult.File))
				{
					xx = true;
				}
				await Task.Delay(5);
			}
		}
		IndicesUpdateService indicesUpdateService2 = this;
		progress = (args.ProgressPercentage = 100);
		indicesUpdateService2._progress = progress;
		args.StatusMessage = "All index files are updated.";
		OnStatusChanged(args);
		return xx;
	}

	private static int GetPercentage(double count, double totalCount)
	{
		return (int)Math.Round(100.0 * count / totalCount);
	}

	private Task<IndexUpdateResult> UpdateIndexFile(FileInfo file)
	{
		return UpdateIndexFile(file, null);
	}

	private async Task<IndexUpdateResult> UpdateIndexFile(FileInfo file, string overrideDownloadUrl)
	{
		IndexFile local = IndexFile.FromFile(file);
		if (!string.IsNullOrWhiteSpace(overrideDownloadUrl) && !local.Info.UpdateUrl.Equals(overrideDownloadUrl))
		{
			Logger.Info("update url for " + file.Name + " has changed from in index from " + local.Info.UpdateUrl + " to " + overrideDownloadUrl);
			local.Info.UpdateUrl = overrideDownloadUrl;
		}
		try
		{
			IndexFile indexFile = await IndexFile.FromUrl(local.Info.UpdateUrl);
			if (!indexFile.MeetsMinimumAppVersionRequirements(AppVersion))
			{
				return new IndexUpdateResult(success: false, local)
				{
					RequiredAppVersion = indexFile.MinimumAppVersion
				};
			}
			if (local.RequiresUpdate(indexFile))
			{
				indexFile.Save(file);
				return new IndexUpdateResult(success: true, indexFile);
			}
		}
		catch (HttpRequestException ex)
		{
			ex.Data["404"] = local.Info.UpdateUrl;
			throw ex;
		}
		return new IndexUpdateResult(success: false, local);
	}

	private async Task<bool> UpdateFileEntries(IndexFile index)
	{
		IndicesUpdateStatusChangedEventArgs args = new IndicesUpdateStatusChangedEventArgs("", _progress, null);
		bool isUpdated = false;
		if (index.Files.Count == 0)
		{
			Logger.Info("no element files in " + index.FileInfo.Name);
			return false;
		}
		string contentDirectory = CreateContentDirectory(index);
		_currentMax += index.Files.Count;
		foreach (IndexFile.FileEntry contentFile in index.Files)
		{
			_current++;
			FileInfo fileInfo = new FileInfo(Path.Combine(contentDirectory, contentFile.Name));
			bool flag = fileInfo.Extension == ".index";
			int progress;
			if (contentFile.IsObsolete)
			{
				if (File.Exists(fileInfo.FullName))
				{
					IndicesUpdateService indicesUpdateService = this;
					progress = (args.ProgressPercentage = GetPercentage(_current, _currentMax));
					indicesUpdateService._progress = progress;
					args.StatusMessage = "removing obsolete " + fileInfo.Name;
					OnStatusChanged(args);
					Logger.Info("removing obsolete " + fileInfo.Name);
					File.Delete(fileInfo.FullName);
					if (flag)
					{
						string text = fileInfo.FullName.Replace(fileInfo.Extension, "");
						Logger.Info("removing all content from " + text);
						IndicesUpdateService indicesUpdateService2 = this;
						progress = (args.ProgressPercentage = GetPercentage(_current, _currentMax));
						indicesUpdateService2._progress = progress;
						args.StatusMessage = "removing all content from " + text;
						OnStatusChanged(args);
						Directory.Delete(text, recursive: true);
					}
				}
				continue;
			}
			if (fileInfo.Exists)
			{
				Logger.Info("checking update for " + fileInfo.Name);
				if (flag)
				{
					IndexUpdateResult indexUpdateResult = await UpdateIndexFile(fileInfo, contentFile.Url);
					Logger.Info(indexUpdateResult.Success ? ("updated " + indexUpdateResult.File.FileInfo.Name) : (indexUpdateResult.File.FileInfo.Name + " is up to date"));
					IndicesUpdateService indicesUpdateService3 = this;
					progress = (args.ProgressPercentage = GetPercentage(_current, _currentMax));
					indicesUpdateService3._progress = progress;
					args.StatusMessage = (indexUpdateResult.Success ? ("updated " + indexUpdateResult.File.FileInfo.Name) : (indexUpdateResult.File.FileInfo.Name + " is up to date"));
					OnStatusChanged(args);
					if (indexUpdateResult.Success)
					{
						OnFileUpdated(args);
					}
					if (indexUpdateResult.Success)
					{
						isUpdated = true;
					}
					continue;
				}
				ElementsFileUpdateResult elementsFileUpdateResult = await UpdateElementsFile(fileInfo, contentFile.Url);
				if (elementsFileUpdateResult.RequiredAppVersion != null)
				{
					Logger.Warning($"You need to run Aurora v{elementsFileUpdateResult.RequiredAppVersion} to get file: {elementsFileUpdateResult.File.Info.UpdateFilename}.");
				}
				Logger.Info(elementsFileUpdateResult.Success ? ("updated " + elementsFileUpdateResult.File.FileInfo.Name) : (elementsFileUpdateResult.File.FileInfo.Name + " is up to date"));
				IndicesUpdateService indicesUpdateService4 = this;
				progress = (args.ProgressPercentage = GetPercentage(_current, _currentMax));
				indicesUpdateService4._progress = progress;
				args.StatusMessage = (elementsFileUpdateResult.Success ? ("updated " + elementsFileUpdateResult.File.FileInfo.Name) : (elementsFileUpdateResult.File.FileInfo.Name + " is up to date"));
				OnStatusChanged(args);
				if (elementsFileUpdateResult.Success)
				{
					OnFileUpdated(args);
				}
				if (elementsFileUpdateResult.Success)
				{
					isUpdated = true;
				}
				continue;
			}
			Logger.Info("downloading " + fileInfo.Name);
			IndicesUpdateService indicesUpdateService5 = this;
			progress = (args.ProgressPercentage = GetPercentage(_current, _currentMax));
			indicesUpdateService5._progress = progress;
			args.StatusMessage = "downloading " + fileInfo.Name;
			OnStatusChanged(args);
			if (flag)
			{
				try
				{
					IndexFile indexFile = await IndexFile.FromUrl(contentFile.Url);
					if (indexFile.MeetsMinimumAppVersionRequirements(AppVersion))
					{
						indexFile.SaveContent(fileInfo);
						Logger.Info("downloaded " + indexFile.FileInfo.Name);
						IndicesUpdateService indicesUpdateService6 = this;
						progress = (args.ProgressPercentage = GetPercentage(_current, _currentMax));
						indicesUpdateService6._progress = progress;
						args.StatusMessage = "downloaded " + indexFile.FileInfo.Name;
						OnStatusChanged(args);
						OnFileUpdated(args);
						isUpdated = true;
						await UpdateFileEntries(indexFile);
					}
					else
					{
						args.StatusMessage = "not downloaded " + contentFile.Url;
						Logger.Warning($"You need to run Aurora v{indexFile.MinimumAppVersion} to get new file: {contentFile.Url}");
						OnStatusChanged(args);
					}
				}
				catch (HttpRequestException ex)
				{
					ex.Data["404"] = contentFile.Url;
					throw ex;
				}
				continue;
			}
			try
			{
				ElementsFile elementsFile = await ElementsFile.FromUrl(contentFile.Url);
				if (elementsFile.MeetsMinimumAppVersionRequirements(AppVersion))
				{
					elementsFile.SaveContent(fileInfo);
					Logger.Info("downloaded " + elementsFile.FileInfo.Name);
					IndicesUpdateService indicesUpdateService7 = this;
					progress = (args.ProgressPercentage = GetPercentage(_current, _currentMax));
					indicesUpdateService7._progress = progress;
					args.StatusMessage = "downloaded " + elementsFile.FileInfo.Name;
					OnStatusChanged(args);
					OnFileUpdated(args);
					isUpdated = true;
				}
				else
				{
					args.StatusMessage = "not downloaded " + contentFile.Url;
					Logger.Warning($"You need to run Aurora v{elementsFile.MinimumAppVersion} to get new file: {contentFile.Url}");
					OnStatusChanged(args);
				}
			}
			catch (HttpRequestException ex2)
			{
				ex2.Data["404"] = contentFile.Url;
				throw ex2;
			}
		}
		return isUpdated;
	}

	private Task<ElementsFileUpdateResult> UpdateElementsFile(FileInfo file)
	{
		return UpdateElementsFile(file, null);
	}

	private async Task<ElementsFileUpdateResult> UpdateElementsFile(FileInfo fileInfo, string overrideDownloadUrl)
	{
		ElementsFile local = ElementsFile.FromFile(fileInfo);
		if (!string.IsNullOrWhiteSpace(overrideDownloadUrl) && !local.Info.UpdateUrl.Equals(overrideDownloadUrl))
		{
			Logger.Info("update url for " + fileInfo.Name + " has changed from in index from " + local.Info.UpdateUrl + " to " + overrideDownloadUrl);
			local.Info.UpdateUrl = overrideDownloadUrl;
		}
		ElementsFile elementsFile = await ElementsFile.FromUrl(local.Info.UpdateUrl);
		if (!elementsFile.MeetsMinimumAppVersionRequirements(AppVersion))
		{
			return new ElementsFileUpdateResult(success: false, local)
			{
				RequiredAppVersion = elementsFile.MinimumAppVersion
			};
		}
		if (local.RequiresUpdate(elementsFile))
		{
			elementsFile.SaveContent(fileInfo);
			return new ElementsFileUpdateResult(success: true, elementsFile);
		}
		return new ElementsFileUpdateResult(success: false, local);
	}

	private string CreateContentDirectory(IndexFile index)
	{
		string contentDirectory = index.GetContentDirectory();
		if (Directory.Exists(contentDirectory))
		{
			return contentDirectory;
		}
		Logger.Info("creating directory '" + contentDirectory + "' for the content files");
		OnStatusChanged(new IndicesUpdateStatusChangedEventArgs("creating directory '" + contentDirectory + "' for the content files", _progress, null));
		Directory.CreateDirectory(contentDirectory);
		return contentDirectory;
	}

	protected virtual void OnStatusChanged(IndicesUpdateStatusChangedEventArgs e)
	{
		this.StatusChanged?.Invoke(this, e);
	}

	protected virtual void OnProgressChanged(UpdateServiceProgressChangedEventArgs e)
	{
		this.ProgressChanged?.Invoke(this, e);
	}

	protected virtual void OnFileUpdated(IndicesUpdateStatusChangedEventArgs e)
	{
		this.FileUpdated?.Invoke(this, e);
	}
}
