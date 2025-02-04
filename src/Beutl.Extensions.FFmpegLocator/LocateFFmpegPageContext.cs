using System.Diagnostics;
using System.IO.Compression;

using Beutl.Extensibility;

using FFmpeg.AutoGen;

using Microsoft.Extensions.Logging;

using Reactive.Bindings;

namespace Beutl.Extensions.FFmpegLocator;

public class LocateFFmpegPageContext : IPageContext
{
    private static readonly string s_defaultFFmpegPath;
    private static readonly string s_defaultFFmpegExePath;
    private static readonly Lazy<HttpClient> s_lazyHttpClient = new(() => new HttpClient());
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ILogger _logger = Logging.Log.CreateLogger<LocateFFmpegPageContext>();
    private const string WindowsDownload = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2023-11-30-12-55/ffmpeg-n6.0.1-win64-gpl-shared-6.0.zip";
    private const string LinuxDownload = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2023-11-30-12-55/ffmpeg-n6.0.1-linux64-gpl-shared-6.0.tar.xz";

    static LocateFFmpegPageContext()
    {
        s_defaultFFmpegPath = Path.Combine(BeutlEnvironment.GetHomeDirectoryPath(), "ffmpeg");
        s_defaultFFmpegExePath = Path.Combine(s_defaultFFmpegPath, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
    }

    public LocateFFmpegPageContext(LocateFFmpegPageExtension extension)
    {
        Extension = extension;
        CheckIsInstalled();

        StatusMessage.Value = (LibraryInstalled.Value, ExecutableInstalled.Value) switch
        {
            (true, true) => "全ての依存関係がインストールされています",
            (false, true) => "FFmpeg実行ファイルはインストールされていますが、ライブラリ群は未インストールです",
            (true, false) => "FFmpegライブラリ群はインストールされていますが、実行ファイルは未インストールです",
            _ => "FFmpeg実行ファイル、ライブラリ群は未インストールです"
        };

        StartInstall.Subscribe(Install);
        Cancel.Subscribe(() => _cancellationTokenSource?.Cancel());
    }

    public PageExtension Extension { get; }

    public string Header => "FFmpegを配置";

    public ReactiveProperty<bool> FullyInstalled { get; } = new();

    public ReactiveProperty<bool> LibraryInstalled { get; } = new();

    public ReactiveProperty<bool> ExecutableInstalled { get; } = new();

    public ReactiveProperty<string?> LibraryInstallDirectory { get; } = new();

    public ReactiveProperty<string?> ExecutableInstallDirectory { get; } = new();

    public ReactiveProperty<List<LibraryModel>> LibrariesStatus { get; } = new();

    public ReactiveProperty<string?> StatusMessage { get; } = new();

    public ReactiveProperty<string> Output { get; } = new();

    public ReactiveCommand StartInstall { get; } = new();

    public ReactiveCommand Cancel { get; } = new();

    public ReactiveProperty<bool> IsBusy { get; } = new();

    private void Log(LogLevel logLevel, string message)
    {
        Output.Value += $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {message}\n";
        _logger.Log(logLevel, message);
    }

    private void Install()
    {
        Task.Run(async () =>
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;
            try
            {
                IsBusy.Value = true;
                Output.Value = "";
                var http = s_lazyHttpClient.Value;
                Log(LogLevel.Information, $"LibraryInstalled: {LibraryInstalled.Value}");
                Log(LogLevel.Information, $"ExecutableInstalled: {ExecutableInstalled.Value}");
                Log(LogLevel.Information, $"LibraryInstallDirectory: {LibraryInstallDirectory.Value}");
                Log(LogLevel.Information, $"ExecutableInstallDirectory: {ExecutableInstallDirectory.Value}");
                Log(LogLevel.Information, "開始");

                if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
                {
                    Log(LogLevel.Error, "Windows Linux 以外には対応していません");
                    return;
                }

                var url = OperatingSystem.IsWindows() ? WindowsDownload : LinuxDownload;
                var tmp = Path.GetTempFileName();
                if (OperatingSystem.IsLinux())
                {
                    tmp = $"{tmp}.tar.xz";
                }

                Log(LogLevel.Information, $"Temp file ({tmp})");
                Log(LogLevel.Information, $"Downloading ({url})");
                using (HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
                using (Stream srcStream = await response.Content.ReadAsStreamAsync(ct))
                using (var dstStream = File.Create(tmp))
                {
                    long? contentLength = response.Content.Headers.ContentLength;

                    if (!contentLength.HasValue)
                    {
                        await srcStream.CopyToAsync(dstStream, ct);
                    }
                    else
                    {
                        int bufferSize = 81920;
                        byte[] buffer = new byte[bufferSize];
                        long totalBytesRead = 0;
                        int bytesRead;
                        var prevProgress = 0;
                        while ((bytesRead = await srcStream.ReadAsync(buffer, ct).ConfigureAwait(false)) != 0)
                        {
                            await dstStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                            totalBytesRead += bytesRead;
                            var p = totalBytesRead / (double)contentLength.Value;
                            var pp = (int)(p * 100);
                            if (pp - prevProgress >= 10)
                            {
                                Log(LogLevel.Information, $"{pp}% downloaded");
                                prevProgress = pp;
                            }
                        }
                    }
                }

                Log(LogLevel.Information, $"Download Completed ({url})");
                if (!Directory.Exists(s_defaultFFmpegPath))
                {
                    Directory.CreateDirectory(s_defaultFFmpegPath);
                }

                if (OperatingSystem.IsWindows())
                {
                    WindowsInstall(tmp, ct);
                }
                else if (OperatingSystem.IsLinux())
                {
                    await LinuxInstall(tmp, ct);
                }

                Log(LogLevel.Information, "完了しました");
                Log(LogLevel.Information, "このアプリケーションを再起動してください");
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    Log(LogLevel.Information, "キャンセルしました");
                }
                else
                {
                    Log(LogLevel.Error, $"例外が発生しました \n{ex}");
                }
            }
            finally
            {
                CheckIsInstalled();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                IsBusy.Value = false;
            }
        });
    }

    private void WindowsInstall(string tmp, CancellationToken ct)
    {
        using (var zipArchive = ZipFile.OpenRead(tmp))
        {
            var dlls = zipArchive.Entries.Where(e => e.Name.EndsWith(".dll"));
            ct.ThrowIfCancellationRequested();
            foreach (var dll in dlls)
            {
                var dst = Path.Combine(s_defaultFFmpegPath, dll.Name);
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (File.Exists(dst))
                    {
                        Log(LogLevel.Information, $"このファイルは既に存在します ({dst})");
                    }
                    else
                    {
                        dll.ExtractToFile(dst);
                        Log(LogLevel.Information, $"展開しました ({dll.FullName} -> {dst})");
                    }
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Warning, $"展開できませんでした ({dst})\n{ex}");
                }
            }

            var exe = zipArchive.Entries.FirstOrDefault(e => e.Name == "ffmpeg.exe");
            if (exe != null)
            {
                var exeDst = Path.Combine(s_defaultFFmpegPath, exe.Name);
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (File.Exists(exeDst))
                    {
                        Log(LogLevel.Information, $"このファイルは既に存在します ({exeDst})");
                    }
                    else
                    {
                        exe.ExtractToFile(exeDst);
                        Log(LogLevel.Information, $"展開しました ({exe.FullName} -> {exeDst})");
                    }
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Warning, $"展開できませんでした ({exeDst})\n{ex}");
                }
            }
            else
            {
                Log(LogLevel.Warning, $"ファイルが見つかりません (ffmpeg.exe)");
            }
        }

        File.Delete(tmp);
        Log(LogLevel.Information, "Deleted temp file");
    }

    private async Task LinuxInstall(string tmp, CancellationToken ct)
    {
        var tempDir = Path.GetTempPath();
        Log(LogLevel.Information, $"コマンド実行 'xz -d {tmp}'");
        var xz = Process.Start(new ProcessStartInfo("xz", $"-d {tmp}")
        {
            WorkingDirectory = tempDir
        })!;
        await xz.WaitForExitAsync(ct);
        tmp = tmp[..^3];

        Log(LogLevel.Information, $"コマンド実行 'tar -x {tmp}'");
        var tar = Process.Start(new ProcessStartInfo("tar", $"xf {tmp}")
        {
            WorkingDirectory = tempDir
        })!;
        await tar.WaitForExitAsync(ct);

        File.Delete(tmp);
        Log(LogLevel.Information, "Deleted temp file");

        var soFiles = Directory.GetFiles(Path.Combine(tempDir, "ffmpeg-n6.0.1-linux64-gpl-shared-6.0", "lib"), "*.*", SearchOption.TopDirectoryOnly);
        ct.ThrowIfCancellationRequested();
        foreach (var soFile in soFiles)
        {
            var dst = Path.Combine(s_defaultFFmpegPath, Path.GetFileName(soFile));
            ct.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(dst))
                {
                    Log(LogLevel.Information, $"このファイルは既に存在します ({dst})");
                }
                else
                {
                    File.Move(soFile, dst);
                    Log(LogLevel.Information, $"配置しました ({soFile} -> {dst})");
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, $"配置できませんでした ({dst})\n{ex}");
            }
        }

        var exeSrc = Path.Combine(tempDir, "ffmpeg-n6.0.1-linux64-gpl-shared-6.0", "bin", "ffmpeg");
        if (exeSrc != null)
        {
            var exeDst = Path.Combine(s_defaultFFmpegPath, "ffmpeg");
            ct.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(exeDst))
                {
                    Log(LogLevel.Information, $"このファイルは既に存在します ({exeDst})");
                }
                else
                {
                    File.Move(exeSrc, exeDst);
                    Log(LogLevel.Information, $"配置しました ({exeSrc} -> {exeDst})");
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, $"配置できませんでした ({exeDst})\n{ex}");
            }
        }
        else
        {
            Log(LogLevel.Warning, $"ファイルが見つかりません (ffmpeg.exe)");
        }

        Directory.Delete(Path.Combine(tempDir, "ffmpeg-n6.0.1-linux64-gpl-shared-6.0"), true);
    }

    private void CheckIsInstalled()
    {
        var choises = LibraryPathChoises();
        var dict = new Dictionary<string, List<LibraryModel>>();
        foreach (var choise in choises)
        {
            var items = CheckLibrariesInstalled(choise);
            dict.Add(choise, items);
            if (items.All(v => v.Installed))
            {
                LibraryInstalled.Value = true;
                LibraryInstallDirectory.Value = choise;
                break;
            }
        }

        if (!LibraryInstalled.Value)
        {
            LibrariesStatus.Value = dict[s_defaultFFmpegPath];
            LibraryInstallDirectory.Value = s_defaultFFmpegPath;
        }
        else
        {
            LibrariesStatus.Value = dict[LibraryInstallDirectory.Value!];
        }

        ExecutableInstallDirectory.Value = GetExecutable();
        ExecutableInstalled.Value = ExecutableInstallDirectory.Value != null;

        FullyInstalled.Value = ExecutableInstalled.Value && LibraryInstalled.Value;
    }

    private static string? GetExecutable()
    {
        var paths = new List<string>
        {
            s_defaultFFmpegExePath,
            Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg")
        };

        if (OperatingSystem.IsLinux())
        {
            paths.Add("/usr/bin/ffmpeg");
        }

        foreach (string item in paths)
        {
            if (File.Exists(item))
            {
                return item;
            }
        }

        return null;
    }

    private static List<string> LibraryPathChoises()
    {
        var paths = new List<string>
        {
            s_defaultFFmpegPath,
            AppContext.BaseDirectory
        };

        if (OperatingSystem.IsWindows())
        {
            paths.Add(Path.Combine(AppContext.BaseDirectory,
                "runtimes",
                Environment.Is64BitProcess ? "win-x64" : "win-x86",
                "native"));
        }
        else if (OperatingSystem.IsLinux())
        {
            paths.Add($"/usr/lib/{(Environment.Is64BitProcess ? "x86_64" : "x86")}-linux-gnu");
        }

        return paths;
    }

    private static List<LibraryModel> CheckLibrariesInstalled(string basePath)
    {
        string[] files = Directory.Exists(basePath)
            ? Directory.GetFiles(basePath)
            : [];

        var list = new List<LibraryModel>();
        foreach (KeyValuePair<string, int> item in ffmpeg.LibraryVersionMap)
        {
            list.Add(new(item.Key, item.Value, files.Any(x => x.Contains(item.Key))));
        }

        return list;
    }

    public void Dispose()
    {
    }
}
