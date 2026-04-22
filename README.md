# 📂 Configurable Disk Cleanup Service for .NET 8

**Configurable Disk Cleanup** is an automated, configurable file lifecycle management tool built as a **.NET 8 Worker Service**. 

Instead of dealing with manual server maintenance or generic junk cleaners, this service runs silently in the background, targeting your specific folders and file types based on your own business rules. When files become obsolete (like old logs, temporary CSVs, or expired reports), the service deletes them to free up disk space and sends a beautiful HTML summary report straight to your email.

## ✨ Features
* **Desirable Targeting:** You decide exactly which directories to monitor and which file extensions to delete.
* **Dry Run Mode:** Safely test your setup without actually deleting any files. The logs will simply tell you what *would* have been deleted.
* **Automated Email Reports:** Whenever files are cleared, you’ll receive an email detailing the execution time, files removed, and total storage freed.
* **Set and Forget:** Runs on a continuous loop or can be scheduled via Windows Task Scheduler.

---

## ⚙️ How to Configure

Before running the app, you need to configure your settings. Open the `appsettings.json` file and adjust the values to fit your needs:


### Configuration Breakdown:
* `DirectoryPaths`: The full paths to the folders you want the service to monitor.
* `FileExtensions`: Only files matching these extensions will be deleted. Leave empty `[]` to target *all* files in the directory.
* `DryMode`: **Keep this set to `true` on your first run!** It prevents files from actually being deleted so you can verify your configuration. Change to `false` when you are ready to go live.
* `IntervalInSeconds`: How often the background loop runs (e.g., `3600` = runs every hour).
* `EmailSettings`: Your standard SMTP credentials inside to send the automated cleanup report.

---

## 🚀 How to Run and Test Locally

To try this out on your own Windows PC:

1. **Install Prerequisites:** Ensure you have the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed.
2. **Clone the Repo:** Download or clone this repository to your machine.
3. **Configure:** Update the `appsettings.json` file with some test folders (make sure `DryMode` is `true`).
4. **Run the App:** Open a terminal in the project folder and run:
5. **Check the Logs:** Watch your terminal to see the service scan your folders and announce what it *would* delete. If valid files were found, check your email for the test report!
6. **Test on Unimportant Files:** Create some dummy files in the target directories that match your configured extensions. Run the app again to see them listed in the logs and included in the email report.

---

## 🌍 How to Deploy on a Server / Another Machine

If you want this to run permanently in the background on your PC or Server, the easiest way is to use the **Windows Task Scheduler**.


### Step 1: Configure the Target Machine
On the machine where the app will run:
1. Open the published folder.
2. Edit the `appsettings.json` with the required directories and email settings.
3. Change `"DryMode": false`.

### Step 2: Publish the App

1. Run the following command to package the app into a neat folder:

```
dotnet publish -c Release -o "C:\Services\FileVaultCleaner"
```

2. Use Command Prompt or PowerShell to setup the service inside your Windows by running this command:

```
sc create FileVaultCleaner binPath= "C:\YourPath\Configurable_Cleanup.exe" start= auto
```

> Note: Replace `C:\YourPath\Configurable_Cleanup.exe` with the actual path to your published executable.

> Recommended to update your services.msc to set the startup type to "Manual" and start the service manually to ensure it works before setting it to "Automatic".

### Step 3: Set up Windows Task Scheduler
If you want the service to run on a predictable schedule (e.g., every night at 2:00 AM) without needing a terminal open:

1. Open **Task Scheduler** on Windows.
2. Click **Create Task** on the right.
3. Give it a name (e.g., `FileVault Cleaner`). Check the box for **"Run whether user is logged on or not"**.
4. Go to the **Triggers** tab, click **New**, and set your desired schedule.
5. Go to the **Actions** tab, click **New**, select **Start a program**, and browse to `Configurable_Cleanup.exe` inside your publish folder.
6. Click **OK**. The service will now run automatically perfectly in the background!

