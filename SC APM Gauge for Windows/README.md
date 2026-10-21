<div align="center">

<!-- 스크린샷을 추가하려면 docs/ 폴더에 이미지를 올리고 아래 주석을 해제하세요 -->
<!-- <img src="docs/banner.png" alt="APM 계기판" width="640"> -->

# ⚡ APM 계기판 (APM Meter)

**엑셀도 스타처럼 — 업무 중 실시간 APM을 보여 주는 윈도우 위젯**

<p>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows&logoColor=white" alt="Windows 10 | 11">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.x%20(built--in)-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET Framework 4.x">
  <img src="https://img.shields.io/badge/language-C%23%205-239120?style=flat-square&logo=csharp&logoColor=white" alt="C# 5">
  <img src="https://img.shields.io/badge/dependencies-none-brightgreen?style=flat-square" alt="No dependencies">
  <img src="https://img.shields.io/badge/data%20collection-none-success?style=flat-square" alt="No data collection">
</p>

<p>
  <a href="#-빠른-시작">빠른 시작</a> •
  <a href="#-화면과-조작">화면과 조작</a> •
  <a href="#-집계-기준">집계 기준</a> •
  <a href="#-계산-방식">계산 방식</a> •
  <a href="#-보안-및-개인정보">보안</a> •
  <a href="#-문제-해결">문제 해결</a>
</p>

</div>

---

## ✨ 소개

엑셀, 웹 서핑, 문서 작성 같은 평범한 사무 작업 중에도 스타크래프트를 하는 듯한 손맛을 주는 **실시간 APM(분당 동작 수) 계기판**입니다.

<table>
  <tr>
    <td align="center" width="33%">🎯<br><b>백그라운드 감지</b><br><sub>어떤 프로그램을 쓰든<br>키보드·클릭을 실시간 집계</sub></td>
    <td align="center" width="33%">📈<br><b>스타식 APM</b><br><sub>동작 1회당 +1<br>손을 떼면 3초 안에 주르륵</sub></td>
    <td align="center" width="33%">🖥️<br><b>깔끔한 위젯</b><br><sub>테두리 없음 · 항상 위<br>네온 연두 고정폭 숫자</sub></td>
  </tr>
</table>

- 별도 런타임이나 라이브러리 **설치 없이** Windows 기본 컴파일러로 `.exe` 하나를 만듭니다.
- **어떤 키를 눌렀는지 저장하지 않습니다.** 파일·레지스트리·네트워크 접근 코드가 없습니다.
- 갱신 주기 50ms, 숫자가 바뀔 때만 다시 그려서 CPU 사용량이 매우 낮습니다.

<!-- <p align="center"><img src="docs/demo.gif" alt="데모" width="480"></p> -->

---

## 🚀 빠른 시작

### 요구 사항

| 항목 | 내용 |
|---|---|
| OS | Windows 10 / 11 |
| 런타임 | .NET Framework 4.x (Windows에 기본 포함) |
| 추가 설치 | **없음** (Visual Studio 불필요) |

### 빌드 및 실행

```text
APM/
├── APM.cs       ← 소스 코드
└── build.bat    ← 빌드 스크립트
```

1. 저장소를 내려받거나 두 파일을 같은 폴더에 둡니다.
2. **`build.bat`** 을 더블클릭합니다.
3. `[OK] APM.exe built.` 메시지가 나오면 **`APM.exe`** 를 실행합니다.
4. 화면 오른쪽 위에 계기판이 나타납니다.

> [!NOTE]
> 빌드 스크립트는 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`(없으면 32비트 경로)를 사용합니다.

> [!IMPORTANT]
> 실행 중인 `APM.exe`는 덮어쓸 수 없습니다. 다시 빌드하기 전에 계기판을 먼저 종료하세요.

---

## 🖥️ 화면과 조작

<!-- <p align="center"><img src="docs/widget.png" alt="계기판 위젯" width="240"></p> -->

```text
┌────────────────────────┐
│ APM           PEAK  312│
│                   187  │
└────────────────────────┘
```

| 요소 | 설명 |
|---|---|
| **숫자** | 현재 APM. 고정폭 글꼴(Consolas)이라 자릿수가 바뀌어도 흔들리지 않음 |
| **PEAK** | 실행 후 최고 기록 |
| **배경** | 검은 반투명, 테두리 없음 |
| **창 위치** | 항상 다른 창 위에 고정. 작업 표시줄과 <kbd>Alt</kbd>+<kbd>Tab</kbd> 목록에는 나타나지 않음 |

### 숫자 색

| APM | 색 |
|---|---|
| 0 ~ 249 | <img src="https://img.shields.io/badge/-네온%20연두-39FF14?style=flat-square" alt="네온 연두"> |
| 250 ~ 399 | <img src="https://img.shields.io/badge/-황색-FFD600?style=flat-square" alt="황색"> |
| 400 이상 | <img src="https://img.shields.io/badge/-주황-FF5A28?style=flat-square" alt="주황"> |

### 조작

| 동작 | 기능 |
|---|---|
| <kbd>좌클릭</kbd> + 드래그 | 계기판 이동 (글자 위를 잡아도 됨) |
| <kbd>더블클릭</kbd> | 종료 |

<details>
<summary><b>더블클릭으로 꺼지지 않을 때</b></summary>
<br>

작업 표시줄에 나타나지 않으므로 작업 관리자를 사용하세요.

1. <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Esc</kbd> 로 작업 관리자를 엽니다.
2. `APM.exe`를 찾아 선택합니다.
3. **작업 끝내기**를 누릅니다.

</details>

---

## 🎯 집계 기준

| 입력 | 집계 | 비고 |
|---|---|---|
| 키보드 키 누름 (KeyDown) | ✅ 1회 | 키를 **꾹 누를 때 생기는 자동 반복은 제외** |
| 마우스 좌클릭 누름 | ✅ 1회 | |
| 마우스 우클릭 누름 | ✅ 1회 | |
| 마우스 이동 | ❌ | |
| 마우스 휠 | ❌ | |
| 키·버튼을 뗄 때 | ❌ | |

프로그램이 백그라운드에 있어도 Windows 전체의 입력을 감지합니다.

---

## 📈 계산 방식

스타크래프트 계열 APM 계산기(APMAlert 등)와 같은 **지수 감쇠 방식**에, 업무용으로 **손을 떼면 빨리 떨어지는 규칙**을 더했습니다.

| 구간 | 동작 |
|---|---|
| ⬆️ **올라갈 때** | 동작 1회당 **+1**. 같은 속도로 계속 치면 실제 분당 속도로 서서히 수렴 |
| ⬇️ **떨어질 때** | 0.4초 동안 입력이 없으면 빠르게 감쇠해 **약 3초 안에 0 근처** |

**손을 뗐을 때 예시** (233 APM에서 시작)

| 경과 | 0초 | 0.5초 | 1초 | 2초 | 3초 |
|---|:-:|:-:|:-:|:-:|:-:|
| APM | 233 | 162 | 78 | 18 | 4 |

<details>
<summary><b>수식 자세히 보기</b></summary>
<br>

과거 동작을 시간에 따라 지수적으로 잊어버리는 누적 카운터 `C`를 사용합니다.

```text
동작 1회 발생 시:   C ← C + 60 / τ
매 갱신(dt초)마다:  C ← C × exp(−dt × k)

  k = 1/τ                 (평소)
  k = 1/τ + 1/τ_idle      (마지막 입력 후 IdleGraceSec 이상 지났을 때)

표시 APM = round(C)
```

- `τ = 60초`이므로 동작 1회가 정확히 +1 APM이 됩니다.
- 분당 `r`회 속도로 꾸준히 치면 `C`는 `r`로 수렴합니다.
- 입력 시각이나 키 정보는 보관하지 않고, 숫자 `C` 하나만 유지합니다.

</details>

### 조정 값

`APM.cs`의 `ApmEngine` 클래스 상단에서 바꿀 수 있습니다. 바꾼 뒤에는 다시 빌드하세요.

| 상수 | 기본값 | 의미 |
|---|---|---|
| `DecayTauSec` | `60.0` | 기본 감쇠 시간. 줄이면 동작 1회당 상승폭이 커짐 (`30.0` → +2) |
| `IdleGraceSec` | `0.4` | 이 시간 동안 입력이 없으면 "손을 뗐다"고 판단. 잠깐 쉴 때 떨어지는 게 싫으면 `0.8` 정도로 |
| `IdleTauSec` | `0.7` | 손을 뗐을 때 떨어지는 속도. 작을수록 빨리 떨어짐 |

> [!TIP]
> 300 APM으로 계속 쳐도 화면 숫자가 300 근처까지 오르는 데 1분 이상 걸립니다. 더 빨리 오르게 하려면 `DecayTauSec`를 줄이세요.

---

## 🔒 보안 및 개인정보

키보드 입력을 감지하는 프로그램이므로 동작을 투명하게 공개합니다.

| 항목 | 내용 |
|---|---|
| 키 내용 저장 | ❌ 어떤 키를 눌렀는지 **저장하지 않음** |
| 입력 기록 | ❌ 입력 시각도 보관하지 않음. 숫자 하나(APM 카운터)에만 반영 |
| 파일 쓰기 | ❌ 없음 |
| 레지스트리 접근 | ❌ 없음 |
| 네트워크 통신 | ❌ 없음 |
| 연산 위치 | ✅ 로컬 메모리에서만 |

<details>
<summary><b>키 코드는 왜 읽나요?</b></summary>
<br>

키를 꾹 누르고 있으면 Windows가 같은 KeyDown을 반복해서 보냅니다(자동 반복). 이것을 1회로만 세기 위해 **"이 키가 이미 눌려 있는지"** 여부를 확인할 때만 키 코드를 사용하고, 판별 후 바로 버립니다.

</details>

> [!WARNING]
> 키보드 감지(저수준 훅) 구조상 일부 백신이 의심 행위로 오진할 수 있습니다. 사내 환경이라면 소스 코드(`APM.cs`)를 보안 담당자에게 함께 전달해 검토 및 예외 등록을 요청하세요.

---

## 🛠️ 문제 해결

| 증상 | 해결 |
|---|---|
| `csc.exe not found` | "Windows 기능 켜기/끄기"에서 **.NET Framework 4.x**를 켜세요. |
| 빌드 시 파일을 쓸 수 없음 | 이전에 만든 `APM.exe`가 실행 중입니다. 종료 후 다시 빌드하세요. |
| 특정 창에서 입력이 안 세짐 | 관리자 권한으로 실행된 창입니다. `APM.exe`도 **관리자 권한으로 실행**하면 집계됩니다. |
| 두 번 실행해도 하나만 뜸 | 정상입니다. 중복 실행을 막아 두었습니다. |
| 계기판이 화면에서 안 보임 | 화면 밖으로 옮겨졌을 수 있습니다. 작업 관리자에서 종료 후 다시 실행하세요. |
| 일부 전체 화면 앱 위에서 가려짐 | 약 3초마다 최상단을 다시 지정합니다. 독점 전체 화면 게임 위에는 표시되지 않을 수 있습니다. |
| 백신이 실행을 차단 | 위 [보안 및 개인정보](#-보안-및-개인정보)를 참고해 예외 등록을 요청하세요. |

---

## 📁 프로젝트 구조

```text
APM/
├── APM.cs        # 전체 소스 (입력 감지 + APM 계산 + 위젯)
├── build.bat     # 빌드 스크립트 (Windows 내장 csc.exe 사용)
└── README.md
```

<details>
<summary><b>코드 구성</b></summary>
<br>

| 클래스 | 역할 |
|---|---|
| `Program` | 시작 (중복 실행 방지, DPI 설정) |
| `NativeMethods` | Windows API 선언 (입력 훅, 최상단 고정) |
| `ApmEngine` | APM 계산 (지수 감쇠 + 빠른 휴지 감쇠) |
| `ApmForm` | 계기판 창 (입력 훅 설치/해제, 그리기, 드래그 이동, 더블클릭 종료) |

</details>

---

<div align="center">
<sub>Made with ⚡ and C# — 오늘도 엑셀에서 300 APM.</sub>
</div>
