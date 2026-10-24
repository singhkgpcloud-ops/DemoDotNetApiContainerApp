# .NET 8 Production Demo API (`DemoDotNetApiContainerApp`)

Production-ready .NET Core 8 Web API with interactive **Swagger UI** enabled in production, containerized for Windows Server 2019 (`1809`) and published to Docker Hub.

- **Live Swagger UI**: [https://net.sujeetkumarsingh.com/swagger/index.html](https://net.sujeetkumarsingh.com/swagger/index.html)
- **Docker Hub Image**: `singhkgp/net-demo-api:latest` (`v1.0.0`)

## Endpoints
- `GET /api/info` — API metadata, runtime details, and uptime
- `GET /api/health` — Health check endpoint
- `GET /api/products` — List all products (with optional `?category=` and `?search=` filters)
- `GET /api/products/{id}` — Get product by ID
- `POST /api/products` — Create a new product
- `PUT /api/products/{id}` — Update a product
- `DELETE /api/products/{id}` — Delete a product
- `GET /api/weather` — Demo weather forecast generator
