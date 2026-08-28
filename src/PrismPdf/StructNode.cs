using PrismPdf.Interop;

namespace PrismPdf;

/// <summary>
/// One element of the logical structure tree (§14.7) — what makes a PDF tagged, and therefore
/// what makes it readable by assistive technology.
/// </summary>
/// <remarks>
/// <para>
/// A node carries a tag (<c>P</c>, <c>H1</c>, <c>Figure</c>, <c>Table</c>…), optional accessibility
/// text, and children: either further nodes, or references into page content by
/// <see cref="AddContent(int, int)"/>, whose <c>mcid</c> matches a
/// <see cref="Content.BeginMarkedContent(string, int)"/> in the page's content stream. That
/// pairing is the whole mechanism: the content stream marks a range, the structure node claims it.
/// </para>
/// <para>
/// Two calls <em>consume</em> a node — <see cref="AddChild(StructNode)"/> and
/// <see cref="Builder.AddStructureNode(StructNode)"/>. On success the node belongs to its new
/// parent and this wrapper is invalid; on a validation failure ownership stays with the caller and
/// the node is still usable. That is semantic contract 3.
/// </para>
/// </remarks>
public sealed unsafe class StructNode : PrismPdfHandle
{
    /// <summary>Create an empty structure element with <c>/S tag</c> (§14.7.4.2).</summary>
    /// <param name="tag">The structure type, e.g. <c>P</c>, <c>H1</c> or <c>Figure</c>.</param>
    public StructNode(string tag)
        : base(NewHandle(tag))
    {
    }

    /// <summary>Alternate text (<c>/Alt</c>, §14.9.3) — what a figure means, in words.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Alt
    {
        set => SetText(NativeMethods.prismpdf_struct_node_set_alt, value, "prismpdf_struct_node_set_alt");
    }

    /// <summary>Replacement text (<c>/ActualText</c>, §14.9.4) — what the marked content says.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string ActualText
    {
        set => SetText(NativeMethods.prismpdf_struct_node_set_actual_text, value,
            "prismpdf_struct_node_set_actual_text");
    }

    /// <summary>This element's natural language (<c>/Lang</c>, §14.9.2), e.g. <c>en-GB</c>.</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Lang
    {
        set => SetText(NativeMethods.prismpdf_struct_node_set_lang, value, "prismpdf_struct_node_set_lang");
    }

    /// <summary>The PDF 2.0 structure namespace URI (<c>/NS</c>, §14.7.4).</summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Namespace
    {
        set => SetText(NativeMethods.prismpdf_struct_node_set_namespace, value,
            "prismpdf_struct_node_set_namespace");
    }

    /// <summary>
    /// The element identifier (<c>/ID</c>, §14.7.4.2) — what a structure destination or a
    /// <c>/Ref</c> from another element points at.
    /// </summary>
    /// <remarks>Write-only: the ABI exposes a setter, not a getter.</remarks>
    public string Id
    {
        set => SetText(NativeMethods.prismpdf_struct_node_set_id, value, "prismpdf_struct_node_set_id");
    }

    /// <summary>Add a <c>/Ref</c> target by element ID (§14.7.4.2, PDF 2.0).</summary>
    /// <param name="targetId">The <see cref="Id"/> of the element this one refers to.</param>
    public void AddReference(string targetId)
        => SetText(NativeMethods.prismpdf_struct_node_add_reference, targetId,
            "prismpdf_struct_node_add_reference");

    /// <summary>Add a name-valued structure attribute (§14.7.6).</summary>
    /// <param name="owner">The attribute owner, e.g. <c>Layout</c> or <c>Table</c>.</param>
    /// <param name="key">The attribute key, e.g. <c>Placement</c>.</param>
    /// <param name="value">The name value, without its leading slash.</param>
    public void AddNameAttribute(string owner, string key, string value)
    {
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_struct_node_add_name_attribute(
                Handle,
                strings.Add(owner, nameof(owner)),
                strings.Add(key, nameof(key)),
                strings.Add(value, nameof(value))),
            "prismpdf_struct_node_add_name_attribute");
    }

    /// <summary>Add an integer-valued structure attribute (§14.7.6).</summary>
    /// <param name="owner">The attribute owner, e.g. <c>Table</c>.</param>
    /// <param name="key">The attribute key, e.g. <c>ColSpan</c>.</param>
    /// <param name="value">The integer value.</param>
    public void AddIntegerAttribute(string owner, string key, long value)
    {
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_struct_node_add_integer_attribute(
                Handle, strings.Add(owner, nameof(owner)), strings.Add(key, nameof(key)), value),
            "prismpdf_struct_node_add_integer_attribute");
    }

    /// <summary>Add a text-valued structure attribute (§14.7.6).</summary>
    /// <param name="owner">The attribute owner.</param>
    /// <param name="key">The attribute key.</param>
    /// <param name="value">The text value.</param>
    public void AddTextAttribute(string owner, string key, string value)
    {
        using var strings = new Utf8Scope();
        Native.Check(
            NativeMethods.prismpdf_struct_node_add_text_attribute(
                Handle,
                strings.Add(owner, nameof(owner)),
                strings.Add(key, nameof(key)),
                strings.Add(value, nameof(value))),
            "prismpdf_struct_node_add_text_attribute");
    }

    /// <summary>
    /// Claim a marked-content range in a page (§14.7.4.3) — the other half of
    /// <see cref="Content.BeginMarkedContent(string, int)"/>.
    /// </summary>
    /// <param name="pageIndex">Zero-based index of the page whose content stream carries the mark.</param>
    /// <param name="mcid">The marked-content identifier used in that content stream.</param>
    public void AddContent(int pageIndex, int mcid)
    {
        Throw.IfNegative(mcid);
        Native.Check(
            NativeMethods.prismpdf_struct_node_add_content(
                Handle, Native.ToIndex(pageIndex, nameof(pageIndex)), (uint)mcid),
            "prismpdf_struct_node_add_content");
    }

    /// <summary>Claim a form widget as a child (<c>/OBJR</c>, §14.7.4.3).</summary>
    /// <param name="fieldIndex">
    /// Zero-based order in which the field was added to the builder — the first
    /// <see cref="Builder.AddCheckbox(int, PdfRect, string, bool, string?)"/> is 0.
    /// </param>
    public void AddWidget(int fieldIndex)
        => Native.Check(
            NativeMethods.prismpdf_struct_node_add_widget(
                Handle, Native.ToIndex(fieldIndex, nameof(fieldIndex))),
            "prismpdf_struct_node_add_widget");

    /// <summary>Claim an annotation as a child (<c>/OBJR</c>, §14.7.4.3).</summary>
    /// <param name="annotationIndex">
    /// Zero-based order in which the annotation was added to the builder, counting links and notes
    /// together.
    /// </param>
    public void AddAnnotation(int annotationIndex)
        => Native.Check(
            NativeMethods.prismpdf_struct_node_add_annotation(
                Handle, Native.ToIndex(annotationIndex, nameof(annotationIndex))),
            "prismpdf_struct_node_add_annotation");

    /// <summary>
    /// Append a child in reading order (§14.7.4.2). <b>Consuming:</b> on success
    /// <paramref name="child"/> belongs to this node and its wrapper is invalid.
    /// </summary>
    /// <param name="child">The node to transfer. Must not be this node.</param>
    public void AddChild(StructNode child)
    {
        Throw.IfNull(child);

        var status = NativeMethods.prismpdf_struct_node_add_child(Handle, child.Handle);
        if (status == PrismPdfStatus.Ok)
        {
            child.MarkConsumed();
        }

        Native.Check(status, "prismpdf_struct_node_add_child");
    }

    /// <summary>
    /// Associate an embedded file with this element (<c>/AF</c>, §14.13.6, PDF 2.0) — how a figure
    /// carries the data it depicts.
    /// </summary>
    /// <param name="name">The file name recorded in the file specification.</param>
    /// <param name="mime">The MIME type, e.g. <c>text/csv</c>.</param>
    /// <param name="relationship">The <c>/AFRelationship</c> value, e.g. <c>Data</c>.</param>
    /// <param name="description">An optional human-readable description.</param>
    /// <param name="data">The file's bytes.</param>
    public void AssociateFile(
        string name,
        string mime,
        string relationship,
        string? description,
        ReadOnlySpan<byte> data)
    {
        using var strings = new Utf8Scope();
        var namePtr = strings.Add(name, nameof(name));
        var mimePtr = strings.Add(mime, nameof(mime));
        var relationshipPtr = strings.Add(relationship, nameof(relationship));
        var descriptionPtr = strings.AddOptional(description);

        fixed (byte* bytes = data)
        {
            Native.Check(
                NativeMethods.prismpdf_struct_node_associate_file(
                    Handle, namePtr, mimePtr, relationshipPtr, descriptionPtr, bytes, (nuint)data.Length),
                "prismpdf_struct_node_associate_file");
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_struct_node_free(handle);

    /// <summary>
    /// Record that the builder took ownership. Called only after a consuming call returned
    /// <c>Ok</c> — semantic contract 3.
    /// </summary>
    internal void MarkTransferred() => MarkConsumed();

    private static nint NewHandle(string tag)
    {
        Throw.IfNull(tag);

        var text = Native.AllocUtf8(tag);
        try
        {
            return NativeMethods.prismpdf_struct_node_new(text);
        }
        finally
        {
            Native.FreeUtf8(text);
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

    private delegate PrismPdfStatus TextSetter(nint node, byte* value);
}
