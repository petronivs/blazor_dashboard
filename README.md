# Blazor FX Dashboard

A pilot project: a **Blazor WebAssembly** dashboard that runs entirely in the browser, takes a few simple inputs, and visualizes data from an open financial API.

> **Status:** Planning / scaffolding. The repository is set up; the Blazor project has not been created yet.

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

## Planned dashboard

**Inputs**
- Base currency
- Target currencies (multi-select)
- Date range
- Amount (for the converter)

**Outputs**
- Latest-rate cards per target currency, with change over the selected range
- Line chart of rates over time
- Currency converter using the latest rates

## Tech stack

- .NET 10 SDK, Blazor WebAssembly (standalone)
- `HttpClient` with a typed Frankfurter client
- Charting library: TBD

## Development

Prerequisites: .NET 10 SDK (developed in WSL / Ubuntu).

Build and run instructions will be added once the project is scaffolded.

## Roadmap

- [x] Choose data source (Frankfurter v2)
- [x] Initialize repository
- [ ] Scaffold Blazor WASM project
- [ ] Typed Frankfurter API client
- [ ] Dashboard page: inputs, rate cards, converter
- [ ] Time-series chart
- [ ] Deploy to GitHub Pages
- [ ] (Stretch) Add a second data source, e.g. World Bank indicators or Finnhub stocks
