# Protocol Human – Güncel Denge Raporu

Her senaryo **3.000 kez** Monte Carlo simülasyonu ile oynatıldı.
Can yenileme (Tamir) kartları kaldırıldı; tüm normal kartlar sadece CPU harcıyor, Hurda sadece Blueprint (Dost Drone çağırma) kartlarında kullanılıyor.

## 1. Birim Değerleri

### Ana Drone ve Dost Dronelar
| Birim | HP | Hareket | Maks CPU | Tur Başı CPU | Şarj (Ömür) | Kit Kartları |
|---|---|---|---|---|---|---|
| **Ana Drone** | **160** *(eski: 120)* | 5 | 6 | **3** *(eski: 2)* | Sınırsız | Sabit + Ortak Havuz |
| **Dost Yakın Drone** | **85** *(eski: 70)* | 4 | - | - | **4 Tur** *(eski: 3)* | `Yakın Saldırı`, `Stun Saldırısı`, `Kalkan` |
| **Dost Uzak Drone** | **50** *(eski: 35)* | 3 | - | - | **4 Tur** *(eski: 3)* | `Kendini Patlat`, `Uzak Saldırı`, `Riskli Uzak Saldırı` |
| **Dost Boss Drone** | **140** *(eski: 110)* | 3 | - | - | **5 Tur** | `Güçlü Yakın Saldırı`, `Taunt`, `CPU Yenile` |

### Düşman Dronelar
| Düşman | HP | Hasar | Menzil | Hareket | Hurda | Kart % | Blueprint % |
|---|---|---|---|---|---|---|---|
| **Yakın Düşman** | **52** *(eski: 60)* | **8–12** *(eski: 11–16)* | 1 | 4 | 2–4 | **%35** *(eski: %30)* | **%16** *(eski: %12)* |
| **Uzak Düşman** | **36** *(eski: 40)* | **6–10** *(eski: 9–13)* | 4 | 3 | 2–4 | **%40** *(eski: %35)* | **%18** *(eski: %15)* |
| **Boss Düşman** | **110** *(eski: 130)* | **12–17** *(eski: 16–23)* | 1 | 3 | 6–10 | **%65** *(eski: %60)* | **%40** *(eski: %35)* |

---

## 2. Kart Değerleri (13 Benzersiz Kart)

| Kart Adı | Tür | CPU | Hurda | Yeni Değer / Etki | Eski Değer | Menzil | İsabet | Özel Etki |
|---|---|---|---|---|---|---|---|---|
| **Yakın Saldırı** | Saldırı | 1 | 0 | **22–30 Hasar** | 18–26 | 1 | %100 | Sabit + Dost Yakın |
| **Uzak Saldırı** | Saldırı | 1 | 0 | **13–18 Hasar** | 10–15 | 6 | %100 | Sabit + Dost Uzak |
| **Kalkan** | Savunma | 1 | 0 | **20–28 Kalkan** | 12–18 | - | - | Ortak Havuz (Ağırlık: 25) + Dost Yakın |
| **Güçlü Yakın Saldırı** | Saldırı | 2 | 0 | **36–46 Hasar** | 30–40 | 1 | %100 | Ortak Havuz (Ağırlık: 15) + Dost Boss |
| **Stun Saldırısı** | Saldırı | 1 | 0 | **12–16 Hasar** | 6–10 | 1 | %100 | **%75 Stun** *(eski: %70)* · Ortak Havuz (15) + Dost Yakın |
| **Riskli Uzak Saldırı** | Saldırı | 2 | 0 | **28–38 Hasar** | 24–34 | 6 | **%65** *(eski: %50)* | Ortak Havuz (Ağırlık: 15) + Dost Uzak |
| **Taunt** | Savunma | 1 | 0 | **22–30 Kalkan** | 15–22 | - | - | **2 Tur Taunt** · Ortak Havuz (12) + Dost Boss |
| **CPU Yenile** | Özel | 0 | 0 | **+2 CPU** | +2 CPU | - | - | Ortak Havuz (Ağırlık: 10) + Dost Boss |
| **Şarj** | Özel | 1 | 0 | **+1 Tur Şarj** | +1 Tur Şarj | - | - | Ortak Havuz (Ağırlık: 10) |
| **Kendini Patlat** | Özel | 0 | 0 | **40–55 Hasar** | 35–50 | Alan: 2 | %100 | Dost Uzak Drone |
| **Blueprint: Yakın Drone** | Çağrı | **2** *(eski: 3)* | **6** *(eski: 8)* | `Drone_Dost_Yakin` | 3 CPU / 8 Hurda | 1 | - | 85 HP, 4 Hareket, 4 Tur Şarj |
| **Blueprint: Uzak Drone** | Çağrı | **2** *(eski: 3)* | **6** *(eski: 8)* | `Drone_Dost_Uzak` | 3 CPU / 8 Hurda | 1 | - | 50 HP, 3 Hareket, 4 Tur Şarj |
| **Blueprint: Boss Drone** | Çağrı | **3** *(eski: 5)* | **10** *(eski: 15)* | `Drone_Dost_Boss` | 5 CPU / 15 Hurda | 1 | - | 140 HP, 3 Hareket, 5 Tur Şarj |

---

## 3. Çevre Değerleri
- **Hurda Yığını:** `2–5 Hurda` *(eski: 1–4)*, `%15 Blueprint Şansı` *(eski: %10)*
- **Patlayan Varil:** `30–40 Alan Hasarı` *(eski: 25–35)*, Yarıçap: `2` (5x5)

---

## 4. Simülasyon Sonuçları (3.000 Koşu)
- **Eski Değerlerle (Tamir Kartları Kaldırıldıktan Sonra):**
  - `Buyuk Level` Bitirme Oranı: **%0,0** (Tamir kartları olmadan 120 HP ve 12–18 Kalkan ile 28 karşılaşma tamamlanamıyordu).
- **Yeni Dengelenmiş Değerlerle:**
  - `Buyuk Level` (28 karşılaşma, 40 düşman – Dost dronelar kullanılarak): **%65,0 Bitirme Oranı** (Ortalama kalan can: `47 / 160`, ortalama çağrılan dost drone: `9,4`).
  - `örnek level` (6 karşılaşma): **%100,0 Bitirme Oranı**.
