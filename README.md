🛡️ Secure Todo API with JWT, EF Core, Swagger, and Logging

A secure and minimal Web API built with ASP.NET Core for managing todos. This project uses JWT-based authentication, EF Core with SQLite, endpoint filters, and includes real-time request logging for educational and Red Team simulation purposes.


🚀 Features

- ✅ Minimal API structure with `Program.cs` only
- 🔐 JWT-based authentication for protected routes
- 🧑‍💻 `/signup` and `/login` endpoints
- 🔒 `/me` endpoint secured with `[RequireAuthorization]`
- 📂 CRUD operations on `/todos` with EF Core + SQLite
- 🧠 Input validation using `AddEndpointFilter`
- 🌐 Swagger UI with support for JWT Bearer tokens
- 🪪 Custom middleware to **log all incoming POST request data**
- 🔁 Legacy route redirection from `/tasks` → `/todos`



📦 Technologies Used

- ASP.NET Core 8 Minimal APIs
- Entity Framework Core (EF Core)
- SQLite
- JWT Authentication
- Swagger / Swashbuckle
- Middleware for request sniffing & logging
- Visual Studio Code

---

 🎯 Red Team Simulation Features

This project also includes offensive security simulation features to help understand how attackers might interact with insecure APIs, making it a valuable Red Teaming practice tool:

- 🔍 **Simulated JWT abuse** scenarios (e.g., missing `unique_name` claim can result in bypass)
- 🪪 **Authorization bypass simulation** with incorrect token claims
- 🐛 **401 Unauthorized debugging logic** for endpoint protection failures
- 📜 **Manual HTTP testing file (`webpage1.http`)** for simulating crafted requests
- 📘 **Logging sensitive activity** to `logs.txt` for post-attack analysis
- 🔐 **Shows importance of proper claim validation in JWT token-based systems**

> This makes the project not only useful for .NET backend development, but also a **training lab for learning API security**, JWT misconfigurations, and Red Team strategy.

---

🔧 How to Run

1. Clone the repository:

   bash
         git clone https://github.com/dheerajbalan/SecureTodoAPI.git
         cd SecureTodoAPI
