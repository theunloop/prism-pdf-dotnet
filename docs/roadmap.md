# Roadmap

## Where the binding stands

Against the vendored header for core `v0.4.0`:

| | Exports |
|---|---:|
| In `native/include/prismpdf.h` | **386** |
| Bound by this SDK | **159** (41%) |
| Not bound yet | **227** |

`NativeSurfaceTests.CoverageGap_IsReported` prints this breakdown on every run, so it cannot go
stale the way this page can. If the two disagree, the test is right.

## What is bound

The **read, manipulate and security** vertical, end to end:

- open (plain, password, options, private key, deprecated limits), lifecycle, open reports
- read: page count, versions, text, positioned text, `/Info`, XMP, dates, structure namespaces,
  VRI keys
- collections: annotations, form fields, outline, attachments, fonts, images, string lists
- manipulate: save (classic / compact / packed / version-targeted), extract, rotate, merge, subset
  fonts, fill and flatten forms — each with its `…WithReport` companion
- transform reports: rewrite mode, signature effect, structure effect
- permissions, encryption (RC4, AES-128, AES-256, AES-256-GCM, public-key, PDF MAC)
- signing (settings, appearance, PAdES, timestamps) and verification (integrity, trust, LTV)

## What is not, in the order it should land

### 1. Authoring: content streams — 40 exports

`prismpdf_content_*`: the 40 page operators, plus the handle that owns them. Everything above
depends on this, and it is the least interesting to write — a flat wall of mechanical setters —
which is exactly why it goes first rather than being deferred forever.

Shape: a `Content` handle with one method per operator, and a borrowed `Bytes` view passed straight
to the builder. No new lifetime questions.

### 2. Authoring: the document builder — 58 exports

`prismpdf_builder_*`, `prismpdf_page_spec_*`, `prismpdf_struct_node_*`.

**This is where consuming calls arrive.** `builder_add_page_spec` and `builder_add_structure_node`
take ownership of their argument on success and leave it with the caller on a validation failure.
`PrismPdfHandle.MarkConsumed()` exists and is tested for exactly this; the work is wiring it, not
designing it.

Also the first place the ABI's *flattened enums* appear: `AnnotationSpec`, `FormFieldSpec` and
`LinkTarget` are Rust enums carrying payloads, which C cannot represent, so the ABI exposes one
entry point per variant (`builder_add_link_uri`, `_add_link_page`, `_add_link_element`,
`_add_link_document_part`, `_add_note`, `_add_checkbox`). C# *can* represent them — but projecting
them back into a discriminated shape would diverge from every other binding. Keep the flattened
methods.

Unblocks the **create** conformance journey (`create.md`).

### 3. Layout: `Flow` — 48 exports

`prismpdf_flow_*`, `prismpdf_table_*`, `prismpdf_text_block_*`, `prismpdf_image_source_*`, plus the
handle-less `prismpdf_measure_text` and `prismpdf_wrap_text` (which go on `Pdf`, per naming rule 6).

The guide calls this "the most binding-friendly API in the engine": pour content, let it break
pages. Two things need care:

- `flow_build` and `flow_into_builder` **consume** the flow. After either call the handle is dead,
  exactly like `fclose`. Do not free it again — `MarkConsumed()`, not `Dispose()`.
- `flow_into_builder` is the composition point: it hands back a `Builder`, so item 2 must land
  first for this to be useful.

### 4. Declarative composition — 35 exports

`prismpdf_composition_*`. The arena model: `Composition` owns everything, and each
`CompositionContainer` carries an arena identity, a slot id, and a generation. Filling a slot
consumes that container generation; releasing the composition invalidates surviving container
handles without leaving them pointing at freed memory; build is one-way finalisation on both
success and failure.

This is the richest lifetime model in the ABI and deserves its own design pass before any code —
in particular, whether generations are tracked in managed code (so stale use raises
`ObjectDisposedException`) or left to the native `InvalidUse` status.

Unblocks the **compose** journey and, with it, **the anchor acceptance test**: a port of
`crates/pdf-ffi/tests/c/compose_invoice.c`. Every binding builds the same tagged multipage invoice
and asserts on it by reopening. That test is the real measure of this SDK being finished.

### 5. Conformance production: PDF/A and PDF/UA — 13 exports

`prismpdf_builder_make_pdfa*`, `_make_pdfua*`, `prismpdf_pdfa_*`, `prismpdf_xmp_metadata_*`.

Small, but it introduces the one status this SDK has no path for yet: `Conformance`, whose
`out_issue` parameter reports *which* rule failed. That wants a dedicated
`PrismPdfConformanceException` carrying a `ConformanceIssue`, so a caller learns "the figure has no
alt text" rather than "conformance failed".

Depends on item 2.

### 6. COS inspection and editing — 33 exports

`prismpdf_document_catalog_object` / `_page_object` / `_get_object` / `_resolve_object`,
`prismpdf_object_*`, `prismpdf_edit_*`.

The expert escape hatch: read any COS value, construct new ones, and commit changes either
incrementally (retaining original signed ranges) or as a full rewrite. `edit_commit` consumes the
edit on success and leaves it caller-owned on failure — the same pattern as item 2.

Lowest priority because nothing else depends on it, and because a caller who needs it can usually
reach the same result through the modelled API.

## Not in the ABI at all

Some gaps are upstream, not here. `docs/ABI.md`'s "Not yet crossing" section lists them: namespace
role maps, the rest of `Builder` (form XObjects, `embed_cid_font`, colour-space constructors, page
labels, document parts, developer extensions), bulk COS enumeration, font helpers (`subset_sfnt`,
`glyphs_for_text`, `shape_text`), name trees, and writing DSS validation material. No binding can
expose those until the C ABI does.

Page **rendering** (§10–§11) is deliberately out of scope for the engine's v1 line and will not
appear here.

## Packaging

Orthogonal to the surface work, and worth doing before the surface is complete:

- **NuGet package with native binaries.** Drop `libpdf_ffi` into `runtimes/<rid>/native/` for each
  supported RID. The loader already falls through to .NET's default probing, so this needs no code
  change — only a CI job that builds the core on each OS and a `.nuspec`/`csproj` that packs the
  results.
- **CI matrix** matching the OS matrix the core supports, running `dotnet test` plus
  `python3 build/gen_native_methods.py --check`.
- **Source link and symbols**, so a stack trace through the interop layer is navigable.

## Feeding back upstream

The guide's final checklist item asks bindings to report what it "got wrong or left ambiguous".
Two notes from writing this one:

1. **`prismpdf_version()` used to report the Cargo workspace version, not the release tag.** At
   `v0.3.1` it returned `"0.3.0"`, so a binding that pinned to a tag and asserted on it was wrong.
   Raised during the native-artifacts handoff and **fixed upstream in `v0.4.0`**: the release
   workflow now fails a tag whose workspace version disagrees, and `docs/native-artifacts.md`
   states the equality as a guarantee bindings may rely on.
2. **Rule 6 is not universally applicable.** It says handle-less functions go on "the top-level
   module/class", which the guide illustrates as `PrismPdf.version()`. C# cannot give a type the
   same name as its enclosing namespace. The rule should say "a top-level class named for the
   library, subject to the language's naming constraints".
