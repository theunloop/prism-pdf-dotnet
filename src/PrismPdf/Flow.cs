using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// Flowed layout: pour content in, let the engine wrap lines and break pages (§9, §14.8).
/// </summary>
/// <remarks>
/// <para>
/// Where <see cref="Builder"/> places a page you have already drawn, a flow decides where things
/// go. Content is appended in reading order — paragraphs, headings, lists, tables, figures — and
/// the engine advances a cursor, starting a new page whenever the next block does not fit.
/// Turn on <see cref="SetTagged(string)"/> and every one of those blocks also emits its structure
/// element, which is what makes the output PDF/UA-capable.
/// </para>
/// <para>
/// <b>Both finishers consume the flow.</b> <see cref="Build"/> serialises it and
/// <see cref="IntoBuilder"/> hands it to a <see cref="Builder"/> for post-processing; either way
/// the handle is dead afterwards — like <c>fclose</c>, and unlike the builder, which survives being
/// built. The ABI takes ownership as the call is entered, so this wrapper stops tracking the handle
/// even when the call reports a failure: a second free would be a double free. Calls after that
/// raise <see cref="ObjectDisposedException"/>.
/// </para>
/// <para>
/// Not thread-safe, per semantic contract 6: confine an instance to one thread.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var flow = new Flow(PdfSize.A4, PdfMargins.Uniform(72),
///     new Dictionary&lt;string, StdFont&gt; { ["F1"] = StdFont.Helvetica });
/// using var body = new TextBlock("F1", "Helvetica", 11, 14);
/// flow.SetTagged("en-GB");
/// flow.AddText(body, "A paragraph, wrapped and broken across pages as needed.");
/// var pdf = flow.Build();
/// </code>
/// </example>
public sealed unsafe class Flow : PrismPdfHandle
{
    /// <summary>
    /// Create a flow with a page size and margins in points, exposing Standard-14 fonts under the
    /// given resource names.
    /// </summary>
    /// <param name="size">The page size.</param>
    /// <param name="margins">The page margins.</param>
    /// <param name="fonts">
    /// Resource name to Standard-14 font. A style names one of these keys; embedding a real
    /// program over one of them is <see cref="EmbedFont(string, ReadOnlySpan{byte})"/>.
    /// </param>
    public Flow(PdfSize size, PdfMargins margins, IReadOnlyDictionary<string, StdFont>? fonts = null)
        : base(NewHandle(size, margins, fonts))
    {
    }

    /// <summary>How many pages the flow has produced so far.</summary>
    public int PageCount
    {
        get
        {
            nuint count = 0;
            Native.Check(NativeMethods.prismpdf_flow_page_count(Handle, &count),
                "prismpdf_flow_page_count");
            return Native.ToCount(count);
        }
    }

    /// <summary>
    /// The vertical cursor, in points from the page bottom — for deciding whether the next block
    /// fits before it breaks.
    /// </summary>
    public double CursorY
    {
        get
        {
            double y = 0;
            Native.Check(NativeMethods.prismpdf_flow_cursor_y(Handle, &y), "prismpdf_flow_cursor_y");
            return y;
        }
    }

    /// <summary>The document title (<c>/Title</c>, §14.3.3).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Title
    {
        set => SetText(NativeMethods.prismpdf_flow_set_title, value, "prismpdf_flow_set_title");
    }

    /// <summary>The document author (<c>/Author</c>, §14.3.3).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Author
    {
        set => SetText(NativeMethods.prismpdf_flow_set_author, value, "prismpdf_flow_set_author");
    }

    // -----------------------------------------------------------------------------------------
    // Finishing — both of these consume the flow
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// <b>Consume</b> the flow and serialise it. The handle is dead afterwards.
    /// </summary>
    /// <returns>The serialized PDF.</returns>
    public byte[] Build()
    {
        var handle = Handle;
        MarkConsumed();

        byte* data = null;
        nuint len = 0;
        Native.Check(NativeMethods.prismpdf_flow_build(handle, &data, &len), "prismpdf_flow_build");
        return Native.TakeBytes(data, len);
    }

    /// <summary>
    /// <b>Consume</b> the flow into a <see cref="Builder"/> without serialising, so the document
    /// can be post-processed — a conformance pass, attachments, annotations. This is the
    /// composition point between the layout API and everything else.
    /// </summary>
    /// <returns>An owned builder; dispose it when done.</returns>
    public Builder IntoBuilder()
    {
        var handle = Handle;
        MarkConsumed();

        nint builder = 0;
        Native.Check(NativeMethods.prismpdf_flow_into_builder(handle, &builder),
            "prismpdf_flow_into_builder");
        return Builder.FromHandle(builder);
    }

    // -----------------------------------------------------------------------------------------
    // Document-level settings
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Turn on logical structure (tagging) in the given language, so the flow emits a structure
    /// tree — the prerequisite for PDF/UA and PDF/A level A.
    /// </summary>
    /// <param name="lang">The document language, e.g. <c>en-GB</c>.</param>
    public void SetTagged(string lang)
        => SetText(NativeMethods.prismpdf_flow_set_tagged, lang, "prismpdf_flow_set_tagged");

    /// <summary>
    /// Embed a real font program under a resource name — the call that makes a flowed document
    /// PDF/A- and PDF/UA-conformant.
    /// </summary>
    /// <remarks>
    /// For a document destined for either standard, do <em>not</em> also declare that name as a
    /// Standard-14 font in the constructor. The engine keeps the Standard-14 registration even
    /// once a program is embedded over the name, and the conformance pass then refuses the
    /// document with <see cref="ConformanceIssue.UnembeddedFont"/>. Embedding under a name the
    /// constructor never mentioned registers it outright, which is what a conformant document
    /// needs.
    /// </remarks>
    /// <param name="resource">The resource name a style will select.</param>
    /// <param name="program">The font program, as an sfnt (TrueType or OpenType).</param>
    /// <exception cref="PrismPdfException">The program could not be parsed as an sfnt.</exception>
    public void EmbedFont(string resource, ReadOnlySpan<byte> program)
    {
        using var strings = new Utf8Scope();
        var name = strings.Add(resource, nameof(resource));
        fixed (byte* bytes = program)
        {
            Native.Check(
                NativeMethods.prismpdf_flow_embed_font(Handle, name, bytes, (nuint)program.Length),
                "prismpdf_flow_embed_font");
        }
    }

    /// <summary>Add an <c>/Info</c> entry (§14.3.3).</summary>
    /// <param name="key">The entry key, without its leading slash.</param>
    /// <param name="value">The entry value.</param>
    public void SetInfo(string key, string value)
    {
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_set_info(
                Handle, strings.Add(key, nameof(key)), strings.Add(value, nameof(value))),
            "prismpdf_flow_set_info");
    }

    /// <summary>Add a bookmark (§12.3.3) pointing at the current position in the flow.</summary>
    /// <param name="title">The bookmark's title.</param>
    public void AddBookmark(string title)
        => SetText(NativeMethods.prismpdf_flow_add_bookmark, title, "prismpdf_flow_add_bookmark");

    // -----------------------------------------------------------------------------------------
    // Content
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Pour a paragraph in the given style, wrapping to the text column and breaking pages as
    /// needed.
    /// </summary>
    /// <param name="block">The text style.</param>
    /// <param name="text">The paragraph.</param>
    public void AddText(TextBlock block, string text)
    {
        Throw.IfNull(block);
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_text(Handle, block.Handle, strings.Add(text, nameof(text))),
            "prismpdf_flow_text");
        GC.KeepAlive(block);
    }

    /// <summary>Pour a heading, tagged <c>H1</c>…<c>H6</c> when the flow is tagged.</summary>
    /// <param name="level">The heading level, 1 to 6.</param>
    /// <param name="block">The text style.</param>
    /// <param name="text">The heading.</param>
    public void AddHeading(int level, TextBlock block, string text)
    {
        Throw.IfNull(block);
        Throw.IfNegative(level);
        Throw.IfGreaterThan(level, byte.MaxValue);

        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_heading(
                Handle, (byte)level, block.Handle, strings.Add(text, nameof(text))),
            "prismpdf_flow_heading");
        GC.KeepAlive(block);
    }

    /// <summary>Pour the document title as a tagged <c>Title</c> element.</summary>
    /// <param name="block">The text style.</param>
    /// <param name="text">The title.</param>
    public void AddTitleElement(TextBlock block, string text)
    {
        Throw.IfNull(block);
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_title_element(
                Handle, block.Handle, strings.Add(text, nameof(text))),
            "prismpdf_flow_title_element");
        GC.KeepAlive(block);
    }

    /// <summary>Pour a bulleted or numbered list.</summary>
    /// <param name="block">The text style.</param>
    /// <param name="items">The list items, in order.</param>
    /// <param name="style">Bullets or numbers.</param>
    public void AddList(TextBlock block, IReadOnlyList<string> items, ListStyle style = ListStyle.Bullet)
    {
        Throw.IfNull(block);
        using var values = new NativeUtf8Array(items, nameof(items));
        Native.Check(
            NativeMethods.prismpdf_flow_list(Handle, block.Handle, values.Pointers, values.Count, style),
            "prismpdf_flow_list");
        GC.KeepAlive(block);
    }

    /// <summary>
    /// Place a table, breaking across pages and repeating the header row where one is set.
    /// </summary>
    /// <param name="table">The table to place. Read, not consumed.</param>
    public void AddTable(Table table)
    {
        Throw.IfNull(table);
        Native.Check(NativeMethods.prismpdf_flow_table(Handle, table.Handle), "prismpdf_flow_table");
        GC.KeepAlive(table);
    }

    /// <summary>
    /// Place an image at an explicit size in points, as an artifact — decoration excluded from the
    /// structure tree. Use <see cref="AddFigure(ImageSource, double, double, string)"/> when the
    /// image carries meaning.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="width">Width in points.</param>
    /// <param name="height">Height in points.</param>
    public void AddImage(ImageSource image, double width, double height)
    {
        Throw.IfNull(image);
        Native.Check(NativeMethods.prismpdf_flow_image(Handle, image.Handle, width, height),
            "prismpdf_flow_image");
        GC.KeepAlive(image);
    }

    /// <summary>Place an image scaled to fit a maximum width, preserving aspect ratio.</summary>
    /// <param name="image">The image.</param>
    /// <param name="maxWidth">The maximum width in points.</param>
    public void AddImageFit(ImageSource image, double maxWidth)
    {
        Throw.IfNull(image);
        Native.Check(NativeMethods.prismpdf_flow_image_fit(Handle, image.Handle, maxWidth),
            "prismpdf_flow_image_fit");
        GC.KeepAlive(image);
    }

    /// <summary>
    /// Place an image as a tagged <c>Figure</c> carrying alternate text — what PDF/UA requires
    /// (§7.3), and the difference between a decoration and an accessible document.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="width">Width in points.</param>
    /// <param name="height">Height in points.</param>
    /// <param name="alt">What the figure means, in words.</param>
    public void AddFigure(ImageSource image, double width, double height, string alt)
    {
        Throw.IfNull(image);
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_figure(
                Handle, image.Handle, width, height, strings.Add(alt, nameof(alt))),
            "prismpdf_flow_figure");
        GC.KeepAlive(image);
    }

    /// <summary>A tagged <c>Figure</c> scaled to fit a maximum width, carrying alternate text.</summary>
    /// <param name="image">The image.</param>
    /// <param name="maxWidth">The maximum width in points.</param>
    /// <param name="alt">What the figure means, in words.</param>
    public void AddFigureFit(ImageSource image, double maxWidth, string alt)
    {
        Throw.IfNull(image);
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_figure_fit(
                Handle, image.Handle, maxWidth, strings.Add(alt, nameof(alt))),
            "prismpdf_flow_figure_fit");
        GC.KeepAlive(image);
    }

    /// <summary>A tagged <c>Figure</c> with a <c>Caption</c> beneath it, kept together on one page.</summary>
    /// <param name="image">The image.</param>
    /// <param name="width">Width in points.</param>
    /// <param name="height">Height in points.</param>
    /// <param name="alt">What the figure means, in words.</param>
    /// <param name="block">The caption's text style.</param>
    /// <param name="caption">The caption.</param>
    public void AddFigureWithCaption(
        ImageSource image,
        double width,
        double height,
        string alt,
        TextBlock block,
        string caption)
    {
        Throw.IfNull(image);
        Throw.IfNull(block);

        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_figure_with_caption(
                Handle, image.Handle, width, height, strings.Add(alt, nameof(alt)), block.Handle,
                strings.Add(caption, nameof(caption))),
            "prismpdf_flow_figure_with_caption");
        GC.KeepAlive(image);
        GC.KeepAlive(block);
    }

    /// <summary>
    /// A footnote tagged <c>Note</c> (PDF/UA-1). PDF/UA-2 forbids <c>Note</c> — use
    /// <see cref="AddFeNote(TextBlock, string, string, IReadOnlyList{string})"/> there.
    /// </summary>
    /// <param name="block">The text style.</param>
    /// <param name="text">The note's text.</param>
    /// <param name="id">The note's identifier, referenced by its citation.</param>
    public void AddNote(TextBlock block, string text, string id)
    {
        Throw.IfNull(block);
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_note(
                Handle, block.Handle, strings.Add(text, nameof(text)), strings.Add(id, nameof(id))),
            "prismpdf_flow_note");
        GC.KeepAlive(block);
    }

    /// <summary>
    /// A footnote tagged <c>FENote</c> (ISO 14289-2 §8.2.5.14) with citation references — the
    /// PDF/UA-2 replacement for <c>Note</c>.
    /// </summary>
    /// <param name="block">The text style.</param>
    /// <param name="text">The note's text.</param>
    /// <param name="id">The note's identifier.</param>
    /// <param name="citations">The identifiers of the elements citing this note.</param>
    public void AddFeNote(
        TextBlock block,
        string text,
        string id,
        IReadOnlyList<string> citations)
    {
        Throw.IfNull(block);

        using var strings = new Utf8Scope();
        var textPtr = strings.Add(text, nameof(text));
        var idPtr = strings.Add(id, nameof(id));
        using var values = new NativeUtf8Array(citations, nameof(citations));

        Native.Check(
            NativeMethods.prismpdf_flow_fenote(
                Handle, block.Handle, textPtr, idPtr, values.Pointers, values.Count),
            "prismpdf_flow_fenote");
        GC.KeepAlive(block);
    }

    /// <summary>
    /// A formula tagged <c>Formula</c> with replacement text — how a mathematical expression is
    /// made readable to assistive technology.
    /// </summary>
    /// <param name="block">The text style.</param>
    /// <param name="text">The formula as it is drawn.</param>
    /// <param name="actualText">The formula as it should be read (<c>/ActualText</c>).</param>
    public void AddFormula(TextBlock block, string text, string actualText)
    {
        Throw.IfNull(block);
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_formula(
                Handle, block.Handle, strings.Add(text, nameof(text)),
                strings.Add(actualText, nameof(actualText))),
            "prismpdf_flow_formula");
        GC.KeepAlive(block);
    }

    /// <summary>A running header drawn at the top of every page, as an artifact.</summary>
    /// <param name="block">The text style.</param>
    /// <param name="text">The header text.</param>
    public void SetHeader(TextBlock block, string text)
    {
        Throw.IfNull(block);
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_set_header(
                Handle, block.Handle, strings.Add(text, nameof(text))),
            "prismpdf_flow_set_header");
        GC.KeepAlive(block);
    }

    /// <summary>A running footer drawn at the bottom of every page, as an artifact.</summary>
    /// <param name="block">The text style.</param>
    /// <param name="text">The footer text.</param>
    public void SetFooter(TextBlock block, string text)
    {
        Throw.IfNull(block);
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_flow_set_footer(
                Handle, block.Handle, strings.Add(text, nameof(text))),
            "prismpdf_flow_set_footer");
        GC.KeepAlive(block);
    }

    /// <summary>Advance the cursor without drawing.</summary>
    /// <param name="dy">The distance in points.</param>
    public void AddSpace(double dy)
        => Native.Check(NativeMethods.prismpdf_flow_space(Handle, dy), "prismpdf_flow_space");

    /// <summary>Finish the current page and start a new one.</summary>
    public void PageBreak()
        => Native.Check(NativeMethods.prismpdf_flow_page_break(Handle), "prismpdf_flow_page_break");

    private protected override void Free(nint handle) => NativeMethods.prismpdf_flow_free(handle);

    private static nint NewHandle(
        PdfSize size,
        PdfMargins margins,
        IReadOnlyDictionary<string, StdFont>? fonts)
    {
        var count = fonts?.Count ?? 0;
        var names = new string[count];
        var values = new StdFont[count];
        if (fonts is not null)
        {
            var i = 0;
            foreach (var entry in fonts)
            {
                names[i] = entry.Key ?? throw new ArgumentException("A font name is null.", nameof(fonts));
                values[i] = entry.Value;
                i++;
            }
        }

        using var fontNames = new NativeUtf8Array(names, nameof(fonts));
        var page = stackalloc double[2] { size.Width, size.Height };
        var box = stackalloc double[4] { margins.Top, margins.Right, margins.Bottom, margins.Left };

        fixed (StdFont* stdFonts = values)
        {
            return NativeMethods.prismpdf_flow_new(
                page, box, fontNames.Pointers, stdFonts, fontNames.Count);
        }
    }

    private void SetText(TextSetter setter, string value, string operation)
    {
        Throw.IfNull(value);

        var text = Native.AllocUtf8(value);
        try
        {
            Native.Check(setter(Handle, text), operation);
        }
        finally
        {
            Native.FreeUtf8(text);
        }
    }

    private delegate PrismPdfStatus TextSetter(nint flow, byte* value);
}
