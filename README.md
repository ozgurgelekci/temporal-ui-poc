# temporal-poc

Temporal UI üzerinde workflow orkestrasyonunu gözlemlemek için hazırlanmış bir .NET 10 POC. Domain kasıtlı olarak genel tutuldu (doküman pipeline'ı) — gerçek projelerden kod/isimlendirme alınmadı.

İki workflow var:

- **DocumentProcessingWorkflow** — validate → parse → enrich, ardından imzalamayı **child workflow** olarak tetikler.
- **DocumentSigningWorkflow** — hash payload → apply signature → verify → persist.

## Mimari

```
Starter ──(StartWorkflow)──▶ Temporal Server ──(task queue)──▶ Worker
                                    │                              │
                                    │                              ├─ DocumentProcessingWorkflow
                                    │                              │    ├─ ValidateAsync      (activity)
                                    │                              │    ├─ ParseAsync         (activity)
                                    │                              │    ├─ EnrichAsync        (activity)
                                    │                              │    └─ ExecuteChildWorkflowAsync
                                    │                              │          │
                                    │                              │          └─ DocumentSigningWorkflow
                                    │                              │                ├─ HashPayloadAsync
                                    │                              │                ├─ ApplySignatureAsync
                                    │                              │                ├─ VerifySignatureAsync
                                    │                              │                └─ PersistSignedDocumentAsync
                                    ▼
                               Postgres (event history)
                                    ▲
                                    │
                               Temporal UI (http://localhost:8080)
```

- **Workflow** → deterministik orkestrasyon kodu. Doğrudan I/O yapmaz; `ExecuteActivityAsync` / `ExecuteChildWorkflowAsync` ile Temporal'a "şunu çalıştır" der.
- **Activity** → asıl işi yapan (validate, hash, imza üretme vb.) kod. Retry policy burada devrededir.
- **Task queue** → worker'ın dinlediği kuyruk (`document-pipeline`). Starter workflow'u bu kuyruğa düşürür, worker çeker.
- **Event history** → her step'in input/output/exception'ı Postgres'te saklanır. Worker crash olsa bile kaldığı adımdan devam eder; tamamlanmış activity'ler tekrar çalıştırılmaz.

## Layout

```
temporal-poc/
├── docker-compose.yml            # Postgres + Temporal server + UI
├── TemporalPoc.slnx
└── src/
    ├── Workflows/                # Workflow & activity tanımları (classlib)
    │   ├── Models.cs
    │   ├── ProcessingActivities.cs
    │   ├── SigningActivities.cs
    │   ├── DocumentProcessingWorkflow.cs
    │   └── DocumentSigningWorkflow.cs
    ├── Worker/                   # Workflow & activity host (long-running)
    └── Starter/                  # Tek bir workflow tetikler, sonucu bekler
```

## Çalıştırma

Tüm komutlar proje kökünden çalıştırılır:

```bash
cd "C:\özgür\temporal-poc"
```

### 1. Temporal + UI'ı ayağa kaldır

```bash
docker compose up -d
```

- Temporal gRPC: `localhost:7233`
- Temporal UI:   http://localhost:8080
- Postgres:      `localhost:5432` (temporal/temporal)

İlk açılışta `auto-setup` image'ı şemayı kurar; UI'ın hazır olması ~15-30 sn sürebilir.

### 2. Build

```bash
dotnet build
```

### 3. Worker'ı başlat (açık bırak)

```bash
dotnet run --project src/Worker
```

Worker `document-pipeline` task queue'suna register olur ve iş bekler.

### 4. Başka bir shell'de workflow tetikle

```bash
dotnet run --project src/Starter
```

Starter:
1. Rastgele bir `DocumentId` üretir.
2. `DocumentProcessingWorkflow`'u başlatır.
3. `WorkflowId` ve `RunId` yazar.
4. Sonucu bekler, imzayı ekrana basar.

## UI'da ne görünür

http://localhost:8080 → `default` namespace:

- `processing-doc-xxx` (parent) ve `signing-doc-xxx` (child) workflow'ları liste halinde.
- Bir workflow'u aç → **Event History** sekmesi: her activity için `ScheduledTask`, `ActivityTaskStarted`, `ActivityTaskCompleted` event'leri; timing, input/output payload'ları, retry denemeleri.
- Parent workflow'da **Child Workflow Started** event'i child'a link verir.
- **Pending Activities** paneli: anlık olarak hangi activity çalışıyor, kaçıncı denemede.

## Hataya dayanıklılık denemesi

- Worker çalışırken Starter tetikle, workflow ortasında worker'ı `Ctrl+C` ile kapat. Workflow UI'da "running" kalır.
- Worker'ı tekrar `dotnet run` ile başlat → Temporal history'den kaldığı yeri okur, activity'leri tekrar çalıştırmadan devam eder.
- Bir activity içinde `throw` eklersen retry policy'yi (3 deneme, exponential backoff) UI'da canlı görebilirsin.

## Konfigürasyon

| Değişken           | Varsayılan        | Açıklama                     |
| ------------------ | ----------------- | ---------------------------- |
| `TEMPORAL_ADDRESS` | `localhost:7233`  | Temporal server gRPC adresi  |

## Teknoloji

- .NET 10
- `Temporalio` 1.22 (resmi .NET SDK)
- `Temporalio.Extensions.Hosting` (DI + IHostedService entegrasyonu)
- `temporalio/auto-setup:1.26` + `temporalio/ui:2.31.2`
- Postgres 16 (event history store)

## POC notları

Yapılanlar:

- .NET 10 solution kuruldu (`Workflows` classlib, `Worker`, `Starter`).
- `Temporalio` 1.22 + `Temporalio.Extensions.Hosting` paketleri eklendi.
- Postgres 16, `temporalio/auto-setup:1.26` ve `temporalio/ui:2.31.2` için compose dosyası yazıldı.
- İki workflow kuruldu: processing workflow'u signing workflow'unu child olarak tetikliyor. Retry policy (3 deneme, exponential backoff) activity options içinde.
- Stack `podman compose up -d` ile ayağa kaldırıldı. `DYNAMIC_CONFIG_FILE_PATH` env'i auto-setup image'ında karşılığı olmadığı için compose'dan çıkarıldı.

Koşu sonucu:

- `doc-7d6a0781` için 7 activity sırayla çalıştı, toplam süre ~2 saniye.
- Validate → Parse → Enrich (processing) → Hash → ApplySignature → VerifySignature → PersistSigned (signing).
- Starter `Status: Completed` döndü, signature üretildi.
- UI'da parent + child workflow, event history ve payload'lar doğrulandı.
