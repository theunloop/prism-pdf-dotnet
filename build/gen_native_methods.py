#!/usr/bin/env python3
"""Generate src/PrismPdf/Interop/NativeMethods.cs from native/include/prismpdf.h.

The binding author's guide requires the raw layer to be "one flat, mechanical, 1:1 projection of
prismpdf.h". Deriving it from the header by script is the cheapest way to keep that literally true:
regenerate after vendoring a newer header and the diff is exactly the ABI's additions.

Only the areas listed in AREAS below are emitted; as of core v0.4.0 that is every export in the
header, and `NativeSurfaceTests` fails if a declaration and the header disagree.

    python3 build/gen_native_methods.py           # rewrite NativeMethods.cs
    python3 build/gen_native_methods.py --check   # exit 1 if the file is stale
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
HEADER = ROOT / "native" / "include" / "prismpdf.h"
OUTPUT = ROOT / "src" / "PrismPdf" / "Interop" / "NativeMethods.cs"

# Ordered (heading, [exact prismpdf_* names]) groups. A name listed here but absent from the header
# is a hard error: it means the vendored header moved and the binding must be revisited.
AREAS: list[tuple[str, list[str]]] = [
    ("Structured failure diagnostics", [
        "prismpdf_last_error",
        "prismpdf_error_info_status",
        "prismpdf_error_info_message",
        "prismpdf_error_info_free",
    ]),
    ("Library-level: version and the two deallocators", [
        "prismpdf_version",
        "prismpdf_string_free",
        "prismpdf_bytes_free",
    ]),
    ("Open and lifecycle", [
        "prismpdf_document_open",
        "prismpdf_document_open_with_password",
        "prismpdf_document_open_with_options",
        "prismpdf_document_open_with_limits",
        "prismpdf_document_open_with_private_key",
        "prismpdf_document_free",
    ]),
    ("Open options (the ABI-extensible replacement for PrismPdfLimits)", [
        "prismpdf_open_options_new",
        "prismpdf_open_options_set_max_depth",
        "prismpdf_open_options_set_max_objstm_objects",
        "prismpdf_open_options_set_max_objects",
        "prismpdf_open_options_set_password",
        "prismpdf_open_options_free",
    ]),
    ("Open report: strict versus recovered", [
        "prismpdf_document_open_report",
        "prismpdf_open_report_mode",
        "prismpdf_open_report_diagnostic_count",
        "prismpdf_open_report_diagnostic",
        "prismpdf_open_report_free",
    ]),
    ("Read path", [
        "prismpdf_document_page_count",
        "prismpdf_document_version",
        "prismpdf_document_min_version",
        "prismpdf_page_text",
        "prismpdf_page_text_positioned",
        "prismpdf_document_text",
        "prismpdf_document_xmp",
        "prismpdf_document_info",
        "prismpdf_document_creation_date",
        "prismpdf_document_modification_date",
        "prismpdf_document_structure_namespaces",
        "prismpdf_document_signature_vri_keys",
    ]),
    ("Write and transform", [
        "prismpdf_document_save",
        "prismpdf_document_save_report",
        "prismpdf_document_save_compact",
        "prismpdf_document_save_compact_report",
        "prismpdf_document_save_packed",
        "prismpdf_document_save_packed_report",
        "prismpdf_document_save_as",
        "prismpdf_document_save_as_report",
        "prismpdf_document_extract_pages",
        "prismpdf_document_extract_pages_report",
        "prismpdf_document_rotate_page",
        "prismpdf_document_rotate_page_report",
        "prismpdf_document_subset_fonts",
        "prismpdf_document_subset_fonts_report",
        "prismpdf_merge",
        "prismpdf_merge_report",
    ]),
    ("Transform report", [
        "prismpdf_transform_report_bytes",
        "prismpdf_transform_report_rewrite_mode",
        "prismpdf_transform_report_signature_effect",
        "prismpdf_transform_report_structure_effect",
        "prismpdf_transform_report_free",
    ]),
    ("Annotations (12.5)", [
        "prismpdf_page_annotations",
        "prismpdf_annotation_list_len",
        "prismpdf_annotation_list_get",
        "prismpdf_annotation_list_free",
        "prismpdf_annotation_subtype",
        "prismpdf_annotation_rect",
        "prismpdf_annotation_contents",
        "prismpdf_annotation_uri",
        "prismpdf_annotation_dest_page",
    ]),
    ("Interactive forms (12.7)", [
        "prismpdf_document_form_fields",
        "prismpdf_form_field_list_len",
        "prismpdf_form_field_list_get",
        "prismpdf_form_field_list_free",
        "prismpdf_form_field_name",
        "prismpdf_form_field_type",
        "prismpdf_form_field_value",
        "prismpdf_document_fill_form",
        "prismpdf_document_fill_form_report",
        "prismpdf_document_flatten_form",
        "prismpdf_document_flatten_form_report",
    ]),
    ("Outline / bookmarks (12.3.3)", [
        "prismpdf_document_outline",
        "prismpdf_outline_list_len",
        "prismpdf_outline_list_get",
        "prismpdf_outline_list_free",
        "prismpdf_outline_item_title",
        "prismpdf_outline_item_dest_page",
        "prismpdf_outline_item_child_count",
        "prismpdf_outline_item_child",
    ]),
    ("Embedded files (7.11)", [
        "prismpdf_document_attachments",
        "prismpdf_attachment_list_len",
        "prismpdf_attachment_list_get",
        "prismpdf_attachment_list_free",
        "prismpdf_attachment_name",
        "prismpdf_attachment_data",
        "prismpdf_attachment_mime",
        "prismpdf_attachment_relationship",
        "prismpdf_attachment_description",
    ]),
    ("Fonts (9.5-9.7, 9.9)", [
        "prismpdf_document_fonts",
        "prismpdf_font_list_len",
        "prismpdf_font_list_get",
        "prismpdf_font_list_free",
        "prismpdf_font_base_font",
        "prismpdf_font_subtype",
        "prismpdf_font_program_format",
        "prismpdf_font_program",
        "prismpdf_font_metrics",
        "prismpdf_font_family_name",
    ]),
    ("Images (8.6, 8.9)", [
        "prismpdf_page_images",
        "prismpdf_image_list_len",
        "prismpdf_image_list_get",
        "prismpdf_image_list_free",
        "prismpdf_image_info",
        "prismpdf_image_color_space",
        "prismpdf_image_components",
        "prismpdf_image_kind",
        "prismpdf_image_data",
    ]),
    ("String lists", [
        "prismpdf_string_list_len",
        "prismpdf_string_list_get",
        "prismpdf_string_list_free",
    ]),
    ("Access permissions (7.6.3.2)", [
        "prismpdf_permissions_restricted",
        "prismpdf_permissions_all",
        "prismpdf_permissions_allow_print",
        "prismpdf_permissions_allow_modify",
        "prismpdf_permissions_allow_copy",
        "prismpdf_permissions_allow_annotate",
        "prismpdf_permissions_allow_fill_forms",
        "prismpdf_permissions_allow_accessibility",
        "prismpdf_permissions_allow_assemble",
        "prismpdf_permissions_allow_print_high_res",
    ]),
    ("Encryption (7.6)", [
        "prismpdf_document_save_encrypted",
        "prismpdf_document_save_encrypted_with",
        "prismpdf_document_save_encrypted_public_key",
        "prismpdf_document_save_encrypted_with_mac",
        "prismpdf_document_verify_pdf_mac",
    ]),
    ("Signing (12.8)", [
        "prismpdf_sign_settings_new",
        "prismpdf_sign_settings_free",
        "prismpdf_sign_settings_set_name",
        "prismpdf_sign_settings_set_reason",
        "prismpdf_sign_settings_set_location",
        "prismpdf_sign_settings_set_contact_info",
        "prismpdf_sign_settings_set_signing_time",
        "prismpdf_sign_settings_set_pades",
        "prismpdf_sign_settings_set_appearance",
        "prismpdf_sign_settings_set_timestamp",
        "prismpdf_document_sign",
        "prismpdf_document_sign_with",
        "prismpdf_document_sign_with_mac",
        "prismpdf_document_timestamp",
    ]),
    ("Verification (12.8, 12.8.4)", [
        "prismpdf_document_verify_signatures",
        "prismpdf_document_verify_signatures_with",
        "prismpdf_document_verify_signatures_ltv",
        "prismpdf_signature_list_len",
        "prismpdf_signature_list_get",
        "prismpdf_signature_list_free",
        "prismpdf_signature_valid",
        "prismpdf_signature_signer",
        "prismpdf_signature_covered_bytes",
        "prismpdf_signature_signing_time",
        "prismpdf_signature_timestamp_time",
        "prismpdf_signature_trusted",
        "prismpdf_signature_pades",
        "prismpdf_signature_revocation",
    ]),
    ("Content streams (8.2, 9.4, 14.6)", [
        "prismpdf_content_new",
        "prismpdf_content_free",
        "prismpdf_content_bytes",
        "prismpdf_content_save",
        "prismpdf_content_restore",
        "prismpdf_content_transform",
        "prismpdf_content_set_line_width",
        "prismpdf_content_set_fill_gray",
        "prismpdf_content_set_stroke_gray",
        "prismpdf_content_set_fill_rgb",
        "prismpdf_content_set_stroke_rgb",
        "prismpdf_content_set_fill_cmyk",
        "prismpdf_content_move_to",
        "prismpdf_content_line_to",
        "prismpdf_content_curve_to",
        "prismpdf_content_rect",
        "prismpdf_content_close_path",
        "prismpdf_content_stroke",
        "prismpdf_content_fill",
        "prismpdf_content_fill_and_stroke",
        "prismpdf_content_begin_text",
        "prismpdf_content_end_text",
        "prismpdf_content_set_char_spacing",
        "prismpdf_content_set_word_spacing",
        "prismpdf_content_set_leading",
        "prismpdf_content_text_move",
        "prismpdf_content_set_text_matrix",
        "prismpdf_content_next_line",
        "prismpdf_content_begin_artifact",
        "prismpdf_content_end_marked_content",
        "prismpdf_content_set_fill_color_space",
        "prismpdf_content_set_fill_color",
        "prismpdf_content_do_xobject",
        "prismpdf_content_inline_image",
        "prismpdf_content_set_font",
        "prismpdf_content_show_text",
        "prismpdf_content_show_str",
        "prismpdf_content_show_glyphs",
        "prismpdf_content_begin_marked_content",
        "prismpdf_content_begin_af_marked_content",
    ]),
    ("Image sources (8.9) — page and flow image resources", [
        "prismpdf_image_source_from_jpeg",
        "prismpdf_image_source_from_rgb",
        "prismpdf_image_source_from_gray",
        "prismpdf_image_source_from_rgba",
        "prismpdf_image_source_size",
        "prismpdf_image_source_free",
    ]),
    ("Page specifications (7.7.3.3, 7.8.2)", [
        "prismpdf_page_spec_new",
        "prismpdf_page_spec_free",
        "prismpdf_page_spec_set_media_box",
        "prismpdf_page_spec_add_standard_font",
        "prismpdf_page_spec_add_embedded_font",
        "prismpdf_page_spec_add_image",
    ]),
    ("Logical structure nodes (14.7)", [
        "prismpdf_struct_node_new",
        "prismpdf_struct_node_free",
        "prismpdf_struct_node_set_alt",
        "prismpdf_struct_node_set_actual_text",
        "prismpdf_struct_node_set_lang",
        "prismpdf_struct_node_set_namespace",
        "prismpdf_struct_node_set_id",
        "prismpdf_struct_node_add_reference",
        "prismpdf_struct_node_add_name_attribute",
        "prismpdf_struct_node_add_integer_attribute",
        "prismpdf_struct_node_add_text_attribute",
        "prismpdf_struct_node_add_content",
        "prismpdf_struct_node_add_widget",
        "prismpdf_struct_node_add_annotation",
        "prismpdf_struct_node_add_child",
        "prismpdf_struct_node_associate_file",
    ]),
    ("Document builder (7.7)", [
        "prismpdf_builder_new",
        "prismpdf_builder_free",
        "prismpdf_builder_build",
        "prismpdf_builder_build_for",
        "prismpdf_builder_set_version",
        "prismpdf_builder_set_media_box",
        "prismpdf_builder_add_page",
        "prismpdf_builder_add_page_spec",
        "prismpdf_builder_set_title",
        "prismpdf_builder_set_author",
        "prismpdf_builder_set_subject",
        "prismpdf_builder_set_keywords",
        "prismpdf_builder_set_creator",
        "prismpdf_builder_set_info",
        "prismpdf_builder_clear_info",
        "prismpdf_builder_set_metadata_xmp",
        "prismpdf_builder_set_lang",
        "prismpdf_builder_set_file_id",
        "prismpdf_builder_set_utf8_text_strings",
        "prismpdf_builder_set_display_doc_title",
        "prismpdf_builder_set_output_intent",
        "prismpdf_builder_add_outline",
        "prismpdf_builder_attach_file",
        "prismpdf_builder_add_link_uri",
        "prismpdf_builder_add_link_page",
        "prismpdf_builder_add_link_element",
        "prismpdf_builder_add_link_document_part",
        "prismpdf_builder_add_note",
        "prismpdf_builder_add_checkbox",
        "prismpdf_builder_add_structure_node",
        "prismpdf_builder_set_structure_namespace",
    ]),
    ("Text styles and measurement (9.4)", [
        "prismpdf_text_block_new",
        "prismpdf_text_block_free",
        "prismpdf_measure_text",
        "prismpdf_wrap_text",
    ]),
    ("Tables", [
        "prismpdf_table_new",
        "prismpdf_table_free",
        "prismpdf_table_set_font",
        "prismpdf_table_set_size",
        "prismpdf_table_set_leading",
        "prismpdf_table_set_padding",
        "prismpdf_table_set_border",
        "prismpdf_table_set_header_row",
        "prismpdf_table_set_align",
        "prismpdf_table_add_row",
    ]),
    ("Flowed layout", [
        "prismpdf_flow_new",
        "prismpdf_flow_free",
        "prismpdf_flow_build",
        "prismpdf_flow_into_builder",
        "prismpdf_flow_set_tagged",
        "prismpdf_flow_embed_font",
        "prismpdf_flow_set_title",
        "prismpdf_flow_set_author",
        "prismpdf_flow_set_info",
        "prismpdf_flow_add_bookmark",
        "prismpdf_flow_text",
        "prismpdf_flow_heading",
        "prismpdf_flow_title_element",
        "prismpdf_flow_list",
        "prismpdf_flow_table",
        "prismpdf_flow_image",
        "prismpdf_flow_image_fit",
        "prismpdf_flow_figure",
        "prismpdf_flow_figure_fit",
        "prismpdf_flow_figure_with_caption",
        "prismpdf_flow_note",
        "prismpdf_flow_fenote",
        "prismpdf_flow_formula",
        "prismpdf_flow_set_header",
        "prismpdf_flow_set_footer",
        "prismpdf_flow_space",
        "prismpdf_flow_page_break",
        "prismpdf_flow_page_count",
        "prismpdf_flow_cursor_y",
    ]),
    ("Declarative composition (the arena model)", [
        "prismpdf_composition_new",
        "prismpdf_composition_free",
        "prismpdf_composition_build",
        "prismpdf_composition_set_tagged_language",
        "prismpdf_composition_add_page",
        "prismpdf_composition_page_set_header",
        "prismpdf_composition_page_set_footer",
        "prismpdf_composition_container_free",
        "prismpdf_composition_container_set_column",
        "prismpdf_composition_column_add_item",
        "prismpdf_composition_container_set_row",
        "prismpdf_composition_row_add_fixed",
        "prismpdf_composition_row_add_relative",
        "prismpdf_composition_row_add_auto",
        "prismpdf_composition_container_set_table",
        "prismpdf_composition_table_add_fixed_column",
        "prismpdf_composition_table_add_relative_column",
        "prismpdf_composition_table_add_auto_column",
        "prismpdf_composition_table_set_header",
        "prismpdf_composition_table_add_row",
        "prismpdf_composition_table_row_add_cell",
        "prismpdf_composition_container_set_text",
        "prismpdf_composition_container_set_image",
        "prismpdf_composition_container_set_page_break",
        "prismpdf_composition_container_set_semantic",
        "prismpdf_composition_container_set_heading",
        "prismpdf_composition_container_set_link",
        "prismpdf_composition_container_set_figure",
        "prismpdf_composition_container_set_padding",
        "prismpdf_composition_container_set_width",
        "prismpdf_composition_container_set_height",
        "prismpdf_composition_container_set_alignment",
        "prismpdf_composition_container_set_extend",
        "prismpdf_composition_container_set_border",
        "prismpdf_composition_container_set_background",
    ]),
    ("Conformance: PDF/A (ISO 19005) and PDF/UA (ISO 14289)", [
        "prismpdf_pdfa_part",
        "prismpdf_pdfa_allows_attachments",
        "prismpdf_pdfa_code",
        "prismpdf_xmp_metadata_new",
        "prismpdf_xmp_metadata_free",
        "prismpdf_xmp_metadata_set_title",
        "prismpdf_xmp_metadata_add_author",
        "prismpdf_xmp_metadata_set_subject",
        "prismpdf_xmp_metadata_set_keywords",
        "prismpdf_xmp_metadata_set_creator_tool",
        "prismpdf_xmp_metadata_set_producer",
        "prismpdf_xmp_metadata_set_create_date",
        "prismpdf_xmp_metadata_set_modify_date",
        "prismpdf_builder_make_pdfa",
        "prismpdf_builder_make_pdfa_with_output_intent",
        "prismpdf_builder_make_pdfua",
        "prismpdf_builder_make_pdfua2",
    ]),
    ("COS inspection (7.3)", [
        "prismpdf_document_catalog_object",
        "prismpdf_document_page_object",
        "prismpdf_document_get_object",
        "prismpdf_document_resolve_object",
        "prismpdf_object_free",
        "prismpdf_object_clone",
        "prismpdf_object_kind",
        "prismpdf_object_boolean",
        "prismpdf_object_integer",
        "prismpdf_object_real",
        "prismpdf_object_bytes",
        "prismpdf_object_reference",
        "prismpdf_object_array_len",
        "prismpdf_object_array_get",
        "prismpdf_object_dictionary_len",
        "prismpdf_object_dictionary_get",
        "prismpdf_object_stream_raw",
    ]),
    ("COS construction (7.3)", [
        "prismpdf_object_new_null",
        "prismpdf_object_new_boolean",
        "prismpdf_object_new_integer",
        "prismpdf_object_new_real",
        "prismpdf_object_new_string",
        "prismpdf_object_new_name",
        "prismpdf_object_new_reference",
        "prismpdf_object_new_array",
        "prismpdf_object_new_dictionary",
        "prismpdf_object_new_stream",
        "prismpdf_object_array_push",
        "prismpdf_object_dictionary_set",
    ]),
    ("Object editing (7.5)", [
        "prismpdf_edit_new",
        "prismpdf_edit_free",
        "prismpdf_edit_set_object",
        "prismpdf_edit_commit",
    ]),
]

# Value types that cross by layout, projected as the matching C# struct rather than nint.
VALUE_STRUCTS = {
    "PrismPdfDate": "PrismPdfDate",
    "PrismPdfLimits": "PrismPdfLimits",
    "PrismPdfCompositionPageStyle": "PrismPdfCompositionPageStyle",
    "PrismPdfCompositionColor": "PrismPdfCompositionColor",
    "PrismPdfCompositionTextStyle": "PrismPdfCompositionTextStyle",
}

# #[repr(C)] enums, mapped to their idiomatic C# names. Rule 9 of the binding author's guide keeps
# the variant names and drops the PrismPdf prefix from the type; PrismPdfStatus keeps its prefix
# because it is the error contract and `Status` alone is too generic a public name in C#.
ENUMS = {
    "PrismPdfStatus": "PrismPdfStatus",
    "PrismPdfOpenMode": "OpenMode",
    "PrismPdfRecoveryReason": "RecoveryReason",
    "PrismPdfRewriteMode": "RewriteMode",
    "PrismPdfSignatureEffect": "SignatureEffect",
    "PrismPdfStructureEffect": "StructureEffect",
    "PrismPdfFontFormat": "FontFormat",
    "PrismPdfColorSpace": "ColorSpace",
    "PrismPdfImageKind": "ImageKind",
    "PrismPdfRevocation": "Revocation",
    "PrismPdfStdFont": "StdFont",
    "PrismPdfPdfAConformance": "PdfAConformance",
    "PrismPdfConformanceIssue": "ConformanceIssue",
    "PrismPdfObjectKind": "ObjectKind",
    "PrismPdfEditCommitMode": "EditCommitMode",
    "PrismPdfAlign": "Align",
    "PrismPdfListStyle": "ListStyle",
    "PrismPdfCompositionSemantic": "CompositionSemantic",
    "PrismPdfCompositionImageSizing": "CompositionImageSizing",
    "PrismPdfCompositionHorizontalAlign": "CompositionHorizontalAlign",
    "PrismPdfCompositionVerticalAlign": "CompositionVerticalAlign",
}

PRIMITIVES = {
    "void": "void",
    "bool": "byte",          # stdbool.h bool is one byte; 0 false, non-zero true.
    "char": "byte",
    "uint8_t": "byte",
    "int8_t": "sbyte",
    "uint16_t": "ushort",
    "int16_t": "short",
    "uint32_t": "uint",
    "int32_t": "int",
    "uint64_t": "ulong",
    "int64_t": "long",
    "uintptr_t": "nuint",
    "intptr_t": "nint",
    "double": "double",
    "float": "float",
}


def parse_header(text: str) -> dict[str, tuple[str, list[tuple[str, str]]]]:
    """Return {name: (return_type, [(c_type, param_name), ...])} for every export."""
    body = text[text.index("extern \"C\" {"):] if 'extern "C" {' in text else text
    # Collapse each declaration onto one line before matching.
    flat = re.sub(r"\s+", " ", body)
    decls = re.findall(r"([A-Za-z_][A-Za-z0-9_ *]*?)\b(prismpdf_[a-z0-9_]+)\s*\(([^)]*)\)\s*;", flat)

    out: dict[str, tuple[str, list[tuple[str, str]]]] = {}
    for ret, name, params in decls:
        param_list: list[tuple[str, str]] = []
        cleaned = params.strip()
        if cleaned and cleaned != "void":
            for raw in cleaned.split(","):
                raw = raw.strip()
                m = re.match(r"^(.*?)([A-Za-z_][A-Za-z0-9_]*)$", raw)
                if not m:
                    raise SystemExit(f"cannot parse parameter {raw!r} of {name}")
                param_list.append((m.group(1).strip(), m.group(2)))
        out[name] = (ret.strip(), param_list)
    return out


def to_csharp(c_type: str) -> str:
    """Map one C type to its raw-layer C# projection."""
    t = c_type.replace("const", " ").replace("struct", " ")
    stars = t.count("*")
    base = t.replace("*", " ").split()
    if not base:
        raise SystemExit(f"cannot map empty type from {c_type!r}")
    base_name = base[0]

    if base_name in PRIMITIVES:
        mapped = PRIMITIVES[base_name]
    elif base_name in VALUE_STRUCTS:
        mapped = VALUE_STRUCTS[base_name]
    elif base_name in ENUMS:
        mapped = ENUMS[base_name]
    elif base_name.startswith("PrismPdf"):
        # An opaque handle. Every level of indirection past the first is a pointer to a handle.
        return "nint" + "*" * (stars - 1) if stars >= 1 else "nint"
    else:
        raise SystemExit(f"unmapped C type {c_type!r}")

    return mapped + "*" * stars


# C keywords that are also C# keywords, so a mechanically copied parameter name would not compile.
# Verbatim identifiers keep the name identical to the header's.
CSHARP_KEYWORDS = {
    "checked", "unchecked", "base", "params", "ref", "out", "in", "object", "string", "event",
    "lock", "fixed", "internal", "namespace", "operator", "readonly", "sealed", "using", "is",
    "as", "new", "this", "default", "delegate",
}


def escape(name: str) -> str:
    """Make one parameter name a legal C# identifier without renaming it."""
    return "@" + name if name in CSHARP_KEYWORDS else name


def emit() -> str:
    header_text = HEADER.read_text(encoding="utf-8")
    exports = parse_header(header_text)

    lines: list[str] = []
    add = lines.append

    add("// <auto-generated>")
    add("//   Generated by build/gen_native_methods.py from native/include/prismpdf.h.")
    add("//   Do not edit by hand: vendor a newer header, then regenerate and review the diff.")
    add("// </auto-generated>")
    add("//")
    add("// The raw layer. Binding author's guide, \"Architecture: two layers, always\":")
    add("//   \"one flat, mechanical, 1:1 projection of prismpdf.h. No logic, no renaming beyond the")
    add("//    language's FFI syntax.\"")
    add("//")
    add("// Three projections are worth knowing, because they are the only places C# cannot say what")
    add("// C says:")
    add("//   * C `bool` (one byte) becomes `byte`. Zero is false, non-zero is true.")
    add("//   * Every opaque `PrismPdf… *` handle becomes `nint`, and `PrismPdf… **` becomes `nint*`.")
    add("//   * `char *` and `uint8_t *` become raw `byte*`. Strings are never auto-marshalled: an")
    add("//     owned `char **` out-param must be copied and then released with prismpdf_string_free,")
    add("//     which no built-in marshaller knows how to do.")
    add("//")
    add("// Declarations are [DllImport], not [LibraryImport]: this SDK targets netstandard2.0, and")
    add("// the LibraryImport source generator requires .NET 7+. The projection is otherwise identical")
    add("// \u2014 cdecl is pinned on the attribute rather than by [UnmanagedCallConv], and ExactSpelling")
    add("// stops the runtime probing for A/W suffixed variants that the C ABI does not have.")
    add("//")
    add("// Ownership is NOT enforced here. It is enforced exactly once, in the idiomatic layer.")
    add("")
    add("using System.Runtime.CompilerServices;")
    add("using System.Runtime.InteropServices;")
    add("")
    add("namespace PrismPdf.Interop;")
    add("")
    add("/// <summary>")
    add("/// Direct P/Invoke declarations for the Prism PDF C ABI. Internal by design: the ownership and")
    add("/// error conventions live in the idiomatic layer, and user code must not be able to bypass")
    add("/// them.")
    add("/// </summary>")
    add("internal static unsafe class NativeMethods")
    add("{")
    add("    /// <summary>")
    add("    /// The native artifact name. The loader resolves it to <c>libpdf_ffi.dylib</c> (macOS),")
    add("    /// <c>libpdf_ffi.so</c> (Linux) or <c>pdf_ffi.dll</c> (Windows). Not to be confused with")
    add("    /// <c>prismpdf</c>, which is the CLI binary.")
    add("    /// </summary>")
    add("    internal const string Library = \"pdf_ffi\";")
    add("")
    add("    [ModuleInitializer]")
    add("    internal static void Initialize() => NativeLibraryResolver.Register();")

    missing: list[str] = []
    for heading, names in AREAS:
        add("")
        add("    // " + "-" * 87)
        add(f"    // {heading}")
        add("    // " + "-" * 87)
        for name in names:
            if name not in exports:
                missing.append(name)
                continue
            ret, params = exports[name]
            cs_ret = to_csharp(ret)
            args = ", ".join(f"{to_csharp(t)} {escape(p)}" for t, p in params)
            add("")
            add("    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]")
            add(f"    internal static extern {cs_ret} {name}({args});")

    add("}")

    if missing:
        raise SystemExit(
            "these exports are no longer in the vendored header: " + ", ".join(missing)
        )

    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="fail if the generated file is stale")
    args = parser.parse_args()

    generated = emit()
    if args.check:
        current = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else ""
        if current != generated:
            print(f"{OUTPUT.relative_to(ROOT)} is stale; run python3 build/gen_native_methods.py",
                  file=sys.stderr)
            return 1
        print(f"{OUTPUT.relative_to(ROOT)} is up to date")
        return 0

    OUTPUT.write_text(generated, encoding="utf-8")
    count = generated.count("internal static extern")
    print(f"wrote {OUTPUT.relative_to(ROOT)} ({count} exports)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
