using PrismPdf.Interop;

namespace PrismPdf.Collections;

/// <summary>
/// A document's terminal interactive form fields (§12.7). Empty when there is no AcroForm.
/// </summary>
public sealed unsafe class FormFieldList : NativeList<FormField>
{
    internal FormFieldList(nint handle)
        : base(handle)
    {
    }

    /// <inheritdoc/>
    public override int Count
    {
        get
        {
            nuint len = 0;
            Native.Check(NativeMethods.prismpdf_form_field_list_len(Handle, &len),
                "prismpdf_form_field_list_len");
            return Native.ToCount(len);
        }
    }

    /// <inheritdoc/>
    public override FormField this[int index]
    {
        get
        {
            nint item = 0;
            Native.Check(
                NativeMethods.prismpdf_form_field_list_get(Handle, Native.ToIndex(index, nameof(index)), &item),
                "prismpdf_form_field_list_get");
            return new FormField(this, item);
        }
    }

    private protected override void Free(nint handle) => NativeMethods.prismpdf_form_field_list_free(handle);
}

/// <summary>One terminal form field, borrowed from its <see cref="FormFieldList"/>.</summary>
public sealed unsafe class FormField : BorrowedItem
{
    internal FormField(FormFieldList owner, nint item)
        : base(owner, item)
    {
    }

    /// <summary>The fully-qualified field name (§12.7.3.2) — what <c>FillForm</c> matches on.</summary>
    public string Name
    {
        get
        {
            byte* text = null;
            Native.Check(NativeMethods.prismpdf_form_field_name(Item, &text), "prismpdf_form_field_name");
            return Native.TakeString(text) ?? string.Empty;
        }
    }

    /// <summary><c>/FT</c> — <c>Tx</c>, <c>Btn</c>, <c>Ch</c> or <c>Sig</c>; empty when unknown.</summary>
    public string FieldType
    {
        get
        {
            byte* text = null;
            Native.Check(NativeMethods.prismpdf_form_field_type(Item, &text), "prismpdf_form_field_type");
            return Native.TakeString(text) ?? string.Empty;
        }
    }

    /// <summary>
    /// The current <c>/V</c> as text, or <see langword="null"/> when unset or not textual — absence,
    /// not an error.
    /// </summary>
    public string? Value
    {
        get
        {
            byte* text = null;
            return Native.CheckOptional(NativeMethods.prismpdf_form_field_value(Item, &text),
                "prismpdf_form_field_value")
                ? Native.TakeString(text)
                : null;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Name} ({FieldType}) = {Value ?? "<unset>"}";
}
