<div align="center">

<!-- 스크린샷을 추가하려면 docs/ 폴더에 이미지를 올리고 아래 주석을 해제하세요 -->
<!-- <img src="docs/banner.png" alt="카메라 파괴소" width="720"> -->

# 💥 카메라 파괴소 (CamSmash)

**캐논 · 소니 · 니콘 · 후지필름 카메라를 소환해서 마음껏 부수는 윈도우 게임**

<p>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows&logoColor=white" alt="Windows 10 | 11">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.x%20(built--in)-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET Framework 4.x">
  <img src="https://img.shields.io/badge/language-C%23%205-239120?style=flat-square&logo=csharp&logoColor=white" alt="C# 5">
  <img src="https://img.shields.io/badge/dependencies-none-brightgreen?style=flat-square" alt="No dependencies">
  <img src="https://img.shields.io/badge/single%20file-.exe-f2c230?style=flat-square" alt="Single exe">
</p>

<p>
  <a href="#-빠른-시작">빠른 시작</a> •
  <a href="#-카메라-소환">카메라 소환</a> •
  <a href="#-도구">도구</a> •
  <a href="#-파괴-시스템">파괴 시스템</a> •
  <a href="#-조작">조작</a> •
  <a href="#-문제-해결">문제 해결</a>
</p>

</div>

---

## ✨ 소개

작업대 위에 카메라를 올려놓고 망치, 원형톱, 화염방사기, 레이저, 폭탄, 블랙홀로 부숴 보세요. 카메라는 **픽셀 단위로 부서지고**, 잘려 나간 조각은 바닥으로 떨어져 튕기며, 렌즈 유리·금속·고무·기판·배터리가 **재질마다 다르게 반응**합니다.

<table>
  <tr>
    <td align="center" width="25%">📷<br><b>64가지 조합</b><br><sub>4개 브랜드<br>바디 4종 × 렌즈 4종</sub></td>
    <td align="center" width="25%">🧰<br><b>도구 8종</b><br><sub>때리고, 자르고<br>태우고, 날리고</sub></td>
    <td align="center" width="25%">🔆<br><b>빛 번짐 효과</b><br><sub>불꽃 · 스파크 · 레이저<br>폭발이 주변을 밝힘</sub></td>
    <td align="center" width="25%">🔊<br><b>실시간 합성 사운드</b><br><sub>동시 재생 · 좌우 위치<br>작업실 울림</sub></td>
  </tr>
</table>

- 별도 런타임, 엔진, 라이브러리 **설치 없이** Windows 기본 컴파일러로 `.exe` 하나를 만듭니다.
- 그래픽은 모든 픽셀을 직접 계산하는 소프트웨어 렌더러로, 소리는 Windows 기본 오디오 장치에 직접 쓰는 신시사이저로 만들었습니다. 이미지·사운드 파일이 하나도 없습니다.
- **슬로모션**으로 폭발과 파편을 천천히 감상할 수 있습니다.

<!-- <p align="center"><img src="docs/demo.gif" alt="데모" width="720"></p> -->

---

## 🚀 빠른 시작

### 요구 사항

| 항목 | 내용 |
|---|---|
| OS | Windows 10 / 11 |
| 런타임 | .NET Framework 4.x (Windows에 기본 포함) |
| 추가 설치 | **없음** (Visual Studio 불필요) |
| 권장 화면 | 1280×720 이상 |

### 빌드 및 실행

```text
CamSmash/
├── CamSmash.cs           ← 소스 코드
└── build_camsmash.bat    ← 빌드 스크립트
```

1. 저장소를 내려받거나 두 파일을 같은 폴더에 둡니다.
2. **`build_camsmash.bat`** 을 더블클릭합니다.
3. `[OK] CamSmash.exe built.` 메시지가 나오면 **`CamSmash.exe`** 를 실행합니다.
4. 처음에는 **Canon EOS R5 + RF 24-70mm F2.8 L** 이 작업대에 올라와 있습니다. 바로 부숴 보세요.

> [!NOTE]
> 빌드 스크립트는 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`(없으면 32비트 경로)를 사용합니다.

---

## 📷 카메라 소환

왼쪽 패널에서 **브랜드 → 바디 → 렌즈**를 고르고 `소환하기`를 누르면 카메라가 작업대 위로 떨어집니다. 바디와 렌즈는 같은 브랜드 안에서 자유롭게 조합할 수 있습니다.

<details open>
<summary><b>Canon</b></summary>
<br>

| 바디 | 형태 | 렌즈 | 특징 |
|---|---|---|---|
| EOS R5 | 미러리스 | RF 24-70mm F2.8 L IS USM | 표준 줌, 빨간 띠 |
| EOS R6 Mark II | 미러리스 | RF 50mm F1.2 L USM | 큰 앞알, 빨간 띠 |
| EOS R3 | 세로그립 일체형 | RF 70-200mm F2.8 L IS USM | 흰색 망원 줌 |
| EOS 5D Mark IV | DSLR | RF 50mm F1.8 STM | 작은 단렌즈 |

</details>

<details>
<summary><b>Sony</b></summary>
<br>

| 바디 | 형태 | 렌즈 | 특징 |
|---|---|---|---|
| α7 IV | 미러리스 | FE 24-70mm F2.8 GM II | 표준 줌 |
| α7R V | 미러리스 | FE 85mm F1.4 GM | 큰 앞알 |
| α1 II | 미러리스 | FE 70-200mm F2.8 GM OSS II | 흰색 망원 줌, 삼각대 받침 |
| α6700 | 납작한 상판(APS-C) | FE 50mm F1.8 | 작은 단렌즈 |

</details>

<details>
<summary><b>Nikon</b></summary>
<br>

| 바디 | 형태 | 렌즈 | 특징 |
|---|---|---|---|
| Z8 | 미러리스 | NIKKOR Z 24-70mm f/2.8 S | 표준 줌 |
| Z9 | 세로그립 일체형 | NIKKOR Z 50mm f/1.2 S | 아주 큰 앞알 |
| Zf | 레트로 (다이얼) | NIKKOR Z 70-200mm f/2.8 VR S | 망원 줌, 삼각대 받침 |
| D850 | DSLR | NIKKOR Z 40mm f/2 | 작은 단렌즈 |

</details>

<details>
<summary><b>Fujifilm</b></summary>
<br>

| 바디 | 형태 | 렌즈 | 특징 |
|---|---|---|---|
| X-T5 | 레트로 (은색 상판, 다이얼) | XF 16-55mm F2.8 R LM WR | 표준 줌 |
| X-H2S | 미러리스 | XF 56mm F1.2 R WR | 큰 앞알 |
| X-Pro3 | 레인지파인더형 | XF 50-140mm F2.8 R LM OIS WR | 망원 줌, 삼각대 받침 |
| GFX100S II | 중형 | XF 35mm F2 R WR | 은색 소형 단렌즈 |

</details>

> [!NOTE]
> 브랜드와 모델명은 어떤 카메라인지 알려 주는 표기일 뿐이며 각 제조사와 관계가 없습니다. 카메라 그림은 실제 디자인을 옮긴 것이 아니라 단순화한 픽셀 일러스트이고, 로고는 그리지 않았습니다. 가격은 게임용 대략값입니다.

---

## 🧰 도구

| 키 | 도구 | 사용법 | 효과 |
|:-:|---|---|---|
| <kbd>1</kbd> | **망치** | 클릭 | 내려친 곳이 부서지고 금이 퍼짐. 유리는 넓게 깨짐 |
| <kbd>2</kbd> | **드릴** | 누르고 있기 | 그 자리에 구멍. 금속·기판에서 스파크 |
| <kbd>3</kbd> | **원형톱** | 드래그 | 지나간 자리를 잘라냄. 끝까지 자르면 **두 동강** |
| <kbd>4</kbd> | **화염방사기** | 누르고 있기 | 플라스틱·고무가 그을리고 타며 불이 번짐 |
| <kbd>5</kbd> | **레이저** | 드래그 | 녹이면서 자름. 금속이 빨갛게 달아오름 |
| <kbd>6</kbd> | **폭탄** | 클릭 | 붙인 곳에서 1.5초 뒤 폭발. 조각이 사방으로 |
| <kbd>7</kbd> | **블랙홀** | 클릭 | 4.5초 동안 주변 파편과 조각을 빨아들임 |
| <kbd>8</kbd> | **손** | 드래그 | 조각을 잡아 던짐. **카메라 본체도 들어 올릴 수 있음** |

> [!TIP]
> 손으로 카메라를 높이 들었다가 바닥에 내던지면 충격으로 부서집니다. 잘라 낸 조각도 세게 던지면 또 깨집니다.

---

## 💥 파괴 시스템

<details open>
<summary><b>재질별 반응</b></summary>
<br>

| 재질 | 어디에 | 반응 |
|---|---|---|
| 플라스틱 | 바디 껍데기, 렌즈 경통 | 잘 깨지고 잘 탐 |
| 금속 | 마운트, 다이얼, 은색 상판, 삼각대 받침 | 단단함. 때리면 스파크, 레이저로 녹음 |
| 고무 | 그립, 줌·초점 링 | 질기고 천천히 탐 |
| 가죽 | 레트로 바디 아래쪽 | 잘리고 탐 |
| 유리 | 렌즈 앞알, 광학 뷰파인더 창 | 아주 약함. 망치에 넓게 깨지며 반짝이는 파편 |
| 기판 | 껍데기 안쪽 | 껍데기를 벗기면 드러남. 스파크가 튐 |
| 이미지 센서 | 렌즈 유리 안쪽 | 렌즈를 깨면 무지갯빛 센서가 드러남 |
| 배터리 | 그립 안쪽 | 건드리거나 뜨거워지면 연기가 나다가 **폭발** |

</details>

<details>
<summary><b>조각과 물리</b></summary>
<br>

- 카메라는 작업대에 고정된 상태로 시작합니다. 바닥에 닿은 부분이 모두 부서지거나 손으로 들어 올리면 고정이 풀려 떨어집니다.
- 잘려서 떨어져 나간 덩어리는 각자 무게와 회전을 가진 조각이 되어 작업대와 바닥에 부딪히고 튕깁니다.
- 아주 작은 덩어리는 파편 가루가 되어 흩어집니다.
- 불은 탈 수 있는 재질을 따라 조금씩 번지다가 잦아듭니다.

</details>

### 계측판

| 항목 | 설명 |
|---|---|
| 파괴율 | 카메라 전체 중 부서진 비율 |
| 이번 피해액 | 조합 가격 × 파괴 정도. 파괴율 55%에서 전액 |
| 상태 | 멀쩡함 → 흠집 → 파손 → 대파 → **수리 불가** |
| 누적 피해액 | 지금까지 소환한 모든 카메라의 피해액 합계 |
| 희생된 카메라 | 5% 이상 부순 카메라 수 |

---

## 🎮 조작

| 입력 | 기능 |
|---|---|
| <kbd>좌클릭</kbd> | 선택한 도구 사용 |
| <kbd>1</kbd> ~ <kbd>8</kbd> | 도구 바꾸기 (숫자 패드도 가능) |
| <kbd>R</kbd> | 같은 조합으로 다시 소환 |
| <kbd>C</kbd> | 잔해 치우기 (가장 큰 덩어리만 남김) |
| <kbd>S</kbd> | 슬로모션 켜기/끄기 (소리도 함께 느려짐) |
| <kbd>M</kbd> | 소리 켜기/끄기 |
| <kbd>F11</kbd> | 전체화면 (<kbd>Esc</kbd>로 해제) |

`잔해 치우기`, `슬로모션`, `소리` 는 화면 오른쪽 위 버튼으로도 할 수 있습니다.

---

## 🔒 보안 및 개인정보

- 네트워크 통신, 레지스트리 접근, 파일 쓰기 코드가 **없습니다.**
- 키보드·마우스는 게임 창 안에서만 처리합니다. 전역 입력 감지는 하지 않습니다.
- 소스 코드(`CamSmash.cs`) 한 파일로 전체 동작을 확인할 수 있습니다.

---

## 🛠️ 문제 해결

| 증상 | 해결 |
|---|---|
| `csc.exe not found` | "Windows 기능 켜기/끄기"에서 **.NET Framework 4.x**를 켜세요. |
| 빌드 시 파일을 쓸 수 없음 | 이전에 만든 `CamSmash.exe`가 실행 중입니다. 종료 후 다시 빌드하세요. |
| 한글이 깨짐 | `CamSmash.cs`가 **UTF-8 (BOM 포함)** 으로 저장되어 있는지 확인하세요. |
| 소리가 안 남 | 출력 장치가 연결되어 있는지, <kbd>M</kbd>으로 꺼 두지 않았는지 확인하세요. 오디오 장치가 없어도 게임은 실행됩니다. |
| 느려짐 | <kbd>C</kbd>로 잔해를 치우세요. 블랙홀이나 폭탄을 여러 개 동시에 쓰면 파편이 많아집니다. |
| 아주 큰 화면에서 글자가 거칠어 보임 | 4K 등 큰 화면에서는 속도를 위해 절반 해상도로 그린 뒤 확대합니다. 창 크기를 줄이면 선명해집니다. |

---

## 📁 프로젝트 구조

```text
CamSmash/
├── CamSmash.cs           # 전체 소스
├── build_camsmash.bat    # 빌드 스크립트 (Windows 내장 csc.exe 사용)
└── README.md
```

<details>
<summary><b>코드 구성</b></summary>
<br>

| 클래스 | 역할 |
|---|---|
| `Catalog` | 브랜드·바디·렌즈 데이터, 도구 목록 |
| `MatInfo` | 재질별 체력, 도구별 저항, 연소 속도 |
| `Builder`, `Raster`, `Paint` | 카메라 앞모습을 픽셀 격자에 그려 넣기 (안쪽 기판·배터리·센서 층 포함) |
| `Body` | 픽셀 격자 + 강체 (무게중심, 관성, 볼록 껍질 충돌) |
| `Game` | 파괴, 열과 불, 조각 분리, 물리, 파티클, 도구, 무대 그리기 |
| `Fb` | 프레임버퍼 그리기 함수와 빛 번짐(블룸) 처리 |
| `Audio`, `Voice`, `Biquad` | waveOut 스트리밍, 실시간 합성, 필터, 울림 |
| `MainForm` | 창, 게임 루프, 화면 출력, UI, 입력 |

</details>

---

<div align="center">
<sub>Made with 💥 and C# — 실제 카메라는 소중히 다뤄 주세요.</sub>
</div>
