# Blazor FX Dashboard

A pilot project: a **Blazor WebAssembly** dashboard that runs entirely in the browser, takes a few simple inputs, and visualizes data from an open financial API.

> **Status:** Working dashboard with inputs, rate cards, a converter and an interactive time-series chart. Deploying to GitHub Pages is next.

## Goals

- Build a client-only Blazor WASM app (no backend) that calls a public API directly from the browser.
- Keep the UI simple: a handful of inputs driving a small set of cards and charts.
- Use a typed API client so a second data source can be added later without reworking the UI.
- Deployable as static files (e.g. GitHub Pages).

## Data source: Frankfurter API (v2)

[Frankfurter](https://frankfurter.dev) provides daily foreign-exchange reference rates.

- **No API key**, no signup.
- **CORS enabled** (`access-control-allow-origin: *`), so the browser can call it directly.
- ~170 currencies, plus gold, silver, platinum and palladium (`XAU`, `XAG`, `XPT`, `XPD`).
- We use **v2**; v1 responses carry a deprecation header pointing to `/v2/rates`.

| Endpoint | Purpose |
|---|---|
| `GET /v2/currencies` | Currency list (ISO code, name, symbol, date range available) |
| `GET /v2/rates?base=USD&quotes=EUR,GBP` | Latest rates |
| `GET /v2/rates?base=USD&quotes=EUR&from=2026-09-01&to=2026-09-30` | Time series |

Base URL: `https://api.frankfurter.dev`

v2 returns a flat array, one row per date/quote:

```json
[{ "date": "2026-09-01", "base": "USD", "quote": "EUR", "rate": 0.862 }]
```

Rates are only published on business days, so time series have gaps (weekends/holidays).

## Dashboard

**Inputs**
- Base currency (any of the ~170 Frankfurter currencies)
- Currencies to compare against, added and removed as chips (default EUR, GBP, JPY; at most 8)
- Date range (default: last 30 days)
- Amount for the converter (default 1,000)

**Outputs**
- One card per compared currency showing:
  - the latest rate and its date
  - the change since the start of the range
  - the amount converted at the latest rate
- A line chart of **% change since the start of the range**, one line per compared currency (details below)

Any input change refetches rates with a single `/v2/rates` time-series call; the newest request cancels any still in flight. While it loads, the cards and chart keep their previous content, dimmed. Currencies with no rates in the selected range are listed under the cards.

### The chart

A hand-written SVG component (`RateChart`), with no charting library and no JavaScript.

- **Why % change and not raw rates:** raw rates differ by orders of magnitude (JPY ~150 vs EUR ~0.9), so on one axis most lines would be flat. Indexing every currency to its first day puts them on one comparable axis, with a zero baseline.
- **Colors** come from an 8-slot categorical palette, validated for color-blind separation in both light and dark themes. A currency keeps its color while it stays selected; removing one never repaints the others, and a new currency takes the lowest free color. That's why at most 8 currencies can be compared.
- **Identity is never color alone:** a legend (2+ series), a label at the end of each line with its latest change (up to 4 series), and a data table.
- **Hover** anywhere over the plot for a crosshair and a tooltip listing every currency's change and rate on that date. The chart is keyboard-accessible: focus it and use ←/→, Home/End, Esc.
- **Data table:** "Show data table" under the chart lists every rate by date.
- Axis ticks use round numbers (1, 2, 2.5 or 5 × 10ⁿ); the x-axis is scaled by calendar date, so weekends and holidays show as gaps.
- On narrow screens the chart scrolls sideways inside its frame instead of shrinking its text.

## Tech stack

- .NET 10 SDK, Blazor WebAssembly (standalone, empty template, no CSS framework)
- `HttpClient` with a typed Frankfurter client
- Plain CSS with light/dark themes via `prefers-color-scheme`
- Charts: hand-written SVG Razor components (no charting library, no JS interop)

## Project structure

```
BlazorDashboard.slnx
dotnet-tools.json                  # Local tools (ReportGenerator for coverage)
src/BlazorDashboard/
├── Program.cs                     # DI setup: FrankfurterClient, TimeProvider
├── Services/
│   ├── FrankfurterClient.cs       # Typed client for /v2/currencies and /v2/rates
│   ├── FrankfurterModels.cs       # Currency and Rate records (JSON mapping)
│   ├── RateSummary.cs             # First/last rate and % change per quote
│   ├── NumberFormat.cs            # Magnitude-aware number formatting
│   └── ReadOnlyListExtensions.cs
├── Charts/
│   ├── RateChart.razor            # SVG line chart: legend, labels, hover, keyboard, table
│   ├── RateChartLayout.cs         # Pure geometry: points, ticks, hover columns, label placement
│   └── NiceScale.cs               # Round-number axis ranges and tick steps
├── Pages/Home.razor               # The dashboard: inputs, rate cards, converter, chart
├── Layout/MainLayout.razor
└── wwwroot/
    ├── index.html
    └── css/app.css                # All styling (theme tokens, chart palette, layout)
tests/BlazorDashboard.Tests/
├── Services/                      # Unit tests: client, summaries, formatting
├── Charts/                        # Unit tests for scale and layout; bUnit tests for RateChart
├── Pages/HomeTests.cs             # bUnit component tests for the dashboard
├── AppTests.cs                    # Routing: dashboard and not-found page
└── TestSupport/                   # Fake Frankfurter API, stub HTTP handler, fixed clock
```

The page gets "today" from an injected `TimeProvider` so tests can pin the date.

## Development

Prerequisites: .NET 10 SDK (developed in WSL / Ubuntu).

```bash
dotnet build BlazorDashboard.slnx
dotnet run --project src/BlazorDashboard --launch-profile http
```

Then open http://localhost:5036.

## Testing

The project is developed test-first: each behavior change starts with a test that is run and seen to fail, then the code is written to make it pass.

Tests use xUnit and [bUnit](https://bunit.dev) and never touch the network: the Frankfurter API is replaced by an in-memory fake behind a stub `HttpMessageHandler`.

```bash
dotnet test BlazorDashboard.slnx
```

Coverage (coverlet + ReportGenerator):

```bash
dotnet tool restore
rm -rf TestResults
dotnet test BlazorDashboard.slnx --collect:"XPlat Code Coverage" --results-directory ./TestResults
dotnet reportgenerator -reports:"TestResults/*/coverage.cobertura.xml" -targetdir:TestResults/coverage-report -reporttypes:"Html;TextSummary"
```

Open `TestResults/coverage-report/index.html` for the full report.

**Current coverage:** 129 tests; 98% line and 98.8% branch coverage. Everything except `Program.cs` (startup wiring, which tests don't run) is at or near 100%. The one uncovered branch in `Home.razor` is a defensive guard that `HttpClient`'s own cancellation handling makes unreachable in tests.

## Roadmap

- [x] Choose data source (Frankfurter v2)
- [x] Initialize repository
- [x] Scaffold Blazor WASM project
- [x] Typed Frankfurter API client
- [x] Dashboard page: inputs, rate cards, converter
- [x] Unit and component tests with coverage reporting
- [x] Time-series chart
- [ ] Deploy to GitHub Pages
- [ ] (Stretch) Add a second data source, e.g. World Bank indicators or Finnhub stocks
