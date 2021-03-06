using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileExplorer.Client.Extensions;
using FileExplorer.Client.Utilities;
using FileExplorer.Shared.Dtos;
using FileExplorer.Shared.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Maze.Client.Library.Services;
using Maze.Modules.Api;
using Maze.Modules.Api.Parameters;
using Maze.Modules.Api.Routing;
using Maze.Server.Connection;
using Maze.Utilities;

namespace FileExplorer.Client.Controllers
{
    [Route("")]
    public class FileExplorerController : MazeController
    {
        [MazeGet("root")]
        public async Task<IActionResult> GetRootElements(CancellationToken cancellationToken)
        {
            var result = new RootElementsDto();
            var directoryHelper = new DirectoryHelper();

            var tasks = new List<Task>
            {
                Task.Run(() => result.RootDirectories = directoryHelper.GetNamespaceDirectories()),
                Task.Run(() =>
                    result.ComputerDirectory =
                        directoryHelper.GetDirectoryEntry(DirectoryInfoEx.MyComputerDirectory, null)),
                Task.Run(async () => result.ComputerDirectoryEntries =
                    (await directoryHelper.GetEntriesKeepOrder(DirectoryInfoEx.MyComputerDirectory, cancellationToken))
                    .ToList())
            };

            await Task.WhenAll(tasks);
            return Ok(result);
        }

        [MazePost("pathTree")]
        public async Task<IActionResult> GetPathTree([FromBody] PathTreeRequestDto request, [FromQuery] bool keepOrder,
            CancellationToken cancellationToken)
        {
            var response = new PathTreeResponseDto();
            var directoryHelper = new DirectoryHelper();

            Task<IEnumerable<FileExplorerEntry>> entriesTask = null;
            if (request.RequestEntries)
            {
                entriesTask = keepOrder
                    ? directoryHelper.GetEntriesKeepOrder(request.Path, cancellationToken)
                    : directoryHelper.GetEntries(request.Path, cancellationToken);
            }

            if (request.RequestedDirectories?.Count > 0)
            {
                var pathDirectories = PathHelper.GetPathDirectories(request.Path).ToList();
                var directories = new ConcurrentDictionary<int, List<DirectoryEntry>>();

                await TaskCombinators.ThrottledAsync(request.RequestedDirectories, async (i, token) =>
                {
                    var directoryPath = pathDirectories[i];
                    directories.TryAdd(i,
                        (await directoryHelper.GetDirectoryEntries(directoryPath, cancellationToken)).ToList());
                }, CancellationToken.None);

                response.Directories = directories;
            }

            if (entriesTask != null)
                response.Entries = (await entriesTask).ToList();

            return Ok(response);
        }

        [MazePost("upload")]
        public async Task<IActionResult> UploadFile([FromQuery] string path, CancellationToken cancellationToken)
        {
            //var sha = SHA256.Create();
            var tmpFile = Path.GetTempFileName();

            using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 8192, FileOptions.Asynchronous | FileOptions.DeleteOnClose))
            {
                //var crypto = new CryptoStream(fs, sha, CryptoStreamMode.Write);
                await Request.Body.CopyToAsync(fs);

                //crypto.FlushFinalBlock();
                //var hash = BitConverter.ToString(sha.Hash).Replace("-", null);

                fs.Position = 0;
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Read, true))
                {
                    string fullName = Directory.CreateDirectory(path).FullName;
                    foreach (var entry in archive.Entries)
                    {
                        var entryPath = Path.GetFullPath(Path.Combine(fullName, entry.FullName));
                        if (Path.GetFileName(entryPath).Length == 0)
                        {
                            Directory.CreateDirectory(entryPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(entryPath));
                            using (Stream destination = System.IO.File.Open(entryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                using (Stream stream = entry.Open())
                                    await stream.CopyToAsync(destination);
                            }

                            System.IO.File.SetLastWriteTime(entryPath, entry.LastWriteTime.DateTime);
                        }
                    }
                }
            }
            return Ok();
        }

        [MazeGet("download")]
        public IActionResult DownloadFile([FromQuery] string path, CancellationToken cancellationToken)
        {
            var file = new FileInfo(path);
            if (file.Exists)
            {
                var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var result = File(fileStream, ContentTypes.Binary, file.Name);
                result.EnableRangeProcessing = true;
                result.LastModified = file.LastWriteTimeUtc;
                result.EntityTag = EntityTagHeaderValue.Parse("\"" + file.Length + "\"");
                return result;
            }

            return NotFound();
        }

        [MazeGet("downloadDirectory")]
        public async Task DownloadDirectory([FromQuery] string path, CancellationToken cancellationToken)
        {
            var directory = new DirectoryInfo(path);
            if (directory.Exists)
            {
                Response.ContentType = ContentTypes.Binary;
                Response.Headers.Add(HeaderNames.ContentEncoding, "zip");
                
                using (var zipArchive = new ZipArchive(Response.Body, ZipArchiveMode.Create, true))
                {
                    var folderOffset = directory.FullName.Length + (directory.FullName.EndsWith("\\") ? 0 : 1);
                    foreach (var fi in directory.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        var entryName = fi.FullName.Substring(folderOffset); // Makes the name in zip based on the folder
                        var entry = zipArchive.CreateEntry(entryName, CompressionLevel.Optimal);
                        using (var reader = fi.OpenRead())
                        using (var writer = entry.Open())
                        {
                            await reader.CopyToAsync(writer, 8192, cancellationToken);
                        }
                    }
                }
            }
            else
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
            }
        }

        [MazePost("clipboard")]
        public IActionResult CopyPathToClipboard([FromQuery] string path, [FromServices] IStaSynchronizationContext context )
        {
            context.Current.Post(state => Clipboard.SetText((string) state, TextDataFormat.UnicodeText), path);
            return Ok();
        }
    }
}