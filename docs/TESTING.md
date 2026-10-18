# Build و اعتبارسنجی

## Build

1. پروژه را در cTrader Desktop → Algo باز کن؛ در صورت استفاده از Visual Studio، SDK و نسخه‌ی .NET موردنیاز cTrader Automate را نصب کن.
2. Build را اجرا کن و مطمئن شو که هیچ خطای کامپایل وجود ندارد.
3. اگر SDK پروژه‌ی خام را مستقیماً باز نکرد، در cTrader یک Indicator جدید با نام SwingM5 بساز، کد src/SwingM5/SwingM5.cs را جایگزین کد تولیدشده کن و از داخل cTrader Build بگیر.

## بررسی بصری روی M5

- اندیکاتور را روی نماد طلای بروکر، مانند XAUUSD با پسوند احتمالی، در M5 اضافه کن.
- از چند روز مختلف بازار، بازه‌ی روندی و بازه‌ی رنج را بررسی کن.
- زیگزاگ/خط اتصال نباید وجود داشته باشد.
- در تنظیمات پیش‌فرض فقط فلش‌های ورود باید دیده شوند؛ برچسب‌های متنی، مثلث سوینگ و زیگزاگ نمایش داده نمی‌شوند.
- هر فلش باید پس از کندل تأیید M5، شکست ساختار M1 و عبور از فیلترهای ATR/M15 ظاهر شود؛ موقعیت فلش به‌صورت گذشته‌نگر روی کندل اکستریم قرار نمی‌گیرد.
- سیگنال‌هایی که فاصله‌ی برگشت کمتر از حداقل ATR یا بیشتر از حداکثر فاصله‌ی ورود دارند باید حذف شوند.
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


## بررسی کیفیت نسخه‌ی فیلترشده

- در روند صعودی قوی، فروش خلاف روند نباید نمایش داده شود مگر قیمت نزدیک سقف pivot معتبر M15 باشد؛ قانون معکوس برای خرید در روند نزولی است.
- کلوز M5 تأیید باید بدنه‌ی قابل‌توجه و کلوز نزدیک انتهای جهت برگشت داشته باشد؛ کندل دوجی نباید از دروازه عبور کند.
- شکست M1 باید با کندل دارای بدنه‌ی قوی و بافر ATR/tick از رنج سه کندل گذشته عبور کند.
- مقایسه‌ی سیگنال‌ها باید با timestamp زمان صدور فلش انجام شود، نه timestamp کف/سقفی که بعداً تأیید شده است.
- حداقل 3 بازه‌ی روندی، 3 بازه‌ی رنج و یک بازه‌ی نوسان شدید را از چند روز متفاوت بررسی کن. تعداد سیگنال‌ها، میزان ادامه‌ی حرکت پس از فلش و فاصله‌ی سیگنال از اکستریم را ثبت کن. بدون این replay، کیفیت بهتر هنوز فرضیه است، نه نتیجه‌ی اثبات‌شده.
