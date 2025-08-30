# PageManager – Quick Start (Docker Compose)

## Stack

* **API + Swagger**: [http://localhost:8080/swagger](http://localhost:8080/swagger)
* **PostgreSQL**: `db:5432` (user: `app` / pass: `appsecret` / db: `pagemanager`)
* **pgAdmin**: [http://localhost:5050](http://localhost:5050) (login: `admin@admin.com` / `admin`)
* **Jaeger (traces)**: [http://localhost:16686](http://localhost:16686)
* **Elasticsearch**: [http://localhost:9200](http://localhost:9200)
* **Kibana**: [http://localhost:5601](http://localhost:5601)

On startup, EF Core migrations are applied automatically and seed data is loaded.

---

## Run

```bash
docker compose up --build
```

On first run, PostgreSQL is initialized, EF creates tables, and the seeder loads **24 sample pages** with drafts and published records. Kibana, Jaeger, and pgAdmin UIs are available for inspection.

---

## Seed Data

* **Demo Site Id**: `5abf2c3f-4fb9-4a19-a806-178139b73651`
* **Pages (slugs)**:
  `home`, `about`, `contact`, `products`, `services`, `blog`, `news`, `faq`,
  `careers`, `privacy`, `terms`, `pricing`, `features`, `integrations`, `partners`, `press`,
  `support`, `status`, `docs`, `api`, `changelog`, `roadmap`, `refunds`, `gdpr`
* Each page has **2 drafts** (`DraftNumber = 1, 2`)
* **Publishing rule**:

    * Even index pages → Draft #1 published
    * Odd index pages → Draft #2 published
    * Every 6th page → no published draft
* Example:

    * `home` → Draft #1 published
    * `about` → Draft #2 published
    * `faq` → no published draft, archived

---

## pgAdmin Connection

1. Open [http://localhost:5050](http://localhost:5050) → `admin@admin.com` / `admin`
2. Add New Server:

    * **Name**: PageManager DB
    * **Host**: `db`
    * **Port**: `5432`
    * **Username**: `app`
    * **Password**: `appsecret`
3. Tables: `Pages`, `PageDrafts`, `PagePublished`

---

## Tracing

* .NET app exports traces via OpenTelemetry OTLP to Jaeger.
* Jaeger UI: [http://localhost:16686](http://localhost:16686) → select Service: `PagesApi`.
* Custom ActivitySource: `Feature.Pages.ArchiveAndMayPublish`

    * Main span: `Handle.ArchiveAndMayPublish`
    * Child span: `Handle.HandlePublishDraftAsync`
* Span tags: `siteId`, `slug`, `publishDraft`, `pageId`, `draftNumber`
* On errors, spans are marked `ERROR`; exception details appear in Attributes.

---

## Elasticsearch & Kibana

* Elasticsearch: [http://localhost:9200](http://localhost:9200)
* Kibana: [http://localhost:5601](http://localhost:5601)
* Docker Compose creates a dummy index `pagesapi-logs` so you can immediately see it in Kibana Discover.

---

## API – cURL Examples

### Get published page

```bash
curl -X GET "http://localhost:8080/api/v1/sites/5abf2c3f-4fb9-4a19-a806-178139b73651/pages/home/published" \
  -H "accept: application/json"
```

Example response:

```json
{
  "pageId": "8c1b8d1b-bc2e-4a60-9f51-b6641b7e7d11",
  "draftId": "d2c6aa9e-d4a8-43b6-8841-1d72fbe91f2f",
  "publishedUtc": "2025-08-29T12:34:56Z"
}
```

### Archive only

```bash
curl -X DELETE "http://localhost:8080/api/v1/sites/5abf2c3f-4fb9-4a19-a806-178139b73651/pages/faq" \
  -H "accept: */*"
```

Response: **204 No Content** → `IsArchived = true`

### Archive + publish draft

```bash
curl -X DELETE "http://localhost:8080/api/v1/sites/5abf2c3f-4fb9-4a19-a806-178139b73651/pages/news?publishDraft=2" \
  -H "accept: */*"
```

Response: **204 No Content** → Draft #2 becomes published.

### Invalid draft

```bash
curl -X DELETE "http://localhost:8080/api/v1/sites/5abf2c3f-4fb9-4a19-a806-178139b73651/pages/home?publishDraft=99" \
  -H "accept: */*"
```

Response: **400 Bad Request**

### Page not found

```bash
curl -X DELETE "http://localhost:8080/api/v1/sites/5abf2c3f-4fb9-4a19-a806-178139b73651/pages/not-exist" \
  -H "accept: */*"
```

Response: **404 Not Found**

---

## Troubleshooting

* Swagger not loading HTTPS: API runs on HTTP (8080). `UseHttpsRedirection()` is disabled.
* “relation … does not exist”: migrations not applied. Restart with `docker compose down -v && docker compose up --build`.
* No spans in Jaeger: ensure `AddSource("Feature.Pages.ArchiveAndMayPublish")` is present and requests hit the API.
* pgAdmin connection errors: Host `db`, user `app`, password `appsecret`, port `5432`.
