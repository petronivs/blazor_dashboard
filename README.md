# Blazor World Finance Dashboard

[![CI](https://github.com/petronivs/blazor_dashboard/actions/workflows/ci.yml/badge.svg)](https://github.com/petronivs/blazor_dashboard/actions/workflows/ci.yml)

A pilot project: a **Blazor WebAssembly** dashboard that runs entirely in the browser, takes a few simple inputs, and visualizes data from open financial APIs.

**Live site:** https://petronivs.github.io/blazor_dashboard/

> **Status:** Working World Finance dashboard shell with two tabs: foreign exchange (Frankfurter) and inflation indicators (World Bank), each with cards and an interactive time-series chart. Every push is built and tested by GitHub Actions; pushes to `main` deploy to GitHub Pages.

## Goals

- Build a client-only Blazor WASM app (no backend) that calls a public API directly from the browser.
- Keep the UI simple: a handful of inputs driving a small set of cards and charts.
- Use a typed API client so a second data source can be added later without reworking the UI.
- Deployable as static files (e.g. GitHub Pages).

## Data sources

### Frankfurter API (v2)

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

### World Bank Indicators API (v2)

[World Bank Indicators](https://datahelpdesk.worldbank.org/knowledgebase/articles/889392-api-basic-call-structures) provides annual country-level macroeconomic series.

- **No API key**, no signup.
- **CORS enabled**, so the browser can call it directly.
- One request can include multiple countries for one indicator.

| Endpoint | Purpose |
|---|---|
| `GET /country?format=json&per_page=400` | Country catalog (aggregates filtered out in-app) |
| `GET /country/USA;DEU;JPN/indicator/FP.CPI.TOTL.ZG?format=json&date=2010:2025&per_page=1000` | Multi-country annual inflation series |

Base URL: `https://api.worldbank.org/v2`

Responses use a two-element JSON array `[paging, rows]`. Rows can be `null`, and individual row values can also be `null` (often for the newest year).

## Dashboard

The app presents a **World Finance Dashboard** shell with dedicated tabs for each data source.

### Foreign exchange tab

**Inputs**
- Base currency (any of the ~170 Frankfurter currencies)
- Currencies to compare against, added and removed as chips (default EUR, GBP, JPY; at most 8)
- Date range (default: last 30 days)
- Amount for the converter (default 1,000)
- The current selections are remembered in a cookie and restored on the next visit
- The default last-30-days range stays rolling, and a To date left at today still rolls forward even if you customize From
- The page shows a cookie notice because the remembered selections use browser cookies

**Outputs**
- One card per compared currency showing:
  - the latest rate and its date
  - the change since the start of the range
  - the amount converted at the latest rate
- A line chart of **% change since the start of the range**, one line per compared currency (details below)

Any input change refetches rates with a single `/v2/rates` time-series call; the newest request cancels any still in flight. While it loads, the cards and chart keep their previous content, dimmed. Currencies with no rates in the selected range are listed under the cards.

### Inflation tab

The World Bank tab compares annual inflation indicators across selected countries.

**Inputs**
- Country chips (default USA, DEU, JPN, GBR; at most 8)
- Indicator dropdown:
  - `FP.CPI.TOTL.ZG` (Inflation, consumer prices, annual %; default)
  - `FP.CPI.TOTL` (Consumer price index, 2010 = 100)
- Year range (default: last 15 years)

**Outputs**
- One card per country with its latest non-null value and source year
- A chart with years on the x-axis and raw indicator values (no re-indexing)
- Countries with no data in the selected range, listed under the cards

Like FX, the newest request cancels any in-flight request and the previous cards/chart stay visible but dimmed while loading.

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
- `HttpClient` with typed Frankfurter and World Bank clients
- Plain CSS with light/dark themes via `prefers-color-scheme`
- Charts: hand-written SVG Razor components, plus a tiny JS helper to read/write the state cookie

## Project structure

```
BlazorDashboard.slnx
dotnet-tools.json                  # Local tools (ReportGenerator for coverage)
.github/workflows/ci.yml           # Build, test, coverage; deploy main to GitHub Pages
src/BlazorDashboard/
├── Program.cs                     # DI setup: FrankfurterClient, WorldBankClient, TimeProvider
├── Services/
│   ├── CookieDashboardStateStore.cs # Reads/writes the remembered dashboard selections cookie
│   ├── DashboardState.cs          # Serializable snapshot of the remembered selections
│   ├── FrankfurterClient.cs       # Typed client for /v2/currencies and /v2/rates
│   ├── WorldBankClient.cs         # Typed client for country list + indicator series endpoints
│   ├── WorldBankModels.cs         # World Bank DTOs and mapped dashboard records
│   ├── IDashboardStateStore.cs    # Abstraction for restoring/saving dashboard selections
│   ├── FrankfurterModels.cs       # Currency and Rate records (JSON mapping)
│   ├── RateSummary.cs             # First/last rate and % change per quote
│   ├── NumberFormat.cs            # Magnitude-aware number formatting
│   └── ReadOnlyListExtensions.cs
├── Charts/
│   ├── RateChart.razor            # SVG line chart: legend, labels, hover, keyboard, table
│   ├── RateChartLayout.cs         # Pure geometry: points, ticks, hover columns, label placement
│   └── NiceScale.cs               # Round-number axis ranges and tick steps
├── Components/
│   ├── FxDashboard.razor          # Frankfurter-backed FX dashboard tab content
│   └── InflationDashboard.razor   # World Bank-backed inflation dashboard tab content
├── Pages/Home.razor               # World Finance shell and dashboard tabs
├── Layout/MainLayout.razor
└── wwwroot/
    ├── index.html
    └── css/app.css                # All styling (theme tokens, chart palette, layout)
tests/BlazorDashboard.Tests/
├── Components/                    # bUnit tests for FxDashboard behavior and cookie persistence
│   ├── FxDashboardTests.cs
│   ├── FxDashboardCookiePersistenceTests.cs
│   └── InflationDashboardTests.cs
├── Services/                      # Unit tests: client, summaries, formatting
├── Charts/                        # Unit tests for scale and layout; bUnit tests for RateChart
├── Pages/HomeTests.cs             # bUnit tests for the World Finance shell
├── AppTests.cs                    # Routing: dashboard and not-found page
└── TestSupport/                   # Fake Frankfurter/World Bank APIs, stub HTTP handler, fixed clock
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

Tests use xUnit and [bUnit](https://bunit.dev) and never touch the network: Frankfurter and World Bank APIs are replaced by in-memory fakes behind a stub `HttpMessageHandler`.

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

**Current coverage:** 160 tests; line/branch coverage remains ~98% overall. Everything except `Program.cs` (startup wiring, which tests don't run) is at or near 100%.

## CI and deployment

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every push to any branch, on pull requests, and on demand:

1. **Build and test** (all runs): Release build, the full test suite with coverage, a coverage table in the run's summary page, and the test results plus HTML coverage report uploaded as the `test-results` artifact.
2. **Deploy to GitHub Pages** (pushes to `main` only, after tests pass): `dotnet publish` in Release, then:
   - rewrite `<base href="/">` to `/blazor_dashboard/`, since Pages serves the site from that sub-path (the step fails if the rewrite doesn't match);
   - delete the pre-compressed `index.html.br`/`.gz`, which would still hold the old base path;
   - copy `index.html` to `404.html` so unknown URLs fall through to the Blazor router.

A newer push cancels an older in-progress run on the same branch, except on `main`, where a deployment is allowed to finish.

GitHub Pages is configured with **Source: GitHub Actions** (repository Settings → Pages).

## Roadmap

- [x] Choose data source (Frankfurter v2)
- [x] Initialize repository
- [x] Scaffold Blazor WASM project
- [x] Typed Frankfurter API client
- [x] Dashboard page: inputs, rate cards, converter
- [x] Unit and component tests with coverage reporting
- [x] Time-series chart
- [x] CI on every push (GitHub Actions)
- [x] Deploy to GitHub Pages
- [x] (Stretch) Add a second data source, e.g. World Bank indicators or Finnhub stocks
