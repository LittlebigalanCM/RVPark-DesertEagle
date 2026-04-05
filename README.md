# Desert Eagle RV Park Web Application

## Table of Contents

- [Getting Started](#getting-started)
- [User Secrets](#user-secrets)
- [Team Members](#team-members)
- [External Links](#external-links)

## Getting Started

Follow these steps to build and run the project in **Visual Studio 2022**:

### **Prerequisites**
- Visual Studio 2022 (Community, Professional, or Enterprise)
- The **ASP.NET and web development** workload installed
- .NET SDK matching the project’s target framework

### **Build & Run Instructions**
1. **Clone the repository**  
  ```bash
  git clone <your-repo-url>
  ```

2. **Restore NuGet Packages**
- Visual Studio normally restores packages automatically.
- If not, right-click the solution in **Solution Explorer** and select **Restore NuGet Packages**.

3. **Set the Startup Project**
- In **Solution Explorer**, right-click the main web project and choose **Set as Startup Project**.

4. **Configure the Database (if required)**
- Open the **Package Manager Console** and run:
  ```
  Add-Migration Initial
  Update-Database
  ```

5. **Build the Project**
- Press **Ctrl+Shift+B**, or go to **Build → Build Solution**.

6. **Run the Application**
- Press **F5** (Debug) or **Ctrl+F5** (Run without debugging).
- Your browser will open to the app’s launch URL.

## User Secrets

In general, it is a very bad idea to publish secrets such as keys and connection strings
to repositories as bad actors could pick up this information and access
interfaces, tools, and data that they shouldn't be able to.
This information should usually be stored in a place where it cannot be
accessed by Git and accidentally pushed to the repository. 
Thankfully, Microsoft's Visual Studio team thought of a solution to this issue
known as the User Secrets Manager.
For this project, the User Secrets Manager should be created for the `RVPark`
.NET project, rather than `ApplicationCore` or `Infrastructure`.
Follow the steps below to access the manager.

1. Right click on `RVPark`
2. Click on `Manage User Secrets`. 
  This will open a new file called `secrets.json`.
  See the [reference image](https://discord.com/channels/1420070994038755572/1420070994755850242/1444252095732650046) on Discord for more information.
3. Enter your secrets into `appsettings.json`.
  A template for this information is [available on Discord](https://discord.com/channels/1420070994038755572/1420070994755850242/1444255283194101982).

And it's as simple as that. 
By default, Visual Studio has an order of which it applies the following files:
- `appsettings.json`
- `appsettings.{Environment}.json` such as `appsettings.Development.json`
- `secrets.json`

This means that `secrets.json` will override entries in `appsettings.json`.

## Team Members

Bitstorm Team Members:

- Abigayle ([AbigayleDelValle](https://github.com/AbigayleDelValle))
- Arla ([arlacontreras](https://github.com/arlacontreras))
- Bailey ([Nonstopgoaty](https://github.com/Nonstopgoaty))
- Christian ([cmartinweber](https://github.com/cmartinweber))
- Ian ([Inicky1](https://github.com/Inicky1))
- Isaac ([IamGreenie](https://github.com/IamGreenie))
- Kawther ([KawtherSH](https://github.com/KawtherSH))
- Matthew ([MetroidSlayer](https://github.com/MetroidSlayer))
- Quintin ([quintonio1000](https://github.com/quintonio1000))
- Yugene ([edu24ylee](https://github.com/edu24ylee))

## External Links

Most of these links require a sign-in and are invite ONLY.

-  [Stripe Dashboard](https://dashboard.stripe.com/acct_1RDM5I4D8Vyglmjd/test/dashboard)
- [Discord Server](https://discord.com/channels/1420070994038755572/1420070994755850241)
