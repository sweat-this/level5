# Level 5 Scope Guardian

Use this workflow before large features, architectural expansions, modernization proposals, or adjacent work discovered while implementing another issue.

## Goal

Protect Level 5 from two opposite failures:

1. preserving harmful legacy architecture forever because refactoring feels large;
2. expanding every local change into a speculative system rewrite.

The preferred strategy is evidence-based, vertical, incremental modernization.

## Classification

Classify proposed work as one of:

### REQUIRED

Needed to:

- satisfy the current acceptance criteria;
- fix a demonstrated regression or correctness problem;
- preserve data/scoring integrity;
- remove a blocker that prevents the requested implementation;
- keep the touched runtime flow safe and coherent.

### HIGH-VALUE

Not strictly required, but a small adjacent change that materially improves:

- player-facing quality;
- reliability;
- testability of the touched contract;
- removal of a dangerous compatibility hazard.

High-value work must still be tightly bounded.

### LATER

Potentially valuable, but not justified by the current task. Examples include:

- generalizing a successful local extraction to every mode immediately;
- future networking infrastructure before the existing versus boundary needs it;
- broad manager replacement triggered by one feature;
- speculative event buses/frameworks;
- converting all duplicated AI behavior while fixing one defender bug;
- large folder/assembly reorganizations without a current dependency need.

For `LATER`, preserve only the seam the current solution genuinely needs.

### REJECT

Work that is unrelated, duplicative, contradicts current approved direction, or adds more complexity than value.

## Evaluation Questions

For each proposal ask:

1. What current player or engineering problem does this solve?
2. What repository evidence proves that problem exists now?
3. Is it part of the current issue/critical path?
4. What is the smallest complete solution?
5. Does an existing Level 5 contract already solve part of it?
6. What behavior must remain compatible?
7. What modes/players/persistence boundaries could regress?
8. Can the future extension be supported later without implementing it now?
9. What test demonstrates value or correctness?

## Level 5 Modernization Rule

Architecture docs describe a gradual extraction strategy: stable contracts first, then one vertical gameplay slice at a time.

Therefore:

- prefer converting one real flow completely over creating half-used infrastructure;
- prefer compatibility adapters over flag-day rewrites;
- prefer tests around the current contract before extraction;
- remove legacy paths only after real callers have migrated;
- do not preserve duplicated state merely to avoid migration when ownership is the actual bug;
- do not abstract two things just because their names look similar—compare behavior first.

## Output

Return:

- classification (`REQUIRED`, `HIGH-VALUE`, `LATER`, `REJECT`);
- evidence;
- minimum justified implementation;
- compatibility constraints;
- validation required;
- explicit deferred follow-ups.
