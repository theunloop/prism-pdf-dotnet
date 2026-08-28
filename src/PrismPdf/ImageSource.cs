using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// Image samples prepared for embedding (§8.9), in one of the four forms the ABI accepts.
/// </summary>
/// <remarks>
/// <para>
/// An image source is a staging handle, not a document object: it holds the pixels until a page
/// specification or a flow copies them into a page. Everything that takes one <em>copies</em> it,
/// so the same source may be added to any number of pages and disposed whenever the caller likes.
/// </para>
/// <para>
/// JPEG data is carried through verbatim as <c>DCTDecode</c>; the raw forms are written as
/// uncompressed samples, with <see cref="FromRgba(int, int, ReadOnlySpan{byte})"/> splitting the
/// alpha channel out into a soft mask.
/// </para>
/// </remarks>
public sealed unsafe class ImageSource : PrismPdfHandle
{
    private ImageSource(nint handle)
        : base(handle)
    {
    }

    /// <summary>Width in samples.</summary>
    public int Width => Size().Width;

    /// <summary>Height in samples.</summary>
    public int Height => Size().Height;

    /// <summary>
    /// Wrap a complete JPEG file, kept verbatim as a <c>DCTDecode</c> stream — no re-encoding, so
    /// nothing is re-compressed and nothing is lost.
    /// </summary>
    /// <param name="data">The JPEG file's bytes.</param>
    /// <returns>An owned image source; dispose it when done.</returns>
    public static ImageSource FromJpeg(ReadOnlySpan<byte> data)
    {
        fixed (byte* bytes = data)
        {
            return new ImageSource(
                NativeMethods.prismpdf_image_source_from_jpeg(bytes, (nuint)data.Length));
        }
    }

    /// <summary>Wrap raw 8-bit RGB samples, three bytes per pixel, row-major.</summary>
    /// <param name="width">Width in samples.</param>
    /// <param name="height">Height in samples.</param>
    /// <param name="data">Exactly <c>width * height * 3</c> bytes.</param>
    /// <returns>An owned image source; dispose it when done.</returns>
    public static ImageSource FromRgb(int width, int height, ReadOnlySpan<byte> data)
        => From(NativeMethods.prismpdf_image_source_from_rgb, width, height, data);

    /// <summary>Wrap raw 8-bit greyscale samples, one byte per pixel, row-major.</summary>
    /// <param name="width">Width in samples.</param>
    /// <param name="height">Height in samples.</param>
    /// <param name="data">Exactly <c>width * height</c> bytes.</param>
    /// <returns>An owned image source; dispose it when done.</returns>
    public static ImageSource FromGray(int width, int height, ReadOnlySpan<byte> data)
        => From(NativeMethods.prismpdf_image_source_from_gray, width, height, data);

    /// <summary>
    /// Wrap raw 8-bit RGBA samples, four bytes per pixel, row-major. The alpha channel becomes the
    /// image's soft mask (<c>/SMask</c>, §11.6.5.3).
    /// </summary>
    /// <param name="width">Width in samples.</param>
    /// <param name="height">Height in samples.</param>
    /// <param name="data">Exactly <c>width * height * 4</c> bytes.</param>
    /// <returns>An owned image source; dispose it when done.</returns>
    public static ImageSource FromRgba(int width, int height, ReadOnlySpan<byte> data)
        => From(NativeMethods.prismpdf_image_source_from_rgba, width, height, data);

    private protected override void Free(nint handle) => NativeMethods.prismpdf_image_source_free(handle);

    private static ImageSource From(RawFactory factory, int width, int height, ReadOnlySpan<byte> data)
    {
        Throw.IfNegative(width);
        Throw.IfNegative(height);

        fixed (byte* bytes = data)
        {
            return new ImageSource(factory((uint)width, (uint)height, bytes, (nuint)data.Length));
        }
    }

    private (int Width, int Height) Size()
    {
        uint width = 0;
        uint height = 0;
        Native.Check(NativeMethods.prismpdf_image_source_size(Handle, &width, &height),
            "prismpdf_image_source_size");
        return ((int)width, (int)height);
    }

    private delegate nint RawFactory(uint width, uint height, byte* data, nuint len);
}
