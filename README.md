# Aurelle Perfumes: API (ASP.NET Core)

REST API for the Aurelle Perfumes e-commerce demo: products, search/filters, cart, wishlist,
orders, reviews, JWT login with **email OTP verification (Gmail)**, **Razorpay test-mode payments**
and an admin dashboard.

- Live API: https://YOUR-API.onrender.com/swagger  (free hosting, first request can take ~1 minute to wake up)
- Frontend repo: https://github.com/YOUR-USERNAME/aurelle-perfumes-web

## Tech
ASP.NET Core 10 Web API, Entity Framework Core 10, SQL Server (Azure SQL), JWT, BCrypt, MailKit, Razorpay.

## Run locally
1. Install .NET 10 SDK and SQL Server LocalDB
2. Keep secrets out of git: right-click project in Visual Studio, **Manage User Secrets**, add
   `Email:User`, `Email:AppPassword` (Google App Password), optional `Razorpay:KeyId` / `Razorpay:KeySecret`
3. Press F5 (http profile). Migrations and the demo admin are applied on startup
   (`admin@aurelle.com` / `Admin@123`, development only)
4. Swagger: `/swagger`

## Environment variables (production)
| Name | Purpose |
|---|---|
| `ConnectionStrings__Default` | Azure SQL connection string |
| `Jwt__Key` | Random secret, 32+ characters |
| `Cors__Origins` | Frontend URL(s), comma separated |
| `Admin__Email`, `Admin__Password` | First admin account |
| `Email__User`, `Email__AppPassword`, `Email__FromEmail`, `Email__CcEmail` | Gmail SMTP |
| `Razorpay__KeyId`, `Razorpay__KeySecret` | Optional, test keys |

## Notes
Demo project. Payments run in test/demo mode only.
