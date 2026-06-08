# EShooting

EShooting ox atis zalinda lane idareetmesi, scoring ve monitor overlay ehtiyaclari ucun qurulmus clean architecture skeletidir.

## Arxitektura

- `src/EShooting.Domain`: esas domain modelleri (`Athlete`, `Lane`, `TrainingSession`, `ScoreEntry`)
- `src/EShooting.Application`: CQRS + MediatR command/query handler-lari
- `src/EShooting.Infrastructure`: EF Core + MS SQL repository implementasiyasi
- `src/EShooting.Web`: MVC UI + REST endpoint-ler + SignalR hub

## Texniki Detallar

- 11 lane ilkin olaraq seed olunur:
  - 1-8: `Amateur`
  - 9-11: `Professional`
- Database olaraq `MS SQL` istifade olunur (`(localdb)\MSSQLLocalDB` default)
- Real-time eventler:
  - `lane-updated`
  - `score-updated`

## Isletme

### Tətbiq (MVC UI + Web endpoint-ler)

```bash
dotnet run --project src/EShooting.Web
```

Sonra brauzerde resepsiya panelini acin:

- `http://localhost:5299/qeydiyyat` (resepsiya)
- `http://localhost:5299/` → `/qeydiyyat`-a yonlendirilir

## IIS — HTTP 500.30 (app failed to start)

Bu xeta Chrome/JavaScript deyil: proqram IIS-de baslamadan dayanir.

1. **ASP.NET Core 8 Hosting Bundle** qurasdirin (serverde .NET 8 runtime).
2. **SQL Server** isleyir ve `appsettings.Production.json` icinde `ConnectionStrings:DefaultConnection` duzgundur.
   - IIS App Pool adeten `ApplicationPoolIdentity` ile SQL-e daxil ola bilmir.
   - Ya SQL-de login verin, ya da connection string-de `User Id` / `Password` istifade edin.
3. Publish qovlugunda `logs` papkasi yaradib App Pool-a **yazma** icazesi verin; `logs\stdout_*.log` faylinda real xeta yazilir.
4. IIS Application Pool → **.NET CLR version: No Managed Code**, **Integrated** pipeline.
