# Project notes for Claude

- Keep `README.md` up to date: whenever a change affects the project's status, setup, structure, data sources, features, or roadmap, update the README in the same change.
- Keep test coverage high: add or update tests in `tests/BlazorDashboard.Tests` with every behavior change, and run `dotnet test BlazorDashboard.slnx` before committing. Tests must not hit the real API; use `TestSupport/FakeFrankfurterApi`.
- The repo lives in WSL (`/home/adam/projects/blazor_dashboard`); run git and dotnet via WSL.
- Git author for this repo is set in the local repo config (GitHub no-reply address); don't change global git config.
