# Build و اعتبارسنجی

## Build

1. پروژه را در cTrader Desktop → Algo باز کن؛ در صورت استفاده از Visual Studio، SDK و نسخه‌ی .NET موردنیاز cTrader Automate را نصب کن.
2. Build را اجرا کن و مطمئن شو که هیچ خطای کامپایل وجود ندارد.
3. اگر SDK پروژه‌ی خام را مستقیماً باز نکرد، در cTrader یک Indicator جدید با نام SwingM5 بساز، کد src/SwingM5/SwingM5.cs را جایگزین کد تولیدشده کن و از داخل cTrader Build بگیر.

## بررسی بصری روی M5

- اندیکاتور را روی نماد طلای بروکر، مانند XAUUSD با پسوند احتمالی، در M5 اضافه کن.
- از چند روز مختلف بازار، بازه‌ی روندی و بازه‌ی رنج را بررسی کن.
- زیگزاگ/خط اتصال نباید وجود داشته باشد.
- مثلث سقف/کف روی کندل اکستریم احتمالی، و فلش BUY/SELL روی کندل M5 بسته‌شده‌ی تأیید ورود ظاهر می‌شود؛ انتظار نباید این دو علامت را روی یک کندل ببیند.
- با مقدار پیش‌فرض عمق 2، کاندید با دو کندل سمت چپ و یک کندل M5 بسته‌شده به‌عنوان عدم ادامه‌ی اکستریم بررسی می‌شود؛ سپس شکست ساختار M1 لازم است.
- SH/SL یعنی اولین سقف/کف پذیرفته‌شده در تاریخچه‌ی در دسترس؛ HH/LH و HL/LL ساختار نسبت به سوینگ هم‌نوع پیشین‌اند؛ EQH/EQL یعنی تفاوت قیمت کمتر از تلورانس ATR.
- در تنظیمات، Minimum score را موقتاً پایین بیاور تا حساسیت بیشتر را ببینی و سپس برای انتخاب خروجی متناسب با بروکر به مقدار مناسب برگردان.
- گزینه‌های M15 و M1 را جداگانه خاموش و روشن کن و تغییر امتیاز/تعداد نقاط را مقایسه کن.
- با اسکرول به تاریخچه‌ی قدیمی‌تر و سپس برگشت به داده‌ی زنده، بررسی کن که سوینگ‌های تأییدشده جابه‌جا نمی‌شوند.
- روی M1 و M15 خود اندیکاتور باید بدون رسم سوینگ متوقف شود و پیام راهنما در لاگ دیده شود.

## آزمون عدم repaint

1. در یک لحظه‌ی مشخص از چارت، زمان مثلث اکستریم و زمان فلش BUY/SELL را جداگانه ثبت کن.
2. اجازه بده چندین کندل جدید بسته شود؛ علامت‌های تاریخی نباید منتقل شوند و فلش ورود نباید روی کندل قدیمی اکستریم backdate شود.
3. اگر نقطه‌ای اضافه شد، باید مربوط به pivot جدیدی باشد که کندل‌های سمت راست آن اکنون بسته شده‌اند.

## قبل از استفاده‌ی عملی

ابتدا حساب آزمایشی و داده‌ی تاریخی همان بروکر را بررسی کن. اجرای زنده، تأخیر شبکه، اسپرد و گپ می‌توانند نتیجه‌ی معاملاتی را تغییر دهند. این اندیکاتور خودش سفارش نمی‌گذارد و سیگنال آن توصیه‌ی مالی نیست.

## Offline compile using cTrader Desktop

When NuGet is unreachable:

1. Run `.\scripts\Copy-ToCTrader.ps1` from the repository root in PowerShell.
2. In cTrader Desktop, choose **Settings → Algo → Embedded compiler**.
3. Open **Algo → Indicators**, select/refresh SwingM5, and click **Build**.
4. Capture the cTrader compile log; continue only if it reports a successful compile.
5. Add the indicator to XAUUSD M5 and check the output against the visual and non-repaint checklist above.

The local cTrader build is the platform acceptance step; a source copy is not a successful compile.

## Verified project build and artifact naming

Run `.\scripts\build-indicator.ps1` from the repository root. It requires a successful restore/build, checks the exact file `src/SwingM5/bin/Release/net6.0/SwingM5.algo`, then installs that artifact in `%USERPROFILE%\Documents\cAlgo\Sources\Indicators\SwingM5.algo` with a timestamped backup if an existing artifact differs.

The root `Directory.Build.targets` sets the cTrader bundle filename before metadata extraction. CI also checks the named artifact so a generic success message or a wrongly named `src.algo` does not pass the artifact-validation gate.
