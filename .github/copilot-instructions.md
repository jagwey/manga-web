# Copilot instructions

## Project structure

- `index.html`, `read.html`, `login.html`, `admin.html` are standalone pages. Each loads React 18 and Babel from a CDN and runs inline `text/babel` code; there is no build step.
- `js/config.js` sets `window.KAGE95_API_URL`. Pages read the API address from there; never hard-code it.
- Styling lives in each page's inline `<style>` block (dark blood-red theme, `--vurgu-rengi`); there is no shared stylesheet.
- `backend/` is an ASP.NET Core (net10.0) API with EF Core on SQL Server (`Kage95DB`) and JWT auth. `Veritabani_Kurulumu.sql` creates the schema and sample data.

## Build and test

- API: `dotnet run` in `backend/` (listens on `http://localhost:5035`, Swagger at `/swagger`).
- Frontend: serve the repository root as static files on port 8080 (matches `.vscode/launch.json`).
- There is no automated test suite; verify changes in a browser at phone and desktop widths.

## Conventions

- User-facing text is Turkish (`lang="tr"`).
- Mobile first: responsive grids, touch targets of at least 44px.
- Palette: near-black backgrounds with blood-red accents (`--vurgu-rengi` #8e0e15, `--vurgu-parlak` #e0343c). No green anywhere; reuse the CSS variables already defined.
- Only content the owner has rights to may be used; sample data uses placeholder images.
