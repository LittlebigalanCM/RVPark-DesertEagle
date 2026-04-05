# Desert Eagle RV Park

A full-stack web application for managing an RV park, built as a capstone project for CS 3750 at Weber State University, Spring 2025. The app handles site reservations, customer accounts, and payment processing through Stripe, with role-based access for guests, staff, and administrators.

## Tech Stack

- **Framework:** ASP.NET Core, Entity Framework Core
- **Database:** SQL Server
- **Payments:** Stripe API
- **Cloud:** Microsoft Azure
- **Architecture:** Clean architecture with ApplicationCore, Infrastructure, and Web layers

## Table of Contents

- [Getting Started](#getting-started)
- [User Secrets](#user-secrets)
- [Team Members](#team-members)

## Getting Started

Follow these steps to build and run the project in **Visual Studio 2022**:

### Prerequisites

- Visual Studio 2022 (Community, Professional, or Enterprise)
- The **ASP.NET and web development** workload installed
- .NET SDK matching the project's target framework

### Build & Run Instructions

1. **Clone the repository**
   ```bash
   git clone https://github.com/LittlebigalanCM/RVPark-DesertEagle.git
   ```

2. **Restore NuGet Packages**
   - Visual Studio normally restores packages automatically.
   - If not, right-click the solution in **Solution Explorer** and select **Restore NuGet Packages**.

3. **Set the Startup Project**
   - In **Solution Explorer**, right-click the main web project and choose **Set as Startup Project**.

4. **Configure User Secrets**
   - See the [User Secrets](#user-secrets) section below. The app requires a database connection string, Stripe API keys, and an Azure storage connection to run. A template is provided in `appsettings.example.json`.

5. **Configure the Database**
   - Open the **Package Manager Console** and run:
     ```
     Add-Migration Initial
     Update-Database
     ```

6. **Build the Project**
   - Press **Ctrl+Shift+B**, or go to **Build → Build Solution**.

7. **Run the Application**
   - Press **F5** (Debug) or **Ctrl+F5** (Run without debugging).
   - Your browser will open to the app's launch URL.

## User Secrets

Never commit API keys, connection strings, or other secrets to a repository. This project uses Visual Studio's **User Secrets Manager** to keep sensitive configuration out of source control.

The `appsettings.example.json` file in the `RVPark` project shows the expected structure. Copy it as a reference when setting up your secrets.

To configure your local secrets:

1. In **Solution Explorer**, right-click the `RVPark` project (not `ApplicationCore` or `Infrastructure`)
2. Click **Manage User Secrets** — this opens a `secrets.json` file tied to your local machine
3. Add your connection string, Stripe keys, and Azure storage connection following the structure in `appsettings.example.json`

Visual Studio applies configuration files in this order, with later files taking priority:

- `appsettings.json`
- `appsettings.{Environment}.json` (e.g. `appsettings.Development.json`)
- `secrets.json`

This means your local `secrets.json` will override anything in `appsettings.json` without touching the committed files.

## Team Members

Built by Team Bitstorm for CS 3750 at Weber State University:

- Abigayle ([AbigayleDelValle](https://github.com/AbigayleDelValle))
- Arla ([arlacontreras](https://github.com/arlacontreras))
- Bailey ([Nonstopgoaty](https://github.com/Nonstopgoaty))
- Christian ([LittlebigalanCM](https://github.com/LittlebigalanCM))
- Ian ([Inicky1](https://github.com/Inicky1))
- Isaac ([IamGreenie](https://github.com/IamGreenie))
- Kawther ([KawtherSH](https://github.com/KawtherSH))
- Matthew ([MetroidSlayer](https://github.com/MetroidSlayer))
- Quintin ([quintonio1000](https://github.com/quintonio1000))
- Yugene ([edu24ylee](https://github.com/edu24ylee))
