using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeViewer.Models;

namespace CodeViewer.Services;

/// <summary>
/// High-performance file service with encoding and line-ending detection.
/// </summary>
public class FileService : IFileService
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly UTF8Encoding Utf8WithBom = new(true);
    private static readonly UnicodeEncoding Utf16Le = new(false, true);
    private static readonly UnicodeEncoding Utf16Be = new(true, true);

    public async Task<DocumentModel> OpenFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"File not found: {filePath}", filePath);
        }

        // Fast encoding detection from initial bytes
        var encodingInfo = await DetectEncodingAsync(filePath, cancellationToken).ConfigureAwait(false);

        // Read text using detected encoding
        string text;
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
        using (var reader = new StreamReader(stream, encodingInfo.Encoding, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        // Detect line ending type
        var lineEnding = DetectLineEndings(text);

        return new DocumentModel
        {
            FilePath = fileInfo.FullName,
            Title = fileInfo.Name,
            Text = text,
            IsModified = false,
            EncodingInfo = encodingInfo,
            LineEnding = lineEnding,
            FileSizeBytes = fileInfo.Length,
            LastModifiedOnDisk = fileInfo.LastWriteTimeUtc
        };
    }

    public async Task SaveFileAsync(DocumentModel document, string? targetPath = null, CancellationToken cancellationToken = default)
    {
        var destinationPath = targetPath ?? document.FilePath;
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new InvalidOperationException("Cannot save document without a target file path.");
        }

        var destinationDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        // Atomic write pattern: write to a temporary file first, then atomically replace
        var tempFilePath = Path.Combine(
            destinationDir ?? Path.GetTempPath(),
            $".cv_{Guid.NewGuid():N}.tmp");

        try
        {
            var encoding = document.EncodingInfo.Encoding;
            var text = document.Text;

            // Normalize line endings to match the document's selected line ending if needed
            var normalizedText = NormalizeLineEndings(text, document.LineEnding);

            using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            using (var writer = new StreamWriter(stream, encoding))
            {
                await writer.WriteAsync(normalizedText.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // Atomic file swap / move
            if (File.Exists(destinationPath))
            {
                File.Replace(tempFilePath, destinationPath, null);
            }
            else
            {
                File.Move(tempFilePath, destinationPath);
            }

            var updatedFileInfo = new FileInfo(destinationPath);
            document.FilePath = updatedFileInfo.FullName;
            document.Title = updatedFileInfo.Name;
            document.FileSizeBytes = updatedFileInfo.Length;
            document.LastModifiedOnDisk = updatedFileInfo.LastWriteTimeUtc;
            document.IsModified = false;
        }
        finally
        {
            // Clean up temporary file if it still exists after an error
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* best effort */ }
            }
        }
    }

    public LineEndingType DetectLineEndings(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return LineEndingType.LF;
        }

        int crlfCount = 0;
        int lfCount = 0;
        int crCount = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlfCount++;
                    i++; // skip \n
                }
                else
                {
                    crCount++;
                }
            }
            else if (c == '\n')
            {
                lfCount++;
            }
        }

        if (crlfCount > 0 && lfCount == 0 && crCount == 0) return LineEndingType.CRLF;
        if (lfCount > 0 && crlfCount == 0 && crCount == 0) return LineEndingType.LF;
        if (crCount > 0 && crlfCount == 0 && lfCount == 0) return LineEndingType.CR;
        if (crlfCount == 0 && lfCount == 0 && crCount == 0) return LineEndingType.LF;

        // Mixed line endings
        return LineEndingType.Mixed;
    }

    public string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private static async Task<FileEncodingInfo> DetectEncodingAsync(string filePath, CancellationToken cancellationToken)
    {
        var buffer = new byte[4];
        int bytesRead;

        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
        }

        if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            return new FileEncodingInfo { Encoding = Utf8WithBom, HasBom = true };
        }

        if (bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            return new FileEncodingInfo { Encoding = Utf16Le, HasBom = true };
        }

        if (bytesRead >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            return new FileEncodingInfo { Encoding = Utf16Be, HasBom = true };
        }

        // Default to UTF-8 without BOM (modern industry standard)
        return new FileEncodingInfo { Encoding = Utf8WithoutBom, HasBom = false };
    }

    private static string NormalizeLineEndings(string text, LineEndingType lineEnding)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string targetEnding = lineEnding.GetString();

        // Normalize all variations (\r\n and lone \r) to \n first, then convert to target
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                sb.Append(targetEnding);
            }
            else if (c == '\n')
            {
                sb.Append(targetEnding);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
