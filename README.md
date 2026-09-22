# Courier Dispatch

[![CI](https://github.com/LegionAlpha/CourierDispatch/actions/workflows/ci.yml/badge.svg)](https://github.com/LegionAlpha/CourierDispatch/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Пет-проект: сервис назначения курьеров на заказы.

## Стек

| Слой                        | Технологии                                                                      |
| --------------------------- | ------------------------------------------------------------------------------- |
| Runtime                     | .NET 10, Docker Compose                                                         |
| API                         | ASP.NET Core Minimal API                                                        |
| Межсервисное взаимодействие | gRPC (синхронно), RabbitMQ + MassTransit (команды, саги), Kafka (поток событий) |
| Данные                      | PostgreSQL (EF Core), Redis (GEO-индекс, Lua, кэш)                              |
| Архитектура                 | Clean Architecture, CQRS (MediatR)                                              |
| Надёжность                  | Saga, Outbox, Inbox, идемпотентность                                            |
| Observability               | Serilog → ELK, OpenTelemetry → Jaeger, Prometheus → Grafana                     |
| Тесты                       | xUnit, Testcontainers, k6                                                       |

## Как это работает

```
 клиент ──REST──▶ Orders ──gRPC──▶ Dispatch ◀──Kafka── CourierSimulator
                    │                 │  ▲
                    │   RabbitMQ      │  │ Redis GEO
                    └────(saga)───────┘  ▼
                                      куда ехать
```

1. Клиент создаёт заказ через `POST /orders`. `Orders` синхронно спрашивает у `Dispatch` по gRPC оценку времени и цены.
2. Заказ и событие `OrderCreated` сохраняются в одной транзакции (outbox). Фоновый паблишер отправляет событие в RabbitMQ.
3. В `Dispatch` стартует сага: ищет ближайших свободных курьеров в Redis GEO, атомарно резервирует слот (Lua), отправляет оффер, ждёт ответа с таймаутом, при отказе пробует следующего.
4. Курьеры — это `CourierSimulator`: консольное приложение, которое гонит геопозиции в Kafka и отвечает на офферы.
5. Результат (`CourierAssigned` / `NoCourierFound`) возвращается в `Orders` через RabbitMQ и inbox.

Жизненный цикл заказа:

```
Created → CourierSearching → Assigned → PickedUp → Delivered
                 │                │
                 ▼                ▼
          NoCourierFound      Cancelled
```

## Запуск

Требования: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), Docker Desktop.

```bash
# Инфраструктура (пока только Postgres, остальное добавится по мере написания кода)
docker compose up -d

# Применить миграции
dotnet ef database update --project src/Orders/Orders.Infrastructure --startup-project src/Orders/Orders.Api

# Запустить Orders API
dotnet run --project src/Orders/Orders.Api
```

Тесты:

```bash
dotnet test
```

## Структура

```
src/
├── Orders/                     # сервис заказов
│   ├── Orders.Domain/          # сущности, инварианты
│   ├── Orders.Application/     # use cases (команды/запросы), интерфейсы
│   ├── Orders.Infrastructure/  # EF Core, брокеры, gRPC-клиент
│   └── Orders.Api/             # REST, composition root
├── Dispatch/                   # сервис диспетчеризации
├── Shared/                     # контракты сообщений, общая инфраструктура
└── CourierSimulator/           # генератор курьеров
tests/
```

## Roadmap

- [x] **1a** — solution, слои `Orders`, правило зависимостей
- [x] **1b** — домен `Order`: state-machine, value objects, unit-тесты
- [x] **1c** — PostgreSQL в Docker, EF Core, миграции
- [ ] **1d** — CQRS через MediatR, валидация, `POST /orders`, `GET /orders/{id}`
- [ ] **1e** — health checks
- [ ] **1f** — сервис `Dispatch`, gRPC-сервер `EstimateDelivery`
- [ ] **1g** — gRPC-клиент в `Orders`
- [ ] **1h** — RabbitMQ, Kafka, Redis в compose, Dockerfile'ы
- [ ] **2** — Outbox / Inbox, MassTransit, сага назначения курьера
- [ ] **3** — Kafka: `CourierSimulator` → консьюмер → Redis GEO
- [ ] **4** — Lua-резерв слота, таймауты оффера, компенсации, отмена
- [ ] **5** — OpenTelemetry, ELK, Prometheus / Grafana
- [ ] **6** — Testcontainers, k6
- [ ] **7** — ADR, диаграммы

## Лицензия

[MIT](LICENSE)
