# shared

Files both halves of the app read, so there is one copy of each.

| File | Read by |
|---|---|
| `platforms.json` | The platform registry and default fee schedules. Embedded in the C# domain assembly and imported by the client as `@shared/platforms.json`. Adding a platform is one entry here plus a `--<id>-color` token in both themes. |
| `demo-items.json` | The sample inventory from the original app's demo, with dates as days before today. Every "Try the demo" visitor gets a private copy, deleted after 24 hours. Embedded in the API. |
| `item-fields.json` | Item conditions, the default condition and text length limits. The API validates against it and sizes its columns from it; the client's New item form and bulk importer read the same file. |
| `profit-cases.json` | Profit-math inputs and expected outputs. The C# domain tests and the client's Vitest suite both run every case, so the two implementations cannot drift. |

The fixture's expected values were generated once from an exact integer-cents
oracle and cross-checked against the original app's `money.js`. Three cases
differ from the original on purpose: halfway fee estimates the original's
floating point rounded a cent low. Edit it by hand from here on; any change has
to pass on both sides.
