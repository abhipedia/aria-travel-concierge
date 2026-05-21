# ✈️ Trip Planner — meet **Aria**, your AI travel concierge

A polished ASP.NET Core **Razor Pages** app (.NET 10) that turns a short form into a
budget-aware, beautifully-paced travel itinerary, written in the voice of *Aria* — an
elite private travel concierge persona. Powered by your choice of **OpenAI** or
**Anthropic (Claude)**.

> _"Tell Aria a few things about your trip and she'll craft a budget-aware,
> beautifully-paced itinerary — complete with a signature wow moment, insider
> tradecraft, and the kind of details a great concierge whispers in your ear."_

---

## ✨ What it does

- **Smart input form** — origin & destination (with currency auto-detection),
  travel month, travelers, days, budget, accommodation tier, pace, travel vibes,
  dietary preferences and "anything to avoid" — all rendered as friendly chip-style
  pickers.
- **First-timer vs. repeat-visitor personalization**
  - First-timers can pick **"Must-sees you don't want to miss"** for the chosen
    destination — Aria anchors the itinerary on those.
  - Repeat visitors can tick **"Places you've already visited"** — Aria deliberately
    routes around them and surfaces fresh ground.
- **Quick-start presets** — one-click trip ideas (Romantic Italy, Family Japan,
  Solo Thailand, Foodie France, Kiwi Adventure, Dubai Luxe) and a "Try an example"
  button.
- **Concierge-grade prompt** — a richly structured prompt with 15+ hard requirements
  plus a strict **OUTPUT CONTRACT** so the response format is consistent across runs.
- **Multi-provider AI client** — OpenAI Chat Completions or Anthropic Messages,
  switchable via configuration. Provider-aware key validation, robust response
  parsing, friendly error messages.
- **Production niceties** — `/health` endpoint, per-IP rate limiting (20/min),
  privacy disclaimer, copy / print / "plan another" actions, Dockerfile.

---

## 🧱 Tech stack

| Concern | Choice |
|---|---|
| Framework | ASP.NET Core **Razor Pages** on **.NET 10** |
| Language | C# 14 |
| UI | Bootstrap 5 + Bootstrap Icons + Google Fonts (Inter, Playfair Display) |
| AI providers | OpenAI Chat Completions API · Anthropic (Claude) Messages API |
| Configuration | `IOptions<AiOptions>` bound to the `Ai` section, user-secrets in dev |
| Rate limiting | `Microsoft.AspNetCore.RateLimiting` (FixedWindow, per-IP) |
| Health checks | `Microsoft.Extensions.Diagnostics.HealthChecks` (`/health`) |
| Hosting | Self-hosted Kestrel · Docker (multi-stage `Dockerfile`) |

---

## 🚀 Run it locally (5-minute setup)

### 1. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| **.NET SDK** | **10.0** or newer | <https://dotnet.microsoft.com/download> |
| **Git** | any recent | <https://git-scm.com/downloads> |
| **An LLM API key** | OpenAI **or** Anthropic | see step 4 |
| **IDE** *(optional)* | Visual Studio 2026 / 2022 (17.12+) or VS Code with C# Dev Kit | the project also runs from a plain terminal |

Verify your SDK:

```powershell
dotnet --version
# expected: 10.0.x
```

### 2. Clone the repo

```powershell
git clone https://github.com/<your-org>/TripPlannerV1.git
cd TripPlannerV1
```

### 3. Restore & build

```powershell
dotnet restore
dotnet build
```

### 4. Get an API key (pick one)

#### Option A — OpenAI

1. Go to <https://platform.openai.com/api-keys> and create a key. It will start with `sk-…`.
2. Recommended models: `gpt-4o-mini` (fast & cheap), `gpt-4o` (higher quality).

#### Option B — Anthropic (Claude)

1. Go to <https://console.anthropic.com/settings/keys> and create a key. It will start with `sk-ant-…`.
2. Recommended models: `claude-3-5-sonnet-latest`, `claude-3-5-haiku-latest`, `claude-3-opus-latest`.

### 5. Store the key with **dotnet user-secrets** (do NOT commit it)

The project ships with `<UserSecretsId>trip-planner-v1-7f6a2b3c</UserSecretsId>` in
the csproj, so user-secrets work out of the box.

```powershell
cd TripPlannerV1     # the project folder, not the repo root

# --- For OpenAI ---
dotnet user-secrets set "Ai:Provider" "OpenAI"
dotnet user-secrets set "Ai:ApiKey"   "sk-...your-key..."
dotnet user-secrets set "Ai:Model"    "gpt-4o-mini"

# --- For Anthropic (Claude) ---
dotnet user-secrets set "Ai:Provider" "Anthropic"
dotnet user-secrets set "Ai:ApiKey"   "sk-ant-...your-key..."
dotnet user-secrets set "Ai:Model"    "claude-3-5-sonnet-latest"

# Optional tuning
dotnet user-secrets set "Ai:MaxTokens"    "4096"
dotnet user-secrets set "Ai:Temperature"  "0.7"

# Verify
dotnet user-secrets list
```

> **Alternative for CI / containers:** set environment variables instead.
> The double-underscore notation maps to nested config keys.
>
> ```powershell
> $env:Ai__Provider = "Anthropic"
> $env:Ai__ApiKey   = "sk-ant-..."
> $env:Ai__Model    = "claude-3-5-sonnet-latest"
> ```

### 6. Run

```powershell
cd TripPlannerV1
dotnet run
```

Open the URL Kestrel prints (typically `https://localhost:5001`) and plan a trip.
Health probe: `GET /health` returns `Healthy`.

---

## ⚙️ Configuration reference

Bound to the `Ai` section via `IOptions<AiOptions>`:

| Key | Type | Default | Description |
|---|---|---|---|
| `Ai:Provider` | string | `"OpenAI"` | `"OpenAI"` / `"GPT"` or `"Anthropic"` / `"Claude"` (case-insensitive) |
| `Ai:ApiKey` | string | — | Provider API key. **Never commit this.** Use user-secrets / env vars. |
| `Ai:Model` | string | provider default | OpenAI: `gpt-4o-mini`. Anthropic: `claude-3-5-sonnet-latest` |
| `Ai:MaxTokens` | int | `4096` | Max tokens for the response. |
| `Ai:Temperature` | double | `0.7` | Sampling temperature, 0.0 – 2.0. |

---

## 🐳 Run in Docker

```powershell
docker build -t trip-planner .
docker run --rm -p 8080:8080 `
  -e Ai__Provider=Anthropic `
  -e Ai__ApiKey=sk-ant-... `
  -e Ai__Model=claude-3-5-sonnet-latest `
  trip-planner
```

Open <http://localhost:8080>.

---

## 🛠️ Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| `Ai:ApiKey is NOT configured` warning at startup | Run `dotnet user-secrets set Ai:ApiKey "..."` in the `TripPlannerV1` folder. |
| `Ai:ApiKey looks too short …` | You pasted a placeholder. Replace with the real key. |
| `Ai:ApiKey does not look like an Anthropic key` | Either the key isn't from Anthropic, or `Ai:Provider` is set wrong. |
| OpenAI returns `401` | A `Bearer ` prefix or stray quotes around the key. Re-set the secret cleanly. |
| Anthropic response truncated | Raise `Ai:MaxTokens` (default 4096). |
| Port already in use | `dotnet run --urls "https://localhost:7080;http://localhost:7081"` |

---

## 📄 License

Add your preferred license (MIT, Apache-2.0, …) and a `LICENSE` file at the repo root.
