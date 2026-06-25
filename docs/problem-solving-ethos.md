# Problem-Solving Ethos

## What this project is trying to optimize for

This tool is not just extracting numbers. It is trying to extract numbers that can be defended.

That changes the engineering standard. In this context, the goal is not:

- shortest code
- fastest patch
- most clever abstraction

The goal is:

- clinically meaningful outputs
- transparent transformations
- reproducible debugging
- failure modes that are obvious instead of silent

## The ethos behind the implementation

The code should behave like a careful reviewer, not a guesser.

That means:

- infer only when there is a defined rule
- fail when the rule does not hold
- keep intermediate context visible
- log enough information to prove why a metric value exists

This is why the repository now keeps both the main extract and the debug CSVs. The extract gives the result. The debug files give the reasoning trail.

## A useful rule for this codebase

When a choice exists between convenience and explainability, prefer explainability.

Examples:

- A normalized structure match is allowed only when the normalized ids match exactly.
- Multiple normalized candidates produce an error with candidate ids.
- Prescription inference is based on explicit `PTV1` naming rules.
- Dose normalization requires current fraction context and fails when that context is missing.

Each of those choices makes the tool slightly less permissive and much more trustworthy.

## Problem solving in practice

The project works best when changes are made in this order:

1. Define the clinical intent clearly.
2. Encode that intent in types or narrow functions.
3. Keep ESAPI and file-system side effects at the edge.
4. Add debug output that proves the transformation.
5. Build and inspect the generated artifacts.

That order prevents a common failure pattern where the code technically runs but the meaning of the result becomes unclear.

## Why explicit debug output matters

In this domain, a wrong number can look completely plausible.

That is why debug output is not a convenience feature. It is part of the validation strategy.

The current debug exports answer questions like:

- Which structure was requested?
- Which structure was actually matched?
- Was the match exact or normalized?
- What original prescription was inferred?
- What Mayo query was finally executed?
- What fraction normalization was applied?
- What dose context did the accepted plan expose?

If those questions cannot be answered from artifacts, the extraction pipeline is too opaque.

## What good changes look like here

A good change in this repo usually has these properties:

- It makes the domain model more explicit.
- It removes hardcoded technical assumptions from config.
- It narrows ambiguity instead of broadening it.
- It adds evidence for how outputs were produced.
- It keeps modules small and responsibilities clean.

## What to avoid

Avoid changes that:

- silently fall back to guesses
- blur the distinction between raw and normalized values
- hide clinical rules inside generic utility functions
- mix ESAPI access, domain logic, and output formatting in one place
- remove debug evidence without replacing it with something equally useful

## A short engineering credo

For this project, good software is software that can answer:

- What did we ask for?
- What did we match?
- What did we execute?
- What did we normalize?
- Why should we trust the result?

If a patch makes those answers easier to give, it is probably moving the code in the right direction.
