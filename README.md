# 📖 Witch's Gospel — Risk of Rain 2 Mod

> *"I die. I come back. And no matter how many times I return, the pain never fades."*

Mod แนว Re:Zero เพิ่ม **Legendary Item** ที่ให้พลัง "Return by Death" — ตายแล้วย้อนเวลากลับต้น stage

---

## 🔴 วิธีที่ item ทำงาน

| | |
|---|---|
| **Tier** | Legendary (Red) |
| **Charge** | 1 ครั้ง ต่อ stack ต่อ stage |
| **รีเซ็ต** | ทุก stage ใหม่ |

**เมื่อตาย:**
- Stage รีสตาร์ทตั้งแต่ต้น
- Inventory กลับไปเป็นเหมือนตอนเริ่ม stage นั้น
- Gospel stack ลดลง 1 (คืนกลับเมื่อไป stage ถัดไป)

**เมื่อ charge หมด:**
- ตายปกติ (Game Over)

**Stacking:**
- 2 stack = ตายได้ 2 ครั้งต่อ stage
- 3 stack = ตายได้ 3 ครั้งต่อ stage

---

## 👥 วิธีติดตั้ง (สำหรับผู้เล่น)

### วิธีที่ 1: ติดตั้งผ่าน r2modman (แนะนำ)

1. โหลด **r2modman** จาก [thunderstore.io](https://thunderstore.io/package/ebkr/r2modman/)
2. เปิด r2modman → เลือก **Risk of Rain 2**
3. สร้าง Profile ใหม่หรือใช้ profile ที่มีอยู่
4. ติดตั้ง dependencies ก่อน (กด Online แล้วค้นหา):
   - `BepInExPack` by bbepis
   - `R2API` by RiskofThunder
   - `HookGenPatcher` by RiskofThunder
5. วางไฟล์ `WitchsGospel.dll` ใน:
   ```
   ...\r2modmanPlus-local\RiskOfRain2\profiles\<ชื่อ profile>\BepInEx\plugins\WitchsGospel\
   ```
6. กด **Start Modded**

### วิธีที่ 2: ติดตั้งแบบ Manual

1. ติดตั้ง BepInEx ลงในโฟลเดอร์เกม
2. สร้างโฟลเดอร์ `BepInEx\plugins\WitchsGospel\`
3. วางไฟล์ `WitchsGospel.dll` ในโฟลเดอร์นั้น
4. เปิดเกมได้เลย

---

## 🎮 Multiplayer

Mod รองรับ multiplayer ครับ โดย:
- **Host** ต้องติดตั้ง mod
- **Client** ควรติดตั้งด้วยเพื่อให้เห็น item description ถูกต้อง
- Return by Death จะ reload stage ให้ผู้เล่นทุกคนในห้อง
- แต่ละคนมี charge ของตัวเอง (ขึ้นกับจำนวน stack ที่ตัวเองถือ)

---

## 🧪 ทดสอบ item ในเกม

ติดตั้ง **DebugToolkit** เพิ่มเติม แล้วเปิด console (`Ctrl+Alt+\``) พิมพ์:
```
give_item WitchsGospel 1
```

---

## 📋 Dependencies

| Mod | Version |
|-----|---------|
| BepInExPack | 5.4.2120+ |
| R2API | latest |
| HookGenPatcher | 1.2.9+ |

---

## 📝 Changelog

### v2.0.0
- ใช้ `NetworkUserId` เป็น key → รองรับ multiplayer ได้ถูกต้อง
- แก้ปัญหา infinite respawn เมื่อ charge หมด
- แก้ item tier ให้เป็น Legendary (Red) จริงๆ
- แก้ปัญหา stage 1 ใช้ item ไม่ได้หลัง `give_item`

### v1.x.x
- Initial releases (beta)

---

## 🛠️ วิธี Setup สำหรับนักพัฒนา (Dev Setup)

### 1. Clone repo
```bash
git clone https://github.com/<ชื่อ repo>.git
cd WitchsGospel
```

### 2. สร้างไฟล์ `Directory.Build.props`
สร้างไฟล์ชื่อ `Directory.Build.props` ในโฟลเดอร์เดียวกับ `WitchsGospel.csproj` แล้วใส่ข้อความนี้:

```xml
<Project>
  <PropertyGroup>
    <!-- แก้ให้ตรงกับที่ติดตั้งเกมในเครื่องคุณ -->
    <RoR2GameDir>C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2</RoR2GameDir>

    <!-- แก้ให้ตรงกับ r2modman profile <ชื่อที่ตั้ง>> -->
    <ProfilePluginsDir>C:\Users\<ชื่อคุณ>\AppData\Roaming\r2modmanPlus-local\RiskOfRain2\profiles\<ชื่อที่ตั้ง>\BepInEx\plugins</ProfilePluginsDir>

    <!-- ปกติไม่ต้องแก้ -->
    <BepInExDir>C:\Users\<ชื่อคุณ>\AppData\Roaming\r2modmanPlus-local\RiskOfRain2\profiles\<ชื่อที่ตั้ง>\BepInEx</BepInExDir>
  </PropertyGroup>
</Project>
```
// ลาบ
> ⚠️ ไฟล์นี้อยู่ใน `.gitignore` แล้ว จะไม่ถูก push ขึ้น GitHub ทุกคนต้องสร้างของตัวเองครับ

### 3. หา path ที่ถูกต้อง

**RoR2GameDir** → เปิด Steam → คลิกขวา Risk of Rain 2 → Manage → Browse local files

**ProfilePluginsDir** → เปิด r2modman → เลือก profile Dev → Settings → Browse profile folder → เข้าไปที่ `BepInEx\plugins`

### 4. Build
```bash
dotnet build
```
DLL จะถูก copy ไปยัง plugins อัตโนมัติ จากนั้นเปิดเกมผ่าน r2modman → Start Modded ได้เลย
