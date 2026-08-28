using PrismPdf.Interop;

namespace PrismPdf.Collections;

/// <summary>
/// The images one page draws (§8.6, §8.9), recursing into form XObjects (§8.10).
/// </summary>
public sealed unsafe class ImageList : NativeList<Image>
{
    internal ImageList(nint handle)
        : base(handle)
    {
    }

    /// <inheritdoc/>
    public override int Count
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_image_list_len(Handle, &len), "prismpdf_image_list_len");
            return Native.ToCount(len);
        }
    }

    /// <inheritdoc/>
    public override Image this[int index]
    {
        get
        {
            nint item = 0;
            Native.Check(
                NativeMethods.prismpdf_image_list_get(Handle, Native.ToIndex(index, nameof(index)), &item),
                "prismpdf_image_list_get");
            return new Image(this, item);
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_image_list_free(handle);
}

/// <summary>One drawn image, borrowed from its <see cref="ImageList"/>.</summary>
public sealed unsafe class Image : BorrowedItem
{
    internal Image(ImageList owner, nint item)
        : base(owner, item)
    {
    }

    /// <summary><c>/Width</c> in samples.</summary>
    public int Width => Info().Width;

    /// <summary><c>/Height</c> in samples.</summary>
    public int Height => Info().Height;

    /// <summary><c>/BitsPerComponent</c>.</summary>
    public int BitsPerComponent => Info().BitsPerComponent;

    /// <summary>The image's colour space.</summary>
    public ColorSpace ColorSpace
    {
        get
        {
            ColorSpace space = default;
            Native.Check(NativeMethods.prismpdf_image_color_space(Item, &space), "prismpdf_image_color_space");
            return space;
        }
    }

    /// <summary>
    /// Components per sample — what you need to walk <see cref="ImageKind.Raw"/> bytes, and the only
    /// way to size a sample in an <see cref="PrismPdf.ColorSpace.Other"/> space.
    /// </summary>
    public int Components
    {
        get
        {
            byte components = 0;
            Native.Check(NativeMethods.prismpdf_image_components(Item, &components), "prismpdf_image_components");
            return components;
        }
    }

    /// <summary>How the payload in <see cref="Data"/> is encoded.</summary>
    public ImageKind Kind
    {
        get
        {
            ImageKind kind = default;
            Native.Check(NativeMethods.prismpdf_image_kind(Item, &kind), "prismpdf_image_kind");
            return kind;
        }
    }

    /// <summary>
    /// The payload, copied out of the list's allocation: decoded samples for
    /// <see cref="ImageKind.Raw"/>, and a complete container file for every other kind.
    /// </summary>
    public byte[] Data
    {
        get
        {
            byte* data = null;
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_image_data(Item, &data, &len), "prismpdf_image_data");
            return Native.CopyBorrowedBytes(data, len);
        }
    }

    /// <summary>Width, height and bit depth in one call, which is how the ABI reports them.</summary>
    /// <returns>The three dimensions.</returns>
    public ImageInfo Info()
    {
        uint width = 0;
        uint height = 0;
        byte bitsPerComponent = 0;
        Native.Check(NativeMethods.prismpdf_image_info(Item, &width, &height, &bitsPerComponent),
            "prismpdf_image_info");
        return new ImageInfo((int)width, (int)height, bitsPerComponent);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var info = Info();
        return $"{info.Width}x{info.Height} {ColorSpace} {info.BitsPerComponent}bpc {Kind}";
    }
}

/// <summary>An extracted image's raster dimensions.</summary>
/// <param name="Width"><c>/Width</c> in samples.</param>
/// <param name="Height"><c>/Height</c> in samples.</param>
/// <param name="BitsPerComponent"><c>/BitsPerComponent</c>.</param>
public readonly record struct ImageInfo(int Width, int Height, int BitsPerComponent);
