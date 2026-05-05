# PaymentEDA — Event-Driven Architecture in Payments (.NET 8)

> A production-ready reference implementation of Event-Driven Architecture for payment systems, built with .NET 8, MassTransit, RabbitMQ, and PostgreSQL.

## 📐 Architecture

```
User/Merchant
     │
     ▼
┌─────────────────┐        ┌────────────────────────────┐
│  Payment Service │──────▶│   RabbitMQ (Event Bus)     │
│  (Producer)      │       │                            │
│  + Outbox Table  │       │  • PaymentCreatedEvent     │──▶ Fraud Service      → Fraud DB
└─────────────────┘       │  • PaymentAuthorizedEvent  │──▶ Notification Svc   
         │                 │  • PaymentCapturedEvent    │──▶ Ledger Service     → Ledger DB
         ▼                 │  • PaymentFailedEvent      │──▶ Reconciliation Svc → Recon DB
    Payment DB             │  • PaymentSettledEvent     │──▶ Analytics Service  → Analytics DB
                           └────────────────────────────┘
                                        │
                                        ▼
                               Saga Service (Orchestrator)
                               + Saga DB (State Machine)
```

## 🏗️ Services

| Service | Port | Responsibility |
|---|---|---|
| **PaymentService** | 5001 | REST API, event producer, Outbox Pattern |
| **FraudService** | 5002 | Fraud scoring, Idempotent Consumer |
| **NotificationService** | 5003 | User notifications (email/SMS/push) |
| **LedgerService** | 5004 | Accounting entries |
| **ReconciliationService** | 5005 | Settlement reconciliation |
| **AnalyticsService** | 5006 | Event tracking & reporting |
| **SagaService** | 5007 | Payment state orchestration |
| **RabbitMQ UI** | 15672 | Message broker management |

## 🎯 Patterns Implemented

### 1. Outbox Pattern
Prevents dual-write issues between DB and message broker. Events are first written to an `OutboxMessages` table in the same transaction as the domain change, then a background worker publishes them to RabbitMQ.

```
[Payment Created] ──transaction──▶ [Payment DB] + [OutboxMessages]
                                         │
                              [OutboxPublisher Background Service]
                                         │
                                         ▼
                                    [RabbitMQ]
```

### 2. Saga Pattern (Orchestration)
`PaymentStateMachine` tracks the full payment lifecycle via `CorrelationId`:

```
Initial ──[PaymentCreated]──▶ Created
Created ──[FraudPassed]──▶ FraudCleared ──[Authorized]──▶ Authorized
Authorized ──[Captured]──▶ Captured ──[Settled]──▶ Settled (Final)
Any ──[PaymentFailed]──▶ Failed
```

### 3. Idempotent Consumer
Every consumer checks `MessageId` against its database before processing. Duplicate messages (from RabbitMQ redeliveries) are silently skipped. A `UNIQUE` index on `MessageId` enforces this at the DB level.

### 4. Dead Letter Queue (DLQ)
Failed messages are retried with exponential backoff (3 attempts). After exhausting retries, MassTransit moves them to `{queue-name}_error` exchange for manual inspection/reprocessing.

```
Message ──▶ Queue ──[fail]──▶ Retry (x3, exponential)
                      └──[max retries]──▶ DLQ (_error queue)
```

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- .NET 8 SDK (for local development)

### Run with Docker Compose

```bash
git clone https://github.com/your-username/PaymentEDA.git
cd PaymentEDA

docker-compose up --build
```

Services will be available at:
- Payment API: http://localhost:5001/swagger
- RabbitMQ UI: http://localhost:15672 (guest/guest)
- Analytics: http://localhost:5006/analytics/summary
- Saga State: http://localhost:5007/saga/{correlationId}

### Create a Payment

```bash
curl -X POST http://localhost:5001/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userId": "4fa85f64-5717-4562-b3fc-2c963f66afa7",
    "amount": 1500.00,
    "currency": "TRY",
    "method": "CreditCard",
    "description": "Order #12345"
  }'
```

### Watch the Event Flow

After creating a payment:
1. Open RabbitMQ UI → Queues (see messages flowing)
2. Check Fraud Service logs → fraud score calculated
3. Check Analytics → `GET /analytics/summary`
4. Check Saga state → `GET /saga/{correlationId}`

## 📁 Project Structure

```
PaymentEDA/
├── src/
│   ├── PaymentEDA.Contracts/          # Shared events & enums
│   │   ├── Events/PaymentEvents.cs    # All event records
│   │   └── Enums/PaymentEnums.cs
│   ├── PaymentEDA.PaymentService/     # Producer
│   │   ├── Controllers/               # REST API
│   │   ├── Services/                  # Business logic
│   │   ├── Models/                    # Domain models
│   │   ├── Data/                      # EF Core DbContext
│   │   └── Outbox/                    # Outbox Pattern impl
│   ├── PaymentEDA.FraudService/       # Consumer + Idempotency + DLQ
│   ├── PaymentEDA.NotificationService/# Consumer + DLQ
│   ├── PaymentEDA.LedgerService/      # Consumer + Idempotency
│   ├── PaymentEDA.ReconciliationService/
│   ├── PaymentEDA.AnalyticsService/   # Multi-event consumer
│   └── PaymentEDA.Saga/               # State machine orchestrator
├── scripts/
│   └── init-databases.sql             # PostgreSQL DB init
├── docker-compose.yml
└── PaymentEDA.sln
```

## 🧪 Testing the Patterns

### Test Idempotency
Send the same payment twice with the same MessageId — the second should be skipped gracefully.

### Test DLQ
Stop the Fraud Service, send a payment, restart → watch retry attempts, then check `_error` queue in RabbitMQ UI.

### Test Outbox
Stop RabbitMQ, create a payment → it persists to DB. Start RabbitMQ → Outbox publisher picks up and delivers.

## 🛠️ Tech Stack

- **.NET 8** — Web API + Background Services
- **MassTransit 8** — Message bus abstraction, Saga state machine
- **RabbitMQ 3.13** — Message broker
- **PostgreSQL 16** — All databases (one per service)
- **Entity Framework Core 8** — ORM + migrations
- **Serilog** — Structured logging
- **Docker Compose** — Local orchestration

## 📖 Related Blog Post

*Event-Driven Architecture in Payments: Outbox, Saga, DLQ, and Idempotent Consumers in .NET 8*

— [https://mehmetoya.tr/posts/2026/payment-eda/]
