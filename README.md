# Local Markdown Archive and Full‑Text Search for Copilot Chat

A lightweight toolchain for turning the **text‑only Copilot CSV export** (downloaded from **Microsoft Accounts → Privacy → Copilot apps → Export all activity history**) into a **local Markdown archive** with **full‑text search** and **HTML preview**.

This project consists of two components:

- **CopilotArchiveSync** -- processes the CSV and maintains a local Markdown archive  
- **CopilotArchiveSearch** -- WPF application for fast, full‑text search and preview


## 1. Downloading Your Copilot Chat History

Microsoft does not provide a Copilot export feature. Instead, you must manually download the **privacy recovery CSV**:

1. Visit: https://account.microsoft.com/privacy/
2. Navigate to: **Your Copilot activity history → Copilot apps**
3. Click: **Export all activity history**
4. Save the resulting `.csv` file (text‑only, no images, no attachments)

> Only the **Copilot apps** CSV is supported at the moment.  
> The Microsoft 365 and Windows apps CSVs use different formats.


## 2. Preparing Your Local Archive Folder

Choose or create a folder where your Markdown archive will live.

CopilotArchiveSync can use:

- the **current working directory**, or  
- a path specified in **CopilotArchiveSync.config**, or  
- a path passed as a **command‑line argument**

To enable search, allow Windows Search Indexer to index `.md` files:

1. Open **Indexing Options** in Control Panel
2. Add your archive folder
3. Ensure `.md` is included in indexed file types


## 3. How CopilotArchiveSync Handles Your CSV File

CopilotArchiveSync is designed to make CSV management effortless. Here’s how it works, in simple terms:

### Automatic detection
The tool automatically finds the **newest Copilot apps CSV** in your **Downloads** folder.

### Keeping a "current CSV"
Your archive folder contains one "current CSV" that the tool uses to generate Markdown files.

When you run CopilotArchiveSync:

- If the archive folder has **no CSV yet**, the newest downloaded CSV is copied there.
- If the archive folder **already has a CSV**, and the newly downloaded one is **newer**, the tool:
  - asks whether you want to replace the old file  
  - if you choose **Yes**, the old CSV is saved as a **date‑tagged backup**  
  - then the new CSV is copied in its place

Backups are kept automatically.  
You never lose data.

### If the archive CSV is newer
If your archive CSV is newer than the downloaded one, the tool keeps your existing file.

### Deleted conversations are handled correctly
If you delete conversations in the Copilot UI, future CSV downloads will not contain them.  
CopilotArchiveSync will:

- **keep the old Markdown files**  
- **add or update only the conversations that still exist**

Your archive remains complete.

### Renamed conversations are handled correctly
If you rename a conversation in Copilot:

- the next CSV download will contain the new name  
- CopilotArchiveSync will detect the rename  
- the corresponding Markdown file will be updated

Your archive stays consistent with your Copilot naming.

### After CSV handling
The tool:

- reads all conversations  
- creates new Markdown files  
- updates existing ones  
- stores backups of replaced Markdown files  
- keeps your archive folder fully in sync

The sync is **incremental** -- only new or changed conversations are processed.


## 4. Running CopilotArchiveSync

Basic usage:

```
CopilotArchiveSync.exe
```

Specify a custom archive path:

```
CopilotArchiveSync.exe c:\path\to\archive
```

Or configure it in:

```
CopilotArchiveSync.config
```


## 5. Searching with CopilotArchiveSearch (WPF)

CopilotArchiveSearch is a simple, fast, dark‑themed WPF application.

Configure the archive folder in:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
    <appSettings>
        <add key="ArchiveFolderPath" value="c:\Data\Copilot\Archive\"/>
    </appSettings>
</configuration>
```

### **Features**

- **Instant search‑as‑you‑type**  
- **List of matching conversations**  
- **HTML preview via WebView2**  
- **CTRL+F search inside the preview**  
- **Dark color scheme similar to VS Code**


## 6. Folder Structure

Your archive will look like:

```
copilot-activity-history/
    2024-11-05_Chat_001.md
    2024-11-05_Chat_002.md
    2024-11-06_Chat_001.md
    ...
copilot-activity-history.bak/
    2024-11-05_Chat_001.md
    ...
copilot-activity-history.csv
copilot-activity-history.2024-11-05.csv
```

Each file contains:

- header with timestamps and message count
- conversation text  
- stable formatting  
- no images (CSV is text‑only)


## 7. Limitations

- Only **Copilot apps** CSV is supported  
- CSV contains **text only**  
- No images, no attachments, no pages  
- Other CSV formats (M365, Windows apps) may be added later


## 8. License

Apache License 2.0