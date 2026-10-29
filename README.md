# Reformat

A fast desktop utility for transforming large text blobs into the exact shapes I need for daily dev and data work.
Paste into the top box, choose an operation, click **Convert** (or press **Ctrl+Enter**), and copy the result from the bottom box.
Runs 100% locally on Windows. No cloud calls. No telemetry.

[![Build](https://github.com/bsoverns/Reformat/actions/workflows/build-wpf.yml/badge.svg)](https://github.com/bsoverns/Reformat/actions/workflows/build-wpf.yml)
[![Latest release](https://img.shields.io/github/v/release/bsoverns/Reformat?label=download)](https://github.com/bsoverns/Reformat/releases/latest)

## Download

**[⬇ Download Reformat.exe (latest)](https://github.com/bsoverns/Reformat/releases/latest/download/Reformat.exe)**

A single file for 64-bit Windows. There's nothing to install and you don't need .NET. Every change merged to `main` builds a new release automatically. See [all releases and their notes](https://github.com/bsoverns/Reformat/releases).

> The exe isn't code-signed, so Windows SmartScreen may warn the first time. Click **More info → Run anyway**,
> or right-click the file → **Properties** → tick **Unblock**.
> The version you're running is shown in the window title.

---

## Why this exists

I often need to:
- Take a column of values and turn it into a properly formatted SQL `IN` list
- Quote/tick wrap fields
- Reshape JSON for quick inspection
- Extract HL7 segments to help identify missing or bad data

Doing this by hand is slow and error-prone. This tool makes the repetitive parts instant.

---

## How it works

1. Paste or type your input in the top editor.
2. Pick an operation from the **Options** dropdown (grouped by category; the last one you used is remembered).
3. If the operation needs a password or a JSONPath mask, that field appears next to the dropdown.
4. Click **Convert** or press **Ctrl+Enter**. The output appears in the bottom editor.

The bar along the bottom shows line/character counts for both boxes and has three buttons:

| Button | What it does |
|---|---|
| **Copy Output** | Copies the bottom box to the clipboard. |
| **Output → Input** | Moves the result into the top box so you can chain operations (e.g. *Distinct Lines* → *Sort Lines* → *Ticks - Flat*). |
| **Clear** | Empties both boxes. |

Large inputs are processed on a background thread so the window stays responsive.

---

## Operations

### SQL

| Operation | Input → Output |
|---|---|
| **SQL Reformat** | Multi-line column list → `a, b, c`. Also pulls `ON` up onto the previous line. |
| **Add Commas** / **- Flat** / **- Flat - No Spaces** | `a⏎b` → `a,⏎b` / `a, b` / `a,b` |
| **Ticks** / **- Flat** / **- Flat - No Spaces** | `a⏎b` → `'a',⏎'b'` / `'a', 'b'` / `'a','b'`. Apostrophes are escaped (`O'Brien` → `'O''Brien'`). |
| **SQL IN (...) - Distinct** | Trims each line, drops blanks and duplicates → `IN ('a', 'b')` |
| **Quotes** / **- Flat** | Same as Ticks with double quotes. |
| **QUOTENAME([column_name], '"')** | `Id⏎Name` → `QUOTENAME([Id], '"'),⏎QUOTENAME([Name], '"')` |
| **SQL NVARCHAR[255]** | Column names → `[col] [nvarchar](255) NULL,` lines for a `CREATE TABLE`. |

### Line tools

| Operation | What it does |
|---|---|
| **Trim Lines / Remove Blanks** | Trims whitespace from every line and removes empty lines. Handy on data copied out of SSMS or Excel. |
| **Distinct Lines** | Trim + remove blanks + remove duplicates (keeps first occurrence order). |
| **Sort Lines** | Trim + remove blanks + case-insensitive sort. |
| **UPPERCASE** / **lowercase** | Changes case. |
| **Proper Case** | Capitalizes each word and lowercases the rest: `brad o'doyle` → `Brad O'Doyle`, `mary-kate` → `Mary-Kate`. Contractions stay lowercase (`don't` → `Don't`). |
| **Comma Split** | `a,b,c` → one value per line (comma kept). |

### JSON / XML

| Operation | What it does |
|---|---|
| **JSON Beautify/Evaluate** | Pretty-prints JSON. Type a JSONPath in the **mask** box (e.g. `$.orders[*].id`) to list only the matching values. Updates live as you type. |
| **JSON Shrink** | Minifies JSON, keeping whitespace inside string values. |
| **XML => JSON** | Converts an XML document to indented JSON. |
| **Json Split Variables** | Extracts property names from `"key": value` lines. |
| **Json Class Objects** | Names → `[JsonProperty("name")] public string name { get; set; }` |

### HL7 / EDI / Mirth

| Operation | What it does |
|---|---|
| **837/835** | Breaks X12 segments at `~` onto separate lines. |
| **HL7 Breakdown** | Expands each segment into `SEG.field.component: value` lines. `MSH` is numbered per the HL7 spec (`MSH.2` = encoding characters). |
| **MSH Reformat** / **Replace Feeds** | Marks `MSH` boundaries with `~~~MSH`, then later flattens and restores one message per line. |
| **Mirth \| separate** / **Mirth , separate** | Joins lines with `\|` or `,`. |
| **Mirth delimiter** / **Mirth quoted delimiter** | Builds `results.getString('…') + delimiter + …` chains for Mirth templates. |
| **GUID** | Replaces `~` with line breaks. |

### Code snippets

| Operation | What it does |
|---|---|
| **{ get; set; }** | Names → properties with `_name` backing fields. |
| **{ get; set; } => Properties only** | Names → `public string name { get; set; }` |
| **Class list** | Names → `this._name = name;` constructor assignments. |
| **CopySQLtoC#** / **CopySQLtoJavaScript** | Turns multi-line SQL into a concatenated string literal. |

### Encoding & encryption

| Operation | What it does |
|---|---|
| **Base64 Encode / Decode (UTF-16)** | Base64 of the UTF-16 bytes. This is what older versions called "Base64 Encrypt/Decrypt". |
| **Base64 Encode / Decode (UTF-8)** | Base64 of the UTF-8 bytes. Matches most other tools, websites and `base64` on the command line. |
| **Base64 Image Encode** | Paste an image (Ctrl+V), drop an image file on the input box, or type an image's path. Pasting an image switches to this operation and converts right away. Output is raw Base64. |
| **Base64 Image Encode (data URI)** | The same, but the output is `data:image/png;base64,...`, ready for HTML `<img src>` or CSS. |
| **Base64 Image Decode** | Paste Base64 (raw or a `data:` URI) and Convert. The image shows in the output box. **Copy Output** copies the image, **Save Image...** saves it, and **Output → Input** re-encodes it. Supports PNG, JPEG, GIF, BMP, ICO, TIFF and WEBP. |
| **Base64 Image → data URI** / **data URI → Base64 Image** | Adds or removes the `data:image/...;base64,` prefix on Base64 image text. The image type is detected from the data, so the prefix is always correct. |
| **URL Encode / Decode** | Percent-encoding for query strings. |
| **SHA-256 Hash** | Lowercase hex SHA-256 of the UTF-8 text. |
| **AES-256 Encrypt** | Legacy format, compatible with the WinForms version. Password up to 32 characters. |
| **AES-256 Encrypt (Strong)** | AES-256-GCM with a PBKDF2-SHA256 key (600k iterations), random salt and nonce. Output starts with `RF2:`. Any password length. |
| **AES-256 Decrypt** | Decrypts both formats. It detects the `RF2:` prefix automatically. |

> **About the legacy AES format:** it uses a fixed IV and pads short passwords with a key that is in this public
> repository. The same text and password always give the same output, and short passwords are weak.
> It's fine for casual obfuscation. For anything sensitive, use **AES-256 Encrypt (Strong)**.

---

## Building

Requires the .NET 8 SDK (or newer) on Windows.

```bash
dotnet build WpfReformat/WpfReformat.sln
dotnet test WpfReformat/WpfReformat.sln
```

Or open `WpfReformat/WpfReformat.sln` in Visual Studio 2022. The executable is `Reformat.exe`.

### CI and releases

`.github/workflows/build-wpf.yml` builds the app and runs the tests on every pull request. When a change is merged to `main`, it also:

1. Stamps the version as `YYYY.M.D.<run number>`, for example `2026.10.16.12`.
2. Publishes a single-file `Reformat.exe`.
3. Creates a GitHub Release tagged `v<version>` with the exe attached. The release notes are generated from the pull requests merged since the previous release.

PR titles become the release notes, so give PRs clear titles.

To get a single self-contained exe:

```bash
dotnet publish WpfReformat/WpfReformat/WpfReformat.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

---

## Project layout

```
WpfReformat/                    Current app (.NET 8, WPF)
  WpfReformat/
    MainWindow.xaml(.cs)        UI only: wiring, status bar, live JSON evaluation
    Transforms/
      TextTransforms.cs         Every text transformation as a pure function
      AesText.cs                Legacy + strong AES
      OperationCatalog.cs       The dropdown: name, category, function, extra input
      Operation.cs              Operation / context types
  WpfReformat.Tests/            xUnit tests for the transforms
WinFormReformat/                Original WinForms app (.NET Framework 4.8). Legacy, no longer maintained.
```

### Adding a new operation

1. Write a pure `string -> string` function in `Transforms/TextTransforms.cs`.
2. Add one line to `OperationCatalog.All` with its name and category.
3. Add a test in `WpfReformat.Tests/TransformTests.cs`.

The UI picks it up automatically.

## License

MIT. See [LICENSE](LICENSE).
