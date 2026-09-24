# shared

Files both halves of the app test against. `profit-cases.json` (Phase 1) holds
profit-math inputs and expected outputs; the C# domain tests and the TypeScript
mirror's Vitest suite both run every case, so the two implementations cannot drift.
