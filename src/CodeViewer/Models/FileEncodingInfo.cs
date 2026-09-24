using System.Text;

namespace CodeViewer.Models;

/// <summary>
/// Encapsulates information about a file's character encoding and byte-order mark (BOM).
/// </summary>
public class FileEncodingInfo
{
    public Encoding Encoding { get; set; } = Encoding.UTF8;
    public bool HasBom { get; set; }
    public string DisplayName => HasBom ? $"{Encoding.WebName.ToUpperInvariant()} with BOM" : Encoding.WebName.ToUpperInvariant();

    public static FileEncodingInfo Utf8NoBom => new()
    {
        Encoding = new UTF8Encoding(false),
        HasBom = false
    };

    public static FileEncodingInfo Utf8WithBom => new()
    {
        Encoding = new UTF8Encoding(true),
        HasBom = true
    };
}
