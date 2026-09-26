# Project notes for Claude

## Test-driven development

All behavior changes (features and bug fixes) follow red → green → refactor:

1. **Red:** write or update the test(s) in `tests/BlazorDashboard.Tests` that describe the new behavior, before touching production code.
2. **Confirm it fails:** run `dotnet test BlazorDashboard.slnx` (use `--filter` to target the new tests) and check that the new tests fail *for the expected reason*: an assertion about the missing behavior, not a compile error, typo, or broken test setup. If a new test passes before the change, the test is wrong or the behavior already exists; fix the test before continuing. Mention the observed failure when reporting the work.
3. **Green:** make the smallest production change that makes the tests pass.
4. **Refactor:** clean up with the full suite passing, then run the whole suite again before committing.

Notes:
- A test that needs a new type or member may fail to compile at first. Add a minimal stub (e.g. `throw new NotImplementedException()`) so the test compiles and fails at runtime, then implement.
- For bug fixes, first write a test that reproduces the bug and fails.
- Pure refactors, docs, styling-only CSS, and config changes don't need a new failing test, but the existing suite must stay green.
- Tests must not hit the real API; use `TestSupport/FakeFrankfurterApi`. Keep coverage high (see README "Testing" for the coverage commands).

## Other conventions

- CI (`.github/workflows/ci.yml`) builds and tests every push, in Release; pushes to `main` deploy to GitHub Pages under `/blazor_dashboard/`. Before pushing, make sure a Release build and test run pass locally (`dotnet build -c Release` then `dotnet test --no-build -c Release`). After pushing, check the run with `gh run list` / `gh run watch`.

- Keep `README.md` up to date: whenever a change affects the project's status, setup, structure, data sources, features, or roadmap, update the README in the same change.
- The repo lives in WSL (`/home/adam/projects/blazor_dashboard`); run git and dotnet via WSL.
- Git author for this repo is set in the local repo config (GitHub no-reply address); don't change global git config.
