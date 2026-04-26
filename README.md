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

Sonra brauzerde ana sehifeni acin:

- `http://localhost:5299/`
