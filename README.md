# Blazor FX Dashboard

A pilot project: a **Blazor WebAssembly** dashboard that runs entirely in the browser, takes a few simple inputs, and visualizes data from an open financial API.

> **Status:** Working first version. The dashboard loads live rates with inputs, rate cards and a converter. The time-series chart is next.

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
- Currencies to compare against, added and removed as chips (default EUR, GBP, JPY)
- Date range (default: last 30 days)
- Amount for the converter (default 1,000)

**Outputs**
- One card per compared currency showing:
  - the latest rate and its date
  - the change since the start of the range
  - the amount converted at the latest rate
- *(Planned)* Line chart of rates over time

Any input change refetches rates with a single `/v2/rates` time-series call; the newest request cancels any still in flight. Currencies with no rates in the selected range are listed under the cards.

## Tech stack

- .NET 10 SDK, Blazor WebAssembly (standalone, empty template, no CSS framework)
- `HttpClient` with a typed Frankfurter client
- Plain CSS with light/dark themes via `prefers-color-scheme`
- Charting library: TBD

## Project structure

```
BlazorDashboard.slnx
src/BlazorDashboard/
├── Program.cs                     # DI setup: registers FrankfurterClient
├── Services/
│   ├── FrankfurterClient.cs       # Typed client for /v2/currencies and /v2/rates
│   └── FrankfurterModels.cs       # Currency and Rate records (JSON mapping)
├── Pages/Home.razor               # The dashboard: inputs, rate cards, converter
├── Layout/MainLayout.razor
└── wwwroot/
    ├── index.html
    └── css/app.css                # All styling (theme tokens, layout, cards)
```

## Development

Prerequisites: .NET 10 SDK (developed in WSL / Ubuntu).

```bash
dotnet build BlazorDashboard.slnx
dotnet run --project src/BlazorDashboard --launch-profile http
```

Then open http://localhost:5036.

## Roadmap

- [x] Choose data source (Frankfurter v2)
- [x] Initialize repository
- [x] Scaffold Blazor WASM project
- [x] Typed Frankfurter API client
- [x] Dashboard page: inputs, rate cards, converter
- [ ] Time-series chart
- [ ] Deploy to GitHub Pages
- [ ] (Stretch) Add a second data source, e.g. World Bank indicators or Finnhub stocks
