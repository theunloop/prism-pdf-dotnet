# Ownership and lifetimes

The rules are the ABI's (`docs/site/ffi-ownership.md` upstream); this page is how they surface in
a garbage-collected language, and what you have to do about them.

## Owned handles: dispose them

Anything deriving from `PrismPdfHandle` owns native memory:

`Document`, `OpenOptions`, `OpenReport`, `TransformReport`, `SignSettings`, and every
`…List`.

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

Some ABI calls take ownership of a handle **on success only**; a validation failure leaves it with
the caller. `PrismPdfHandle.MarkConsumed()` is how a wrapper records that, and it is called only
when the native call returned `Ok`.

No entry point in the currently shipped surface consumes a handle — the consuming calls all live in
the authoring, layout, and composition areas (`edit_commit`, `builder` page and struct commits,
`flow_build`, `flow_into_builder`, `composition_build`). The machinery is in place and tested so
those areas can be added without revisiting the lifetime model.

When they arrive, the contract for you is: after a successful consuming call the wrapper is dead,
and touching it raises `ObjectDisposedException`. Do not dispose it — the library owns those bytes
now. `using` on a consumed handle is still correct: `Dispose` on an already-consumed wrapper is a
no-op.

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
