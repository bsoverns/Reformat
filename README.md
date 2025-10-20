# Reformat

A fast desktop utility for transforming large text blobs into the exact shapes I need for daily dev and data work.  
Paste into the top box, choose an operation, click Convert, and copy the result from the bottom box.  
Runs 100% locally on Windows. No cloud calls. No telemetry.

---

## Why this exists

I often need to:
- Take a column of values and turn it into a properly formatted SQL IN list
- Quote/tick wrap fields
- Reshape JSON for quick inspection
- Extract HL7 Segments to help identify missing or bad data

Doing this by hand is slow and error-prone. This tool makes the repetitive parts instant.  
I’ll try to keep this README updated going forward.

---

## How it works (UI flow)

1. Paste or type your input in the top editor.
2. Pick an operation from the **Options** combo box.
3. If the operation needs a password or a mask, the field will appear.
4. Click **Convert**. The output appears in the bottom editor.

---

## Special inputs

- **AES-256 Encrypt/Decrypt**  
  A `Password` field appears. Supply your passphrase (≤ 32 chars).

- **JSON Evaluate**  
  A `Mask` field appears. Enter a JSONPath expression (e.g., `$.orders[*].id`).

---

## Operations

### SQL Helpers

- **SQL Reformat**  
  Normalize commas and spacing from multi-line to flat `a, b, c` style. Also normalizes `" on"` line breaks.

- **Add Commas / Add Commas – Flat / Add Commas – Flat – No Spaces**  
  Convert line-separated values to comma lists, with your preferred spacing and line breaks.

- **Ticks / Ticks – Flat / Ticks – Flat – No Spaces**  
  Wrap each value in single quotes for SQL: `'a','b','c'` (newline or flat).

- **Quotes / Quotes – Flat**  
  Same as above, but with double quotes.

- **QUOTENAME([column_name], '"')**  
  Produces `QUOTENAME("value")` series for safer SQL identifiers.

- **SQL NVARCHAR[255]**  
  Turn a list of column names into `NULL,` lines for quick insert scripting.

---

### JSON and XML

- **JSON Evaluate** 
  Pretty-print minified or ugly JSON.
  Parse once and evaluate a JSONPath mask (e.g., `$.items[*].price`).  
  Results stream into the bottom box.

- **JSON Shrink**  
  Minify JSON by stripping unnecessary whitespace outside of string values.

- **XML ⇒ JSON**  
  Convert XML into JSON text via a straightforward mapping.

---

### HL7 / Mirth

- **837/835**  
  Break segments at `~` into lines.

- **MSH Reformat / Replace Feeds**  
  Prepare or restore HL7 feed breaks between MSH segments.

- **HL7 Breakdown**  
  Expand `Segment|field^component` into  
  `Segment.<fieldIndex>.<componentIndex>: value` lines for inspection.

- **Mirth | separate / Mirth , separate**  
  Join lines with a chosen delimiter for Mirth templates.

- **Mirth delimiter / Mirth quoted delimiter**  
  Compose Mirth string concatenations with delimiter or `quote + delimiter + quote` glue.

---

### Dev Snippets

- **Comma Split**  
  Turn `a,b,c` into:
  a,
  b,
  c

- **GUID**  
Quick line splitting for long GUID blobs.

- **{ get; set; } / ⇒ Properties only**  
Generate C# backing-field or auto-property declarations from variable names.

- **Class list**  
Generate `this._name = name;` assignment lines for constructors.

- **CopySQLtoC# / CopySQLtoJavaScript**  
Escape and join SQL text for embedding inside C# or JavaScript.

- **Json Split Variables**  
Extract property names from JSON into a flat list.

- **Json Class Objects**  
Generate:
```csharp
[JsonProperty("name")]
public string name { get; set; }
