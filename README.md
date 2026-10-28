# WebToApp Studio (.NET 9 / WinForms)

برنامه ویندوزی برای تبدیل **آدرس یک سایت** به اپلیکیشن **اندروید (APK / AAB)** و **iOS (IPA)**.
A Windows app that turns a website URL into an Android app (APK/AAB) and an iOS app (IPA).

## اجرا / Run
1. نصب **.NET 9 SDK** → `dotnet run` یا `build.bat` (خروجی: `publish\WebToAppStudio.exe`).
2. در تب «پروژه» نام، آدرس سایت، Package/Bundle ID، آیکون و رنگ را وارد کنید.

## اندروید / Android  (کاملاً روی ویندوز)
- نیاز: **Android Studio** (یک‌بار نصب و باز شود؛ JDK و SDK را می‌آورد). Gradle خودکار دانلود می‌شود.
- تب «اندروید» → *ساخت APK*. اولین بار چند دقیقه طول می‌کشد (دانلود اجزای Android).
- اگر خطای License دیدید، گزینه «لایسنس‌ها را می‌پذیرم» را بزنید یا `sdkmanager --licenses` را اجرا کنید.
- Keystore خودکار ساخته می‌شود (`<خروجی>\keystore`). **حتماً از آن و فایل رمزش نسخه پشتیبان بگیرید.**
- نصب مستقیم روی گوشی اندروید با کابل: USB debugging روشن + دکمه «نصب روی گوشی».

## iOS  (ویندوز + یک بار کامپایل ابری)
ویندوز نمی‌تواند Swift را کامپایل کند، پس:
1. تب «آیفون» → **خروجی سورس iOS** (پوشه `ios-source`).
2. آن پوشه را در یک مخزن GitHub بگذارید → *Actions → Build iOS app → Run workflow* → فایل **Template.ipa** را از Artifacts بگیرید
   (روی مک ابریِ GitHub ساخته می‌شود؛ نیازی به مک ندارید).
3. در برنامه: Template.ipa + گواهی `.p12` + پروفایل `.mobileprovision` را انتخاب کنید → **ساخت IPA امضاشده**
   (برنامه تنظیمات جدید را داخل IPA می‌نویسد و با zsign امضا می‌کند).
4. آیفون را با کابل وصل کنید، Trust بزنید → **پیدا کردن آیفون** → **نصب روی آیفون**.
   اپ مستقیم روی گوشی ظاهر می‌شود (در صورت نیاز: Settings ▸ General ▸ VPN & Device Management ▸ Trust).

نیازهای ویندوز برای مرحله ۳ و ۴: `zsign.exe`، `ideviceinstaller.exe`، `idevice_id.exe`، `ideviceinfo.exe` در پوشه `tools`
(راهنما: `tools\README.txt`) و نصب بودن **Apple Devices** (Microsoft Store) یا iTunes برای درایور USB.
گواهی و پروفایل باید از **حساب Apple Developer** شما باشد و UDID آیفون داخل پروفایل Development ثبت شده باشد.

## انتشار در App Store / Google Play
- iOS: همان Workflow اگر Secretهای `IOS_P12_BASE64`, `IOS_P12_PASSWORD`, `IOS_PROFILE_BASE64` (گواهی Distribution و پروفایل App Store) را داشته باشد،
  خروجی **AppStore-export** می‌سازد؛ با Secretهای `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_P8_BASE64` مستقیم به App Store Connect/TestFlight آپلود می‌شود،
  وگرنه با **Apple Transporter (ویندوز)** از همان پوشه (شامل `AppStoreInfo.plist`) آپلود کنید.
- Android: تیک «AAB» را بزنید و فایل `.aab` را در Google Play Console آپلود کنید.
- ⚠️ اپل طبق Guideline 4.2 اپ‌هایی را که فقط یک سایت ساده هستند اغلب رد می‌کند؛ قابلیت واقعی (اعلان، ورود، امکانات بومی) اضافه کنید.

## ساختار کد
`Core/` منطق (AppConfig, AndroidProjectGenerator, AndroidBuilder, IosSourceExporter, IosIpaBuilder, DeviceInstaller, ToolLocator) —
`UI/MainForm.cs` رابط فارسی/انگلیسی.
