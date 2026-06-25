# F# And Architecture

## Why F# fits this tool

This codebase benefits from F# because the problem is mostly about transforming clinical intent into safe, explicit program flow.

The application does not need a heavy object model. It needs:

- precise domain types
- predictable branching
- explicit failure handling
- straightforward composition between steps

F# gives that with discriminated unions, records, modules, and `Result<_, string>`.

## The beauty in the current shape

The most useful part of the design is that the metric configuration stores intent rather than implementation detail.

Instead of hardcoding final Mayo strings in configuration, [Config.fs](../Config.fs) describes two clinical ideas:

- `RelativeVolumeAtFullCourseDose`
- `DoseAtVolumeNeedsFractionNormalization`

That matters because the final query string depends on prescription context. The config stays stable while the extraction layer computes the exact backend query later.

This is a good F# move:

- define the domain in the type system
- keep derived technical details out of config
- convert intent into execution close to the boundary

## How the code works

The execution path is intentionally narrow.

### 1. Configuration

[Config.fs](../Config.fs) contains:

- patient ids
- metric definitions

Each metric carries an id, a typed metric kind, and an output unit.

### 2. Core domain records

[Domain.fs](../Domain.fs) defines the records shared across modules:

- `MetricDefinition`
- `PrescriptionContext`
- `MetricRow`

These records are the stable contracts between configuration, extraction, and CSV writing.

### 3. ESAPI access and structure matching

[EsapiQuery.fs](../EsapiQuery.fs) is the boundary to Eclipse access concerns.

It handles:

- opening patients
- filtering courses and plans
- matching structures by exact id first
- falling back to normalized structure id matching
- rejecting ambiguous normalized matches

That separation keeps ESAPI-specific concerns away from the prescription logic.

### 4. Prescription inference

[Prescription.fs](../Prescription.fs) translates the matched `PTV1` structure id into original treatment context.

Today the inference rules are intentionally simple:

- `PTV1_66` means `66 Gy` over `33` fractions
- `PTV1_68` means `68 Gy` over `34` fractions

Once that context exists, the rest of the pipeline can build relative-dose Mayo queries and normalize session-plan dose metrics.

### 5. Metric extraction

[MetricExtraction.fs](../MetricExtraction.fs) is where domain intent becomes executed work.

It does four important things:

1. Build the Mayo query from prescription context.
2. Match the target structure for the metric.
3. Execute the ESAPIX Mayo query.
4. Normalize `D1.8cc` values to the original full-course fraction context.

This module is a good example of keeping the clinical meaning visible in code. The normalization logic is not hidden in a helper with a vague name. It sits close to the metric-kind branching, which makes the behavior easier to audit.

### 6. Application orchestration

[Program.fs](../Program.fs) owns the application workflow:

- create the ESAPI application
- iterate patients
- collect metric rows
- collect plan-level debug rows
- collect dose-context debug rows
- write CSV outputs
- print summary counts

This module stays imperative on purpose. It is the program shell. The inner logic remains functional in the other modules.

## Functional style choices that help here

Several choices in the repo are worth keeping:

- Small modules with one responsibility each.
- Records for shaped data instead of loose tuples.
- `Result<_, string>` for recoverable failures.
- Pure helpers for formatting and query construction.
- Side effects pushed to the edges.

This style helps a clinical extraction tool because it reduces hidden state and makes audit paths clearer.

## Where the design is strict

The code is strict in a few places for good reasons:

- Ambiguous structure matches fail instead of guessing.
- Unknown prescription patterns fail instead of inventing defaults.
- Missing fraction context blocks normalization instead of fabricating a value.

Those are not inconveniences. They are safety boundaries.

## Practical reading order

If someone new joins the project, the best reading order is:

1. [Config.fs](../Config.fs)
2. [Domain.fs](../Domain.fs)
3. [Prescription.fs](../Prescription.fs)
4. [MetricExtraction.fs](../MetricExtraction.fs)
5. [EsapiQuery.fs](../EsapiQuery.fs)
6. [Program.fs](../Program.fs)

That order starts with intent, then moves through data shape, then logic, then orchestration.
