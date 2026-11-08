# 🎮 ARC Oyun Fabrikası

**Fikrini yaz, oyunun gelsin.** GitHub'da bir issue açıp oyun fikrini Türkçe yazarsın. Claude oyunu Unity için kodlar, GitHub'ın sunucuları Unity ile derler, sana da şunlar gelir:

- ▶️ Telefonda ve bilgisayarda **tarayıcıda hemen oynanan bir link**
- 📱 Telefona kurulabilen **Android APK**
- 📦 Her sürümün saklandığı bir **Release** sayfası

Bilgisayarında Unity açmana, kod yazmana ya da bilgisayarını açık tutmana gerek yok.

> _Emre Arıcı / ARC Studios - Tam Bağımsız Fikir Stüdyosu_

---

## Nasıl çalışır?

```
 Sen: Issue → "🎮 Oyun Fikri" formu
        │
        ▼
 🧠 Claude (GitHub Actions)  →  CLAUDE.md kurallarıyla C# kodu yazar → main'e kaydeder
        │
        ▼
 🛠️ Unity 6 (GameCI, bulutta)  →  WebGL + Android APK derler
        │
        ▼
 🚀 Cloudflare Pages + GitHub Release  →  Linkler issue'ya yorum olarak gelir
```

Bunu mümkün kılan bir püf noktası var: Projede hazır sahne, model, resim ya da ses dosyası **yok**. Her şey koddan üretiliyor. Şekiller, renkler, ses efektleri ve müzik çalışma anında oluşuyor (`Assets/Scripts/Arc/` içindeki yardımcılar sayesinde). Bu yüzden Claude sadece kod yazarak tam bir oyun çıkarabiliyor.

---

## Tek seferlik kurulum (~20 dk)

### 1) Repo
`arcstudios7/arc-oyun-fabrikasi` (**private**). Dosyalar zaten yüklü.

### 2) Unity lisans dosyasını al (ücretsiz Personal lisans)
1. Windows'a **Unity Hub** kur: https://unity.com/download
2. Unity hesabınla giriş yap.
3. Hub → ⚙️ **Preferences / Settings** → **Licenses** → **Add** → **Get a free personal license**.
4. Şu dosyayı Not Defteri ile aç: `C:\ProgramData\Unity\Unity_lic.ulf`
   (ProgramData gizli bir klasör. Adres çubuğuna yazarak gidebilirsin.)
5. İçindeki metnin **tamamını** kopyala. `<?xml` ile başlıyor.

> Unity hesabında iki adımlı doğrulama (2FA) açıksa build sırasında giriş yapılamayabilir. Sorun çıkarsa 2FA'yı kapat ya da GameCI belgelerine bak.

### 3) Gizli anahtarları ekle
Repo → **Settings → Secrets and variables → Actions → New repository secret**:

| İsim | Değer |
|---|---|
| `UNITY_LICENSE` | 2. adımda kopyaladığın `.ulf` metninin tamamı |
| `UNITY_EMAIL` | Unity hesabının e-postası |
| `UNITY_PASSWORD` | Unity hesabının şifresi |
| `ANTHROPIC_API_KEY` | https://console.anthropic.com → **API Keys → Create Key** ile aldığın `sk-ant-...` anahtarı |
| `CLOUDFLARE_API_TOKEN` | 4. adımda oluşturacağın token |
| `CLOUDFLARE_ACCOUNT_ID` | Cloudflare hesap kimliğin (4. adım) |

> Console'da **Limits / Billing** bölümünden aylık harcama limiti koymanı öneririm. Her oyun ya da değişiklik birkaç dolarlık API kullanımı tutabilir.

### 4) Cloudflare Pages (web linki için)
Ücretsiz GitHub planında private repolarda GitHub Pages yok. Web sürümünü bu yüzden Cloudflare Pages'e koyuyoruz. Her oyun kendi adresinde yayında kalıyor: `arc-<oyun-adi>.pages.dev`.
1. https://dash.cloudflare.com → sağ üstte profil → **My Profile → API Tokens → Create Token**
2. **Create Custom Token**: izin olarak **Account → Cloudflare Pages → Edit** seç → **Continue → Create Token** → token'ı kopyala → `CLOUDFLARE_API_TOKEN`
3. Hesap kimliği: Dashboard ana sayfasında hesabının yanındaki **⋯ → Copy Account ID** (ya da Workers & Pages sayfasının sağ tarafında) → `CLOUDFLARE_ACCOUNT_ID`

İstersen bir oyuna sonradan Cloudflare'den `oyun.arcstudios.com.tr` gibi bir alt alan adı bağlayabilirsin (Workers & Pages → proje → Custom domains).
Cloudflare ayarlanmazsa da sistem çalışır. Web sürümü o zaman sadece Release'te zip olarak gelir.

### 5) Kurulumu kontrol et
Repo → **Actions → ✅ Kurulum Kontrolü → Run workflow**.
Bu adım `oyun-fikri` etiketini oluşturur ve eksik bir şey varsa tabloda ❌ ile gösterir.

### 6) (İsteğe bağlı) İlk build'i dene
**Actions → 🛠️ Unity Build → Run workflow** ile örnek oyun "Yıldız Avcısı"yı derle. İlk build 15-20 dk sürebilir (Unity indiriliyor). Sonrakiler önbellek sayesinde daha hızlı.

---

## Kullanım

### Yeni oyun
**Issues → New issue → 🎮 Oyun Fikri** → formu doldur → **Create**.
Birkaç saniye içinde "Fikrini aldım" yorumu gelir. Bitince link de yorum olarak gelir.

### Değişiklik iste
Aynı issue'ya `/oyun` ile başlayan bir yorum yaz:
- `/oyun düşmanlar daha yavaş olsun, 3 can ekle`
- `/oyun arka planı gün batımı renklerine çevir`
- `/oyun düzelt` (build hata verdiyse Claude hata kaydını okuyup onarır)

### Bu sohbetten (Claude Code ile) kullanım
Repoyu Claude Code'a bağlayıp "şöyle bir oyun yap" demen de yeterli. Claude aynı `CLAUDE.md` kurallarıyla kodlar ve `main`'e gönderir. **🛠️ Unity Build** otomatik çalışıp web sürümünü yayınlar. APK için Actions'tan elle çalıştırırsın.

---

## Süreler ve maliyet
- Web sürümü: ~10-15 dk. APK ile: ~30-40 dk.
- Private repoda ücretsiz GitHub planı ayda **2.000 Actions dakikası** veriyor. Yaklaşık 50 APK'lı ya da 130 sadece-web oyun eder. Kullanımı Settings → Billing'den görebilirsin.
- Cloudflare Pages ücretsiz planı bu kullanım için yeterli.
- Claude kullanımı API anahtarından ücretlenir (console.anthropic.com → Usage).

## Sınırlar
- Grafikler koddan üretilen şekillerden oluşuyor (küp, küre, kapsül, 2D şekiller). Sade ve renkli bir "minimal" görünüm. Hazır karakter modeli yok.
- Sesler retro/chiptune tarzında sentezleniyor.
- Hızlı oynanan arcade, refleks, bulmaca, koşu, nişancı ve fizik oyunları en iyi sonucu verir. Çok büyük açık dünya ya da online çok oyunculu oyunlar bu yapıya uygun değil.
- iOS bu sürümde yok. Apple Developer hesabı ve bir macOS runner ile sonradan eklenebilir.
- APK debug imzalı. Telefona kurmak için "bilinmeyen kaynaklara izin ver" gerekir. Google Play'e yüklemek için ayrıca keystore ile imzalı AAB build eklenmeli.
- Unity Personal lisansında açılışta "Made with Unity" logosu görünür.

## Sorun giderme
| Belirti | Çözüm |
|---|---|
| Issue açtım, hiçbir şey olmadı | `oyun-fikri` etiketi var mı? Kurulum Kontrolü'nü çalıştır. Issue'yu sen mi açtın? (Sadece repo sahibi tetikleyebilir.) |
| `License activation failed` | `UNITY_LICENSE` içeriğinin tamamı yapıştırılmış mı? E-posta/şifre doğru mu? 2FA kapalı mı? |
| `Unity image not found` / sürüm hatası | `ProjectSettings/ProjectVersion.txt` içindeki sürümü, [GameCI imaj listesinde](https://hub.docker.com/r/unityci/editor/tags) bulunan başka bir `6000.0.x` sürümüyle değiştir |
| Build başarısız (kod hatası) | Issue'ya `/oyun düzelt` yaz |
| Web linki gelmedi | `CLOUDFLARE_API_TOKEN` ve `CLOUDFLARE_ACCOUNT_ID` var mı? Kurulum Kontrolü'nü çalıştır |
| APK linki açılmıyor | Repo private: telefonda GitHub'a giriş yapmış olman lazım (ya da APK'yı PC'den indirip telefona at) |
| Push yetki hatası | Settings → Actions → General → Workflow permissions → **Read and write** |

## Klasörler
```
Assets/Scripts/Arc/   → motor katmanı (şekil, ses, efekt, kontrol, menü). Dokunma.
Assets/Scripts/Game/  → aktif oyun. Claude burayı yazar.
Assets/Editor/        → otomatik sahne kurulumu ve build betiği
CLAUDE.md             → Claude'un oyun üretme kuralları (İngilizce)
GAME.md               → aktif oyunun Türkçe tanıtımı
.github/workflows/    → fikirden-oyuna, build, kurulum
```
