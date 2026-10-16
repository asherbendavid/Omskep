# The `omskep-meta` processing instruction

A small, optional, hand-editable metadata block that can appear at the top of
an Omskep `.ssml` file. It carries information that has nothing to do with
speech synthesis (who wrote the study, what year, a track number, and so on)
so that it can travel with the document and be used when exporting an MP3,
without becoming part of what is actually sent to Azure for synthesis.

This document describes the file format only. It makes no assumptions about
what reads or writes it -- Omskep is one implementation of a reader/writer,
described briefly at the end, but any tool can produce or consume a `.ssml`
file containing this block using nothing more than the rules below.

## Where it lives

An SSML document, like any XML document, may have comments and processing
instructions before its single root element (its "prolog"). The
`omskep-meta` block is a processing instruction placed there, before the
opening `<speak>` tag:

```xml
<?omskep-meta
Title: Die Heelal: Toeval? (English)
Artist: J. Smith
Album:
Track: 4
Year: 2026
Copyright:
?>
<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="af-ZA">
  <voice name="af-ZA-WillemNeural">
    ...
  </voice>
</speak>
```

A processing instruction's content is raw, unparsed text up to the first
`?>` that follows it -- nothing inside it needs XML escaping of any kind
(no `&amp;`, no `&quot;`), and it may freely contain newlines, as shown
above. This is why a processing instruction was chosen over an XML comment:
a comment's content can never contain the two-character sequence `--`,
which a hand-typed value (a date range, a double dash used as a word
separator) could easily contain by accident. A processing instruction has
no such restriction -- its only forbidden content is the literal `?>`
sequence itself, and its target name (`omskep-meta`) may not begin with the
letters `xml` in any case combination, per the XML specification.

Because this is ordinary, well-formed XML prolog content, any XML parser
already skips over it correctly if it has no idea `omskep-meta` exists. A
tool that only cares about the spoken content can ignore it entirely; a
tool that wants the metadata parses the one processing instruction whose
target is `omskep-meta` and ignores everything else in the prolog.

## Format of the block's content

One field per line, in the form:

```
Key: Value
```

- The field name and value are separated by the **first** colon on the
  line. Everything before it (trimmed of surrounding whitespace) is the
  key; everything after it, with a single leading space removed if present,
  is the value. This means a value may itself contain colons -- for
  example `Album: Die Heelal: Toeval?` is a single field named `Album`
  with the value `Die Heelal: Toeval?`.
- Field names are matched **case-insensitively** (`Artist:`, `artist:` and
  `ARTIST:` are the same field).
- A field whose value is empty, or that does not appear in the block at
  all, is **null** -- there is no separate "placeholder" convention. A
  reader must not treat an empty string and "field absent" as different
  outcomes.
- A line with no colon on it at all does not describe a field. A
  conforming reader **silently ignores** such a line, and likewise silently
  ignores any field name it does not recognise (see Field list, below).
  This block is meant to survive being hand-edited by someone who might
  make a typo, add a blank separator line, or write a note to themselves;
  none of that should be treated as an error.
- If the same field name appears more than once in the block, the
  **first** occurrence is authoritative and every later one is ignored --
  including when the first occurrence is itself empty. (This is a
  deliberate choice: someone scanning the block for a field to correct
  will naturally look at, and fix, the first one they see.)
- The block, and the processing instruction containing it, are entirely
  optional. A document with no `omskep-meta` processing instruction at all
  is exactly equivalent to one whose block defines every field as null.

### Parsing pseudocode

```
function parse_omskep_meta(xml_text):
    pi_text = find the processing instruction with target "omskep-meta"
              anywhere in the prolog (before the document's root element)
    if not found:
        return {}                          // every field is null

    fields = {}
    for each line in pi_text.split("\n"):
        line = line.trim()
        if line is empty:
            continue
        colon_pos = line.indexOf(":")
        if colon_pos == -1:
            continue                       // not "key: value" shaped -- ignore
        key = line[0:colon_pos].trim().toLowerCase()
        if key is already in fields:
            continue                       // first occurrence wins
        value = line[colon_pos+1:]
        if value starts with a single space:
            value = value[1:]
        value = value.trimEnd()            // trailing whitespace is not meaningful
        fields[key] = value if value is not empty else null
    return fields
```

A writer constructing this block needs no corresponding escaping logic at
all: it only ever needs to avoid writing a literal newline in the middle of
a single field's value, and avoid writing the literal sequence `?>` -- both
already extremely unlikely in metadata of this kind, and both are exactly
the restrictions the surrounding processing-instruction syntax already
imposes.

## Field list

### Defined fields

These field names have an agreed meaning wherever this spec is used.

| Field | Meaning |
| --- | --- |
| `Title` | A human-readable title for the work. |
| `Artist` | The primary author, speaker, or study leader. |
| `Album` | The collection or series this document belongs to. |
| `Track` | A number giving this document's position within its Album. |
| `Year` | A year associated with the work (for example, when it was written or recorded). |
| `Copyright` | A copyright or rights statement. |
| `Comment` | A free-text note about the document. |
| `Genre` | A genre or category label. |
| `AlbumArtist` | The primary artist for the Album as a whole, when it differs from `Artist` (for example, a various-studies collection). |
| `Composer` | The writer of the original material, when distinct from `Artist`. |
| `Conductor` | A credited conductor or director, where applicable. |
| `Publisher` | The organisation or person that published the work. |
| `Disc` | A disc or volume number, for a work spanning more than one. |
| `Grouping` | A free-text grouping label, for organising documents beyond Album/Track. |
| `Subtitle` | A secondary title or subtitle. |
| `Language` | A free-text statement of the work's language, distinct from the SSML document's own `xml:lang`. |

Any other field name is simply unrecognised: a conforming reader ignores
it, per the parsing rule above, rather than treating it as an error. New
field names may be proposed and added to this list over time; doing so
never changes the meaning of an existing field or the parsing rules
themselves, so older readers continue to work correctly (they just ignore
whatever is new to them) and older files remain valid under a newer
version of this spec.

### A note on this spec's scope

This document defines the *file format* only. What a particular
application chooses to do with each field -- which ones it prefills, which
ones it lets a person edit, how (or whether) it combines a field with
other data the application has to hand -- is specific to that application
and is not part of this spec. Omskep's own use of these fields, described
briefly below, is one example, not a requirement placed on any other
implementation.

## Omskep's own use of this block (informative, not part of the format)

This section describes how the Omskep application specifically behaves;
it is included for context, not as a requirement for other implementations.

- Omskep reads this block to prefill six fields when writing an exported
  MP3's ID3 tags: `Title`, `Artist`, `Album`, `Track`, `Year`, and
  `Copyright`. New documents, and documents imported from a study PDF,
  begin with this block present but every one of those six fields
  written empty; Omskep never fills in a placeholder string for any of
  them.
- `Comment` and `Genre` are treated differently: Omskep itself computes a
  value for both (a generated comment describing the voices used, speaking
  rate, and synthesis disclosure; and the fixed genre `Audiobook`), and by
  default these computed values are what gets written to the exported
  file's ID3 tags. Omskep's New/Import templates never write a `Comment:`
  or `Genre:` line. If a person deliberately hand-types one of these two
  fields into the document, its presence (a non-null value) offers a
  choice, at export time, between Omskep's computed value and the typed
  one -- the typed value never silently overrides the computed one.
- Every other field in the list above is reserved for a future version of
  Omskep to read, and is safe to write by hand today: Omskep's parser
  already recognises these field names (so a typo in one of them would not
  be silently treated as a typo in some other field by accident), even
  though nothing yet reads their value.
- Omskep strips the entire `omskep-meta` processing instruction, along
  with the rest of the document's prolog, before composing the document
  into the SSML actually sent to Azure for synthesis. The block never
  reaches Azure and is never billed as synthesized characters.
