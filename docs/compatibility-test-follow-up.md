# Follow-up: Builder.Data compatibility assertion

Recorded during agentic-coding training preparation on September 16, 2026.

- [ ] Update `Aurora.Tests/Tests/BuilderDataCompatibilityTests.cs`, specifically `RestoredAssembly_PreservesLegacyIdentityAndExportedTypeCount`.

At training baseline commit `c7f81f2c01ff290243f3041657dde320560b1158`, the test expects exactly 140 exported types, but the assembly exposes 165. Commit `624b6b7` added 25 public types for local corrections and content-review support without updating the original restoration-era assertion.

This failure establishes a stale exact-count expectation, not a demonstrated runtime regression. The source-ownership documentation permits intentional additions after restoration.

Prefer checking that required legacy types and public signatures remain compatible while permitting intentional additions. Do not merely replace 140 with 165 or remove compatibility coverage. Retain the assembly name/version assertions where still required by policy. Verify the chosen approach against the legacy oracle and focused compatibility tests.

Recheck the current branch before implementing: the finding above refers to the pinned training baseline. No test or production-code fix was made as part of this reminder.
