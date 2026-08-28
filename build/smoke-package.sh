#!/usr/bin/env bash
#
# Pack the SDK, install the resulting .nupkg into a throwaway project from a local feed, and run a
# real document through it.
#
# Everything else in the suite reaches the library through a ProjectReference, which never
# exercises the part most likely to be wrong: whether NuGet unpacks runtimes/<rid>/native/ for the
# consumer's platform and whether the loader then finds it. That path has no unit test — it has
# this.
#
#   build/smoke-package.sh
#   build/smoke-package.sh --rid linux-arm64     # also check a RID-specific publish
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid) RID="$2"; shift 2 ;;
    -h|--help)
      sed -n '2,13p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

SAMPLE="${REPO_ROOT}/native/corpus/valid/two-pages-text.pdf"
if [[ ! -f "${SAMPLE}" ]]; then
  echo "error: no corpus at native/corpus. Run: build/fetch-natives.sh --corpus" >&2
  exit 1
fi

WORK="$(mktemp -d)"
trap 'rm -rf "${WORK}"' EXIT

echo "==> packing"
dotnet pack "${REPO_ROOT}/src/PrismPdf/PrismPdf.csproj" -c Release -o "${WORK}/feed" --nologo -v q

VERSION="$(basename "$(ls "${WORK}"/feed/*.nupkg)" .nupkg)"
VERSION="${VERSION#PrismPdf.}"
echo "==> packed PrismPdf ${VERSION} ($(du -h "${WORK}"/feed/PrismPdf.*.nupkg | cut -f1))"

# The consumer. A clear/<clear> on the feed list is deliberate: it proves the package is resolved
# from the local build and not from a cached or published copy of the same version.
mkdir -p "${WORK}/app"
cat > "${WORK}/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="${WORK}/feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

( cd "${WORK}/app" && dotnet new console >/dev/null )
cat > "${WORK}/app/Program.cs" <<'EOF'
using PrismPdf;

var path = Environment.GetEnvironmentVariable("SMOKE_PDF")!;
using var doc = Document.Open(File.ReadAllBytes(path));
using var reopened = Document.Open(doc.Save());

Console.WriteLine($"engine  {Pdf.Version}");
Console.WriteLine($"pages   {doc.PageCount}");
Console.WriteLine($"text    {doc.PageText(0)?.Trim()}");
Console.WriteLine($"reopen  {reopened.PageCount} pages");

if (Pdf.Version.Length == 0 || doc.PageCount == 0 || reopened.PageCount != doc.PageCount)
{
    return 1;
}
return 0;
EOF

echo "==> installing PrismPdf ${VERSION} from the local feed"
( cd "${WORK}/app" && dotnet add package PrismPdf --version "${VERSION}" >/dev/null )

# Unset the override so this can only succeed through the packaged runtimes/ layout.
echo "==> running"
( cd "${WORK}/app" \
  && env -u PRISMPDF_NATIVE_PATH SMOKE_PDF="${SAMPLE}" dotnet run --no-launch-profile )

if [[ -n "${RID}" ]]; then
  echo
  echo "==> publishing for ${RID}"
  ( cd "${WORK}/app" \
    && dotnet publish -r "${RID}" --self-contained false -o "${WORK}/pub" --nologo -v q )
  found=$(find "${WORK}/pub" -name '*pdf_ffi*' | wc -l)
  if [[ "${found}" -ne 1 ]]; then
    echo "error: a RID-specific publish carried ${found} native libraries, expected exactly 1" >&2
    find "${WORK}/pub" -name '*pdf_ffi*' >&2
    exit 1
  fi
  echo "    one native library, $(du -sh "${WORK}/pub" | cut -f1) total"
fi

echo
echo "==> the packaged shape works"
