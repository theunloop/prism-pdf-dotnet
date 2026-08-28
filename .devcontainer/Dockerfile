# .NET 9 SDK on Debian bookworm.
#
# 9, not 8, for one specific reason: the solution is PrismPdf.slnx, and the XML solution format is
# only understood by SDK 9.0.200 and newer. A plain .NET 8 SDK cannot build this repository at all
# — `dotnet build` reports MSB1003, "no project or solution file". global.json asks for 8.0.0 with
# rollForward: latestMajor, so the 9.x SDK here satisfies it.
ARG VARIANT=1-9.0-bookworm
FROM mcr.microsoft.com/devcontainers/dotnet:${VARIANT}

# The base image still carries the legacy yarn apt source, whose signing key is no longer
# published — it makes `apt-get update` fail outright. Nothing here needs yarn, so drop it.
RUN rm -f /etc/apt/sources.list.d/yarn.list \
    && apt-get update \
    && export DEBIAN_FRONTEND=noninteractive \
    && apt-get install -y --no-install-recommends \
        # cargo shells out to cc to link the pdf_ffi cdylib; without this the Rust build fails
        # at the link step rather than at compile time, which is a confusing way to find out.
        build-essential \
        pkg-config \
        # build/gen_native_methods.py is plain python3, no packages.
        python3 \
        # Day-to-day tooling inside the container.
        ripgrep \
        fd-find \
        jq \
        tree \
        less \
        htop \
        unzip \
        vim \
    # Debian ships fd as fdfind to avoid a name clash; restore the usual name.
    && ln -sf "$(command -v fdfind)" /usr/local/bin/fd \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# tests/PrismPdf.Tests targets net8.0, so running it needs the 8.0 runtime alongside the 9.x SDK
# that builds it. (The SDK library itself targets netstandard2.0 and needs no runtime of its own.)
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 8.0 --runtime dotnet --install-dir /usr/share/dotnet \
    && rm /tmp/dotnet-install.sh

# Rust, for building the pdf_ffi cdylib out of a prism-pdf checkout.
#
# Installed directly rather than through the devcontainer Rust feature: that feature also pulls
# lldb and llvm-14, about 735 MB of debugger tooling for a toolchain this repository only ever
# invokes as `cargo build -p pdf-ffi`. The minimal profile is enough; rust-src is here so the
# rust-analyzer extension can resolve into the standard library.
ENV RUSTUP_HOME=/usr/local/rustup \
    CARGO_HOME=/usr/local/cargo \
    PATH=/usr/local/cargo/bin:$PATH
RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs \
        | sh -s -- -y --no-modify-path --profile minimal --default-toolchain stable --component rust-src \
    # The container runs as `vscode`, so cargo's registry cache and rustup's toolchains must be
    # writable by it, not only by root.
    && chmod -R a+rwX "${RUSTUP_HOME}" "${CARGO_HOME}" \
    && rustc --version && cargo --version
