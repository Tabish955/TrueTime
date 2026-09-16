<p align="center">
  <img src="assets/banner.png" alt="TrueTime Professional Banner" width="100%" />
</p>

<p align="center">
  <img src="assets/logo.png" alt="TrueTime Professional Logo" width="96" height="96" />
</p>

<h1 align="center">TrueTime Professional</h1>

<p align="center">
  <strong>Next-Generation Multi-Server NTP Precision Synchronization Suite & Glassmorphic Dashboard for Windows</strong>
</p>

<p align="center">
  <a href="https://github.com/Tabish955/TrueTime/releases"><img src="https://img.shields.io/badge/release-v1.0.0-00D2FF.svg?style=for-the-badge&logo=github" alt="Release" /></a>
  <a href="https://github.com/Tabish955/TrueTime"><img src="https://img.shields.io/badge/platform-Windows%20x64-0078D6.svg?style=for-the-badge&logo=windows" alt="Platform" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8.0%20LTS-512BD4.svg?style=for-the-badge&logo=dotnet" alt="Runtime" /></a>
  <a href="https://www.ntp.org/"><img src="https://img.shields.io/badge/protocol-RFC%205905%20NTP-059669.svg?style=for-the-badge" alt="Protocol" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-059669.svg?style=for-the-badge" alt="License" /></a>
  <a href="https://www.virustotal.com/"><img src="https://img.shields.io/badge/security-VirusTotal%20Clean-10B981.svg?style=for-the-badge&logo=virustotal" alt="Security" /></a>
</p>

**TrueTime Professional** is an ultra-reliable, microsecond-accurate Network Time Protocol (NTP/SNTP) synchronization suite for modern Windows environments. Engineered as a high-performance replacement for legacy utilities like NetTime, TrueTime combines a resilient Windows background service with a sleek dark obsidian glassmorphic system tray dashboard featuring real-time brand logos for authoritative time providers.

[Download Latest Release](https://github.com/Tabish955/TrueTime/releases) • [Features](#-key-features) • [Comparison](#-comparison-with-alternatives) • [Architecture](#-architecture) • [Deployment](#-enterprise-deployment--silent-installation)

---

## 🌟 Key Features

### 🎨 Dark Obsidian Glassmorphic Dashboard & Branded NTP Providers
- **Ultra-Modern Dark Obsidian Theme**: Engineered with deep obsidian panels (`#0B1222` / `#111B33`), glowing cyan accents (`#00D2FF`), and native Windows 10/11 DWM Immersive Dark Mode title bars.
- **Authoritative Provider Logos**: Integrated high-resolution brand logos for major global NTP infrastructure:
  - 🌐 **Cloudflare** (`time.cloudflare.com`)
  - 🔍 **Google Public NTP** (`time.google.com`)
  - 👥 **Meta / Facebook** (`time.facebook.com`)
  - 🍎 **Apple Time** (`time.apple.com`)
  - 🪟 **Microsoft Windows Time** (`time.windows.com`)
  - ⚛️ **NIST Boulder Atomic Clocks** (`time.nist.gov`)
  - 🏊 **NTP Pool Project** (`pool.ntp.org`)
- **Dual Precision Metric Cards**: Real-time system clock monitor alongside high-precision synchronization offset indicators (`Accurate (+0.0 ms)`).
- **Color-Coded Status Badges**: Real-time visual status pills (`✔ In Sync`, `✖ No Internet`, `⚡ Drift Detected`, `🔄 Syncing...`, `⚠ Service Offline`).

### 💎 Next-Generation Diagnostics & Precision
- **Motherboard Crystal Oscillator Drift (PPM)**: Calculates your motherboard hardware Real-Time Clock (RTC) crystal frequency deviation in **Parts Per Million (PPM)** between sync intervals.
- **Statistical Pool Jitter Measurement**: Computes the true Root-Mean-Square (RMS) dispersion and standard deviation across all responding NTP stratum servers in real-time.
- **Concurrent Multi-Server Polling**: Queries your entire authoritative NTP pool simultaneously over asynchronous UDP 123 sockets, dynamically locking onto the lowest-latency, lowest-dispersion time source.
- **Integrated Local LAN NTP Daemon**: Acts as an authoritative stratum time server for your entire local subnet, industrial equipment, PLC controllers, and virtual machines without requiring internet access.
- **Active Network-Awareness with Live Countdown**: Actively monitors Windows network adapter states (`NetworkChange.NetworkAvailabilityChanged`). If internet connectivity drops, TrueTime immediately reflects offline status—eliminating deceptive false "In Sync" readings—and counts down live second-by-second until the next automated retry.
- **Single-Instance Window Activation**: Clicking the desktop shortcut or tray icon activates and brings the existing dashboard window to the foreground via UIPI-safe Windows messaging (`WM_SHOW_TRUETIME`).
- **Audit Logging & One-Click CSV Export**: Comprehensive event history tracking sync timestamps, offsets, latencies, and adjust actions with instant CSV export for regulatory and compliance audits.

---

## 📊 Comparison with Alternatives

| Capability | TrueTime Professional | Legacy NetTime | Windows w32time |
| :--- | :---: | :---: | :---: |
| **Modern Windows 11 Fluent Dashboard** | ✅ **Yes** (Slate Theme) | ❌ No (Win95 Dialog) | ❌ No GUI |
| **Concurrent UDP 123 Multi-Server Polling** | ✅ **Yes** (Parallel) | ❌ Sequential Only | ❌ Single Server |
| **Hardware Quartz RTC Drift in PPM** | ✅ **Yes** | ❌ No | ❌ No |
| **Authoritative Pool Jitter Metrics (RMS)** | ✅ **Yes** | ❌ No | ❌ No |
| **Integrated Local LAN NTP Daemon Server** | ✅ **Yes** (Built-in UDP 123) | ⚠️ Partial | ⚠️ Registry Hack |
| **Intelligent Offline Detection & Live Countdown** | ✅ **Yes** (1s Precision) | ❌ No | ❌ No |
| **Single-Instance Foreground Pop-up** | ✅ **Yes** (UIPI Safe) | ⚠️ Basic | N/A |
| **Clean WiX 5 Enterprise MSI Package** | ✅ **Yes** (GPO / Intune Ready) | ❌ No | Built-in |
| **64-bit Native .NET 8 Architecture** | ✅ **Yes** | ❌ 32-bit Legacy Delphi | Native C++ |
| **Audit Log with One-Click CSV Export** | ✅ **Yes** | ❌ No | ⚠️ Event Viewer |

---

## 🏛️ Architecture

TrueTime is engineered as a decoupled, fault-tolerant two-tier architecture:

```
                  ┌────────────────────────────────────────────────────────┐
                  │ Authoritative NTP Servers (pool.ntp.org, cloudflare...)│
                  └───────────────────────────▲────────────────────────────┘
                                              │ UDP 123 (Parallel)
                                              ▼
  ┌──────────────────────────────────────────────────────────────────────────────────────┐
  │ TrueTimeService.exe (Background Windows Service)                                     │
  │ • Runs under LocalSystem with SE_SYSTEMTIME_NAME privilege                           │
  │ • Concurrent SNTP query coordinator & RTC Crystal PPM drift analyzer                 │
  │ • Integrated LAN NTP broadcast daemon (UDP port 123)                                 │
  │ • IPC Named Pipe Server (\\.\pipe\TrueTimePipe with SDDL security)                    │
  │ • Shared persistent configuration: %ProgramData%\TrueTime\settings.json            │
  └───────────────────────────────────────────▲──────────────────────────────────────────┘
                                              │ Secure Win32 Named Pipe
                                              ▼
  ┌──────────────────────────────────────────────────────────────────────────────────────┐
  │ TrueTime.Tray.exe (Desktop System Tray Dashboard)                                    │
  │ • Unprivileged interactive user session application                                  │
  │ • Windows 11 Fluent design with custom double-buffered GDI+ rendering                │
  │ • Live second-by-second countdown & network availability event listener              │
  │ • Inter-process window activation via RegisterWindowMessage (WM_SHOW_TRUETIME)       │
  └──────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 📦 Downloads & Releases

Release binaries are published under [GitHub Releases](https://github.com/Tabish955/TrueTime/releases).

| Package | Format | Architecture | Size | Description |
| :--- | :---: | :---: | :---: | :--- |
| **[TrueTimeSetup.msi](https://github.com/Tabish955/TrueTime/releases/latest/download/TrueTimeSetup.msi)** | MSI | Windows x64 | ~1.4 MB | Native WiX 5 Windows Installer. Automatically registers the Windows Service, configures auto-start, and installs shortcuts. Ideal for enterprise GPO / Intune deployments. |
| **[TrueTimeSetup.exe](https://github.com/Tabish955/TrueTime/releases/latest/download/TrueTimeSetup.exe)** | EXE | Windows x64 | ~3.0 MB | Interactive Inno Setup wizard. Automatically starts service and launches tray application immediately upon installation completion. |
| **[TrueTime-v1.0.0-Portable.zip](https://github.com/Tabish955/TrueTime/releases/latest/download/TrueTime-v1.0.0-Portable.zip)** | ZIP | Windows x64 | ~1.0 MB | Standalone portable archive. Includes one-click `install.bat` and `uninstall.bat` scripts for flash drives and air-gapped systems. |

### 🔒 Cryptographic Verification (SHA-256 Checksums)

```text
A58B8BF78376F33F0BC94E71F35BB7DE0792C4FC55F845B90867DC7DE4E19FAA  TrueTimeSetup.msi
EC7A3598A5314D3D574E1CDD87801646CC6244717094378C9121ECDCCDF363FF  TrueTimeSetup.exe
47A9C42905D6D64EDD0B33AAE130A85630B3799969946739353323933190DBD7  TrueTime-v1.0.0-Portable.zip
```

---

## 🚀 Enterprise Deployment & Silent Installation

### Active Directory / Microsoft Intune / SCCM (MSI)
```powershell
# Unattended silent installation with logging
msiexec.exe /i TrueTimeSetup.msi /qn /norestart /l*v C:\Windows\Temp\TrueTime_Install.log

# Silent uninstallation
msiexec.exe /x TrueTimeSetup.msi /qn /norestart
```

### Inno Setup Installer (.exe)
```powershell
# Completely silent unattended install
TrueTimeSetup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-

# Silent uninstallation
"C:\Program Files\TrueTime\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

---

## 🛠️ Building from Source

### Prerequisites
- **Windows 10 / 11** or **Windows Server 2016+** (x64)
- **.NET 8.0 SDK** (v8.0.100 or newer)
- **WiX Toolset v5** (`dotnet tool install --global wix`)
- **Inno Setup 6** (for compiling `SetupScript.iss`)

### Build Steps

1. **Clone the repository:**
   ```bash
   git clone https://github.com/Tabish955/TrueTime.git
   cd TrueTime
   ```

2. **Compile the solution:**
   ```powershell
   dotnet build NetTimeService/NetTimeService.sln -c Release
   ```

3. **Publish clean release binaries:**
   ```powershell
   .\publish.bat
   ```

4. **Build Installers:**
   ```powershell
   # 1. Compile WiX 5 MSI Package
   wix build -arch x64 Package.wxs -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o Output\TrueTimeSetup.msi

   # 2. Compile Inno Setup Executable
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" SetupScript.iss

   # 3. Create Portable Zip
   Compress-Archive -Path publish\* -DestinationPath Output\TrueTime-v1.0.0-Portable.zip -Force
   ```

---

## 🛡️ Security & Integrity

- **Zero Third-Party Telemetry**: TrueTime communicates exclusively with configured NTP time servers over UDP port 123. It performs zero analytics, tracking, or outbound HTTP requests.
- **Unpacked Native Assemblies**: Compiled directly into transparent, uncompressed native assemblies without third-party packers, protector wrappers, or obfuscation tools, ensuring clean reputation and zero false-positive security scanner detections.
- **Secure IPC**: Inter-process communication across the named pipe enforces Strict Security Descriptor Definition Language (SDDL) rules allowing access strictly to LocalSystem, Administrators, and Authenticated Users.

---

## 📄 License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for complete details.
