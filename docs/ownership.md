# Ownership and lifetimes

The rules are the ABI's (`docs/site/ffi-ownership.md` upstream); this page is how they surface in
a garbage-collected language, and what you have to do about them.

## Owned handles: dispose them

Anything deriving from `PrismPdfHandle` owns native memory:

`Document`, `OpenOptions`, `OpenReport`, `TransformReport`, `SignSettings`, `Content`, `Builder`,
`PageSpec`, `StructNode`, `ImageSource`, `Flow`, `Table`, `TextBlock`, `Composition`,
`CompositionContainer`, `XmpMetadata`, `PdfObject`, `Edit`, and every `…List`.

```csharp
using var doc = Document.OpenFile("input.pdf");
using var fonts = doc.Fonts();
```

A finalizer will free a forgotten handle eventually, but it is a safety net, not the API: until the
GC runs, a PDF's worth of native memory stays resident, invisible to .NET's heap accounting and to
anything watching managed memory pressure. Use `using`.

Disposing twice is safe. Using a disposed handle raises `ObjectDisposedException` — the wrapper
raises it, so the native library never sees a dangling pointer.

## Borrowed items: they die with their list

Items lent out of a list — `Annotation`, `FormField`, `OutlineItem`, `Attachment`, `Font`, `Image`,
`Signature` — are **not** owned and are never disposed. They are valid exactly as long as their
list is.

The SDK makes this safe two ways: each item holds a strong reference to its list, so the collector
cannot free the list underneath a reachable item; and each item checks that reference on every
access, so this raises instead of corrupting memory:

```csharp
Attachment escaped;
using (var attachments = doc.Attachments())
{
    escaped = attachments[0];
}

_ = escaped.Name;   // ObjectDisposedException — correct, and not a crash
```

**Copy what you need before the list goes out of scope.** Byte payloads (`Attachment.Data`,
`Font.Program`, `Image.Data`) are already copies — the ABI lends a borrowed view and the SDK copies
it — so those arrays outlive the list. Item wrappers do not.

```csharp
byte[] payload;
using (var attachments = doc.Attachments())
{
    payload = attachments[0].Data;   // a copy; safe to keep
}
```

## Nested trees are owned by their root

The outline tree needs no per-level handle. `OutlineList` owns the whole tree, and children are
lent straight out of their parents:

```csharp
using var outline = doc.Outline();
foreach (var top in outline)
{
    foreach (var child in top.Children())   // borrowed from the same allocation
    {
        Console.WriteLine($"  {child.Title}");
    }
}
```

Disposing the root invalidates every entry in the tree, nested ones included.

## Consuming calls

Some ABI calls take ownership of a handle. The contract for you is the same in every case: after
the call the wrapper is dead, touching it raises `ObjectDisposedException`, and you must not free
it — the library owns those bytes now. `using` on a consumed handle stays correct, because
`Dispose` on an already-consumed wrapper is a no-op.

```csharp
using var content = new Content();
var page = new PageSpec(content);

using var builder = new Builder();
builder.AddPageSpec(page);      // consumes `page`

page.IsInvalid;                 // true
page.AddStandardFont("F1", StdFont.Helvetica);   // ObjectDisposedException
page.Dispose();                 // no-op, not a double free
```

**When** ownership transfers is not uniform across the ABI, and getting it wrong is a double free.
There are three shapes:

| Shape | Calls | What the wrapper does |
|---|---|---|
| Consumes **on success only** | `builder.AddPageSpec`, `builder.AddStructureNode`, `node.AddChild`, `edit.Commit` | `MarkConsumed()` after the status came back `Ok`; a rejection leaves the handle owned and usable |
| Consumes **unconditionally** | `flow.Build`, `flow.IntoBuilder` | `MarkConsumed()` *before* the call — the ABI takes the box as it is entered, so a failure must not leave a wrapper that would free it again |
| **Finalises without consuming** | `composition.Build` | nothing; the composition becomes immutable but the handle is still yours to dispose |

Semantic contract 3 describes only the first shape. Each export's header comment says which one it
is; read it before binding a new consuming call.

## Composition containers are scoped handles, not nodes

A `CompositionContainer` addresses one slot in the composition's arena. Three consequences:

- **Disposing a container releases the handle, not the node.** The node stays in the composition,
  which is why a tree written with nested `using` declarations still builds completely.
- **Filling a slot spends its handle.** The engine advances the slot's generation, so the next use
  of that handle raises `PrismPdfException` with `InvalidUse` — from the library, not from managed
  code, because the engine is the only thing that knows which calls consume a generation.
  Appending to a column, a row or a table does not spend anything.
- **Releasing the composition leaves surviving containers safe.** They are invalidated without
  being left pointing at freed memory, and disposing them afterwards is fine. Each container also
  holds a reference to its composition, so the collector cannot finalise the arena underneath a
  live handle.

## Strings and buffers

You never free library memory. Owned strings are copied into .NET strings and released with
`prismpdf_string_free` in a `finally`; owned byte buffers are copied and released with
`prismpdf_bytes_free`, passing back the original length as the ABI requires. Both happen inside
`Native.TakeString` / `Native.TakeBytes`, once.

The single exception is `Pdf.Version`, which reads a static string the library owns forever and
must not be freed.

## Threading

**The ABI makes no thread-safety promise for a handle.** Treat every handle as externally
synchronized: confine it to one thread, or guard it with your own lock.

The visible consequence is the diagnostic slot, which is thread-local. If a native call fails on
thread A and you read the error on thread B, you get the wrong error — or none. The SDK never
crosses a thread between a call and its error read, and neither should any extension to it.

Two documents opened independently are independent; nothing in this SDK holds global mutable state
beyond the loader registration, which is idempotent.

```csharp
// Fine: one document per task, no sharing.
await Parallel.ForEachAsync(paths, async (path, ct) =>
{
    using var doc = Document.OpenFile(path);
    await File.WriteAllTextAsync(path + ".txt", doc.Text, ct);
});

// Not fine: one document shared across tasks.
```

Note the `await` above happens *after* every native call on `doc` has completed. Never hold a
native call and a scheduler hop in the same expression.
