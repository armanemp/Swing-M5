# Swing-M5

اندیکاتور cTrader برای شناسایی سوینگ‌های تأییدشده‌ی طلا (XAUUSD) روی نمودار M5. این پروژه یک ابزار تحلیل تکنیکال است؛ سیگنال‌ها تضمین سود یا توصیه‌ی معاملاتی نیستند.

## قابلیت‌ها

- بدون زیگزاگ یا خطوط اتصال؛ سوینگ سقف/کف با مثلث و ساختار بازار برچسب می‌خورد.
- عمق پیش‌فرض دو کندل سمت چپ و یک کندل M5 بسته‌شده برای تأیید اولیه‌ی برگشت.
- علامت ورود BUY/SELL فقط پس از کندل برگشتی M5 بسته‌شده و شکست ساختار خرد M1 نمایش داده می‌شود.
- آستانه‌ی امتیاز پیش‌فرض 60 از 100 است؛ فیلتر ATR، رد قیمت، ساختار و زمینه‌ی M15 نیز در امتیاز دخیل‌اند.
- زمان خود سوینگ و زمان تأیید ورود جدا نشان داده می‌شوند؛ سیگنال ورود روی کندل تأیید درج می‌شود، نه به‌صورت گذشته‌نگر روی کف/سقف.
- هشدار صوتی و پیام لاگ برای تأیید ورود زنده؛ سقف تعداد اشیای نمودار قابل تنظیم است.

## ساختار مخزن

- src/SwingM5/SwingM5.cs — کد اندیکاتور
- src/SwingM5/SwingM5.csproj — پروژه‌ی .NET برای cTrader Automate SDK
- docs/LOGIC.md — تعریف منطق و روش امتیازدهی
- docs/TESTING.md — چک‌لیست build و بررسی روی چارت

## ساخت و نصب

1. با نسخه‌ی دسکتاپ cTrader، بخش Algo را باز کن و یک Indicator به نام SwingM5 بساز؛ یا پروژه‌ی موجود در این مخزن را با پشتیبانی cTrader Automate SDK باز کن.
2. محتوای src/SwingM5/SwingM5.cs را در فایل اندیکاتور قرار بده و Build کن.
3. اندیکاتور را روی نمودار نماد طلای بروکر خودت (مثلاً XAUUSD یا پسوند اختصاصی بروکر) در تایم‌فریم M5 اجرا کن.
4. در شروع با مقادیر پیش‌فرض کار کن. برای خروجی کمتر و سخت‌گیرانه‌تر، پارامتر Minimum score را بالا ببر؛ برای دیدن سوینگ‌های بیشتر آن را کمی پایین بیاور.

پروژه مخصوص M5 است؛ اگر روی تایم‌فریم دیگری اجرا شود، در لاگ هشدار می‌دهد و سوینگی رسم نمی‌کند.

## نکته‌ی مهم

هیچ الگوریتم عمومی نمی‌تواند سوینگ‌های آینده را از قبل با قطعیت پیش‌بینی کند. تأیید pivot به تعداد کندل‌های سمت راست نیاز دارد؛ بنابراین علامت پس از تأخیر تعریف‌شده ظاهر می‌شود. قبل از استفاده‌ی واقعی، روی داده‌ی بروکر خودت و در حساب آزمایشی ارزیابی کن.

## عیب‌یابی Restore در ویندوز

اگر خطای NU1301 برای `https://api.nuget.org/v3/index.json` دریافت شد، این خطا معمولاً پیش از کامپایل و هنگام اتصال به NuGet رخ می‌دهد.

ابتدا این دستورات را در PowerShell اجرا کن:

```powershell
dotnet --info
dotnet nuget list source
Test-NetConnection api.nuget.org -Port 443
Invoke-WebRequest "https://api.nuget.org/v3/index.json" -TimeoutSec 15 -UseBasicParsing
```

اگر منبع nuget.org غیرفعال یا حذف شده است، آن را با نامی که در فهرست منابع وجود ندارد اضافه کن:

```powershell
dotnet nuget add source "https://api.nuget.org/v3/index.json" --name nuget.org
```

سپس کش NuGet را پاک و Restore را دوباره اجرا کن:

```powershell
dotnet nuget locals all --clear
dotnet restore .\src\SwingM5\SwingM5.csproj --source "https://api.nuget.org/v3/index.json" --disable-parallel
dotnet build .\src\SwingM5\SwingM5.csproj --configuration Release --no-restore
```

اگر تست اتصال TCP یا `Invoke-WebRequest` شکست خورد، مشکل هنوز شبکه، DNS، پراکسی یا محدودیت فایروال است؛ تغییر کد اندیکاتور آن را برطرف نمی‌کند. از شبکه‌ی دیگری استفاده کن یا تنظیمات پراکسی/فایروال را بررسی کن. از واردکردن گواهی یا غیرفعال‌کردن بررسی TLS به‌عنوان راه‌حل دائمی خودداری کن.

راه جایگزین برای کامپایل مستقیم داخل cTrader: در نسخه‌ی دسکتاپ، به بخش **Algo** برو، یک Indicator با نام `SwingM5` بساز، کد فایل `src/SwingM5/SwingM5.cs` را در آن قرار بده و با دکمه‌ی **Build** خود cTrader کامپایل کن. این روش می‌تواند از نیاز به Restore پروژه‌ی بیرونی جلوگیری کند، زیرا کد اندیکاتور به بسته‌ی NuGet دیگری به‌جز SDK ساخت بیرونی وابسته نیست.

## Build directly with the installed cTrader compiler (no NuGet required)

When access to nuget.org is unavailable, use cTrader Desktop's embedded compiler instead of running `dotnet restore`.

1. From the cloned repository, run this PowerShell script:

   ```powershell
   Set-Location "$HOME\Swing-M5"
   git pull --ff-only origin main
   .\scripts\Copy-ToCTrader.ps1
   ```

2. The script copies the single-file indicator source to `%USERPROFILE%\Documents\cAlgo\Sources\Indicators\SwingM5\SwingM5.cs`. If a file already exists there, the script first saves a timestamped backup.
3. Open **cTrader Desktop → Settings → Algo** and select **Embedded compiler** in the compiler selector.
4. Open **Algo → Indicators**, select/refresh `SwingM5`, then press **Build**.
5. Confirm that cTrader reports a successful build and the indicator appears under its Indicators list. Then add it to an XAUUSD M5 chart.

The source-copy script only stages code; it does not compile it. Prefer the validated project build script above when the cached cTrader.Automate package is available. The actual compile result must be confirmed in the build log. If the installed cTrader version does not pick up the new source automatically, restart cTrader Desktop and open the Indicators section again.

## External .NET build

The external project targets .NET 6 and pins `cTrader.Automate` to `1.0.21`. If that package is already in the local NuGet cache, an offline restore can be attempted with:

```powershell
dotnet restore .\src\SwingM5\SwingM5.csproj --ignore-failed-sources
dotnet build .\src\SwingM5\SwingM5.csproj --configuration Release --no-restore
```

If the package is not cached, the external build still needs package access. The installed embedded compiler route above is the preferred offline fallback.

## Build the named SwingM5.algo package

The project lives below `src/SwingM5`, so cTrader.Automate can otherwise infer the wrong bundle filename from the parent folder and emit `src.algo`. The root `Directory.Build.targets` explicitly fixes the bundle metadata to the `AlgoName`, and CI verifies that the output is named `SwingM5.algo`.

After pulling the latest commit, build and publish with the repository script:

```powershell
Set-Location "$HOME\Swing-M5"
git pull --ff-only origin main
.\scripts\build-indicator.ps1
```

The script restores from the available NuGet cache when network access is unavailable, builds Release, validates the exact local artifact path, and verifies/copies the resulting `SwingM5.algo` to `%USERPROFILE%\Documents\cAlgo\Sources\Indicators\SwingM5.algo`. If a previous file with that exact name is different, it creates a timestamped backup before replacing it.

The old `%USERPROFILE%\Documents\cAlgo\Sources\Indicators\src.algo` is a wrongly named artifact from the earlier build. Do not treat it as SwingM5. Once the newly named `SwingM5.algo` is verified, you may remove or move that stale `src.algo` if you confirm it belongs to this project's earlier build and is not used by another project.
