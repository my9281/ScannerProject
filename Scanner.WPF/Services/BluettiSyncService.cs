using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Scanner.Helpers;
using Scanner.Helpers.Services;

namespace Scanner.WPF.Services
{
    public enum BluettiSyncState { CheckingConfiguration, ConfigurationProblem, Importing, Succeeded, Failed }

    public sealed class BluettiSyncResult
    {
        public BluettiSyncState State { get; set; }
        public int ForecastCount { get; set; }
        public int BaseCount { get; set; }
        public bool ForecastImported { get; set; }
        public bool BaseImported { get; set; }
        public bool CleanupPending { get; set; }
        public BluettiExportStage? TimedOutStage { get; set; }
        public string ErrorCode { get; set; }
    }

    public sealed class BluettiConfigurationException : Exception { }

    public sealed class BluettiSyncService
    {
        private readonly BluettiSessionService _sessions;
        private readonly BluettiConfigStore _config;
        private readonly BluettiDownloadRetention _retention;
        public BluettiSyncService(BluettiSessionService sessions = null, BluettiConfigStore config = null, BluettiDownloadRetention retention = null)
        {
            _sessions = sessions ?? new BluettiSessionService();
            _config = config ?? new BluettiConfigStore();
            _retention = retention ?? new BluettiDownloadRetention();
        }

        public async Task<BluettiSession> PrepareAsync(CancellationToken token)
        {
            BluettiLoginConfig config;
            BluettiSession session;
            try { config = _config.Load(); session = _sessions.Load(); }
            catch (Exception e) when (e is CryptographicException || e is XmlException || e is FormatException || e is InvalidDataException || e is ArgumentException)
            { throw new BluettiConfigurationException(); }
            try
            {
                if (session != null)
                {
                    try { await _sessions.ValidateAsync(session, token); }
                    catch (BluettiAuthenticationException) { _sessions.Clear(); session = null; }
                }
                if (session == null)
                {
                    if (string.IsNullOrWhiteSpace(config?.Username) || string.IsNullOrEmpty(config.Password)) throw new BluettiConfigurationException();
                    session = await _sessions.LoginAsync(config.Username, config.Password, token);
                    session.SiteId = config.SiteId;
                }
                var sites = await _sessions.GetSitesAsync(session, token);
                var site = sites.FirstOrDefault(s => s.SiteId == session.SiteId);
                if (site == null) throw new BluettiConfigurationException();
                await _sessions.SelectSiteAsync(session, site, token);
                _sessions.Save(session);
                return session;
            }
            catch (BluettiAuthenticationException) { throw new BluettiConfigurationException(); }
            catch (InvalidDataException) { throw new BluettiConfigurationException(); }
        }

        public BluettiSyncResult RestoreCached(Func<string, UrgentWorkOrderImportResult> forecastImport, ChecklistDataCache cache)
        {
            var result = new BluettiSyncResult { State = BluettiSyncState.Succeeded };
            System.Collections.Generic.List<string> forecastFiles;
            try { forecastFiles = _retention.GetCachedFiles(true); }
            catch (Exception) { forecastFiles = new System.Collections.Generic.List<string>(); result.State = BluettiSyncState.Failed; }
            foreach (var file in forecastFiles)
            {
                try
                {
                    result.ForecastCount = forecastImport(file).ImportedRowCount;
                    result.ForecastImported = true;
                    break;
                }
                catch (Exception) { /* Try the previous cache if a newer file was interrupted or corrupted. */ }
            }
            System.Collections.Generic.List<string> baseFiles;
            try { baseFiles = _retention.GetCachedFiles(false); }
            catch (Exception) { baseFiles = new System.Collections.Generic.List<string>(); result.State = BluettiSyncState.Failed; }
            foreach (var file in baseFiles)
            {
                try
                {
                    cache.ImportBase(file);
                    result.BaseCount = cache.Records.Count;
                    result.BaseImported = true;
                    break;
                }
                catch (Exception) { }
            }
            if ((forecastFiles.Count > 0 && !result.ForecastImported) || (baseFiles.Count > 0 && !result.BaseImported))
                result.State = BluettiSyncState.Failed;
            return result;
        }

        public async Task<BluettiSyncResult> ImportAsync(BluettiSession session,
            Func<BluettiSession, bool, CancellationToken, Task<string>> download,
            Func<string, UrgentWorkOrderImportResult> forecastImport, ChecklistDataCache cache,
            Action<BluettiSyncResult> progress, CancellationToken token)
        {
            var result = new BluettiSyncResult { State = BluettiSyncState.Importing };
            progress?.Invoke(result);
            string pending = null;
            string operation = "forecast export";
            try
            {
                pending = await download(session, true, token);
                operation = "forecast import";
                result.ForecastCount = forecastImport(pending).ImportedRowCount;
                result.ForecastImported = true;
                var forecastFile = pending;
                pending = null;
                result.CleanupPending |= !_retention.KeepLatest(forecastFile, true);
                progress?.Invoke(result);
                operation = "base export";
                pending = await download(session, false, token);
                operation = "base import";
                // ImportBase parses before replacing the existing cache.
                cache.ImportBase(pending);
                result.BaseCount = cache.Records.Count;
                result.BaseImported = true;
                var baseFile = pending;
                pending = null;
                result.CleanupPending |= !_retention.KeepLatest(baseFile, false);
                result.State = BluettiSyncState.Succeeded;
            }
            catch (BluettiAuthenticationException) { result.State = BluettiSyncState.ConfigurationProblem; }
            catch (BluettiExportTimeoutException ex) { result.State = BluettiSyncState.Failed; result.TimedOutStage = ex.Stage; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result.State = BluettiSyncState.Failed;
                result.ErrorCode = ex.GetType().Name;
                BluettiDiagnostics.Write("Sync failure at " + operation + ": " + ex);
            }
            finally
            {
                if (pending != null) _retention.Discard(pending);
            }
            progress?.Invoke(result);
            return result;
        }
    }

    // Only manages files created by this feature inside its own Downloads directory.
    public sealed class BluettiDownloadRetention
    {
        public string Root { get; }
        public BluettiDownloadRetention(string root = null)
        {
            Root = Path.GetFullPath(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Scanner", "Bluetti", "Downloads"));
        }

        private bool IsManaged(string path)
        {
            var full = Path.GetFullPath(path);
            var folder = Path.GetDirectoryName(full);
            if (!string.Equals(Path.GetDirectoryName(folder), Root, StringComparison.OrdinalIgnoreCase)) return false;
            // Do not traverse junctions or symlinks during cleanup.
            return (!Directory.Exists(Root) || (File.GetAttributes(Root) & FileAttributes.ReparsePoint) == 0) &&
                (!Directory.Exists(folder) || (File.GetAttributes(folder) & FileAttributes.ReparsePoint) == 0) &&
                (!File.Exists(full) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0);
        }

        public bool KeepLatest(string latest, bool forecast)
        {
            if (!IsManaged(latest)) throw new InvalidOperationException("Download is outside the managed folder.");
            try
            {
                var pointer = Path.Combine(Root, forecast ? "latest-forecast.txt" : "latest-base.txt");
                File.WriteAllText(pointer + ".tmp", Path.GetRelativePath(Root, latest));
                File.Move(pointer + ".tmp", pointer, true);
                return CleanOld(latest, forecast);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public System.Collections.Generic.List<string> GetCachedFiles(bool forecast)
        {
            var files = new System.Collections.Generic.List<string>();
            if (!Directory.Exists(Root)) return files;
            var pointer = Path.Combine(Root, forecast ? "latest-forecast.txt" : "latest-base.txt");
            if (File.Exists(pointer))
            {
                var latest = Path.GetFullPath(Path.Combine(Root, File.ReadAllText(pointer).Trim()));
                if (IsManaged(latest) && File.Exists(latest) &&
                    Path.GetFileName(latest) == (forecast ? "DTC.xlsx" : "InboundDetection.xlsx")) files.Add(latest);
            }
            // Compatibility with caches downloaded before the latest-successful-file marker existed.
            foreach (var folder in Directory.EnumerateDirectories(Root).OrderByDescending(Path.GetFileName))
            {
                var path = Path.Combine(folder, forecast ? "DTC.xlsx" : "InboundDetection.xlsx");
                if (IsManaged(path) && File.Exists(path) && !files.Contains(path, StringComparer.OrdinalIgnoreCase)) files.Add(path);
            }
            return files;
        }

        private bool CleanOld(string latest, bool forecast)
        {
            bool cleaned = true;
            string name = forecast ? "DTC.xlsx" : "InboundDetection.xlsx";
            foreach (var directory in Directory.EnumerateDirectories(Root))
            {
                var candidate = Path.Combine(directory, name);
                if (string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(latest), StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(candidate)) cleaned &= Discard(candidate);
                else TryRemoveEmpty(directory);
            }
            return cleaned;
        }

        public bool Discard(string path)
        {
            try
            {
                if (!IsManaged(path)) return false;
                File.Delete(path);
                TryRemoveEmpty(Path.GetDirectoryName(path));
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private void TryRemoveEmpty(string folder)
        {
            try
            {
                if (string.Equals(Path.GetDirectoryName(folder), Root, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(folder) && (File.GetAttributes(folder) & FileAttributes.ReparsePoint) == 0 &&
                    !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
